namespace San14.Core;

/// <summary>Provisional ten-day food settlement, separate from original-rule providers.</summary>
public static class PlayableSupplyUpkeep
{
    public const string OwnerChangedReportKind = "food_source_changed";
    public const string OwnerChangedRuleId = "san14:provisional-period-food-owner-changed-v1";
    public const string MappedShortageRuleId = "san14:provisional-mapped-food-shortage-unresolved-v1";

    /// <summary>Compatibility helper for callers that settle at their current
    /// quantity cutoff. Campaign advancement captures both rosters explicitly
    /// before travel so arriving troops cannot enter both whole-period bills.</summary>
    public static void Settle(Dictionary<int, CityState> cities, Dictionary<int, UnitState> units,
        IReadOnlyCollection<UnitState> periodFoodUnits,
        List<PeriodReportEntry> reports) =>
        Settle(cities, units, PlayableSupplyUpkeepSnapshot.Capture(cities, periodFoodUnits), reports);

    public static void Settle(Dictionary<int, CityState> cities, Dictionary<int, UnitState> units,
        PlayableSupplyUpkeepSnapshot basis, List<PeriodReportEntry> reports)
    {
        ArgumentNullException.ThrowIfNull(cities); ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(basis); ArgumentNullException.ThrowIfNull(reports);
        PlayableSupplyUpkeepSnapshot.ValidateCities(cities);
        foreach ((int id, UnitState army) in units)
            if (army is null || id != army.Id || id < 0 || army.ForceId < 0 || army.OfficerId < 0 ||
                army.SourceLocationId < 0 || army.Troops < 1 || army.Supplies < 0)
                throw new InvalidDataException("Current period food unit identity or quantity is invalid.");
        foreach (PlayableCitySupplyUpkeepBasis snapshot in basis.Cities)
            if (!cities.ContainsKey(snapshot.CityId))
                throw new InvalidDataException("A captured period food city no longer exists.");

        var cityWrites = new Dictionary<int, CityState>();
        var unitWrites = new Dictionary<int, UnitState>();
        var additions = new List<PeriodReportEntry>(basis.MappingReports);
        foreach (PlayableCitySupplyUpkeepBasis snapshot in basis.Cities)
        {
            if (snapshot.ForceId is not int forceId) continue;
            CityState current = cities[snapshot.CityId];
            PlayableUnitSupplyUpkeepBasis[] sourcedUnits = basis.Units.Where(unit => unit.BillingForceId == forceId &&
                unit.FoodCityId == snapshot.CityId).ToArray();
            long fieldTroops = sourcedUnits.Sum(unit => (long)unit.Troops);
            // Placeholder divisors; only the qualitative per-period expense is confirmed.
            long garrisonDue = basis.Cities.Where(row => row.GarrisonFoodCityId == snapshot.CityId)
                .Sum(row => (row.GarrisonFoodTroops + 29) / 30) +
                basis.Gates.Where(row => row.FoodCityId == snapshot.CityId).Sum(row => (row.FoodTroops + 29) / 30);
            long dueLong = garrisonDue + (fieldTroops + 59) / 60;
            int due = (int)Math.Min(int.MaxValue, dueLong);
            if (due == 0) continue;
            if (current.ForceId != forceId)
            {
                //Old-force upkeep cannot debit the new owner. Precise expense
                //timing around the capture is an unclosed daily-rule boundary.
                additions.Add(new PeriodReportEntry(OwnerChangedReportKind, forceId,
                    current.Id, Amount: due)
                { RuleId = OwnerChangedRuleId, MissingRuleKeys = ["food_upkeep.owner_change_timing"] });
                continue;
            }
            int paid = Math.Min(current.Supplies, due);
            CityState city = current with { Supplies = current.Supplies - paid };
            if (paid > 0)
                additions.Add(new PeriodReportEntry("food_upkeep", forceId, city.Id, null, paid));
            int shortage = due - paid;
            if (shortage > 0)
            {
                additions.Add(new PeriodReportEntry("food_shortage", forceId, city.Id, null, shortage));
                bool legacyRecipients = basis.Cities.All(row => row.GarrisonFoodCityId != city.Id || row.CityId == city.Id) &&
                    basis.Gates.All(row => row.FoodCityId != city.Id || row.FoodTroops == 0) &&
                    sourcedUnits.All(row => row.SourceLocationId == city.Id);
                if (!legacyRecipients)
                {
                    //The expense receiver is the City, while a source Gate
                    //shortage callback punishes that Gate. Do not reuse the
                    //legacy payer-City penalty for new mapped participants.
                    additions.Add(new("food_mapped_shortage_unresolved", forceId, city.Id, Amount: shortage)
                        { RuleId = MappedShortageRuleId, MissingRuleKeys = ["food_upkeep.mapped_shortage_consequences"] });
                    cityWrites[city.Id] = city;
                    continue;
                }
                int deserters = (int)Math.Min(city.Troops, (long)shortage * 2);
                if (deserters > 0)
                {
                    city = city with { Troops = city.Troops - deserters };
                    additions.Add(new PeriodReportEntry("food_desertion", forceId, city.Id,
                        null, deserters));
                }
                foreach (PlayableUnitSupplyUpkeepBasis armyBasis in sourcedUnits)
                {
                    if (!units.TryGetValue(armyBasis.UnitId, out UnitState? unit) ||
                        unit.ForceId != armyBasis.ForceId || unit.OfficerId != armyBasis.OfficerId ||
                        unit.SourceLocationId != armyBasis.SourceLocationId) continue;
                    int lost = Math.Min(Math.Max(0, unit.Morale), 10);
                    if (lost == 0) continue;
                    unitWrites[unit.Id] = unit with { Morale = unit.Morale - lost };
                    additions.Add(new PeriodReportEntry("food_unit_morale", forceId,
                        city.Id, unit.OfficerId, lost));
                }
            }
            cityWrites[city.Id] = city;
        }
        //Stage the whole ledger before publication; a later invalid input must
        //not commit an earlier city's food payment or any report.
        foreach ((int id, CityState city) in cityWrites) cities[id] = city;
        foreach ((int id, UnitState army) in unitWrites) units[id] = army;
        reports.AddRange(additions);
    }
}
