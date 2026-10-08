namespace San14.Data;

/// <summary>Original named selection keys, not RDB file IDs or resolved seasonal resources.</summary>
public sealed record OriginalMapSeasonSelection(int SeasonIndex, string ActionName, uint ActionNameHash,
    string EffectEventName, int EffectEventTableIndex);

/// <summary>SC/TC CE ordinary month getter and its two named consumers. The caller owns the date.</summary>
public static class OriginalMapSeasonMath
{
    private static readonly OriginalMapSeasonSelection[] Seasons =
    [
        new(0, "A_CE_01_SPRING", 0x6BABC16C, "map_season_effect_001", 130),
        new(1, "A_CE_02_SUMMER", 0x3B39C56D, "map_season_effect_002", 131),
        new(2, "A_CE_03_AUTUMN", 0xC7655BB3, "map_season_effect_003", 132),
        new(3, "A_CE_04_WINTER", 0x6EB05581, "map_season_effect_004", 133)
    ];
    private static readonly byte[] MonthIndices = [0,0,0,1,1,1,2,2,2,3,3,3];

    public static OriginalMapSeasonSelection SelectMonth(int sourceMonth)
    {
        if (sourceMonth is < 1 or > 12)
            throw new InvalidDataException("Original map season requires an explicit source month in 1..12.");
        return Seasons[MonthIndices[sourceMonth - 1]];
    }
}
