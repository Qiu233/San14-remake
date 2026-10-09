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

    public static CeAreaIncomeStages Calculate(CeAreaIncomeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Month is < 1 or > 12 || input.Security54 > 100 ||
            input.EffectivePerson2 < 0 || input.EffectivePerson3 < 0 ||
            input.GoldDifficultyPercent < 0 || input.SuppliesDifficultyPercent < 0)
            throw new InvalidDataException("Resolved CE area income input is outside its supported native domain.");
        int goldLevel = Level(input.RawProgress3C), suppliesLevel = Level(input.RawProgress3E);
        int goldPersonContribution = PersonContribution(input.EffectivePerson2, input.PersonTaskPercent, input.NativePersonValid);
        int suppliesPersonContribution = PersonContribution(input.EffectivePerson3, input.PersonTaskPercent, input.NativePersonValid);
        int goldMultiplier = goldLevel switch { 1 => 10, 2 => 12, 3 => 14, 4 => 16, _ => 18 };
        int suppliesMultiplier = suppliesLevel switch { 1 => 10, 2 => 13, 3 => 16, 4 => 18, _ => 20 };
        int goldN, suppliesN, goldQ, suppliesQ;
        unchecked
        {
            goldN = 100 + 10 * goldPersonContribution / 100 + 10 * input.RawBonus6D;
            suppliesN = 100 + 10 * suppliesPersonContribution / 100 + 10 * input.RawBonus6D;
            goldQ = goldN * goldMultiplier * input.RawField44 / 1000;
            suppliesQ = suppliesN * suppliesMultiplier * input.RawField46 / 1000;
        }
        float populationTerm = MathF.Sqrt((float)input.RawField52);
        populationTerm *= Math.Max(input.Security54, (byte)50);
        populationTerm /= 100f;
        float goldV = populationTerm + goldQ;
        goldV *= input.OwnedCells68;
        goldV /= Math.Max(input.TerrainCells76, (ushort)1);
        float suppliesV = populationTerm + suppliesQ;
        suppliesV *= input.OwnedCells68;
        suppliesV /= Math.Max(input.TerrainCells76, (ushort)1);
        int goldMidstage, suppliesMidstage, goldPerson, suppliesPerson;
        int goldForce, suppliesForce, suppliesSeason, gold, supplies;
        unchecked
        {
            goldMidstage = (int)goldV * 65 / 100;
            suppliesMidstage = (int)suppliesV * 30 / 100;
            goldPerson = goldMidstage + goldMidstage * input.PersonEffect24 / 100;
            suppliesPerson = suppliesMidstage + suppliesMidstage * input.PersonEffect25 / 100;
            goldForce = goldPerson * (100 + input.ForceEffect35) / 100;
            suppliesForce = suppliesPerson * (100 + input.ForceEffect34) / 100;
            suppliesSeason = input.Month is >= 7 and <= 9
                ? suppliesForce * 125 * ((input.AreaFlags38 & 0x10) != 0 ? 150 : 100) / 10000
                : suppliesForce;
            gold = goldForce * input.GoldDifficultyPercent / 100;
            supplies = suppliesSeason * input.SuppliesDifficultyPercent / 100;
        }
        return new(goldLevel, suppliesLevel, goldPersonContribution, suppliesPersonContribution, goldMultiplier, suppliesMultiplier,
            goldN, suppliesN, goldQ, suppliesQ, goldV, suppliesV, goldMidstage, suppliesMidstage,
            goldPerson, suppliesPerson, goldForce, suppliesForce, suppliesSeason, gold, supplies);
    }
}
