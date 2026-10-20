namespace San14.Data;

/// <summary>Resolved inputs to the ordinary CE area-income branch. EffectivePerson2/3
/// are outputs of native2300B0, before the caller's current task percentage.</summary>
public sealed record CeAreaIncomeInput(ushort RawField44, ushort RawField46,
    ushort RawField52, byte Security54, ushort RawProgress3C, ushort RawProgress3E,
    byte RawBonus6D, ushort OwnedCells68, ushort TerrainCells76,
    int EffectivePerson2, int EffectivePerson3, int PersonEffect24, int PersonEffect25,
    int ForceEffect35, int ForceEffect34, int Month, byte AreaFlags38,
    int GoldDifficultyPercent, int SuppliesDifficultyPercent,
    int PersonTaskPercent, bool NativePersonValid);

public sealed record CeAreaIncomeStages(int GoldLevel, int SuppliesLevel,
    int GoldPersonContribution, int SuppliesPersonContribution,
    int GoldLevelMultiplier, int SuppliesLevelMultiplier, int GoldN, int SuppliesN,
    int GoldQ, int SuppliesQ, float GoldV, float SuppliesV, int GoldMidstage,
    int SuppliesMidstage, int GoldAfterPersonEffect, int SuppliesAfterPersonEffect,
    int GoldAfterForceEffect, int SuppliesAfterForceEffect, int SuppliesAfterSeason,
    int Gold, int Supplies);

public sealed record CeAreaGoldIncomeInput(ushort RawField44, ushort RawField52, byte Security54,
    ushort RawProgress3C, byte RawBonus6D, ushort OwnedCells68, ushort TerrainCells76,
    int EffectivePerson2, int PersonTaskPercent, bool NativePersonValid,
    int PersonEffect24, int ForceEffect35, int GoldDifficultyPercent, int? AdvancedGoldPercent = null, bool NativeForceValid = true);

public sealed record CeAreaGoldIncomeStages(int Level, int PersonContribution, int LevelMultiplier,
    int N, int Q, float V, int Midstage, int AfterPersonEffect, int AfterForceEffect,
    int AfterDifficulty, int Gold);

public sealed record CeAreaSuppliesIncomeInput(ushort RawField46, ushort RawField52, byte Security54,
    ushort RawProgress3E, byte RawBonus6D, ushort OwnedCells68, ushort TerrainCells76,
    int EffectivePerson3, int PersonTaskPercent, bool NativePersonValid,
    int PersonEffect25, int ForceEffect34, int Month, byte? AreaFlags38,
    int SuppliesDifficultyPercent, int? AdvancedSuppliesPercent = null, bool NativeForceValid = true);

public sealed record CeAreaSuppliesIncomeStages(int Level, int PersonContribution, int LevelMultiplier,
    int N, int Q, float V, int Midstage, int AfterPersonEffect, int AfterForceEffect,
    int AfterSeason, int AfterDifficulty, int Supplies);

public sealed record CeAreaTroopsIncomeInput(ushort RawField48, ushort RawField52, byte Security54,
    ushort RawProgress40, ushort OwnedCells68, ushort TerrainCells76, int EffectiveRecruiter1,
    bool RecruiterBusy, int PreparedCycles, int ForcePolicy1C, int RecruiterEffect11,
    int AreaOfficerEffect26, int ForcePanel36, int DifficultyPercent,
    int? AdvancedTroopsPercent = null, bool NativeForceValid = true);

public sealed record CeAreaTroopsIncomeStages(int Level, int LevelMultiplier,
    float PopulationTerm, float OfficerTerm, float V, int Midstage, int AfterPolicy,
    int AfterTraits, int AfterPanel, int AfterDifficulty, int BeforePopulationCap, int Troops);

/// <summary>Complete ordinary-branch area arithmetic, with the native signed32 and
/// float32 operation order. Alternate force46..50 branches and city stock posting
/// require separate rules. Source functions: SC2BD0D0(gold), SC2BCBA0(supplies).</summary>
public static class CeAreaIncomeMath
{
    public const string FormulaId = "san14:ce-area-income-main-20261002";

