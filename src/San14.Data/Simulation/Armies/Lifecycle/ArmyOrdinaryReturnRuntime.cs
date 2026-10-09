using San14.Core;

namespace San14.Data;

public sealed record ArmyOrdinaryReturnResolution(ArmyOrdinaryReturnPacket? Packet, string? UnresolvedOwner);
public sealed class ArmyOrdinaryReturnPacket
{
    public ArmyUnitReference Army { get; }
    public BaseReference Base { get; }
    public DistrictReference SavedCommanderDistrict { get; }
    public DistrictReference SavedTargetDistrict { get; }
    public ArmyPending70AdmissionOutcome PendingOutcome { get; }
    public IReadOnlyList<int> RulerTailPersonOrder { get; }
    public IReadOnlyList<string> SourceEffectOrder { get; }
    public PersonAssignmentPrecallResult? HomeAssignments { get; }
    public bool HasAssignmentCurrentWrites { get; }
    public IReadOnlyList<PersonAssignmentPrecallResult> RulerTailAssociationUpdates { get; }
    public ArmyReturnCaptivesReceipt? Captives { get; }
    internal GameState? CaptivesState { get; }
    internal SourceWorldContext Owner { get; }
    internal WorldIndexContext Pool { get; }
    internal WorldIndexContext DraftPool { get; }
    internal ArmyOrdinaryReturnPacket(ArmyUnitReference army, BaseReference target,
        DistrictReference savedCommander, DistrictReference savedTarget, ArmyPending70AdmissionOutcome outcome,
        int[] order, string[] effects, SourceWorldContext owner, WorldIndexContext pool,
        WorldIndexContext draft, PersonAssignmentPrecallResult? homeAssignments = null,
        bool hasDistrictTransferWrites = false, PersonAssignmentPrecallResult[]? rulerTailAssociationUpdates = null,
        GameState? captivesState = null, ArmyReturnCaptivesReceipt? captives = null)
    {
        Army = army; Base = target; SavedCommanderDistrict = savedCommander; SavedTargetDistrict = savedTarget; PendingOutcome = outcome;
        RulerTailPersonOrder = Array.AsReadOnly(order); SourceEffectOrder = Array.AsReadOnly(effects);
        Owner = owner; Pool = pool; DraftPool = draft;
        HomeAssignments = homeAssignments;
        HasAssignmentCurrentWrites = homeAssignments is not null || hasDistrictTransferWrites;
        RulerTailAssociationUpdates = Array.AsReadOnly(rulerTailAssociationUpdates ?? []);
        CaptivesState = captivesState; Captives = captives;
    }
}

