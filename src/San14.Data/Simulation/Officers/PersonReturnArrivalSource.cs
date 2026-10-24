using San14.Core;

namespace San14.Data;

public sealed record PersonReturnArrivalReceipt(int ReceiverIdentity, int PersonPhysical,
    BaseReference Target, ushort Destination, bool Base88Appended, bool HealthFlagWritten,
    int NativeReturn, IReadOnlyList<ArmyReturnGovernorPacket> GovernorUpdates,
    IReadOnlyList<string> SourceEffectOrder)
{
    public IReadOnlyList<PersonReturnArrivalMemberReceipt> Members { get; init; } = [];
}
public sealed record PersonReturnArrivalMemberReceipt(int PersonPhysical, bool Base88Appended,
    bool HealthFlagWritten);
public sealed record PersonReturnArrivalResult(GameState? State,
    PersonReturnArrivalReceipt? Receipt, string? UnresolvedOwner);

/// <summary>32B190→271320 for the ordered members of a real Move or Return.
/// Writes the enclosing operation-local current
/// world; on unknown the caller discards that world. No command retirement,
/// live Army prerequisite, repeated preparation or internal fingerprints.</summary>
public static partial class PersonReturnArrivalSource
{
    public static PersonReturnArrivalResult Apply(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        int receiverIdentity, BaseReference target, IArmyReturnGovernorCurrentQueries queries,
        bool borrowedCurrent = false)
        => ApplyCore(state, current, pool, receiverIdentity, target, queries, borrowedCurrent, null);

