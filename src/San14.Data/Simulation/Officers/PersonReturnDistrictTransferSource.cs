using San14.Core;

namespace San14.Data;

/// <summary>The reached ruler relocation's 26D2E0(Base,primaryDistrict,ruler,0,1).
/// All nested Base/Person lists borrow the caller's existing current and pool.</summary>
internal static class PersonReturnDistrictTransferSource
{
    internal static void ApplyRulerCurrent(SourceWorldContext current, int person, BaseReference target,
        IArmyReturnGovernorCurrentQueries queries, Action<int, PersonAssignmentPrecallResult> receiveAssignment,
        List<ArmyReturnGovernorPacket> governors, HashSet<int> changedAreas, HashSet<int> changedGates,
        Action<string> observe, DistrictReference? requestedDistrict = null)
    {
        var force = current.ReadPersonForce(person) ?? throw Missing("ruler_Force");
        var primary = requestedDistrict ?? ForcePrimaryDistrictSource.Resolve(current, force);
        int requested = K(current.ReadDistrictPhysicalSlot(primary), "primary_District_physical");
        if (requested is < 1 or > 51) throw Missing("26D2E0_primary_District_validity");
        var oldDistrict = current.ReadBaseDistrictReference(target) ?? throw Missing("old_target_District");
        int newForce = K(current.ReadDistrictForceSlot(primary), "new_District10");
        int location = K(current.ReadBaseObjectLink(target), "target_Base4E");
        var originalArea = CurrentGateMapQueries.FindArea(target, current);
        int originalCitySlot = K(current.ReadAreaValidityCode(originalArea), "target_original_Area34");
        var originalCity = current.ReadCityTableReference(originalCitySlot <= 51 ? originalCitySlot : 0)
            ?? throw Missing("target_original_City");
        if (location < 52)
        {
            int city = K(target.PhysicalSlot, "target_City_physical");
            current.SetCityDistrictSlot(city, (byte)requested);
            observe("26D2E0.City240D90_District");
        }
        else if (K(current.ReadBaseDistrictSlot(originalCity), "original_City78") == requested)
            AssignGate(target, K(originalCity.PhysicalSlot, "original_City_physical"));
        else if (K(current.ReadPersonNativeValidity(person), "fallback_ruler_validity"))
        {
            var rulerHome = BaseLookup.Find(K(current.ReadPersonFirstLocation(person), "fallback_ruler_E2"), current);
            int home = K(current.ReadBaseObjectLink(rulerHome), "fallback_ruler_Base4E");
            if (home > 51) home = K(CurrentGateMapQueries.FindCity(rulerHome, current).PhysicalSlot, "fallback_ruler_City");
            if (home is >= 1 and <= 51) AssignGate(target, home);
        }

        var affected = NewTemporary();
        try
        {
            Append(affected, target);
            if (location < 52)
            {
                var associated = NewTemporary();
                try
                {
                    foreach (var entry in Gates())
                        if (CurrentGateMapQueries.FindCity(entry, current) == target) Append(associated, entry);
                    foreach (var gate in Bases(associated))
                        if (K(current.ReadBaseForceSlot(target), "new_City_Force") ==
                            K(current.ReadBaseForceSlot(gate), "associated_Gate_Force")) Append(affected, gate);
                }
                finally { Release(associated); }
            }
            foreach (var at in Bases(affected))
                foreach (int resident in HomePeople(at))
                {
                    byte rawForce = K(current.ReadPersonOldForceSlot(resident), "resident_Force22CF60");
                    bool transfer = rawForce == newForce;
                    if (!transfer)
                    {
                        var holder = PersonPositionRepair.DecodeSecondLocation(
                            K(current.ReadPersonSecondLocation(resident), "resident_E4"));
                        var army = current.ReadArmyUnitTableReference(holder.Kind == 27 && holder.Index <= 500 ? holder.Index : 0)
                            ?? throw Missing("resident_Army22A200");
                        if (K(current.ReadArmyUnitCommanderId(army), "resident_Army12") !=
                            K(current.ReadPersonIdentity(resident), "resident_Person10"))
                            army = current.ReadArmyUnitTableReference(0) ?? throw Missing("resident_Army_fallback");
                        transfer = !K(current.ReadArmyUnitNativeValidity(army), "resident_Army_validity");
                    }
                    if (!transfer) continue;
                    var results = new List<PersonAssignmentPrecallResult>();
                    var previousRefresh = current.TransferServiceRefresher;
                    current.TransferServiceRefresher = (first, second, third) =>
                        (queries as ArmyReturnDistrictOperationQueries ?? throw Missing("personnel_milestone_operation_owner"))
                        .RefreshPersonnelMilestoneCurrent(first, second, third);
                    try
                    {
                        PersonDistrictTransfer.Apply(resident, requested,
                            ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, results,
                                result => receiveAssignment(resident, result)));
                    }
                    finally { current.TransferServiceRefresher = previousRefresh; }
                    observe("26D2E0.240DB0.Person" + resident);
                }
        }
        finally { Release(affected); }
        //The primary District's related Gates are repaired after the resident
        //loop and its temporary destruction, before the captured old District.
        if (location < 52 && K(current.ReadDistrictNumber(requested), "primary_District11") == 1)
            foreach (var gate in Gates())
            {
                var area = CurrentGateMapQueries.FindArea(gate, current);
                if (K(current.ReadAreaValidityCode(area), "repair_Gate_Area34") != target.PhysicalSlot ||
                    K(current.ReadBaseDistrictSlot(gate), "repair_Gate78") != requested) continue;
                if (CurrentGateMapQueries.FindCity(gate, current) == target) continue;
                AssignGate(gate, K(target.PhysicalSlot, "repair_City_physical"));
            }
        if (K(current.ReadDistrictNativeValidity(oldDistrict), "old_District_validity"))
            governors.AddRange(ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(oldDistrict, current, queries));
        observe("26D2E0.old_District2E9140");
        return;

