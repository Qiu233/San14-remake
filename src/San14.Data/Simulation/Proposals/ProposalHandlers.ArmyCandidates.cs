using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    private OwnedArmyGovernorComparatorInputs? _proposalArmyPosts;

    internal bool TryGenerateArmy(ProposalKind kind, int proposerPersonId, int cost, out ProposalArguments arguments)
    {
        arguments = default;
        bool enemy = kind is ProposalKind.MoraleDown or ProposalKind.StunEnemy or ProposalKind.ConfuseEnemy or ProposalKind.BurnEnemy;
        if ((kind is ProposalKind.StunEnemy or ProposalKind.ConfuseEnemy or ProposalKind.BurnEnemy) &&
            PersonAbility(proposerPersonId, 2) <= Rule("enemy-intelligence-threshold", 50)) return false;
        int proposerPhysical = PersonPhysical(proposerPersonId);
        int ownForce = K(Current.ReadForcePhysicalSlot(Current.ReadPersonForce(proposerPhysical) ?? throw Missing("candidate_proposer_force")), "candidate_proposer_force_ordinal");
        int home = K(Current.ReadPersonFirstLocation(proposerPhysical), "candidate_proposer_home");
        var homeBase = BaseLookup.Find(home, Current);
        int homeHex = Current.ReadBaseCurrentHexIndex(homeBase);
        var homeCity = ProposalArmyCity(ProposalAreaCity(homeHex));
        int cityPhysical = K(homeCity.PhysicalSlot, "candidate_home_city");
        if (kind == ProposalKind.FillTroops && K(Current.ReadBaseResource(homeCity, BaseResourceKind.Troops), "candidate_fill_city_ready") <
            unchecked(Rule("fill-troops-amount", 3000) + Rule("fill-city-reserve", 1000))) return false;
        var neighborhood = ProposalNeighborCities(homeCity);
        var grouping = new ArmyMapGroupingCurrentQueries(State, Current, Scenario,
            _proposalArmyPosts ??= OwnedArmyGovernorComparatorInputs.Load(Repository, Scenario, Language), Language, SimulationRuleIdentity.ForLocale(Language));
        var candidates = new List<ArmyUnitReference>();
        // The source walks the fixed pointer table, including its invalid zero
        // receiver, rather than the active registry's order.
        for (int physical = 0; physical < 501; physical++)
        {
            var army = ProposalArmy(physical);
            if (!K(Current.ReadArmyUnitNativeValidity(army), "candidate_army_validity")) continue;
            var image = ProposalArmyImage(physical);
            int formation = K(ArmyConfigurationSourceAdapter.ReadCurrentLand(State, image), "candidate_land_formation");
            if (formation is 19 or 20 || K(grouping.IsAtHome2C96E0(physical), "candidate_at_home")) continue;
            int force = ProposalArmyForce(army);
            if (enemy)
            {
                if (!ProposalEnemy(proposerPersonId, army) || force is >= 46 and <= 51) continue;
            }
            else if (force != ownForce) continue;
            if (ProposalArmyDuplicate(kind, 2, physical)) continue;
            int areaCity = ProposalAreaCity(ProposalArmyHex(army));
            if (kind == ProposalKind.MoraleUp ? areaCity != cityPhysical : areaCity != cityPhysical && !neighborhood.Contains(areaCity)) continue;
            var resources = ProposalArmyResources(army);
            var runtime = image.NativeTacticRuntime ?? throw Missing("candidate_army_runtime");
            bool connected = (runtime.Flags26 & 1) != 0;
            bool eligible = kind switch
            {
                ProposalKind.MoraleUp => ProposalArmyMorale(army) < Rule("morale-up-target-threshold", 100),
                ProposalKind.MoraleDown => ProposalArmyMorale(army) >= Rule("morale-down-target-threshold", 50),
                ProposalKind.RecoverWoundedSoldiers => ProposalWoundedCandidate(resources),
                ProposalKind.BefriendAlly => runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0,
                ProposalKind.StunEnemy => connected && runtime.Abnormal24 == 0 && !ProposalCommanderImmune(army, [23, 21, 98]),
                ProposalKind.ConfuseEnemy => connected && runtime.Abnormal23 == 0 && !ProposalCommanderImmune(army, [22, 21, 98]),
                ProposalKind.BurnEnemy => !ProposalCommanderImmune(army, [25]) && ProposalBurnCandidate(army, ownForce),
                ProposalKind.ExtinguishFriendlyFire => connected && ProposalFire(ProposalArmyHex(army)) != 0,
                ProposalKind.FillTroops => !ProposalArmyDuplicate(kind, 3, cityPhysical) && ProposalArmyVacancy(army) >= Rule("fill-troops-amount", 3000) &&
                    (connected || K(ArmyConfigurationSourceAdapter.ReadAction(State, image), "candidate_fill_action") is >= 4 and < 8) &&
                    unchecked(K(resources.Ready16, "candidate_fill_ready") * 100) / PersonTroopCapacity(ProposalArmyCommander(army)) <= Rule("fill-target-capacity-percent", 50),
                _ => false
            };
            if (eligible) candidates.Add(army);
        }
        if (candidates.Count == 0) return false;
        candidates.Sort((a, b) => ProposalCompareArmy(kind, a, b));
        var selected = candidates[0];
        int target = K(selected.PhysicalSlot, "selected_army");
        if (kind is ProposalKind.MoraleUp or ProposalKind.MoraleDown or ProposalKind.RecoverWoundedSoldiers)
            arguments = new(Math.Max(0, cost), 0, target, ProposalArmyCommander(selected), ProposalArmyForce(selected));
        else if (kind == ProposalKind.FillTroops) arguments = new(Math.Max(0, cost), 0, target, cityPhysical, 0);
        else
        {
            int score = ProposalArmySuccessScore(kind, proposerPersonId, selected);
            bool success = ProposalDifficulty() == 0 || PairSample(1000, proposerPersonId, ProposalArmyCommander(selected)) < score;
            arguments = new(Math.Max(0, cost), 0, target, success ? 1 : 0, Math.Max(0, score));
        }
        return true;
    }

    private bool ProposalArmyDuplicate(ProposalKind kind, int argument, int value) =>
        (State.Proposals ?? throw Missing("candidate_board")).Slots.Any(entry => entry.Kind == kind && entry.Arguments[argument] == value);
    private bool ProposalWoundedCandidate(ArmyReturnResourceFields resources)
    {
        int wounded = K(resources.Wounded18, "candidate_wounded");
        if (wounded == 0) return false;
        int all = unchecked(wounded + K(resources.Ready16, "candidate_ready"));
        return wounded >= Rule("recovery-wounded-threshold", 3000) || unchecked(wounded * 100) / all > Rule("recovery-wounded-percent", 30);
    }
    private bool ProposalCommanderImmune(ArmyUnitReference army, int[] codes)
    {
        int person = PersonPhysical(ProposalArmyCommander(army));
        foreach (int code in codes) if (K(TacticRemakeCurrentTraits.CommanderImmune(Current, person, code), "candidate_commander_immunity")) return true;
        return false;
    }
    private int ProposalAreaCity(int rawHex)
    {
        var hex = Current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("candidate_hex");
        int areaSlot = K(Current.ReadHexAreaSlot(hex), "candidate_hex_area");
        var area = Current.ReadAreaTableReference(areaSlot < 501 ? areaSlot : 0) ?? throw Missing("candidate_area");
        return K(Current.ReadAreaCitySlot(area), "candidate_area_city");
    }
    private HashSet<int> ProposalNeighborCities(BaseReference city)
    {
        var list = Current.CreateTemporaryHexList() ?? throw Missing("candidate_neighbor_list");
        var result = new HashSet<int>();
        try
        {
            new PersonFreedomHomeSource(Scenario).FillCities22F430(Current, city, list);
            var node = Current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("candidate_neighbor_head");
            while (node.Identity.HasValue)
            {
                var value = Current.ReadRegistryNodePayload(node) ?? throw Missing("candidate_neighbor_payload");
                if (value.Kind != RegistryKind.City) throw Missing("candidate_neighbor_kind");
                result.Add(K(value.PhysicalSlot, "candidate_neighbor_city"));
                node = Current.ReadRegistryNodeNext(node) ?? throw Missing("candidate_neighbor_next");
            }
        }
        finally { Current.ClearTemporaryHexList(list); Current.ReleaseTemporaryHexList(list); }
        return result;
    }
    private int ProposalFire(int rawHex) => K(Current.ReadCaptureHexWord16(rawHex < 48400 ? rawHex : 0), "candidate_fire");
    private bool ProposalBurnCandidate(ArmyUnitReference army, int ownForce)
    {
        int hexId = ProposalArmyHex(army);
        if (ProposalFire(hexId) != 0) return false;
        foreach (int neighbor in ProposalBurnNeighbors(hexId))
        {
            if (ProposalFire(neighbor) != 0) return false;
            var hex = Current.ReadHexTableReference(neighbor) ?? throw Missing("burn_neighbor_hex");
            int objectSlot = K(Current.ReadHexObjectLink(hex), "burn_neighbor_object");
            var obj = Current.ReadObjectTableReference(objectSlot < 3001 ? objectSlot : 0) ?? throw Missing("burn_neighbor_object_pointer");
            if (K(Current.ReadObjectNativeValidity(obj), "burn_neighbor_object_validity") &&
                (obj.PhysicalSlot is >= 1 and <= 61 || K(Current.ReadObjectForceSlot13(obj), "burn_neighbor_object_force") == ownForce)) return false;
            int rawArmy = unchecked((short)K(Current.ReadHexRaw1C(hex), "burn_neighbor_army"));
            var other = ProposalArmy(rawArmy < 501 ? rawArmy : 0);
            if (K(Current.ReadArmyUnitNativeValidity(other), "burn_neighbor_army_validity") && ProposalArmyForce(other) == ownForce) return false;
        }
        int areaId = K(Current.ReadHexAreaSlot(Current.ReadHexTableReference(hexId < 48400 ? hexId : 0) ?? throw Missing("burn_hex")), "burn_area_slot");
        var area = Current.ReadAreaTableReference(areaId < 501 ? areaId : 0) ?? throw Missing("burn_area");
        return K(Current.ReadAreaCurrentForceSlot(area), "burn_area_force") == ownForce;
    }
    private static IEnumerable<int> ProposalBurnNeighbors(int rawCenter)
    {
        // 414DF0 calls 2A7CD0 with the raw Army hex and radius 1.
        // Preserve its ring order and clip each output, even when the
        // raw center is the known 48400 sentinel. Hex 0 aliasing belongs
        // to the separate center fire and Area reads above.
        int x = rawCenter % 220, y = rawCenter / 220;
        Step(4);
        foreach (int direction in new[] { 2, 3, 6, 5, 4, 1 })
        {
            Step(direction);
            if ((uint)x < 220 && (uint)y < 220) yield return y * 220 + x;
        }
        void Step(int direction)
        {
            bool odd = (Math.Abs(x) & 1) != 0;
            (int dx, int dy) = direction switch
            {
                1 => (-1, odd ? -1 : 0), 2 => (0, -1), 3 => (1, odd ? -1 : 0),
                4 => (-1, odd ? 0 : 1), 5 => (0, 1), _ => (1, odd ? 0 : 1)
            };
            x += dx; y += dy;
        }
    }
    private int ProposalCompareArmy(ProposalKind kind, ArmyUnitReference a, ArmyUnitReference b)
    {
        int c;
        int Ready(ArmyUnitReference x) => K(ProposalArmyResources(x).Ready16, "sort_ready");
        int Ability(ArmyUnitReference x, int dimension) => PersonAbility(ProposalArmyCommander(x), dimension);
        int Physical(ArmyUnitReference x) => K(x.PhysicalSlot, "sort_physical");
        if (kind is ProposalKind.MoraleUp or ProposalKind.MoraleDown or ProposalKind.RecoverWoundedSoldiers)
        {
            c = kind == ProposalKind.MoraleDown ? ProposalArmyMorale(b).CompareTo(ProposalArmyMorale(a)) : ProposalArmyMorale(a).CompareTo(ProposalArmyMorale(b));
            if (c != 0) return c;
        }
        if (kind == ProposalKind.BefriendAlly)
        {
            int Connected(ArmyUnitReference x) => (ProposalArmyImage(Physical(x)).NativeTacticRuntime ?? throw Missing("sort_runtime")).Flags26 & 1;
            c = Connected(b).CompareTo(Connected(a)); if (c != 0) return c;
        }
        bool readyAscending = kind is ProposalKind.FillTroops or ProposalKind.ExtinguishFriendlyFire;
        c = readyAscending ? Ready(a).CompareTo(Ready(b)) : Ready(b).CompareTo(Ready(a));
        if (c != 0) return c;
        bool hostileTrial = kind is ProposalKind.StunEnemy or ProposalKind.ConfuseEnemy or ProposalKind.BurnEnemy;
        c = hostileTrial ? Ability(a, 2).CompareTo(Ability(b, 2)) : Ability(b, 0).CompareTo(Ability(a, 0));
        return c != 0 ? c : Physical(a).CompareTo(Physical(b));
    }
}
