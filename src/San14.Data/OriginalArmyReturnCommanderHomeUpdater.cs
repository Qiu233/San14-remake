using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace San14.Data;

public sealed record OriginalArmyReturnCommanderHomeResolution(OriginalArmyReturnCommanderHomePacket? Packet, string? UnresolvedOwner);
public sealed class OriginalArmyReturnCommanderHomePacket
{
    public OriginalArmyUnitReference Army { get; }
    public OriginalBaseReference Base { get; }
    public int CommanderPhysical { get; }
    public bool Person88Appended { get; }
    public IReadOnlyList<OriginalArmyReturnGovernorPacket> GovernorUpdates { get; }
    internal OriginalEventInitializationContext Owner { get; }
    internal OriginalCurrentListPool Pool { get; }
    internal OriginalCurrentListPool DraftPool { get; }
    internal IOriginalArmyReturnGovernorCurrentQueries Queries { get; }
    internal string Fingerprint { get; }
    internal ushort Destination { get; }
    internal OriginalEventInitializationContext DraftOwner { get; }
    public OriginalPersonAssignmentPrecallResult? AssignmentPrecall { get; }
    internal OriginalArmyReturnCommanderHomePacket(OriginalArmyUnitReference army, OriginalBaseReference target, int commander,
        bool appended, OriginalArmyReturnGovernorPacket[] updates, OriginalEventInitializationContext owner,
        OriginalCurrentListPool pool, OriginalCurrentListPool draft, IOriginalArmyReturnGovernorCurrentQueries queries,
        string fingerprint, ushort destination, OriginalEventInitializationContext draftOwner,
        OriginalPersonAssignmentPrecallResult? assignmentPrecall)
    {
        Army = army; Base = target; CommanderPhysical = commander; Person88Appended = appended;
        GovernorUpdates = Array.AsReadOnly(updates); Owner = owner; Pool = pool; DraftPool = draft; Queries = queries;
        Fingerprint = fingerprint; Destination = destination;
        DraftOwner = draftOwner; AssignmentPrecall = assignmentPrecall;
    }
}

/// <summary>29F220's successful one-Commander temporary-list call to271320:
/// ordinary role1..4, same Force/District with same or changed City. Executes actual
/// owned Base+88 admission, Person locations, governor/District calls and
/// shared temporary-node reuse on a detached Context/pool before publication.
/// This is not the preceding trait cleanup or the remaining whole29F220.</summary>
public static class OriginalArmyReturnCommanderHomeUpdater
{
    public static OriginalArmyReturnCommanderHomeResolution Prepare(OriginalArmyUnitReference army,
        OriginalBaseReference target, OriginalEventInitializationContext context, OriginalCurrentListPool pool,
        IOriginalArmyReturnGovernorCurrentQueries queries) => PrepareCore(army, target, context, pool, queries, true);

    /// <summary>The enclosing29F220 already owns its private draft. Execute
    /// this source step once there, without another prepare/fingerprint pass.</summary>
    internal static OriginalArmyReturnCommanderHomeResolution ApplyCurrent(OriginalArmyUnitReference army,
        OriginalBaseReference target, OriginalEventInitializationContext context, OriginalCurrentListPool pool,
        IOriginalArmyReturnGovernorCurrentQueries queries, bool transportEnter = false) =>
        PrepareCore(army, target, context, pool, queries, false, transportEnter);