    public static int Level(ushort progress) => progress switch
    {
        < 100 => 1, < 300 => 2, < 600 => 3, < 1000 => 4, _ => 5
    };

    public static int PersonContribution(int effectiveValue, int taskPercent, bool nativePersonValid) =>
        nativePersonValid ? Math.Max(1, unchecked(effectiveValue * taskPercent) / 100) : 0;

    public static CeAreaGoldIncomeStages CalculateGold(CeAreaGoldIncomeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        int level = Level(input.RawProgress3C);
        int contribution = PersonContribution(input.EffectivePerson2, input.PersonTaskPercent, input.NativePersonValid);
        int multiplier = level switch { 1 => 10, 2 => 12, 3 => 14, 4 => 16, _ => 18 };
        int n = unchecked(100 + 10 * contribution / 100 + 10 * input.RawBonus6D);
        int q = unchecked(n * multiplier * input.RawField44) / 1000;
        float v = MathF.Sqrt((float)input.RawField52);
        v *= Math.Max(input.Security54, (byte)50);
        v /= 100f;
        v += q;
        v *= input.OwnedCells68;
        v /= Math.Max(input.TerrainCells76, (ushort)1);
        int truncated = TruncateSource(v);
        int midstage = unchecked(truncated * 65) / 100;
        int person = unchecked(midstage + unchecked(midstage * input.PersonEffect24) / 100);
        int force = input.NativeForceValid ? unchecked(person * (100 + input.ForceEffect35)) / 100 : person;
        int difficulty = unchecked(force * input.GoldDifficultyPercent) / 100;
        int gold = input.AdvancedGoldPercent is int percent ? unchecked(difficulty * percent) / 100 : difficulty;
        return new(level, contribution, multiplier, n, q, v, midstage, person, force, difficulty, gold);
    }

    public static CeAreaSuppliesIncomeStages CalculateSupplies(CeAreaSuppliesIncomeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        int level = Level(input.RawProgress3E);
        int contribution = PersonContribution(input.EffectivePerson3, input.PersonTaskPercent, input.NativePersonValid);
        int multiplier = level switch { 1 => 10, 2 => 13, 3 => 16, 4 => 18, _ => 20 };
        int n = unchecked(100 + 10 * contribution / 100 + 10 * input.RawBonus6D);
        int q = unchecked(n * multiplier * input.RawField46) / 1000;
        float v = MathF.Sqrt((float)input.RawField52);
        v *= Math.Max(input.Security54, (byte)50);
        v /= 100f;
        v += q;
        v *= input.OwnedCells68;
        v /= Math.Max(input.TerrainCells76, (ushort)1);
        int midstage = unchecked(TruncateSource(v) * 30) / 100;
        int person = unchecked(midstage + unchecked(midstage * input.PersonEffect25) / 100);
        int force = input.NativeForceValid ? unchecked(person * (100 + input.ForceEffect34)) / 100 : person;
        if (input.Month is < 1 or > 12) throw new InvalidDataException("Supplies income season month is outside the native table.");
        int season = force;
        if (input.Month is >= 7 and <= 9)
        {
            byte flags = input.AreaFlags38 ?? throw new InvalidDataException("Supplies income season flags38 are unknown.");
            season = unchecked(((flags & 0x10) != 0 ? 150 : 100) * 125 * force) / 10000;
        }
        int difficulty = unchecked(season * input.SuppliesDifficultyPercent) / 100;
        int supplies = input.AdvancedSuppliesPercent is int percent ? unchecked(difficulty * percent) / 100 : difficulty;
        return new(level, contribution, multiplier, n, q, v, midstage, person, force, season, difficulty, supplies);
    }

