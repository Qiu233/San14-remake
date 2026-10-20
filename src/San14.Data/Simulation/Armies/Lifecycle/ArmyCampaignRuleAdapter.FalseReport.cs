using San14.Core;
namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyFalseReportCommandRules
{
    public CommandResult IssueFalseReport(GameState state, UnitState actor, UnitState target, int destinationHexId)
    {
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            int at = FPhysical(actor), to = FPhysical(target);
            if (!FK(current.ReadArmyUnitNativeValidity(new(at)), "submission_actor_validity") ||
                !FK(current.ReadArmyUnitNativeValidity(new(to)), "submission_target_validity") ||
                current.ReadHexTableReference(destinationHexId)?.PhysicalSlot != destinationHexId)
                return CommandResult.Reject("Invalid false-report actor, target or Hex.", "army.false_report_target");
            var lifecycle = state.ReadArmyLifecycle() ?? throw FM("lifecycle");
            var receiver = lifecycle.ReceiverSlots()[at].Receiver;
            if (receiver.ActiveUnitId != actor.Id || receiver.SavedCommand?.Kind != ArmySavedCommandKind.Cleared)
                throw FM("submission_complete_cleared_actor");
            var result = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(27, checked((ushort)to)));
            lifecycle = result.ReadArmyLifecycle()!;
            result = result.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver with
                { FalseReportDestination = ArmyMarchDestination.AtHex(destinationHexId) }) });
            return CommandResult.Accept(result with { Units = new(result.Units) { [actor.Id] = actor with
                { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(17) } },
                ResourceState = ArmyConfigurationSourceAdapter.WriteAction(result.ResourceState, actor.Id, 17) });
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("rules.army_false_report", error.Message); }
    }

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
            if (candidates.Count > 32) throw FM("large_candidate_sort");
            //247B20 insertion order; ability is read only for equal scores.
            for (int index = 1; index < candidates.Count; index++)
            {
                var value = candidates[index]; int at = index;
                while (at > 0 && Before(value, candidates[at - 1])) { candidates[at] = candidates[at - 1]; at--; }
                candidates[at] = value;
            }
            var working = state;
            foreach (var row in candidates) working = FApply(working, row.Physical, moment);
            return new(working, checks, null);

            bool Before((int Physical, int Score) left, (int Physical, int Score) right)
            {
                if (left.Score != right.Score) return left.Score > right.Score;
                var a = FArmy(state, left.Physical); var b = FArmy(state, right.Physical);
                int ai = FIntelligence(state, current, a), bi = FIntelligence(state, current, b);
                return ai != bi ? ai > bi : a.OfficerId < b.OfficerId;
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
        int person = FPerson(current, actor.OfficerId), defender = FPerson(current, target.OfficerId);
        int attack = FPersonIntelligence(state, current, person), defense = FPersonIntelligence(state, current, defender);
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
        var targetReceiver = lifecycle.ReceiverSlots()[targetPhysical].Receiver;
        byte control = targetReceiver.SavedCommand?.Control ?? target.NativeCommandControl140 ?? throw FM("target_control");
        if (control != 0) return state;
        int start = FHex(state, actor), end = FHex(state, target);
        if (FireCommandDistance(start, end) > 3 || !FReachable(current, start, end)) return state;
        if (!FTrial(state, current, actor, target, moment).Success) throw FM("failure_morale_and_backfire_branch");
        byte oldAction = FK(ArmyConfigurationSourceAdapter.ReadAction(state, target), "saved_action");
        var oldTarget = OwnedArmyLifecycleTargets.Read(state, targetReceiver.ReceiverId) ?? throw FM("saved_target");
        if (oldAction != 13 || oldTarget is not { Kind38: 9, Identity3A: ushort oldHex } || oldHex >= 48400)
            throw FM("saved_stay_Hex_scope");
        var actorReceiver = lifecycle.ReceiverSlots()[physical].Receiver;
        var destination = actorReceiver.FalseReportDestination ?? throw FM("false_report_destination");
        ushort goal = ArmyMarchSourceAdapter.EncodeDestination(destination) ?? throw FM("destination_encoding");
        if (target.NativeTacticRuntime is not { Abnormal25: 0 } || target.March is null)
            throw FM("target_normal_march");
        var working = OwnedArmyLifecycleTargets.Write(state, targetReceiver.ReceiverId, new(9, goal));
        var changed = target with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(1),
            OverrideTarget = ArmyOverrideTargetState.Cleared,
            March = target.March with { NextStep = ArmyMarchDestination.None } };
        working = working with { Units = new(working.Units) { [changed.Id] = changed },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, changed.Id, 1) };
        working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
        working = FRoute(working, changed.Id); //Before waypoint clearing and backup installation.
        changed = working.Units[changed.Id] with { NativeCommandControl140 = oldAction,
            March = working.Units[changed.Id].March! with { Target = ArmyMarchDestination.None,
                FollowingWaypoints = new ArmyWaypointContinuation([]) } };
        lifecycle = working.ReadArmyLifecycle()!;
        var saved = ArmySavedCommandState.Store(new(new(oldAction), new(ArmyTargetKind.Hex,
            new(ArmyTargetDestinationKind.Hex, null, null) { HexId = oldHex })));
        var sources = lifecycle.Source.Slots.ToArray(); sources[targetPhysical] = sources[targetPhysical] with { CommandControl140 = oldAction };
        working = (working with { Units = new(working.Units) { [changed.Id] = changed } }).WithArmyLifecycle(lifecycle with
        { Receivers = lifecycle.Receivers.With(lifecycle.Receivers.Find(targetReceiver.ReceiverId)! with
            { SavedCommand = saved, OverrideTarget = ArmyOverrideTargetState.Cleared }), Source = lifecycle.Source with { Slots = sources } });
        int n = Math.Max(unchecked(target.Troops - FP(working, "merit-troop-threshold")), 0);
        int requested = FDoubleInt((Math.Sqrt(n) + 100d) * unchecked(FP(working, "merit-scale") * 10) / 100d);
        var rewardCurrent = CaptureWritableTransportRouteCurrent(working);
        var reward = _pendingMerit.TryApplyWithinArmyOperation(working, rewardCurrent, actor.OfficerId, requested);
        if (reward.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved) throw FM("pending_merit");
        working = FRoute(reward.State, changed.Id); //After the complete saved command and cleared waypoints.
        lifecycle = working.ReadArmyLifecycle()!;
        working = working.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(
            lifecycle.Receivers.Find(actorReceiver.ReceiverId)! with { FalseReportDestination = ArmyMarchDestination.None }) });
        return FFinish(working, actor.Id, target.Id, moment);
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

    private GameState FFinish(GameState state, int actorId, int targetId, NativeArmyProgressStageMoment moment)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        var actor = state.Units[actorId]; var target = state.Units[targetId]; int physical = FPhysical(actor);
        if (actor.ForceId is < 1 or > 45 || actor.NativeMapUiByte13C != 0 || actor.NativeCommandChoice41 != 0)
            throw FM("finish_ordinary_force_MapUi0_choice0");
        var requests = new List<CampaignEffectRequest>();
        var flags = FK(current.ReadArmyUnitFlags150(new(physical)), "finish_flags150");
        if ((flags & 4) == 0) Speech(actor, CombatSpeechEvent.ArmyOrderFinished);
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 2, false)) throw FM("finish_flag_write");
        state = state.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(state.ReadArmyLifecycle()!, current));
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw FM("report_player");
        int playerForce = player.PhysicalSlot ?? throw FM("report_player_identity");
        if (actor.ForceId == playerForce || target.ForceId == playerForce)
        {
            int person = FPerson(current, actor.OfficerId);
            int district = FK(current.ReadPersonDistrictSlot(person), "report_district");
            var force = current.ReadForceTableReference(actor.ForceId) ?? throw FM("report_force");
            requests.Add(new(null, new(district, FHex(state, actor), FSampler(state, moment).Day450,
                actor.OfficerId, state.Officers[actor.OfficerId].Name, state.Officers[target.OfficerId].Name,
                CombatParticipantKind.Army, target.Id, actor.ForceId, current.ReadForceReportColor(force),
                current.ReadForceKingdomNameIndex(actor.ForceId), current.ReadForceRecord(actor.ForceId)?.RawRuler10)
                { Event = CampaignReportEvent.FalseReportSucceeded }));
        }
        Speech(actor, CombatSpeechEvent.FalseReportInitiatorSucceeded);
        Speech(target, CombatSpeechEvent.FalseReportTargetDeceived);
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw FM("effect_owner"))
            .Append(CampaignEffectSource.ArmyFalseReport, moment.CalendarCycleOrdinal, requests) };

        void Speech(UnitState speaker, CombatSpeechEvent action)
        {
            if (!FK(current.ReadArmyUnitNativeValidity(new(FPhysical(speaker))), "speech_Army_validity")) return;
            int person = FPerson(current, speaker.OfficerId);
            int hex = PersonReturnCommands.ResolvePersonPosition(state, current, person, moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
            int officer = FK(current.ReadPersonIdentity(person), "speech_person_identity");
            requests.Add(new(new(CombatParticipantKind.Army, speaker.Id, officer, action, hex, null, null, null, null, null), null));
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
        var slot = state.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical) ?? throw FM("Army_receiver");
        return slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw FM("Army_active") : slot.PendingImage ?? throw FM("Army_image");
    }
    private static int FPhysical(UnitState army) => army.NativeArmyOrigin?.NativeSlotIndex ?? throw FM("Army_physical");
    private static int FHex(GameState state, UnitState army) => FK(ArmyMarchSourceAdapter.ReadCurrentHex(state, army), "Army_Hex");
    private static int FPerson(SourceWorldContext current, int id) => current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot ?? throw FM("Person_pointer");
    private int FIntelligence(GameState state, SourceWorldContext current, UnitState army)
    {
        int person = FPerson(current, army.OfficerId);
        return FK(current.ReadPersonNativeValidity(person), "sort_person_validity") ? FPersonIntelligence(state, current, person) : 0;
    }
    private int FPersonIntelligence(GameState state, SourceWorldContext current, int person) =>
        (new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language))
            .Resolve(state, FK(current.ReadPersonIdentity(person), "Person_identity")) ?? throw FM("current_intelligence")).Intelligence;
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
