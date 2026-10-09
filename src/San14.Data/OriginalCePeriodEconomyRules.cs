using San14.Core;

namespace San14.Data;

/// <summary>Source-bound gates for period economy operations whose final expressions
/// are still under investigation. No unverified amount can pass as an available result.</summary>
public sealed class OriginalCePeriodEconomyRules : IPlayablePeriodEconomyRules
{
    public const string RuleId = "san14:ce-period-economy-source-20261002";
    public string RuleVersion { get; }
    public string Locale { get; }

    private OriginalCePeriodEconomyRules(string version, string locale) =>
        (RuleVersion, Locale) = (version, locale);

    public static OriginalCePeriodEconomyRules LoadCurrent(string sourceDirectory, string locale) =>
        new(SimulationRuleIdentity.ForLocale(locale), locale);

    public PeriodEconomyQuote QuoteDevelopment(GameState state, CityState city,
        AreaState area, OfficerState officer)
    {
        var missing = new List<string>();
        if (area.EconomySource is null) missing.Add("development.static_area_source");
        if (area.Development is null) missing.Add("development.current_progress");
        if (area.Security is null) missing.Add("development.current_security");
        missing.AddRange(["development.effective_officer_context", "development.growth_expression",
            "development.final_cost_expression", "development.unfunded_outcome"]);
        return new(area.EconomySource is null ? PeriodEconomyCalculationStatus.Unavailable
            : PeriodEconomyCalculationStatus.PartialCurrentContext, RuleId, missing.ToArray());
    }

    public PeriodEconomyQuote QuoteTraining(GameState state, CityState city, OfficerState officer)
    {
        var missing = new List<string>();
        if (city.Morale is null) missing.Add("training.current_morale");
        missing.AddRange(["training.effective_officer_context", "training.morale_expression",
            "training.final_cost_expression", "training.unfunded_outcome"]);
        return new(PeriodEconomyCalculationStatus.PartialCurrentContext, RuleId, missing.ToArray());
    }

    public PeriodEconomyQuote QuoteRecruitment(GameState state, CityState city,
        OfficerState officer, int preparationPeriods)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(preparationPeriods);
        return new(PeriodEconomyCalculationStatus.PartialCurrentContext, RuleId,
            ["recruitment.area_current_inputs", "recruitment.effective_officer_context",
             "recruitment.preparation_expression", "recruitment.final_cost_expression",
             "recruitment.capacity", "recruitment.morale_expression", "recruitment.unfunded_outcome"]);
    }
}