    public static CeAreaTroopsIncomeStages CalculateTroops(CeAreaTroopsIncomeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        int level = Level(input.RawProgress40);
        int multiplier = level switch { 1 => 10, 2 => 13, 3 => 16, 4 => 19, _ => 22 };
        float population = MathF.Sqrt((float)input.RawField52);
        population *= Math.Max(input.Security54, (byte)50);
        population /= 100f;
        float officer = multiplier;
        officer *= input.RawField48;
        float ability = input.EffectiveRecruiter1;
        ability += 100f;
        officer *= ability;
        officer /= 1000f;
        float v = population + officer;
        v *= input.OwnedCells68;
        v /= Math.Max(input.TerrainCells76, (ushort)1);
        v /= input.RecruiterBusy ? 2f : 1f;
        int midstage = unchecked(TruncateSource(v) * 5 * input.PreparedCycles) / 100;
        int policy = unchecked(midstage + unchecked(input.ForcePolicy1C * midstage) / 100);
        int traits = unchecked(policy + unchecked(unchecked(input.RecruiterEffect11 + input.AreaOfficerEffect26) * policy) / 100);
        int panel = input.NativeForceValid ? unchecked((input.ForcePanel36 + 100) * traits) / 100 : traits;
        int difficulty = unchecked(input.DifficultyPercent * panel) / 100;
        int beforeCap = input.AdvancedTroopsPercent is int percent ? unchecked(percent * difficulty) / 100 : difficulty;
        int troops = Math.Min(beforeCap, Math.Max(input.RawField52 - 5000, 0));
        troops = Math.Min(troops, input.RawField52 - 1);
        return new(level, multiplier, population, officer, v, midstage, policy, traits, panel, difficulty, beforeCap, troops);
    }

    /// <summary>The special branch after the caller's current-stock/capacity gate.</summary>
    public static int CalculateSpecialTroops(int currentTroops, int capacity, int difficulty)
    {
        int rate = difficulty switch { 0 => 10, 1 => 20, 2 => 30, _ => 40 };
        int amount = unchecked(unchecked(capacity - currentTroops) * rate) / 100;
        return Math.Min(15000, Math.Max(1000, amount));
    }

    private static int TruncateSource(float value) =>
        value >= -2147483648f && value < 2147483648f ? (int)value : int.MinValue;

    public static CeAreaIncomeStages Calculate(CeAreaIncomeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Month is < 1 or > 12 || input.Security54 > 100 ||
            input.EffectivePerson2 < 0 || input.EffectivePerson3 < 0 ||
            input.GoldDifficultyPercent < 0 || input.SuppliesDifficultyPercent < 0)
            throw new InvalidDataException("Resolved CE area income input is outside its supported native domain.");
        var gold = CalculateGold(new(input.RawField44, input.RawField52, input.Security54,
            input.RawProgress3C, input.RawBonus6D, input.OwnedCells68, input.TerrainCells76,
            input.EffectivePerson2, input.PersonTaskPercent, input.NativePersonValid,
            input.PersonEffect24, input.ForceEffect35, input.GoldDifficultyPercent));
        var supplies = CalculateSupplies(new(input.RawField46, input.RawField52, input.Security54,
            input.RawProgress3E, input.RawBonus6D, input.OwnedCells68, input.TerrainCells76,
            input.EffectivePerson3, input.PersonTaskPercent, input.NativePersonValid,
            input.PersonEffect25, input.ForceEffect34, input.Month, input.AreaFlags38,
            input.SuppliesDifficultyPercent));
        return new(gold.Level, supplies.Level, gold.PersonContribution, supplies.PersonContribution, gold.LevelMultiplier, supplies.LevelMultiplier,
            gold.N, supplies.N, gold.Q, supplies.Q, gold.V, supplies.V, gold.Midstage, supplies.Midstage,
            gold.AfterPersonEffect, supplies.AfterPersonEffect, gold.AfterForceEffect, supplies.AfterForceEffect, supplies.AfterSeason, gold.Gold, supplies.Supplies);
    }
}
