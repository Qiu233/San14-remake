using System.Collections.Immutable;
using Godot;
using San14.Core;

/// <summary>One execution context's three source slots. Native handles may
/// finish independently, but their invocation slots remain occupied.</summary>
internal sealed class StoryOwnedEffectHost
{
    private sealed record Entry(StoryOwnedEffectSlot Description, UniqueTacticEffectHandle? Handle, object? MapKey = null);
    private readonly Entry?[] slots = new Entry?[3];
    private readonly object owner;
    private readonly long sequence;
    private readonly Control output;
    private readonly Func<UniqueTacticEffectHost> createBackend;
    private readonly Func<UniqueTacticEffectHost, double, int, Task> advanceBackend;
    private readonly Action<UniqueTacticEffectHost> forgetBackend;
    private readonly Func<int> framesPerSecond;
    private readonly Func<object, int, CancellationToken, Task<CaptureEffectAdmission>> createMapEffect;
    private readonly Func<Task> retireMapEffects;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim operations = new(1, 1);
    private UniqueTacticEffectHost? backend;
    private Task advancing = Task.CompletedTask;
    private Task<StoryOwnedEffectResult>? restoration;
    private Task<StoryOwnedEffectResult>? operation;
    private Task? retirement;
    private int operationStep = -1;
    private bool restored, disposed, stopping;
    private double accumulatedSeconds;
    private Exception? failure;

    internal UniqueTacticEffectHost? Backend => backend;

    internal StoryOwnedEffectHost(object owner, Control output, long sequence,
        Func<UniqueTacticEffectHost> createBackend,
        Func<UniqueTacticEffectHost, double, int, Task> advanceBackend,
        Action<UniqueTacticEffectHost> forgetBackend, Func<int> framesPerSecond,
        Func<object, int, CancellationToken, Task<CaptureEffectAdmission>> createMapEffect, Func<Task> retireMapEffects)
    {
        this.owner = owner; this.output = output; this.sequence = sequence;
        this.createBackend = createBackend; this.advanceBackend = advanceBackend;
        this.forgetBackend = forgetBackend;
        this.framesPerSecond = framesPerSecond;
        this.createMapEffect = createMapEffect; this.retireMapEffects = retireMapEffects;
    }

    internal bool Matches(Control canvas, CampaignStoryRequest story) =>
        !disposed && ReferenceEquals(canvas, output) && story.Sequence == sequence;

    private ImmutableArray<StoryOwnedEffectSlot> Snapshot() =>
        slots.Where(entry => entry is not null).Select(entry => entry!.Description).ToImmutableArray();

    private StoryOwnedEffectResult Ready() => new(new(Snapshot()), null);
    private static StoryOwnedEffectResult Missing(string owner) => new(null, owner);

