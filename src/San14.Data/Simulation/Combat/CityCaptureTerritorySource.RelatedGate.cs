using San14.Core;

namespace San14.Data;

public static partial class CityCaptureTerritorySource
{
    //26E83B..26F3E0 are the Gate-Area arm of the same26DA10 operation.
    //The source retains its District list until every related Area is done.
    private static CityCaptureAreaReceipt ApplyRelatedGateAreaCurrent(SourceWorldContext current,
        CityCaptureAreaScope scope, AreaReference area, HexListHandle districts,
        ArmyReturnDistrictOperationQueries operation, PersonFreedomHomeSource neighborhoods)
    {
        int areaId = K(area.PhysicalSlot, "related_Gate_Area_identity");
        var assignments = new List<AreaCityAssignmentResult>();
        var hex = AreaHex(current, area);
        ushort link = K(current.ReadHexObjectLink(hex), "related_Gate_Hex1A");
        var gate = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0)
            ?? throw Missing("related_Gate_lookup22D0D0");
        ushort governorId = K(current.ReadArmyReturnBaseGovernor2E(gate), "related_Gate_governor2E");
        int governor = K((current.ReadPersonLookupReference(governorId <= 6000 ? governorId : 0)
            ?? throw Missing("related_Gate_governor_pointer")).PhysicalSlot, "related_Gate_governor_physical");
        //233780 is only raw Person status equality; there is no validity test.
        bool governorRuler = K(current.ReadPersonStatus(governor), "related_Gate_governor_status") == 1;
        int areaForce = K(current.ReadAreaCurrentForceSlot(area), "related_Gate_Area_HexForce");
        var owner = current.ReadForceTableReference(areaForce <= 51 ? areaForce : 0)
            ?? throw Missing("related_Gate_Area_Force_pointer");
        int gateForce = K(current.ReadBaseForceSlot(gate), "related_Gate_B0");
        if (gateForce == 0)
        {
            Assign(0);
            return Receipt();
        }
        if (K(current.ReadForcePhysicalSlot(owner), "related_Gate_Area_Force_physical") == scope.NewForcePhysical)
        {
            int capturedCity = K(current.ReadBaseObjectLink(scope.City), "related_Gate_captured_City4E");
            if (capturedCity > 51) capturedCity = 0;
            if (K(current.ReadAreaValidityCode(area), "related_Gate_original_City") != capturedCity) return Receipt();
            //Both native v78 calls re-fetch this same associated City.
            int firstDistrict = K(current.ReadBaseDistrictSlot(AssociatedCity()), "related_Gate_first_City78");
            int secondDistrict = K(current.ReadBaseDistrictSlot(AssociatedCity()), "related_Gate_second_City78");
            if (firstDistrict == secondDistrict || governorRuler)
            {
                int destination = K(current.ReadBaseDistrictSlot(AssociatedCity()), "related_Gate_destination78");
                ArmyCampaignRuleAdapter.TransferDistrictAdministrationBaseCurrent(current, gate,
                    GateDistrict(current, destination), operation);
            }
            else
                MoveHomePeople(current, gate, () => AssociatedCity(), districts, operation);
            return Receipt();
        }

