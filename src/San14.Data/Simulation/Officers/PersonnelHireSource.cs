using San14.Core;

namespace San14.Data;

public sealed record PersonnelHireCity(int CityId, string Name);
//These identities belong to the independent template catalogue, not to the
//main Person lookup. PhysicalSlot is the template's own catalogue ordinal.
internal sealed record PersonnelHireCandidate(int OfficerId, int PhysicalSlot, string Name)
{
    internal int TemplateId => OfficerId;
}
internal sealed record PersonnelHireRules(int ForceId, int DistrictId,
    IReadOnlyList<PersonnelHireCity> Cities, IReadOnlyList<PersonnelHireCandidate> Candidates,
    int CurrentHired, int Capacity, int RemainingSlots, int MaximumSelection,
    int Gold, int GoldPerPerson, int Books, int BookCost)
{
    public int? DefaultCityId { get; init; }
    internal IReadOnlyDictionary<int, PersonnelHireTemplate> Templates { get; init; } =
        new Dictionary<int, PersonnelHireTemplate>();
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string HireParameterPrefix = "simulation.personnel.hire-";
    private static readonly IReadOnlyDictionary<string, int> HireStaticRules = new Dictionary<string, int>
    {
        ["gold-per-person"] = 500, ["command-book-cost"] = 1,
        ["rank-4-limit"] = 30, ["rank-5-limit"] = 24, ["rank-6-limit"] = 20,
        ["rank-7-limit"] = 12, ["rank-8-limit"] = 10, ["rank-9-limit"] = 5,
        ["other-rank-limit"] = 40
    };

    internal static GameState InstallFreshPersonnelHireParameters(GameState state)
    {
        var native = state.NativeArmySimulation ?? throw HireMissing("parameter_owner");
        if (native.Parameters.Language != "TC") return state;
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var pair in HireStaticRules) values.TryAdd(HireParameterPrefix + pair.Key, pair.Value);
        return state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };
    }

    //72CB60/6DBC10/6D9490/74BB80. One captured current owner supplies the
    //page and command; an unknown hire manager is not an empty offer list.
    internal PersonnelHireRules ReadPersonnelHireRulesCurrent(GameState state,
        SourceWorldContext current, int districtId)
    {
        var owner = state.PersonnelHire ?? throw HireMissing("manager_owner");
        var district = current.ReadDistrictTableReference(districtId) ?? throw HireMissing("District_pointer");
        if (!HireNeed(current.ReadDistrictNativeValidity(district), "District_validity"))
            throw HireMissing("District_inactive");
        int forceId = HireNeed(current.ReadDistrictForceSlot(district), "District10");
        var force = current.ReadForceTableReference(forceId < 52 ? forceId : 0) ?? throw HireMissing("Force_pointer");
        if (!HireNeed(current.ReadForceNativeValidity(force), "Force_validity")) throw HireMissing("Force_inactive");
        forceId = HireNeed(current.ReadForcePhysicalSlot(force), "Force_identity");
        var roster = owner.Forces.GetValueOrDefault(forceId) ?? throw HireMissing("Force_roster_owner");
        var templates = owner.Templates ?? throw HireMissing("template_catalogue_owner");
        var candidates = new List<PersonnelHireCandidate>();
        foreach (int id in roster.AvailableTemplateOfficerIds)
        {
            var template = templates.GetValueOrDefault(id) ?? throw HireMissing("template_catalogue_entry");
            var person = template.Person;
            //A catalogue entry is a real non-null Person object. Its own
            //native behavior/status predicate never indexes main Officers[id].
            if (!HireNeed(NativeValidity.PersonWithBehavior(template.Id, checked((ushort)person.Id),
                person.Behavior, OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus)), "template_validity")) continue;
            candidates.Add(new(template.Id, template.Id, person.Name));
        }
        var cities = new List<PersonnelHireCity>();
        //2243A0/224560 iterate the fixed City table in its native order.
        for (int slot = 1; slot <= 51; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw HireMissing("City_pointer");
            if (!HireNeed(current.ReadBaseNativeValidity(city), "City_validity") ||
                HireNeed(current.ReadBaseForceSlot(city), "City_Force") != forceId) continue;
            int location = HireNeed(current.ReadBaseObjectLink(city), "City_identity4E");
            var record = state.Cities.GetValueOrDefault(location) ?? throw HireMissing("City_domain_owner");
            cities.Add(new(location, record.Name));
        }
        int rank = HireNeed(current.ReadMoraleWaveForceRank47(forceId), "Force_rank47");
        int capacity = rank is < 1 or > 12 ? 0 : HireParameter(state,
            rank is >= 4 and <= 9 ? $"rank-{rank}-limit" : "other-rank-limit");
        int currentHired = roster.HiredOfficerIds.Length; //260's actual list count, not a validity filter.
        int gold = BaseCurrencyAdditionSource.ReadCurrentGold(ReadPersonnelHireTreasuryCurrent(current, districtId), current);
        int unitCost = HireParameter(state, "gold-per-person");
        int bookCost = HireParameter(state, "command-book-cost");
        if (unitCost <= 0 || bookCost < 0) throw HireMissing("invalid_cost_rule");
        int books = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining ?? throw HireMissing("District14");
        int maximum = Math.Max(0, Math.Min(Math.Min(owner.RemainingSlots, capacity - currentHired),
            Math.Min(candidates.Count, gold / unitCost)));
        return new(forceId, districtId, cities.AsReadOnly(), candidates.AsReadOnly(), currentHired,
            capacity, owner.RemainingSlots, maximum, gold, unitCost, books, bookCost)
            { DefaultCityId = cities.Count == 1 ? cities[0].CityId : null, Templates = templates };
    }

    private static BaseReference ReadPersonnelHireTreasuryCurrent(SourceWorldContext current, int districtId)
    {
        ushort id = HireNeed(current.ReadDistrictLeader(districtId), "treasury_District12");
        var leader = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
            ?? throw HireMissing("treasury_leader_lookup");
        //22D4E0 has no Person-validity gate before reading the leader's E2.
        return BaseLookup.Find(HireNeed(current.ReadPersonFirstLocation(HireNeed(leader.PhysicalSlot,
            "treasury_leader_pointer")), "treasury_leader_E2"), current);
    }

    private static int HireParameter(GameState state, string name)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw HireMissing("parameter_owner");
        string key = HireParameterPrefix + name;
        if (bank.Language != "TC" || bank.RuleVersion != SimulationRuleIdentity.ForLocale("TC") ||
            bank.Float32Values.ContainsKey(key)) throw HireMissing(key);
        return bank.Int32Values.TryGetValue(key, out int value) ? value
            : HireStaticRules.TryGetValue(name, out int initial) ? initial : throw HireMissing(key);
    }
    private static T HireNeed<T>(T? value, string name) where T : struct => value ?? throw HireMissing(name);
    private static InvalidDataException HireMissing(string name) => new("personnel.hire." + name);
}
