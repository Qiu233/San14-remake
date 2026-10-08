using San14.Core;

namespace San14.Data;

/// <summary>Current native query results. Null stays unknown. In particular,
/// progress3C/3E must not be taken from static fixed coefficients or a UI level.</summary>
public sealed record OriginalCeAreaIncomeCurrentContext
{
    public bool? NativeAreaValid { get; init; }
    public int? RepresentativeForceSlot { get; init; }
    public bool? AlternativeBranch { get; init; }
    public ushort? RawField44 { get; init; }
    public ushort? RawField46 { get; init; }
    public ushort? RawField52 { get; init; }
    public ushort? Progress3C { get; init; }
    public ushort? Progress3E { get; init; }
    public byte? Bonus6D { get; init; }
    public ushort? OwnedCells68 { get; init; }
    public ushort? TerrainCells76 { get; init; }
    public int? EffectivePerson2 { get; init; }
    public int? EffectivePerson3 { get; init; }
    public bool? NativePersonValid { get; init; }
    public int? PersonTaskPercent { get; init; }
    public int? PersonEffect24 { get; init; }
    public int? PersonEffect25 { get; init; }
    public int? ForceEffect35 { get; init; }
    public int? ForceEffect34 { get; init; }
    public byte? AreaFlags38 { get; init; }
}

public sealed record OriginalCeCityIncomeCurrentContext(bool? NativeCityValid,
    byte? CapacityScaleB8, bool? SpecialCapacityPredicate)
{
    public bool? NativeOwnerForceValid { get; init; }
}

/// <summary>Read current inputs without mutating the simulation or importing a
/// live process. A host may bind a fully replayed native initialization context.</summary>
public interface IOriginalCeMonthlyIncomeContext
{
    OriginalCeAreaIncomeCurrentContext? ReadArea(GameState state, CityState city, AreaState area);
    OriginalCeCityIncomeCurrentContext? ReadCity(GameState state, CityState city);
}

/// <summary>Source-backed monthly settlement. Static source words are available
/// immediately; unproved runtime initialization stays PartialCurrentContext.
/// Current SC ordinary arithmetic is supported. TC arithmetic awaits its own audit.</summary>
public sealed class OriginalCeMonthlyIncomeRules : IPlayableMonthlyIncomeQuoteRules
{
    private readonly OriginalCeDifficultyIncomeRates _rates;
    private readonly IOriginalCeMonthlyIncomeContext? _context;
    private readonly bool _arithmeticAvailable;
    private OriginalCeMonthlyIncomeRules(OriginalCeDifficultyIncomeRates rates,
        IOriginalCeMonthlyIncomeContext? context)
    {
        (_rates, _context, _arithmeticAvailable) = (rates, context, rates.Locale == "SC");
    }

    public static OriginalCeMonthlyIncomeRules LoadCurrent(string sourceDirectory, string locale,
        string difficultyId, IReadOnlySet<int> humanForceIds,
        IOriginalCeMonthlyIncomeContext? context = null) => new(
            OriginalCeDifficultyIncomeRates.LoadCurrent(sourceDirectory, locale, difficultyId, humanForceIds), context);

    // The compatibility interface has no date/current modifier context. Refuse to
    // turn source inputs into a stock credit when a legacy caller bypasses receipts.
    public (int Gold, int Supplies) Quote(CityState city,
        IReadOnlyDictionary<int, AreaState> areas) => (0, 0);

