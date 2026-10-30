using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>The installed experience resource's investment and area-level
/// adjustment rows. Current environment choices select their original columns.</summary>
internal sealed class SceneryInvestmentSource
{
    private readonly byte[][] levels, adjustments;
    private readonly SourceProvenance source;
    internal SceneryInvestmentSource(LoadedExperienceData loaded)
    {
        source = loaded.Source;
        if (loaded.Document.Tables.Count != 4 || loaded.Document.Tables[2] is not
            { RecordStride: 15, Records.Count: 7 } level || loaded.Document.Tables[3] is not
            { RecordStride: 6, Records.Count: 16 } adjustment)
            throw Missing("experience_investment_tables");
        levels = level.Records.Select(row => row.RawBytes).ToArray();
        adjustments = adjustment.Records.Select(row => row.RawBytes).ToArray();
    }
    internal (int Gold, int Days) Read(GameState state, int currentLevel, int areaLevelSum)
    {
        Require(state);
        if (currentLevel is < 0 or > 5) return (0, 0);
        byte[] row = levels[currentLevel + 1], adjustment = adjustments[areaLevelSum < 16 ? areaLevelSum : 0];
        int goldMode = K(BattleRemakeCurrentSettings.ReadInt32(state, 0x118), "gold_mode");
        int daysMode = K(BattleRemakeCurrentSettings.ReadInt32(state, 0x11C), "days_mode");
        int rawGold = BinaryPrimitives.ReadInt16LittleEndian(row.AsSpan(goldMode == 0 ? 9 : goldMode == 2 ? 12 : 5, 2));
        float adjusted = rawGold * ((float)(100 - adjustment[5]) / 100f);
        int gold = Math.Max(1, NativeInt(adjusted));
        int days = Math.Max(1, (daysMode == 0 ? row[8] : row[4]) - adjustment[4]);
        gold = Advanced(state, gold, 19);
        days = Math.Max(1, Advanced(state, days, 20));
        return (gold, days);
    }
    internal int ReadIncrement(GameState state, int currentLevel)
    {
        Require(state);
        byte[] row = levels[Math.Min(currentLevel + 1, 6)];
        int mode = K(BattleRemakeCurrentSettings.ReadInt32(state, 0x114), "effect_growth_mode");
        return Advanced(state, row[mode == 0 ? 11 : mode == 2 ? 14 : 7], 18);
    }
    private void Require(GameState state)
    {
        if (state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256)
            throw Missing("experience_resource_identity");
    }
    private static int Advanced(GameState state, int value, int index) =>
        K(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "advanced_settings")
            ? unchecked(K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, index), "advanced_parameter_" + index) * value) / 100
            : value;
    private static int NativeInt(float value) => value is >= -2147483648f and < 2147483648f ? (int)value : int.MinValue;
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("scenery.investment." + field);
}
