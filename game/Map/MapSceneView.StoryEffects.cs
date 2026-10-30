using Godot;
using San14.Core;

internal sealed record StoryOwnedEffectResult(StoryOwnedEffectReceipt? Receipt, string? UnresolvedOwner);

public partial class MapSceneView
{
    private readonly Dictionary<object, StoryOwnedEffectHost> storyEffectHosts = new(ReferenceEqualityComparer.Instance);
    private readonly List<Task> storyEffectRetirements = [];
    private double storyEffectSeconds;

    private StoryOwnedEffectHost StoryEffectHost(object owner, Control output, CampaignStoryRequest story)
    {
        if (!ReferenceEquals(storyPresentationOwner, owner))
            throw new InvalidOperationException("story_effect.presentation_owner");
        if (storyEffectHosts.TryGetValue(owner, out var host))
        {
            if (!host.Matches(output, story)) throw new InvalidOperationException("story_effect.invocation_owner_changed");
            return host;
        }
        host = new(owner, output, story.Sequence,
            () => CreateUniqueTacticEffectHost(output, output, _ => byte.MaxValue, singleBackLayer: true),
            AdvanceUniqueTacticEffects,
            backend => _uniqueEffectHosts.Remove(backend),
            () => _d3d11AnimationPacer is { } pacer ? pacer.FramesPerSecond(pacer.FpsMode) :
                throw new InvalidOperationException("story_effect.actual_animation_pacer"),
            (key, kind, token) => CreateStoryMapEffect(owner, key, kind, token), () => RetireStoryMapEffectsAsync(owner));
        storyEffectHosts.Add(owner, host); return host;
    }

    internal async Task<StoryOwnedEffectResult> ExecuteStoryEffectAsync(object owner, Control output,
        CampaignStoryRequest story, CancellationToken token)
    {
        try { return await StoryEffectHost(owner, output, story).ExecuteAsync(story).WaitAsync(token); }
        catch (Exception error) when (error is not OperationCanceledException) { return new(null, error.Message); }
    }

    internal async Task<StoryOwnedEffectResult> RestoreStoryEffectsAsync(object owner, Control output,
        CampaignStoryRequest story, CancellationToken token)
    {
        try { return await StoryEffectHost(owner, output, story).RestoreAsync(story).WaitAsync(token); }
        catch (Exception error) when (error is not OperationCanceledException) { return new(null, error.Message); }
    }

    // Synchronous front half is required by StoryPlaybackHost.Abandon: its
    // parent may QueueFree immediately after calling this method.
    internal Task RetireStoryEffectsAsync(object owner)
    {
        if (!storyEffectHosts.Remove(owner, out var host)) return Task.CompletedTask;
        var retirement = host.RetireAsync();
        if (host.Backend is { } backend) _uniqueEffectHosts.Remove(backend);
        storyEffectRetirements.RemoveAll(task => task.IsCompletedSuccessfully);
        storyEffectRetirements.Add(retirement);
        return retirement;
    }

    private Task RetireAllStoryEffectsAsync()
    {
        RetireStoryEffectCommonCamera();
        var owners = storyEffectHosts.Keys.ToArray();
        // All surfaces detach before returning the aggregate drain task.
        foreach (var owner in owners) RetireStoryEffectsAsync(owner);
        // An abandoned view can already be gone while its GPU fence still
        // drains. Map context teardown must join those earlier retirements too.
        return Task.WhenAll(storyEffectRetirements.ToArray());
    }

    private void AdvanceStoryEffectClock(double seconds, bool due)
    {
        if (storyEffectHosts.Count == 0) { storyEffectSeconds = 0; return; }
        storyEffectSeconds += seconds;
        if (!due) return;
        var elapsed = storyEffectSeconds; storyEffectSeconds = 0;
        foreach (var host in storyEffectHosts.Values) host.Advance(elapsed);
    }
}
