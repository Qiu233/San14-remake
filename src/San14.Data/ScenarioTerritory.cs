using San14.Data.Formats;

namespace San14.Data;

public readonly record struct TerritoryCell(int HexId, int AreaId, int ForceId);

/// <summary>
/// Resolves initial tile ownership through scenario regions. Original map dimensions
/// and binary IDs stay in the source adapter; clients receive integer entity IDs.
/// </summary>
public sealed record ScenarioTerritory(int Width, int Height,
    IReadOnlyList<TerritoryCell> Cells, int TotalLandCells,
    IReadOnlyDictionary<int, int> LandCellsByForce)
{
    public static ScenarioTerritory Build(MapDocument map, ScenarioCityPreview scenario)
    {
        if (map.Cells.Length != checked(map.Width * map.Height))
            throw new InvalidDataException("Map dimensions and tile count differ.");
        Dictionary<int, AreaPreviewRecord> areas = scenario.Areas.ToDictionary(area => area.Slot);
        foreach (AreaPreviewRecord area in scenario.Areas.Where(area => area.CitySlot != 0))
        {
            if (area.HexId >= map.Cells.Length || map.Cells[area.HexId].AreaSlot != area.Slot)
                throw new InvalidDataException($"Map and scenario region {area.Slot} coordinates differ.");
        }

        var cells = new TerritoryCell[map.Cells.Length];
        var counts = new Dictionary<int, int>();
        int land = 0;
        foreach (MapCell cell in map.Cells)
        {
            if (cell.Id < 0 || cell.Id >= cells.Length ||
                !areas.TryGetValue(cell.AreaSlot, out AreaPreviewRecord? area) ||
                (cell.AreaSlot != 0 && area.CitySlot == 0))
                throw new InvalidDataException($"Map tile {cell.Id} has an invalid region {cell.AreaSlot}.");
            // Region zero is the source's unassigned map space, not a neutral land region.
            int force = cell.AreaSlot == 0 ? 0 : area.ForceSlot;
            cells[cell.Id] = new TerritoryCell(cell.Id, cell.AreaSlot, force);
            if (cell.AreaSlot == 0) continue;
            land++;
            counts[force] = counts.GetValueOrDefault(force) + 1;
        }
        return new ScenarioTerritory(map.Width, map.Height, cells, land, counts);
    }
}
