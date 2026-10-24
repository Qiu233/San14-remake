namespace San14.Core;

/// <summary>Temporary period accounting quantities, frozen together after
/// deployment/effects/ticks and before travel, combat or arrival. This is not
/// an original daily expense formula or a new saved simulation owner.</summary>
public sealed record PlayableCitySupplyUpkeepBasis(int CityId, int? ForceId, int Troops)
{
    public int? GarrisonFoodCityId { get; init; } = CityId;
    public long GarrisonFoodTroops { get; init; } = Troops;
    public int? GarrisonExpense { get; init; }
}
public sealed record PlayableUnitSupplyUpkeepBasis(int UnitId, int ForceId, int OfficerId,
    int? SourceLocationId, int Troops)
{
    public int? FoodCityId { get; init; } = SourceLocationId;
    public int BillingForceId { get; init; } = ForceId;
}
public sealed record PlayableGateSupplyUpkeepBasis(int LocationId, int? FoodCityId, long FoodTroops)
{
    public int? GarrisonExpense { get; init; }
}

public sealed class PlayableSupplyUpkeepSnapshot
{
    public IReadOnlyList<PlayableCitySupplyUpkeepBasis> Cities { get; }
    public IReadOnlyList<PlayableUnitSupplyUpkeepBasis> Units { get; private set; }
    public IReadOnlyList<PlayableGateSupplyUpkeepBasis> Gates { get; }
    public IReadOnlyList<PeriodReportEntry> MappingReports { get; }
    public bool UsesCurrentGarrisonExpenses { get; }
    public IReadOnlyDictionary<int, PlayableArmyFoodExpense>? ArmyExpenses { get; private set; }
    public IReadOnlyList<PlayableArmyFoodExpenseEntry>? ArmyExpenseOrder { get; private set; }

    internal void AttachArmyExpenses(GameState state, IReadOnlyDictionary<int, PlayableArmyFoodExpense>? expenses,
        IReadOnlyList<PlayableArmyFoodExpenseEntry>? order = null)
    {
        ArmyExpenses = expenses;
        ArmyExpenseOrder = order;
        if (expenses is null) return;
        Units = Array.AsReadOnly(Units.Select(unit => expenses.TryGetValue(unit.UnitId, out var quote)
            ? unit with { FoodCityId = quote.FoodCityId,
                BillingForceId = quote.FoodCityId is int payer && state.Cities.TryGetValue(payer, out var city)
                    ? city.ForceId ?? unit.ForceId : unit.ForceId } : unit).ToArray());
    }

    private PlayableSupplyUpkeepSnapshot(PlayableCitySupplyUpkeepBasis[] cities,
        PlayableUnitSupplyUpkeepBasis[] units, PlayableGateSupplyUpkeepBasis[]? gates = null,
        PeriodReportEntry[]? mappingReports = null, bool usesCurrentGarrisonExpenses = false)
    {
        Cities = Array.AsReadOnly(cities);
        Units = Array.AsReadOnly(units);
        Gates = Array.AsReadOnly(gates ?? []);
        MappingReports = Array.AsReadOnly(mappingReports ?? []);
        UsesCurrentGarrisonExpenses = usesCurrentGarrisonExpenses;
    }

    public static PlayableSupplyUpkeepSnapshot Capture(IReadOnlyDictionary<int, CityState> cities,
        IEnumerable<UnitState> units)
    {
        ArgumentNullException.ThrowIfNull(cities); ArgumentNullException.ThrowIfNull(units);
        ValidateCities(cities);
        var armyIds = new HashSet<int>();
        var field = new List<PlayableUnitSupplyUpkeepBasis>();
        foreach (UnitState army in units)
        {
            if (army is null || army.Id < 0 || army.ForceId < 0 || army.OfficerId < 0 ||
                army.SourceLocationId < 0 || army.Troops < 1 || !armyIds.Add(army.Id))
                throw new InvalidDataException("Period food units have invalid or duplicate identities/quantities.");
            field.Add(new(army.Id, army.ForceId, army.OfficerId, army.SourceLocationId, army.Troops));
        }
        return new(cities.Values.OrderBy(city => city.Id)
                .Select(city => new PlayableCitySupplyUpkeepBasis(city.Id, city.ForceId, city.Troops)).ToArray(),
            field.OrderBy(army => army.UnitId).ToArray());
    }

