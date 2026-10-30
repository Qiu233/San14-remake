using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    private bool TryGenerateCityOther(ProposalKind kind, int proposerPersonId, int cost, out ProposalArguments arguments)
    {
        arguments = default;
        if (kind is ProposalKind.BuyFood or ProposalKind.SellFood)
            return CityGenerateTrade(kind, proposerPersonId, out arguments);
        if (kind == ProposalKind.RepairEndurance)
        {
            int person = PersonPhysical(proposerPersonId), district = K(Current.ReadPersonDistrictSlot(person), "repair_person_district");
            int home = K(Current.ReadPersonFirstLocation(person), "repair_person_home");
            var candidates = CityBases().Where(target => K(Current.ReadBaseNativeValidity(target), "repair_validity") &&
                K(Current.ReadBaseDistrictSlot(target), "repair_district") == district &&
                !CityDuplicate(kind, K(Current.ReadBaseObjectLink(target), "repair_link")) &&
                (K(Current.ReadBaseObjectLink(target), "repair_link") == home || CityRawLinks(target).Any(link => link == home)) &&
                (BasePostLoad.ReadLinkedField14(target, Current) <= Rule("repair-low-wall", 1000) ||
                 unchecked(BasePostLoad.ReadLinkedField14(target, Current) * 100) / BasePostLoad.ReadCapacities(target, Current).Field46 < Rule("repair-low-wall-percent", 50)))
                .OrderBy(target => BasePostLoad.ReadLinkedField14(target, Current))
                .ThenBy(target => CityResource(target, BaseResourceKind.Troops))
                .ThenBy(target => K(Current.ReadBaseObjectLink(target), "repair_sort_link")).ToList();
            if (candidates.Count == 0) return false;
            arguments = new(Math.Max(0, cost), 0, K(Current.ReadBaseObjectLink(candidates[0]), "repair_target"), 0, 0);
            return true;
        }
        return CityGenerateTerritory(kind, proposerPersonId, cost, out arguments);
    }

    private void ApplyCityOther(ProposalEntry entry)
    {
        if (entry.Kind is ProposalKind.BuyFood or ProposalKind.SellFood)
        {
            var city = CityReference(entry.Arguments.Parameter2);
            int food = entry.Arguments.Parameter3, gold = entry.Arguments.Parameter4;
            int sign = entry.Kind == ProposalKind.BuyFood ? 1 : -1;
            CityWriteResource(city, BaseResourceKind.Gold, Math.Clamp(unchecked(CityResource(city, BaseResourceKind.Gold) - sign * gold), 0, CityCapacity(city, BaseResourceKind.Gold)));
            CityWriteResource(city, BaseResourceKind.Supplies, Math.Clamp(unchecked(CityResource(city, BaseResourceKind.Supplies) + sign * food), 0, CityCapacity(city, BaseResourceKind.Supplies)));
            ushort location = K(Current.ReadBaseObjectLink(city), "trade_report_city");
            Report(entry, entry.Kind == ProposalKind.BuyFood ? 0x4F7E : 0x4F86,
                new(Person0: entry.ProposerPersonId, Person1: CityReportRuler(), Base0: location, Integers: [food, gold, 0]),
                entry.Kind == ProposalKind.BuyFood ? 0x4F85 : 0x4F8D,
                new(Base0: location, Integers: [food, gold, 0]), Current.ReadBaseCurrentHexIndex(city));
            //The same ordinary City food warning producer runs after a trade.
            State = Package.ApplyProposalCityFoodWarning(State, Current, city);
            Reward(entry.ProposerPersonId, entry.Kind, 3, food / Rule("trade-reward-divisor", 500) + Rule("trade-reward-base", 10), true);
            return;
        }
        if (entry.Kind == ProposalKind.RepairEndurance)
        {
            var target = CityBase(entry.Arguments.Parameter2);
            int initial = BasePostLoad.ReadLinkedField14(target, Current), cap = BasePostLoad.ReadCapacities(target, Current).Field46;
            int gain = Math.Min(unchecked(CityRepairGain(target, entry.ProposerPersonId) *
                (Rule("repair-duration", 800) / Rule("repair-duration-divisor", 150))),
                unchecked(Rule("repair-target-percent", 80) * cap) / 100 - initial);
            BasePostLoad.WriteLinkedField14(target, unchecked(initial + gain), Current);
            byte flag = BasePostLoad.ReadFlag66(target, Current);
            Current.SetBasePostLoadFlag66(target, flag);
            CityPublishWall(target, flag);
            int after = BasePostLoad.ReadLinkedField14(target, Current);
            ushort location = K(Current.ReadBaseObjectLink(target), "repair_report_base");
            EnterScene(0x36);
            Speech(entry, 0x4F5D, new(Person0: entry.ProposerPersonId, Person1: CityReportRuler(), Base0: location,
                Integers: [after - initial]));
            ExitScene();
            History(entry, 0x4F65, new(Base0: location, Integers: [after - initial, after, 0]), Current.ReadBaseCurrentHexIndex(target));
            Reward(entry.ProposerPersonId, entry.Kind, 3,
                gain / Rule("repair-reward-divisor", 30) + Rule("repair-reward-base", 20), true);
            return;
        }
        CityApplyTerritory(entry);
    }

    private int CityReportRuler() => K(Current.ReadMoraleWaveRuler22EAC0(PlayerForce), "city_report_ruler");

    private IReadOnlyList<byte> CityRawLinks(BaseReference target) => BaseNeighborLinksSource.Read(Scenario, target);
    private int[] CityNeighbors(int city)
    {
        var list = Current.CreateTemporaryHexList() ?? throw Missing("city_neighbor_list");
        var values = new List<int>();
        try
        {
            new PersonFreedomHomeSource(Scenario).FillCities22F430(Current, CityReference(city), list);
            var node = Current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("city_neighbor_head");
            while (node.Identity.HasValue)
            {
                var item = Current.ReadRegistryNodePayload(node) ?? throw Missing("city_neighbor_payload");
                if (item.Kind != RegistryKind.City) throw Missing("city_neighbor_kind");
                values.Add(K(item.PhysicalSlot, "city_neighbor_identity"));
                node = Current.ReadRegistryNodeNext(node) ?? throw Missing("city_neighbor_next");
            }
        }
        finally { Current.ClearTemporaryHexList(list); Current.ReleaseTemporaryHexList(list); }
        return values.ToArray();
    }
    private HashSet<int> CitySecurityDownTargets(int person)
    {
        int force = K((Current.ReadPersonForce(person) ?? throw Missing("security_down_force")).PhysicalSlot, "security_down_force_identity");
        var result = new HashSet<int>();
        foreach (var entry in Current.ReadRegisteredAreaReferences() ?? throw Missing("security_down_area_registry"))
        {
            var area = entry ?? throw Missing("security_down_area_pointer");
            int id = K(area.PhysicalSlot, "security_down_area_identity");
            int city = K(Current.ReadAreaCitySlot(area), "security_down_city");
            if (K(Current.ReadBaseForceSlot(CityReference(city)), "security_down_city_force") != force &&
                !CityNeighbors(city).Any(neighbor => K(Current.ReadBaseForceSlot(CityReference(neighbor)), "security_down_neighbor_force") == force)) continue;
            int target = CityAreaForce(id);
            var targetForce = Current.ReadForceTableReference(target < 52 ? target : 0) ?? throw Missing("security_down_target_force");
            if (!K(Current.ReadForceNativeValidity(targetForce), "security_down_target_validity") || target is >= 46 and <= 50 || target == force) continue;
            var native = State.NativeArmySimulation ?? throw Missing("security_down_native_owner");
            var own = native.Forces.GetValueOrDefault(force) ?? throw Missing("security_down_own_owner");
            var other = native.Forces.GetValueOrDefault(target) ?? throw Missing("security_down_target_owner");
            byte[] treaties = other.RelationsEA is { Length: 52 } bytes ? bytes : throw Missing("security_down_relations");
            if ((treaties[force] & 127) != 0 || (K(own.Flags12, "security_down_own_flags") & 2) != 0 && (K(other.Flags12, "security_down_target_flags") & 2) != 0) continue;
            result.Add(id);
        }
        return result;
    }

    private int CityRepairGain(BaseReference target, int personId)
    {
        if (!K(Current.ReadBaseNativeValidity(target), "repair_base_validity")) return 0;
        int cap = BasePostLoad.ReadCapacities(target, Current).Field46, wall = BasePostLoad.ReadLinkedField14(target, Current);
        if (wall >= cap) return 0;
        int person = PersonPhysical(personId), politics = K(Current.ReadPersonNativeValidity(person), "repair_person_validity") ? Math.Max(20, PersonAbility(personId, 3)) : 20;
        int sample = Next(politics);
        int basis = Math.Clamp(unchecked((politics + sample) * 2), 40, 400);
        var actorForce = Current.ReadPersonForce(person) ?? throw Missing("repair_person_force");
        int policy = ForcePolicyScratchCurrent.ReadBeforeFreedom(State, Current, actorForce, 0x27, true).Value;
        int trait = 0, location = K(Current.ReadBaseObjectLink(target), "repair_location");
        foreach (int? physical in Current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("repair_person_registry"))
        {
            int candidate = K(physical, "repair_resident_pointer");
            if (K(Current.ReadPersonFirstLocation(candidate), "repair_resident_location") != location ||
                K(Current.ReadPersonStatus(candidate), "repair_resident_status") is < 1 or > 4 || PersonCurrentBusySource.Read(Current, candidate, Missing)) continue;
            trait = K(TacticRemakeCurrentTraits.PersonContribution(Current, candidate, 0x2B), "repair_resident_trait");
            if (trait != 0) break;
        }
        int term = unchecked(trait * basis) / 100;
        term = trait > 0 ? Math.Max(1, term) : trait < 0 ? Math.Min(-1, term) : 0;
        int force = K(Current.ReadBaseForceSlot(target), "repair_target_force");
        var owner = Current.ReadForceTableReference(force < 52 ? force : 0) ?? throw Missing("repair_target_force_pointer");
        int advantage = K(PersonReturnPathSource.ReadForceAdvantage(State, Current, owner, 4).Value, "repair_advantage");
        int gain = unchecked(basis + unchecked(advantage * basis) / 100 + term + unchecked(basis * policy) / 100);
        int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(State), "repair_selector");
        if (selector == 0) gain = unchecked(K(BattleRemakeCurrentSettings.ReadAdvancedParameter(State, 6), "repair_advanced") * gain) / 100;
        else
        {
            int mode = K(BattleRemakeCurrentSettings.ReadInt32(State, 0xC8), "repair_mode");
            gain = unchecked((mode == 0 ? 80 : mode == 2 ? 120 : 100) * gain) / 100;
        }
        return Math.Min(gain, cap - wall);
    }
    private void CityPublishWall(BaseReference target, byte flag)
    {
        int id = K(target.PhysicalSlot, "wall_identity");
        int location = K(Current.ReadBaseObjectLink(target), "wall_object");
        var obj = Current.ReadObjectTableReference(location < 3001 ? location : 0) ?? throw Missing("wall_object_pointer");
        int value = K(Current.ReadObjectRawField14(obj), "wall_stored_value");
        if (target.Kind == BaseKind.City)
        {
            var owner = State.Cities.GetValueOrDefault(id) ?? throw Missing("wall_city_owner");
            State = State with { Cities = new(State.Cities) { [id] = owner with { Durability = value, DamageTier = flag } } };
        }
        else
        {
            var owner = State.Gates.GetValueOrDefault(id) ?? throw Missing("wall_gate_owner");
            State = State with { Gates = new(State.Gates) { [id] = owner with { Durability = value, DamageTier = flag } } };
        }
    }
}