        int previousCity = K(current.ReadAreaCitySlot(area), "related_Gate_previous_City35");
        if (previousCity > 51) previousCity = 0;
        if (previousCity != K(current.ReadBaseObjectLink(scope.City), "related_Gate_captured_location")) return Receipt();
        var candidates = current.CreateNearbyTemporary();
        try
        {
            var neighbors = current.CreateNearbyTemporary();
            try
            {
                neighborhoods.FillCities22F430(current, gate, neighbors);
                foreach (var city in GateCities(current, neighbors))
                {
                    int candidateForce = K(current.ReadBaseForceSlot(city), "related_Gate_neighbor_Force");
                    if (candidateForce is >= 1 and <= 51 &&
                        candidateForce == K(current.ReadAreaCurrentForceSlot(area), "related_Gate_current_AreaForce"))
                        _ = current.AppendCaptureBaseTemporary(candidates, city);
                }
            }
            finally { current.ClearNearbyTemporary(neighbors, true); }
            if (!GateHasNodes(current, candidates))
            {
                //2243A0 visits the actual City pointer table in slot order.
                int forcePhysical = K(current.ReadForcePhysicalSlot(owner), "related_Gate_fallback_Force");
                if (forcePhysical is >= 1 and <= 51)
                    for (int slot = 0; slot < 52; slot++)
                    {
                        var city = current.ReadCityTableReference(slot) ?? throw Missing("related_Gate_fallback_City");
                        if (K(current.ReadBaseForceSlot(city), "related_Gate_fallback_CityForce") == forcePhysical)
                            _ = current.AppendCaptureBaseTemporary(candidates, city);
                    }
            }
            if (!GateHasNodes(current, candidates)) return Receipt();
            SortRelatedGateCities(current, candidates, gate, operation.Latest());
            var selected = GateCities(current, candidates).First();
            int oldDistrict = K(current.ReadBaseDistrictSlot(AssociatedCity()), "related_Gate_old_City78");
            Assign(K(selected.PhysicalSlot, "related_Gate_selected_City"));
            int newDistrict = K(current.ReadBaseDistrictSlot(selected), "related_Gate_new_City78");
            if (oldDistrict != newDistrict)
            {
                if (governorRuler)
                {
                    int rawForce = K(current.ReadBaseForceSlot(gate), "related_Gate_primary_Force");
                    var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                        ?? throw Missing("related_Gate_primary_Force_pointer");
                    var primary = ForcePrimaryDistrictSource.Resolve(current, force);
                    //The native receiver here is the selected City, not Gate.
                    ArmyCampaignRuleAdapter.TransferDistrictAdministrationBaseCurrent(current, selected,
                        GateDistrict(current, K(primary.PhysicalSlot, "related_Gate_primary_District_physical")), operation);
                }
                else
                    MoveHomePeople(current, gate, () => selected, districts, operation);
            }
        }
        finally { current.ClearNearbyTemporary(candidates, true); }
        return Receipt();

