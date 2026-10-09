using San14.Core;
namespace San14.Data;

public sealed record ArmyReturnCommanderHomeResolution(ArmyReturnCommanderHomePacket? Packet, string? UnresolvedOwner);
public sealed class ArmyReturnCommanderHomePacket
{
    public ArmyUnitReference Army { get; }
    public BaseReference Base { get; }
    public int CommanderPhysical { get; }
    public bool Person88Appended { get; }
    public IReadOnlyList<ArmyReturnGovernorPacket> GovernorUpdates { get; }
    public PersonAssignmentPrecallResult? AssignmentPrecall { get; }
    internal ArmyReturnCommanderHomePacket(ArmyUnitReference army, BaseReference target, int commander,
        bool appended, ArmyReturnGovernorPacket[] updates, PersonAssignmentPrecallResult? assignmentPrecall)
    {
        Army = army; Base = target; CommanderPhysical = commander; Person88Appended = appended;
        GovernorUpdates = Array.AsReadOnly(updates); AssignmentPrecall = assignmentPrecall;
    }
}

/// <summary>29F220's successful one-Commander temporary-list call to271320:
/// ordinary role1..4, same Force/District with same or changed City. Executes actual
/// owned Base+88 admission, Person locations, governor/District calls and
/// ordered governor updates on the caller owned operation context before publication.
/// This is not the preceding trait cleanup or the remaining whole29F220.</summary>
public static class ArmyReturnCommanderHomeUpdater
{
    internal static ArmyReturnCommanderHomeResolution ApplyCurrent(ArmyUnitReference army,
        BaseReference target, SourceWorldContext context, WorldIndexContext pool,
        IArmyReturnGovernorCurrentQueries queries, bool transportEnter = false)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(pool); ArgumentNullException.ThrowIfNull(queries);
        try
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, context) || !pool.OwnsArmyReturnProfile())
                throw Missing("current.complete_owned_shared_pool");
            if (context.RegistryLockEnabledReader is not null || context.RegistryDescriptorKeyReader is not null ||
                context.RegistryHeadTablePresenceReader is not null || context.RegistryLockEnabled != false ||
                context.RegistryHeadTablePresent != true)
                throw Missing("current.owned_sequential_list_profile");
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            byte armyStatus = Get(context.ReadArmyReturnUnitStatus10(army), "current.Army_status10");
            if (armyStatus != 1 && !(transportEnter && armyStatus == 2))
                throw Missing("branch.not_ordinary_army_return_caller");
            ushort commanderId = Get(context.ReadArmyUnitCommanderId(army), "current.Army_commander12");
            int commander = (context.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)
                ?? throw Missing("current.Commander_lookup")).PhysicalSlot ?? throw Missing("current.Commander_pointer");
            if (!Get(context.ReadPersonNativeValidity(commander), "current.Commander_validity")) throw Missing("branch.invalid_commander");
            byte status = Get(context.ReadPersonStatus(commander), "current.Commander_statusE6");
            bool ordinaryPerson = Get(OfficerBehaviorRules.UsesOrdinaryPersonRules(context.ReadPersonBehavior(commander)), "current.Commander_flags15E");
            ushort first = Get(context.ReadPersonFirstLocation(commander), "current.Commander_firstE2");
            ushort second = Get(context.ReadPersonSecondLocation(commander), "current.Commander_secondE4");
            ushort identity = Get(context.ReadPersonIdentity(commander), "current.Commander_id10");
            if (status is < 1 or > 4 || !ordinaryPerson || SpecialId(identity)) throw Missing("branch.nonordinary_commander");
            if (!Get(context.ReadBaseNativeValidity(target), "current.target_Base_validity"))
                throw Missing("branch.nonordinary_target_base");
            ushort location = Get(context.ReadBaseObjectLink(target), "current.target_Base_word4E");
            if (location is < 1 or > 61 || first is < 1 or > 61) throw Missing("branch.commander_invalid_original_base");
            var actualTarget = BaseLookup.Find(location, context);
            var oldBase = BaseLookup.Find(first, context);
            if (actualTarget != target || !Get(context.ReadBaseNativeValidity(oldBase), "current.old_Base_validity"))
                throw Missing("branch.commander_invalid_original_base_pointer");
            byte personDistrict = Get(context.ReadPersonDistrictSlot(commander), "current.Commander_districtE0");
            int baseDistrict = Get(context.ReadBaseDistrictSlot(target), "current.target_Base_virtual78");
            if (personDistrict != baseDistrict) throw Missing("branch.commander_different_district");
            var district = context.ReadBaseDistrictReference(target) ?? throw Missing("current.target_Base_virtual80");
            //27153B/54D still perform both real map queries before their equality
            //test, even though the already proved saved Base pointer is equal.
            var oldCity = CurrentGateMapQueries.FindCity(oldBase, context);
            var newCity = CurrentGateMapQueries.FindCity(target, context);
            bool changedCity = oldCity != newCity;
            uint worldFlags = Get(context.ReadWorldTransferServiceFlags(), "current.World16A8");
            bool admit88 = (worldFlags & 0x100) == 0 && PlayerRulerDistrict(context, district);
            var draft = context;
            bool appended = false;
            if (admit88) appended = AppendBase88(draft, target, commander);
            PersonAssignmentPrecallResult? assignmentPrecall = null;
            if (changedCity)
            {
                assignmentPrecall = PersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(draft, commander, district, target);
                if (assignmentPrecall.NativeReturn is null)
                    throw Missing(assignmentPrecall.UnresolvedOwner ?? "current.29C430_changed_City");
            }
            draft.WritePersonFirstLocation(commander, location); //271767.
            draft.WritePersonSecondLocation(commander, location); //2717B2,role1..4.
            var updates = new List<ArmyReturnGovernorPacket>();
            ApplyGovernor(target); ApplyPrimary(district); //2717DD/271801.
            ApplyGovernor(oldBase);
            ApplyPrimary(draft.ReadBaseDistrictReference(oldBase) ?? throw Missing("current.old_Base_District")); //27185A/27187E.
            return new(new(army, target, commander, appended, updates.ToArray(), assignmentPrecall), null);

            void ApplyGovernor(BaseReference at)
            {
                var result = ArmyReturnGovernorUpdater.PrepareCurrent(at, draft, queries);
                var update = result.Packet ?? throw Missing(result.UnresolvedOwner ?? "current.Governor_unknown");
                ApplyGovernorOwned(draft, update); updates.Add(update);
            }
            void ApplyPrimary(DistrictReference at)
            {
                updates.AddRange(ApplyPrimaryOwned(at, draft, queries));
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.CommanderHome_input: " + error.Message); }
    }

    private static bool PlayerRulerDistrict(SourceWorldContext state, DistrictReference district)
    {
        var player = state.ReadCurrentPlayerForce() ?? throw Missing("current.PlayerForce32EFA0");
        bool valid = Get(state.ReadForceNativeValidity(player), "current.PlayerForce_validity");
        if (!valid) return false;
        int at = district.PhysicalSlot ?? throw Missing("current.District_pointer");
        byte force = Get(state.ReadDistrictForceId(at), "current.District_force10");
        int playerPhysical = Get(state.ReadForcePhysicalSlot(player), "current.PlayerForce_physical60");
        if (force != playerPhysical) return false;
        ushort leaderId = Get(state.ReadDistrictLeader(at), "current.District_leader12");
        var leader = state.ReadPersonLookupReference(leaderId <= 6000 ? leaderId : 0) ?? throw Missing("current.District_leader_lookup");
        if (!leader.PhysicalSlot.HasValue) return false;
        valid = Get(state.ReadPersonNativeValidity(leader.PhysicalSlot.Value), "current.District_leader_validity");
        if (!valid) return false;
        byte role = Get(state.ReadPersonStatus(leader.PhysicalSlot.Value), "current.District_leader_statusE6");
        return role == 1;
    }
    private static bool AppendBase88(SourceWorldContext state, BaseReference target, int commander)
    {
        var descriptor = state.ReadArmyReturnBasePerson88Descriptor(target) ?? throw Missing("current.Base_person88_descriptor");
        if (!descriptor.Identity.HasValue) throw Missing("branch.Base_person88_null_descriptor_allocation");
        var node = state.ReadRegistryHead(descriptor) ?? throw Missing("current.Base_person88_head");
        var visited = new HashSet<int>();
        while (node.Identity is int identity)
        {
            if (!visited.Add(identity)) throw new InvalidDataException("Base+88 chain contains a cycle.");
            var person = state.ReadRegistryNodePayload(node) ?? throw Missing("current.Base_person88_payload");
            if (person.Kind != RegistryKind.Person) throw Missing("current.Base_person88_payload_kind");
            if (person.PhysicalSlot == commander) return false;
            node = state.ReadRegistryNodeNext(node) ?? throw Missing("current.Base_person88_next");
        }
        var appended = state.AllocateRegistryNode(descriptor) ?? throw Missing("current.Base_person88_node");
        if (!appended.Identity.HasValue) throw Missing("current.Base_person88_node_allocation_failure");
        state.WriteRegistryNodePayload(appended, new(RegistryKind.Person, commander));
        return true;
    }
    internal static void ApplyGovernorOwned(SourceWorldContext state, ArmyReturnGovernorPacket update)
        => ArmyReturnGovernorUpdater.ApplyCurrentWrites(state, update);

    internal static IReadOnlyList<ArmyReturnGovernorPacket> ApplyPrimaryOwned(DistrictReference district,
        SourceWorldContext state, IArmyReturnGovernorCurrentQueries queries)
    {
        var applied = ArmyReturnPrimaryDistrictUpdater.ApplyCurrent(district, state, queries);
        return applied.Packet?.GovernorUpdates
            ?? throw new InvalidDataException(applied.UnresolvedOwner ?? "current.PrimaryDistrict_unknown");
    }
    private static bool SpecialId(ushort id) => Enumerable.Range(0, 5).Any(i => id >= 4111 + 20 * i && id <= 4120 + 20 * i);
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string value) => new(value);
    private sealed class MissingOwner(string value) : Exception(value);
}
