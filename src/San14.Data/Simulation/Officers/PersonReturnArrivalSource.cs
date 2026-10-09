using San14.Core;

namespace San14.Data;

public sealed record PersonReturnArrivalReceipt(int ReceiverIdentity, int PersonPhysical,
    BaseReference Target, ushort Destination, bool Base88Appended, bool HealthFlagWritten,
    int NativeReturn, IReadOnlyList<ArmyReturnGovernorPacket> GovernorUpdates,
    IReadOnlyList<string> SourceEffectOrder);
public sealed record PersonReturnArrivalResult(GameState? State,
    PersonReturnArrivalReceipt? Receipt, string? UnresolvedOwner);

/// <summary>32B190→271320 for a real Return with one ordinary person, their
/// original City and District. Writes the enclosing operation-local current
/// world; on unknown the caller discards that world. No command retirement,
/// live Army prerequisite, repeated preparation or internal fingerprints.</summary>
public static class PersonReturnArrivalSource
{
    public static PersonReturnArrivalResult Apply(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        int receiverIdentity, BaseReference target, IArmyReturnGovernorCurrentQueries queries)
    {
        HexListHandle? caller = null, affected = null;
        var order = new List<string>(); var governors = new List<ArmyReturnGovernorPacket>();
        try
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Unknown("current.primary_pool_owner");
            var manager = current.ReadOwnedPersonCommandManager() ?? throw Unknown("current.Command_manager");
            var command = manager.Tasks.GetValueOrDefault(receiverIdentity) ?? throw Unknown("current.Command_receiver");
            if (command.DispatchSlot is null)
                throw Unknown("branch.invalid_Return_command");
            if (!Known(current.ReadBaseNativeValidity(target), "current.target_Base_validity"))
                throw Unknown("branch.invalid_target_Base");
            if (command.MemberIds.Length != 1) throw Unknown("return arrival requires one officer");
            int member = command.MemberIds[0];
            if (member is < 1 or > 6000) throw Unknown("invalid return officer identity");
            int person = current.ReadPersonLookupReference(member)?.PhysicalSlot
                ?? throw Unknown("return officer location");
            byte status = Known(current.ReadPersonStatus(person), "current.PersonE6");
            ushort flags = Known(current.ReadArmyReturnPersonFlags196(person), "current.Person_flags15E");
            ushort identity = Known(current.ReadPersonIdentity(person), "current.Person10");
            if (status is < 1 or > 4 || OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(person)) == false || SpecialId(identity))
                throw Unknown("branch.nonordinary_Person");
            ushort destination = Known(current.ReadBaseObjectLink(target), "current.target_Base4E");
            ushort first = Known(current.ReadPersonFirstLocation(person), "current.PersonE2");
            if (target.Kind != BaseKind.City || destination is < 1 or > 51 || first != destination)
                throw Unknown("branch.not_original_City");
            var old = BaseLookup.Find(first, current);
            if (old != target || BaseLookup.Find(destination, current) != target)
                throw Unknown("branch.not_original_Base_pointer");
            byte personDistrict = Known(current.ReadPersonDistrictSlot(person), "current.PersonE0");
            if (personDistrict != Known(current.ReadBaseDistrictSlot(target), "current.Base_virtual78"))
                throw Unknown("branch.different_District");
            if (CurrentGateMapQueries.FindCity(old, current) != CurrentGateMapQueries.FindCity(target, current))
                throw Unknown("branch.different_City");
            var district = current.ReadCityDistrictReference(target) ?? throw Unknown("current.Base_virtual80");
            var health = OfficerHealthSourceAdapter.Read(current, person) ?? throw Unknown("current.Person_health161");
            uint worldFlags = Known(current.ReadWorldTransferServiceFlags(), "current.World16A8");
            bool admit = (worldFlags & 0x100) == 0 && PlayerRulerDistrict(current, district);

            caller = NewTemporary(current); order.Add("32B190.caller_Person_temporary");
            if (!current.AppendPersonReturnTemporary(caller, person)) throw Unknown("branch.caller_Person_node_allocation");
            if (OfficerHealthRules.IsInjured(health))
            {
                current.WriteArmyReturnPersonFlags196(person, OfficerBehaviorSourceAdapter.MarkReturn(flags));
                order.Add("32B190.health161_flags15E_bit0");
            }
            //24E4C0's real one-node list has no next node: no comparator calls.
            affected = NewTemporary(current); order.Add("271320.affected_Base_temporary");
            bool appended = admit && AppendBase88(current, target, person);
            if (admit) order.Add("258C30.target_Base88");
            AppendBase(current, affected, old);
            current.WritePersonFirstLocation(person, destination); order.Add("271767.PersonE2");
            current.WritePersonSecondLocation(person, destination); order.Add("2717B2.PersonE4");
            Governor(target); Primary(district); //2717DD,271801.
            Governor(old); Primary(district); //27185A,27187E: repeat even old==target.
            ClearRelease(current, affected); affected = null; order.Add("271320.affected_release");
            ClearRelease(current, caller); caller = null; order.Add("32B190.caller_release");
            var result = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
            result = OwnedArmyLifecycleDetachedInputs.PublishCurrent(result, current);
            result = result with { NativeArmyLifecycle = result.NativeArmyLifecycle! with { Indexes = pool.ExportOwnedState() },
                PersonnelReturns = current.ReadOwnedPersonCommandManager() };
            NativeArmyLifecycleStateValidation.Validate(result);
            return new(result, new(receiverIdentity, person, target, destination, appended, OfficerHealthRules.IsInjured(health), 1,
                governors.AsReadOnly(), order.AsReadOnly()), null);

