using System.Collections.ObjectModel;

namespace San14.Data.Formats;

public sealed record OriginalBaseParameterCatalog(IReadOnlyDictionary<int, int> RawValues, string ProfileId)
{
    public int? RuntimeTroopsExtraInitial { get; init; }
}

/// <summary>Owned CE base-rule defaults. Compatibility keys identify parameter
/// meanings; they are no longer addresses read from an executable.</summary>
public static class OriginalBaseParameterReader
{
    public const string ImplementationVersion = "san14:ce-base-defaults-v1";
    public const string ResourceUri = "san14://rules/ce/base-defaults/v1";
    public const int SpecialGoldCapacity = 250000;
    public const int SpecialFoodCapacity = 500000;
    public const int SpecialTroopCapacity = 300000;
    public const int MaximumDurability = 9999;
    public const int GoldCapacityPerCitySize = 50000;
    public const int FoodCapacityPerCitySize = 100000;
    public const int BaseTroopCapacity = 50000;
    public const int MediumCityExtraTroops = 20000;
    public const int LargeCityExtraTroops = 50000;
    public const int MetropolisExtraTroops = 100000;
    public const int BaseDurability = 2000;
    public const int DurabilityPerAdditionalSize = 1000;
    public const int DurabilityPolicyScale = 1;
    public const int BaseMoraleMaximum = 100;
    public const int MoralePolicyScale = 1;
    public const float PercentScale = 100f;
    public const int DamageTierFirstThreshold = 75;
    public const int DamageTierSecondThreshold = 50;
    public const int DamageTierThirdThreshold = 25;

    private static readonly (int Key, int Value)[] Defaults =
    [
        (0x1A4BC98, SpecialGoldCapacity), (0x1A4BCA4, SpecialFoodCapacity),
        (0x1A4BCB0, SpecialTroopCapacity), (0x1A4BCB8, MaximumDurability),
        (0x1A4C1FC, GoldCapacityPerCitySize), (0x1A4C208, FoodCapacityPerCitySize),
        (0x1A4C214, BaseTroopCapacity), (0x1A4C228, MediumCityExtraTroops),
        (0x1A4C234, LargeCityExtraTroops), (0x1A4C240, MetropolisExtraTroops),
        (0x1A4C248, BaseDurability), (0x1A4C254, DurabilityPerAdditionalSize),
        (0x1A4C260, DurabilityPolicyScale), (0x1A4B79C, BaseMoraleMaximum),
        (0x1A4B7A8, MoralePolicyScale), (0x1344C4C, BitConverter.SingleToInt32Bits(PercentScale)),
        (0x1A4C644, DamageTierFirstThreshold), (0x1A4C650, DamageTierSecondThreshold),
        (0x1A4C65C, DamageTierThirdThreshold)
    ];
    // Existing parameter dictionaries and saves keep their numeric key identity.
    public static IReadOnlyList<int> ParameterRvas { get; } =
        Array.AsReadOnly(Defaults.Select(field => field.Key).ToArray());
    private static readonly OriginalBaseParameterCatalog Catalog = new(
        new ReadOnlyDictionary<int, int>(Defaults.ToDictionary(field => field.Key, field => field.Value)),
        ImplementationVersion) { RuntimeTroopsExtraInitial = 0 };

    public static OriginalBaseParameterCatalog ReadDefaults(string locale)
    {
        if (locale is not ("SC" or "TC"))
            throw new NotSupportedException("CE base defaults support SC and TC only.");
        return Catalog;
    }
}