    /// <summary>The same provisional quantity cut with source-supplied current
    /// receivers. City/Gate garrisons remain separate expense quantities; only
    /// their food writes share a City, never their soldiers or carried cargo.</summary>
    public static PlayableSupplyUpkeepSnapshot Capture(GameState state, IPlayableFoodOwnerRules ownerRules)
    {
        var legacy = Capture(state.Cities, state.Units.Values);
        var owners = ownerRules.CaptureFoodOwners(state);
        var reports = new List<PeriodReportEntry>();
        var garrisons = new List<PlayableGateSupplyUpkeepBasis>();
        bool currentExpenses = owners.Values.Any(owner => owner.GarrisonExpense is not null);
        var cities = legacy.Cities.Select(row =>
        {
            var city = state.Cities[row.CityId]; var owner = Resolve(row.CityId, row.ForceId, null, false);
            if (city.NativeReturnRuntime is { Wounded40: null })
            {
                Unknown(row.CityId, row.ForceId, null, "food_upkeep.current_city40", owner?.RuleId);
                return row with { GarrisonFoodCityId = null };
            }
            return row with { GarrisonFoodCityId = owner?.GarrisonCityId,
                GarrisonExpense = Expense(row.CityId, row.ForceId, owner),
                GarrisonFoodTroops = (long)city.Troops + (city.NativeReturnRuntime?.Wounded40 ?? 0) };
        }).ToArray();
        foreach (var gate in state.Gates.Values.OrderBy(x => x.Id))
        {
            var owner = Resolve(gate.Id, gate.ForceId, null, false);
            if (gate.NativeCombatRuntime?.Wounded40 is not int wounded)
            {
                if (gate.Troops > 0) Unknown(gate.Id, gate.ForceId, null, "food_upkeep.current_gate40", owner?.RuleId);
                continue;
            }
            garrisons.Add(new(gate.Id, owner?.GarrisonCityId, (long)gate.Troops + wounded)
                { GarrisonExpense = Expense(gate.Id, gate.ForceId, owner) });
        }
        var units = legacy.Units.Select(row =>
        {
            var owner = row.SourceLocationId is int home ? Resolve(home, row.ForceId, row.OfficerId, true) : null;
            int? payer = owner?.ArmyCityId;
            return row with { FoodCityId = payer, BillingForceId = payer is int cityId &&
                state.Cities.TryGetValue(cityId, out var city) ? city.ForceId ?? row.ForceId : row.ForceId };
        }).ToArray();
        return new(cities, units, garrisons.ToArray(), reports.ToArray(), currentExpenses);

        int? Expense(int location, int? force, PlayableFoodPayerResolution? owner)
        {
            if (!currentExpenses) return null;
            if (owner?.GarrisonExpense is { Amount: int amount }) return amount;
            Unknown(location, force, null, owner?.GarrisonExpense?.MissingOwner ??
                "food_upkeep.current_garrison_expense", owner?.RuleId);
            return null;
        }

        PlayableFoodPayerResolution? Resolve(int location, int? force, int? person, bool army)
        {
            if (!owners.TryGetValue(location, out var owner))
            { Unknown(location, force, person, "food_upkeep.current_food_payer", null); return null; }
            string? missing = army ? owner.ArmyMissingOwner : owner.GarrisonMissingOwner;
            if (missing is not null) Unknown(location, force, person, missing, owner.RuleId);
            return owner;
        }
        void Unknown(int location, int? force, int? person, string missing, string? rule)
        {
            if (force is not int owner) return;
            reports.Add(new("food_source_unresolved", owner, state.Cities.ContainsKey(location) ? location : null, person)
                { LocationId = location, RuleId = rule ?? "san14:unresolved-current-food-payer", MissingRuleKeys = [missing] });
        }
    }

    internal static void ValidateCities(IReadOnlyDictionary<int, CityState> cities)
    {
        foreach ((int id, CityState city) in cities)
            if (city is null || id != city.Id || id < 0 || city.ForceId < 0 ||
                city.Troops < 0 || city.Supplies < 0)
                throw new InvalidDataException("Period food city identity, garrison or current food stock is invalid.");
    }
}
