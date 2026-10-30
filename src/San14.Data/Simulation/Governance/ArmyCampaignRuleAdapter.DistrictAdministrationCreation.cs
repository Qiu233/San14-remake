using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The player creation path of 2D7A70, using the caller's live
    /// table, registered chains and list arena. Settings belong to 73F460.</summary>
    internal DistrictReference CreateDistrictAdministrationCurrent(SourceWorldContext current,
        WorldIndexContext pool, ForceReference force, int leaderPhysical,
        IReadOnlyList<BaseReference> orderedBases, ArmyReturnDistrictOperationQueries operation,
        int number = -1, Action<BaseReference, DistrictReference>? transferForeignBase = null)
    {
        if (!ReferenceEquals(current.ReadOwnedCurrentListPool(), pool)) throw DistrictCreationMissing("current_pool");
        int forceSlot = DistrictCreationKnown(current.ReadForcePhysicalSlot(force), "Force60");
        DistrictReference? created = null;
        for (int slot = 0; slot < 52; slot++)
        {
            var candidate = current.ReadDistrictTableReference(slot) ?? throw DistrictCreationMissing("District_table");
            int physical = DistrictCreationKnown(candidate.PhysicalSlot, "District_physical");
            if (physical == 0 || DistrictCreationKnown(current.ReadDistrictNativeValidity(candidate), "District_validity") ||
                physical == 51 || physical is >= 46 and <= 50) continue;
            created = candidate; break;
        }
        if (created is null) throw DistrictCreationMissing("no_free_District");
        int district = DistrictCreationKnown(created.PhysicalSlot, "new_District");
        current.ResetDistrict(district); //226DC0: byte23/2B remain untouched.
        current.SetDistrictRawByte(district, 0x10, (byte)Math.Clamp(forceSlot, 0, 51));
        if (number == -1)
        {
            var numbers = new List<int>();
            foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw DistrictCreationMissing("DistrictD0"))
            {
                int at = DistrictCreationKnown(entry?.PhysicalSlot, "number_District_pointer");
                if (DistrictCreationKnown(current.ReadDistrictForceId(at), "number_District10") == forceSlot)
                    numbers.Add(DistrictCreationKnown(current.ReadDistrictNumber(at), "number_District11"));
            }
            numbers.Sort(); number = 1;
            foreach (int existing in numbers) if (existing == number) number++;
        }
        current.SetDistrictRawByte(district, 0x11, (byte)Math.Clamp(number, 0, 8));
        operation.TouchedDistricts.Add(district);

        int? ruler = null;
        foreach (var at in orderedBases)
        {
            if (DistrictCreationKnown(current.ReadBaseForceSlot(at), "BaseB0") == forceSlot)
                TransferDistrictAdministrationBaseCurrent(current, at, created, operation);
            else if (transferForeignBase is not null) transferForeignBase(at, created);
            else throw DistrictCreationMissing("player_Base_requires_same_Force_26DA10_capture");
            foreach (int person in DistrictCreationHomePeople(current, at))
            {
                if (DistrictCreationKnown(current.ReadPersonStatus(person), "resident_E6") == 1) ruler = person;
                else current.WritePersonStatus(person, 4);
            }
        }
        int selected = ruler ?? leaderPhysical;
        current.WriteDistrictLeader(district, (ushort)Math.Min(
            (int)DistrictCreationKnown(current.ReadPersonIdentity(selected), "leader_Person10"), 6000));
        if (ruler.HasValue) current.WritePersonStatus(leaderPhysical, 4);
        else if (DistrictCreationKnown(current.ReadPersonStatus(leaderPhysical), "selected_E6") != 1)
            current.WritePersonStatus(leaderPhysical, 2);
        DistrictCreationTransferPerson(current, leaderPhysical, district, operation);
        foreach (var at in orderedBases)
        {
            if (!DistrictCreationKnown(current.ReadBaseNativeValidity(at), "governor_Base_validity")) continue;
            var prepared = ArmyReturnGovernorUpdater.PrepareCurrent(at, current, operation);
            ArmyReturnGovernorUpdater.ApplyCurrentWrites(current,
                prepared.Packet ?? throw DistrictCreationMissing(prepared.UnresolvedOwner ?? "282C10"));
        }
        foreach (var at in orderedBases) MaintainDistrictAdministrationAppointmentsCurrent(current, at);
        ObjectRegistration.Register(new(RegistryKind.District, district), current);
        //D2E30 registers this actual new District with the policy manager.
        //It is not a map-presentation notification or a full roster rebuild.
        var state = RegisterOriginalAiDistrictContextCurrent(operation.Latest(), district);
        var districts = new Dictionary<int, DistrictState>(state.Districts)
        {
            [district] = new(district, forceSlot,
                DistrictCreationKnown(current.ReadDistrictNumber(district), "published_number"),
                DistrictCreationKnown(current.ReadDistrictLeader(district), "published_leader"))
            { OrdersRemaining = DistrictCreationKnown(current.ReadDistrictRawByte(district, 0x14), "published_order_books14") }
        };
        operation.Publish(state with { Districts = districts });
        return created;
    }

    /// <summary>26D2E0(Base,District,0,0,1), the same-Force transfer used
    /// by both player creation and editing. The old District is maintained
    /// after residents and related Gate ownership have really changed.</summary>
    internal static void TransferDistrictAdministrationBaseCurrent(SourceWorldContext current,
        BaseReference target, DistrictReference destination, ArmyReturnDistrictOperationQueries operation,
        TransferPersonReference? executor = null)
    {
        int district = DistrictCreationKnown(destination.PhysicalSlot, "transfer_District");
        int force = DistrictCreationKnown(current.ReadDistrictForceSlot(destination), "transfer_District10");
        var old = current.ReadBaseDistrictReference(target) ?? throw DistrictCreationMissing("old_Base_District");
        int location = DistrictCreationKnown(current.ReadBaseObjectLink(target), "Base4E");
        var originalArea = CurrentGateMapQueries.FindArea(target, current);
        int originalCityId = DistrictCreationKnown(current.ReadAreaValidityCode(originalArea), "Area34");
        var originalCity = current.ReadCityTableReference(originalCityId <= 51 ? originalCityId : 0)
            ?? throw DistrictCreationMissing("original_City");
        if (location < 52)
            current.SetCityDistrictSlot(DistrictCreationKnown(target.PhysicalSlot, "City_physical"), (byte)district);
        else if (DistrictCreationKnown(current.ReadBaseDistrictSlot(originalCity), "original_City78") == district)
            AssignGate(target, DistrictCreationKnown(originalCity.PhysicalSlot, "original_City_physical"));
        else if (executor?.PhysicalSlot is int executorPhysical &&
            DistrictCreationKnown(current.ReadPersonNativeValidity(executorPhysical), "executor_validity"))
        {
            var home = BaseLookup.Find(DistrictCreationKnown(current.ReadPersonFirstLocation(executorPhysical), "executor_E2"), current);
            int homeId = DistrictCreationKnown(current.ReadBaseObjectLink(home), "executor_Home4E");
            if (homeId > 51) homeId = DistrictCreationKnown(CurrentGateMapQueries.FindCity(home, current).PhysicalSlot, "executor_HomeCity");
            if (homeId is >= 1 and <= 51) AssignGate(target, homeId);
        }
        // Existing administration callers pass a null executor;149 supplies
        // its actual selected Person, preserving26D2E0's Gate fallback.
        int commanderCount = DistrictCreationKnown(operation.Latest().CommanderSelection?.ActiveCount, "World165D");
        var affected = current.CreateTemporaryHexList() ?? throw DistrictCreationMissing("affected_temporary");
        try
        {
            current.AppendCaptureBaseTemporary(affected, target);
            if (location < 52)
            {
                var associated = current.CreateTemporaryHexList() ?? throw DistrictCreationMissing("Gate_temporary");
                try
                {
                    foreach (var gate in DistrictCreationGates(current))
                        if (CurrentGateMapQueries.FindCity(gate, current) == target)
                            current.AppendCaptureBaseTemporary(associated, gate);
                    foreach (var gate in Bases(associated))
                        if (DistrictCreationKnown(current.ReadBaseForceSlot(target), "City_Force") ==
                            DistrictCreationKnown(current.ReadBaseForceSlot(gate), "Gate_Force"))
                            current.AppendCaptureBaseTemporary(affected, gate);
                }
                finally { Release(associated); }
            }
            foreach (var at in Bases(affected))
            {
                if (at.Kind == BaseKind.Gate)
                    operation.ChangedGates.Add(DistrictCreationKnown(at.PhysicalSlot, "affected_Gate"));
                foreach (int person in DistrictCreationHomePeople(current, at))
                {
                    bool transfer = DistrictCreationKnown(current.ReadPersonOldForceSlot(person), "resident_Force") == force;
                    if (!transfer)
                    {
                        var holder = PersonPositionRepair.DecodeSecondLocation(
                            DistrictCreationKnown(current.ReadPersonSecondLocation(person), "resident_E4"));
                        var army = current.ReadArmyUnitTableReference(holder.Kind == 27 && holder.Index <= 500 ? holder.Index : 0)
                            ?? throw DistrictCreationMissing("resident_Army");
                        if (DistrictCreationKnown(current.ReadArmyUnitCommanderId(army), "Army12") !=
                            DistrictCreationKnown(current.ReadPersonIdentity(person), "resident_ID"))
                            army = current.ReadArmyUnitTableReference(0) ?? throw DistrictCreationMissing("Army0");
                        transfer = !DistrictCreationKnown(current.ReadArmyUnitNativeValidity(army), "Army_validity");
                    }
                    if (!transfer) continue;
                    DistrictCreationTransferPerson(current, person, district, operation);
                    ClearCommanderSelection(person);
                }
            }
        }
        finally { Release(affected); }
        if (location < 52 && DistrictCreationKnown(current.ReadDistrictNumber(district), "District11") == 1)
            foreach (var gate in DistrictCreationGates(current))
            {
                var area = CurrentGateMapQueries.FindArea(gate, current);
                if (DistrictCreationKnown(current.ReadAreaValidityCode(area), "repair_Area34") != target.PhysicalSlot ||
                    DistrictCreationKnown(current.ReadBaseDistrictSlot(gate), "repair_Gate78") != district ||
                    CurrentGateMapQueries.FindCity(gate, current) == target) continue;
                AssignGate(gate, DistrictCreationKnown(target.PhysicalSlot, "repair_City"));
            }
        if (DistrictCreationKnown(current.ReadDistrictNativeValidity(old), "old_District_validity"))
        {
            int previous = DistrictCreationKnown(old.PhysicalSlot, "old_District");
            operation.TouchedDistricts.Add(previous);
            _ = ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(old, current, operation);
        }

        void AssignGate(BaseReference gate, int city)
        {
            var area = CurrentGateMapQueries.FindArea(gate, current);
            int previous = DistrictCreationKnown(current.ReadAreaCitySlot(area), "Gate_Area35");
            if (previous != city)
            {
                var before = current.ReadCityTableReference(previous <= 51 ? previous : 0)
                    ?? throw DistrictCreationMissing("Gate_previous_City");
                var beforeDistrict = current.ReadBaseDistrictReference(before) ?? throw DistrictCreationMissing("Gate_previous_District");
                foreach (int person in DistrictCreationHomePeople(current, gate))
                    operation.Assignment(person, PersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(current, person, beforeDistrict, before));
            }
            current.AssignAreaCity(area, city);
            operation.ChangedAreas.Add(DistrictCreationKnown(area.PhysicalSlot, "changed_Area"));
            operation.ChangedGates.Add(DistrictCreationKnown(gate.PhysicalSlot, "changed_Gate"));
        }
        void ClearCommanderSelection(int person)
        {
            var player = current.ReadCurrentPlayerForce() ?? throw DistrictCreationMissing("commander_Player");
            if (!DistrictCreationKnown(current.ReadForceNativeValidity(player), "commander_Player_validity") ||
                DistrictCreationKnown(current.ReadForcePhysicalSlot(player), "commander_Player60") != force) return;
            ushort leader = DistrictCreationKnown(current.ReadDistrictLeader(district), "commander_District12");
            int leaderAt = DistrictCreationKnown((leader <= 6000 ? current.ReadPersonLookupReference(leader)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "commander_leader_lookup");
            if ((DistrictCreationKnown(current.ReadPersonNativeValidity(leaderAt), "commander_leader_validity") &&
                DistrictCreationKnown(current.ReadPersonStatus(leaderAt), "commander_leader_E6") == 1) || commanderCount == 0) return;
            var state = operation.Latest();
            var selection = state.CommanderSelection ?? throw DistrictCreationMissing("commander_selection");
            var candidates = selection.Candidates ?? throw DistrictCreationMissing("commander_storage");
            if (commanderCount > candidates.Length) throw DistrictCreationMissing("commander_active_prefix");
            int id = DistrictCreationKnown(current.ReadPersonIdentity(person), "commander_Person10");
            var cleared = new List<int>();
            for (int index = 0; index < commanderCount; index++)
                if (DistrictCreationKnown(candidates[index].OfficerId, "commander_candidate") == id) cleared.Add(index);
            if (cleared.Count != 0) operation.Publish(state with { CommanderSelection = selection.ClearAt(cleared) });
        }
        IEnumerable<BaseReference> Bases(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw DistrictCreationMissing("temporary_head");
            while (node.Identity.HasValue)
            {
                yield return current.ReadBaseRegistryNodePayload(node) ?? throw DistrictCreationMissing("temporary_payload");
                node = current.ReadRegistryNodeNext(node) ?? throw DistrictCreationMissing("temporary_next");
            }
        }
        void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    }

    /// <summary>2755F0 retains appointments whose Force and current Home City
    /// still match, and removes only the actual invalidated assignment.</summary>
    internal static void MaintainDistrictAdministrationAppointmentsCurrent(SourceWorldContext current, BaseReference at)
    {
        int location = DistrictCreationKnown(current.ReadBaseObjectLink(at), "appointments_Base4E");
        if (location > 51) return;
        int areaCity = DistrictCreationKnown(at.PhysicalSlot, "appointments_iterator_City");
        int city = DistrictCreationKnown(CurrentGateMapQueries.FindCity(at, current).PhysicalSlot, "appointments_City");
        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw DistrictCreationMissing("appointments_AreaB0"))
        {
            var area = entry ?? throw DistrictCreationMissing("appointments_Area_pointer");
            if (areaCity is < 1 or > 51 || DistrictCreationKnown(current.ReadAreaCitySlot(area), "appointments_Area35") != areaCity) continue;
            int physical = DistrictCreationKnown(area.PhysicalSlot, "appointments_Area");
            ushort old = DistrictCreationKnown(current.ReadAssignmentArea36(physical), "appointments_Area36");
            int person = Person(old);
            if (!DistrictCreationKnown(current.ReadPersonNativeValidity(person), "appointments_Person_validity")) continue;
            int areaForce = DistrictCreationKnown(current.ReadAreaCurrentForceSlot(area), "appointments_Area_Force");
            int personForce = DistrictCreationKnown(current.ReadPersonOldForceSlot(person), "appointments_Person_Force");
            if (areaForce == personForce && SameHomeCity(person)) continue;
            if (old != 0)
            {
                current.SetAssignmentArea36(physical, 0);
                var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, physical, old);
                if (removed.UnresolvedOwner is not null) throw DistrictCreationMissing(removed.UnresolvedOwner);
            }
            current.SetAssignmentArea72(physical, 0);
            if (areaForce != personForce) current.SetAssignmentArea6D(physical, 0);
        }
        ushort recruiter = DistrictCreationKnown(current.ReadAssignmentCityCA(city), "appointments_CityCA");
        if (InvalidDuty(recruiter)) { current.SetAssignmentCityCA(city, 0); current.SetCaptureCityByteCF(city, 0); }
        ushort trainer = DistrictCreationKnown(current.ReadAssignmentCityCC(city), "appointments_CityCC");
        if (InvalidDuty(trainer)) current.SetAssignmentCityCC(city, 0);

        int Person(ushort id) => DistrictCreationKnown((id <= 6000 ? current.ReadPersonLookupReference(id)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "appointments_Person_lookup");
        bool SameHomeCity(int person)
        {
            var home = BaseLookup.Find(DistrictCreationKnown(current.ReadPersonFirstLocation(person), "appointments_PersonE2"), current);
            return DistrictCreationKnown(CurrentGateMapQueries.FindCity(home, current).PhysicalSlot, "appointments_Home_City") == city;
        }
        bool InvalidDuty(ushort id)
        {
            int person = Person(id);
            if (!DistrictCreationKnown(current.ReadPersonNativeValidity(person), "duty_validity")) return false;
            return DistrictCreationKnown(current.ReadPersonStatus(person), "duty_E6") is < 1 or > 4 ||
                DistrictCreationKnown(current.ReadPersonOldForceSlot(person), "duty_Force") !=
                    DistrictCreationKnown(current.ReadBaseForceSlot(at), "duty_Base_Force") || !SameHomeCity(person);
        }
    }

    /// <summary>73E850's remaining Cities. Primary redistribution first plans
    /// every destination against the same current ownership, then transfers in
    /// physical City order. Secondary leftovers return to the actual primary.</summary>
    internal static void RedistributeDistrictAdministrationCitiesCurrent(SourceWorldContext current,
        ForceReference force, DistrictReference edited, IReadOnlyList<int> selectedCityIds,
        ArmyReturnDistrictOperationQueries operation)
    {
        int editedSlot = DistrictCreationKnown(edited.PhysicalSlot, "redistribute_District");
        int editedNumber = DistrictCreationKnown(current.ReadDistrictNumber(editedSlot), "redistribute_District11");
        var selected = selectedCityIds.ToHashSet();
        if (editedNumber != 1)
        {
            var primary = ForcePrimaryDistrictSource.Resolve(current, force);
            if (!DistrictCreationKnown(current.ReadDistrictNativeValidity(primary), "redistribute_primary_validity")) return;
            for (int citySlot = 0; citySlot < 52; citySlot++)
            {
                var city = current.ReadCityTableReference(citySlot) ?? throw DistrictCreationMissing("redistribute_City_table");
                if (selected.Contains(citySlot) || DistrictCreationKnown(current.ReadBaseDistrictSlot(city), "redistribute_City78") != editedSlot) continue;
                TransferDistrictAdministrationBaseCurrent(current, city, primary, operation);
            }
            return;
        }

        //7B3150 keeps the first registered District at the smallest other
        //Number. An absent fallback is legitimate and leaves a City in place.
        int forceSlot = DistrictCreationKnown(current.ReadForcePhysicalSlot(force), "redistribute_Force60");
        DistrictReference? fallback = null;
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw DistrictCreationMissing("redistribute_DistrictD0"))
        {
            var district = entry ?? throw DistrictCreationMissing("redistribute_District_pointer");
            int physical = DistrictCreationKnown(district.PhysicalSlot, "redistribute_District_physical");
            if (DistrictCreationKnown(current.ReadDistrictForceId(physical), "redistribute_District10") != forceSlot ||
                DistrictCreationKnown(current.ReadDistrictNumber(physical), "redistribute_candidate11") == editedNumber) continue;
            if (fallback is null || DistrictCreationKnown(current.ReadDistrictNumber(physical), "redistribute_candidate_number") <
                DistrictCreationKnown(current.ReadDistrictNumber(fallback.PhysicalSlot!.Value), "redistribute_fallback_number"))
                fallback = district;
        }
        var planned = new DistrictReference?[52];
        for (int citySlot = 0; citySlot < 52; citySlot++)
        {
            var city = current.ReadCityTableReference(citySlot) ?? throw DistrictCreationMissing("redistribute_City_table");
            if (selected.Contains(citySlot) || DistrictCreationKnown(current.ReadBaseDistrictSlot(city), "redistribute_City78") != editedSlot) continue;
            DistrictReference? neighborDistrict = null;
            foreach (var neighbor in NeighborCities(city))
            {
                if (DistrictCreationKnown(current.ReadBaseForceSlot(neighbor), "redistribute_neighbor_Force") != forceSlot) continue;
                var district = current.ReadBaseDistrictReference(neighbor) ?? throw DistrictCreationMissing("redistribute_neighbor_District");
                int physical = DistrictCreationKnown(district.PhysicalSlot, "redistribute_neighbor_District_physical");
                if (physical == editedSlot) continue;
                if (neighborDistrict is null || DistrictCreationKnown(current.ReadDistrictNumber(physical), "redistribute_neighbor11") <
                    DistrictCreationKnown(current.ReadDistrictNumber(neighborDistrict.PhysicalSlot!.Value), "redistribute_best11"))
                    neighborDistrict = district;
            }
            var destination = neighborDistrict ?? fallback;
            if (destination is not null && DistrictCreationKnown(current.ReadDistrictNativeValidity(destination), "redistribute_destination_validity"))
                planned[citySlot] = destination;
        }
        for (int citySlot = 0; citySlot < planned.Length; citySlot++)
            if (planned[citySlot] is { } destination)
                TransferDistrictAdministrationBaseCurrent(current,
                    current.ReadCityTableReference(citySlot) ?? throw DistrictCreationMissing("redistribute_apply_City"), destination, operation);

        //22F430 expands at most two consecutive Gates. Pointer identity
        //deduplication keeps the first encounter in the ten-link source order.
        IEnumerable<BaseReference> NeighborCities(BaseReference origin)
        {
            int original = DistrictCreationKnown(current.ReadBaseObjectLink(origin), "redistribute_origin4E");
            var seen = new HashSet<BaseReference>();
            foreach (byte first in Links(origin))
            {
                var a = Linked(first, original);
                if (a is null) continue;
                if (DistrictCreationKnown(current.ReadBaseObjectLink(a), "redistribute_first4E") < 52)
                { if (seen.Add(a)) yield return a; continue; }
                foreach (byte second in Links(a))
                {
                    var b = Linked(second, original);
                    if (b is null) continue;
                    if (DistrictCreationKnown(current.ReadBaseObjectLink(b), "redistribute_second4E") < 52)
                    { if (seen.Add(b)) yield return b; continue; }
                    int firstGate = DistrictCreationKnown(current.ReadBaseObjectLink(a), "redistribute_first_Gate4E");
                    foreach (byte third in Links(b))
                    {
                        if (third == firstGate) continue;
                        var c = Linked(third, original);
                        if (c is not null && DistrictCreationKnown(current.ReadBaseObjectLink(c), "redistribute_third4E") < 52 && seen.Add(c))
                            yield return c;
                    }
                }
            }
        }
        BaseReference? Linked(byte link, int original)
        {
            if (link == 0 || link == original) return null;
            var at = BaseLookup.Find(link, current);
            return DistrictCreationKnown(current.ReadBaseNativeValidity(at), "redistribute_link_validity") ? at : null;
        }
        IEnumerable<byte> Links(BaseReference at)
        {
            var package = operation.Package ?? throw DistrictCreationMissing("redistribute_resource_package");
            int slot = DistrictCreationKnown(at.PhysicalSlot, "redistribute_links_Base");
            var links = (at.Kind == BaseKind.City ? package._scenario.Snapshot.Cities[slot].LinkedLocationSlots
                : package._scenario.Snapshot.Gates[slot].LinkedLocationSlots).Take(10).ToArray();
            if (links.Length != 10) throw DistrictCreationMissing("redistribute_Base58_ten_links");
            return links;
        }
    }

    private static void DistrictCreationTransferPerson(SourceWorldContext current, int person, int district,
        ArmyReturnDistrictOperationQueries operation)
    {
        var results = new List<PersonAssignmentPrecallResult>();
        PersonDistrictTransfer.Apply(person, district, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current,
            results, result => operation.Assignment(person, result)));
    }
    private static IEnumerable<int> DistrictCreationHomePeople(SourceWorldContext current, BaseReference at)
    {
        int home = DistrictCreationKnown(current.ReadBaseObjectLink(at), "Home_Base4E");
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw DistrictCreationMissing("Person100"))
        {
            int person = DistrictCreationKnown(entry, "Person_pointer");
            if (home is >= 1 and <= 61 && DistrictCreationKnown(current.ReadPersonFirstLocation(person), "Home_E2") == home &&
                DistrictCreationKnown(current.ReadPersonStatus(person), "Home_E6") is >= 1 and <= 4) yield return person;
        }
    }
    private static IEnumerable<BaseReference> DistrictCreationGates(SourceWorldContext current) =>
        (current.ReadArmyReturnRegisteredGates() ?? throw DistrictCreationMissing("GateA0"))
            .Select(value => value ?? throw DistrictCreationMissing("Gate_pointer"));
    private static T DistrictCreationKnown<T>(T? value, string field) where T : struct => value ?? throw DistrictCreationMissing(field);
    private static InvalidDataException DistrictCreationMissing(string field) => new("district.create." + field);
}
