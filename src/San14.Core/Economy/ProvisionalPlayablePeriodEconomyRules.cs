namespace San14.Core;

/// <summary>Former skeleton amounts, explicitly provisional and independent of native economy formulas.</summary>
public sealed class ProvisionalPlayablePeriodEconomyRules : IPlayablePeriodEconomyRules
{
    public static ProvisionalPlayablePeriodEconomyRules Instance { get; } = new();
    public const string Id = "san14:provisional-period-economy-v1";
    private ProvisionalPlayablePeriodEconomyRules() { }
    private static PeriodEconomyQuote Known() => new(PeriodEconomyCalculationStatus.Available, Id, []);

    public PeriodEconomyQuote QuoteDevelopment(GameState state, CityState city, AreaState area, OfficerState officer) =>
        Known() with
        {
            GoldCost = 50,
            GoldIncome = area.Administration?.DevelopmentFocus == AreaDevelopmentFocus.Commerce ? 10 : 0,
            SuppliesIncome = area.Administration?.DevelopmentFocus == AreaDevelopmentFocus.Agriculture ? 50 : 0,
            TroopsIncome = area.Administration?.DevelopmentFocus == AreaDevelopmentFocus.Barracks ? 20 : 0
        };

    public PeriodEconomyQuote QuoteTraining(GameState state, CityState city, OfficerState officer) =>
        Known() with { GoldCost = 300, MoraleIncrease = city.Morale is int morale ? Math.Max(0, Math.Min(5, 100 - morale)) : 0 };

    public PeriodEconomyQuote QuoteRecruitment(GameState state, CityState city, OfficerState officer, int preparationPeriods)
    {
        int controlled = city.AreaIds.Count(id => state.Areas[id].ForceId == city.ForceId);
        int recruits = (int)Math.Min(200L * preparationPeriods, int.MaxValue - (long)city.Troops);
        int total = checked(city.Troops + recruits);
        return Known() with
        {
            GoldCost = recruits > 0 ? checked(controlled * 150) : 0, TroopsIncome = recruits,
            NextMorale = city.Morale is int morale && recruits > 0
                ? (int)(((long)city.Troops * morale + (long)recruits * 50) / total) : null
        };
    }
}

/// <summary>Settles provider quotes against current stocks in stable area/city order.</summary>
internal static class PlayablePeriodEconomySettlement
{
    internal static void Settle(GameState state, Dictionary<int, CityState> cities,
        Dictionary<int, AreaState> areas, IReadOnlyDictionary<int, OfficerState> officers,
        IPlayablePeriodEconomyRules rules, List<PeriodReportEntry> reports)
    {
        GameState Current() => state with { Cities = cities, Areas = areas, Officers = officers.ToDictionary() };
        foreach (AreaState area in areas.Values.OrderBy(area => area.Id).ToArray())
        {
            if (area.Administration is not { OverseerId: int officerId, DevelopmentFocus: not null } ||
                area.ForceId is not int forceId || !cities.TryGetValue(area.CityId, out CityState? city) ||
                city.ForceId != forceId || !officers.TryGetValue(officerId, out OfficerState? officer) ||
                officer.ForceId != forceId || officer.LocationId != city.Id ||
                officer.Presence != OfficerPresence.Present || officer.Deployed ||
                PlayableOfficerAvailability.IsSourceBusy(state, officer)) continue;
            // Army entry has already updated the commander's current base. The
            // former appointment is cleared at publication and cannot authorize
            // development in a different city during this intermediate state.
            PeriodEconomyQuote quote = rules.QuoteDevelopment(Current(), city, area, officer);
            if (!TryQuote(quote, "development", forceId, city, officerId, reports)) continue;
            cities[city.Id] = Apply(city, quote);
            if (quote.NextDevelopment is not null || quote.NextSecurity is not null)
                areas[area.Id] = area with
                {
                    Development = quote.NextDevelopment is { } nextDevelopment
                        ? new AreaDevelopmentValues(nextDevelopment.Agriculture ?? area.Development?.Agriculture,
                            nextDevelopment.Commerce ?? area.Development?.Commerce,
                            nextDevelopment.Barracks ?? area.Development?.Barracks)
                        : area.Development,
                    Security = quote.NextSecurity ?? area.Security
                };
            Add("development_cost", quote.GoldCost);
            Add("development_supplies_cost", quote.SuppliesCost);
            Add("commerce", quote.GoldIncome);
            Add("agriculture", quote.SuppliesIncome);
            Add("barracks", quote.TroopsIncome);
            Add("development_morale", quote.MoraleIncrease);
            void Add(string kind, int amount)
            { if (amount > 0) reports.Add(Entry(kind, forceId, city.Id, officerId, amount, quote)); }
        }

        foreach (CityState existing in cities.Values.OrderBy(city => city.Id).ToArray())
        {
            if (existing.ForceId is not int forceId || existing.Duties is not { } duties) continue;
            CityState city = existing;
            bool Working(int? id) => id is int officerId && officers.TryGetValue(officerId, out OfficerState? officer) &&
                officer.ForceId == forceId && officer.LocationId == city.Id &&
                officer.Presence == OfficerPresence.Present && !officer.Deployed &&
                !PlayableOfficerAvailability.IsSourceBusy(state, officer);
            bool recruiting = Working(duties.RecruiterId);
            int preparation = recruiting ? Math.Min(3, duties.PreparationPeriods + 1) : 0;
            if (Working(duties.TrainerId))
            {
                PeriodEconomyQuote quote = rules.QuoteTraining(Current(), city, officers[duties.TrainerId!.Value]);
                if (TryQuote(quote, "training", forceId, city, duties.TrainerId.Value, reports))
                {
                    city = Apply(city, quote);
                    if (quote.GoldCost > 0) reports.Add(Entry("training_cost", forceId, city.Id, duties.TrainerId, quote.GoldCost, quote));
                    if (quote.MoraleIncrease > 0) reports.Add(Entry("training", forceId, city.Id, duties.TrainerId, quote.MoraleIncrease, quote));
                    cities[city.Id] = city;
                }
            }
            if (state.Date.Day == 21)
            {
                if (recruiting && preparation > 0)
                {
                    PeriodEconomyQuote quote = rules.QuoteRecruitment(Current(), city, officers[duties.RecruiterId!.Value], preparation);
                    if (TryQuote(quote, "recruitment", forceId, city, duties.RecruiterId.Value, reports))
                    {
                        city = Apply(city, quote);
                        if (quote.GoldCost > 0) reports.Add(Entry("recruit_cost", forceId, city.Id, duties.RecruiterId, quote.GoldCost, quote));
                        if (quote.TroopsIncome > 0) reports.Add(Entry("recruit", forceId, city.Id, duties.RecruiterId, quote.TroopsIncome, quote));
                    }
                }
                preparation = 0;
            }
            cities[city.Id] = city with { Duties = duties with { PreparationPeriods = preparation } };
        }
    }