        void AssignGate(BaseReference gate, int city)
        {
            var area = CurrentGateMapQueries.FindArea(gate, current);
            int previous = K(current.ReadAreaCitySlot(area), "Gate_current_Area35");
            if (previous != city)
            {
                var oldCity = current.ReadCityTableReference(previous <= 51 ? previous : 0)
                    ?? throw Missing("Gate_former_City");
                var district = current.ReadBaseDistrictReference(oldCity) ?? throw Missing("Gate_former_District");
                foreach (int resident in HomePeople(gate))
                {
                    var result = PersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(current, resident, district, oldCity);
                    receiveAssignment(resident, result);
                }
            }
            _ = current.AssignAreaCity(area, city);
            changedAreas.Add(K(area.PhysicalSlot, "Gate_changed_Area"));
            changedGates.Add(K(gate.PhysicalSlot, "changed_Gate_physical"));
            observe("26D2E0.270880_Area35");
        }
        IEnumerable<int> HomePeople(BaseReference at)
        {
            int home = K(current.ReadBaseObjectLink(at), "Home_Base4E");
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_manager100"))
            {
                int resident = K(entry, "Person_registry_pointer");
                if (home is < 1 or > 61 || K(current.ReadPersonFirstLocation(resident), "Home_PersonE2") != home) continue;
                if (K(current.ReadPersonStatus(resident), "Home_PersonE6") is >= 1 and <= 4) yield return resident;
            }
        }
        IEnumerable<BaseReference> Gates() => (current.ReadArmyReturnRegisteredGates()
            ?? throw Missing("Gate_managerA0")).Select(gate => gate ?? throw Missing("Gate_pointer"));
        IEnumerable<BaseReference> Bases(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("Base_temporary_head");
            while (node.Identity.HasValue)
            {
                yield return current.ReadBaseRegistryNodePayload(node) ?? throw Missing("Base_temporary_payload");
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("Base_temporary_next");
            }
        }
        HexListHandle NewTemporary() => current.CreateTemporaryHexList() is { Identity: not null } list
            ? list : throw Missing("temporary_allocation");
        void Append(HexListHandle list, BaseReference at)
        {
            if (!current.AppendCaptureBaseTemporary(list, at)) throw Missing("Base_temporary_node");
            if (at.Kind == BaseKind.Gate) changedGates.Add(K(at.PhysicalSlot, "affected_Gate_physical"));
        }
        void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    }

    internal static GameState PublishAreaAssignments(GameState state, SourceWorldContext current,
        HashSet<int> changed, HashSet<int> changedGates)
    {
        if (changed.Count == 0 && changedGates.Count == 0) return state;
        var areas = new Dictionary<int, AreaState>(state.Areas);
        var cities = new Dictionary<int, CityState>(state.Cities);
        var touchedCities = new HashSet<int>();
        foreach (int id in changed)
        {
            var old = areas.GetValueOrDefault(id) ?? throw Missing("saved_changed_Area");
            int? city = AreaCityProjection.FromNative(K(current.ReadAreaCitySlot(new(id)), "published_Area35"));
            if (city is int receivingCity)
            {
                if (!cities.ContainsKey(receivingCity)) throw Missing("published_Area_City");
                touchedCities.Add(receivingCity);
            }
            if (old.CityId is int previousCity) touchedCities.Add(previousCity);
            areas[id] = old with { CityId = city };
        }
        foreach (int city in touchedCities)
            if (cities.TryGetValue(city, out var value)) cities[city] = value with
                { AreaIds = areas.Values.Where(area => area.CityId == city).Select(area => area.Id).OrderBy(id => id).ToArray() };
        var gates = new Dictionary<int, GateState>(state.Gates);
        foreach (int physical in changedGates)
        {
            var gate = gates.GetValueOrDefault(physical + 51) ?? throw Missing("saved_changed_Gate");
            var at = new BaseReference(BaseKind.Gate, physical);
            int district = K(current.ReadBaseDistrictSlot(at), "published_Gate78");
            int force = K(current.ReadBaseForceSlot(at), "published_GateB0");
            gates[gate.Id] = gate with { DistrictId = district == 0 ? null : district, ForceId = force == 0 ? null : force };
        }
        return state with { Areas = areas, Cities = cities, Gates = gates };
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("person-return.district." + field);
}
