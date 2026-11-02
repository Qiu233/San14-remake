using San14.Core;

namespace San14.Data;

public sealed record ArmyOrdinaryReturnResolution(ArmyOrdinaryReturnPacket? Packet, string? UnresolvedOwner)
{
    internal ArmyOrdinaryReturnSuspension? Suspension { get; init; }
}

/// <summary>Immediate private publication of an actual player-prefix boundary.
/// Only its domain State survives; these borrowed owners are not serialized.</summary>
internal sealed record ArmyOrdinaryReturnSuspension(GameState State, SourceWorldContext Owner,
    WorldIndexContext Pool, WorldIndexContext DraftPool, ArmyReturnDistrictOperationQueries? DistrictOperation);
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
    internal ArmyReturnDistrictOperationQueries? DistrictOperation { get; }
    internal ArmyOrdinaryReturnPacket(ArmyUnitReference army, BaseReference target,
        DistrictReference savedCommander, DistrictReference savedTarget, ArmyPending70AdmissionOutcome outcome,
        int[] order, string[] effects, SourceWorldContext owner, WorldIndexContext pool,
        WorldIndexContext draft, PersonAssignmentPrecallResult? homeAssignments = null,
        bool hasDistrictTransferWrites = false, PersonAssignmentPrecallResult[]? rulerTailAssociationUpdates = null,
        GameState? captivesState = null, ArmyReturnCaptivesReceipt? captives = null,
        ArmyReturnDistrictOperationQueries? districtOperation = null)
    {
        Army = army; Base = target; SavedCommanderDistrict = savedCommander; SavedTargetDistrict = savedTarget; PendingOutcome = outcome;
        RulerTailPersonOrder = Array.AsReadOnly(order); SourceEffectOrder = Array.AsReadOnly(effects);
        Owner = owner; Pool = pool; DraftPool = draft;
        HomeAssignments = homeAssignments;
        HasAssignmentCurrentWrites = homeAssignments is not null || hasDistrictTransferWrites;
        RulerTailAssociationUpdates = Array.AsReadOnly(rulerTailAssociationUpdates ?? []);
        CaptivesState = captivesState; Captives = captives;
        DistrictOperation = districtOperation;
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
        ArmyReturnCaptivesSource? captives = null, byte? transportFlags26 = null,
        Action<SourceWorldContext, int, PersonAssignmentPrecallResult>? receiveAssignment = null,
        Func<SourceWorldContext, IArmyReturnGovernorCurrentQueries, ArmyReturnDistrictOperationQueries>? createDistrictOperation = null)
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
            //An enclosing Person/captive operation may own its real service
            //callback. It is not copied to the transaction: this private draft
            //binds the returning operation's own pending-service publisher.
            if (context.TransferServiceEligibilityReader is not null || context.TransferModeStateReader is not null)
                throw Missing("current.owned_return_service_source_fields");
            if (!Get(context.ReadArmyUnitNativeValidity(army), "current.Army_validity")) throw Missing("branch.invalid_army");
            byte armyStatus = Get(context.ReadArmyReturnUnitStatus10(army), "current.Army_status10");
            bool transportEnter = armyStatus == 2;
            if (armyStatus != 1 && !(transportEnter && captives is not null))
                throw Missing("branch.not_ordinary_status1_return");
            if (transportEnter && transportFlags26 is null) throw Missing("current.transport_Flags26");
            bool personReturn = transportEnter && (transportFlags26!.Value & 0x20) != 0;
            var draft = context.CreateArmyReturnTransactionDraft(); var stagedPool = pool.DetachForArmyReturn(draft);
            captives?.BindPersonnelServiceCurrent(draft);
            var districtOperation = createDistrictOperation?.Invoke(draft, queries);
            if (districtOperation is not null) queries = districtOperation;
            int commander = Person(Get(draft.ReadArmyUnitCommanderId(army), "current.Commander_word12"));
            if (!Get(draft.ReadPersonNativeValidity(commander), "current.Commander_validity")) throw Missing("branch.invalid_commander");
            var health = OfficerHealthSourceAdapter.Read(draft, commander) ?? throw Missing("current.Commander_byte161");

            if (San14.Core.OfficerHealthRules.IsInjured(health)) Marker("29F2AB.Person15E_or1");
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
                var home = ArmyReturnCommanderHomeUpdater.ApplyCurrent(army, target, draft, stagedPool, queries, transportEnter,
                    assignment => receiveAssignment?.Invoke(draft, commander, assignment));
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
                if (captiveResult.PlayerPreparation is { } playerPreparation)
                {
                    ClearRelease(draft, report);
                    var held = captives.PreparePlayerSuspension(draft, army, target,
                        savedCommander, savedTarget, personReturn, playerPreparation);
                    if (districtOperation is not null)
                    {
                        held = PersonReturnDistrictTransferSource.PublishAreaAssignments(held, draft,
                            districtOperation.ChangedAreas, districtOperation.ChangedGates);
                        held = PersonReturnDistrictReorganizationSource.PublishCurrent(held, draft, districtOperation);
                        held = held with { PersonnelCommands = draft.ReadOwnedPersonCommandManager() };
                        captives.AdoptCurrentState(held);
                    }
                    return new(null, null)
                        { Suspension = new(held, context, pool, stagedPool, districtOperation) };
                }
                if (captiveResult.Receipt is null) throw Missing(captiveResult.UnresolvedOwner ?? "current.2F8F00_captives");
                effects.Add(captiveResult.Receipt.PersonOrder.Length == 0 ? "29F720.2F8F00_empty_associates_report_descriptor_release"
                    : "29F720.2F8F00_player_nativeState_absent_captives");
            }
            else { RequireNoAssociates(); RequireEmpty(report); effects.Add("29F720.2F8F00_empty_associates_report_descriptor_release"); }
            ClearRelease(draft, report);
            return PrepareTailCurrent(army, target, context, pool, draft, stagedPool, queries,
                savedCommander, savedTarget, personReturn, captives, captiveResult, districtOperation,
                homeAssignments, districtTransferWrites, effects, receiveAssignment);

            int Person(ushort id)
            {
                var value = draft.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw Missing("current.Person_lookup");
                return value.PhysicalSlot ?? throw Missing("current.Person_pointer");
            }
            void Marker(string step)
            {
                ushort flags = Get(draft.ReadArmyReturnPersonFlags196(commander), "current.Commander_flags15E");
                draft.WriteArmyReturnPersonFlags196(commander, OfficerBehaviorSourceAdapter.MarkReturn(flags)); effects.Add(step);
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
    internal static ArmyOrdinaryReturnResolution PrepareTailCurrent(ArmyUnitReference army, BaseReference target,
        SourceWorldContext context, WorldIndexContext pool, SourceWorldContext draft, WorldIndexContext stagedPool,
        IArmyReturnGovernorCurrentQueries queries, DistrictReference savedCommander, DistrictReference savedTarget,
        bool personReturn, ArmyReturnCaptivesSource? captives, ArmyReturnCaptivesResult? captiveResult,
        ArmyReturnDistrictOperationQueries? districtOperation, PersonAssignmentPrecallResult? homeAssignments,
        bool districtTransferWrites, List<string> effects,
        Action<SourceWorldContext, int, PersonAssignmentPrecallResult>? receiveAssignment)
    {
        var rulerOrder = new List<int>();
        var transferAssociations = new List<PersonAssignmentPrecallResult>();
        captives?.BindPersonnelServiceCurrent(draft);
        int commander = (draft.ReadPersonLookupReference(Get(draft.ReadArmyUnitCommanderId(army), "tail_Commander12"))
            ?? throw Missing("tail_Commander_lookup")).PhysicalSlot ?? throw Missing("tail_Commander_pointer");
        try
        {
            var associated = NewTemporary(draft);
            draft.ClearTemporaryHexList(associated); //230C8E before manager100 scan.
            if (captiveResult?.Receipt is { PersonOrder.Length: > 0 } ||
                captives?.CurrentState.ArmyReturnCaptiveContinuation is { OriginalOfficerIds.Length: > 0 })
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
            int targetForce = Get(draft.ReadBaseForceSlot(target), "current.tail_Base_virtualB0");
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
                    PersonDistrictTransfer.Apply(person, savedPhysical, new Transfer(draft, transferAssociations,
                        receivePrecall: assignment => receiveAssignment?.Invoke(draft, person, assignment)));
                    districtTransferWrites |= formerDistrict != savedPhysical;
                    rulerOrder.Add(person); effects.Add($"29F910.240DB0.Person[{person}]");
                }
            }
            else if (!personReturn && role == 1)
                effects.Add("29F8BC.Gate_ruler_skips_City_District_and_resident_transfer");
            else if (!personReturn && role == 2)
            {
                int currentDistrict = Get(draft.ReadBaseDistrictSlot(target), "current.leader_tail_Base_virtual78");

                if (currentDistrict != savedPhysical)
                {
                    PersonDistrictTransfer.Apply(commander, currentDistrict, new Transfer(draft, transferAssociations,
                        receivePrecall: assignment => receiveAssignment?.Invoke(draft, commander, assignment)));
                    draft.WritePersonStatus(commander, 4); //29F99C:241D30(Person,4), after240DB0.
                    districtTransferWrites = true;
                    effects.Add("29F979.leader_District240DB0_then_status4");
                }
                else effects.Add("29F979.same_District_leader_no_transfer");
            }
            else if (!personReturn && (role is < 3 or > 4)) throw Missing("branch.nonordinary_tail_role");
            _ = ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(savedTarget, draft, queries);
            effects.Add("29F9A7.saved_target_District2E9140");
            ClearRelease(draft, associated); effects.Add("29F9F5.associated_descriptor_clear_release");
            var publication = captives?.CurrentState ?? captiveResult?.State;
            if (districtOperation is not null)
            {
                publication = districtOperation.Latest();
                publication = PersonReturnDistrictTransferSource.PublishAreaAssignments(publication, draft,
                    districtOperation.ChangedAreas, districtOperation.ChangedGates);
                publication = PersonReturnDistrictReorganizationSource.PublishCurrent(publication, draft, districtOperation);
                publication = publication with { PersonnelCommands = draft.ReadOwnedPersonCommandManager() };
                districtTransferWrites = true;
            }
            if (publication?.ArmyReturnCaptiveContinuation is not null)
                publication = publication with { ArmyReturnCaptiveContinuation = null };
            return new(new(army, target, savedCommander, savedTarget, outcome, rulerOrder.ToArray(), effects.ToArray(), context,
                pool, stagedPool, homeAssignments,
                districtTransferWrites, transferAssociations.ToArray(), publication, captiveResult?.Receipt, districtOperation), null);

            int[] RegisteredPersons() => (draft.ReadRegisteredPersonPhysicalSlots()
                ?? throw Missing("tail_Person_registry")).Select(value => value ?? throw Missing("tail_Person_pointer")).ToArray();
            void RequireNoAssociates()
            {
                int location = army.PhysicalSlot is >= 1 and <= 500 ? army.PhysicalSlot.Value + 61 : 0;
                foreach (int person in RegisteredPersons())
                    if (Get(draft.ReadPersonStatus(person), "tail_associate_status") == 6 &&
                        Get(draft.ReadPersonSecondLocation(person), "tail_associate_E4") == location)
                        throw Missing("branch.nonempty_status6_Army_associates2F8F00");
            }
            void RequireEmpty(HexListHandle value)
            {
                if ((draft.ReadRegistryHead(new(value.Identity)) ?? throw Missing("tail_associate_head")).Identity.HasValue ||
                    Get(draft.ReadTemporaryHexListCount(value), "tail_associate_count") != 0)
                    throw Missing("branch.report_not_actual_empty_descriptor");
            }
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, "current.OrdinaryReturn_tail: " + error.Message); }
    }

    internal static GameState CommitSuspension(ArmyOrdinaryReturnSuspension value, bool borrowed)
    {
        value.Pool.CommitArmyReturnPool(value.DraftPool);
        value.Owner.CommitArmyReturnTransactionFields(value.DraftPool.ArmyReturnOwner);
        if (value.DistrictOperation is { } district)
            value.Owner.CommitArmyReturnDistrictOperationFields(value.DraftPool.ArmyReturnOwner, district);
        var state = OwnedArmyReturnCurrentFields.PublishCurrent(value.State, value.Owner);
        state = PersonAssignmentPrecallInputs.PublishCurrent(state, value.Owner);
        state = OwnedArmyLifecycleDetachedInputs.PublishCurrent(state, value.Owner);
        //No pending70 or receiver retirement has occurred at this boundary.
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with
            { Indexes = borrowed ? value.Pool.ProjectOwnedRoles() : value.Pool.ExportOwnedState() });
    }

    internal static void Commit(ArmyOrdinaryReturnPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        packet.Pool.CommitArmyReturnPool(packet.DraftPool);
        packet.Owner.CommitArmyReturnTransactionFields(packet.DraftPool.ArmyReturnOwner);
        if (packet.DistrictOperation is { } operation)
            packet.Owner.CommitArmyReturnDistrictOperationFields(packet.DraftPool.ArmyReturnOwner, operation);
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
    /// <summary>240DB0's current adapter, shared with the ordinary return
    /// caller. It neither captures another world nor publishes/exports a pool.</summary>
    internal static IPersonDistrictTransferState CreateCurrentPersonDistrictTransfer(
        SourceWorldContext current, List<PersonAssignmentPrecallResult> associations,
        Action<PersonAssignmentPrecallResult>? receivePrecall = null)
        => new Transfer(current, associations, unresolvedAsData: true, receivePrecall);

    private sealed class Transfer(SourceWorldContext state,
        List<PersonAssignmentPrecallResult> associations, bool unresolvedAsData = false,
        Action<PersonAssignmentPrecallResult>? receivePrecall = null) : IPersonDistrictTransferState
    {
        public byte? ReadPersonDistrictSlot(int person) => state.ReadPersonDistrictSlot(person);
        public ForceReference? ReadPersonForce(int person) => state.ReadPersonForce(person);
        public ForceReference? ReadRequestedDistrictForce(int district) => state.ReadRequestedDistrictForce(district);
        public void RemoveForcePersonAssociation(ForceReference force, int person)
        {
            //240DB0 uses the same real2783B0 R8=0/R9=1 arguments.
            var result = PersonAssignmentPrecallSource.Apply2783B0ForCapture(state, force, person);
            if (result.NativeReturn is null) throw Failure(result.UnresolvedOwner ?? "current.ruler_tail_2783B0");
            associations.Add(result);
            receivePrecall?.Invoke(result);
        }
        public int? ReadForcePhysicalSlot(ForceReference force) => state.ReadForcePhysicalSlot(force);
        public void WritePersonResetWord1FF(int person, ushort value) => state.WritePersonResetWord1FF(person, value);
        public void WritePersonResetByte1FE(int person, byte value) => state.WritePersonResetByte1FE(person, value);
        public void WritePersonDistrictSlot(int person, byte value) => state.WritePersonDistrictSlot(person, value);
        public byte? ReadDistrictForceWithoutValidity(int district) => state.ReadDistrictForceWithoutValidity(district);
        public ForceReference? ReadCurrentPlayerForce() => state.ReadCurrentPlayerForce();
        public void RefreshTransferService(int first, int second, int third)
        {
            if (first != 1 || second != 1 || third != 0) throw Failure("branch.unsupported_service_dispatch");
            if (state.TransferService is null) throw Failure("current.ruler_tail_service_owner");
            if (!TransferService.HasPersonnelMilestoneReached(new ServiceQueries(state))) return;
            var refresh = state.TransferServiceRefresher
                ?? throw Failure("current.personnel_milestone_service_writer");
            refresh(first, second, third);
        }
        private Exception Failure(string field) => unresolvedAsData
            ? new InvalidDataException(field) : Missing(field);
        private T Known<T>(T? value, string field) where T : struct => value ?? throw Failure(field);
    }
    private sealed class Eligibility(SourceWorldContext state) : ITransferServiceEligibilityQueries
    {
        public uint? ReadWorldTransferServiceFlags()
            => state.ReadWorldTransferServiceFlags();
        public TransferModeState? ReadTransferModeState()
            => state.TransferModeStateReader is { } reader ? reader() : state.ArmyReturnTransferMode;
    }
    private sealed class ServiceQueries(SourceWorldContext state) : ITransferServiceQueries
    {
        public bool? ReadTransferServiceEligibility() => TransferServiceEligibility.IsEligible(new Eligibility(state));
        public ForceReference? ReadCurrentPlayerForce() => state.ReadCurrentPlayerForce();
        public bool? ReadForceNativeValidity(ForceReference force) => state.ReadForceNativeValidity(force);
        public int? ReadForcePhysicalSlot(ForceReference force) => state.ReadForcePhysicalSlot(force);
        public TransferPersonReference? ReadTransferPersonTableReference(int slot) => state.ReadTransferPersonTableReference(slot);
        public bool? ReadPersonNativeValidity(int person) => state.ReadPersonNativeValidity(person);
        public byte? ReadPersonStatus(int person) => state.ReadPersonStatus(person);
        public byte? ReadPersonCurrentRawForceSlot(int person) => state.ReadPersonCurrentRawForceSlot(person);
    }
    private static T Get<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static MissingOwner Missing(string field) => new(field);
    private sealed class MissingOwner(string message) : Exception(message);
}
