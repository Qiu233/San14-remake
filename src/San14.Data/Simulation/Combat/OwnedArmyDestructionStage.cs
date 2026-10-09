using San14.Core;

namespace San14.Data;

public sealed record OwnedArmyDestructionReceipt(int PhysicalSlot, int UnitId, int CommanderId, int ForceId,
    int PreviousHex, int HomeCity, PersonArmyInjuryReceipt? Injury, IReadOnlyList<string> SourceEffectOrder)
{
    public int NativeReportKind => 14; //25B850→1B4AB0, ordinary nondead Person.
    public int? WinnerUnitId { get; init; }
    public int? WinnerCommanderId { get; init; }
    public int? WinnerBaseLocation { get; init; }
    public int? NativeWinnerReportKind => WinnerUnitId is null ? null : 10;
    public PersonNormalArmyRewardReceipt? WinnerReward { get; init; }
    public int? WinnerForce164Gain { get; init; }
    public PersonArmyCaptureReceipt? Capture { get; init; }
    public RulerCaptureReleaseReceipt? RulerRelease { get; init; }
    public PersonArmyCaptureWholeReceipt? Custody { get; init; }
    public ArmyDestructionTrackingWrite[] TrackingWrites { get; init; } = [];
    public PeriodReportEntry Report => new("field_destroyed", ForceId, HomeCity, CommanderId, 0, PreviousHex, PreviousHex)
        { RuleId = OwnedArmyDestructionStage.Revision };
}
public sealed record OwnedArmyDestructionPhaseResult(GameState? State,
    IReadOnlyList<OwnedArmyDestructionReceipt> Receipts, string? UnresolvedOwner)
{
    public OwnedGateCaptureReceipt[] BaseReceipts { get; init; } = [];
    public OwnedCityCaptureReceipt[] CityReceipts { get; init; } = [];
    public PeriodReportEntry[]? SourceReports { get; init; }
    public IReadOnlyList<PeriodReportEntry> Reports => State is null ? [] :
        SourceReports ?? [.. Receipts.Select(x => x.Report), .. BaseReceipts.SelectMany(x => x.Reports), .. CityReceipts.SelectMany(x => x.Reports)];
}