    internal static PersonReturnArrivalResult ApplyWithReportsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int receiverIdentity, BaseReference target,
        IArmyReturnGovernorCurrentQueries queries, bool borrowedCurrent,
        Func<GameState, int, PersonAssignmentPrecallResult, GameState> applyReports,
        ArmyCampaignRuleAdapter? package = null, NativeMapSeasonDate? operationDate = null)
        => ApplyCore(state, current, pool, receiverIdentity, target, queries, borrowedCurrent, applyReports, package, operationDate);

    private static PersonReturnArrivalResult ApplyCore(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int receiverIdentity, BaseReference target,
        IArmyReturnGovernorCurrentQueries queries, bool borrowedCurrent,
        Func<GameState, int, PersonAssignmentPrecallResult, GameState>? applyReports,
        ArmyCampaignRuleAdapter? package = null, NativeMapSeasonDate? operationDate = null)
    {
        HexListHandle? caller = null, affected = null;
        var order = new List<string>(); var governors = new List<ArmyReturnGovernorPacket>();
        var working = state;
        try
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Unknown("current.primary_pool_owner");
            var manager = current.ReadOwnedPersonCommandManager() ?? throw Unknown("current.Command_manager");
            var command = manager.Returns.GetValueOrDefault(receiverIdentity) ?? throw Unknown("current.Command_receiver");
            if (command.DispatchSlot is null)
                throw Unknown("branch.invalid_Return_command");
            if (!Known(current.ReadBaseNativeValidity(target), "current.target_Base_validity"))
                throw Unknown("branch.invalid_target_Base");
            caller = NewTemporary(current); order.Add("32B190.caller_Person_temporary");
            var injured = new HashSet<int>();
            foreach (int member in command.MemberIds)
            {
                //The actual caller skips identities outside 1..6000, without
                //normalizing or splitting the task's shared member list.
                if (member is < 1 or > 6000) continue;
                int person = current.ReadPersonLookupReference(member)?.PhysicalSlot
                    ?? throw Unknown("return officer location");
                if (!current.AppendPersonReturnTemporary(caller, person))
                    throw Unknown("branch.caller_Person_node_allocation");
                if (Known(OfficerHealthSourceAdapter.ReadByte(current, person), "current.Person_health161") != 0)
                {
                    ushort flags = Known(current.ReadArmyReturnPersonFlags196(person), "current.Person_flags15E");
                    current.WriteArmyReturnPersonFlags196(person, OfficerBehaviorSourceAdapter.MarkReturn(flags));
                    injured.Add(person); order.Add("32B190.health161_flags15E_bit0");
                }
            }
            if (Known(current.ReadTemporaryHexListCount(caller), "current.caller_count") == 0)
                return new(state, null, null);
            ushort destination = Known(current.ReadBaseObjectLink(target), "current.target_Base4E");
            SortMembers(state, current, caller, queries);
            order.Add("271320.24E440_24E4C0_255970");
            affected = NewTemporary(current); order.Add("271320.affected_Base_temporary");
            var oldBases = new List<BaseReference>();
            var members = new List<PersonReturnArrivalMemberReceipt>();
            var changedAreas = new HashSet<int>();
            var changedGates = new HashSet<int>();
            int? firstPhysical = null;
            bool assignmentWritten = false;
            queries = new ArmyReturnDistrictOperationQueries(queries, () => working, value => working = value,
                ReceiveAssignment, current, changedGates, package, operationDate);
            var node = current.ReadHexListHead(caller) ?? throw Unknown("current.caller_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Unknown("current.caller_payload");
                if (payload.Kind != RegistryKind.Person) throw Unknown("current.caller_payload_kind");
                int person = Known(payload.PhysicalSlot, "current.caller_Person");
                firstPhysical ??= person;
                ushort identity = Known(current.ReadPersonIdentity(person), "current.Person10");
                //233240 is a conditional source identity rule, not a blanket
                //rejection of every special identity. Its disallowed cases
                //skip this member while the enclosing ordered task continues.
                if (ShouldApplyPersonArrivalCurrent(current, person, target, identity))
                {
                    ushort first = Known(current.ReadPersonFirstLocation(person), "current.PersonE2");
                    var old = BaseLookup.Find(first, current);
                    bool appended = false;
                    uint worldFlags = Known(current.ReadWorldTransferServiceFlags(), "current.World16A8");
                    if ((worldFlags & 0x100) == 0)
                    {
                        byte rawDistrict = Known(current.ReadPersonDistrictSlot(person), "current.PersonE0");
                        var oldDistrict = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
                            ?? throw Unknown("current.old_District");
                        if (PlayerRulerDistrict(current, oldDistrict) || PlayerRulerDistrict(current,
                            current.ReadBaseDistrictReference(target) ?? throw Unknown("current.Base_virtual80")))
                        {
                            appended = AppendBase88(current, target, person);
                            order.Add("258C30.target_Base88");
                        }
                    }
                    if (Known(current.ReadBaseNativeValidity(old), "current.old_Base_validity"))
                    {
                        var oldCity = CurrentGateMapQueries.FindCity(old, current);
                        var newCity = CurrentGateMapQueries.FindCity(target, current);
                        if (oldCity != newCity)
                        {
                            var assignment = PersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(current,
                                person, current.ReadBaseDistrictReference(target)
                                    ?? throw Unknown("current.assignment_target_District"), target);
                            if (assignment.NativeReturn is null)
                                throw Unknown(assignment.UnresolvedOwner ?? "current.changed_City_29C430");
                            ReceiveAssignment(person, assignment);
                            assignmentWritten = true; order.Add("271320.changed_City_29C430");
                        }
                    }
                    byte oldDistrictId = Known(current.ReadPersonDistrictSlot(person), "current.transfer_PersonE0");
                    int newDistrictId = Known(current.ReadBaseDistrictSlot(target), "current.transfer_Base78");
                    if (oldDistrictId != newDistrictId && Known(current.ReadPersonStatus(person), "current.transfer_PersonE6") == 1)
                    {
                        PersonReturnDistrictTransferSource.ApplyRulerCurrent(current, person, target, queries,
                            ReceiveAssignment, governors, changedAreas, changedGates, order.Add);
                        assignmentWritten = true;
                    }
                    else if (ArmyReturnCommanderHomeUpdater.ApplyPersonHomeDistrictCurrent(person, target, current,
                        value => ReceiveAssignment(person, value))) assignmentWritten = true;
                    if (Known(current.ReadBaseNativeValidity(old), "current.affected_Base_validity") && !oldBases.Contains(old))
                    {
                        ArmyReturnCommanderHomeUpdater.AppendAffectedBase(current, affected, old, Unknown);
                        oldBases.Add(old);
                    }
                    current.WritePersonFirstLocation(person, (ushort)Math.Clamp((int)destination, 0, 61));
                    order.Add("271767.PersonE2");
                    ApplyArrivalSecondLocationCurrent(current, person, target, destination, order.Add);
                    members.Add(new(person, appended, injured.Contains(person)));
                }
                node = current.ReadHexListNodeNext(node) ?? throw Unknown("current.caller_next");
            }
            //The original updates roles only after every sorted Person has
            //moved. Target first, then pointer-deduped old Bases in visit order.
            UpdateRoles(target);
            foreach (var old in oldBases) UpdateRoles(old);
            ClearRelease(current, affected); affected = null; order.Add("271320.affected_release");
            ClearRelease(current, caller); caller = null; order.Add("32B190.caller_release");
            var result = assignmentWritten ? PersonAssignmentPrecallInputs.PublishCurrent(working, current) : working;
            result = OwnedArmyReturnCurrentFields.PublishCurrent(result, current);
            result = OwnedArmyLifecycleDetachedInputs.PublishCurrent(result, current);
            result = PersonReturnDistrictTransferSource.PublishAreaAssignments(result, current, changedAreas, changedGates);
            result = PersonReturnDistrictReorganizationSource.PublishCurrent(result, current,
                queries as ArmyReturnDistrictOperationQueries);
            result = (result with { PersonnelCommands = current.ReadOwnedPersonCommandManager() }).WithArmyLifecycle(result.ReadArmyLifecycle()! with { Indexes = borrowedCurrent ? pool.ProjectOwnedRoles() : pool.ExportOwnedState() });
            int receiptPerson = Known(firstPhysical, "current.first_caller_Person");
            var firstMember = members.FirstOrDefault(value => value.PersonPhysical == firstPhysical);
            return new(result, new(receiverIdentity, receiptPerson, target, destination,
                firstMember?.Base88Appended ?? false, injured.Contains(receiptPerson), 1,
                governors.AsReadOnly(), order.AsReadOnly()) { Members = members.AsReadOnly() }, null);

            void UpdateRoles(BaseReference at)
            {
                if (Known(current.ReadBaseNativeValidity(at), "current.role_Base_validity"))
                {
                    var update = ArmyReturnGovernorUpdater.PrepareCurrent(at, current, queries);
                    var packet = update.Packet ?? throw Unknown(update.UnresolvedOwner ?? "current.Governor_update");
                    ArmyReturnCommanderHomeUpdater.ApplyGovernorOwned(current, packet);
                    governors.Add(packet); order.Add("282C10→26D090.Base" + at.PhysicalSlot);
                }
                var district = current.ReadBaseDistrictReference(at) ?? throw Unknown("current.role_Base_virtual80");
                governors.AddRange(ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(district, current, queries));
                order.Add("2E9140.primary_District" + district.PhysicalSlot);
            }

            void ReceiveAssignment(int person, PersonAssignmentPrecallResult result)
            {
                if (result.NativeReturn is null) throw Unknown(result.UnresolvedOwner ?? "current.assignment_cleanup");
                if (result.Reports.Count != 0)
                {
                    if (applyReports is null) throw Unknown("current.assignment_history_writer");
                    working = applyReports(working, person, result);
                }
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
