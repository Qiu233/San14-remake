namespace San14.Core;

/// <summary>Provisional ownership of traversed cells; area and city ownership remain separate.</summary>
public static class PlayableTerritoryPainting
{
    public static Dictionary<int, TerritoryState>? PaintTraversed(
        IReadOnlyDictionary<int, TerritoryState> original,
        Dictionary<int, TerritoryState>? changed, ReadOnlySpan<int> traversed,
        int forceId, out int painted)
    {
        painted = 0;
        foreach (int hexId in traversed)
        {
            IReadOnlyDictionary<int, TerritoryState> current = changed ?? original;
            if (!current.TryGetValue(hexId, out TerritoryState? cell) ||
                cell.AreaId is null || cell.ForceId == forceId)
                continue;
            changed ??= new Dictionary<int, TerritoryState>(original);
            changed[hexId] = cell with { ForceId = forceId };
            painted++;
        }
        return changed;
    }
}
