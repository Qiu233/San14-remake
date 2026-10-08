using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using San14.Core;

namespace San14.Data;

public sealed record OriginalArmyOrdinaryReturnResolution(OriginalArmyOrdinaryReturnPacket? Packet, string? UnresolvedOwner);
public sealed class OriginalArmyOrdinaryReturnPacket
{
    public OriginalArmyUnitReference Army { get; }
    public OriginalBaseReference Base { get; }
    public OriginalDistrictReference SavedCommanderDistrict { get; }
    public OriginalDistrictReference SavedTargetDistrict { get; }
    public OriginalArmyPending70AdmissionOutcome PendingOutcome { get; }
    public IReadOnlyList<int> RulerTailPersonOrder { get; }
    public IReadOnlyList<string> SourceEffectOrder { get; }
    public OriginalPersonAssignmentPrecallResult? HomeAssignments { get; }
    public bool HasAssignmentCurrentWrites { get; }
    public IReadOnlyList<OriginalPersonAssignmentPrecallResult> RulerTailAssociationUpdates { get; }
    public OriginalArmyReturnCaptivesReceipt? Captives { get; }
    internal GameState? CaptivesState { get; }
    internal OriginalEventInitializationContext Owner { get; }
    internal OriginalCurrentListPool Pool { get; }
    internal OriginalCurrentListPool DraftPool { get; }
    internal IOriginalArmyReturnGovernorCurrentQueries Queries { get; }
    internal OriginalArmyReturnTraitPresentationOwners Presentation { get; }
    internal int NativeMode { get; }
    internal bool UsesResourceOutputProjection { get; }
    internal bool IsOperationLocal { get; }
    internal string Fingerprint { get; }
    internal OriginalArmyOrdinaryReturnPacket(OriginalArmyUnitReference army, OriginalBaseReference target,
        OriginalDistrictReference savedCommander, OriginalDistrictReference savedTarget, OriginalArmyPending70AdmissionOutcome outcome,
        int[] order, string[] effects, OriginalEventInitializationContext owner, OriginalCurrentListPool pool,
        OriginalCurrentListPool draft, IOriginalArmyReturnGovernorCurrentQueries queries,
        OriginalArmyReturnTraitPresentationOwners presentation, int nativeMode, string fingerprint,
        bool usesResourceOutputProjection = false, OriginalPersonAssignmentPrecallResult? homeAssignments = null,
        bool hasDistrictTransferWrites = false, OriginalPersonAssignmentPrecallResult[]? rulerTailAssociationUpdates = null,
        bool isOperationLocal = false, GameState? captivesState = null, OriginalArmyReturnCaptivesReceipt? captives = null)
    {
        Army = army; Base = target; SavedCommanderDistrict = savedCommander; SavedTargetDistrict = savedTarget; PendingOutcome = outcome;
        RulerTailPersonOrder = Array.AsReadOnly(order); SourceEffectOrder = Array.AsReadOnly(effects);
        Owner = owner; Pool = pool; DraftPool = draft; Queries = queries; Presentation = presentation; NativeMode = nativeMode; Fingerprint = fingerprint;
        UsesResourceOutputProjection = usesResourceOutputProjection;
        IsOperationLocal = isOperationLocal;
        HomeAssignments = homeAssignments;
        HasAssignmentCurrentWrites = homeAssignments is not null || hasDistrictTransferWrites;
        RulerTailAssociationUpdates = Array.AsReadOnly(rulerTailAssociationUpdates ?? []);
        CaptivesState = captivesState; Captives = captives;
    }
}

