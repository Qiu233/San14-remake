using San14.Core;

namespace San14.Data;

public sealed record DistrictCommandBudgetQuote(int DistrictId, int Books, int ReturnLimit,
    int CityCount, int PersonCount, int OwnedAreaCount, int LeaderPolitics, int AdvisorPolitics, int PolicyAddition);

public sealed record DistrictCommandBudgetQuery(IReadOnlyList<DistrictCommandBudgetQuote>? Districts, string? UnresolvedInput)
{
    public bool Available => Districts is not null && UnresolvedInput is null;
}

public sealed partial class ArmyCampaignRuleAdapter : IPlayableDistrictCommandBudgetRules
{
    public DistrictCommandBudgetQuery QueryDistrictCommandBudgets(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw BudgetMissing("parameters");
            if (parameters.Language != Seed.Language || parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language) || P("version") != 1)
                throw BudgetMissing("profile");
            int P(string name) => parameters.Int32Values.TryGetValue(DistrictCommandBudgetParameters.Key(name), out int value)
                ? value : throw BudgetMissing(name);
            int baseValue = P("base"), cityCoefficient = P("cityCoefficient"), politicsDivisor = P("politicsDivisor"),
                personDivisor = P("personDivisor"), upper = P("upperBound"), effectMultiplier = P("effectMultiplier"),
                areaDivisor = P("areaDivisor"), areaMultiplier = P("areaMultiplier"), minimum = P("minimumReturnLimit"),
                politicsPercent = P("politicsPercent"), personPercent = P("personPercent"), cityMultiplier = P("cityMultiplier");
            if (politicsDivisor == 0 || personDivisor == 0 || areaDivisor == 0) throw BudgetMissing("zero_divisor");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            var arena = state.ReadArmyLifecycle() ?? throw BudgetMissing("saved_indexes");
            _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, parameters.RuleVersion);
            var forces = current.ReadRegistryReferences(RegistryKind.Force) ?? throw BudgetMissing("force_registry");
            var districts = current.ReadArmyReturnRegisteredDistricts() ?? throw BudgetMissing("district_registry");
            var people = current.ReadRegisteredPersonPhysicalSlots() ?? throw BudgetMissing("person_registry");
            var areas = current.ReadRegisteredAreaReferences() ?? throw BudgetMissing("area_registry");
            var politics = new Dictionary<int, int>();
            var result = new List<DistrictCommandBudgetQuote>();
            foreach (var forceEntry in forces)
            {
                int forcePhysical = BudgetKnown(forceEntry?.PhysicalSlot, "force_pointer");
                var forceReference = new ForceReference(forcePhysical);
                if (!BudgetKnown(current.ReadForceNativeValidity(forceReference), "force_validity")) continue;
                foreach (var district in districts)
                {
                    int physical = BudgetKnown(district?.PhysicalSlot, "district_pointer");
                    if (BudgetKnown(current.ReadDistrictForceId(physical), "district_force") != forcePhysical) continue;
                    if (!state.Districts.ContainsKey(physical)) throw BudgetMissing("district_owner");
                    int leaderPolitics = Politics(BudgetKnown(current.ReadDistrictLeader(physical), "leader"));
                    int returnLimit = BudgetReturnLimit(leaderPolitics, upper, minimum);
                    if (!BudgetKnown(current.ReadDistrictNativeValidity(physical), "district_validity"))
                    { result.Add(new(physical, Math.Clamp(Math.Min(0, upper), 0, 200), returnLimit, 0, 0, 0, leaderPolitics, 0, 0)); continue; }
                    if (!state.Forces.TryGetValue(forcePhysical, out var force)) throw BudgetMissing("force_owner");
                    string advisorKey = BattleRemakeCurrentForcePolicies.ForceFieldKey(forcePhysical, 0x52);
                    if (!parameters.Int32Values.TryGetValue(advisorKey, out int advisor) || advisor is < 0 or > ushort.MaxValue)
                        throw BudgetMissing("advisor_current52");
                    int advisorPolitics = Politics(advisor), cityCount = 0, personCount = 0, areaCount = 0;
                    foreach (int? personEntry in people)
                    {
                        int person = BudgetKnown(personEntry, "person_pointer");
                        if (physical is < 1 or > 51) continue;
                        byte? membership = current.ReadPersonDistrictSlot(person), status = current.ReadPersonStatus(person);
                        // Either known false half proves the pure conjunction
                        // false without manufacturing the other half's value.
                        if (membership.HasValue && membership != physical || status.HasValue && status is not (1 or 2 or 3 or 4)) continue;
                        if (BudgetKnown(membership, $"person_district:{person}") == physical &&
                            BudgetKnown(status, $"person_status:{person}") is 1 or 2 or 3 or 4) personCount = unchecked(personCount + 1);
                    }
                    // CDistrictCityIterator scans the fixed 52 City pointers,
                    // while CCityAreaIterator consumes the current Area manager.
                    for (int slot = 0; slot < 52; slot++)
                    {
                        var city = current.ReadCityTableReference(slot) ?? throw BudgetMissing("city_pointer");
                        int cityPhysical = BudgetKnown(city.PhysicalSlot, "city_physical");
                        if (physical is < 1 or > 51 || BudgetKnown(current.ReadCityDistrictReference(city)?.PhysicalSlot, $"city_district:{slot}") != physical) continue;
                        cityCount = unchecked(cityCount + 1);
                        if (cityPhysical is < 1 or > 51) continue;
                        foreach (var area in areas)
                        {
                            if (area is null) throw BudgetMissing("area_pointer");
                            if (BudgetKnown(current.ReadAreaCitySlot(area), "area_city") != cityPhysical) continue;
                            if (BudgetKnown(current.ReadAreaCurrentForceSlot(area), "area_representative_force") == forcePhysical)
                                areaCount = unchecked(areaCount + 1);
                        }
                    }
                    int effect = BudgetKnown(BattleRemakeCurrentForcePolicies.ReadForceEffect(state, force, 37), "policy37");
                    int raw = unchecked(cityMultiplier * cityCoefficient * cityCount + Divide(areaCount, areaDivisor) * areaMultiplier + baseValue +
                        Divide(unchecked(Divide(personCount, personDivisor) * personPercent), 100) +
                        Divide(unchecked(Divide(unchecked(leaderPolitics + advisorPolitics), politicsDivisor) * politicsPercent), 100) + effect * effectMultiplier);
                    int books = Math.Clamp(Math.Min(raw, upper), 0, 200);
                    result.Add(new(physical, books, returnLimit, cityCount, personCount, areaCount, leaderPolitics, advisorPolitics, effect));
                }
            }
            return new(result.AsReadOnly(), null);

            int Politics(int requested)
            {
                return ReadBudgetPolitics(state, current, abilities, requested, politics);
            }
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public int? ReadDistrictCommandReturnLimit(GameState state, int districtId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if (!state.Districts.ContainsKey(districtId)) throw BudgetMissing("district_owner");
            var parameters = state.NativeArmySimulation?.Parameters ?? throw BudgetMissing("parameters");
            if (parameters.Language != Seed.Language || parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language) ||
                !parameters.Int32Values.TryGetValue(DistrictCommandBudgetParameters.Key("version"), out int version) || version != 1 ||
                !parameters.Int32Values.TryGetValue(DistrictCommandBudgetParameters.Key("upperBound"), out int upper) ||
                !parameters.Int32Values.TryGetValue(DistrictCommandBudgetParameters.Key("minimumReturnLimit"), out int minimum))
                throw BudgetMissing("return_limit_parameters");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, parameters.RuleVersion);
            int leader = BudgetKnown(current.ReadDistrictLeader(districtId), "leader");
            return BudgetReturnLimit(ReadBudgetPolitics(state, current, abilities, leader), upper, minimum);
        }
        catch (InvalidDataException) { return null; }
    }

    private static int ReadBudgetPolitics(GameState state, SourceWorldContext current,
        TacticRemakeCurrentAbilities abilities, int requested, Dictionary<int, int>? cache = null)
    {
        int person = BudgetKnown(current.ReadPersonLookupReference(requested <= 6000 ? requested : 0)?.PhysicalSlot, "politics_person_pointer");
        if (!BudgetKnown(current.ReadPersonNativeValidity(person), "politics_person_validity")) return 0;
        if (cache is not null && cache.TryGetValue(person, out int saved)) return saved;
        int id = BudgetKnown(current.ReadPersonIdentity(person), "politics_person_identity");
        int politics = (abilities.Resolve(state, id) ?? throw BudgetMissing("current_effective_politics")).Politics;
        if (cache is not null) cache[person] = politics;
        return politics;
    }

    private static int BudgetReturnLimit(int politics, int upper, int minimum) =>
        Math.Max(Math.Min(unchecked(politics * 2), upper), minimum);

    public GameState RefreshDistrictCommandBudgets(GameState state)
    {
        var query = QueryDistrictCommandBudgets(state);
        if (!query.Available) throw new InvalidDataException(query.UnresolvedInput);
        var districts = new Dictionary<int, DistrictState>(state.Districts);
        foreach (var quote in query.Districts!) districts[quote.DistrictId] = districts[quote.DistrictId] with
            { OrdersRemaining = quote.Books, OrderLimit = quote.ReturnLimit };
        return state with { Districts = districts };
    }

    private static int Divide(int value, int divisor) => value == int.MinValue && divisor == -1
        ? throw BudgetMissing("division_overflow") : value / divisor;
    private static T BudgetKnown<T>(T? value, string name) where T : struct => value ?? throw BudgetMissing(name);
    private static InvalidDataException BudgetMissing(string name) => new("district.command_budget." + name);
}