    private static bool TryQuote(PeriodEconomyQuote quote, string operation, int forceId,
        CityState city, int officerId, List<PeriodReportEntry> reports)
    {
        if (quote is null || !Enum.IsDefined(quote.Status) || string.IsNullOrWhiteSpace(quote.RuleId) ||
            quote.MissingInputs is null || quote.MissingInputs.Any(string.IsNullOrWhiteSpace) ||
            quote.GoldCost < 0 || quote.SuppliesCost < 0 || quote.GoldIncome < 0 || quote.SuppliesIncome < 0 ||
            quote.TroopsIncome < 0 || quote.MoraleIncrease < 0 || quote.NextMorale is < 0 or > 100 ||
            quote.NextSecurity is < 0 or > 100 || quote.NextDevelopment is { } values &&
                (values.Agriculture is < 0 or > 1000 || values.Commerce is < 0 or > 1000 || values.Barracks is < 0 or > 1000) ||
            quote.Status == PeriodEconomyCalculationStatus.Available && quote.MissingInputs.Length != 0 ||
            quote.Status != PeriodEconomyCalculationStatus.Available &&
                (quote.GoldCost != 0 || quote.SuppliesCost != 0 || quote.GoldIncome != 0 || quote.SuppliesIncome != 0 ||
                 quote.TroopsIncome != 0 || quote.MoraleIncrease != 0 || quote.NextDevelopment is not null ||
                 quote.NextSecurity is not null || quote.NextMorale is not null))
            throw new InvalidDataException("Period economy quote is incomplete or changes an unresolved calculation.");
        if (quote.Status != PeriodEconomyCalculationStatus.Available)
        {
            reports.Add(Entry(operation + "_unresolved", forceId, city.Id, officerId, 0, quote));
            return false;
        }
        if (city.Gold < quote.GoldCost || city.Supplies < quote.SuppliesCost)
        {
            if (operation == "development")
                reports.Add(Entry("development_unfunded", forceId, city.Id, officerId,
                    Math.Max(quote.GoldCost - city.Gold, quote.SuppliesCost - city.Supplies), quote));
            return false;
        }
        return true;
    }

    private static CityState Apply(CityState city, PeriodEconomyQuote quote) => city with
    {
        Gold = checked(city.Gold - quote.GoldCost + quote.GoldIncome),
        Supplies = checked(city.Supplies - quote.SuppliesCost + quote.SuppliesIncome),
        PeriodGoldExpenditure = city.PeriodGoldExpenditure is int goldSpent
            ? unchecked(goldSpent + quote.GoldCost) : null,
        PeriodFoodExpenditure = city.PeriodFoodExpenditure is int foodSpent
            ? unchecked(foodSpent + quote.SuppliesCost) : null,
        Troops = checked(city.Troops + quote.TroopsIncome),
        Morale = quote.NextMorale ?? (quote.MoraleIncrease == 0 ? city.Morale :
            city.Morale is int morale ? checked((int)Math.Min(100L, (long)morale + quote.MoraleIncrease)) : null)
    };

    private static PeriodReportEntry Entry(string kind, int forceId, int cityId, int? officerId,
        int amount, PeriodEconomyQuote quote) => new(kind, forceId, cityId, officerId, amount)
        { RuleId = quote.RuleId, MissingRuleKeys = quote.MissingInputs.ToArray() };
}