    internal Task<StoryOwnedEffectResult> RestoreAsync(CampaignStoryRequest story)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (restored)
            return Task.FromResult(story.Scene.OwnedEffects is { } actual && actual.SequenceEqual(Snapshot())
                ? Ready() : Missing("story_effect.scene_slot_snapshot_changed"));
        return restoration ??= RestoreCoreAsync(story);
    }

    private async Task<StoryOwnedEffectResult> RestoreCoreAsync(CampaignStoryRequest story)
    {
        await operations.WaitAsync(lifetime.Token);
        try
        {
            if (story.Scene.OwnedEffects is not { } saved)
                return Missing("story_effect.legacy_owned_slots_unknown");

            // Empty new-context knowledge needs neither a renderer nor a GPU.
            if (saved.IsEmpty) { restored = true; return Ready(); }
            foreach (var slot in saved)
            {
                if (slot.SelectorMode == 1)
                {
                    var key = new object();
                    var admission = await createMapEffect(key, slot.EffectId, lifetime.Token);
                    if (!admission.Completed || !admission.SourceAllocated)
                        return Missing(admission.UnresolvedInput ?? $"story_effect.restore_slot_{slot.Slot}_actual_allocation_rejected");
                    slots[slot.Slot] = new(slot, null, key); continue;
                }
                var actual = backend ??= createBackend();
                var handle = actual.TryCreate(slot.EffectId, 2, owner, lifetime.Token);
                if (handle is null)
                    return Missing($"story_effect.restore_slot_{slot.Slot}_actual_allocation_rejected");
                slots[slot.Slot] = new(slot, handle);
            }
            StartAdvance(0);
            await Task.WhenAll(slots.Where(entry => entry?.Handle is not null)
                .Select(entry => entry!.Handle!.StoryAcceptance!.Task)).WaitAsync(lifetime.Token);
            restored = true; return Ready();
        }
        catch (OperationCanceledException) when (!disposed)
        { return Missing("story_effect.restore_instance_retired_before_acceptance"); }
        catch (Exception error) when (error is not OperationCanceledException)
        { return Missing(error.Message); }
        finally { operations.Release(); }
    }

    internal Task<StoryOwnedEffectResult> ExecuteAsync(CampaignStoryRequest story)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (operation is not null && operationStep == story.NextStep) return operation;
        operationStep = story.NextStep;
        return operation = ExecuteCoreAsync(story);
    }

    private async Task<StoryOwnedEffectResult> ExecuteCoreAsync(CampaignStoryRequest story)
    {
        if ((uint)story.NextStep >= story.Steps.Length || story.Steps[story.NextStep] is not StoryOwnedEffect effect)
            return Missing("story_effect.waiting_operation");
        var restore = await RestoreAsync(story);
        if (restore.Receipt is null) return restore;
        await operations.WaitAsync(lifetime.Token);
        try
        {
            if (effect.Operation == StoryOwnedEffectOperation.StopAll)
            {
                stopping = true;
                await advancing;
                if (backend is { } actual)
                {
                    actual.RetireOwnedImmediately(owner);
                    // Detach the actual output immediately, drain its native
                    // work, then release this context's renderer. A subsequent
                    // create gets a new real surface on the same source pane.
                    await actual.RetireDetachedAsync();
                    forgetBackend(actual); backend = null;
                }
                await retireMapEffects();
                Array.Clear(slots); failure = null; return Ready();
            }
            if (failure is not null) return Missing(failure.Message);
            int index = Array.FindIndex(slots, entry => entry is null);
            // Source3E27C0 does not call the creator when all three pointers exist.
            if (index < 0) return Ready();
            if (effect.SelectorMode == 1)
            {
                var key = new object();
                var admission = await createMapEffect(key, effect.EffectId, lifetime.Token);
                if (!admission.Completed) return Missing(admission.UnresolvedInput ?? "story_effect.mode1_native_admission");
                if (admission.SourceAllocated) slots[index] = new(new(index, effect.EffectId, 1), null, key);
                return Ready();
            }
            var renderer = backend ??= createBackend();
            var handle = renderer.TryCreate(effect.EffectId, 2, owner, lifetime.Token);
            // This is the actual shared manager's rejected allocation, not a
            // renderer failure. Source writes its null result back to the slot.
            if (handle is null) return Ready();
            slots[index] = new(new(index, effect.EffectId, effect.SelectorMode), handle);
            StartAdvance(0);
            await handle.StoryAcceptance!.Task.WaitAsync(lifetime.Token);
            return Ready();
        }
        catch (OperationCanceledException) when (!disposed)
        { return Missing("story_effect.instance_retired_before_acceptance"); }
        catch (Exception error) when (error is not OperationCanceledException)
        { return Missing(error.Message); }
        finally { stopping = false; operations.Release(); }
    }

    // Called only by Map's actual accepted animation pacer, not an independent
    // timer or a fixed observation frame. A slow native submit never queues an
    // unbounded list of frame tasks; elapsed time is retained for the next tick.
    internal void Advance(double seconds)
    {
        if (disposed || stopping || backend is null || failure is not null) return;
        accumulatedSeconds += seconds;
        if (!advancing.IsCompleted) return;
        var elapsed = accumulatedSeconds; accumulatedSeconds = 0;
        StartAdvance(elapsed);
    }

    private void StartAdvance(double seconds)
    {
        if (disposed || backend is null || !advancing.IsCompleted) return;
        advancing = AdvanceCoreAsync(seconds);
    }

    private async Task AdvanceCoreAsync(double seconds)
    {
        try { await advanceBackend(backend!, seconds, framesPerSecond()); }
        catch (Exception error)
        {
            failure = error;
            foreach (var entry in slots)
                entry?.Handle?.StoryAcceptance?.TrySetException(error);
        }
    }

    // Deliberately not an async method: every dependent Canvas/Surface is
    // detached before this returns a Task and the caller frees its view.
    internal Task RetireAsync()
    {
        if (retirement is not null) return retirement;
        disposed = true; lifetime.Cancel();
        retirement = Task.WhenAll(backend?.RetireDetachedAsync() ?? Task.CompletedTask, retireMapEffects());
        return retirement;
    }
}
