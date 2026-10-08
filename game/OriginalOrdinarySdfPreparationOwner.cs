using San14.Data;

/// <summary>The actual attached operation0 owns four buffers and its source
/// ring state. Counts are unavailable until their actual producer has written
/// them; fresh allocation is not used as a count0 proof.</summary>
public interface IOriginalOrdinarySdfOperation0
{
    OriginalOrdinarySdfOperation0Counts CaptureCurrentCounts();
    bool ApplyCurrentWorldPopulation(object actualSelectedViewOperation0Bank);
}

public interface IOriginalOrdinarySdfOperation1
{
    uint CurrentCount38 { get; }
}

public readonly record struct OriginalOrdinarySdfPreparationCurrent(
    bool CurrentTablePresent, uint Host7F8, uint? CommonFlags10,
    object ActualCurrentTable, object ActualSelectedView, object ActualOcWrapper,
    ushort OcWidth40, ushort OcHeight42);

/// <summary>The real retained D7C6E0 path returns the actual old-image writers.
/// Both references may be null only when the corresponding source field is
/// actually null. This result follows real preparation/population/transport.</summary>
public sealed record OriginalOrdinarySdfOperation1Returned(bool Alpha,
    object? PreviousImage30, object? PreviousImage38);

/// <summary>Backend boundary for original manager/operations and active op1
/// methods. It must consume current View1C58 buffers/metadata/atlas and the real
/// prior images; the old standalone diagnostic owner does not implement this.</summary>
public interface IOriginalOrdinarySdfPreparationBackend
{
    object? ConstructManager();
    IOriginalOrdinarySdfOperation0? ConstructAndAttachOperation0(object manager,
        uint sphereCapacity, uint dualSphereCapacity);
    IOriginalOrdinarySdfOperation1? ConstructAndAttachOperation1(object manager);
    OriginalOrdinarySdfOperation1Returned ApplyRetainedOperation1(
        IOriginalOrdinarySdfOperation1 operation,
        OriginalOrdinarySdfPreparationCurrent actualCurrent,
        uint actualSweepSide, object? actualPreviousImage30, object? actualPreviousImage38);
}

public readonly record struct OriginalOrdinarySdfPreparationReturned(
    bool Operation0Alpha, bool Operation1Alpha);

/// <summary>The shared A024EF37 CPU lifetime. Graph2 always invokes both
/// preparation methods, including the false return after a real attach. Season
/// replacement keeps this owner. No frame ordinal controls a source branch.</summary>
public sealed class OriginalOrdinarySdfPreparationOwner
{
    private readonly IOriginalOrdinarySdfPreparationBackend backend;
    private object? manager8;
    private IOriginalOrdinarySdfOperation0? operation0;
    private IOriginalOrdinarySdfOperation1? operation1;

    public OriginalOrdinarySdfPreparationOwner(LoadedOriginalOrdinaryRsLayerSource source,
        IOriginalOrdinarySdfPreparationBackend actualBackend, uint actualCurrentSweepSide)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualBackend);
        Source = source; backend = actualBackend;
        if (actualCurrentSweepSide >= 4) throw new ArgumentOutOfRangeException(nameof(actualCurrentSweepSide));
        SweepSide = actualCurrentSweepSide;
        // Only D220B0's explicit numeric initializer. Attach follows graph2.
        State40 = source.ConstructFreshSdfNumericState().State40;
    }
    public LoadedOriginalOrdinaryRsLayerSource Source { get; }
    public uint State40 { get; private set; }
    public object? PreviousImage30 { get; private set; }
    public object? PreviousImage38 { get; private set; }
    public uint SweepSide { get; private set; }
    public bool Manager8Present => manager8 is not null;

    public OriginalOrdinarySdfPreparationReturned PrepareCurrent(
        OriginalOrdinarySdfPreparationCurrent current,
        object? actualSelectedViewOperation0Bank)
    {
        bool zero = PrepareOperation0(current, actualSelectedViewOperation0Bank);
        // EA92? always executes preparation1, even when preparation0 ALfalse.
        bool one = PrepareOperation1(current);
        return new(zero, one);
    }

    private void ApplyHostReset(uint host7F8)
    { if ((host7F8 & 8) != 0) State40 &= 0x7FFFFFFF; }

    private bool PrepareOperation0(OriginalOrdinarySdfPreparationCurrent current,
        object? actualBank)
    {
        if (!current.CurrentTablePresent) return false;
        ApplyHostReset(current.Host7F8);
        manager8 ??= backend.ConstructManager();
        if (manager8 is null) return false;
        if (operation0 is null)
        {
            operation0 = backend.ConstructAndAttachOperation0(manager8, 256, 256);
            return false;
        }
        if (current.CommonFlags10 is uint flags && (flags & (1u << 22)) == 0)
            return true; // D7C450 retains the previous operation.
        return actualBank is not null && operation0.ApplyCurrentWorldPopulation(actualBank);
    }

    private bool PrepareOperation1(OriginalOrdinarySdfPreparationCurrent current)
    {
        if (!current.CurrentTablePresent) return false;
        ApplyHostReset(current.Host7F8);
        manager8 ??= backend.ConstructManager();
        if (manager8 is null) return false;
        if (operation1 is null)
        {
            operation1 = backend.ConstructAndAttachOperation1(manager8);
            return false;
        }
        if (current.CommonFlags10 is uint flags && (flags & (1u << 8)) == 0)
            return false;
        var returned = backend.ApplyRetainedOperation1(operation1, current,
            SweepSide, PreviousImage30, PreviousImage38);
        PreviousImage30 = returned.PreviousImage30;
        PreviousImage38 = returned.PreviousImage38;
        SweepSide = (SweepSide + 1) & 3; // D7CB81..90, after real retained return.
        return returned.Alpha;
    }

    /// <summary>D61120 does not read operation0 at all when operation1 count38
    /// is nonzero. Keep this order when the new operation0 ring is unpublished.</summary>
    public bool HasCurrentSdfPopulation()
    {
        if (manager8 is null) return false;
        if (operation1 is not null && operation1.CurrentCount38 != 0) return true;
        return operation0 is not null && operation0.CaptureCurrentCounts().SelectedTotal != 0;
    }

    public OriginalDeferredSdfDecision PrepareCurrentSdf(int sdfNode,
        OriginalOrdinarySdfCurrentCall current)
    {
        var stage = Source.StageAtSdf(sdfNode);
        uint index = unchecked((current.HostViewIndex3F8 & 0x7FFFFFFFu) +
            ((stage.SdfPackedProcess30 >> 24) & 0x7Fu));
        // Native wrapper gates precede the population query as well.
        bool reachesProvider = current.CurrentTablePresent && current.HostContextFlags3F0 >= 0 &&
            index < 32 && current.SelectedViewPresent && current.RequiredOcDepthNsResolved;
        return OriginalDeferredExtraLayerPlan.Sdf(new(
            current.CurrentTablePresent && current.HostContextFlags3F0 >= 0,
            index, current.SelectedViewPresent, current.RequiredOcDepthNsResolved,
            stage.SdfPackedProcess30, reachesProvider && HasCurrentSdfPopulation(),
            current.CurrentView158C, State40, current.CurrentTargetHelper959250));
    }

    /// <summary>Commit only after the caller's actual selected fallback method
    /// has returned, including its helper-false normal return.</summary>
    public void CommitReturnedFallback(OriginalDeferredSdfDecision actualDecision)
    { State40 = actualDecision.NextState40; }

    public void CommitReturnedActiveFinalMethod()
    { State40 = Source.AfterCompletedActiveFinalMethod(State40); }
}