    public MonthlyIncomeQuote Quote(GameState state, CityState city)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(city);
        int month = state.Date.Next().Month;
        var missing = new List<string>();
        var receipts = new List<MonthlyIncomeAreaReceipt>();
        if (!_arithmeticAvailable) missing.Add("monthly_income.locale_arithmetic");
        if (state.DifficultyId != _rates.DifficultyId) missing.Add("monthly_income.bound_difficulty");
        if (state.Date.Day != 21) missing.Add("monthly_income.month_boundary");
        if (city.ForceId is not int force)
            return Result(PeriodEconomyCalculationStatus.Available, [], [], month);
        OriginalCeCityIncomeCurrentContext? capacity = _context?.ReadCity(state, city);
        if (capacity?.NativeCityValid == false || capacity?.NativeOwnerForceValid == false)
            return Result(PeriodEconomyCalculationStatus.Available, [], [], month);
        (int goldPercent, int suppliesPercent) = _rates.ForForce(force);
        int totalGold = 0, totalSupplies = 0;
        foreach (int areaId in city.AreaIds)
        {
            if (!state.Areas.TryGetValue(areaId, out AreaState? area))
            {
                missing.Add($"area.{areaId}.current_record");
                continue;
            }
            (MonthlyIncomeAreaReceipt receipt, int gold, int supplies) =
                QuoteArea(state, city, area, month, goldPercent, suppliesPercent);
            receipts.Add(receipt);
            missing.AddRange(receipt.MissingInputs.Select(key => $"area.{area.Id}.{key}"));
            totalGold = unchecked(totalGold + gold);
            totalSupplies = unchecked(totalSupplies + supplies);
        }
        if (capacity?.NativeCityValid is null) missing.Add("city.native_validity");
        if (capacity?.NativeOwnerForceValid is null) missing.Add("city.owner_force_native_validity");
        if (capacity?.CapacityScaleB8 is null) missing.Add("city.capacity_scale_b8");
        if (capacity?.SpecialCapacityPredicate is null) missing.Add("city.special_capacity_predicate");
        if (totalGold < 0 || totalSupplies < 0) missing.Add("city.negative_income_result");
        if (missing.Count > 0)
            return Result(city.AreaIds.Any(id => state.Areas.TryGetValue(id, out AreaState? area) && area.EconomySource is not null)
                    ? PeriodEconomyCalculationStatus.PartialCurrentContext : PeriodEconomyCalculationStatus.Unavailable,
                missing.Distinct().ToArray(), receipts.ToArray(), month);
        bool specialCapacity = capacity!.SpecialCapacityPredicate!.Value;
        byte capacityScale = capacity.CapacityScaleB8!.Value;
        int goldCapacity = specialCapacity ? 250000 : 50000 * capacityScale;
        int suppliesCapacity = specialCapacity ? 500000 : 100000 * capacityScale;
        int goldStock = Math.Clamp(unchecked(city.Gold + totalGold), 0, goldCapacity);
        int suppliesStock = Math.Clamp(unchecked(city.Supplies + totalSupplies), 0, suppliesCapacity);
        if (goldStock < city.Gold || suppliesStock < city.Supplies)
            return Result(PeriodEconomyCalculationStatus.PartialCurrentContext,
                ["city.stock_reduction_requires_resource_posting"], receipts.ToArray(), month);
        return Result(PeriodEconomyCalculationStatus.Available, [], receipts.ToArray(), month) with
        {
            Gold = goldStock - city.Gold, Supplies = suppliesStock - city.Supplies,
            GrossGold = totalGold, GrossSupplies = totalSupplies,
            GoldCapacity = goldCapacity, SuppliesCapacity = suppliesCapacity,
            CurrentGoldStock = city.Gold, CurrentSuppliesStock = city.Supplies,
            NextGoldStock = goldStock, NextSuppliesStock = suppliesStock
        };
    }

    private MonthlyIncomeQuote Result(PeriodEconomyCalculationStatus status, string[] missing,
        MonthlyIncomeAreaReceipt[] receipts, int month) => new(status,
            OriginalCeAreaIncomeMath.FormulaId, missing, receipts)
        { SourceExecutableSha256 = _rates.SourceExecutableSha256, SettlementMonth = month };

    private (MonthlyIncomeAreaReceipt Receipt, int Gold, int Supplies) QuoteArea(GameState state,
        CityState city, AreaState area, int month, int goldPercent, int suppliesPercent)
    {
        AreaEconomySourceInputs? source = area.EconomySource;
        OriginalCeAreaIncomeCurrentContext current = _context?.ReadArea(state, city, area) ?? new();
        var inputs = new EconomyCalculationInput[]
        {
            new("source.raw44", source?.RawField44), new("source.raw46", source?.RawField46),
            new("source.raw52", source?.RawField52), new("current.security54", area.Security),
            new("current.raw44", current.RawField44), new("current.raw46", current.RawField46),
            new("current.raw52", current.RawField52),
            new("current.progress3c", current.Progress3C), new("current.progress3e", current.Progress3E),
            new("current.bonus6d", current.Bonus6D), new("current.owned68", current.OwnedCells68),
            new("current.terrain76", current.TerrainCells76), new("current.effective_person2", current.EffectivePerson2),
            new("current.effective_person3", current.EffectivePerson3), new("current.person_effect24", current.PersonEffect24),
            new("current.person_native_validity", current.NativePersonValid is bool personValid ? personValid ? 1 : 0 : null),
            new("current.person_task_percent", current.PersonTaskPercent),
            new("current.person_effect25", current.PersonEffect25), new("current.force_effect35", current.ForceEffect35),
            new("current.force_effect34", current.ForceEffect34), new("settlement.month", month),
            new("current.flags38", current.AreaFlags38),
            new("current.native_validity", current.NativeAreaValid is bool valid ? valid ? 1 : 0 : null),
            new("current.representative_force_slot", current.RepresentativeForceSlot),
            new("current.alternative_branch", current.AlternativeBranch is bool alternate ? alternate ? 1 : 0 : null),
            new("difficulty.gold_percent", goldPercent), new("difficulty.supplies_percent", suppliesPercent)
        };
        var missing = inputs.Where(input => input.Value is null).Select(input => input.Name).ToList();
        if (current.NativeAreaValid is null) missing.Add("native_validity");
        if (current.RepresentativeForceSlot is null) missing.Add("representative_force_slot");
        if (current.AlternativeBranch is null) missing.Add("alternative_branch");
        if (current.AlternativeBranch == true) missing.Add("alternative_branch.arithmetic");
        if (!_arithmeticAvailable) missing.Add("locale_arithmetic");
        if (current.NativeAreaValid == false ||
            current.RepresentativeForceSlot is int representative && representative != city.ForceId)
            return (new(area.Id, PeriodEconomyCalculationStatus.Available, inputs,
                [new("native_zero", "invalid area or city force differs from representative force", 0)], []), 0, 0);
        if (missing.Count > 0)
            return (new(area.Id, source is null ? PeriodEconomyCalculationStatus.Unavailable
                : PeriodEconomyCalculationStatus.PartialCurrentContext, inputs, [], missing.Distinct().ToArray()), 0, 0);
        var input = new OriginalCeAreaIncomeInput(current.RawField44!.Value,
            current.RawField46!.Value, current.RawField52!.Value,
            checked((byte)area.Security!.Value), current.Progress3C!.Value, current.Progress3E!.Value,
            current.Bonus6D!.Value, current.OwnedCells68!.Value, current.TerrainCells76!.Value,
            current.EffectivePerson2!.Value, current.EffectivePerson3!.Value,
            current.PersonEffect24!.Value, current.PersonEffect25!.Value,
            current.ForceEffect35!.Value, current.ForceEffect34!.Value, month, current.AreaFlags38!.Value,
            goldPercent, suppliesPercent, current.PersonTaskPercent!.Value, current.NativePersonValid!.Value);
        OriginalCeAreaIncomeStages stages = OriginalCeAreaIncomeMath.Calculate(input);
        EconomyCalculationStep[] steps =
        [
            new("gold.level", "progress3c<100?1:<300?2:<600?3:<1000?4:5", stages.GoldLevel),
            new("supplies.level", "progress3e<100?1:<300?2:<600?3:<1000?4:5", stages.SuppliesLevel),
            new("gold.multiplier", "level1..5:[10,12,14,16,18]", stages.GoldLevelMultiplier),
            new("supplies.multiplier", "level1..5:[10,13,16,18,20]", stages.SuppliesLevelMultiplier),
            new("gold.person_contribution", "native_valid?max(1,trunc(effective_person2*task_percent/100)):0", stages.GoldPersonContribution),
            new("supplies.person_contribution", "native_valid?max(1,trunc(effective_person3*task_percent/100)):0", stages.SuppliesPersonContribution),
            new("gold.n", "100+trunc(10*person_contribution/100)+10*bonus6d", stages.GoldN),
            new("supplies.n", "100+trunc(10*person_contribution/100)+10*bonus6d", stages.SuppliesN),
            new("gold.q", "trunc(N * multiplier * current.raw44 / 1000)", stages.GoldQ),
            new("supplies.q", "trunc(N * multiplier * current.raw46 / 1000)", stages.SuppliesQ),
            new("gold.v", "f32((sqrt(raw52)*max(security54,50)/100 + Q)*owned68/max(terrain76,1))", stages.GoldV),
            new("supplies.v", "f32((sqrt(raw52)*max(security54,50)/100 + Q)*owned68/max(terrain76,1))", stages.SuppliesV),
            new("gold.midstage", "trunc(trunc(V)*65/100)", stages.GoldMidstage),
            new("supplies.midstage", "trunc(trunc(V)*30/100)", stages.SuppliesMidstage),
            new("gold.person_effect", "mid + trunc(mid*person_effect24/100)", stages.GoldAfterPersonEffect),
            new("supplies.person_effect", "mid + trunc(mid*person_effect25/100)", stages.SuppliesAfterPersonEffect),
            new("gold.force_effect", "trunc(person*(100+force_effect35)/100)", stages.GoldAfterForceEffect),
            new("supplies.force_effect", "trunc(person*(100+force_effect34)/100)", stages.SuppliesAfterForceEffect),
            new("supplies.season", "months7..9: trunc(force*125*(flags38.bit4?150:100)/10000)", stages.SuppliesAfterSeason),
            new("gold.result", "trunc(force*difficulty_gold_percent/100)", stages.Gold),
            new("supplies.result", "trunc(season*difficulty_supplies_percent/100)", stages.Supplies)
        ];
        return (new(area.Id, PeriodEconomyCalculationStatus.Available, inputs, steps, []), stages.Gold, stages.Supplies);
    }
}
