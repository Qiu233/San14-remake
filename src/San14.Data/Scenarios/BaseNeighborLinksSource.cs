namespace San14.Data;

/// <summary>The fixed foothold neighbor window, including all serialized zero
/// slots. It is distinct from the decoded list of nonzero location identities.</summary>
internal static class BaseNeighborLinksSource
{
    internal static IReadOnlyList<byte> Read(LoadedScenario source, BaseReference reference)
    {
        int slot = reference.PhysicalSlot
            ?? throw new InvalidDataException("The fixed base links receiver is unknown.");
        IReadOnlyList<byte>? links = reference.Kind switch
        {
            BaseKind.City when (uint)slot < source.Snapshot.Cities.Count => source.Snapshot.Cities[slot].RawNeighborSlots58,
            BaseKind.Gate when (uint)slot < source.Snapshot.Gates.Count => source.Snapshot.Gates[slot].RawNeighborSlots58,
            _ => throw new InvalidDataException("The fixed base links receiver is outside its table.")
        };
        return links is { Count: 10 } ? links
            : throw new InvalidDataException("The ten serialized Base58 neighbor slots are unavailable.");
    }
}
