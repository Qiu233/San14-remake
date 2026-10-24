using San14.Core;

namespace San14.Data;

/// <summary>Decodes area and terrain definitions once, then delegates counting to the simulation core.</summary>
public static class AreaStatisticsSourceAdapter
{
    /// <summary>Bind the saved, scheduled statistics into one operation's
    /// current view. Capturing a view never refreshes ownership or recounts tiles.</summary>
    public static void BindCurrent(GameState state, SourceWorldContext current)
    {
        if (state.RegionStatistics is not { } statistics) return;
        foreach (var (physical, area) in statistics.Entries)
        {
            current.SetAreaRawField76(physical,
                AreaMapStatisticsInitialization.CountToNativeWord(area.TerrainCellCount));
            current.SetAreaRawField68(physical, area.IsOwnedCountCurrent
                ? AreaMapStatisticsInitialization.CountToNativeWord(area.OwnedCellCount) : null);
        }
    }

    public static GameState Initialize(GameState state, LoadedScenario scenario, LoadedMap map,
        LoadedTerrainTypes terrain)
    {
        if (state.RegionStatistics is not null)
            throw new InvalidOperationException("Regional map statistics are already initialized.");
        if (scenario.Snapshot.WorldParameters?.RawField48 is not int mode || mode is >= 100 and <= 199)
            throw new NotSupportedException("This scenario requires a separate regional map initialization rule.");
        var cells = map.Map?.Cells ?? throw new InvalidDataException("Map cells are unavailable.");
        var definitions = scenario.Snapshot.Areas.Select(area => new AreaStatisticsDefinition(area.Slot,
            area.CitySlot == 0 ? null : area.CitySlot, area.HexId, area.RegionalNeighborMask));
        var types = terrain.Catalog.Types;
        var countedTerrainHexes = cells.Where(cell =>
            types[cell.SurfaceCode < types.Count ? cell.SurfaceCode : 0].CountsTowardRegionalArea)
            .Select(cell => cell.Id).ToHashSet();
        return state with
        {
            RegionStatistics = AreaStatisticsRules.Initialize(definitions, state.Territory, countedTerrainHexes)
        };
    }
}
