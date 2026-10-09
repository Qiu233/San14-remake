using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data.Formats;

public sealed record OriginalBaseParameterCatalog(IReadOnlyDictionary<string, int> Values, string ProfileId)
{
    public int? RuntimeTroopsExtraInitial { get; init; }
}

/// <summary>Owned CE base-rule defaults, keyed by simulation meaning.</summary>
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

    private static readonly (string Key, int Value)[] Defaults =
    [
        (SimulationParameterKeys.BaseCapacity.SpecialGoldCapacity, SpecialGoldCapacity), (SimulationParameterKeys.BaseCapacity.SpecialFoodCapacity, SpecialFoodCapacity),
        (SimulationParameterKeys.BaseCapacity.SpecialTroopCapacity, SpecialTroopCapacity), (SimulationParameterKeys.BaseCapacity.MaximumDurability, MaximumDurability),
        (SimulationParameterKeys.BaseCapacity.GoldCapacityPerCitySize, GoldCapacityPerCitySize), (SimulationParameterKeys.BaseCapacity.FoodCapacityPerCitySize, FoodCapacityPerCitySize),
        (SimulationParameterKeys.BaseCapacity.BaseTroopCapacity, BaseTroopCapacity), (SimulationParameterKeys.BaseCapacity.MediumCityExtraTroops, MediumCityExtraTroops),
        (SimulationParameterKeys.BaseCapacity.LargeCityExtraTroops, LargeCityExtraTroops), (SimulationParameterKeys.BaseCapacity.MetropolisExtraTroops, MetropolisExtraTroops),
        (SimulationParameterKeys.BaseCapacity.BaseDurability, BaseDurability), (SimulationParameterKeys.BaseCapacity.DurabilityPerAdditionalSize, DurabilityPerAdditionalSize),
        (SimulationParameterKeys.BaseCapacity.DurabilityPolicyScale, DurabilityPolicyScale), (SimulationParameterKeys.Morale.Maximum, BaseMoraleMaximum),
        (SimulationParameterKeys.Morale.PolicyScale, MoralePolicyScale), (SimulationParameterKeys.Shared.PercentDenominator, BitConverter.SingleToInt32Bits(PercentScale)),
        (SimulationParameterKeys.BaseCapacity.DamageTierFirstThreshold, DamageTierFirstThreshold), (SimulationParameterKeys.BaseCapacity.DamageTierSecondThreshold, DamageTierSecondThreshold),
        (SimulationParameterKeys.BaseCapacity.DamageTierThirdThreshold, DamageTierThirdThreshold)
    ];
    public static IReadOnlyList<string> ParameterKeys { get; } =
        Array.AsReadOnly(Defaults.Select(field => field.Key).ToArray());
    private static readonly OriginalBaseParameterCatalog Catalog = new(
        new ReadOnlyDictionary<string, int>(Defaults.ToDictionary(field => field.Key, field => field.Value)),
        ImplementationVersion) { RuntimeTroopsExtraInitial = 0 };

    public static OriginalBaseParameterCatalog ReadDefaults(string locale)
    {
        if (locale is not ("SC" or "TC"))
            throw new NotSupportedException("CE base defaults support SC and TC only.");
        return Catalog;
    }
}