/// <summary>One complete conditional ordinary29F220 execution on an owned
/// detached world: ordinary same Force/District Base return. Nonempty status6
/// Army associates need the optional finite captive executor; the default
/// contract supports only a truly empty associated list.
/// known-absent native presentation refresh, successful owned allocations.
/// Roles1..4 are supported; role1 service reaches only source-blocked or
/// complete-table count&lt;200 branches. No Main admission or Core deletion is
/// inferred from this sealed source packet.</summary>
public static class OriginalArmyOrdinaryReturnRuntime
{
    public static OriginalArmyOrdinaryReturnResolution PrepareResourceOutputs(OriginalArmyUnitReference army, OriginalBaseReference target,
        int nativeMode, OriginalEventInitializationContext context, OriginalCurrentListPool pool,
        IOriginalArmyReturnGovernorCurrentQueries queries, OriginalArmyReturnTraitPresentationOwners presentation) =>
        Prepare(army,target,nativeMode,context,pool,queries,presentation,true);

    internal static OriginalArmyOrdinaryReturnResolution PrepareCurrentResourceOutputs(OriginalArmyUnitReference army, OriginalBaseReference target,
        int nativeMode, OriginalEventInitializationContext context, OriginalCurrentListPool pool,
        IOriginalArmyReturnGovernorCurrentQueries queries, OriginalArmyReturnTraitPresentationOwners presentation,
        OriginalArmyReturnCaptivesSource? captives = null, byte? transportFlags26 = null) =>
        Prepare(army, target, nativeMode, context, pool, queries, presentation, true, true, captives, transportFlags26);

