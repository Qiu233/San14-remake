using San14.Core;
namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyFalseReportCommandRules
{
    public CommandResult IssueFalseReport(GameState state, UnitState actor, UnitState target, int destinationHexId)
        => IssueFalseReports(state, [actor], target, destinationHexId, []);

    private ArmyCommandStageResult AdvanceFalseReportStage(GameState state, NativeArmyProgressStageMoment moment)
    {
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var registry = current.ReadRegistryReferences(RegistryKind.Army) ?? throw FM("manager60");
            var checks = new List<ArmyCommandStageCheck>();
            var candidates = new List<(int Physical, int Score)>();
            foreach (var entry in registry)
            {
                int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int slot } ? slot : throw FM("manager_pointer");
                bool valid = FK(current.ReadArmyUnitNativeValidity(new(physical)), "native_validity");
                if (!valid) { checks.Add(new(physical, null, false, null, null)); continue; }
                var actor = FArmy(state, physical);
                byte action = FK(ArmyConfigurationSourceAdapter.ReadAction(state, actor), "action");
                checks.Add(new(physical, actor.Id, true, action, null));
                if (action != 17) continue;
                var target = FTarget(state, physical);
                candidates.Add((physical, FTrial(state, current, actor, target, moment).Score));
            }
            //24A270 preserves native comparator calls, including its >32 partition/heap branches.
            FSort(candidates, Before);
            var working = state;
            foreach (var row in candidates) working = FApply(working, row.Physical, moment);
            return new(working, checks, null);

            bool Before((int Physical, int Score) left, (int Physical, int Score) right)
            {
                if (left.Score != right.Score) return left.Score > right.Score;
                var a = FArmy(state, left.Physical); var b = FArmy(state, right.Physical);
                int ai = FIntelligence(state, current, a), bi = FIntelligence(state, current, b);
                return ai != bi ? ai > bi :
                    FK(current.ReadArmyUnitCommanderId(new(left.Physical)), "sort_Commander12") <
                    FK(current.ReadArmyUnitCommanderId(new(right.Physical)), "sort_Commander12");
            }
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private (int Score, bool Success) FTrial(GameState state, SourceWorldContext current, UnitState actor,
        UnitState target, NativeArmyProgressStageMoment moment)
    {
        if (!FK(current.ReadArmyUnitNativeValidity(new(FPhysical(actor))), "trial_actor_validity") ||
            !FK(current.ReadArmyUnitNativeValidity(new(FPhysical(target))), "trial_target_validity")) return (0, false);
        if (FK(TacticRemakeCurrentBattleControl.Read(state, 53), "tutorial53")) return (100, true);
        int person = FPerson(current, FK(current.ReadArmyUnitCommanderId(new(FPhysical(actor))), "trial_actor_Commander12")),
            defender = FPerson(current, FK(current.ReadArmyUnitCommanderId(new(FPhysical(target))), "trial_target_Commander12"));
        int attack = FIntelligence(state, current, actor), defense = FIntelligence(state, current, target);
        if (defense >= 100) return (0, false);
        if (FK(TacticRemakeCurrentTraits.CommanderHasEffect(current, defender, 115), "trait115")) return (0, false);
        int[] traits = TacticRemakeCurrentTraits.ArmyContributions(current, person,
            current.ReadArmyReturnTraitFields(new(FPhysical(actor)))?.Traits64) ?? throw FM("trait118");
        int rate = FInt((float)unchecked(attack - defense) * ((float)FP(state, "success-intelligence-percent") / 100f) + traits[118]);
        rate = Math.Max(rate, FP(state, "success-minimum")); rate = Math.Min(rate, FP(state, "success-maximum"));
        if (FK(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "settings_selector"))
            rate = Math.Min(unchecked(rate + FK(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 38), "setting38") / 100), 100);
        int sample = TacticEffectSourceSamplerMath.Sample(100, 1, FHex(state, target), FHex(state, actor), FK(current.ReadPersonIdentity(person), "sampler_commander_identity"), 0,
            FSampler(state, moment));
        return (Math.Clamp(rate, 0, 100), sample <= rate);
    }

    private GameState FApply(GameState state, int physical, NativeArmyProgressStageMoment moment)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        if (!FK(current.ReadArmyUnitNativeValidity(new(physical)), "application_actor_validity")) return state;
        var actor = FArmy(state, physical);
        if (FK(ArmyConfigurationSourceAdapter.ReadAction(state, actor), "application_action") != 17) return state;
        var target = FTarget(state, physical);
        int targetPhysical = FPhysical(target);
        var lifecycle = state.ReadArmyLifecycle()!;
        var targetReceiver = lifecycle.Receivers.Find(lifecycle.Slots[targetPhysical].ReceiverId) ?? throw FM("target_receiver");
        byte control = targetReceiver.SavedCommand?.Control ?? target.NativeCommandControl140 ?? throw FM("target_control");
        if (control != 0) return state;
        int start = FHex(state, actor), end = FHex(state, target);
        if (FireCommandDistance(start, end) > 3 || !FReachable(current, start, end)) return state;
        bool success = FTrial(state, current, actor, target, moment).Success;
        bool backfired = false;
        var requests = new List<CampaignEffectRequest>();
        var working = state;
        if (!success)
            working = FApplyFailure(working, current, physical, targetPhysical, moment, requests, out backfired);
        else
        {
            byte oldAction = FK(ArmyConfigurationSourceAdapter.ReadAction(working, target), "saved_action");
            var oldTarget = OwnedArmyLifecycleTargets.Read(working, targetReceiver.ReceiverId) ?? throw FM("saved_target");
            //The backup is a semantic command. Decode its actual current fields,
            //including old loaded kind6 residue; do not copy an unresolved projection.
            var decoded = ArmyTargetSourceAdapter.Decode(targetReceiver.ReceiverId, oldTarget,
                new ArmyReceiverSourceDirectory(lifecycle), current.CurrentMapObjectDirectory).Target;
            byte savedAction = Math.Min(oldAction, (byte)17);
            if (savedAction != 0 && (decoded.Kind is null or ArmyTargetKind.None or ArmyTargetKind.Unresolved ||
                decoded.Destination is null or { Kind: ArmyTargetDestinationKind.Unresolved }))
                throw FM("saved_command_target_identity");
            var saved = savedAction == 0 ? ArmySavedCommandState.Cleared :
                ArmySavedCommandState.Store(new(new(savedAction), decoded));
            var actorReceiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId) ?? throw FM("actor_receiver");
            ushort goal = ArmyMarchSourceAdapter.EncodeDestination(actorReceiver.FalseReportDestination) ?? throw FM("destination_encoding");
            var march = target.March ?? throw FM("target_march");
            var runtime = target.NativeTacticRuntime ?? throw FM("target_abnormal25");
            working = OwnedArmyLifecycleTargets.Write(working, targetReceiver.ReceiverId, new(9, goal));
            current.SetArmyLifecycleTargetFields(targetPhysical, new(9, goal));
            var changed = target with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(1) };
            working = working with { Units = new(working.Units) { [changed.Id] = changed },
                ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, changed.Id, 1) };
            //2E35D0 skips only this first clear/route under provocation25.
            //The waypoint clearing, backup, merit and second route are unconditional.
            if (runtime.Abnormal25 == 0)
            {
                changed = changed with { OverrideTarget = ArmyOverrideTargetState.Cleared,
                    March = march with { NextStep = ArmyMarchDestination.None } };
                working = working with { Units = new(working.Units) { [changed.Id] = changed } };
                working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
                current.ClearArmyCommandOverride(targetPhysical);
                working = FRoute(working, changed.Id, true, current, false);
            }
            changed = working.Units[changed.Id] with { NativeCommandControl140 = savedAction,
                //The original action remains suspended in its real saved owner.
                //Five source points are cleared here, so the UI projection must
                //not retain an old route for restoration to replay later.
                MilitaryPlan = changed.MilitaryPlan is { } suspended ? suspended with
                    { WaypointHexIds = [], NextWaypointIndex = 0, IncidentalEnemyId = null } : null,
                March = working.Units[changed.Id].March! with { Target = ArmyMarchDestination.None,
                    FollowingWaypoints = new ArmyWaypointContinuation([]) } };
            lifecycle = working.ReadArmyLifecycle()!;
            var sources = lifecycle.Source.Slots.ToArray();
            sources[targetPhysical] = sources[targetPhysical] with { CommandControl140 = savedAction };
            var latestReceiver = lifecycle.Receivers.Find(targetReceiver.ReceiverId)!;
            working = (working with { Units = new(working.Units) { [changed.Id] = changed } }).WithArmyLifecycle(lifecycle with
                { Receivers = lifecycle.Receivers.With(latestReceiver with { SavedCommand = saved,
                    OverrideTarget = runtime.Abnormal25 == 0 ? ArmyOverrideTargetState.Cleared : latestReceiver.OverrideTarget }),
                  Source = lifecycle.Source with { Slots = sources } });
            int n = Math.Max(unchecked(target.Troops - FP(working, "merit-troop-threshold")), 0);
            int requested = FDoubleInt((Math.Sqrt(n) + 100d) * unchecked(FP(working, "merit-scale") * 10) / 100d);
            var reward = _pendingMerit.TryApplyWithinArmyOperation(working, current, actor.OfficerId, requested);
            if (reward.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved) throw FM("pending_merit");
            working = FRoute(reward.State, changed.Id, true, current, false);
        }
        lifecycle = working.ReadArmyLifecycle()!;
        var actorOwner = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId) ?? throw FM("actor_receiver");
        working = working.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(
            actorOwner with { FalseReportDestination = ArmyMarchDestination.None }) });
        if (requests.Count != 0)
        {
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw FM("effect_owner"))
                .Append(CampaignEffectSource.ArmyFalseReport, moment.CalendarCycleOrdinal, requests) };
            requests.Clear();
        }
        //The real common2A11A0(0) policy tail precedes the outcome history/speeches.
        //It includes default choice2 Hold and choice1 withdrawal, not a special false-report no-op.
        working = FFinishFalseReportActorCurrent(working, current, physical, moment);
        working = FFinish(working, current, physical, targetPhysical, success, backfired, moment, requests);
        working = FPublishCompletedCapture(working, physical);
        return OwnedArmyLifecycleTargets.PublishCurrent(working.WithArmyLifecycle(
            OwnedArmyDestructorFlags.PublishCurrent(working.ReadArmyLifecycle()!, current) with
                { Indexes = (current.ReadOwnedCurrentListPool() ?? throw FM("pool")).ExportOwnedState() }), current);
    }

    private GameState FFinishFalseReportActorCurrent(GameState state, SourceWorldContext current,
        int physical, NativeArmyProgressStageMoment moment)
    {
        var army = new ArmyUnitReference(physical);
        var image = FArmy(state, physical);
        if (FK(current.ReadArmyUnitNativeValidity(army), "finish_actor_validity"))
            return FinishNoPathArmyCommand(state, current, image, moment);
        //2A11A0 has no validity entry gate. Its3C speech rejects the invalid
        //Army, but2409E0 still clears150 bit2 before the2A1980 policy call.
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 2, false)) throw FM("finish_pending_flag_write");
        state = state.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(state.ReadArmyLifecycle()!, current));
        if (FK(image.NativeMapUiByte13C, "finish_pending_MapUi13C") != 0)
            throw FM("finish_pending_MapUi_policy");
        int force = FK(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "finish_pending_Force");
        if (force is >= 46 and <= 51) throw FM("finish_pending_special_Force_policy");
        byte choice = FK(image.NativeCommandChoice41, "finish_pending_Choice41");
        if (choice == 0) return state;
        if (choice == 1) throw FM("finish_pending_withdrawal_2EA480");
        if (choice != 2) throw FM("finish_pending_Choice41_policy");
        //304A20 guards action, all five points and main target with validity,
        //but reads2A and clears48 unconditionally. Its3D speech again rejects
        //this invalid Army. Do not fabricate an active image or restore it.
        _ = FK(current.ReadArmyUnitRawHex2A(army), "finish_pending_Hold_Army2A");
        var held = image with { March = (image.March ?? throw FM("finish_pending_March")) with
            { NextStep = ArmyMarchDestination.None } };
        if (state.Units.ContainsKey(image.Id))
            return state with { Units = new(state.Units) { [image.Id] = held } };
        var lifecycle = state.ReadArmyLifecycle()!;
        var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId) ?? throw FM("finish_pending_receiver");
        if (receiver.PendingImage?.Id != image.Id) throw FM("finish_pending_image");
        return state.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver with { PendingImage = held }) });
    }

    private static GameState FPublishCompletedCapture(GameState state, int physical)
    {
        var actor = FArmy(state, physical);
        if (!state.Units.ContainsKey(actor.Id) || ArmyConfigurationSourceAdapter.ReadAction(state, actor) != 13 ||
            actor.MilitaryPlan is not { Completed: false } plan) return state;
        //Native state remains authoritative; only finish the existing command's UI projection.
        if (plan.Action.ActionKey != "capture") return state;
        return state with { Units = new(state.Units) { [actor.Id] = actor with
            { MilitaryPlan = plan with { Completed = true, NextWaypointIndex = plan.WaypointHexIds.Length,
                IncidentalEnemyId = null, RestingHexId = actor.HexId } } } };
    }

    private GameState FRoute(GameState state, int unitId, bool refresh = true, SourceWorldContext? suppliedCurrent = null, bool publishPool = true)
    {
        var current = suppliedCurrent ?? CaptureWritableTransportRouteCurrent(state); var unit = state.Units[unitId];
        var route = OrdinaryArmyRouteSource.Rewrite(state, current, unit, refresh);
        var receipt = route.Receipt ?? throw FM(route.UnresolvedOwner ?? "route");
        unit = unit with { NativeRouteGoal1F8 = receipt.CachedGoalWrite ?? unit.NativeRouteGoal1F8,
            March = receipt.NextHexWrite is ushort next ? unit.March! with
                { NextStep = ArmyMarchSourceAdapter.DecodeDestination(next) } : unit.March };
        var rewritten = state with { Units = new(state.Units) { [unitId] = unit } };
        return publishPool ? rewritten.WithArmyLifecycle(state.ReadArmyLifecycle()! with
            { Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() }) : rewritten;
    }

    private GameState FFinish(GameState state, SourceWorldContext current, int physical, int targetPhysical,
        bool success, bool backfired, NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> requests)
    {
        var actorArmy = new ArmyUnitReference(physical);
        var targetArmy = new ArmyUnitReference(targetPhysical);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw FM("report_player");
        if (current.ReadArmyUnitForceReference(actorArmy) == player || current.ReadArmyUnitForceReference(targetArmy) == player)
        {
            int message = success ? 0xC96B : backfired ? 0xC96D : 0xC96C;
            var operands = backfired ? new HistoryMessageOperands(Army0: actorArmy,
                String0: ReadHistoryDataText(1, 0x25C)) : new HistoryMessageOperands(Army0: actorArmy, Army1: targetArmy);
            var formatted = FormatHistoryReport(state, current, message, operands);
            state = formatted.State;
            int payload = FK(current.ReadArmyUnitCommanderId(actorArmy), "report_Commander12");
            int hex = FK(current.ReadArmyUnitRawHex2A(actorArmy), "report_Army2A");
            int day = StatusCampaignDay(state, moment);
            int district = ReadFacilityHistoryDistrict(current, actorArmy);
            state = WriteHistory(state, current, new ReportCommand(5, district, hex, day, formatted.Text,
                CampaignHistoryWriter.Supplemental(state), payload, MarkBit4: 1), requests);
        }
        Speech(physical, success ? CombatSpeechEvent.FalseReportInitiatorSucceeded : CombatSpeechEvent.FalseReportInitiatorFailed);
        Speech(targetPhysical, success ? CombatSpeechEvent.FalseReportTargetDeceived : CombatSpeechEvent.FalseReportTargetResisted);
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw FM("effect_owner"))
            .Append(CampaignEffectSource.ArmyFalseReport, moment.CalendarCycleOrdinal, requests) };

        void Speech(int slot, CombatSpeechEvent action)
        {
            var army = new ArmyUnitReference(slot);
            if (!FK(current.ReadArmyUnitNativeValidity(army), "speech_Army_validity")) return;
            var speaker = FArmy(state, slot);
            int rawCommander = FK(current.ReadArmyUnitCommanderId(army), "speech_Commander12");
            int person = FPerson(current, rawCommander);
            PersonReturnCommands.BindCurrent(state, current);
            int hex = PersonReturnCommands.ResolvePersonPosition(state, current, person, moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
            state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw FM("speech_personnel_owner") };
            int officer = FK(current.ReadPersonIdentity(person), "speech_person_identity");
            requests.Add(new(new(CombatParticipantKind.Army, speaker.Id, officer, action, hex, null, null,
                success ? null : rawCommander, null, null), null));
        }
    }

    private static bool FReachable(SourceWorldContext current, int start, int target)
    {
        var queue = new PriorityQueue<int, int>(); var visited = new HashSet<int>(); queue.Enqueue(start, 0);
        while (queue.TryDequeue(out int at, out _))
        {
            if (at == target) return true;
            if (!visited.Add(at)) continue;
            var hex = current.ReadHexTableReference(at) ?? throw FM("connection_Hex");
            int code = FK(current.ReadHexSurfaceCode(hex), "connection_terrain");
            var surface = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw FM("connection_terrain_pointer");
            if (FK(current.ReadSurfaceTypeRawField2A(surface), "connection_terrain2A") == 0 || surface.PhysicalSlot == 17) continue;
            for (int d = 1; d <= 6; d++)
            {
                int next = PersonReturnPathSource.Neighbor(at, d);
                if (next >= 48400 || FireCommandDistance(start, next) > 3 || visited.Contains(next)) continue;
                queue.Enqueue(next, unchecked((FireCommandDistance(start, next) + FireCommandDistance(target, next)) * 100000 + next));
            }
        }
        return false;
    }
    private UnitState FTarget(GameState state, int physical)
    {
        var arena = state.ReadArmyLifecycle() ?? throw FM("lifecycle");
        var target = OwnedArmyLifecycleTargets.Read(state, arena.Slots[physical].ReceiverId) ?? throw FM("target_fields");
        if (target is not { Kind38: 27, Identity3A: ushort id }) throw FM("target_Army_scope");
        return FArmy(state, id <= 500 ? id : 0);
    }
    private static UnitState FArmy(GameState state, int physical)
    {
        var arena = state.ReadArmyLifecycle() ?? throw FM("lifecycle");
        if (physical < 0 || physical >= arena.Slots.Count) throw FM("Army_physical_range");
        var slot = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw FM("Army_receiver");
        return slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw FM("Army_active") : slot.PendingImage ?? throw FM("Army_image");
    }
    private static int FPhysical(UnitState army) => army.NativeArmyOrigin?.NativeSlotIndex ?? throw FM("Army_physical");
    private static int FHex(GameState state, UnitState army) => FK(ArmyMarchSourceAdapter.ReadCurrentHex(state, army), "Army_Hex");
    private static int FPerson(SourceWorldContext current, int id) => current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot ?? throw FM("Person_pointer");
    private int FIntelligence(GameState state, SourceWorldContext current, UnitState army)
    {
        int person = FPerson(current, FK(current.ReadArmyUnitCommanderId(new(FPhysical(army))), "sort_Commander12"));
        return FK(current.ReadPersonNativeValidity(person), "sort_person_validity") ? FPersonIntelligence(state, current, person) : 0;
    }
    private int FPersonIntelligence(GameState state, SourceWorldContext current, int person) =>
        new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language))
            .ResolveDimension(state, FK(current.ReadPersonIdentity(person), "Person_identity"), 2) ?? throw FM("current_intelligence");
    private static int FP(GameState state, string name) => state.NativeArmySimulation?.Parameters is { } bank &&
        bank.Int32Values.GetValueOrDefault(FalseReportParameters.Prefix + "version") == 1 &&
        !bank.Float32Values.ContainsKey(FalseReportParameters.Prefix + name) && bank.Int32Values.TryGetValue(FalseReportParameters.Prefix + name, out int value)
            ? value : throw FM("parameter_" + name);
    private static TacticSourceSamplerWorld FSampler(GameState state, NativeArmyProgressStageMoment moment)
    {
        var season = state.NativeMapSeason ?? throw FM("saved_calendar");
        var clock = state.NativeClock ?? throw FM("current_clock");
        var epoch = state.NativeArmySimulation?.World.SamplerEpoch ?? throw FM("sampler_epoch");
        if (moment.CycleWithinAtomicBlock is < 0 or >= NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock ||
            moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + moment.CycleWithinAtomicBlock) ||
            clock.Revision != season.NativeClockRevision || epoch.NativeClockRevision != clock.Revision ||
            epoch.SourceScenarioUri != season.SourceScenarioUri || epoch.SourceScenarioSha256 != season.SourceScenarioSha256 ||
            epoch.SourceWorldSha256 != season.SourceWorldSha256 ||
            state.ContentHashes.GetValueOrDefault(epoch.SourceScenarioUri) != epoch.SourceScenarioSha256)
            throw FM("sampler_identity");
        MapSeasonRuntime.ValidateOwned(season);
        var date = season.ThirdDate; long days = season.AdvancedDays; int waiting = 0;
        for (int i = 0; i <= moment.CycleWithinAtomicBlock; i++)
        {
            if (NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(date)) waiting++;
            if (i == moment.CycleWithinAtomicBlock) break;
            var step = MapSeasonRuntime.StepCalendarWriter(date); date = step.Date; if (step.DayAdvanced) days++;
        }
        if (date != moment.PreWriterThirdDate || clock.UpdateOrdinal != checked(season.ProcessedNativeOrdinal + waiting))
            throw FM("reached_sampler_date");
        return new(unchecked(epoch.Day450 + (int)days), epoch.Salt45C);
    }
    private static int FInt(float value) => value is >= int.MinValue and < 2147483648f ? (int)value : int.MinValue;
    private static int FDoubleInt(double value) => value is >= int.MinValue and < 2147483648d ? (int)value : int.MinValue;
    private static T FK<T>(T? value, string field) where T : struct => value ?? throw FM(field);
    private static InvalidDataException FM(string field) => new("army.false_report." + field);
}
