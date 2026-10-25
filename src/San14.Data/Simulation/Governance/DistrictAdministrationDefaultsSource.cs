using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //761FB0's explicit local packet stores. These are draft defaults, never
    //a migration or installation into a borrowed world's unknown settings.
    internal static DistrictSettings ReadDistrictAdministrationInitialSettingsCurrent() => new(1, null, 1,
        true, true, true, true, true, Enumerable.Range(0, 31).ToImmutableHashSet(), new(0, 0, 0),
        new(true, ImmutableHashSet.Create(0, 1, 3, 4, 5, 6, 7))) { Demolition = true };

    internal static DistrictSettings ApplyDistrictAdministrationPolicyFields(DistrictSettings settings, int policy) => policy switch
    {
        1 => settings with { PolicyId = 1, Target = null, PriorityId = 1, RecruitOfficers = true,
            Attack = true, Transport = true, RecruitTroops = true, Merchant = true, Demolition = true,
            Resources = new(0, 0, 0), Military = settings.Military with { Enabled = true } },
        2 or 3 => settings with { PolicyId = policy, Attack = true },
        4 => settings with { PolicyId = 4, Target = null, Attack = false, Transport = false },
        5 => settings with { PolicyId = 5, Attack = false, Transport = true },
        _ => throw DistrictPageMissing("policy_selection")
    };

    private DistrictSettings ReadDistrictAdministrationPolicyChoiceCurrent(GameState state, SourceWorldContext current,
        int forceId, DistrictSettings settings, int policy, IReadOnlyList<int> cityIds)
    {
        if (policy is 1 or 4) return settings;
        DistrictTargetKind kind = policy switch
        { 2 => DistrictTargetKind.Force, 3 => DistrictTargetKind.Foothold, _ => DistrictTargetKind.District };
        int maximum = policy == 3 ? 61 : 51;
        if (settings.Target is { } retained && retained.Kind == kind && retained.Id is >= 1 && retained.Id <= maximum)
            return settings;
        if (policy == 5)
        {
            var force = current.ReadForceTableReference(forceId) ?? throw DistrictPageMissing("policy_primary_Force");
            int primary = ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot
                ?? throw DistrictPageMissing("policy_primary_District");
            return settings with { Target = new(DistrictTargetKind.District, primary) };
        }
        //The original automatic recommendation is an independent weighted
        //source. Keep its missing owner distinct from the fully available
        //manual target list; selecting an actual target replaces this gap.
        throw DistrictPageMissing(policy == 2 ? "automatic_Force_target_score" : "automatic_Foothold_target_score");
    }

    internal (DistrictSettings Settings, int? LeaderId) ReadDistrictAdministrationDefaultsCurrent(GameState state,
        SourceWorldContext current, int forceId, int? districtId, IReadOnlyList<int> cityIds,
        DistrictSettings settings, int? priorLeaderId, bool initializeDefaults)
    {
        if (initializeDefaults) settings = ReadDistrictAdministrationInitialSettingsCurrent();
        if (!initializeDefaults && priorLeaderId is int old && KeepsLeader(old)) return (settings, old);
        var choices = QueryDistrictAdministrationSelectionCurrent(state, current, forceId, districtId, cityIds, settings).Leaders;
        int? selected = null;
        var capacity = new Dictionary<int, int>();
        var office = new Dictionary<int, int>();
        var leadership = new Dictionary<int, int>();
        TacticRemakeCurrentAbilities? abilities = null;
        foreach (var row in choices)
        {
            if (selected is not int best || Before(row.Id, best)) selected = row.Id;
        }
        return (settings, selected);

        bool KeepsLeader(int id)
        {
            int p = Physical(id);
            if (!DistrictPageNeed(current.ReadPersonNativeValidity(p), "leader_validity")) return false;
            var home = BaseLookup.Find(DistrictPageNeed(current.ReadPersonFirstLocation(p), "leader_E2"), current);
            if (!DistrictPageNeed(current.ReadBaseNativeValidity(home), "leader_home_validity")) return false;
            var city = CurrentGateMapQueries.FindCity(home, current);
            return DistrictPageNeed(current.ReadBaseNativeValidity(city), "leader_City_validity") &&
                cityIds.Contains(DistrictPageNeed(current.ReadBaseObjectLink(city), "leader_City4E"));
        }
        int Physical(int id) => current.ResolveOfficerSource(id) ?? throw DistrictPageMissing("leader_Person_pointer");
        int Capacity(int id)
        {
            if (capacity.TryGetValue(id, out int value)) return value;
            var result = OfficerCommandCapacity.Resolve(state, current, Physical(id));
            return capacity[id] = result.Capacity ?? throw new InvalidDataException(result.UnresolvedInput ?? "district.page.leader_capacity");
        }
        int Office(int id)
        {
            if (office.TryGetValue(id, out int value)) return value;
            int raw = DistrictPageNeed(current.ReadMoraleWavePersonPostE7(Physical(id)), "leader_PostE7");
            var post = current.ReadStaticTableReference(0x784F8, raw) ?? throw DistrictPageMissing("leader_Post_lookup");
            return office[id] = post.PhysicalSlot is int p &&
                DistrictPageNeed(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, p), "leader_Post_validity") ? p : int.MaxValue;
        }
        int Leadership(int id)
        {
            if (leadership.TryGetValue(id, out int value)) return value;
            abilities ??= new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            return leadership[id] = abilities.ResolveDimension(state, id, 0,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
                ?? throw DistrictPageMissing("leader_effective_leadership");
        }
        //6959B0: capacity, valid/smaller physical Post, effective leadership,
        //then actual Person identity. No extra ruler/status ordering.
        bool Before(int candidate, int best)
        {
            int first = Capacity(candidate), second = Capacity(best);
            if (first != second) return first > second;
            first = Office(candidate); second = Office(best);
            if (first != second) return first < second;
            first = Leadership(candidate); second = Leadership(best);
            return first != second ? first > second : candidate < best;
        }
    }

    private static T DistrictPageNeed<T>(T? value, string field) where T : struct => value ?? throw DistrictPageMissing(field);
    private static InvalidDataException DistrictPageMissing(string field) => new("district.page." + field);
}
