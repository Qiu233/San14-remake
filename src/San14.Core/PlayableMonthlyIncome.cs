namespace San14.Core;

/// <summary>Compatibility calculation interface. Original providers also expose
/// status and a receipt through IPlayableMonthlyIncomeQuoteRules.</summary>
public interface IPlayableMonthlyIncomeRules
{
    (int Gold, int Supplies) Quote(CityState city,
        IReadOnlyDictionary<int, AreaState> areas);
}

public sealed record EconomyCalculationInput(string Name, double? Value);
public sealed record EconomyCalculationStep(string Name, string Expression, double Value);
public sealed record MonthlyIncomeAreaReceipt(int AreaId,
    PeriodEconomyCalculationStatus Status, EconomyCalculationInput[] Inputs,
    EconomyCalculationStep[] Steps, string[] MissingInputs);

/// <summary>Source-backed calculated amounts or a named incomplete context.
/// Partial steps are evidence only and cannot be credited to city stocks.</summary>
public sealed record MonthlyIncomeQuote(PeriodEconomyCalculationStatus Status, string RuleId,
    string[] MissingInputs, MonthlyIncomeAreaReceipt[] Areas)
{
    public int Gold { get; init; }
    public int Supplies { get; init; }
    public string? RuleVersion { get; init; }
    public int? SettlementMonth { get; init; }
    public int? CityId { get; init; }
    public int? GrossGold { get; init; }
    public int? GrossSupplies { get; init; }
    public int? GoldCapacity { get; init; }
    public int? SuppliesCapacity { get; init; }
    public int? CurrentGoldStock { get; init; }
    public int? CurrentSuppliesStock { get; init; }
    public int? NextGoldStock { get; init; }
    public int? NextSuppliesStock { get; init; }
}

public interface IPlayableMonthlyIncomeQuoteRules : IPlayableMonthlyIncomeRules
{
    MonthlyIncomeQuote Quote(GameState state, CityState city);
}

public sealed class UnresolvedMonthlyIncomeRules : IPlayableMonthlyIncomeQuoteRules
{
    public static UnresolvedMonthlyIncomeRules Instance { get; } = new();
    private UnresolvedMonthlyIncomeRules() { }
    public (int Gold, int Supplies) Quote(CityState city,
        IReadOnlyDictionary<int, AreaState> areas) => (0, 0);
    public MonthlyIncomeQuote Quote(GameState state, CityState city) => new(
        PeriodEconomyCalculationStatus.Unavailable, "san14:monthly-income-unresolved",
        ["monthly_income.source_rule"], []) { SettlementMonth = state.Date.Next().Month };
}

/// <summary>Session-bound percentages. Player control is supplied by the host,
/// not persisted in the shared world state.</summary>
public interface IPlayableMonthlyIncomeRates
{
    (int GoldPercent, int SuppliesPercent) ForForce(int forceId);
}

/// <summary>Temporary area-based amounts; original income arithmetic is still unresolved.</summary>
public sealed class ProvisionalAreaMonthlyIncomeRules(
    IPlayableMonthlyIncomeRates? rates = null) : IPlayableMonthlyIncomeRules
{
    public (int Gold, int Supplies) Quote(CityState city,
        IReadOnlyDictionary<int, AreaState> areas)
    {
        int controlled = city.AreaIds.Count(id => areas.TryGetValue(id, out AreaState? area) &&
            area.ForceId == city.ForceId);
        (int goldPercent, int suppliesPercent) = rates?.ForForce(city.ForceId ?? 0)
            ?? (100, 100);
        if (goldPercent < 0 || suppliesPercent < 0)
            throw new InvalidDataException("Monthly income percentages cannot be negative.");
        // The temporary area bases (300/800) are divisible by 100. Applying
        // the known source percentage here agrees with per-area truncation for
        // this provisional rule, without claiming the base-income formula.
        return (checked((int)((long)controlled * 300 * goldPercent / 100)),
            checked((int)((long)controlled * 800 * suppliesPercent / 100)));
    }
}

