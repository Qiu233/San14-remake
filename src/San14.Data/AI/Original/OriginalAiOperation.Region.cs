namespace San14.Data.AI.Original;

/// <summary>A normal immutable region-definition receiver. It is not a
/// Province, Area or mutable world entity.</summary>
public readonly record struct OriginalAiRegionReference(int PhysicalSlot);

public sealed partial class OriginalAiOperation
{
    private readonly Dictionary<uint, OriginalAiRegionReference> regions = [];

    internal bool TryRegionReference(uint key, out OriginalAiRegionReference value)
    {
        // Existing null/list writers may keep a shadowed auxiliary entry.
        // Read only the active object tag, as the other typed receivers do.
        if (integers.ContainsKey(key) || entities.ContainsKey(key) || candidateLists.ContainsKey(key) ||
            candidateNullEntities.Contains(key) || hexes.ContainsKey(key) || troops.ContainsKey(key) ||
            tradeCountries.ContainsKey(key) || hireOffers.ContainsKey(key) || personSkills.ContainsKey(key) || definitions.ContainsKey(key))
        { value = default; return false; }
        return regions.TryGetValue(key, out value);
    }

    internal OriginalAiNodeStatus SetRegionReference(uint key, int physicalSlot)
    {
        if (physicalSlot is < 0 or >= 18)
            throw new InvalidDataException("original_ai.region.reference");
        ClearCandidateNull(key);
        integers.Remove(key); entities.Remove(key); candidateLists.Remove(key);
        regions[key] = new(physicalSlot);
        return OriginalAiNodeStatus.Success;
    }

    internal void ClearRegionVariable(uint key) => regions.Remove(key);

    private bool TryCopyRegionVariable(uint source, uint destination)
    {
        if (!TryRegionReference(source, out var region)) return false;
        SetRegionReference(destination, region.PhysicalSlot);
        return true;
    }

    private bool TryCompareRegionVariables(uint left, uint right, out bool equal)
    {
        bool first = TryRegionReference(left, out var a), second = TryRegionReference(right, out var b);
        equal = first && second && a == b;
        return first || second;
    }
}
