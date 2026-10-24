using San14.Core;

namespace San14.Data;

/// <summary>State and report ownership for a reached District operation.
/// The existing comparator/associated getters remain with their actual provider.</summary>
internal sealed class ArmyReturnDistrictOperationQueries(IArmyReturnGovernorCurrentQueries queries,
    Func<GameState> latest, Action<GameState> publish, Action<int, PersonAssignmentPrecallResult> assignment,
    SourceWorldContext current, HashSet<int> changedGates, ArmyCampaignRuleAdapter? package = null,
    NativeMapSeasonDate? date = null) : IArmyReturnGovernorCurrentQueries
{
    internal Func<GameState> Latest { get; } = latest;
    internal Action<GameState> Publish { get; } = publish;
    internal Action<int, PersonAssignmentPrecallResult> Assignment { get; } = assignment;
    internal ArmyCampaignRuleAdapter? Package { get; } = package;
    internal NativeMapSeasonDate? Date { get; } = date;
    internal HashSet<int> ChangedAreas { get; } = [];
    internal HashSet<int> ChangedGates { get; } = changedGates;
    internal HashSet<int> TouchedDistricts { get; } = [];
    public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int person) => queries.ReadCurrentComparatorFields(person);
    public int? ReadCurrentAssociatedVirtual98(PersonLinkedEntityReference reference) => queries.ReadCurrentAssociatedVirtual98(reference);
    internal int Leadership(int person) => Package?.ReadReturnDistrictLeadership(Latest(), current, person)
        ?? throw new InvalidDataException("person-return.district.leadership_rules");
}

internal static class PersonReturnDistrictReorganizationSource
{
    //2E9140 ruler-empty branch: retain other Districts in D0 order, sort by
    //actual Base route node count, merge into the first, then promote Number1.
    internal static IReadOnlyList<ArmyReturnGovernorPacket> InheritPrimaryCurrent(SourceWorldContext current,
        DistrictReference old, ForceReference force, ArmyReturnDistrictOperationQueries operation)
    {
        var candidates = Temporary(current);
        try
        {
            foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("inherit_DistrictD0"))
            {
                var candidate = entry ?? throw Missing("inherit_District_pointer");
                int physical = K(candidate.PhysicalSlot, "inherit_District_physical");
                int actualForce = K(current.ReadForcePhysicalSlot(force), "inherit_Force_identity");
                if (actualForce is < 1 or > 51 ||
                    K(current.ReadDistrictForceId(physical), "inherit_District10") != actualForce || candidate == old) continue;
                var node = current.AllocateRegistryNode(new(candidates.Identity));
                if (node?.Identity is not null)
                    current.WriteRegistryNodePayload(node, new(RegistryKind.District, physical));
            }
            BaseReference origin = Home(K(old.PhysicalSlot, "inherit_old"));
            var route = Temporary(current);
            try
            {
                Sort(current, candidates, Before);
            }
            finally { Release(current, route); }
            var head = current.ReadRegistryHead(new(candidates.Identity)) ?? throw Missing("inherit_head");
            if (head.Identity is null) throw Missing("inherit_native_empty_District_candidates");
            int selected = K(current.ReadRegistryNodePayload(head)?.PhysicalSlot, "inherit_selected_District");
            var updates = MergeCurrent(current, old, new(selected), operation);
            current.SetDistrictRawByte(selected, 0x11, 1);
            operation.TouchedDistricts.Add(selected);
            return updates;

            bool Before(int left, int right)
            {
                int leftDistance = Distance(left), rightDistance = Distance(right);
                if (leftDistance != rightDistance) return leftDistance < rightDistance;
                return K(current.ReadDistrictNumber(left), "inherit_left_number") <
                    K(current.ReadDistrictNumber(right), "inherit_right_number");
            }
            int Distance(int district)
            {
                current.ClearTemporaryHexList(route);
                var target = Home(district);
                if (!K(current.ReadBaseNativeValidity(origin), "inherit_source_Base_validity") ||
                    !K(current.ReadBaseNativeValidity(target), "inherit_target_Base_validity")) return 48400;
                if (K(current.ReadBaseForceSlot(origin), "inherit_source_Base_Force") !=
                    K(current.ReadBaseForceSlot(target), "inherit_target_Base_Force")) return 48400;
                var hexes = ArmyRouteSearch.DistrictBaseRoute(operation.Latest(), current, origin, target);
                if (hexes.Length == 0) return 48400;
                foreach (int hex in hexes)
                    current.AppendTemporaryHexReference(route, current.ReadHexTableReference(hex)
                        ?? throw Missing("inherit_route_Hex_pointer"));
                return K(current.ReadTemporaryHexListCount(route), "inherit_route_count");
            }
        }
        finally { Release(current, candidates); }