/// <summary>Prepares a friendly-base return in one private operation context.
/// Trait removal, commander relocation, resources, captives and district
/// updates run in order. Missing inputs discard this candidate; publication
/// and receiver retirement are handled by the enclosing campaign operation.</summary>
public static class ArmyOrdinaryReturnRuntime
{
    internal static ArmyOrdinaryReturnResolution PrepareCurrentResourceOutputs(ArmyUnitReference army, BaseReference target,
        int nativeMode, SourceWorldContext context, WorldIndexContext pool,
        IArmyReturnGovernorCurrentQueries queries, ArmyReturnTraitPresentationOwners presentation,
        ArmyReturnCaptivesSource? captives = null, byte? transportFlags26 = null)
    {
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pool); ArgumentNullException.ThrowIfNull(queries); ArgumentNullException.ThrowIfNull(presentation);
        var effects = new List<string>(); var rulerOrder = new List<int>();
        bool districtTransferWrites = false;
        var transferAssociations = new List<PersonAssignmentPrecallResult>();
        ArmyReturnCaptivesResult? captiveResult = null;
        try
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, context) || !pool.OwnsArmyReturnProfile()) throw Missing("current.complete_owned_shared_pool");
            if (context.TransferServiceRefresher is not null || context.TransferServiceEligibilityReader is not null || context.TransferModeStateReader is not null)
                throw Missing("current.owned_return_service_source_fields");
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            byte armyStatus = Get(context.ReadArmyReturnUnitStatus10(army), "current.Army_status10");
            bool transportEnter = armyStatus == 2;
            if (armyStatus != 1 && !(transportEnter && captives is not null))
                throw Missing("branch.not_ordinary_status1_return");
            if (transportEnter && transportFlags26 is null) throw Missing("current.transport_Flags26");
            bool personReturn = transportEnter && (transportFlags26!.Value & 0x20) != 0;
            var draft = context.CreateArmyReturnTransactionDraft(); var stagedPool = pool.DetachForArmyReturn(draft);
            int commander = Person(Get(draft.ReadArmyUnitCommanderId(army), "current.Commander_word12"));
            if (!Get(draft.ReadPersonNativeValidity(commander), "current.Commander_validity")) throw Missing("branch.invalid_commander");
            byte byte161 = Get(draft.ReadArmyReturnPersonByte199(commander), "current.Commander_byte161");

            if (byte161 != 0) Marker("29F2AB.Person15E_or1");
            //22BB10 returns the Commander's current DISTRICT pointer. Keep
            //that saved identity across Home writes and the ruler tail.
            byte rawDistrict = Get(draft.ReadPersonDistrictSlot(commander), "current.Commander_districtE0");
            var savedCommander = draft.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0) ?? throw Missing("current.saved_Commander_District22BB10");
            var savedTarget = draft.ReadBaseDistrictReference(target) ?? throw Missing("current.saved_target_virtual80_District");
            int targetForce = Get(draft.ReadBaseForceSlot(target), "current.Base_virtualB0");
            byte armyForce = Get(draft.ReadPersonOldForceSlot(commander), "current.Army_force22CD80");

            if (targetForce != armyForce || armyForce is < 1 or > 45) throw Missing("branch.nonordinary_same_force_return");

            var trait = ArmyReturnTraitCleanup.ApplyCurrent(army, draft, stagedPool, presentation);
            if (trait.Packet is null) throw Missing(trait.UnresolvedOwner ?? "current.Trait_cleanup");

            effects.Add("29F2F8.9x2A2340_then36x2A2540");
            PersonAssignmentPrecallResult? homeAssignments = null;
            if (personReturn)
            {
                if (captives!.ApplyTransportPersonReturn(draft, commander) is string unresolvedReturn) throw Missing(unresolvedReturn);
                effects.Add("29F35D.transport_bit20_Person2EA9A0");
            }
            else
            {
                var home = ArmyReturnCommanderHomeUpdater.ApplyCurrent(army, target, draft, stagedPool, queries, transportEnter);
                if (home.Packet is null) throw Missing(home.UnresolvedOwner ?? "current.CommanderHome");

                homeAssignments = home.Packet.AssignmentPrecall;
                if (homeAssignments is not null) effects.Add("271560.29C430_changed_City_assignments");
                effects.Add("29F449.271320_and_caller_list_release");
            }
            Marker("29F47A.Person15E_or1");
            var resource = ArmyReturnResourceUpdater.ApplyCurrentResourceOutputs(army, target, draft,
                transportEnter ? (current, amount) => captives!.ApplyTransportMerit(current, army, amount) : null);
            if (resource.Packet is null) throw Missing(resource.UnresolvedOwner ?? "current.Resources");

            effects.Add("29F483.Resources_morale_ready_wounded_gold_supplies");
            ushort governor = Get(draft.ReadArmyReturnBaseGovernor2E(target), "current.Base_governor2E");
            int currentGovernor = Person(governor);
            bool validGovernor = Get(draft.ReadPersonNativeValidity(currentGovernor), "current.Base_governor_validity");

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
            if (captives is not null)
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
                int[] captiveOrder = ArmyReturnCaptivesSource.FillArmyAssociates(draft, associated, army);
                ushort targetSecond = Math.Min(Get(draft.ReadBaseObjectLink(target), "current.return_captive_Base4E"), (ushort)1061);
                foreach (int captive in captiveOrder) draft.WritePersonSecondLocation(captive, targetSecond);
                effects.Add("29F78F.230C70_captives_E4_to_Base4E");
            }
            else { RequireNoAssociates(); RequireEmpty(associated); effects.Add("29F78F.230C70_empty_current_associates"); }
            var pendingDescriptor = draft.ReadArmyReturnPending70Descriptor() ?? throw Missing("current.manager_pending70_descriptor");
            if (!pendingDescriptor.Identity.HasValue || !stagedPool.OwnsArmyReturnDescriptor(pendingDescriptor))
                throw Missing("current.pending70_owned_descriptor");
            var outcome = ArmyPending70Admission.EnqueueCurrentPointer(new(RegistryKind.Army, army.PhysicalSlot), new Pending(draft));
            if (outcome == ArmyPending70AdmissionOutcome.KnownAllocationFailure) throw Missing("branch.pending70_allocation_failure");
            effects.Add("29F809.pending31F370");
            ushort rawHex = Get(draft.ReadArmyUnitRawHex2A(army), "current.Army_hex2A");
            var hex = draft.ReadHexTableReference(rawHex <= 48399 ? rawHex : 0) ?? throw Missing("current.Army_Hex_table");

            if (hex.PhysicalSlot is int physicalHex) draft.SetHexRaw1C(physicalHex, 0); //226870, no validity guard.
            effects.Add("29F840.Hex1C_clear_after_pending");
            targetForce = Get(draft.ReadBaseForceSlot(target), "current.tail_Base_virtualB0");
            if (targetForce == 0) throw Missing("branch.return_capture26DA10");
            byte role = Get(draft.ReadPersonStatus(commander), "current.tail_Commander_statusE6");
            ushort location = Get(draft.ReadBaseObjectLink(target), "current.Base_word4E");
            int savedPhysical = Get(draft.ReadDistrictPhysicalSlot(savedCommander), "current.saved_Commander_District_virtual60");

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

                    if (status is < 1 or > 4) continue;
                    byte formerDistrict = Get(draft.ReadPersonDistrictSlot(person), "current.ruler_tail_Person_E0");
                    PersonDistrictTransfer.Apply(person, savedPhysical, new Transfer(draft, transferAssociations));
                    districtTransferWrites |= formerDistrict != savedPhysical;
                    rulerOrder.Add(person); effects.Add($"29F910.240DB0.Person[{person}]");
                }
            }
            else if (!personReturn && role == 1)
                effects.Add("29F8BC.Gate_ruler_skips_City_District_and_resident_transfer");
            else if (!personReturn && role == 2)
            {
                int currentDistrict = Get(draft.ReadBaseDistrictSlot(target), "current.leader_tail_Base_virtual78");

                if (currentDistrict != savedPhysical) throw Missing("branch.leader_tail_different_District240DB0");
                effects.Add("29F979.same_District_leader_no_transfer");
            }
            else if (!personReturn && (role is < 3 or > 4)) throw Missing("branch.nonordinary_tail_role");
            _ = ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(savedTarget, draft, queries);
            effects.Add("29F9A7.saved_target_District2E9140");
            ClearRelease(draft, associated); effects.Add("29F9F5.associated_descriptor_clear_release");
            return new(new(army, target, savedCommander, savedTarget, outcome, rulerOrder.ToArray(), effects.ToArray(), context,
                pool, stagedPool, homeAssignments,
                districtTransferWrites, transferAssociations.ToArray(), captiveResult?.State, captiveResult?.Receipt), null);

            int Person(ushort id)
            {
                var value = draft.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw Missing("current.Person_lookup");
                return value.PhysicalSlot ?? throw Missing("current.Person_pointer");
            }
            void Marker(string step)
            {
                ushort flags = Get(draft.ReadArmyReturnPersonFlags196(commander), "current.Commander_flags15E");
                draft.WriteArmyReturnPersonFlags196(commander, (ushort)(flags | 1)); effects.Add(step);
            }
            int[] RegisteredPersons()
            {
                var sequence = draft.ReadRegisteredPersonPhysicalSlots()?.ToArray() ?? throw Missing("current.Person_manager100_complete_order");
                return sequence.Select(x => x ?? throw Missing("current.Person_manager100_payload")).ToArray();
            }
            void RequireNoAssociates()
            {
                int location = army.PhysicalSlot is >= 1 and <= 500 ? army.PhysicalSlot.Value + 61 : 0;
                foreach (int person in RegisteredPersons())
                {
                    byte status = Get(draft.ReadPersonStatus(person), "current.associated_Person_statusE6");
                    if (status != 6) continue;
                    ushort second = Get(draft.ReadPersonSecondLocation(person), "current.associated_Person_secondE4");
                    if (second == location) throw Missing("branch.nonempty_status6_Army_associates2F8F00");
                }
            }
            void RequireEmpty(HexListHandle descriptor)
            {
                var head = draft.ReadRegistryHead(new(descriptor.Identity)) ?? throw Missing("current.associated_temporary_head");
                int count = Get(draft.ReadTemporaryHexListCount(descriptor), "current.associated_temporary_count");

                if (head.Identity.HasValue || count != 0) throw Missing("branch.report_not_actual_empty_descriptor");
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.OrdinaryReturn_input: " + error.Message); }
    }
    internal static void Commit(ArmyOrdinaryReturnPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        packet.Pool.CommitArmyReturnPool(packet.DraftPool);
        packet.Owner.CommitArmyReturnTransactionFields(packet.DraftPool.ArmyReturnOwner);
    }
    private static HexListHandle NewTemporary(SourceWorldContext state)
    {
        var value = state.CreateTemporaryHexList() ?? throw Missing("current.temporary_descriptor");
        if (!value.Identity.HasValue) throw Missing("branch.return_temporary_allocation_failure"); return value;
    }
    private static void ClearRelease(SourceWorldContext state, HexListHandle value)
    { state.ClearTemporaryHexList(value); state.ReleaseTemporaryHexList(value); }
    private sealed class Pending(SourceWorldContext state) : IArmyPending70AdmissionState
    {
        public RegistryListHandle? ReadPending70Descriptor() => state.ReadArmyReturnPending70Descriptor();
        public RegistryListNodeReference? ReadPending70Head(RegistryListHandle value) => state.ReadRegistryHead(value);
        public RegistryObjectReference? ReadPending70Payload(RegistryListNodeReference node) => state.ReadRegistryNodePayload(node);
        public RegistryListNodeReference? ReadPending70Next(RegistryListNodeReference node) => state.ReadRegistryNodeNext(node);
        public bool? ReadPending70LockEnabled() => state.ReadRegistryLockEnabled();
        public void EnterPending70Lock() => state.EnterRegistryLock();
        public void ExitPending70Lock() => state.ExitRegistryLock();
        public RegistryListNodeReference? AppendPending70Node(RegistryListHandle value) => state.AllocateRegistryNode(value);
        public void WritePending70Payload(RegistryListNodeReference node, RegistryObjectReference army) => state.WriteRegistryNodePayload(node, army);
    }
    private sealed class Transfer(SourceWorldContext state,
        List<PersonAssignmentPrecallResult> associations) : IPersonDistrictTransferState
    {
        public byte? ReadPersonDistrictSlot(int person) => state.ReadPersonDistrictSlot(person);
        public ForceReference? ReadPersonForce(int person) => state.ReadPersonForce(person);
        public ForceReference? ReadRequestedDistrictForce(int district) => state.ReadRequestedDistrictForce(district);
        public void RemoveForcePersonAssociation(ForceReference force, int person)
        {
            //240DB0 uses the same real2783B0 R8=0/R9=1 arguments.
            var result = PersonAssignmentPrecallSource.Apply2783B0ForCapture(state, force, person);
            if (result.NativeReturn is null) throw Missing(result.UnresolvedOwner ?? "current.ruler_tail_2783B0");
            associations.Add(result);
        }
        public int? ReadForcePhysicalSlot(ForceReference force) => state.ReadForcePhysicalSlot(force);
        public void WritePersonResetWord1FF(int person, ushort value) => state.WritePersonResetWord1FF(person, value);
        public void WritePersonResetByte1FE(int person, byte value) => state.WritePersonResetByte1FE(person, value);
        public void WritePersonDistrictSlot(int person, byte value) => state.WritePersonDistrictSlot(person, value);
        public byte? ReadDistrictForceWithoutValidity(int district) => state.ReadDistrictForceWithoutValidity(district);
        public ForceReference? ReadCurrentPlayerForce() => state.ReadCurrentPlayerForce();
        public void RefreshTransferService(int first, int second, int third)
        {
            if (first != 1 || second != 1 || third != 0) throw Missing("branch.unsupported_service_dispatch");
            if (state.TransferService is null) throw Missing("current.ruler_tail_service_owner");
            var eligibility = new Eligibility(state);
            bool eligible = TransferServiceEligibility.IsEligible(eligibility);
            //Both branches execute the original eligibility/count logic.
            //No state/bitmap callback is used to pretend a missing effect ran.
            if (!eligible) return;
            var player = state.ReadCurrentPlayerForce() ?? throw Missing("current.service_PlayerForce");
            bool valid = Get(state.ReadForceNativeValidity(player), "current.service_PlayerForce_validity");
            if (!valid) return;
            int count = 0;
            for (int i = 0; i < 1851; i++)
            {
                var person = state.ReadTransferPersonTableReference(i) ?? throw Missing($"current.service_owned_Person_table[{i}]");
                if (person.PhysicalSlot is not int physical) continue;
                bool personValid = Get(state.ReadPersonNativeValidity(physical), "current.service_Person_validity");
                if (!personValid) continue;
                byte status = Get(state.ReadPersonStatus(physical), "current.service_Person_statusE6");
                if (status is < 1 or > 4) continue;
                int force = Get(state.ReadForcePhysicalSlot(player), "current.service_PlayerForce_virtual60");
                byte raw = Get(state.ReadPersonCurrentRawForceSlot(physical), "current.service_Person_Force22CF60");
                if (force == raw) count++;
            }

            if (count >= 200) throw Missing("branch.ruler_tail_service_flag17_or_completion_effects");
        }
    }
    private sealed class Eligibility(SourceWorldContext state) : ITransferServiceEligibilityQueries
    {
        public uint? ReadWorldTransferServiceFlags()
            => state.ReadWorldTransferServiceFlags();
        public TransferModeState? ReadTransferModeState()
            => state.ArmyReturnTransferMode;
    }
    private static T Get<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static MissingOwner Missing(string field) => new(field);
    private sealed class MissingOwner(string message) : Exception(message);
}