    public static OriginalArmyOrdinaryReturnResolution Prepare(OriginalArmyUnitReference army, OriginalBaseReference target,
        int nativeMode, OriginalEventInitializationContext context, OriginalCurrentListPool pool,
        IOriginalArmyReturnGovernorCurrentQueries queries, OriginalArmyReturnTraitPresentationOwners presentation,
        bool useResourceOutputProjection = false, bool operationLocal = false, OriginalArmyReturnCaptivesSource? captives = null,
        byte? transportFlags26 = null)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pool); ArgumentNullException.ThrowIfNull(queries); ArgumentNullException.ThrowIfNull(presentation);
        var reads = new List<object>(); var effects = new List<string>(); var rulerOrder = new List<int>();
        bool districtTransferWrites = false;
        var transferAssociations = new List<OriginalPersonAssignmentPrecallResult>();
        OriginalArmyReturnCaptivesResult? captiveResult = null;
        try
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, context) || !pool.OwnsArmyReturnProfile()) throw Missing("current.complete_owned_shared_pool");
            if (context.TransferServiceRefresher is not null || context.TransferServiceEligibilityReader is not null || context.TransferModeStateReader is not null)
                throw Missing("current.owned_return_service_source_fields");
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            byte armyStatus = Get(context.ReadArmyReturnUnitStatus10(army), "current.Army_status10");
            bool transportEnter = armyStatus == 2;
            if (armyStatus != 1 && !(transportEnter && operationLocal && captives is not null))
                throw Missing("branch.not_ordinary_status1_return");
            if (transportEnter && transportFlags26 is null) throw Missing("current.transport_Flags26");
            bool personReturn = transportEnter && (transportFlags26!.Value & 0x20) != 0;
            if (!operationLocal) reads.Add(new { nativeMode, Before = context.ArmyReturnTransactionFingerprint(), Pool = pool.ArmyReturnFingerprint() });
            var draft = context.CreateArmyReturnTransactionDraft(); var stagedPool = pool.DetachForArmyReturn(draft);
            int commander = Person(Get(draft.ReadArmyUnitCommanderId(army), "current.Commander_word12"));
            if (!Get(draft.ReadPersonNativeValidity(commander), "current.Commander_validity")) throw Missing("branch.invalid_commander");
            byte byte161 = Get(draft.ReadArmyReturnPersonByte199(commander), "current.Commander_byte161");
            reads.Add(new { commander, byte161 });
            if (byte161 != 0) Marker("29F2AB.Person15E_or1");
            //22BB10 returns the Commander's current DISTRICT pointer. Keep
            //that saved identity across Home writes and the ruler tail.
            byte rawDistrict = Get(draft.ReadPersonDistrictSlot(commander), "current.Commander_districtE0");
            var savedCommander = draft.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0) ?? throw Missing("current.saved_Commander_District22BB10");
            var savedTarget = draft.ReadBaseDistrictReference(target) ?? throw Missing("current.saved_target_virtual80_District");
            int targetForce = Get(draft.ReadBaseForceSlot(target), "current.Base_virtualB0");
            byte armyForce = Get(draft.ReadPersonOldForceSlot(commander), "current.Army_force22CD80");
            reads.Add(new { rawDistrict, savedCommander, savedTarget, targetForce, armyForce });
            if (targetForce != armyForce || armyForce is < 1 or > 45) throw Missing("branch.nonordinary_same_force_return");

            var trait = operationLocal ? OriginalArmyReturnTraitCleanup.ApplyCurrent(army, draft, stagedPool, presentation)
                : OriginalArmyReturnTraitCleanup.Prepare(army, draft, stagedPool, presentation);
            if (trait.Packet is null) throw Missing(trait.UnresolvedOwner ?? "current.Trait_cleanup");
            reads.Add(new { Trait = trait.Packet.Fingerprint });
            if (!operationLocal && !OriginalArmyReturnTraitCleanup.ApplyPrepared(trait.Packet)) throw Missing("current.Trait_changed_during_prepare");
            effects.Add("29F2F8.9x2A2340_then36x2A2540");
            OriginalPersonAssignmentPrecallResult? homeAssignments = null;
            if (personReturn)
            {
                if (captives!.ApplyTransportPersonReturn(draft, commander) is string unresolvedReturn) throw Missing(unresolvedReturn);
                effects.Add("29F35D.transport_bit20_Person2EA9A0");
            }
            else
            {
                var home = OriginalArmyReturnCommanderHomeUpdater.ApplyCurrent(army, target, draft, stagedPool, queries, transportEnter);
                if (home.Packet is null) throw Missing(home.UnresolvedOwner ?? "current.CommanderHome");
                reads.Add(new { Home = home.Packet.Fingerprint });
                homeAssignments = home.Packet.AssignmentPrecall;
                if (homeAssignments is not null) effects.Add("271560.29C430_changed_City_assignments");
                effects.Add("29F449.271320_and_caller_list_release");
            }
            Marker("29F47A.Person15E_or1");
            var resource = operationLocal ? OriginalArmyReturnResourceUpdater.ApplyCurrentResourceOutputs(army, target, draft,
                    transportEnter ? (current, amount) => captives!.ApplyTransportMerit(current, army, amount) : null)
                : useResourceOutputProjection ? OriginalArmyReturnResourceUpdater.PrepareResourceOutputs(army, target, draft)
                : OriginalArmyReturnResourceUpdater.Prepare(army, target, draft);
            if (resource.Packet is null) throw Missing(resource.UnresolvedOwner ?? "current.Resources");
            reads.Add(new { Resource = resource.Packet.Fingerprint });
            if (!operationLocal && !OriginalArmyReturnResourceUpdater.ApplyPrepared(resource.Packet)) throw Missing("current.Resources_changed_during_prepare");
            effects.Add("29F483.Resources_morale_ready_wounded_gold_supplies");
            ushort governor = Get(draft.ReadArmyReturnBaseGovernor2E(target), "current.Base_governor2E");
            int currentGovernor = Person(governor);
            bool validGovernor = Get(draft.ReadPersonNativeValidity(currentGovernor), "current.Base_governor_validity");
            reads.Add(new { governor, currentGovernor, validGovernor });
            if (!validGovernor)
            {
                ushort id = Get(draft.ReadPersonIdentity(commander), "current.Commander_id10");
                draft.SetArmyReturnBaseGovernor2E(target, Math.Min(id, (ushort)6000));
                effects.Add("29F70B.Governor_fallback_word2E");
            }
            //2F8F00(R9=0): an actual empty associated list jumps from90F7
            //to9F1A and releases its allocated descriptor; no report/merit
            //or external hooks in the nonempty branch are silently omitted.
            var report = NewTemporary(draft);
            draft.ClearTemporaryHexList(report); //230C8E→669E60→018640.
            if (captives is not null && operationLocal)
            {
                captiveResult = captives.ApplyReport(draft, report, army, target, nativeMode);
                if (captiveResult.Receipt is null) throw Missing(captiveResult.UnresolvedOwner ?? "current.2F8F00_captives");
                effects.Add(captiveResult.Receipt.PersonOrder.Length == 0 ? "29F720.2F8F00_empty_associates_report_descriptor_release"
                    : "29F720.2F8F00_player_nativeState_absent_captives");
            }
            else { RequireNoAssociates(); RequireEmpty(report); effects.Add("29F720.2F8F00_empty_associates_report_descriptor_release"); }
            ClearRelease(draft, report);
            var associated = NewTemporary(draft);
            draft.ClearTemporaryHexList(associated); //230C8E before manager100 scan.
            if (captiveResult?.Receipt is { PersonOrder.Length: > 0 })
            {
                int[] captiveOrder = OriginalArmyReturnCaptivesSource.FillArmyAssociates(draft, associated, army);
                ushort targetSecond = Math.Min(Get(draft.ReadBaseObjectLink(target), "current.return_captive_Base4E"), (ushort)1061);
                foreach (int captive in captiveOrder) draft.WritePersonSecondLocation(captive, targetSecond);
                effects.Add("29F78F.230C70_captives_E4_to_Base4E");
            }
            else { RequireNoAssociates(); RequireEmpty(associated); effects.Add("29F78F.230C70_empty_current_associates"); }
            var pendingDescriptor = draft.ReadArmyReturnPending70Descriptor() ?? throw Missing("current.manager_pending70_descriptor");
            if (!pendingDescriptor.Identity.HasValue || !stagedPool.OwnsArmyReturnDescriptor(pendingDescriptor))
                throw Missing("current.pending70_owned_descriptor");
            var outcome = OriginalArmyPending70Admission.EnqueueCurrentPointer(new(OriginalRegistryKind.Army, army.PhysicalSlot), new Pending(draft));
            if (outcome == OriginalArmyPending70AdmissionOutcome.KnownAllocationFailure) throw Missing("branch.pending70_allocation_failure");
            effects.Add("29F809.pending31F370");
            ushort rawHex = Get(draft.ReadArmyUnitRawHex2A(army), "current.Army_hex2A");
            var hex = draft.ReadHexTableReference(rawHex <= 48399 ? rawHex : 0) ?? throw Missing("current.Army_Hex_table");
            reads.Add(new { rawHex, hex });
            if (hex.PhysicalSlot is int physicalHex) draft.SetHexRaw1C(physicalHex, 0); //226870, no validity guard.
            effects.Add("29F840.Hex1C_clear_after_pending");
            targetForce = Get(draft.ReadBaseForceSlot(target), "current.tail_Base_virtualB0");
            if (targetForce == 0) throw Missing("branch.return_capture26DA10");
            byte role = Get(draft.ReadPersonStatus(commander), "current.tail_Commander_statusE6");
            ushort location = Get(draft.ReadBaseObjectLink(target), "current.Base_word4E");
            int savedPhysical = Get(draft.ReadDistrictPhysicalSlot(savedCommander), "current.saved_Commander_District_virtual60");
            reads.Add(new { targetForce, role, location, savedPhysical });
            if (!personReturn && role == 1 && location <= 51)
            {
                //Actual City vtable88=240D90 writes byte30 even when equal.
                draft.SetCityDistrictSlot(target.PhysicalSlot ?? throw Missing("current.City_pointer"), (byte)Math.Clamp(savedPhysical, 0, 51));
                effects.Add("29F8D0.City_virtual88_District_write");
                foreach (int person in RegisteredPersons())
                {
                    ushort first = Get(draft.ReadPersonFirstLocation(person), "current.ruler_tail_Person_firstE2");
                    if (first != location) continue;
                    byte status = Get(draft.ReadPersonStatus(person), "current.ruler_tail_Person_statusE6");
                    reads.Add(new { person, first, status });
                    if (status is < 1 or > 4) continue;
                    byte formerDistrict = Get(draft.ReadPersonDistrictSlot(person), "current.ruler_tail_Person_E0");
                    OriginalPersonDistrictTransfer.Apply(person, savedPhysical, new Transfer(draft, reads, transferAssociations));
                    districtTransferWrites |= formerDistrict != savedPhysical;
                    rulerOrder.Add(person); effects.Add($"29F910.240DB0.Person[{person}]");
                }
            }
            else if (!personReturn && role == 1)
                effects.Add("29F8BC.Gate_ruler_skips_City_District_and_resident_transfer");
            else if (!personReturn && role == 2)
            {
                int currentDistrict = Get(draft.ReadBaseDistrictSlot(target), "current.leader_tail_Base_virtual78");
                reads.Add(new { currentDistrict });
                if (currentDistrict != savedPhysical) throw Missing("branch.leader_tail_different_District240DB0");
                effects.Add("29F979.same_District_leader_no_transfer");
            }
            else if (!personReturn && (role is < 3 or > 4)) throw Missing("branch.nonordinary_tail_role");
            var primary = operationLocal ? OriginalArmyReturnPrimaryDistrictUpdater.PrepareCurrent(savedTarget, draft, new CapacityQueries(queries, stagedPool))
                : OriginalArmyReturnPrimaryDistrictUpdater.Prepare(savedTarget, draft, new CapacityQueries(queries, stagedPool));
            if (primary.Packet is null) throw Missing(primary.UnresolvedOwner ?? "current.final_PrimaryDistrict");
            reads.Add(new { FinalPrimary = primary.Packet.Fingerprint });
            foreach (var gov in OriginalArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(savedTarget, draft, stagedPool, queries))
                reads.Add(new { AppliedGovernor = gov.Fingerprint });
            effects.Add("29F9A7.saved_target_District2E9140");
            ClearRelease(draft, associated); effects.Add("29F9F5.associated_descriptor_clear_release");
            if (!operationLocal) reads.Add(new { After = draft.ArmyReturnTransactionFingerprint(), Pool = stagedPool.ArmyReturnFingerprint(), outcome, rulerOrder, effects });
            string fingerprint = operationLocal ? "" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reads))));
            return new(new(army, target, savedCommander, savedTarget, outcome, rulerOrder.ToArray(), effects.ToArray(), context,
                pool, stagedPool, queries, presentation, nativeMode, fingerprint, useResourceOutputProjection, homeAssignments,
                districtTransferWrites, transferAssociations.ToArray(), operationLocal, captiveResult?.State, captiveResult?.Receipt), null);

            int Person(ushort id)
            {
                var value = draft.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw Missing("current.Person_lookup");
                int physical = value.PhysicalSlot ?? throw Missing("current.Person_pointer"); reads.Add(new { id, physical }); return physical;
            }
            void Marker(string step)
            {
                ushort flags = Get(draft.ReadArmyReturnPersonFlags196(commander), "current.Commander_flags15E");
                reads.Add(new { flags, step }); draft.WriteArmyReturnPersonFlags196(commander, (ushort)(flags | 1)); effects.Add(step);
            }
            int[] RegisteredPersons()
            {
                var sequence = draft.ReadRegisteredPersonPhysicalSlots()?.ToArray() ?? throw Missing("current.Person_manager100_complete_order");
                reads.Add(sequence); return sequence.Select(x => x ?? throw Missing("current.Person_manager100_payload")).ToArray();
            }
            void RequireNoAssociates()
            {
                int location = army.PhysicalSlot is >= 1 and <= 500 ? army.PhysicalSlot.Value + 61 : 0;
                foreach (int person in RegisteredPersons())
                {
                    byte status = Get(draft.ReadPersonStatus(person), "current.associated_Person_statusE6"); reads.Add(new { person, status });
                    if (status != 6) continue;
                    ushort second = Get(draft.ReadPersonSecondLocation(person), "current.associated_Person_secondE4"); reads.Add(new { second });
                    if (second == location) throw Missing("branch.nonempty_status6_Army_associates2F8F00");
                }
            }
            void RequireEmpty(OriginalHexListHandle descriptor)
            {
                var head = draft.ReadRegistryHead(new(descriptor.Identity)) ?? throw Missing("current.associated_temporary_head");
                int count = Get(draft.ReadTemporaryHexListCount(descriptor), "current.associated_temporary_count");
                reads.Add(new { descriptor, head, count });
                if (head.Identity.HasValue || count != 0) throw Missing("branch.report_not_actual_empty_descriptor");
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.OrdinaryReturn_input: " + error.Message); }
    }
    public static bool ApplyPrepared(OriginalArmyOrdinaryReturnPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var current = packet.IsOperationLocal ? packet : Prepare(packet.Army, packet.Base, packet.NativeMode, packet.Owner, packet.Pool, packet.Queries, packet.Presentation,
            packet.UsesResourceOutputProjection).Packet;
        if (current is null || current.Fingerprint != packet.Fingerprint) return false;
        packet.Pool.CommitArmyReturnPool(current.DraftPool);
        packet.Owner.CommitArmyReturnTransactionFields(current.DraftPool.ArmyReturnOwner); return true;
    }
    private static OriginalHexListHandle NewTemporary(OriginalEventInitializationContext state)
    {
        var value = state.CreateTemporaryHexList() ?? throw Missing("current.temporary_descriptor");
        if (!value.Identity.HasValue) throw Missing("branch.return_temporary_allocation_failure"); return value;
    }
    private static void ClearRelease(OriginalEventInitializationContext state, OriginalHexListHandle value)
    { state.ClearTemporaryHexList(value); state.ReleaseTemporaryHexList(value); }
    private sealed class CapacityQueries(IOriginalArmyReturnGovernorCurrentQueries owner, OriginalCurrentListPool pool) : IOriginalArmyReturnGovernorCurrentQueries
    {
        public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int person) => owner.ReadCurrentComparatorFields(person);
        public int? ReadCurrentAssociatedVirtual98(OriginalPersonLinkedEntityReference reference) => owner.ReadCurrentAssociatedVirtual98(reference);
        public OriginalArmyReturnGovernorPoolCapacity? ReadCurrentTemporaryPoolCapacity() => pool.ArmyReturnCapacity;
    }
    private sealed class Pending(OriginalEventInitializationContext state) : IOriginalArmyPending70AdmissionState
    {
        public OriginalRegistryListHandle? ReadPending70Descriptor() => state.ReadArmyReturnPending70Descriptor();
        public OriginalRegistryListNodeReference? ReadPending70Head(OriginalRegistryListHandle value) => state.ReadRegistryHead(value);
        public OriginalRegistryObjectReference? ReadPending70Payload(OriginalRegistryListNodeReference node) => state.ReadRegistryNodePayload(node);
        public OriginalRegistryListNodeReference? ReadPending70Next(OriginalRegistryListNodeReference node) => state.ReadRegistryNodeNext(node);
        public bool? ReadPending70LockEnabled() => state.ReadRegistryLockEnabled();
        public void EnterPending70Lock() => state.EnterRegistryLock();
        public void ExitPending70Lock() => state.ExitRegistryLock();
        public OriginalRegistryListNodeReference? AppendPending70Node(OriginalRegistryListHandle value) => state.AllocateRegistryNode(value);
        public void WritePending70Payload(OriginalRegistryListNodeReference node, OriginalRegistryObjectReference army) => state.WriteRegistryNodePayload(node, army);
    }
    private sealed class Transfer(OriginalEventInitializationContext state, List<object> reads,
        List<OriginalPersonAssignmentPrecallResult> associations) : IOriginalPersonDistrictTransferState
    {
        public byte? ReadPersonDistrictSlot(int person) => state.ReadPersonDistrictSlot(person);
        public OriginalForceReference? ReadPersonForce(int person) => state.ReadPersonForce(person);
        public OriginalForceReference? ReadRequestedDistrictForce(int district) => state.ReadRequestedDistrictForce(district);
        public void RemoveForcePersonAssociation(OriginalForceReference force, int person)
        {
            //240DB0 uses the same real2783B0 R8=0/R9=1 arguments.
            var result = OriginalPersonAssignmentPrecallSource.Apply2783B0ForCapture(state, force, person);
            if (result.NativeReturn is null) throw Missing(result.UnresolvedOwner ?? "current.ruler_tail_2783B0");
            reads.Add(result); associations.Add(result);
        }
        public int? ReadForcePhysicalSlot(OriginalForceReference force) => state.ReadForcePhysicalSlot(force);
        public void WritePersonResetWord1FF(int person, ushort value) => state.WritePersonResetWord1FF(person, value);
        public void WritePersonResetByte1FE(int person, byte value) => state.WritePersonResetByte1FE(person, value);
        public void WritePersonDistrictSlot(int person, byte value) => state.WritePersonDistrictSlot(person, value);
        public byte? ReadDistrictForceWithoutValidity(int district) => state.ReadDistrictForceWithoutValidity(district);
        public OriginalForceReference? ReadCurrentPlayerForce() => state.ReadCurrentPlayerForce();
        public void RefreshTransferService(int first, int second, int third)
        {
            if (first != 1 || second != 1 || third != 0) throw Missing("branch.unsupported_service_dispatch");
            if (state.TransferService is null) throw Missing("current.ruler_tail_service_owner");
            var eligibility = new Eligibility(state, reads);
            bool eligible = OriginalTransferServiceEligibility.IsEligible(eligibility); reads.Add(new { eligible });
            //Both branches execute the original eligibility/count logic.
            //No state/bitmap callback is used to pretend a missing effect ran.
            if (!eligible) return;
            var player = state.ReadCurrentPlayerForce() ?? throw Missing("current.service_PlayerForce");
            bool valid = Get(state.ReadForceNativeValidity(player), "current.service_PlayerForce_validity"); reads.Add(new { player, valid });
            if (!valid) return;
            int count = 0;
            for (int i = 0; i < 1851; i++)
            {
                var person = state.ReadTransferPersonTableReference(i) ?? throw Missing($"current.service_owned_Person_table[{i}]");
                reads.Add(new { i, person }); if (person.PhysicalSlot is not int physical) continue;
                bool personValid = Get(state.ReadPersonNativeValidity(physical), "current.service_Person_validity"); reads.Add(new { personValid });
                if (!personValid) continue;
                byte status = Get(state.ReadPersonStatus(physical), "current.service_Person_statusE6"); reads.Add(new { status });
                if (status is < 1 or > 4) continue;
                int force = Get(state.ReadForcePhysicalSlot(player), "current.service_PlayerForce_virtual60");
                byte raw = Get(state.ReadPersonCurrentRawForceSlot(physical), "current.service_Person_Force22CF60"); reads.Add(new { force, raw });
                if (force == raw) count++;
            }
            reads.Add(new { serviceCurrentCount = count });
            if (count >= 200) throw Missing("branch.ruler_tail_service_flag17_or_completion_effects");
        }
    }
    private sealed class Eligibility(OriginalEventInitializationContext state, List<object> reads) : IOriginalTransferServiceEligibilityQueries
    {
        public uint? ReadWorldTransferServiceFlags()
        { var value = state.ReadWorldTransferServiceFlags(); reads.Add(new { world16A8 = value }); return value; }
        public OriginalTransferModeState? ReadTransferModeState()
        { var value = state.ArmyReturnTransferMode; reads.Add(new { mode = value }); return value; }
    }
    private static T Get<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static MissingOwner Missing(string field) => new(field);
    private sealed class MissingOwner(string message) : Exception(message);
}
