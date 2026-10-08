using San14.Data;

public sealed record OriginalWorldGeometryStageSecondBinding(
    OriginalWorldGeometryTargetPopSubmission AfterArmy52, OriginalWorldGeometryTargetBinding? Ordinary53);

/// <summary>Normal World target lifetime over the scene's actual allocations.
/// Entry accepts the backend's actual display row; explicit diagnostic callers
/// may still select context.OutputFrame. Main.v20 is the renderer lock gate.
/// Source metadata is already loaded. Frames/copy storage survive completed
/// requests and retire only after their borrowing draw owners on resize/dispose.</summary>
public sealed class OriginalWorldGeometryStageLifetimeOwner : IAsyncDisposable
{
    private readonly LoadedOriginalWorldGeometryStageSource source;
    private readonly LoadedOriginalOrdinaryRs4Source? rs4Source;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object disposalGate = new();
    private Generation? generation;
    private Invocation? current;
    private bool faulted, retiring, disposed;
    private Task? disposal;
    private string entrySourceEvidence="explicit diagnostic entry/context.OutputFrame;not-original-display-surface";

    public OriginalWorldGeometryStageLifetimeOwner(LoadedOriginalWorldGeometryStageSource loadedSource,
        LoadedOriginalOrdinaryRs4Source? actualRs4Source = null)
    { ArgumentNullException.ThrowIfNull(loadedSource); source = loadedSource; rs4Source = actualRs4Source; }

    public LoadedOriginalWorldGeometryStageSource Source => source;
    public OriginalWorldGeometryTargetFrames SharedFrames => generation?.Frames ??
        throw new InvalidOperationException("World geometry target generation has not been created.");
    public string EntrySourceEvidence=>entrySourceEvidence;

    /// <summary>Called in the scene's serial frame scope after current source
    /// preparation. Domains contain actual current wrapper/setter values.
    /// readPostBindDomain selects the actual48/53 wrapper domain. The first
    /// callback drains this invocation's read consumers; the second retires
    /// cached borrowing draw owners only at target-generation retirement.
    /// Callbacks must not re-enter this owner or the scene's serial gate.</summary>
    public async Task BeginActualAsync(OriginalMapSourceCreatedNativeSessionPreparedFrame actualFrame,
        Func<OriginalWorldGeometryCurrentCallerFields> currentQGetter,
        OriginalMapSourceCreatedWorldPlayableHost actualHost, Action requireCurrent,
        OriginalWorldGeometryDrawDomain actualEntryDomain,
        Func<int, OriginalWorldGeometryDrawDomain> readPostBindDomain,
        Func<Task> actualConsumersDrained,
        Func<OriginalNativeMapTargetContext, Task> retireBorrowedDrawOwners,
        OriginalWorldGeometryTargetRow? actualEntryRow=null)
    {
        ArgumentNullException.ThrowIfNull(actualFrame); ArgumentNullException.ThrowIfNull(currentQGetter);
        ArgumentNullException.ThrowIfNull(actualHost); ArgumentNullException.ThrowIfNull(requireCurrent);
        ArgumentNullException.ThrowIfNull(actualEntryDomain); ArgumentNullException.ThrowIfNull(readPostBindDomain);
        ArgumentNullException.ThrowIfNull(actualConsumersDrained); ArgumentNullException.ThrowIfNull(retireBorrowedDrawOwners);
        await gate.WaitAsync();
        try
        {
            RequireAccepting();
            if (current is not null) throw new InvalidOperationException("Previous World geometry consumers have not ended their invocation.");
            requireCurrent(); actualEntryDomain.Validate();
            var context = actualFrame.Context;
            if (!ReferenceEquals(actualHost.Scene.TerrainFrame, context.TerrainFrame) ||
                !ReferenceEquals(actualHost.Scene.OutputFrame, context.OutputFrame) ||
                !ReferenceEquals(actualHost.Session.CurrentFrame, actualFrame) ||
                actualFrame.MainCamera.ExecutableLocale != source.Native.Locale)
                throw new InvalidDataException("World geometry entry requires the actual current Host/session/target generation.");
            if (generation is { } previous && (!ReferenceEquals(previous.Targets.TerrainFrame, context.TerrainFrame) ||
                !ReferenceEquals(previous.Targets.OutputFrame, context.OutputFrame)))
                await RetireGenerationCoreAsync();
            if (generation is null)
            {
                var frames = await OriginalWorldGeometryTargetFrames.BuildAsync(context.TerrainFrame, rs4Source);
                OriginalWorldGeometryTargetScopeOwner? scope = null;
                try
                {
                    scope = await OriginalWorldGeometryTargetScopeOwner.BuildAsync(source, frames);
                    generation = new(frames, scope, new(context.TerrainFrame, context.OutputFrame), retireBorrowedDrawOwners);
                }
                catch
                {
                    if (scope is not null) await scope.DisposeAsync();
                    await frames.DisposeAsync(); throw;
                }
            }
            // RenderingDevice draws select a framebuffer on their PSO/draw
            // list. This is the explicit backend's actual row selection, not
            // an empty draw, clear, imported native pointer or source73 claim.
            var prior = actualEntryRow??new OriginalWorldGeometryTargetRow(context.OutputFrame, 0,
                actualEntryDomain with { SourceEvidence = actualEntryDomain.SourceEvidence +
                    ";explicit diagnostic entry/context.OutputFrame;not-original-display-surface" });
            prior.RequireCurrent();
            entrySourceEvidence=prior.Domain.SourceEvidence;
            generation.Scope.BeginCurrent(actualFrame, prior, currentQGetter, requireCurrent);
            current = new(currentQGetter, readPostBindDomain, actualConsumersDrained, requireCurrent);
        }
        finally { gate.Release(); }
    }