        BaseReference Home(int district)
        {
            int leader = Person(current, K(current.ReadDistrictLeader(district), "inherit_leader12"));
            return BaseLookup.Find(K(current.ReadPersonFirstLocation(leader), "inherit_leaderE2"), current);
        }
    }

    internal static void ReselectLeader(SourceWorldContext current, DistrictReference district,
        IArmyReturnGovernorCurrentQueries queries)
    {
        var operation = queries as ArmyReturnDistrictOperationQueries ?? throw Missing("leader_state_owner");
        int id = K(district.PhysicalSlot, "leader_District");
        int previous = Person(current, K(current.ReadDistrictLeader(id), "leader12"));
        if (K(current.ReadPersonStatus(previous), "former_leaderE6") == 2) current.WritePersonStatus(previous, 4);
        var list = Temporary(current);
        try
        {
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
            {
                int person = K(entry, "Person_pointer");
                if (K(current.ReadPersonDistrictSlot(person), "candidate_E0") != id ||
                    K(current.ReadPersonStatus(person), "candidate_E6") is < 1 or > 4) continue;
                ushort home = K(current.ReadPersonFirstLocation(person), "candidate_E2");
                if (home is >= 1 and <= 61)
                {
                    var force = current.ReadPersonForce(person) ?? throw Missing("candidate_Force");
                    int physical = K(current.ReadForcePhysicalSlot(force), "candidate_Force60");
                    if (physical is >= 46 and <= 50 || Special(K(current.ReadPersonIdentity(person), "candidate_ID"))) continue;
                }
                if (!current.AppendPersonReturnTemporary(list, person)) throw Missing("candidate_node");
            }
            Sort(current, list, Before);
            var head = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("candidate_head");
            if (!head.Identity.HasValue) throw Missing("native_empty_leader_candidates");
            int selected = K(current.ReadRegistryNodePayload(head)?.PhysicalSlot, "selected_Person");
            current.WriteDistrictLeader(id, Math.Min(K(current.ReadPersonIdentity(selected), "selected_Person10"), (ushort)6000));
            if (K(current.ReadPersonStatus(selected), "selected_E6") != 1) current.WritePersonStatus(selected, 2);
            operation.TouchedDistricts.Add(id);
        }
        finally { Release(current, list); }

        bool Before(int left, int right)
        {
            byte a = K(current.ReadPersonStatus(left), "sort_E6"), b = K(current.ReadPersonStatus(right), "sort_E6");
            if (a == 1 && b != 1) return true;
            if (a != 1 && b == 1) return false;
            int Capacity(int person)
            {
                var value = OfficerCommandCapacity.Resolve(operation.Latest(), current, person);
                return value.Capacity ?? throw Missing(value.UnresolvedInput ?? "command_capacity2B2CF0");
            }
            int first = Capacity(left), second = Capacity(right);
            if (second < first) return true;
            if (second > first) return false;
            byte lo = K(queries.ReadCurrentComparatorFields(left)?.PersonOfficeE7, "sort_officeE7");
            byte ro = K(queries.ReadCurrentComparatorFields(right)?.PersonOfficeE7, "sort_officeE7");
            if (lo == 0 && ro != 0) return false;
            if (lo != 0 && ro == 0) return true;
            if (lo < ro) return true;
            if (lo > ro) return false;
            first = operation.Leadership(left); second = operation.Leadership(right);
            if (second < first) return true;
            return second == first && K(current.ReadPersonIdentity(left), "sort_ID") < K(current.ReadPersonIdentity(right), "sort_ID");
        }
    }

    //293490(District, retained primary,0): serving and captive members move
    //before Cities, then the real District24 stock, group and registry tails.
    internal static IReadOnlyList<ArmyReturnGovernorPacket> MergeCurrent(SourceWorldContext current,
        DistrictReference old, DistrictReference target, ArmyReturnDistrictOperationQueries operation)
    {
        int from = K(old.PhysicalSlot, "merge_old_District"), to = K(target.PhysicalSlot, "merge_target_District");
        if (from == to) throw Missing("merge_primary_self");
        var governors = new List<ArmyReturnGovernorPacket>();
        int former = Person(current, K(current.ReadDistrictLeader(from), "merge_leader12"));
        byte status = K(current.ReadPersonStatus(former), "merge_former_E6");
        if (status is >= 2 and <= 4) current.WritePersonStatus(former, 4);
        var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(former), "merge_former_E2"), current);
        Update(home);
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("merge_Person100"))
        {
            int person = K(entry, "merge_Person_pointer");
            if (K(current.ReadPersonDistrictSlot(person), "merge_PersonE0") != from) continue;
            byte serving = K(current.ReadPersonStatus(person), "merge_PersonE6");
            if (serving is not (>= 1 and <= 4) && serving != 6) continue;
            var results = new List<PersonAssignmentPrecallResult>();
            PersonDistrictTransfer.Apply(person, to, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current,
                results, result => operation.Assignment(person, result)));
            if (serving is >= 1 and <= 4)
            {
                var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("merge_associated");
                if (K(current.ReadLinkedEntityNativeValidity(linked), "merge_associated_validity"))
                    operation.Publish(PersonReturnCommands.CancelAssociatedCommandCurrent(operation.Latest(), current,
                        K(linked.Identity, "merge_associated_receiver"), operation.Date ?? throw Missing("merge_actual_cut_date")));
            }
        }
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw Missing("merge_City_table");
            if (K(current.ReadBaseDistrictSlot(city), "merge_City78") != from) continue;
            if (!K(current.ReadDistrictNativeValidity(target), "merge_target_validity"))
                throw Missing("neutral_District_City_resources");
            current.SetCityDistrictSlot(slot, (byte)Math.Clamp(to, 0, 51));
            Update(city);
            foreach (var gate in Gates(current))
                if (CurrentGateMapQueries.FindCity(gate, current) == city)
                { Update(gate); operation.ChangedGates.Add(K(gate.PhysicalSlot, "merge_Gate_physical")); }
        }
        if (K(current.ReadDistrictNativeValidity(old), "merge_old_validity") &&
            K(current.ReadDistrictNativeValidity(target), "merge_target_stock_validity"))
        {
            int amount = Word24(current, from);
            if (amount > 0)
            {
                int oldCap = Cap(current, operation.Latest(), from), targetCap = Cap(current, operation.Latest(), to);
                Write24(current, from, Math.Min(0, oldCap));
                int total = Math.Min(unchecked(amount + Word24(current, to)), targetCap);
                Write24(current, to, total < 0 ? 0 : Math.Min(total, targetCap));
                operation.TouchedDistricts.Add(to);
            }
        }
        ResetGroups(current, from, operation);
        foreach (var at in (current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("merge_DistrictD0")).ToArray())
        {
            int physical = K(at?.PhysicalSlot, "merge_route_District");
            if (K(current.ReadDistrictRawByte(physical, 0x19), "merge_route_kind") != 8 ||
                K(current.ReadDistrictRawByte(physical, 0x18), "merge_route_target") != from) continue;
            current.SetDistrictRawByte(physical, 0x17, 1); current.SetDistrictRawByte(physical, 0x18, 0);
            current.SetDistrictRawByte(physical, 0x19, 0xFF); current.SetDistrictRawByte(physical, 0x1A, 0);
            current.SetDistrictRawByte(physical, 0x1B, 0xFF); operation.TouchedDistricts.Add(physical);
        }
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.District) ?? throw Missing("merge_D0_descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("merge_D0_head");
        while (node.Identity.HasValue)
        {
            var next = current.ReadRegistryNodeNext(node) ?? throw Missing("merge_D0_next");
            if (current.ReadRegistryNodePayload(node) == new RegistryObjectReference(RegistryKind.District, from))
            { current.UnlinkArmyReturnTraitNode(descriptor, node); break; }
            node = next;
        }
        current.ResetDistrict(from); operation.TouchedDistricts.Add(from);
        return governors;

        void Update(BaseReference at)
        {
            if (!K(current.ReadBaseNativeValidity(at), "merge_Base_validity")) return;
            var value = ArmyReturnGovernorUpdater.PrepareCurrent(at, current, operation);
            var packet = value.Packet ?? throw Missing(value.UnresolvedOwner ?? "merge_Governor");
            ArmyReturnGovernorUpdater.ApplyCurrentWrites(current, packet); governors.Add(packet);
        }
    }

    internal static GameState PublishCurrent(GameState state, SourceWorldContext current,
        ArmyReturnDistrictOperationQueries? operation = null)
    {
        if (operation is null || operation.TouchedDistricts.Count == 0) return state;
        var owner = state.NativeForceUnregisterCurrent ?? throw Missing("saved_District_windows");
        var windows = new Dictionary<int, byte?[]>(owner.DistrictWindows10);
        var districts = new Dictionary<int, DistrictState>(state.Districts);
        foreach (int id in operation.TouchedDistricts)
        {
            windows[id] = Enumerable.Range(0x10, 28).Select(at => current.ReadDistrictRawByte(id, at)).ToArray();
            if (!districts.TryGetValue(id, out var before)) continue;
            bool valid = K(current.ReadDistrictNativeValidity(id), "published_District_validity");
            int leader = K(current.ReadDistrictLeader(id), "published_District_leader");
            districts[id] = before with { ForceId = K(current.ReadDistrictForceId(id), "published_District_force"),
                Number = K(current.ReadDistrictNumber(id), "published_District_number"),
                LeaderId = leader == 0 ? null : leader,
                OrdersRemaining = valid ? before.OrdersRemaining : 0 };
        }
        return state with { Districts = districts, NativeForceUnregisterCurrent = owner with { DistrictWindows10 = windows } };
    }

    private static void ResetGroups(SourceWorldContext current, int district, ArmyReturnDistrictOperationQueries operation)
    {
        //293490 walks80BA8 (CCautionTroopsData), not the military group table.
        //Its invalid rows short-circuit before the stored District14 read.
        //The existing caution owner has labels/members but no District14 yet.
        for (int slot = 0; slot < 501; slot++)
        {
            var reference = current.ReadRegistryTableReference(RegistryKind.CautionTroops, slot)
                ?? throw Missing("caution_group_table");
            if (!K(current.ReadRegistryObjectNativeValidity(reference), "caution_group_validity")) continue;
            throw Missing("caution_group_stored_District14_for_32B570");
        }
    }
    private static int Cap(SourceWorldContext current, GameState state, int district)
    {
        if (state.NativeArmySimulation?.Parameters.Language != "TC") throw Missing("SC_District24_cap_table");
        int force = K(current.ReadDistrictForceId(district), "budget_Force");
        int rank = state.Forces.GetValueOrDefault(force)?.RankId ?? throw Missing("budget_current_rank47");
        //TC231350's immutable data table, extracted offline2026-10-24.
        return rank switch { 1 or 2 or 3 or 10 or 11 or 12 => 200000, 4 or 5 => 150000, 6 or 7 => 100000, _ => 50000 };
    }
    private static int Word24(SourceWorldContext current, int district)
    {
        uint value = 0;
        for (int i = 0; i < 4; i++) value |= (uint)K(current.ReadDistrictRawByte(district, 0x24 + i), "District24") << (i * 8);
        return unchecked((int)value);
    }
    private static void Write24(SourceWorldContext current, int district, int value)
    { for (int i = 0; i < 4; i++) current.SetDistrictRawByte(district, 0x24 + i, unchecked((byte)(value >> (i * 8)))); }
    private static IEnumerable<BaseReference> Gates(SourceWorldContext current) =>
        (current.ReadArmyReturnRegisteredGates() ?? throw Missing("GateA0")).Select(value => value ?? throw Missing("Gate_pointer"));
    private static int Person(SourceWorldContext current, ushort id) => K((id <= 6000
        ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
    private static bool Special(ushort id) => Enumerable.Range(0, 5).Any(i => id >= 4111 + i * 20 && id <= 4120 + i * 20);
    private static HexListHandle Temporary(SourceWorldContext current) => current.CreateTemporaryHexList()
        ?? throw Missing("temporary");
    private static void Release(SourceWorldContext current, HexListHandle list)
    { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    private static void Sort(SourceWorldContext current, HexListHandle list, Func<int, int, bool> before)
    {
        var order = new List<HexListNodeReference>();
        var node = current.ReadHexListHead(list) ?? throw Missing("sort_head");
        while (node.Identity.HasValue) { order.Add(node); node = current.ReadHexListNodeNext(node) ?? throw Missing("sort_next"); }
        int PersonAt(HexListNodeReference at) => K(current.ReadRegistryNodePayload(new(at.Identity))?.PhysicalSlot, "sort_payload");
        for (int i = 1; i < order.Count; i++)
        {
            var value = order[i]; int at = i;
            while (at > 0 && before(PersonAt(value), PersonAt(order[at - 1])))
            { order[at] = order[at - 1]; at--; }
            order[at] = value;
        }
        current.ReorderCurrentList(list, order);
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("person-return.district-reorganization." + field);
}
