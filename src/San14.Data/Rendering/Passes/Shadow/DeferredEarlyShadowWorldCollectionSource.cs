namespace San14.Data;

/// <summary>The currently installed Scene22A8 Data's actual CPU. The original
/// caller reads V38 once, then V28 for each index; an inventory grouped by
/// static/Army or ordinary visibility is not this provider.</summary>
public interface IDeferredEarlyShadowCurrentWorldCollection<TData> where TData : class
{
    uint ReadActualCountV38();
    TData? ReadActualDataV28(uint actualIndex);
    void RequireCurrent();
    string SourceEvidence { get; }
}

public enum DeferredEarlyShadowWorldCollectionCallKind { SceneRoot18, Collection22A8 }

/// <summary>One actual D68E40 call in source traversal order. Null Data is
/// skipped; aliases and duplicate references preserve their actual positions.</summary>
public sealed record DeferredEarlyShadowWorldCollectionCall<TData>(
    DeferredEarlyShadowWorldCollectionCallKind Kind, uint? ActualCollectionIndex, TData Data)
    where TData : class;

/// <summary>Finite D4BD30 consumer contract. It preserves an already installed
/// collection's actual index order; it does not assign a resource key or invent
/// registration/creation order for a missing collection producer.</summary>
public sealed class DeferredEarlyShadowWorldCollectionSource<TData> where TData : class
{
    private readonly IDeferredEarlyShadowCurrentWorldCollection<TData>? actualCollection;
    private DeferredEarlyShadowWorldCollectionSource(long frame, string scene,
        IDeferredEarlyShadowCurrentWorldCollection<TData>? collection,
        DeferredEarlyShadowWorldCollectionCall<TData>[] calls, string evidence)
        => (FrameNumber, SceneIdentity, actualCollection, Calls, SourceEvidence) =
            (frame, scene, collection, Array.AsReadOnly(calls), evidence);
    public long FrameNumber { get; }
    public string SceneIdentity { get; }
    public IReadOnlyList<DeferredEarlyShadowWorldCollectionCall<TData>> Calls { get; }
    public string SourceEvidence { get; }
    public void RequireCurrent() => actualCollection?.RequireCurrent();

    /// <summary>D4BE12's optional root precedes the collection only when the
    /// real Aux exists and flags8.bit31 is set. D4BE78 caches the count and
    /// D4BEA8 reads indices0..count-1, without family sorting/deduplication.</summary>
    public static DeferredEarlyShadowWorldCollectionSource<TData> CaptureCurrent(
        long actualFrameNumber, string actualSceneIdentity, bool actualAux240Present,
        uint actualAuxFlags8, TData? actualSceneRoot18, bool actualCollection22A8Present,
        IDeferredEarlyShadowCurrentWorldCollection<TData>? actualCollection22A8,
        string actualCurrentInstallerEvidence)
    {
        if (actualFrameNumber <= 0 || string.IsNullOrWhiteSpace(actualSceneIdentity) ||
            string.IsNullOrWhiteSpace(actualCurrentInstallerEvidence))
            throw new InvalidDataException("World collection requires its actual current Scene and installer source.");
        if (actualCollection22A8Present != (actualCollection22A8 is not null))
            throw new NotSupportedException("Current Scene22A8 presence requires the installed Data's actual collection CPU getter.");
        var calls = new List<DeferredEarlyShadowWorldCollectionCall<TData>>();
        if (actualAux240Present && (actualAuxFlags8 & 0x80000000u) != 0 && actualSceneRoot18 is not null)
            calls.Add(new(DeferredEarlyShadowWorldCollectionCallKind.SceneRoot18, null, actualSceneRoot18));
        if (actualCollection22A8 is not null)
        {
            actualCollection22A8.RequireCurrent();
            if (string.IsNullOrWhiteSpace(actualCollection22A8.SourceEvidence))
                throw new InvalidDataException("World collection getter has no actual current source.");
            uint count = actualCollection22A8.ReadActualCountV38();
            for (uint index = 0; index < count; index++)
                if (actualCollection22A8.ReadActualDataV28(index) is { } data)
                    calls.Add(new(DeferredEarlyShadowWorldCollectionCallKind.Collection22A8, index, data));
            actualCollection22A8.RequireCurrent();
        }
        return new(actualFrameNumber, actualSceneIdentity, actualCollection22A8, calls.ToArray(),
            actualCurrentInstallerEvidence + ";TC:D4BD30/D4BE08..D4BED6;" + actualCollection22A8?.SourceEvidence +
            ";ascending-actual-V28-index/no-family-reordering/no-dedup;D68E40->Data20.WorldV30");
    }
}