    /// <summary>Select the actual seven-MRT source43 prefix when this
    /// generation was constructed with its loaded source. This performs no
    /// queue work, clear or RS4 initialization.</summary>
    public async Task<OriginalWorldGeometryTargetBinding?> Bind43Async()
    {
        await gate.WaitAsync();
        try
        {
            var invocation = RequireCurrent(Phase.Entry);
            if (rs4Source is null) throw new InvalidOperationException("This geometry lifetime has no loaded RS4 prefix.");
            var value = await generation!.Scope.BindAsync(43, ReadCurrentBindDomain(invocation, 43));
            invocation.Rs4Binding = value; invocation.Phase = Phase.Rs4;
            return value;
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    /// <summary>Await the real44/45/46 caller, then perform source47's
    /// no-copy pop before48. A null interface never replaces that caller.</summary>
    public async Task<OriginalWorldGeometryTargetPopSubmission> Pop47AfterCallerAsync(
        Func<OriginalWorldGeometryTargetBinding?, Task> actual44Through46Caller)
    {
        ArgumentNullException.ThrowIfNull(actual44Through46Caller);
        await gate.WaitAsync();
        try
        {
            var invocation = RequireCurrent(Phase.Rs4);
            await actual44Through46Caller(invocation.Rs4Binding);
            invocation.RequireCurrent();
            var pop = await generation!.Scope.PopAsync(47);
            invocation.Rs4Binding = null; invocation.Phase = Phase.Entry;
            return pop;
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    public async Task<OriginalWorldGeometryTargetBinding?> Bind48Async()
    {
        await gate.WaitAsync();
        try
        {
            var invocation = RequireCurrent(Phase.Entry);
            var value = await generation!.Scope.BindAsync(48, ReadCurrentBindDomain(invocation, 48));
            invocation.Army48 = value; invocation.Phase = Phase.Army48;
            return value;
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    /// <summary>The real48 binding for its draw owners. Actual source no-bind
    /// returns null from Bind48Async and cannot manufacture an Army target.</summary>
    public OriginalArmy48TargetBinding GetArmyTarget()
    {
        var invocation = RequireCurrent(Phase.Army48);
        return (invocation.Army48 ?? throw new InvalidDataException("Source48 gate produced no actual Army target."))
            .CaptureArmy48Binding();
    }

    /// <summary>Invoke only after the real49/50 and GeneralScene51 caller
    /// returned. Their draw/source receipts remain with those owners.</summary>
    public async Task<OriginalWorldGeometryStageSecondBinding> Pop52AndBind53Async()
    {
        await gate.WaitAsync();
        try
        {
            var invocation = RequireCurrent(Phase.Army48);
            var pop = await generation!.Scope.PopAsync(52);
            var ordinary = await generation.Scope.BindAsync(53, ReadCurrentBindDomain(invocation, 53));
            invocation.Ordinary53 = ordinary; invocation.Phase = Phase.Ordinary53;
            return new(pop, ordinary);
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    public OriginalWorldGeometryTargetBinding? GetOrdinaryTarget() => RequireCurrent(Phase.Ordinary53).Ordinary53;

    /// <summary>The ordered caller executes its actual57/58 owners here after
    /// static54/55 and GeneralScene56. This awaits that real call before59;
    /// no caller completion boolean or fabricated geometry receipt is issued.
    /// A source null shader still executes the caller's original state tails.</summary>
    public async Task<OriginalWorldGeometryTargetPopSubmission> Pop59AfterCallerAsync(
        Func<OriginalWorldGeometryTargetBinding?, Task> actual57And58Caller)
    {
        ArgumentNullException.ThrowIfNull(actual57And58Caller);
        await gate.WaitAsync();
        try
        {
            var invocation = RequireCurrent(Phase.Ordinary53);
            await actual57And58Caller(invocation.Ordinary53);
            invocation.RequireCurrent();
            var pop = await generation!.Scope.PopAsync(59);
            invocation.Phase = Phase.Final59; return pop;
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    /// <summary>Only the scope's actual59 TextureCopy publication; it remains
    /// live through Early/late/post consumption until this invocation ends.</summary>
    public OriginalWorldGeometryDepthSamplingView RequireFinalSampling()
    {
        _ = RequireCurrent(Phase.Final59);
        return generation!.Scope.RequireFinal59SamplingView();
    }

    /// <summary>Drain only this invocation's actual read leases/consumers.
    /// Cached draw objects, the shared48 frame and independent copy storage
    /// remain allocated for the next request using this target generation.</summary>
    public async Task EndCurrentAsync()
    {
        await gate.WaitAsync();
        try
        {
            var invocation = RequireCurrent(Phase.Final59);
            await invocation.ConsumersDrained();
            generation!.Scope.EndCurrent(); current = null;
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    /// <summary>Use from BeforeTargetRetirement while the scene's ordinary
    /// storage/output are still live. First drain current consumers, then
    /// retire borrowing draw owners, the copy scope and finally shared48 views.
    /// Ordinary53/storage/output stay owned by the scene.</summary>
    public async Task RetireTargetGenerationAsync(OriginalNativeMapTargetContext actualTargets)
    {
        ArgumentNullException.ThrowIfNull(actualTargets);
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (generation is not { } value) { faulted = false; return; }
            if (!ReferenceEquals(value.Targets.TerrainFrame, actualTargets.TerrainFrame) ||
                !ReferenceEquals(value.Targets.OutputFrame, actualTargets.OutputFrame))
                throw new InvalidDataException("Target retirement belongs to another actual scene generation.");
            await RetireGenerationCoreAsync(); faulted = false;
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    private async Task RetireGenerationCoreAsync()
    {
        var value = generation;
        if (value is null) return;
        if (current is { } invocation)
        {
            await invocation.ConsumersDrained();
            // A failed/partial source scope is retired, not called complete.
            current = null;
        }
        if (!value.BorrowersRetired)
        {
            await value.RetireBorrowers(value.Targets); value.BorrowersRetired = true;
        }
        if (!value.ScopeRetired)
        {
            await value.Scope.DisposeAsync(); value.ScopeRetired = true;
        }
        await value.Frames.DisposeAsync(); generation = null;
    }

    private Invocation RequireCurrent(Phase expected)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (faulted) throw new InvalidOperationException("Failed World geometry invocation must retire before reuse.");
        var value = current ?? throw new InvalidOperationException("World geometry has no current invocation.");
        value.RequireCurrent();
            if (value.Phase != expected) throw new InvalidOperationException("World geometry operation is out of its actual loaded source order.");
        return value;
    }
    private static OriginalWorldGeometryDrawDomain? ReadCurrentBindDomain(Invocation invocation, int node)
    {
        var fields = invocation.CurrentQGetter();
        return fields.CurrentContextFlags3F0 < 0 || !fields.CurrentTablePresent
            ? null : invocation.ReadPostBindDomain(node);
    }
    private void RequireAccepting()
    {
        ObjectDisposedException.ThrowIf(retiring || disposed, this);
        if (faulted) throw new InvalidOperationException("Failed World geometry invocation must retire before reuse.");
    }
    public ValueTask DisposeAsync()
    {
        lock (disposalGate)
        {
            retiring = true;
            if (disposal is null || disposal.IsFaulted || disposal.IsCanceled) disposal = DisposeCoreAsync();
            return new(disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (disposed) return;
            await RetireGenerationCoreAsync(); disposed = true;
        }
        finally { gate.Release(); }
    }
    private enum Phase { Entry, Rs4, Army48, Ordinary53, Final59 }
    private sealed class Invocation(Func<OriginalWorldGeometryCurrentCallerFields> currentQGetter,
        Func<int, OriginalWorldGeometryDrawDomain> readPostBindDomain, Func<Task> drained, Action requireCurrent)
    {
        public Func<OriginalWorldGeometryCurrentCallerFields> CurrentQGetter { get; } = currentQGetter;
        public Func<int, OriginalWorldGeometryDrawDomain> ReadPostBindDomain { get; } = readPostBindDomain;
        public Func<Task> ConsumersDrained { get; } = drained;
        public Action RequireCurrent { get; } = requireCurrent;
        public Phase Phase { get; set; }
        public OriginalWorldGeometryTargetBinding? Rs4Binding { get; set; }
        public OriginalWorldGeometryTargetBinding? Army48 { get; set; }
        public OriginalWorldGeometryTargetBinding? Ordinary53 { get; set; }
    }
    private sealed class Generation(OriginalWorldGeometryTargetFrames frames, OriginalWorldGeometryTargetScopeOwner scope,
        OriginalNativeMapTargetContext targets, Func<OriginalNativeMapTargetContext, Task> retireBorrowers)
    {
        public OriginalWorldGeometryTargetFrames Frames { get; } = frames;
        public OriginalWorldGeometryTargetScopeOwner Scope { get; } = scope;
        public OriginalNativeMapTargetContext Targets { get; } = targets;
        public Func<OriginalNativeMapTargetContext, Task> RetireBorrowers { get; } = retireBorrowers;
        public bool BorrowersRetired { get; set; }
        public bool ScopeRetired { get; set; }
    }
}
