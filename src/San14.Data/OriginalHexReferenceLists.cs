namespace San14.Data;

// Outer null is unknown. A present reference with no identity is an observed null pointer.
public sealed record OriginalHexListHandle(int? Identity);
public sealed record OriginalHexListNodeReference(int? Identity);

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
