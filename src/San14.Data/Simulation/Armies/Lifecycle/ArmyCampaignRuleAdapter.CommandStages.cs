using San14.Core;

namespace San14.Data;

public sealed record ArmyCommandStageCheck(int PhysicalSlot, int? UnitId, bool? NativeValid,
    byte? Action, byte? CommandControl);
public sealed record ArmyCommandStageResult(GameState? State,
    IReadOnlyList<ArmyCommandStageCheck> Checks, string? UnresolvedOwner)
{
    //Failure-only values observed by this traversal. No second restoration,
    //capture, branch evaluation or world serialization is performed.
    internal IReadOnlyDictionary<string, object?>? FailureData { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Stage3's actual registered false-report operation.</summary>
    public ArmyCommandStageResult TryAdvanceSpecialArmyActions(GameState state,
        NativeArmyProgressStageMoment moment) => TryAdvanceArmyCommandGate(state, moment, false);

    /// <summary>Stage4: inspect saved control before validity, then restore
    /// through the actual Object, cached117 and eligible-neighbor branches.
    /// One writable current owns every temporary, route and report.</summary>
    public ArmyCommandStageResult TryAdvanceArmyCommandRestoration(GameState state,
        NativeArmyProgressStageMoment moment) => TryAdvanceArmyCommandGate(state, moment, true);

    private ArmyCommandStageResult TryAdvanceArmyCommandGate(GameState state,
        NativeArmyProgressStageMoment moment, bool restoration)
    {
        byte stage = (byte)(restoration ? NativeArmyProgressStage.RestoreArmyCommands : NativeArmyProgressStage.SpecialArmyActions);
        if (moment.SchedulerStageIndex != stage || moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Army command stage requires its reached ordinary two-hour stage entry.");
        if (!restoration) return AdvanceFalseReportStage(state, moment);
        int? reachedPhysical = null, reachedUnit = null;
        byte? reachedControl = null, reachedAction = null;
        ushort? reachedTargetIdentity = null;
        string reachedOperation = "capture-current";
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            //Both233560 calls in2AE570/2C96E0 read this operation's real
            //pending70/settlement owners, never the startup-model lists.
            current.BindOwnedDestructionQueue(state.PendingCombatSettlements, state);
            var pool = current.ReadOwnedCurrentListPool() ?? throw CommandStageMissing("restoration_primary_pool");
            var registry = current.ReadRegistryReferences(RegistryKind.Army) ?? throw CommandStageMissing("manager60");
            //2E1AD0 retains this pointer before the registered-Army traversal.
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            var checks = new List<ArmyCommandStageCheck>();
            var effects = new List<CampaignEffectRequest>();
            var working = state;
            foreach (var entry in registry)
            {
                reachedOperation = "manager-entry";
                reachedPhysical = null; reachedUnit = null;
                reachedControl = null; reachedAction = null; reachedTargetIdentity = null;
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical } || physical is < 0 or > 500)
                    throw CommandStageMissing("manager_Army_pointer");
                reachedOperation = "registered-army";
                reachedPhysical = physical;
                var slot = working.ReadArmyLifecycle()!.ReceiverSlots()[physical];
                UnitState? image = slot.ActiveUnitId is int id
                    ? working.Units.GetValueOrDefault(id) ?? throw CommandStageMissing("active_Army_owner")
                    : slot.PendingImage;
                reachedUnit = image?.Id;
                //No native-validity gate precedes140. A complete receiver
                //command is authoritative; legacy scalar knowledge stays local.
                byte control = slot.SavedCommand?.Control ?? (image?.NativeCommandControl140 is byte known ? known : image is null
                    ? slot.CommandControl140 ?? throw CommandStageMissing("retired_Army_control140")
                    : throw CommandStageMissing("current_Army_control140"));
                reachedControl = control;
                if (control != 0)
                {
                    reachedOperation = "restore-current-command";
                    working = RestoreCurrentSavedCommand(working, current, slot, image, player, s => StatusCampaignDay(s, moment), effects,
                        ref reachedAction, ref reachedTargetIdentity);
                }
                checks.Add(new(physical, image?.Id, null, null, control));
            }
            //No per-Army export or capture: all nearby/route descriptors have
            //left their finally scopes before the one owning publication.
            reachedOperation = "export-owned-state";
            reachedPhysical = null; reachedUnit = null;
            reachedControl = null; reachedAction = null; reachedTargetIdentity = null;
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
            if (effects.Count != 0)
            {
                reachedOperation = "publish-effect-batch";
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw CommandStageMissing("restoration_effect_owner"))
                    .Append(CampaignEffectSource.ArmyCommandMaintenance, moment.CalendarCycleOrdinal, effects) };
            }
            return new(working, checks.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return Failed(error); }
        catch (ArmyRouteSearch.MissingOwner error) { return Failed(error); }

        ArmyCommandStageResult Failed(Exception error) => new(null, [], error.Message)
        { FailureData = new Dictionary<string, object?> {
            ["RestorationOperation"] = reachedOperation,
            ["RestorationLowerErrorType"] = error.GetType().FullName,
            ["RestorationPhysicalSlot"] = reachedPhysical,
            ["RestorationUnitId"] = reachedUnit,
            ["RestorationControl140"] = reachedControl,
            ["RestorationAction37"] = reachedAction,
            ["RestorationTargetIdentity3A"] = reachedTargetIdentity
        } };
    }

    private GameState RestoreCurrentSavedCommand(GameState state, SourceWorldContext current,
        ArmyLifecycleReceiverView slot, UnitState? image, ForceReference? player,
        Func<GameState, int> readCampaignDay, List<CampaignEffectRequest> effects,
        ref byte? reachedAction, ref ushort? reachedTargetIdentity)
    {
        if (image is null) throw CommandStageMissing("restoration_current_image");
        var army = new ArmyUnitReference(slot.PhysicalSlot);
        byte action = RestorationNeed(ArmyConfigurationSourceAdapter.ReadAction(state, image), "restoration_current_action37");
        reachedAction = action;
        var main = OwnedArmyLifecycleTargets.Read(state, slot.ReceiverId) ?? throw CommandStageMissing("restoration_current_target");
        ushort raw = RestorationNeed(main.Identity3A, "restoration_current_identity3A");
        reachedTargetIdentity = raw;
        //The source uses raw3A as a Hex lookup even before testing action1.
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw CommandStageMissing("restoration_target_Hex_pointer");
        ushort link = RestorationNeed(current.ReadHexObjectLink(hex), "restoration_target_Object_link");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw CommandStageMissing("restoration_Object_pointer");
        bool direct = action != 1, rescued = false;
        ArmyUnitReference? rescuer = null;
        if (!direct)
        {
            if (!RestorationNeed(current.ReadObjectNativeValidity(obj), "restoration_Object_validity")) direct = true;
            else
            {
                var fields = current.ReadArmyReturnTraitFields(army) ?? throw CommandStageMissing("restoration_cached36_owner");
                for (int index = 0; index < 36; index++)
                {
                    ushort key = fields.Traits64[index];
                    if (key is 0 or > 355) continue;
                    var trait = current.ReadPersonalityTableReference(key) ?? throw CommandStageMissing("restoration_cached_trait_pointer");
                    if (!RestorationNeed(current.ReadSkillPersonalityValidity(trait), "restoration_cached_trait_validity")) continue;
                    if (RestorationNeed(current.ReadPersonalityByte(trait, 0xB6), "restoration_cached_effect1") != 117 &&
                        RestorationNeed(current.ReadPersonalityByte(trait, 0xBA), "restoration_cached_effect2") != 117) continue;
                    rescued = true;
                    ushort ownerKey = fields.PersonsAC[index];
                    int owner = (ownerKey <= 6000 ? current.ReadPersonLookupReference(ownerKey)
                        : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
                        ?? throw CommandStageMissing("restoration_trait117_Person_pointer");
                    //22A200(Person,1) has no extra Person-validity gate.
                    rescuer = PersonArmyLocationSource.ReadCommanderArmy(current, owner)
                        ?? throw CommandStageMissing("restoration_trait117_CommanderArmy22A200");
                    break;
                }
                var nearby = current.CreateNearbyTemporary();
                try
                {
                    ushort at = RestorationNeed(current.ReadArmyUnitRawHex2A(army), "restoration_nearby_Army2A");
                    var center = current.ReadHexTableReference(at < 48400 ? at : 0)
                        ?? throw CommandStageMissing("restoration_nearby_center");
                    //The reached2AE570 includes native validity,233560 and
                    //2C96E0. The generic valid-only nearby query is different.
                    TrapCandidates.FillNearbyArmies(state, current, center, 1, nearby);
                    var node = current.ReadRegistryHead(new(nearby.Identity)) ?? throw CommandStageMissing("restoration_nearby_head");
                    while (node.Identity.HasValue)
                    {
                        var entry = current.ReadRegistryNodePayload(node) ?? throw CommandStageMissing("restoration_nearby_payload");
                        if (entry.Kind != RegistryKind.Army) throw CommandStageMissing("restoration_nearby_Army_kind");
                        var other = new ArmyUnitReference(entry.PhysicalSlot);
                        if (other != army && current.ReadArmyUnitForceReference(other) == current.ReadArmyUnitForceReference(army))
                        { rescued = true; rescuer = other; break; }
                        node = current.ReadRegistryNodeNext(node) ?? throw CommandStageMissing("restoration_nearby_next");
                    }
                }
                finally { current.ClearNearbyTemporary(nearby, true); }
            }
        }
        var working = state;
        if (direct || rescued)
        {
            bool valid = RestorationNeed(current.ReadArmyUnitNativeValidity(army), "restoration_Army_validity");
            if (valid)
            {
                var saved = slot.SavedCommand is { Kind: ArmySavedCommandKind.Stored, Command: not null } owner
                    ? owner.Command : throw CommandStageMissing("restoration_complete_saved_command");
                var arena = working.ReadArmyLifecycle()!;
                var target = ArmyTargetSourceAdapter.Encode(saved.Target, null, new(arena), current.CurrentMapObjectDirectory);
                byte targetKind = RestorationNeed(target.Kind38, "restoration_saved_target_kind");
                ushort targetId = saved.Action.Id == 13
                    ? RestorationNeed(current.ReadArmyUnitRawHex2A(army), "restoration_stay_current_Hex")
                    : RestorationNeed(target.Identity3A, "restoration_saved_target_identity");
                byte restoredAction = checked((byte)Math.Clamp(saved.Action.Id, 0, 17));
                image = image with { NativeCommandControl140 = 0,
                    FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(restoredAction),
                    //Stage3 already cleared the real five points and the plan's
                    //matching route. Restore its original terminal action only;
                    //never recover an old waypoint array or invent completion.
                    MilitaryPlan = restoredAction == 13 && image.MilitaryPlan is { Completed: true } held
                        ? held with { RestingHexId = targetId } : image.MilitaryPlan };
                working = OwnedArmyLifecycleTargets.Write(working, slot.ReceiverId, new(targetKind, targetId));
                current.SetArmyLifecycleTargetFields(slot.PhysicalSlot, new(targetKind, targetId));
                var lifecycle = working.ReadArmyLifecycle()!;
                var sources = lifecycle.Source.Slots.ToArray();
                sources[slot.PhysicalSlot] = sources[slot.PhysicalSlot] with { CommandControl140 = 0 };
                working = working.WithArmyLifecycle(lifecycle with { Source = lifecycle.Source with { Slots = sources },
                    Receivers = lifecycle.Receivers.With(lifecycle.Receivers.Find(slot.ReceiverId)! with
                        { SavedCommand = ArmySavedCommandState.Cleared }) });
                working = RestorationWriteImage(working, slot, image) with
                    { ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, image.Id, restoredAction) };
            }
            //25 alone decides whether to clear override/Next and call301DD0.
            //The five intermediate points are neither restored nor cleared.
            if ((image.NativeTacticRuntime ?? throw CommandStageMissing("restoration_abnormal_owner")).Abnormal25 == 0)
            {
                image = image with { OverrideTarget = ArmyOverrideTargetState.Cleared,
                    March = (image.March ?? throw CommandStageMissing("restoration_march")) with
                        { NextStep = ArmyMarchDestination.None } };
                working = RestorationWriteImage(working, slot, image);
                var overrideArena = working.ReadArmyLifecycle()!;
                working = working.WithArmyLifecycle(overrideArena with { Receivers = overrideArena.Receivers.With(
                    (overrideArena.Receivers.Find(slot.ReceiverId) ?? throw CommandStageMissing("restoration_override_receiver")) with
                        { OverrideTarget = ArmyOverrideTargetState.Cleared }) });
                working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, image);
                current.ClearArmyCommandOverride(slot.PhysicalSlot);
                if (slot.ActiveUnitId == image.Id) working = FRoute(working, image.Id, true, current, false);
                else
                {
                    //Invalid registered pending receivers retain140, and the
                    //native route returns before target/path writers. Do not
                    //turn a valid pending route into a substitute active Army.
                    var route = OrdinaryArmyRouteSource.Rewrite(working, current, image, true);
                    var receipt = route.Receipt ?? throw CommandStageMissing(route.UnresolvedOwner ?? "restoration_pending_route");
                    if (receipt.NextHexWrite.HasValue || receipt.CachedGoalWrite.HasValue)
                        throw CommandStageMissing("restoration_pending_route_writers");
                }
            }
        }
        var force = current.ReadArmyUnitForceReference(army);
        var retainedPlayer = player ?? throw CommandStageMissing("restoration_current_player_pointer");
        if (direct && force == retainedPlayer)
            working = RestorationReport(working, current, army, null, readCampaignDay, effects);
        else if (rescued)
        {
            byte objectForceRaw = RestorationNeed(current.ReadObjectForceSlot13(obj), "restoration_Object_Force13");
            var objectForce = current.ReadForceTableReference(objectForceRaw <= 51 ? objectForceRaw : 0)
                ?? throw CommandStageMissing("restoration_Object_Force_pointer");
            if (force == retainedPlayer || objectForce == retainedPlayer)
                working = RestorationReport(working, current,
                    rescuer ?? throw CommandStageMissing("restoration_rescuer_pointer"), army, readCampaignDay, effects);
        }
        if (rescued)
        {
            int commander = RestorationNeed(current.ReadArmyUnitCommanderId(army), "restoration_speech_Commander12");
            int at = RestorationNeed(current.ReadArmyUnitRawHex2A(army), "restoration_speech_Army2A");
            effects.Add(new(new CombatSpeechRequest(CombatParticipantKind.Army, image.Id, commander,
                CombatSpeechEvent.FalseReportArmyRescued, at, null, null, commander, null, null), null));
        }
        return working;
    }

    private GameState RestorationReport(GameState state, SourceWorldContext current,
        ArmyUnitReference reporter, ArmyUnitReference? victim, Func<GameState, int> readCampaignDay,
        List<CampaignEffectRequest> effects)
    {
        var formatted = FormatHistoryReport(state, current, victim is null ? 0xC96F : 0xC96E,
            new(Army0: reporter, Army1: victim));
        state = formatted.State;
        int commander = RestorationNeed(current.ReadArmyUnitCommanderId(reporter), "restoration_report_Commander12");
        int day = readCampaignDay(state);
        int at = RestorationNeed(current.ReadArmyUnitRawHex2A(reporter), "restoration_report_Army2A");
        int district = ReadFacilityHistoryDistrict(current, reporter);
        return WriteHistory(state, current, new(5, district, at, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), commander, MarkBit4: 1), effects);
    }

    private static GameState RestorationWriteImage(GameState state, ArmyLifecycleReceiverView slot, UnitState image)
    {
        var arena = state.ReadArmyLifecycle() ?? throw CommandStageMissing("restoration_image_lifecycle");
        if (slot.ActiveUnitId is int id)
            return state with { Units = new(state.Units) { [id] = image } };
        return state.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(
            (arena.Receivers.Find(slot.ReceiverId) ?? throw CommandStageMissing("restoration_image_receiver")) with { PendingImage = image }) });
    }
    private static T RestorationNeed<T>(T? value, string field) where T : struct => value ?? throw CommandStageMissing(field);
    private static InvalidDataException CommandStageMissing(string owner) => new("army.command_stage." + owner);
}
