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
        PlayableSupplyUpkeepSnapshot basis, List<PeriodReportEntry> reports,
        ICollection<PlayableArmyFoodShortage>? armyShortages = null,
        Action<PlayableGarrisonFoodShortage>? garrisonShortage = null)
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
        if (basis.ArmyExpenses is { } currentExpenses)
            foreach (var army in basis.Units)
                if (!currentExpenses.TryGetValue(army.UnitId, out var expense) || expense.Amount is null)
                    additions.Add(new("food_source_unresolved", army.ForceId, army.FoodCityId, army.OfficerId)
                    { RuleId = "CE-current-army-food", MissingRuleKeys = [expense?.MissingOwner ?? "food_upkeep.army_expense"] });
        if (basis.UsesCurrentGarrisonExpenses)
        {
            // Keep the original Base receiver separate from its mapped payer.
            // Each callback runs before that bill's debit, using the stock left
            // by earlier bills; City and Gate troop losses are never aggregated.
            foreach (var row in basis.Cities)
            {
                BillGarrison(row.CityId, row.GarrisonFoodCityId, row.GarrisonExpense);
                foreach (var gate in basis.Gates.Where(gate => gate.FoodCityId == row.CityId))
                    BillGarrison(gate.LocationId, gate.FoodCityId, gate.GarrisonExpense);
            }
        }
        void BillGarrison(int location, int? payerId, int? amount)
        {
            if (payerId is not int payer || amount is not int due || due <= 0 ||
                !cities.TryGetValue(payer, out var city) || city.ForceId is not int force) return;
            var owner = basis.Cities.FirstOrDefault(row => row.CityId == payer);
            if (owner?.ForceId != force)
            {
                additions.Add(new(OwnerChangedReportKind, force, payer, Amount: due)
                { LocationId = location, RuleId = OwnerChangedRuleId,
                    MissingRuleKeys = ["food_upkeep.owner_change_timing"] });
                return;
            }
            int paid = Math.Min(city.Supplies, due);
            if (paid < due)
            {
                if (garrisonShortage is null)
                    throw new InvalidDataException("food_upkeep.current_garrison_shortage_consequences");
                int percent = Math.Max(1, unchecked((due - city.Supplies) * 100) / due);
                garrisonShortage(new(location, payer, force, due, city.Supplies, percent));
                // The callback publishes the original Base, which can be this
                // payer. Refresh it before the food write so 3C/44 survive.
                city = cities[payer];
            }
            paid = Math.Min(city.Supplies, due);
            cities[payer] = city with { Supplies = city.Supplies - paid };
            if (paid > 0) additions.Add(new("food_upkeep", force, payer, Amount: paid) { LocationId = location });
            if (paid < due) additions.Add(new("food_shortage", force, payer, Amount: due - paid) { LocationId = location });
        }
        foreach (PlayableCitySupplyUpkeepBasis snapshot in basis.Cities)
        {
            if (snapshot.ForceId is not int forceId) continue;
            CityState current = cities[snapshot.CityId];
            PlayableUnitSupplyUpkeepBasis[] sourcedUnits = basis.Units.Where(unit => unit.BillingForceId == forceId &&
                unit.FoodCityId == snapshot.CityId).ToArray();
            long fieldTroops = sourcedUnits.Sum(unit => (long)unit.Troops);
            // Production garrisons use individually quoted current Base costs.
            // An unresolved quote has its own report and cannot fall back to
            // placeholder math. Legacy/custom providers retain their old rule.
            long garrisonDue = basis.UsesCurrentGarrisonExpenses ? 0 : basis.Cities.Where(row => row.GarrisonFoodCityId == snapshot.CityId)
                .Sum(row => basis.UsesCurrentGarrisonExpenses ? row.GarrisonExpense ?? 0L : (row.GarrisonFoodTroops + 29) / 30) +
                basis.Gates.Where(row => row.FoodCityId == snapshot.CityId)
                .Sum(row => basis.UsesCurrentGarrisonExpenses ? row.GarrisonExpense ?? 0L : (row.FoodTroops + 29) / 30);
            // Production profiles supply the logistics route expense. Other
            // providers retain their separately identified provisional rule.
            //Current Army bills settle individually below. One final stock
            //comparison cannot decide which earlier Army was fully supplied.
            long fieldDue = basis.ArmyExpenses is not null ? 0 : (fieldTroops + 59) / 60;
            long dueLong = garrisonDue + fieldDue;
            int due = (int)Math.Min(int.MaxValue, dueLong);
            if (due == 0)
            {
                continue;
            }
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
                if (basis.UsesCurrentGarrisonExpenses)
                {
                    //The garrison loss consumer is independent of the Army
                    //morale callback. Do not run legacy2/-10 on current units.
                    additions.Add(new("food_garrison_shortage_unresolved", forceId, city.Id, Amount: shortage)
                    { MissingRuleKeys = ["food_upkeep.current_garrison_shortage_consequences"] });
                    cityWrites[city.Id] = city;
                    continue;
                }
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
                    unitWrites[unit.Id] = unit with { MoraleState = unit.MoraleState with { ProvisionalValue = unit.Morale - lost } };
                    additions.Add(new PeriodReportEntry("food_unit_morale", forceId,
                        city.Id, unit.OfficerId, lost));
                }
            }
            cityWrites[city.Id] = city;
        }
        if (basis.ArmyExpenses is { } armyExpenses)
        {
            var roster = basis.Units.ToDictionary(unit => unit.UnitId);
            var order = basis.ArmyExpenseOrder ?? armyExpenses.Select(pair =>
                new PlayableArmyFoodExpenseEntry(pair.Key, pair.Value)).ToArray();
            foreach (var entry in order)
            {
                var quote = entry.Expense;
                if (!quote.Admitted || quote.Amount is not int due || quote.FoodCityId is not int payer ||
                    !roster.TryGetValue(entry.UnitId, out var row) || !cities.TryGetValue(payer, out var original)) continue;
                var city = cityWrites.GetValueOrDefault(payer) ?? original;
                if (city.ForceId != row.BillingForceId)
                {
                    if (due > 0) additions.Add(new(OwnerChangedReportKind, row.ForceId, payer, row.OfficerId, due)
                    { RuleId = OwnerChangedRuleId, MissingRuleKeys = ["food_upkeep.owner_change_timing"] });
                    continue;
                }
                int paid = Math.Min(city.Supplies, due);
                bool supplied = paid == due;
                if (!supplied && armyShortages is null)
                    throw new InvalidDataException("food_upkeep.current_army_shortage_consequences");
                cityWrites[payer] = city with { Supplies = city.Supplies - paid };
                if (paid > 0) additions.Add(new("food_upkeep", row.ForceId, payer, row.OfficerId, paid));
                if (!supplied)
                {
                    additions.Add(new("food_shortage", row.ForceId, payer, row.OfficerId, due - paid));
                    armyShortages!.Add(new(row.UnitId, row.ForceId, row.OfficerId, payer, due - paid));
                }
                if (units.TryGetValue(row.UnitId, out var current) && current.ForceId == row.ForceId &&
                    current.OfficerId == row.OfficerId)
                {
                    var image = unitWrites.GetValueOrDefault(row.UnitId) ?? current;
                    if (image.NativeTacticRuntime is { } runtime)
                        unitWrites[row.UnitId] = image with { NativeTacticRuntime = runtime with
                        { Flags26 = supplied ? (byte)(runtime.Flags26 | 0x40) : (byte)(runtime.Flags26 & ~0x40) } };
                }
            }
        }
        //Stage the whole ledger before publication; a later invalid input must
        //not commit an earlier city's food payment or any report.
        foreach ((int id, CityState city) in cityWrites) cities[id] = city;
        foreach ((int id, UnitState army) in unitWrites) units[id] = army;
        reports.AddRange(additions);

    }
}