public static class PlayableMonthlyIncome
{
    public static MonthlyIncomeQuote[] Settle(GameState state,
        Dictionary<int, CityState> cities, IReadOnlyDictionary<int, AreaState> areas,
        IPlayableMonthlyIncomeRules rules, List<PeriodReportEntry> reports)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (rules is not IPlayableMonthlyIncomeQuoteRules sourceRules)
        {
            Settle(cities, areas, rules, reports);
            return [];
        }
        var receipts = new List<MonthlyIncomeQuote>();
        GameState currentState = state with { Cities = cities, Areas = areas.ToDictionary() };
        foreach (CityState current in cities.Values.OrderBy(city => city.Id).ToArray())
        {
            if (current.ForceId is not int forceId) continue;
            MonthlyIncomeQuote quote = sourceRules.Quote(currentState, current);
            ValidateQuote(quote);
            quote = quote with { CityId = current.Id };
            receipts.Add(quote);
            if (quote.Status != PeriodEconomyCalculationStatus.Available)
            {
                reports.Add(new PeriodReportEntry("monthly_income_unavailable", forceId, current.Id)
                { RuleId = quote.RuleId, MissingRuleKeys = quote.MissingInputs });
                continue;
            }
            Credit(cities, current, forceId, quote.Gold, quote.Supplies, reports);
        }
        return receipts.ToArray();
    }

    public static void Settle(Dictionary<int, CityState> cities,
        IReadOnlyDictionary<int, AreaState> areas, IPlayableMonthlyIncomeRules rules,
        List<PeriodReportEntry> reports)
    {
        foreach (CityState current in cities.Values.OrderBy(city => city.Id).ToArray())
        {
            if (current.ForceId is not int forceId) continue;
            (int gold, int supplies) = rules.Quote(current, areas);
            Credit(cities, current, forceId, gold, supplies, reports);
        }
    }

    private static void Credit(Dictionary<int, CityState> cities, CityState current,
        int forceId, int gold, int supplies, List<PeriodReportEntry> reports)
    {
        if (gold < 0 || supplies < 0)
            throw new InvalidDataException($"Monthly income for city {current.Id} is negative.");
        cities[current.Id] = current with
        {
            Gold = checked(current.Gold + gold), Supplies = checked(current.Supplies + supplies)
        };
        if (gold > 0) reports.Add(new PeriodReportEntry("gold_income", forceId, current.Id, Amount: gold));
        if (supplies > 0) reports.Add(new PeriodReportEntry("supplies_income", forceId, current.Id, Amount: supplies));
    }

    public static void ValidateQuote(MonthlyIncomeQuote? quote)
    {
        if (quote is null || string.IsNullOrWhiteSpace(quote.RuleId) || quote.MissingInputs is null ||
            quote.Areas is null || !Enum.IsDefined(quote.Status) || quote.Gold < 0 || quote.Supplies < 0 ||
            quote.CityId is <= 0 || quote.SettlementMonth is < 1 or > 12 ||
            quote.GrossGold < 0 || quote.GrossSupplies < 0 || quote.GoldCapacity < 0 ||
            quote.SuppliesCapacity < 0 || quote.CurrentGoldStock < 0 || quote.CurrentSuppliesStock < 0 ||
            quote.NextGoldStock < 0 || quote.NextSuppliesStock < 0 ||
            (quote.RuleVersion is { } ruleVersion && string.IsNullOrWhiteSpace(ruleVersion)) ||
            quote.MissingInputs.Any(string.IsNullOrWhiteSpace) ||
            quote.MissingInputs.Distinct().Count() != quote.MissingInputs.Length ||
            (quote.Status != PeriodEconomyCalculationStatus.Available &&
                (quote.Gold != 0 || quote.Supplies != 0)) ||
            (quote.Status == PeriodEconomyCalculationStatus.Available && quote.MissingInputs.Length != 0) ||
            (quote.Status != PeriodEconomyCalculationStatus.Available && quote.MissingInputs.Length == 0))
            throw new InvalidDataException("Monthly income quote is incomplete or credits an unresolved calculation.");
        var areaIds = new HashSet<int>();
        foreach (MonthlyIncomeAreaReceipt? area in quote.Areas)
        {
            if (area is null || area.AreaId <= 0 || !areaIds.Add(area.AreaId) ||
                !Enum.IsDefined(area.Status) || area.Inputs is null || area.Steps is null ||
                area.MissingInputs is null || area.MissingInputs.Any(string.IsNullOrWhiteSpace) ||
                area.MissingInputs.Distinct().Count() != area.MissingInputs.Length ||
                (area.Status == PeriodEconomyCalculationStatus.Available && area.MissingInputs.Length > 0) ||
                (area.Status != PeriodEconomyCalculationStatus.Available && area.MissingInputs.Length == 0) ||
                area.Inputs.Any(input => input is null || string.IsNullOrWhiteSpace(input.Name) ||
                    input.Value is double value && !double.IsFinite(value)) ||
                area.Inputs.Select(input => input.Name).Distinct().Count() != area.Inputs.Length ||
                area.Steps.Any(step => step is null || string.IsNullOrWhiteSpace(step.Name) ||
                    string.IsNullOrWhiteSpace(step.Expression) || !double.IsFinite(step.Value)) ||
                area.Steps.Select(step => step.Name).Distinct().Count() != area.Steps.Length)
                throw new InvalidDataException("Monthly income area receipt is incomplete or has invalid calculation evidence.");
        }
        if (quote.Status == PeriodEconomyCalculationStatus.Available &&
            quote.Areas.Any(area => area.Status != PeriodEconomyCalculationStatus.Available))
            throw new InvalidDataException("Monthly income credits an unresolved area receipt.");
        if ((quote.NextGoldStock.HasValue && !quote.CurrentGoldStock.HasValue) ||
            (quote.NextSuppliesStock.HasValue && !quote.CurrentSuppliesStock.HasValue) ||
            (quote.NextGoldStock.HasValue && quote.NextGoldStock.Value - (long)quote.CurrentGoldStock!.Value != quote.Gold) ||
            (quote.NextSuppliesStock.HasValue && quote.NextSuppliesStock.Value - (long)quote.CurrentSuppliesStock!.Value != quote.Supplies) ||
            (quote.NextGoldStock.HasValue && quote.GoldCapacity.HasValue && quote.NextGoldStock > quote.GoldCapacity) ||
            (quote.NextSuppliesStock.HasValue && quote.SuppliesCapacity.HasValue && quote.NextSuppliesStock > quote.SuppliesCapacity) ||
            (quote.Status != PeriodEconomyCalculationStatus.Available &&
                (quote.NextGoldStock.HasValue || quote.NextSuppliesStock.HasValue)))
            throw new InvalidDataException("Monthly income stock receipt disagrees with its credited amounts.");
    }
}