/// <summary>450830's current request traversal and the ordinary259360 branch
/// with an ordinary employed Person and its still friendly original home. Supports a
/// valid winner Army or ordinary City winner and actual remote Return command creation. This is
/// destruction, not29F220 city return.
/// The separate26th phase owns282580 cleanup; no live zero-soldier projection
/// is published. A captured ruler's actual AI release early-return is supported;
/// ordinary status4 custody has its real source callbacks; other capture and
/// death branches remain explicit unknown.</summary>
public sealed partial class OwnedArmyDestructionStage(ArmyCampaignRuleAdapter package)
{
    public const string Revision = "san14:ce-owned-ordinary-Army-destruction-20261007";
    public OwnedArmyDestructionPhaseResult TryConsumePhase19(GameState state,
        TacticSourceSamplerWorld? sampler = null, NativeMapSeasonDate? captureWorld38Date = null)
    {
        var receipts = new List<OwnedArmyDestructionReceipt>();
        var captures = new List<OwnedGateCaptureReceipt>();
        var cityCaptures = new List<OwnedCityCaptureReceipt>();
        var reports = new List<PeriodReportEntry>();
        //The legacy parameter name describes one consumer of World34's final
        //byte38. The same actual pre-writer ThirdDate also owns Return dates.
        var sourceDate = captureWorld38Date ?? state.NativeMapSeason?.ThirdDate;
        if (state.PendingCombatSettlements is not { } queue)
            return new(null, receipts, "current.manager140_destruction_queue");
        try
        {
            var world = package.CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var working = state;
            var destroyed = new HashSet<int>();
            // Read the current queue after every callback. A callback can append
            // another request which must settle in this same phase.
            for (int cursor = 0; cursor < queue.Requests.Count; cursor++)
            {
                current.BindOwnedDestructionQueue(queue, working);
                var request = queue.Requests[cursor];
                if (request is ExpiredCombatSettlement) continue;
                if (request is BaseCaptureRequest capture)
                {
                    var target = ArmyDestructionQueue.ResolveBaseReference(working, current, capture.Target);
                    if (!Known(current.ReadBaseNativeValidity(target), "request_Base_validity")) continue;
                    working = PublishCurrent(working, current, world.Pool, destroyed, queue);
                    if (target.Kind == BaseKind.Gate)
                    {
                        var captured = new OwnedGateCaptureStage(package).TryApply(working, capture, sourceDate, sampler);
                        working = captured.State ?? throw Missing(captured.UnresolvedOwner ?? "Gate2851B0");
                        captures.Add(captured.Receipt!); reports.AddRange(captured.Receipt!.Reports);
                    }
                    else
                    {
                        var captured = new OwnedCityCaptureStage(package).TryApply(working, capture, sourceDate, sampler);
                        working = captured.State ?? throw Missing(captured.UnresolvedOwner ?? "City2851B0");
                        cityCaptures.Add(captured.Receipt!); reports.AddRange(captured.Receipt!.Reports);
                    }
                    world = package.CaptureOwnedArmyReturnWorld(working); current = world.Context;
                    destroyed.Clear(); queue = current.OwnedDestructionQueue ?? throw Missing("current.Base_postcallback_settlements");
                    continue;
                }
                var defeat = (UnitDefeatRequest)request;
                if (ArmyDestructionQueue.ResolveUnitSlot(working, defeat.VictimUnitId) is not int physical) continue;
                if (current.ReadArmyUnitNativeValidity(new(physical)) is not bool valid) throw Missing("current.victim_validity");
                if (!valid || ArmyPending70Admission.ContainsCurrentPointer(new(RegistryKind.Army, physical), new Pending(current))) continue;
                var receipt = DestroyCurrent(working, current, world.Pool, defeat, physical,
                    sampler ?? package.ReadArmyDestructionSampler(working, current), sourceDate);
                working = receipt.State;
                destroyed.Add(physical); receipts.Add(receipt.Receipt); reports.Add(receipt.Receipt.Report);
                queue = current.OwnedDestructionQueue ?? throw Missing("current.operation_settlements");
            }
            queue = CombatSettlementQueue.Create();
            current.BindOwnedDestructionQueue(queue, working);
            var result = PublishCurrent(working, current, world.Pool, destroyed, queue);
            ArmyLifecycleSaveValidation.Validate(result);
            return new(result, receipts, null) { BaseReceipts = captures.ToArray(), CityReceipts = cityCaptures.ToArray(), SourceReports = reports.ToArray() };
        }
        catch (InvalidDataException error) { return new(null, receipts, error.Message); }
    }

