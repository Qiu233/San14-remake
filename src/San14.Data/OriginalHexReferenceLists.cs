namespace San14.Data;

// Outer null is unknown. A present reference with no identity is an observed null pointer.
public sealed record OriginalHexListHandle(int? Identity);
public sealed record OriginalHexListNodeReference(int? Identity);

/// <summary>
/// Explicit normal current pool profile. Allocation outcomes and returned nodes
/// are supplied by the caller, not inferred from scenario records or free memory.
/// Missing callback/result is unknown; a result with Identity=null is allocation failure.
/// Physical pool bytes, locks, global free-list totals and native persistence are outside
/// this logical stable-node adapter. Callbacks own their current external pool effects.
/// </summary>
public sealed class OriginalHexListAllocationProfile
{
    public bool? NodePoolPresent { get; set; }
    public int? ListKeyCapacity { get; set; }
    public Func<OriginalHexListHandle?>? TemporaryDescriptorAllocator { get; set; }
    public Func<OriginalHexListHandle, OriginalHexListNodeReference?>? NodeAllocator { get; set; }
    // Native018D70 follows the local count increment, before return/caller payload store.
    public Action<OriginalHexListHandle>? NodeCountIncrementer { get; set; }
    // O(1) free-chain splice uses head/tail; it does not scan interior Next fields.
    public Action<OriginalHexListHandle, OriginalHexListNodeReference, OriginalHexListNodeReference>? NodeReturner { get; set; }
    // The old count is consumed only after current head/tail have been cleared.
    public Action<OriginalHexListHandle, int>? NodeCountReturner { get; set; }
    public Action<OriginalHexListHandle>? TemporaryDescriptorReleaser { get; set; }
    public Action? InternalPoolLockEnterer { get; set; }
    public Action? InternalPoolLockExiter { get; set; }
}

public interface IOriginalHexReferenceListState
{
    OriginalHexListHandle? ReadBaseHexListHandle(OriginalBaseReference baseReference);
    OriginalHexListNodeReference? ReadHexListHead(OriginalHexListHandle handle);
    OriginalHexReference? ReadHexListNodePayload(OriginalHexListNodeReference node);
    OriginalHexListNodeReference? ReadHexListNodeNext(OriginalHexListNodeReference node);
    void ClearBaseHexReferences(OriginalBaseReference baseReference);
    bool AppendBaseHexReference(OriginalBaseReference baseReference, OriginalHexReference hex);
    OriginalHexListHandle? CreateTemporaryHexList();
    void ClearTemporaryHexList(OriginalHexListHandle handle);
    bool AppendTemporaryHexReference(OriginalHexListHandle handle, OriginalHexReference hex);
    int? ReadTemporaryHexListCount(OriginalHexListHandle handle);
    // The outer caller clears first. This method only releases the descriptor.
    void ReleaseTemporaryHexList(OriginalHexListHandle handle);
}
