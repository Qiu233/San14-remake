namespace San14.Data;

// Outer null is unknown. A present reference with no identity is an observed null pointer.
public sealed record HexListHandle(int? Identity);
public sealed record HexListNodeReference(int? Identity);

public interface IHexReferenceListState
{
    HexListHandle? ReadBaseHexListHandle(BaseReference baseReference);
    HexListNodeReference? ReadHexListHead(HexListHandle handle);
    HexReference? ReadHexListNodePayload(HexListNodeReference node);
    HexListNodeReference? ReadHexListNodeNext(HexListNodeReference node);
    void ClearBaseHexReferences(BaseReference baseReference);
    bool AppendBaseHexReference(BaseReference baseReference, HexReference hex);
    HexListHandle? CreateTemporaryHexList();
    void ClearTemporaryHexList(HexListHandle handle);
    bool AppendTemporaryHexReference(HexListHandle handle, HexReference hex);
    int? ReadTemporaryHexListCount(HexListHandle handle);
    // The outer caller clears first. This method only releases the descriptor.
    void ReleaseTemporaryHexList(HexListHandle handle);
}
