namespace San14.Core;

/// <summary>Only Available calculations may change stocks or current development.</summary>
public enum PeriodEconomyCalculationStatus { Available, Unavailable, PartialCurrentContext }

/// <summary>A resolved result of one period operation. Unknown inputs are named,
/// and partial calculations never carry a payable or creditable amount.</summary>
public sealed record PeriodEconomyQuote(PeriodEconomyCalculationStatus Status, string RuleId,
    string[] MissingInputs)
{
    public int GoldCost { get; init; }
    public int SuppliesCost { get; init; }
    public int GoldIncome { get; init; }
    public int SuppliesIncome { get; init; }
    public int TroopsIncome { get; init; }
    public int MoraleIncrease { get; init; }
    public int? NextMorale { get; init; }
    public AreaDevelopmentValues? NextDevelopment { get; init; }
    public int? NextSecurity { get; init; }
}

/// <summary>Current-state providers for development, training and recruitment.
/// Source coefficients and officer base attributes alone do not establish a result.</summary>
public interface IPlayablePeriodEconomyRules
{
    PeriodEconomyQuote QuoteDevelopment(GameState state, CityState city,
        AreaState area, OfficerState officer);
    PeriodEconomyQuote QuoteTraining(GameState state, CityState city, OfficerState officer);
    PeriodEconomyQuote QuoteRecruitment(GameState state, CityState city,
        OfficerState officer, int preparationPeriods);
}

public sealed class UnresolvedPlayablePeriodEconomyRules : IPlayablePeriodEconomyRules
{
    public static UnresolvedPlayablePeriodEconomyRules Instance { get; } = new();
    private UnresolvedPlayablePeriodEconomyRules() { }
    private static PeriodEconomyQuote Missing(string operation) => new(
        PeriodEconomyCalculationStatus.Unavailable, "san14:economy-unresolved",
        [$"{operation}.source_rule"]);
    public PeriodEconomyQuote QuoteDevelopment(GameState state, CityState city,
        AreaState area, OfficerState officer) => Missing("development");
    public PeriodEconomyQuote QuoteTraining(GameState state, CityState city,
        OfficerState officer) => Missing("training");
    public PeriodEconomyQuote QuoteRecruitment(GameState state, CityState city,
        OfficerState officer, int preparationPeriods) => Missing("recruitment");
}