    private static OriginalArmyReturnCommanderHomeResolution PrepareCore(OriginalArmyUnitReference army,
        OriginalBaseReference target, OriginalEventInitializationContext context, OriginalCurrentListPool pool,
        IOriginalArmyReturnGovernorCurrentQueries queries, bool detachedPreparation, bool transportEnter = false)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(pool); ArgumentNullException.ThrowIfNull(queries);
        var reads = new List<object>();
        try
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, context) || !pool.OwnsArmyReturnProfile())
                throw Missing("current.complete_owned_shared_pool");
            if (context.RegistryLockEnabledReader is not null || context.RegistryDescriptorKeyReader is not null ||
                context.RegistryHeadTablePresenceReader is not null || context.RegistryLockEnabled != false ||
                context.RegistryHeadTablePresent != true)
                throw Missing("current.owned_sequential_list_profile");
            if (detachedPreparation) reads.Add(new { pool = pool.ArmyReturnFingerprint() });
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            byte armyStatus = Get(context.ReadArmyReturnUnitStatus10(army), "current.Army_status10");
            reads.Add(new { armyStatus });
            if (armyStatus != 1 && !(transportEnter && armyStatus == 2))
                throw Missing("branch.not_ordinary_army_return_caller");
            ushort commanderId = Get(context.ReadArmyUnitCommanderId(army), "current.Army_commander12");
            int commander = (context.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)
                ?? throw Missing("current.Commander_lookup")).PhysicalSlot ?? throw Missing("current.Commander_pointer");
            if (!Get(context.ReadPersonNativeValidity(commander), "current.Commander_validity")) throw Missing("branch.invalid_commander");
            byte status = Get(context.ReadPersonStatus(commander), "current.Commander_statusE6");
            ushort flags = Get(context.ReadArmyReturnPersonFlags196(commander), "current.Commander_flags15E");
            ushort first = Get(context.ReadPersonFirstLocation(commander), "current.Commander_firstE2");
            ushort second = Get(context.ReadPersonSecondLocation(commander), "current.Commander_secondE4");
            ushort identity = Get(context.ReadPersonIdentity(commander), "current.Commander_id10");
            reads.Add(new { army, commanderId, commander, identity, status, flags, first, second });
            if (status is < 1 or > 4 || (flags & 0x4000) != 0 || SpecialId(identity)) throw Missing("branch.nonordinary_commander");
            if (!Get(context.ReadBaseNativeValidity(target), "current.target_Base_validity"))
                throw Missing("branch.nonordinary_target_base");
            ushort location = Get(context.ReadBaseObjectLink(target), "current.target_Base_word4E");
            if (location is < 1 or > 61 || first is < 1 or > 61) throw Missing("branch.commander_invalid_original_base");
            var actualTarget = OriginalBaseLookup.Find(location, context);
            var oldBase = OriginalBaseLookup.Find(first, context);
            reads.Add(new { location, actualTarget, oldBase });
            if (actualTarget != target || !Get(context.ReadBaseNativeValidity(oldBase), "current.old_Base_validity"))
                throw Missing("branch.commander_invalid_original_base_pointer");
            byte personDistrict = Get(context.ReadPersonDistrictSlot(commander), "current.Commander_districtE0");
            int baseDistrict = Get(context.ReadBaseDistrictSlot(target), "current.target_Base_virtual78");
            reads.Add(new { personDistrict, baseDistrict });
            if (personDistrict != baseDistrict) throw Missing("branch.commander_different_district");
            var district = context.ReadBaseDistrictReference(target) ?? throw Missing("current.target_Base_virtual80");
            //27153B/54D still perform both real map queries before their equality
            //test, even though the already proved saved Base pointer is equal.
            var oldCity = OriginalCurrentGateMapQueries.FindCity(oldBase, context);
            var newCity = OriginalCurrentGateMapQueries.FindCity(target, context);
            reads.Add(new { oldCity, newCity });
            bool changedCity = oldCity != newCity;
            uint worldFlags = Get(context.ReadWorldTransferServiceFlags(), "current.World16A8");
            reads.Add(new { worldFlags });
            bool admit88 = (worldFlags & 0x100) == 0 && PlayerRulerDistrict(context, district, reads);
            var draft = detachedPreparation ? context.CreateArmyReturnHomeDraft() : context;
            var draftPool = detachedPreparation ? pool.DetachForArmyReturn(draft) : pool;
            var scoped = new PoolQueries(queries, draftPool);
            //The caller's exact successful one-person list;24E4C0 sees a null
            //head.next and therefore performs no comparisons/reordering.
            var caller = NewTemporary(draft);
            AppendPerson(draft, caller, commander);
            var affected = NewTemporary(draft); //2713F2.
            bool appended = false;
            if (admit88) appended = AppendBase88(draft, target, commander);
            OriginalPersonAssignmentPrecallResult? assignmentPrecall = null;
            if (changedCity)
            {
                assignmentPrecall = OriginalPersonAssignmentPrecallSource.Apply29C430ForHomeTransfer(draft, commander, district, target);
                if (assignmentPrecall.NativeReturn is null)
                    throw Missing(assignmentPrecall.UnresolvedOwner ?? "current.29C430_changed_City");
                reads.Add(assignmentPrecall);
            }
            AppendBase(draft, affected, oldBase); //27172C, distinct pointer once.
            draft.WritePersonFirstLocation(commander, location); //271767.
            draft.WritePersonSecondLocation(commander, location); //2717B2,role1..4.
            var updates = new List<OriginalArmyReturnGovernorPacket>();
            ApplyGovernor(target); ApplyPrimary(district); //2717DD/271801.
            ApplyGovernor(oldBase);
            ApplyPrimary(draft.ReadBaseDistrictReference(oldBase) ?? throw Missing("current.old_Base_District")); //27185A/27187E.
            ClearRelease(draft, affected); //2718BE/2718CD.
            ClearRelease(draft, caller); //29F44F/29F45E after271320 returns.
            if (detachedPreparation) reads.Add(new { appended, AfterPool = draftPool.ArmyReturnFingerprint() });
            string fingerprint = detachedPreparation ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reads)))) : "";
            return new(new(army, target, commander, appended, updates.ToArray(), context, pool, draftPool, queries, fingerprint, location, draft, assignmentPrecall), null);

            void ApplyGovernor(OriginalBaseReference at)
            {
                var result = OriginalArmyReturnGovernorUpdater.PrepareCurrent(at, draft, scoped);
                var update = result.Packet ?? throw Missing(result.UnresolvedOwner ?? "current.Governor_unknown");
                reads.Add(new { Governor = update.Fingerprint });
                ReplayGovernorTemporary(draft, update);
                WriteGovernor(draft, update); updates.Add(update);
            }
            void ApplyPrimary(OriginalDistrictReference at)
            {
                var result = OriginalArmyReturnPrimaryDistrictUpdater.PrepareCurrent(at, draft, scoped);
                var packet = result.Packet ?? throw Missing(result.UnresolvedOwner ?? "current.PrimaryDistrict_unknown");
                reads.Add(new { Primary = packet.Fingerprint });
                int physical = at.PhysicalSlot ?? throw Missing("current.PrimaryDistrict_pointer");
                //Repeat the proved normal loop with physical owned temporaries,
                //rather than treating the preparation's capacity checks as pool writes.
                for (int slot = 0; slot < 52; slot++)
                {
                    var city = draft.ReadCityTableReference(slot) ?? throw Missing("current.City_table");
                    if (Get(draft.ReadBaseDistrictSlot(city), "current.City_virtual78") != physical) continue;
                    if (Get(draft.ReadBaseNativeValidity(city), "current.City_validity")) ApplyGovernor(city);
                    var gates = NewTemporary(draft);
                    var associated = new List<OriginalBaseReference>();
                    foreach (var gate in draft.ReadArmyReturnRegisteredGates() ?? throw Missing("current.Gate_managerA0"))
                    {
                        if (gate is null) throw Missing("current.Gate_payload");
                        if (OriginalCurrentGateMapQueries.FindCity(gate, draft) != city) continue;
                        AppendBase(draft, gates, gate); associated.Add(gate);
                    }
                    foreach (var gate in associated)
                        if (Get(draft.ReadBaseNativeValidity(gate), "current.Gate_validity")) ApplyGovernor(gate);
                    ClearRelease(draft, gates);
                }
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.CommanderHome_input: " + error.Message); }
    }

    public static bool ApplyPrepared(OriginalArmyReturnCommanderHomePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var current = Prepare(packet.Army, packet.Base, packet.Owner, packet.Pool, packet.Queries).Packet;
        if (current is null || current.Fingerprint != packet.Fingerprint) return false;
        //Every source effect ran in order on the detached world. Publishing
        //its final pool plus the explicit fields invokes no external handler.
        packet.Pool.CommitArmyReturnPool(current.DraftPool);
        packet.Owner.CommitAssignmentCurrentFields(current.DraftOwner);
        packet.Owner.WritePersonFirstLocation(current.CommanderPhysical, current.Destination);
        packet.Owner.WritePersonSecondLocation(current.CommanderPhysical, current.Destination);
        foreach (var update in current.GovernorUpdates) WriteGovernor(packet.Owner, update);
        return true;
    }
    private static bool PlayerRulerDistrict(OriginalEventInitializationContext state, OriginalDistrictReference district, List<object> reads)
    {
        var player = state.ReadCurrentPlayerForce() ?? throw Missing("current.PlayerForce32EFA0");
        bool valid = Get(state.ReadForceNativeValidity(player), "current.PlayerForce_validity");
        reads.Add(new { player, valid });
        if (!valid) return false;
        int at = district.PhysicalSlot ?? throw Missing("current.District_pointer");
        byte force = Get(state.ReadDistrictRawByte(at, 0x10), "current.District_force10");
        int playerPhysical = Get(state.ReadForcePhysicalSlot(player), "current.PlayerForce_physical60");
        reads.Add(new { at, force, playerPhysical }); if (force != playerPhysical) return false;
        ushort leaderId = Get(state.ReadDistrictLeader(at), "current.District_leader12");
        var leader = state.ReadPersonLookupReference(leaderId <= 6000 ? leaderId : 0) ?? throw Missing("current.District_leader_lookup");
        reads.Add(new { leaderId, leader });
        if (!leader.PhysicalSlot.HasValue) return false;
        valid = Get(state.ReadPersonNativeValidity(leader.PhysicalSlot.Value), "current.District_leader_validity");
        reads.Add(new { leader, valid }); if (!valid) return false;
        byte role = Get(state.ReadPersonStatus(leader.PhysicalSlot.Value), "current.District_leader_statusE6");
        reads.Add(new { role }); return role == 1;
    }
    private static bool AppendBase88(OriginalEventInitializationContext state, OriginalBaseReference target, int commander)
    {
        var descriptor = state.ReadArmyReturnBasePerson88Descriptor(target) ?? throw Missing("current.Base_person88_descriptor");
        if (!descriptor.Identity.HasValue) throw Missing("branch.Base_person88_null_descriptor_allocation");
        var node = state.ReadRegistryHead(descriptor) ?? throw Missing("current.Base_person88_head");
        var visited = new HashSet<int>();
        while (node.Identity is int identity)
        {
            if (!visited.Add(identity)) throw new InvalidDataException("Base+88 chain contains a cycle.");
            var person = state.ReadRegistryNodePayload(node) ?? throw Missing("current.Base_person88_payload");
            if (person.Kind != OriginalRegistryKind.Person) throw Missing("current.Base_person88_payload_kind");
            if (person.PhysicalSlot == commander) return false;
            node = state.ReadRegistryNodeNext(node) ?? throw Missing("current.Base_person88_next");
        }
        AppendPerson(state, new(descriptor.Identity), commander); return true;
    }
    private static OriginalHexListHandle NewTemporary(OriginalEventInitializationContext state)
    {
        var handle = state.CreateTemporaryHexList() ?? throw Missing("current.temporary_descriptor");
        if (!handle.Identity.HasValue) throw Missing("branch.temporary_descriptor_allocation_failure");
        return handle;
    }
    private static void AppendPerson(OriginalEventInitializationContext state, OriginalHexListHandle handle, int person)
    {
        var node = state.AllocateRegistryNode(new(handle.Identity)) ?? throw Missing("current.Person_list_node");
        if (!node.Identity.HasValue) throw Missing("branch.Person_list_node_allocation_failure");
        state.WriteRegistryNodePayload(node, new(OriginalRegistryKind.Person, person));
    }
    private static void AppendBase(OriginalEventInitializationContext state, OriginalHexListHandle handle, OriginalBaseReference target)
    {
        var node = state.AllocateBaseRegistryNode(new(handle.Identity)) ?? throw Missing("current.Base_list_node");
        if (!node.Identity.HasValue) throw Missing("branch.Base_list_node_allocation_failure");
        state.WriteBaseRegistryNodePayload(node, target);
    }
    private static void ReplayGovernorTemporary(OriginalEventInitializationContext state, OriginalArmyReturnGovernorPacket packet)
    {
        var handle = NewTemporary(state);
        var nodes = new Dictionary<int, Queue<OriginalHexListNodeReference>>();
        foreach (int person in packet.CandidateOrderBeforeSort)
        {
            var node = state.AllocateRegistryNode(new(handle.Identity)) ?? throw Missing("current.Governor_list_node");
            if (!node.Identity.HasValue) throw Missing("branch.Governor_list_node_allocation_failure");
            state.WriteRegistryNodePayload(node, new(OriginalRegistryKind.Person, person));
            if (!nodes.TryGetValue(person, out var queue)) nodes[person] = queue = new();
            queue.Enqueue(new(node.Identity));
        }
        var sorted = packet.CandidateOrderAfterSort.Select(person => nodes[person].Dequeue()).ToArray();
        if (sorted.Length > 0)
        {
            state.SetHexListHead(handle, sorted[0]); state.SetHexListTail(handle, sorted[^1]);
            for (int i = 0; i < sorted.Length; i++)
            {
                state.SetHexListNodePrevious(sorted[i], i == 0 ? new(null) : sorted[i - 1]);
                state.SetHexListNodeNext(sorted[i], i + 1 == sorted.Length ? new(null) : sorted[i + 1]);
            }
        }
        ClearRelease(state, handle);
    }
    private static void ClearRelease(OriginalEventInitializationContext state, OriginalHexListHandle handle)
    { state.ClearTemporaryHexList(handle); state.ReleaseTemporaryHexList(handle); }
    private static void WriteGovernor(OriginalEventInitializationContext state, OriginalArmyReturnGovernorPacket update)
    {
        foreach (var write in update.PersonWrites.Where(x => x.SourceStep.StartsWith("26D0DE", StringComparison.Ordinal)))
            state.WritePersonStatus(write.PersonPhysical, write.StatusAfter);
        state.SetArmyReturnBaseGovernor2E(update.Base, update.GovernorWord2EAfter);
        foreach (var write in update.PersonWrites.Where(x => x.SourceStep.StartsWith("26D11C", StringComparison.Ordinal)))
            state.WritePersonStatus(write.PersonPhysical, write.StatusAfter);
    }
    //Shared by source operations on their private current world. The numeric
    //Primary updater alone has no authority to reconstruct temporary-node reuse.
    internal static void ApplyGovernorOwned(OriginalEventInitializationContext state, OriginalArmyReturnGovernorPacket update)
    {
        try { ReplayGovernorTemporary(state, update); WriteGovernor(state, update); }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message); }
    }
    internal static IReadOnlyList<OriginalArmyReturnGovernorPacket> ApplyPrimaryOwned(OriginalDistrictReference district,
        OriginalEventInitializationContext state, OriginalCurrentListPool pool, IOriginalArmyReturnGovernorCurrentQueries queries)
    {
        try
        {
            var scoped = new PoolQueries(queries, pool);
            var prepared = OriginalArmyReturnPrimaryDistrictUpdater.PrepareCurrent(district, state, scoped);
            if (prepared.Packet is null) throw Missing(prepared.UnresolvedOwner ?? "current.PrimaryDistrict_unknown");
            int physical = district.PhysicalSlot ?? throw Missing("current.PrimaryDistrict_pointer");
            var applied = new List<OriginalArmyReturnGovernorPacket>();
            for (int slot = 0; slot < 52; slot++)
            {
                var city = state.ReadCityTableReference(slot) ?? throw Missing("current.City_table");
                if (Get(state.ReadBaseDistrictSlot(city), "current.City_virtual78") != physical) continue;
                if (Get(state.ReadBaseNativeValidity(city), "current.City_validity")) Governor(city);
                var gates = NewTemporary(state);
                var associated = new List<OriginalBaseReference>();
                foreach (var gate in state.ReadArmyReturnRegisteredGates() ?? throw Missing("current.Gate_managerA0"))
                {
                    if (gate is null) throw Missing("current.Gate_payload");
                    if (OriginalCurrentGateMapQueries.FindCity(gate, state) != city) continue;
                    AppendBase(state, gates, gate); associated.Add(gate);
                }
                foreach (var gate in associated)
                    if (Get(state.ReadBaseNativeValidity(gate), "current.Gate_validity")) Governor(gate);
                ClearRelease(state, gates);
            }
            return applied.AsReadOnly();
            void Governor(OriginalBaseReference target)
            {
                var result = OriginalArmyReturnGovernorUpdater.PrepareCurrent(target, state, scoped);
                var packet = result.Packet ?? throw Missing(result.UnresolvedOwner ?? "current.Governor_unknown");
                ApplyGovernorOwned(state, packet); applied.Add(packet);
            }
        }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message); }
    }
    private sealed class PoolQueries(IOriginalArmyReturnGovernorCurrentQueries owner, OriginalCurrentListPool pool)
        : IOriginalArmyReturnGovernorCurrentQueries
    {
        public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int person) => owner.ReadCurrentComparatorFields(person);
        public int? ReadCurrentAssociatedVirtual98(OriginalPersonLinkedEntityReference reference) => owner.ReadCurrentAssociatedVirtual98(reference);
        public OriginalArmyReturnGovernorPoolCapacity? ReadCurrentTemporaryPoolCapacity() => pool.ArmyReturnCapacity;
    }
    private static bool SpecialId(ushort id) => Enumerable.Range(0, 5).Any(i => id >= 4111 + 20 * i && id <= 4120 + 20 * i);
    private static T Get<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string value) => new(value);
    private sealed class MissingOwner(string value) : Exception(value);
}