            void Governor(BaseReference at)
            {
                var update = ArmyReturnGovernorUpdater.PrepareCurrent(at, current, queries);
                var packet = update.Packet ?? throw Unknown(update.UnresolvedOwner ?? "current.Governor_update");
                ArmyReturnCommanderHomeUpdater.ApplyGovernorOwned(current, packet);
                governors.Add(packet); order.Add("282C10→26D090.Base" + at.PhysicalSlot);
            }
            void Primary(DistrictReference at)
            {
                governors.AddRange(ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(at, current, queries));
                order.Add("2E9140.primary_District" + at.PhysicalSlot);
            }
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
        finally
        {
            if (affected is not null) ClearRelease(current, affected);
            if (caller is not null) ClearRelease(current, caller);
        }
    }

    private static bool PlayerRulerDistrict(SourceWorldContext current, DistrictReference district)
    {
        var player = current.ReadCurrentPlayerForce() ?? throw Unknown("current.Player_Force");
        if (!Known(current.ReadForceNativeValidity(player), "current.Player_Force_validity")) return false;
        int physical = Known(district.PhysicalSlot, "current.District_pointer");
        if (Known(current.ReadDistrictForceId(physical), "current.District_force10") !=
            Known(current.ReadForcePhysicalSlot(player), "current.Player_Force_physical")) return false;
        ushort leader = Known(current.ReadDistrictLeader(physical), "current.District_leader12");
        var person = current.ReadPersonLookupReference(leader <= 6000 ? leader : 0) ?? throw Unknown("current.District_leader_pointer");
        return person.PhysicalSlot is int at && Known(current.ReadPersonNativeValidity(at), "current.District_leader_validity") &&
            Known(current.ReadPersonStatus(at), "current.District_leaderE6") == 1;
    }
    private static bool AppendBase88(SourceWorldContext current, BaseReference target, int person)
    {
        var handle = current.ReadArmyReturnBasePerson88Descriptor(target) ?? throw Unknown("current.Base88_descriptor");
        var node = current.ReadRegistryHead(handle) ?? throw Unknown("current.Base88_head");
        while (node.Identity.HasValue)
        {
            var value = current.ReadRegistryNodePayload(node) ?? throw Unknown("current.Base88_payload");
            if (value.Kind != RegistryKind.Person) throw Unknown("current.Base88_payload_kind");
            if (value.PhysicalSlot == person) return false;
            node = current.ReadRegistryNodeNext(node) ?? throw Unknown("current.Base88_next");
        }
        var added = current.AllocateRegistryNode(handle) ?? throw Unknown("current.Base88_node");
        if (!added.Identity.HasValue) throw Unknown("branch.Base88_node_allocation");
        current.WriteRegistryNodePayload(added, new(RegistryKind.Person, person));
        return true;
    }
    private static void AppendBase(SourceWorldContext current, HexListHandle list, BaseReference target)
    {
        var node = current.AllocateBaseRegistryNode(new(list.Identity)) ?? throw Unknown("current.affected_Base_node");
        if (!node.Identity.HasValue) throw Unknown("branch.affected_Base_node_allocation");
        current.WriteBaseRegistryNodePayload(node, target);
    }
    private static HexListHandle NewTemporary(SourceWorldContext current)
    {
        var list = current.CreateTemporaryHexList() ?? throw Unknown("current.temporary_descriptor");
        if (!list.Identity.HasValue) throw Unknown("branch.temporary_descriptor_allocation");
        return list;
    }
    private static void ClearRelease(SourceWorldContext current, HexListHandle list)
    { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    private static bool SpecialId(ushort id)
    { for (int i = 0; i < 5; i++) if (id >= 4111 + i * 20 && id <= 4120 + i * 20) return true; return false; }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Unknown(field);
    private static InvalidDataException Unknown(string field) => new("Person Return arrival source unresolved: " + field);
}
