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

internal sealed record PersonHomeTransferPacket(int PersonPhysical, BaseReference Target,
    bool Base88Appended, IReadOnlyList<ArmyReturnGovernorPacket> GovernorUpdates,
    PersonAssignmentPrecallResult? AssignmentPrecall);
internal sealed record PersonHomeTransferResolution(PersonHomeTransferPacket? Packet, string? UnresolvedOwner);

/// <summary>29F220's successful one-Commander temporary-list call to271320:
/// ordered Person relocation, including ordinary cross-District transfer. Executes actual
/// owned Base+88 admission, Person locations, governor/District calls and
/// ordered governor updates on the caller owned operation context before publication.
/// This is not the preceding trait cleanup or the remaining whole29F220.</summary>
public static class ArmyReturnCommanderHomeUpdater
{
    internal static ArmyReturnCommanderHomeResolution ApplyCurrent(ArmyUnitReference army,
        BaseReference target, SourceWorldContext context, WorldIndexContext pool,
        IArmyReturnGovernorCurrentQueries queries, bool transportEnter = false,
        Action<PersonAssignmentPrecallResult>? receiveAssignment = null)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(pool); ArgumentNullException.ThrowIfNull(queries);
        HexListHandle? caller = null;
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
            //29F220 owns the caller Person list;271320 allocates its affected
            //Base list only after that list is nonempty. A singleton's24E440
            //sort has no comparator or node movement.
            caller = context.CreateTemporaryHexList() ?? throw Missing("current.CommanderHome_caller_descriptor");
            if (!caller.Identity.HasValue) throw Missing("current.CommanderHome_caller_allocation");
            var node = context.AllocateRegistryNode(new(caller.Identity))
                ?? throw Missing("current.CommanderHome_caller_node");
            if (!node.Identity.HasValue)
                return new(new(army, target, commander, false, [], null), null);
            context.WriteRegistryNodePayload(node, new(RegistryKind.Person, commander));
            var moved = ApplyPersonCurrent(commander, target, context, pool, queries, receiveAssignment);
            var packet = moved.Packet ?? throw Missing(moved.UnresolvedOwner ?? "current.CommanderHome_271320");
            return new(new(army, target, commander, packet.Base88Appended,
                packet.GovernorUpdates.ToArray(), packet.AssignmentPrecall), null);
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.CommanderHome_input: " + error.Message); }
        finally
        {
            if (caller is not null)
            { context.ClearTemporaryHexList(caller); context.ReleaseTemporaryHexList(caller); }
        }
    }

    /// <summary>271320's single ordinary Person body. The enclosing caller
    /// owns its actual one-Person list; this entry owns only the nested affected
    /// Base list and writes the same operation-local current, without publication.
    /// Ruler transfers reuse the caller's complete District operation owner.</summary>
    internal static PersonHomeTransferResolution ApplyPersonCurrent(int person, BaseReference target,
        SourceWorldContext current, WorldIndexContext pool, IArmyReturnGovernorCurrentQueries queries,
        Action<PersonAssignmentPrecallResult>? receiveAssignment = null)
    {
        HexListHandle? affected = null;
        try
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("current.complete_owned_shared_pool");
            if (current.RegistryLockEnabledReader is not null || current.RegistryDescriptorKeyReader is not null ||
                current.RegistryHeadTablePresenceReader is not null || current.RegistryLockEnabled != false ||
                current.RegistryHeadTablePresent != true)
                throw Missing("current.owned_sequential_list_profile");
            //The caller resolved destination through32C7D0. This finite Base
            //entry retains that actual object's location identity.
            ushort destination = Get(current.ReadBaseObjectLink(target), "current.PersonHome_target_Base4E");
            if (destination is < 1 or > 61 || BaseLookup.Find(destination, current) != target)
                throw Missing("branch.PersonHome_target_location_profile");
            affected = current.CreateTemporaryHexList() ?? throw Missing("current.PersonHome_affected_descriptor");
            if (!affected.Identity.HasValue) throw Missing("branch.PersonHome_affected_descriptor_allocation");
            ushort identity = Get(current.ReadPersonIdentity(person), "current.PersonHome_Person10");
            var updates = new List<ArmyReturnGovernorPacket>();
            bool appended = false;
            PersonAssignmentPrecallResult? assignment = null;
            BaseReference? affectedBase = null;
            if (PersonReturnArrivalSource.ShouldApplyPersonArrivalCurrent(current, person, target, identity))
            {
                ushort first = Get(current.ReadPersonFirstLocation(person), "current.PersonHome_oldE2");
                var oldBase = BaseLookup.Find(first, current);
                uint flags = Get(current.ReadWorldTransferServiceFlags(), "current.PersonHome_World16A8");
                if ((flags & 0x100) == 0)
                {
                    byte rawDistrict = Get(current.ReadPersonDistrictSlot(person), "current.PersonHome_oldE0");
                    var oldDistrict = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
                        ?? throw Missing("current.PersonHome_old_District");
                    if (PlayerRulerDistrict(current, oldDistrict) || PlayerRulerDistrict(current,
                        current.ReadBaseDistrictReference(target) ?? throw Missing("current.PersonHome_target_District")))
                        appended = AppendBase88(current, target, person);
                }
                if (Get(current.ReadBaseNativeValidity(oldBase), "current.PersonHome_old_Base_validity"))
                {
                    var oldCity = CurrentGateMapQueries.FindCity(oldBase, current);
                    var newCity = CurrentGateMapQueries.FindCity(target, current);
                    if (oldCity != newCity)
                    {
                        var district = current.ReadBaseDistrictReference(target) ?? throw Missing("current.PersonHome_cleanup_District");
                        var result = PersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(current, person, district, target);
                        if (result.NativeReturn is null) throw Missing(result.UnresolvedOwner ?? "current.PersonHome_29C430");
                        ReceiveAssignment(result);
                    }
                }
                //240DB0 invokes its association cleanup callback before E0 is
                //written. Both that callback and29C430 precede the E2/E4 tail.
                byte personDistrict = Get(current.ReadPersonDistrictSlot(person), "current.PersonHome_transfer_E0");
                int targetDistrict = Get(current.ReadBaseDistrictSlot(target), "current.PersonHome_transfer_Base78");
                if (personDistrict != targetDistrict &&
                    Get(current.ReadPersonStatus(person), "current.PersonHome_transfer_E6") == 1)
                {
                    var operation = queries as ArmyReturnDistrictOperationQueries
                        ?? throw Missing("current.PersonHome_26D2E0_operation_owner");
                    //26D2E0 can clean assignments for residents other than the
                    //returning commander. Keep their actual physical identity
                    //and publish through the same caller-owned transaction.
                    PersonReturnDistrictTransferSource.ApplyRulerCurrent(current, person, target, queries,
                        operation.Assignment, updates, operation.ChangedAreas, operation.ChangedGates, static _ => { });
                }
                else ApplyPersonHomeDistrictCurrent(person, target, current, ReceiveAssignment);
                if (Get(current.ReadBaseNativeValidity(oldBase), "current.PersonHome_old_Base_validity_repeated"))
                {
                    AppendAffectedBase(current, affected, oldBase);
                    affectedBase = oldBase;
                }
                current.WritePersonFirstLocation(person, (ushort)Math.Clamp((int)destination, 0, 61));
                PersonReturnArrivalSource.ApplyArrivalSecondLocationCurrent(current, person, target, destination, static _ => { });
            }
            //271320 updates the target even when its member was skipped, then
            //visits only the old Bases actually admitted to its affected list.
            UpdateRoles(target);
            if (affectedBase is not null) UpdateRoles(affectedBase);
            return new(new(person, target, appended, updates.AsReadOnly(), assignment), null);

            void ReceiveAssignment(PersonAssignmentPrecallResult result)
            {
                assignment = result;
                receiveAssignment?.Invoke(result);
            }
            void UpdateRoles(BaseReference at)
            {
                if (Get(current.ReadBaseNativeValidity(at), "current.PersonHome_role_Base_validity"))
                {
                    var update = ArmyReturnGovernorUpdater.PrepareCurrent(at, current, queries);
                    var packet = update.Packet ?? throw Missing(update.UnresolvedOwner ?? "current.PersonHome_Governor");
                    ApplyGovernorOwned(current, packet); updates.Add(packet);
                }
                updates.AddRange(ApplyPrimaryOwned(current.ReadBaseDistrictReference(at)
                    ?? throw Missing("current.PersonHome_role_District"), current, queries));
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.PersonHome_input: " + error.Message); }
        finally
        {
            if (affected is not null)
            { current.ClearTemporaryHexList(affected); current.ReleaseTemporaryHexList(affected); }
        }
    }

    internal static bool ApplyPersonHomeDistrictCurrent(int person, BaseReference target,
        SourceWorldContext current, Action<PersonAssignmentPrecallResult>? receiveAssignment = null)
    {
        byte personDistrict = current.ReadPersonDistrictSlot(person)
            ?? throw new InvalidDataException("current.PersonHome_PersonE0");
        int targetDistrict = current.ReadBaseDistrictSlot(target)
            ?? throw new InvalidDataException("current.PersonHome_target_virtual78");
        if (personDistrict == targetDistrict) return false;
        byte status = current.ReadPersonStatus(person)
            ?? throw new InvalidDataException("current.PersonHome_transfer_statusE6");
        if (status == 1) throw new InvalidDataException("branch.PersonHome_26D2E0_current_operation_owner");
        if (status is not (5 or 6))
        {
            var associations = new List<PersonAssignmentPrecallResult>();
            PersonDistrictTransfer.Apply(person, targetDistrict,
                ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, associations, receiveAssignment));
        }
        //World165D/165E/1668 belongs to the original deployment page's temporary
        //formation/member selection. No campaign selection is fabricated here.
        return true;
    }

    /// <summary>Mirror only this successful Person relocation and the ordered
    /// governor packets' actual stores. The live pool is neither exported nor
    /// imported; assignment cleanup is published by its existing owner.</summary>
    internal static GameState PublishPersonCurrent(GameState state, SourceWorldContext current,
        PersonHomeTransferPacket packet)
    {
        if (state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Int32Values.GetValueOrDefault(OwnedArmyLifecycleDetachedInputs.VersionKey) != 1)
            throw new InvalidDataException("current.PersonHome_saved_detached_owner");
        var people = new Dictionary<int, OfficerState>(state.Officers);
        var cities = new Dictionary<int, CityState>(state.Cities);
        var gates = new Dictionary<int, GateState>(state.Gates);
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        ushort id = Identity(packet.PersonPhysical);
        var person = people[id];
        ushort first = current.ReadPersonFirstLocation(packet.PersonPhysical)
            ?? throw new InvalidDataException("current.PersonHome_published_E2");
        byte district = current.ReadPersonDistrictSlot(packet.PersonPhysical)
            ?? throw new InvalidDataException("current.PersonHome_published_E0");
        var location = current.ReadOfficerLocation(id)
            ?? throw new InvalidDataException("current.PersonHome_published_location");
        int? forceId = person.ForceId;
        if (person.DistrictId != (district == 0 ? null : (int?)district))
        {
            var force = current.ReadPersonForce(packet.PersonPhysical)
                ?? throw new InvalidDataException("current.PersonHome_published_Force");
            int physical = current.ReadForcePhysicalSlot(force)
                ?? throw new InvalidDataException("current.PersonHome_published_Force_identity");
            forceId = physical == 0 ? null : physical;
        }
        people[id] = person with { LocationId = first == 0 ? null : first,
            DistrictId = district == 0 ? null : district, ForceId = forceId, LocationState = location };
        values[OwnedArmyLifecycleDetachedInputs.Key("Person", packet.PersonPhysical, "E2")] = first;
        values[OwnedArmyLifecycleDetachedInputs.Key("Person", packet.PersonPhysical, "E0")] = district;
        var residue = current.ReadOfficerLocationResidue(id);
        var resources = CampaignSourceKnowledge.From(state.ResourceState).With(new(id,
            location.Position?.Kind == OfficerPositionKind.Unresolved ? residue?.LocationCode : null,
            location.Membership?.Kind == OfficerDistrictMembershipKind.Unresolved ? residue?.DistrictCode : null));
        foreach (var update in packet.GovernorUpdates)
        {
            foreach (var write in update.PersonWrites)
            {
                ushort changedId = Identity(write.PersonPhysical);
                var changed = people[changedId];
                var status = OfficerServiceStatusSourceAdapter.Decode(write.StatusAfter);
                people[changedId] = changed with { ServiceStatus = status,
                    Role = OfficerServiceRules.PublishedRole(status, changed.Role) };
            }
            if (update.Base.PhysicalSlot is not int physicalBase)
                throw new InvalidDataException("current.PersonHome_Governor_Base_identity");
            ushort governor = update.GovernorWord2EAfter;
            if (update.Base.Kind == BaseKind.City && cities.TryGetValue(physicalBase, out var city) &&
                city.NativeReturnRuntime is { } runtime)
                cities[physicalBase] = city with { GovernorId = governor == 0 ? null : governor,
                    NativeReturnRuntime = runtime with { Governor2E = governor } };
            else if (update.Base.Kind == BaseKind.Gate && gates.TryGetValue(physicalBase + 51, out var gate))
            {
                gates[gate.Id] = gate with { GovernorId = governor == 0 ? null : governor };
                values[OwnedArmyLifecycleDetachedInputs.Key("Gate", physicalBase, "2E")] = governor;
            }
            else throw new InvalidDataException("current.PersonHome_Governor_saved_Base");
        }
        return state with { Officers = people, Cities = cities, Gates = gates, ResourceState = resources,
            NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };

        ushort Identity(int physical)
        {
            ushort identity = current.ReadPersonIdentity(physical)
                ?? throw new InvalidDataException("current.PersonHome_publication_Person_identity");
            if (current.ResolveOfficerSource(identity) != physical || !people.ContainsKey(identity))
                throw new InvalidDataException("current.PersonHome_publication_saved_Person");
            return identity;
        }
    }

    /// <summary>The common location/governor mutation tail. The two historical
    /// wrappers retain their pre-read Districts and complete prefix read order.
    /// The direct source entry instead performs fresh getters at their source
    /// points, including status after the E2 write. No pool export or rebinding.</summary>
    internal static IReadOnlyList<ArmyReturnGovernorPacket> ApplyPreparedPersonLocationsCurrent(int person,
        BaseReference target, BaseReference oldBase, ushort destination, SourceWorldContext current,
        IArmyReturnGovernorCurrentQueries queries, DistrictReference? targetDistrict = null,
        DistrictReference? oldDistrict = null, bool readCurrentStatus = false, bool readTargetValidity = false,
        Action<string>? sourceEffect = null, Func<string, Exception>? unresolved = null,
        string governorMissing = "current.Governor_unknown")
    {
        current.WritePersonFirstLocation(person, destination); sourceEffect?.Invoke("271767.PersonE2");
        if (readCurrentStatus)
        {
            byte status = Get(current.ReadPersonStatus(person), "current.PersonHome_status_after_E2");
            if (status is < 1 or > 4) throw Missing("branch.PersonHome_nonordinary_status");
        }
        current.WritePersonSecondLocation(person, destination); sourceEffect?.Invoke("2717B2.PersonE4");
        var updates = new List<ArmyReturnGovernorPacket>();
        if (!readTargetValidity || Get(current.ReadBaseNativeValidity(target), "current.PersonHome_target_validity")) Governor(target);
        Primary(targetDistrict ?? current.ReadBaseDistrictReference(target) ?? throw Missing("current.PersonHome_target_virtual80"));
        if (!readTargetValidity || Get(current.ReadBaseNativeValidity(oldBase), "current.PersonHome_affected_validity")) Governor(oldBase);
        Primary(oldDistrict ?? current.ReadBaseDistrictReference(oldBase) ?? throw Missing("current.old_Base_District"));
        return updates.AsReadOnly();

        void Governor(BaseReference at)
        {
            var result = ArmyReturnGovernorUpdater.PrepareCurrent(at, current, queries);
            var packet = result.Packet ?? throw Failure(result.UnresolvedOwner ?? governorMissing);
            ApplyGovernorOwned(current, packet); updates.Add(packet);
            sourceEffect?.Invoke("282C10→26D090.Base" + at.PhysicalSlot);
        }
        void Primary(DistrictReference at)
        {
            updates.AddRange(ApplyPrimaryOwned(at, current, queries));
            sourceEffect?.Invoke("2E9140.primary_District" + at.PhysicalSlot);
        }
        Exception Failure(string field) => unresolved?.Invoke(field) ?? Missing(field);
    }

    internal static void AppendAffectedBase(SourceWorldContext current, HexListHandle list, BaseReference target,
        Func<string, Exception>? unresolved = null)
    {
        var node = current.AllocateBaseRegistryNode(new(list.Identity))
            ?? throw (unresolved?.Invoke("current.affected_Base_node") ?? Missing("current.affected_Base_node"));
        if (!node.Identity.HasValue)
            throw (unresolved?.Invoke("branch.affected_Base_node_allocation") ?? Missing("branch.affected_Base_node_allocation"));
        current.WriteBaseRegistryNodePayload(node, target);
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
        SourceWorldContext state, IArmyReturnGovernorCurrentQueries queries, bool allowLeaderless = false,
        bool useCaptureForceRetirement = true)
    {
        var applied = ArmyReturnPrimaryDistrictUpdater.ApplyCurrent(district, state, queries,
            allowLeaderless, useCaptureForceRetirement);
        return applied.Packet?.GovernorUpdates
            ?? throw new InvalidDataException(applied.UnresolvedOwner ?? "current.PrimaryDistrict_unknown");
    }
    private static bool SpecialId(ushort id) => Enumerable.Range(0, 5).Any(i => id >= 4111 + 20 * i && id <= 4120 + 20 * i);
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string value) => new(value);
    private sealed class MissingOwner(string value) : Exception(value);
}