    private (GameState State, OwnedArmyDestructionReceipt Receipt) DestroyCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        UnitDefeatRequest request, int physical, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate? sourceDate)
    {
        if (request.WinnerUnitId == request.VictimUnitId)
            throw Missing("branch.ordinary_null_winner_mode1_cause0");
        var arena = state.ReadArmyLifecycle()!;
        var slot = arena.ReceiverSlots()[physical];
        if (slot.ActiveUnitId is not int id || !state.Units.TryGetValue(id, out var army) || army.Troops != 0 ||
            army.NativeArmyConfiguration is null || army.NativeTacticRuntime is null)
            throw Missing("current.actual_zero_ready_Army_image");
        var victim = new ArmyUnitReference(physical);
        var winner = new ArmyUnitReference(request.WinnerUnitId is int winnerIdForMapping
            ? ArmyDestructionQueue.ResolveUnitSlot(state, winnerIdForMapping) : null);
        bool winnerValid = Known(current.ReadArmyUnitNativeValidity(winner), "winner_Army_validity");
        if (!winnerValid) winner = new(null);
        var winnerBase = ResolveWinnerBase(state, current, request.WinnerBase, winnerValid);
        bool winnerBaseValid = Known(current.ReadBaseNativeValidity(winnerBase), "winner_Base_validity");
        int? winnerPhysical = winner.PhysicalSlot;
        int? winnerCommander = winnerValid ? Known(current.ReadArmyUnitCommanderId(winner), "winner_Commander12") : null;
        var winnerPerson = winnerCommander is int winnerId
            ? (winnerId <= 6000 ? current.ReadPersonLookupReference(winnerId) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("current.winner_Commander_pointer")
            : new TransferPersonReference(null);
        ushort commanderId = Known(current.ReadArmyUnitCommanderId(victim), "Army12");
        int commander = (commanderId <= 6000 ? current.ReadPersonLookupReference(commanderId) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw Missing("current.Commander_pointer");
        if (!state.Officers.TryGetValue(commanderId, out var person) || current.ReadPersonNativeValidity(commander) != true ||
            current.ReadPersonStatus(commander) is not (>= 1 and <= 4))
            throw Missing("branch.ordinary_employed_Commander");
        var force = current.ReadArmyUnitForceReference(victim);
        if (force.PhysicalSlot is not (>= 1 and <= 45) || current.ReadForceNativeValidity(force) != true ||
            current.ReadPersonStatus(commander) == 1 && current.ReadForceRecord(force.PhysicalSlot.Value)?.RawRuler10 != commanderId)
            throw Missing("branch.ordinary_current_Force");
        var captured = ArmyDestructionResources.CaptureOwners(current, victim, winner, winnerBase);
        if (captured.VictimHome is not { Kind: BaseKind.City, PhysicalSlot: int home } ||
            current.ReadBaseNativeValidity(captured.VictimHome) != true || current.ReadBaseForceSlot(captured.VictimHome) != force.PhysicalSlot)
            throw Missing("branch.same_friendly_original_home");
        int hex = Known(current.ReadArmyUnitRawHex2A(victim), "Army2A");
        bool atHome = current.ReadBaseCurrentHexIndex(captured.VictimHome) == hex;
        var effects = new List<string>();
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 1, true)) throw Missing("current.Army150");
        effects.Add("25944F.2409E0_Army150_bit1");
        //2C8DA0 is called before any cached trait cleanup, even when the later
        //known-null winner branch does not need capture immunity.
        int captureImmunity = CachedEffect(current, victim, 0x36);
        var cleanup = ArmyReturnTraitCleanup.ApplyCurrent(victim, current, pool, new(false, false));
        if (cleanup.Packet is null) throw Missing(cleanup.UnresolvedOwner ?? "current.trait_cleanup");
        effects.Add("259526.nine2A2340_then36_2A2540");
        bool immune = PersonalityImmunity(state, current, commander);
        effects.Add("2595A0.2307D0_Personality_temporary");
        bool protectedNearby = NearbyProtection(state, current, victim, force, sourceDate);
        effects.Add("259729.2A78D0_protector_temporary");
        PersonArmyInjuryReceipt? injuryReceipt = null;
        if (!immune && !protectedNearby)
        {
            var injury = PersonArmyInjurySource.Apply(state, current, commander, winnerPerson, package.CreateArmyDestructionAbilities(current), sampler);
            if (injury.NativeReturn is null) throw Missing(injury.UnresolvedOwner ?? "current.27D900");
            injuryReceipt = injury.Receipt;
        }
        effects.Add("259C32.ordinary27D900_healthy_current");
        var resource = ArmyDestructionResources.Prepare(state, current, victim, winner, winnerBase, captured);
        if (resource.Packet is null) throw Missing(resource.UnresolvedOwner ?? "current.resources259360");
        ArmyDestructionResources.ApplyContext(resource.Packet);
        state = ArmyDestructionResources.PublishResources(state, resource.Packet);
        effects.Add("259Fxx.259360_resource_writes");
        //Loyalty implementation is supplied below once its real source inputs
        //have closed. It is not bypassed merely because the Person is ruler.
        state = package.ApplyArmyDestructionLoyalty(state, current, commander, force);
        effects.Add("25A834.Person_E8_loyalty_writer");
        var wave = ArmyDestructionMoraleWave.Apply(state, current, victim,
            new(new(commander), force, captured.WinnerForce, false), winner);
        if (wave.EffectCount is null) throw Missing(wave.UnresolvedOwner ?? "current.ruler_morale_wave");
        effects.Add("25B1xx.current_rank_office_morale_wave");
        PersonArmyCaptureReceipt? captureReceipt = null;
        RulerCaptureReleaseReceipt? releaseReceipt = null;
        PersonArmyCaptureWholeReceipt? custodyReceipt = null;
        bool returnedByCapture = false;
        if (captureImmunity == 0 && winnerValid)
        {
            var capture = PersonArmyCaptureSource.Evaluate(state, current, commander, winnerPerson,
                package.CreateArmyDestructionAbilities(current), sourceDate, sampler, new(false));
            if (capture.Captured is not bool capturedPerson) throw Missing(capture.UnresolvedOwner ?? "current.27E870_capture");
            captureReceipt = capture.Receipt;
            effects.Add("25B75x.27E870_capture_predicate");
            if (capturedPerson)
            {
                //2E0FC0's actual employed ruler/Army-position branch calls
                //2F86F0 and returns1 BEFORE task/appointment/District custody
                //callbacks. A real AI release can therefore close this early
                //return without pretending that normal captivity has run.
                bool ordinaryPerson = Known(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(commander)), "captured_Person15E");
                if (!ordinaryPerson)
                    throw Missing("branch.2E0FC0_captured_Person_custody");
                if (current.ReadPersonStatus(commander) == 1)
                {
                    if (PersonPositionRepair.DecodeSecondLocation(Known(current.ReadPersonSecondLocation(commander), "captured_PersonE4")).Kind != 27)
                        throw Missing("branch.2E0FC0_captured_Person_custody");
                    var decision = RulerCaptureReleaseSource.Decide(state, current, commander, captured.WinnerForce, sampler);
                    if (decision.Decision != RulerCaptureDecision.Release)
                        throw Missing(decision.UnresolvedOwner ?? (decision.Decision == RulerCaptureDecision.PlayerDecisionRequired
                            ? "branch.Lord_player_806D00_decision" : "branch.Lord_execute_27F820_295EE0"));
                    var released = RulerCaptureReleaseSource.Release(state, current, decision,
                        sourceDate ?? throw Missing("current.Lord_return_World34_date"), new(false));
                    state = released.State ?? throw Missing(released.UnresolvedOwner ?? "current.Lord_release");
                    releaseReceipt = released.Receipt; returnedByCapture = true;
                    effects.Add("25B75x.2E0FC0_2F86F0_Lord_AI_release_early_return");
                }
                else
                {
                    var custody = package.ApplyOrdinaryWinnerArmyCapture(state, current, pool, commander, winner);
                    state = custody.State ?? throw Missing(custody.UnresolvedOwner ?? "current.2E0FC0_ordinary_custody");
                    custodyReceipt = custody.Receipt; returnedByCapture = true;
                    effects.Add("25B75x.2E0FC0_ordinary_capture_callbacks_and_custody");
                }
            }
        }
        if (!returnedByCapture && atHome)
        {
            current.WritePersonSecondLocation(commander, Math.Min(Known(current.ReadBaseObjectLink(captured.VictimHome), "Base4E"), (ushort)1061));
            effects.Add("25B7CE.2EA9A0_same_home_secondE4");
        }
        else if (!returnedByCapture)
        {
            //The actual remote source creates its Return receiver before the
            //Army enters pending70. Its current second-location still points
            //to the live victim and supplies Army2A to231670; do not first
            //replace the Person location with its home.
            var date = sourceDate ?? throw Missing("current.remote_return_World34_date");
            var prepared = PersonReturnCommands.Prepare(state, current, commander, date);
            if (prepared.Packet is not { } returnPacket) throw Missing(prepared.UnresolvedOwner ?? "current.remote_return_prepare");
            var returned = PersonReturnCommands.Apply(state, current, returnPacket);
            state = returned.State ?? throw Missing(returned.UnresolvedOwner ?? "current.remote_return_create");
            effects.Add("25B7CE.2EA9A0_1F24F0_1F68B0_remote_return_command");
        }
        effects.Add("25B850.1B4AB0_report_kind14");
        RequireNoCaptives(current, physical);
        effects.Add("25B89x.230C70_captive_temporary_empty");
        var tracking = ArmyDestructionTrackingTail.ApplyCurrent(state, current, physical);
        state = tracking.State;
        NearbyPersonalityTail(state, current, victim, force, captured.WinnerForce);
        effects.Add("25BAxx.current_manager60_and_nearby_tail");
        var originalHex = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("current.original_Hex_pointer");
        if (originalHex.PhysicalSlot is int actualHex) current.SetHexRaw1C(actualHex, 0);
        effects.Add("25C171.226870_Hex1C_clear_before_pending");
        var admitted = ArmyPending70Admission.EnqueueCurrentPointer(new(RegistryKind.Army, physical), new Pending(current));
        if (admitted == ArmyPending70AdmissionOutcome.KnownAllocationFailure) throw Missing("branch.pending70_allocation_failure");
        effects.Add("25C180.31F370_pending70");
        PersonNormalArmyRewardReceipt? winnerReward = null;
        int? forceGain = null;
        int? winnerUnitId = null;
        if (winnerPhysical is int winnerSlot)
        {
            winnerUnitId = state.ReadArmyLifecycle()!.ReceiverSlots()[winnerSlot].ActiveUnitId;
            //The original victim has entered pending70 before2B9290. Publish
            //that current image only to this enclosing operation's draft;
            //phase26 still owns header reset/unregistration.
            state = PublishCurrent(state, current, pool, [physical], current.OwnedDestructionQueue!);
            var reward = package.TryApplyNativeNormalPersonRewardDuringArmyOperation(state, winnerCommander!.Value, 2, 0);
            if (reward.Outcome == PersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
                throw Missing("current.winner_2B9290:" + reward.MissingPhase);
            state = reward.State; winnerReward = reward.Receipt;
            effects.Add("25C1xx.2B9290_winner_kind2_reward");
        }
        if (winnerPhysical is not null || winnerBaseValid)
        {
            var experience = ArmyDestructionForce164.Prepare(state, current, victim, winner, winnerBase);
            if (experience.Packet is not { } experiencePacket) throw Missing(experience.UnresolvedOwner ?? "current.winner_Force164");
            ArmyDestructionForce164.ApplyContext(experiencePacket);
            state = ArmyDestructionForce164.PublishCurrent(state, experiencePacket);
            forceGain = experiencePacket.RequestedGain;
            effects.Add("25C2xx.258A60_winner_Force164");
        }
        if (winnerPhysical is int winnerTargetSlot)
        {
            var target = current.ReadArmyTargetState(winnerTargetSlot) ?? throw Missing("current.winner_Target38");
            if (Known(target.Kind, "winner_Target38") == ArmyTargetKind.ArmyReceiver &&
                current.TargetsArmyReceiver(target.Destination ?? throw Missing("current.winner_Target3A"), physical))
                if (!OwnedArmyDestructorFlags.SetSourceBit(current, winnerTargetSlot, 2, true)) throw Missing("current.winner_Army150");
            effects.Add("25C3xx.winner_Target38_3A_flag2");
        }
        foreach (var other in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
        {
            int index = Known(other.PhysicalSlot, "target_clear_Army");
            if (!Known(current.ReadArmyUnitNativeValidity(new(index)), "target_clear_Army_validity")) continue;
            var saved = state.ReadArmyLifecycle()!.ReceiverSlots()[index];
            var image = saved.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : saved.PendingImage;
            if (Known(image?.NativeArmyConfiguration?.Action37, "target_clear_Army37") != 7) continue;
            var target = current.ReadArmyTargetState(index) ?? throw Missing("current.target_clear_Army38");
            if (Known(target.Kind, "target_clear_Army38") == ArmyTargetKind.ArmyReceiver &&
                current.TargetsArmyReceiver(target.Destination ?? throw Missing("current.target_clear_Army3A"), physical))
                current.ClearArmyTargetToPlaceholder(index);
        }
        effects.Add("25C4xx.current_manager60_target_clear");
        return (state, new(physical, id, commanderId, army.ForceId, hex, home, injuryReceipt, effects)
            { WinnerUnitId = winnerUnitId, WinnerCommanderId = winnerCommander,
                WinnerBaseLocation = winnerBaseValid ? Known(current.ReadBaseObjectLink(winnerBase), "winner_Base4E") : null,
                WinnerReward = winnerReward,
                WinnerForce164Gain = forceGain, Capture = captureReceipt, RulerRelease = releaseReceipt, Custody = custodyReceipt,
                TrackingWrites = tracking.Writes });
    }

    private static GameState PublishCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, HashSet<int> destroyed, CombatSettlementQueue queue)
    {
        var result = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
        result = OwnedArmyLifecycleDetachedInputs.PublishCurrent(result, current);
        var arena = result.ReadArmyLifecycle()!;
        var units = new Dictionary<int, UnitState>(result.Units);
        var people = new Dictionary<int, OfficerState>(result.Officers);
        var slots = arena.ReceiverSlots().ToArray();
        for (int i = 0; i < slots.Length; i++)
        {
            var old = slots[i];
            var image = old.ActiveUnitId is int active ? units.GetValueOrDefault(active) : old.PendingImage;
            var traits = current.ReadArmyReturnTraitFields(new(i));
            if (image is not null && traits is not null)
                image = image with { NativeArmyConfiguration = image.NativeArmyConfiguration! with { TraitSlots64 = traits.Traits64.ToArray() } };
            if (image is not null && current.ReadArmyReturnResourceFields(new(i)) is { } resources)
                image = image with { NativeMorale1A = resources.Morale1A, WoundedTroops = resources.Wounded18 };
            if (destroyed.Contains(i))
            {
                if (image is null || old.ActiveUnitId is null && old.PendingImage is null) throw Missing("current.destroyed_Army_projection");
                if (old.ActiveUnitId is int id) units.Remove(id);
                people[image.OfficerId] = people[image.OfficerId] with { Deployed = false };
                slots[i] = old with { ActiveUnitId = null, PendingImage = image, AuxiliaryAC = traits?.PersonsAC.ToArray(), AuxiliaryF4 = traits?.AuxiliaryF4.ToArray() };
            }
            else if (image is not null)
            {
                if (old.ActiveUnitId is int id) units[id] = image;
                slots[i] = old with { PendingImage = old.PendingImage is null ? null : image,
                    AuxiliaryAC = traits?.PersonsAC.ToArray() ?? old.AuxiliaryAC, AuxiliaryF4 = traits?.AuxiliaryF4.ToArray() ?? old.AuxiliaryF4 };
            }
        }
        arena = OwnedArmyDestructorFlags.PublishCurrent(arena, current).WithReceiverSlots(slots)
            with { Indexes = pool.ExportOwnedState() };
        return OwnedArmyLifecycleTargets.PublishCurrent((result with { Units = units,
                Officers = people,
                ArmyMapOccupancy = current.ExportArmyMapOccupancy(),
                PendingCombatSettlements = queue,
                PersonnelReturns = current.ReadOwnedPersonCommandManager() }).WithArmyLifecycle(arena), current);
    }

    private static bool PersonalityImmunity(GameState state, SourceWorldContext current, int commander)
    {
        var temporary = current.CreatePersonInjuryTemporary();
        bool immune = false;
        try
        {
            current.ClearPersonInjuryTemporary(temporary, false);
            for (int i = 0; i < 9; i++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, commander, i) ?? throw Missing("current.230730_trait_pointer");
                if (Known(current.ReadSkillPersonalityValidity(trait), "Personality_validity")) current.AppendPersonInjuryTemporary(temporary, trait);
            }
            var node = current.ReadSkillTemporaryHead(temporary) ?? throw Missing("current.Personality_temporary_head");
            while (node.Identity is not null)
            {
                var trait = current.ReadSkillTemporaryPayload(node) ?? throw Missing("current.Personality_temporary_payload");
                if (FirstEffect(current, trait, 0x35) > 0 && Radius(state, current, trait) == 0) immune = true;
                node = current.ReadHexListNodeNext(node) ?? throw Missing("current.Personality_temporary_next");
            }
        }
        finally { current.ClearPersonInjuryTemporary(temporary, true); }
        return immune;
    }
    private static void RequireNoCaptives(SourceWorldContext current, int army)
    {
        var temporary = current.CreateNearbyTemporary();
        try
        {
            current.ClearNearbyTemporary(temporary, false);
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person) ?? throw Missing("current.Person100_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("current.Person100_head");
            while (node.Identity is not null)
            {
                var person = current.ReadRegistryNodePayload(node) ?? throw Missing("current.Person100_payload");
                if (person.Kind != RegistryKind.Person || person.PhysicalSlot is not int physical) throw Missing("current.Person100_typed_pointer");
                if (Known(current.ReadPersonStatus(physical), "PersonE6") == 6 && Known(current.ReadPersonSecondLocation(physical), "PersonE4") == army + 61)
                {
                    var appended = current.AllocateRegistryNode(new(temporary.Identity)) ?? throw Missing("current.230C70_node");
                    if (appended.Identity is not null) current.WriteRegistryNodePayload(appended, person);
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.Person100_next");
            }
            if (Known(current.ReadTemporaryHexListCount(temporary), "230C70_count") != 0) throw Missing("branch.captive_release2E7890");
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
    }
    private static int CachedEffect(SourceWorldContext current, ArmyUnitReference army, byte effect)
    {
        var fields = current.ReadArmyReturnTraitFields(army) ?? throw Missing("current.Army_cached36");
        foreach (ushort key in fields.Traits64)
        {
            if (key is < 1 or > 355) continue;
            var trait = current.ReadPersonalityTableReference(key) ?? throw Missing("current.Personality_pointer");
            if (!Known(current.ReadSkillPersonalityValidity(trait), "Personality_validity")) continue;
            if (Known(current.ReadPersonalityByte(trait, 0xB6), "PersonalityB6") == effect || Known(current.ReadPersonalityByte(trait, 0xBA), "PersonalityBA") == effect) return 1;
        }
        return 0;
    }
    private static int FirstEffect(SourceWorldContext current, PersonalityReference trait, byte effect)
    {
        foreach (int at in new[] { 0xB6, 0xBA })
            if (Known(current.ReadPersonalityByte(trait, at), "Personality_effect_code") == effect)
                return Math.Clamp((int)unchecked((short)Known(current.ReadPersonalityWord(trait, at + 2), "Personality_effect_value")), -100, 2000);
        return 0;
    }
    private static int Radius(GameState state, SourceWorldContext current, PersonalityReference trait)
    {
        byte kind = Known(current.ReadPersonalityByte(trait, 0xA4), "Personality_categoryA4");
        if (kind is 2 or 3 or 0x17 or 0x18) return 1;
        if (kind is not (9 or 10 or 0x15 or 0x16 or 0x1F or 0x20 or 0x27 or 0x28)) return 0;
        if (trait.PhysicalSlot is >= 256 and <= 355)
        {
            int value = 0;
            for (int i = 0; i < 4; i++) value |= Known(current.ReadPersonalityByte(trait, 0xA8 + i), "Personality_radiusA8") << (i * 8);
            if (value >= 1) return value;
        }
        return Known(ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius), "Personality_default_radius");
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing("current." + field);
    private static InvalidDataException Missing(string field) => new(field);
    private sealed class Pending(SourceWorldContext current) : IArmyPending70AdmissionState
    {
        public RegistryListHandle? ReadPending70Descriptor() => current.ReadArmyReturnPending70Descriptor();
        public RegistryListNodeReference? ReadPending70Head(RegistryListHandle descriptor) => current.ReadRegistryHead(descriptor);
        public RegistryObjectReference? ReadPending70Payload(RegistryListNodeReference node) => current.ReadRegistryNodePayload(node);
        public RegistryListNodeReference? ReadPending70Next(RegistryListNodeReference node) => current.ReadRegistryNodeNext(node);
        public bool? ReadPending70LockEnabled() => current.ReadRegistryLockEnabled();
        public void EnterPending70Lock() => current.EnterRegistryLock();
        public void ExitPending70Lock() => current.ExitRegistryLock();
        public RegistryListNodeReference? AppendPending70Node(RegistryListHandle descriptor) => current.AllocateRegistryNode(descriptor);
        public void WritePending70Payload(RegistryListNodeReference node, RegistryObjectReference receiver) => current.WriteRegistryNodePayload(node, receiver);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal IEffectiveOfficerAbilities CreateArmyDestructionAbilities(SourceWorldContext current) =>
        new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
    internal TacticSourceSamplerWorld? ReadArmyDestructionSampler(GameState state, SourceWorldContext current) =>
        Context(current).ReadCurrentSamplerWorld(state);
    internal GameState ApplyArmyDestructionLoyalty(GameState state, SourceWorldContext current,
        int personPhysical, ForceReference capturedVictimForce)
    {
        var result = ArmyDestructionLoyalty.Prepare(state, current, personPhysical, _governorInputs, capturedVictimForce);
        if (result.Packet is not { } packet) throw new InvalidDataException(result.UnresolvedOwner ?? "current.destruction_loyalty");
        ArmyDestructionLoyalty.ApplyContext(packet);
        return ArmyDestructionLoyalty.PublishCurrent(state, packet);
    }
}
