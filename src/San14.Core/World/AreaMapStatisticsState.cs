namespace San14.Core;

/// <summary>The map statistics used by regional effects, latched at their scheduled update.</summary>
public sealed record AreaMapStatisticsEntry(int AreaId, int? CityId, int CenterHexId,
    byte NeighborMask, int OwnedCellCount, int TerrainCellCount, int? ForceId,
    bool IsOwnedCountCurrent);

public sealed record AreaMapStatisticsState(Dictionary<int, AreaMapStatisticsEntry> Entries,
    int[] UpdateOrder);

/// <summary>Static inputs decoded by the resource adapter; no original memory layout is required.</summary>
public sealed record AreaStatisticsDefinition(int AreaId, int? CityId, int CenterHexId,
    byte NeighborMask);

public static class AreaStatisticsRules
{
    public static AreaMapStatisticsState Initialize(IEnumerable<AreaStatisticsDefinition> definitions,
        IReadOnlyDictionary<int, TerritoryState> territory, IReadOnlySet<int> countedTerrainHexes)
    {
        var areaDefinitions = definitions.ToArray();
        var entries = areaDefinitions.ToDictionary(definition => definition.AreaId, definition =>
        {
            bool active = definition.AreaId != 0 && definition.CityId is not null;
            int? force = active ? territory[definition.CenterHexId].ForceId : null;
            return new AreaMapStatisticsEntry(definition.AreaId, definition.CityId, definition.CenterHexId,
                definition.NeighborMask, 0, 0, force, force is not null);
        });
        foreach (TerritoryState cell in territory.Values)
        {
            if (cell.AreaId is not int areaId || !entries.TryGetValue(areaId, out var area) ||
                areaId == 0 || area.CityId is null) continue;
            entries[areaId] = area with
            {
                OwnedCellCount = area.OwnedCellCount + (cell.ForceId == area.ForceId ? 1 : 0),
                TerrainCellCount = area.TerrainCellCount + (countedTerrainHexes.Contains(cell.Id) ? 1 : 0)
            };
        }
        int[] updateOrder = areaDefinitions.Where(area => area.AreaId != 0 && area.CityId is not null)
            .Select(area => area.AreaId).ToArray();
        return new(entries, updateOrder);
    }

    /// <summary>Ownership changes do not silently refresh these statistics. The region-update phase does.</summary>
    public static AreaMapStatisticsState RefreshOwnership(AreaMapStatisticsState previous,
        IReadOnlyDictionary<int, TerritoryState> territory)
    {
        var entries = new Dictionary<int, AreaMapStatisticsEntry>(previous.Entries);
        var forces = entries.Values.Where(area => area.AreaId != 0 && area.CityId is not null)
            .ToDictionary(area => area.AreaId, area => territory[area.CenterHexId].ForceId);
        var counts = new Dictionary<int, int>();
        foreach (TerritoryState cell in territory.Values)
            if (cell.AreaId is int areaId && forces.TryGetValue(areaId, out int? force) &&
                force is not null && cell.ForceId == force)
                counts[areaId] = counts.GetValueOrDefault(areaId) + 1;
        foreach (int areaId in previous.UpdateOrder)
            entries[areaId] = entries[areaId] with
            {
                ForceId = forces[areaId], OwnedCellCount = counts.GetValueOrDefault(areaId),
                IsOwnedCountCurrent = true
            };
        return previous with { Entries = entries };
    }
}
