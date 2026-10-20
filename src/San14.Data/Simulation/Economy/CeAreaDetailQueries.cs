using San14.Core;

namespace San14.Data;

public sealed record CeAreaDevelopmentDisplay(int Level, int? Progress,
    int? RequiredProgress, bool ShowProgressSeparator);

public sealed record CeAreaDetailValues(int? Population, bool SuppressPopulationAndProgress,
    CeAreaDevelopmentDisplay? Commerce, CeAreaDevelopmentDisplay? Agriculture,
    CeAreaDevelopmentDisplay? Barracks, int? OwnedCells, int? TerrainCells)
{
    public int? CurrentForceId { get; init; }
    public bool? HasValidOwner { get; init; }
}

/// <summary>Read-only area-detail values. The native panel uses the representative
/// cell's owner, current population and within-level development progress; it does
/// not restore missing values from initial scenario records.</summary>
public sealed class CeAreaDetailQueries
{
    private readonly CeInitialAreaDevelopment development;

    public CeAreaDetailQueries(string sourceDirectory, string locale) =>
        development = CeInitialAreaDevelopment.LoadCurrent(sourceDirectory, locale);

    public CeAreaDetailValues? Read(GameState state, int areaId)
    {
        if (!state.Areas.TryGetValue(areaId, out var area) || area.HexId is not int center ||
            !state.Territory.TryGetValue(center, out var cell)) return null;
        //22C950 follows Area3A -> Hex14 -> Force. Null domain affiliation is
        //the known neutral owner, not the Area's potentially stale ForceId.
        bool special = cell.ForceId is >= 46 and <= 50;
        var statistics = state.RegionStatistics?.Entries.GetValueOrDefault(areaId);
        bool? ownerValid = cell.ForceId is null or 0 ? false :
            state.Forces.TryGetValue(cell.ForceId.Value, out var owner) && owner.RulerId is int ruler
                ? NativeValidity.Force(cell.ForceId.Value, checked((ushort)ruler)) : null;
        bool countsKnown = ownerValid == true && statistics?.IsOwnedCountCurrent == true;
        return new(special ? null : area.NativeNormalAftermath?.Quantity52, special,
            ReadDevelopment(area.Development?.Commerce, special),
            ReadDevelopment(area.Development?.Agriculture, special),
            ReadDevelopment(area.Development?.Barracks, special),
            countsKnown ? statistics!.OwnedCellCount : null,
            countsKnown ? statistics!.TerrainCellCount : null)
        { CurrentForceId = cell.ForceId ?? 0, HasValidOwner = ownerValid };
    }

    private CeAreaDevelopmentDisplay? ReadDevelopment(int? currentProgress, bool special)
    {
        if (currentProgress is not int progress || progress is < 0 or > ushort.MaxValue) return null;
        int level = development.ReadLevel((ushort)progress);
        if (special) return new(level, null, null, false);
        //The level-five panel renders a dash on both sides of its separator.
        if (level == 5) return new(level, null, null, true);
        int lower = level == 1 ? 0 : development.LevelThresholds[level - 2];
        int upper = development.LevelThresholds[level - 1];
        return new(level, progress - lower, upper - lower, true);
    }
}