        BaseReference AssociatedCity()
        {
            int raw = K(current.ReadAreaCitySlot(area), "related_Gate_associated_City35");
            return current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw Missing("related_Gate_associated_City");
        }
        void Assign(int city)
        {
            assignments.Add(current.AssignAreaCity(area, city));
            operation.ChangedAreas.Add(areaId);
            operation.ChangedGates.Add(K(gate.PhysicalSlot, "related_Gate_changed_Gate"));
        }
        CityCaptureAreaReceipt Receipt() => new(areaId, assignments.ToArray(), null);
    }

    private static void CompleteRelatedGateDistrictsCurrent(SourceWorldContext current,
        HexListHandle districts, ArmyReturnDistrictOperationQueries operation)
    {
        var at = current.ReadRegistryHead(new(districts.Identity)) ?? throw Missing("related_Gate_District_head");
        while (at.Identity.HasValue)
        {
            var payload = current.ReadRegistryNodePayload(at) ?? throw Missing("related_Gate_District_payload");
            if (payload.Kind != RegistryKind.District) throw Missing("related_Gate_District_kind");
            int physical = K(payload.PhysicalSlot, "related_Gate_District_identity");
            operation.TouchedDistricts.Add(physical);
            _ = ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(new(physical), current, operation,
                allowLeaderless: true, useCaptureForceRetirement: false);
            at = current.ReadRegistryNodeNext(at) ?? throw Missing("related_Gate_District_next");
        }
    }

    private static void MoveHomePeople(SourceWorldContext current, BaseReference gate,
        Func<BaseReference> targetCity, HexListHandle districts, ArmyReturnDistrictOperationQueries operation)
    {
        int home = K(current.ReadBaseObjectLink(gate), "related_Gate_Home4E");
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("related_Gate_Home_manager100"))
        {
            int person = K(entry, "related_Gate_Home_Person");
            if (home is < 1 or > 61 || K(current.ReadPersonFirstLocation(person), "related_Gate_HomeE2") != home) continue;
            byte status = K(current.ReadPersonStatus(person), "related_Gate_Home_status");
            if (status is < 1 or > 4) continue;
            if (status == 2)
            {
                AddDistrict(GateDistrict(current, K(current.ReadPersonDistrictSlot(person), "related_Gate_old_PersonE0")));
                AddDistrict(current.ReadBaseDistrictReference(targetCity()) ?? throw Missing("related_Gate_target_City80"));
                current.WritePersonStatus(person, 4);
                var state = operation.Latest();
                int id = K(current.ReadPersonIdentity(person), "related_Gate_changed_Person10");
                var officer = state.Officers.GetValueOrDefault(id) ?? throw Missing("related_Gate_changed_Person_owner");
                var service = OfficerServiceStatusSourceAdapter.Decode(4);
                operation.Publish(state with { Officers = new(state.Officers) { [id] = officer with
                    { ServiceStatus = service, Role = OfficerServiceRules.PublishedRole(service, officer.Role) } } });
            }
            int destination = K(current.ReadBaseDistrictSlot(targetCity()), "related_Gate_person_target78");
            var previousRefresh = current.TransferServiceRefresher;
            current.TransferServiceRefresher = operation.RefreshPersonnelMilestoneCurrent;
            try
            {
                PersonDistrictTransfer.Apply(person, destination,
                    ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, [], result => operation.Assignment(person, result)));
            }
            finally { current.TransferServiceRefresher = previousRefresh; }
        }

        void AddDistrict(DistrictReference district)
        {
            int physical = K(district.PhysicalSlot, "related_Gate_collected_District");
            var at = current.ReadRegistryHead(new(districts.Identity)) ?? throw Missing("related_Gate_collected_head");
            while (at.Identity.HasValue)
            {
                if (current.ReadRegistryNodePayload(at) == new RegistryObjectReference(RegistryKind.District, physical)) return;
                at = current.ReadRegistryNodeNext(at) ?? throw Missing("related_Gate_collected_next");
            }
            var node = current.AllocateRegistryNode(new(districts.Identity)) ?? throw Missing("related_Gate_District_allocator");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.District, physical));
        }
    }

    //24C630/24C6F0 moves real nodes using the complete2541E0 comparator.
    //It has no distance key. Strict comparisons retain stable tie order.
    private static void SortRelatedGateCities(SourceWorldContext current, HexListHandle cities,
        BaseReference gate, GameState state)
    {
        var order = new List<HexListNodeReference>();
        var at = current.ReadHexListHead(cities) ?? throw Missing("related_Gate_sort_head");
        while (at.Identity.HasValue)
        { order.Add(at); at = current.ReadHexListNodeNext(at) ?? throw Missing("related_Gate_sort_next"); }
        BaseReference City(HexListNodeReference node) => current.ReadBaseRegistryNodePayload(new(node.Identity))
            ?? throw Missing("related_Gate_sort_City");
        for (int index = 1; index < order.Count; index++)
        {
            var value = order[index]; int destination = index;
            while (destination > 0 && Before(City(value), City(order[destination - 1])))
            { order[destination] = order[destination - 1]; destination--; }
            order[destination] = value;
        }
        current.ReorderCurrentList(cities, order);

        bool Before(BaseReference left, BaseReference right)
        {
            int sourceDistrict = K(current.ReadBaseDistrictSlot(gate), "related_Gate_sort_source78");
            bool a = K(current.ReadBaseDistrictSlot(left), "related_Gate_sort_left78") == sourceDistrict;
            bool b = K(current.ReadBaseDistrictSlot(right), "related_Gate_sort_right78") == sourceDistrict;
            if (a != b) return a;
            int sourceState = State(CurrentGateMapQueries.FindCity(gate, current));
            int leftState = State(left), rightState = State(right);
            a = leftState == sourceState; b = rightState == sourceState;
            if (a != b) return a;
            int region = K(BaseCaptureRewardInputs.ReadStateRegion(state, sourceState <= 22 ? sourceState : 0), "related_Gate_sort_source_State2E");
            a = K(BaseCaptureRewardInputs.ReadStateRegion(state, leftState <= 22 ? leftState : 0), "related_Gate_sort_left_State2E") == region;
            b = K(BaseCaptureRewardInputs.ReadStateRegion(state, rightState <= 22 ? rightState : 0), "related_Gate_sort_right_State2E") == region;
            if (a != b) return a;
            a = PrimaryLeader(left); b = PrimaryLeader(right);
            if (a != b) return a;
            int rawForce = K(current.ReadBaseForceSlot(left), "related_Gate_sort_left_Force");
            var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("related_Gate_sort_Force");
            int physical = K(force.PhysicalSlot, "related_Gate_sort_Force_physical");
            ushort ruler = current.ReadForceRecord(physical)?.RawRuler10 ?? throw Missing("related_Gate_sort_Force_ruler10");
            int person = K((current.ReadPersonLookupReference(ruler <= 6000 ? ruler : 0)
                ?? throw Missing("related_Gate_sort_Ruler_pointer")).PhysicalSlot, "related_Gate_sort_Ruler_physical");
            var capital = BaseLookup.Find(K(current.ReadPersonFirstLocation(person), "related_Gate_sort_RulerE2"), current);
            int capitalLocation = K(current.ReadBaseObjectLink(capital), "related_Gate_sort_Capital4E");
            a = K(current.ReadBaseObjectLink(left), "related_Gate_sort_left4E") == capitalLocation;
            b = K(current.ReadBaseObjectLink(right), "related_Gate_sort_right4E") == capitalLocation;
            if (a != b) return a;
            return K(left.PhysicalSlot, "related_Gate_sort_left_physical") < K(right.PhysicalSlot, "related_Gate_sort_right_physical");
        }
        int State(BaseReference city)
        {
            int physical = K(city.PhysicalSlot, "related_Gate_sort_State_City");
            var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("related_Gate_sort_State_owner");
            if (bank.Int32Values.GetValueOrDefault(PersonGrowthCurrentQueries.VersionKey) != 1 ||
                !bank.Int32Values.TryGetValue(PersonGrowthCurrentQueries.CityStateKey(physical), out int value) || value is < 0 or > 255)
                throw Missing("related_Gate_sort_CityCE");
            return value;
        }
        bool PrimaryLeader(BaseReference city)
        {
            var district = current.ReadBaseDistrictReference(city) ?? throw Missing("related_Gate_sort_City80");
            int physical = K(district.PhysicalSlot, "related_Gate_sort_District_physical");
            ushort leader = K(current.ReadDistrictLeader(physical), "related_Gate_sort_District12");
            int person = K((current.ReadPersonLookupReference(leader <= 6000 ? leader : 0)
                ?? throw Missing("related_Gate_sort_leader_pointer")).PhysicalSlot, "related_Gate_sort_leader_physical");
            return K(current.ReadPersonNativeValidity(person), "related_Gate_sort_leader_validity") &&
                K(current.ReadPersonStatus(person), "related_Gate_sort_leader_status") == 1;
        }
    }

    private static DistrictReference GateDistrict(SourceWorldContext current, int raw) =>
        current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw Missing("related_Gate_District_lookup");
    private static bool GateHasNodes(SourceWorldContext current, HexListHandle list) =>
        (current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("related_Gate_list_head")).Identity.HasValue;
    private static IEnumerable<BaseReference> GateCities(SourceWorldContext current, HexListHandle list)
    {
        var at = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("related_Gate_City_head");
        while (at.Identity.HasValue)
        {
            yield return current.ReadBaseRegistryNodePayload(at) ?? throw Missing("related_Gate_City_payload");
            at = current.ReadRegistryNodeNext(at) ?? throw Missing("related_Gate_City_next");
        }
    }
}
