using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached stage28 manager. Unsupported positive command
    /// branches reject the complete private operation, never skip a receiver.</summary>
    public ArmyCommandStageResult TryAdvanceArmyCommandMaintenance(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.MaintainArmyCommands ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Command maintenance requires reached stage28.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var order = current.ReadRegistryReferences(RegistryKind.Army) ?? throw MaintenanceMissing("manager60");
            var working = state;
            var checks = new List<ArmyCommandStageCheck>();
            foreach (var entry in order)
            {
                int slot = entry is { Kind: RegistryKind.Army, PhysicalSlot: int physical } ? physical : throw MaintenanceMissing("manager_pointer");
                var image = NotificationArmy(working, slot);
                byte action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "action37");
                byte? control = ArmySavedCommandKnowledge.ReadControl(working, image);
                //Keep the saved personnel manager boundary that a fresh per-Army
                //capture had. This does not rebind locations or task indexes.
                current.SetOwnedPersonCommandManager(working.PersonnelCommands);
                working = MaintainOrdinaryArmyCommand(working, image, moment, current);
                //The unchanged helper reads working/image during this receiver.
                //Mirror only its actual final override for subsequent receivers;
                //registered pending images retain the same receiver lookup.
                var maintainedImage = NotificationArmy(working, slot);
                if (!ReferenceEquals(image.OverrideTarget, maintainedImage.OverrideTarget))
                    current.SetArmyCommandOverride(slot, ArmyOverrideTargetSourceAdapter.Read(working, maintainedImage));
                checks.Add(new(slot, image.Id, null, action, control));
            }
            //All command/route/nearby temporaries have left their finally scopes.
            //This entry owns the current; publish its actual complete index roles.
            var pool = current.ReadOwnedCurrentListPool() ?? throw MaintenanceMissing("current_pool");
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
            return new(working, checks.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
        catch (ArmyRouteSearch.MissingOwner error) { return new(null, [], error.Message); }
    }

    /// <summary>The ordinary stage28 business pass, borrowed by a private
    /// movement operation at its actual date. No scheduler moment is created.
    /// Unsupported reached branches discard the complete operation view.</summary>
    internal ArmyCommandStageResult CompleteOrdinaryArmyCommands(GameState state, SourceWorldContext current,
        NativeMapSeasonDate date, long revision, Func<GameState, int> readCampaignDay)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(readCampaignDay);
        if (date.Hour is < 0 or > 22 || (date.Hour & 1) != 0)
            throw new InvalidDataException("Ordinary command maintenance requires an actual two-hour date.");
        try
        {
            var operation = new FacilityOperation(() => date, revision, readCampaignDay);
            var order = current.ReadRegistryReferences(RegistryKind.Army) ?? throw MaintenanceMissing("manager60");
            var working = state;
            var checks = new List<ArmyCommandStageCheck>();
            foreach (var entry in order)
            {
                int slot = entry is { Kind: RegistryKind.Army, PhysicalSlot: int physical }
                    ? physical : throw MaintenanceMissing("manager_pointer");
                var image = NotificationArmy(working, slot);
                byte action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "action37");
                bool ordinary = action is 1 or 13 && image.Mission?.Kind == UnitMissionKind.Combat && image.MilitaryPlan is not
                    { Completed: false, Action.ActionKey: not ("move" or "stay") };
                bool entering = image.NativeTacticRuntime?.Status10 == 1 && action is 2 or 3 &&
                    image.Mission?.Kind == UnitMissionKind.Combat && image.MilitaryPlan is { Action.ActionKey: "enter" };
                bool attacking = image.NativeTacticRuntime?.Status10 == 1 && action is >= 4 and <= 7 &&
                    image.Mission?.Kind == UnitMissionKind.Combat && image.MilitaryPlan is { Action.ActionKey: "attack" };
                bool withdrawing = action is 11 or 12 && image.Mission?.Kind == UnitMissionKind.Retreat;
                bool transporting = image.NativeTacticRuntime?.Status10 == 2 && action == 2 && image.Mission?.Kind == UnitMissionKind.Transport;
                if (image.NativeTacticRuntime is not { Status10: 1 or 2 } || !(ordinary || entering || attacking || withdrawing || transporting))
                    throw MaintenanceMissing("ordinary_move_stay_operation_scope");
                byte? control = ArmySavedCommandKnowledge.ReadControl(working, image);
                working = MaintainOrdinaryArmyCommand(working, image, operation, null, current);
                if (action is >= 1 and <= 7 && working.Units[image.Id] is { MilitaryPlan: { Completed: false } plan } held &&
                    ArmyConfigurationSourceAdapter.ReadAction(working, held) == 13)
                    working = working with { Units = new(working.Units) { [held.Id] = held with
                        { MilitaryPlan = plan with { Completed = true, NextWaypointIndex = plan.WaypointHexIds.Length,
                            IncidentalEnemyId = null, RestingHexId = held.HexId } } } };
                checks.Add(new(slot, image.Id, null, action, control));
            }
            var pool = current.ReadOwnedCurrentListPool() ?? throw MaintenanceMissing("current_pool");
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
            return new(working, checks.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
        catch (ArmyRouteSearch.MissingOwner error) { return new(null, [], error.Message); }
    }

    private GameState MaintainOrdinaryArmyCommand(GameState state, UnitState image, NativeArmyProgressStageMoment moment,
        SourceWorldContext? operationCurrent = null)
        => MaintainOrdinaryArmyCommand(state, image, (FacilityOperation)moment, moment, operationCurrent);

    private GameState MaintainOrdinaryArmyCommand(GameState state, UnitState image, FacilityOperation operation,
        NativeArmyProgressStageMoment? nativeMoment, SourceWorldContext? operationCurrent)
    {
        byte action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "action37");
        var current = operationCurrent ?? CaptureWritableTransportRouteCurrent(state);
        if (action is 8 or 9) BindFacilityConstructionInputs(state, current);
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("army_receiver");
        var army = new ArmyUnitReference(physical);
        if (action is 11 or 12)
        {
            //2725E0 reads2B5370 before its native-validity/home gates. A
            //still-friendly exact selected home takes no prefix writer;
            //a lost/changed home reaches2409E0 and MapUI handling, not a
            //nearest-friendly retarget or a silent successful maintenance.
            var selected = OwnedArmyLifecycleTargets.Read(state, state.ReadArmyLifecycle()!.ReceiverSlots()[physical].ReceiverId)
                ?? throw MaintenanceMissing("withdraw_target");
            ushort identity = MaintenanceKnown(selected.Identity3A, "withdraw_target_identity");
            var selectedBase = selected.Kind38 switch
            {
                5 => current.ReadCityTableReference(identity <= 51 ? identity : 0),
                6 => current.ReadGateTableReference(identity <= 10 ? identity : 0),
                _ => throw MaintenanceMissing("withdraw_target_kind")
            };
            if (selectedBase is null) throw MaintenanceMissing("withdraw_target_pointer");
            int targetForce = MaintenanceKnown(current.ReadBaseForceSlot(selectedBase), "withdraw_target_force");
            if (targetForce != current.ReadArmyUnitRawForceSlot(army))
            {
                state = FinishNoPathArmyCommand(state, current, image, operation);
                image = state.Units.GetValueOrDefault(image.Id) ?? throw MaintenanceMissing("withdraw_changed_home_image");
                action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "withdraw_changed_home_action");
            }
        }
        bool transportTargetChanged = false;
        if (image.NativeTacticRuntime?.Status10 == 2 && action is >= 1 and <= 3)
        {
            transportTargetChanged = TransportMaintenanceTargetChanged(state, current, image);
            if (transportTargetChanged)
            {
                //The source clears Next before validity/home gates and retains
                //local908 to prohibit a new route in this actor's type2 suffix.
                image = image with { March = (image.March ?? throw MaintenanceMissing("transport_target_March")) with
                    { NextStep = ArmyMarchDestination.None } };
                state = state with { Units = new(state.Units) { [image.Id] = image } };
            }
        }
        if (!MaintenanceKnown(current.ReadArmyUnitNativeValidity(army), "native_validity")) return state;
        //Use the complete current2C96E0 predicate, including its special-force
        //and invalid-home branches, rather than comparing a projected home Hex.
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.Version);
        if (MaintenanceKnown(grouping.IsAtHome2C96E0(physical), "current_at_home")) return state;
        bool excluded = MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "manager233560");
        if (excluded) return state;
        int force = current.ReadArmyUnitRawForceSlot(army);
        ushort at = MaintenanceKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, image), "current_hex");
        if (image.NativeTacticRuntime is { Status10: 2 } transport &&
            ((MaintenanceKnown(current.ReadArmyUnitFlags150(army), "transport_flags150") & 8) != 0 || (transport.Flags26 & 2) == 0))
            return state;
        var runtime = image.NativeTacticRuntime ?? throw MaintenanceMissing("command_runtime");
        var lifecycle = state.ReadArmyLifecycle()!;
        var receiver = lifecycle.ReceiverSlots()[physical].Receiver;
        byte control = MaintenanceKnown(ArmySavedCommandKnowledge.ReadControl(state, image), "saved_control");
        var working = MaintainArmyOverridePrefix(state, current, image, operation, control);
        image = working.Units[image.Id];
        runtime = image.NativeTacticRuntime!;
        action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "after_override_action37");
        byte category = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadCategory(working, image), "category40");
        var temporary = current.CreateNearbyTemporary();
        try
        {
            //272Axx creates the outer descriptor even when the gate is false.
            if (category == 2 && ((runtime.Flags26 & 1) == 0 ||
                (MaintenanceKnown(current.ReadArmyUnitFlags150(army), "automatic_flags150_bit4") & 16) != 0) &&
                control == 0 && runtime.Abnormal23 == 0 && runtime.Abnormal24 == 0 && runtime.Abnormal25 == 0 && action is not (11 or 12))
            {
                FillAutomaticNearbyTargets(working, current, image, temporary);
                if (MaintenanceKnown(current.ReadTemporaryHexListCount(temporary), "automatic_count") > 0)
                {
                    working = nativeMoment is not null
                        ? ApplyAutomaticNearbyTarget(working, current, image, nativeMoment, temporary)
                        : ApplyAutomaticNearbyTarget(working, current, image, operation, temporary,
                            () => new(operation.ReadCampaignDay(working),
                                (working.NativeArmySimulation?.World.SamplerEpoch
                                    ?? throw MaintenanceMissing("automatic_target_sampler_epoch")).Salt45C));
                    image = working.Units[image.Id];
                }
            }
            if (control == 0 && runtime.Abnormal23 == 0 && runtime.Abnormal24 == 0 && runtime.Abnormal25 == 0 && (runtime.Flags26 & 4) != 0 && action is not (11 or 12))
            {
                _baseCapacityInputs.BindCurrent(working, current);
                var resources = current.ReadArmyReturnResourceFields(army) ?? throw MaintenanceMissing("automatic_retreat_resources");
                int upper = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
                int morale = Math.Min(upper, MaintenanceKnown(resources.Morale1A, "automatic_retreat_morale1A"));
                if (morale < ProgressLowMorale || MaintenanceKnown(resources.Ready16, "automatic_retreat_troops16") < ProgressLowTroops ||
                    (runtime.Flags26 & 2) == 0)
                {
                    working = ApplyAutomaticArmyWithdrawal(working, current, image, operation);
                    image = working.Units[image.Id];
                    action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "automatic_retreat_action37");
                }
            }
            //A newly selected candidate is valid/nonexcluded, and bit3==0
            //excluded retreat actions in the actual candidate filter. Existing
            //overrides must still pass these current tracking checks.
            var trackingFields = ArmyOverrideTargetSourceAdapter.Read(working, image);
            //The source retains this value before tracking can clear3C.
            byte retainedTrackingKind = MaintenanceKnown(trackingFields.Kind3C, "tracking_kind3C");
            if ((runtime.Flags26 & 8) == 0 && retainedTrackingKind == 27)
            {
                ushort tracking = MaintenanceKnown(trackingFields.Identity3E, "tracking3E");
                if (tracking != 0)
                {
                    int tracked = tracking <= 500 ? tracking : 0;
                    var targetArmy = current.ReadArmyUnitTableReference(tracked) ?? throw MaintenanceMissing("tracking_pointer");
                    int targetPhysical = MaintenanceKnown(targetArmy.PhysicalSlot, "tracking_physical");
                    bool cleared = false;
                    if (!MaintenanceKnown(current.ReadArmyUnitNativeValidity(targetArmy), "tracking_validity") ||
                        MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, targetPhysical), "tracking233560"))
                    {
                        //26CDC0 clears only the temporary target and next step,
                        //then301DD0(0,1) resumes the retained ordinary command.
                        //The surrounding prefix has already required25==0.
                        working = ClearTrackedArmyOverride(working, current, image);
                        cleared = true;
                    }
                    else if (runtime.Abnormal25 == 0 && runtime.Abnormal23 == 0 && MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, FArmy(working, targetPhysical)), "tracking_action37") is 11 or 12)
                    {
                        //272Cxx has the same stores/route for a retreating
                        //target, additionally gated by the actor's23==0.
                        working = ClearTrackedArmyOverride(working, current, image);
                        cleared = true;
                    }
                    image = working.Units[image.Id];
                    //Native invalid targets have no C35D report. A still-valid
                    //pending/retreating target keeps its actual Army identity.
                    if (MaintenanceKnown(current.ReadArmyUnitNativeValidity(targetArmy), "tracking_report_validity") &&
                        cleared && IsPlayerRulerLedDistrict(working, current, image))
                        working = AddMaintenanceReport(working, current, image, operation, 0xC35D, targetArmy);
                }
            }
            var mainTarget = OwnedArmyLifecycleTargets.Read(working, receiver.ReceiverId)
                ?? throw MaintenanceMissing("retreat_target_main");
            ArmyUnitReference? retreatingTarget = null;
            if ((runtime.Flags26 & 8) == 0 && retainedTrackingKind == 255 &&
                mainTarget.Kind38 == 27 && runtime.Abnormal23 == 0)
            {
                ushort targetIdentity = MaintenanceKnown(mainTarget.Identity3A, "retreat_target_main_identity");
                var targetArmy = current.ReadArmyUnitTableReference(targetIdentity <= 500 ? targetIdentity : 0)
                    ?? throw MaintenanceMissing("retreat_target_main_pointer");
                int targetSlot = MaintenanceKnown(targetArmy.PhysicalSlot, "retreat_target_main_physical");
                var targetReceiver = working.ReadArmyLifecycle()!.ReceiverSlots()[targetSlot];
                var targetImage = targetReceiver.ActiveUnitId is int targetId
                    ? working.Units.GetValueOrDefault(targetId) : targetReceiver.PendingImage;
                //This read precedes native validity, including reusable receivers.
                if (MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, targetImage), "retreat_target_main_action37") is 11 or 12)
                    retreatingTarget = targetArmy;
            }
            if (retreatingTarget is null)
            {
                working = RewriteSavedArmyCommandAfterTargetChange(working, current, image, control);
                image = working.Units[image.Id];
            }
            if (retreatingTarget is not null)
            {
                if (IsPlayerRulerLedDistrict(working, current, image))
                    working = AddMaintenanceReport(working, current, image, operation, 0xC35E, retreatingTarget);
                //The original early exit reaches2A11A0(0), not the route suffix.
                working = FinishNoPathArmyCommand(working, current, image, operation);
            }
            else if (runtime.Status10 is not (1 or 2))
            {
                //Other classes leave after the same override/saved-command prefix.
            }
            else if (runtime.Status10 == 2)
            {
                //2725E0's type2 switch is separate from the ordinary route
                //tail. Only retreat11/12 with connection02 uses its stop150
                //branch here; another failed action reaches2A11A0(0).
                if (action != 13 && image.March?.NextStep == ArmyMarchDestination.None)
                {
                    if (!transportTargetChanged)
                        working = FRoute(working, image.Id, false, current, false);
                    if (working.Units[image.Id].March?.NextStep == ArmyMarchDestination.None)
                    {
                        image = working.Units[image.Id];
                        if (action is 11 or 12)
                        {
                            if ((runtime.Flags26 & 2) != 0)
                                working = StopTransportRoute(working, current, image, operation, maintenanceCaller: true);
                        }
                        else working = FinishNoPathArmyCommand(working, current, image, operation);
                    }
                }
                else if (action == 1 && OrdinaryArmyRouteSource.ReadCurrentTarget(working, image, current) == at)
                    working = FinishNoPathArmyCommand(working, current, image, operation);
            }
            else if (action == 17)
            {
                working = MaintainFalseReportArmyCommand(working, current, image, receiver, operation, force, at);
            }
            else if (action is 8 or 9)
            {
                working = MaintainFacilityConstructionCommand(working, current, image,
                    operation, action);
            }
            else if (action == 10)
            {
                //2725E0 case10: the real Object validity/pending-retirement
                //pair decides between27344A (2A11A0(1)) and the shared route tail.
                var target = OwnedArmyLifecycleTargets.Read(working, receiver.ReceiverId)
                    ?? throw MaintenanceMissing("demolition_target");
                ushort identity = MaintenanceKnown(target.Identity3A, "demolition_target_identity");
                var obj = current.ReadObjectTableReference(identity <= 3000 ? identity : 0)
                    ?? throw MaintenanceMissing("demolition_target_pointer");
                working = !MaintenanceKnown(current.ReadObjectNativeValidity(obj), "demolition_target_validity") ||
                    ConstructionObjectIsRetired(current, obj)
                    ? FinishInvalidArmyCommand(working, current, image, operation)
                    : CompleteCommandRouteTail(working, current, image, operation, action, force, at);
                //Only a real native Hold has ended this work mission. Keep its
                //historical plan for the existing stage28 completion publisher.
                var finished = working.Units[image.Id];
                if (ArmyConfigurationSourceAdapter.ReadAction(working, finished) == 13)
                    working = working with { Units = new(working.Units) { [image.Id] = finished with
                        { Mission = new(UnitMissionKind.Combat), DestinationHexId = finished.HexId } } };
            }
            else if (action is 2 or 3)
            {
                //2725E0's ordinary Enter switch compares2B5370's main
                //target virtualB0 with22CD80. It does not add a target
                //validity gate or replace a lost destination by withdrawal.
                var selected = OwnedArmyLifecycleTargets.Read(working, receiver.ReceiverId)
                    ?? throw MaintenanceMissing("enter_target");
                ushort identity = MaintenanceKnown(selected.Identity3A, "enter_target_identity");
                var selectedBase = selected.Kind38 switch
                {
                    5 => current.ReadCityTableReference(identity <= 51 ? identity : 0),
                    6 => current.ReadGateTableReference(identity <= 10 ? identity : 0),
                    _ => throw MaintenanceMissing("enter_target_kind")
                };
                if (selectedBase is null) throw MaintenanceMissing("enter_target_pointer");
                int targetForce = MaintenanceKnown(current.ReadBaseForceSlot(selectedBase), "enter_target_force");
                working = targetForce != force
                    ? FinishInvalidArmyCommand(working, current, image, operation)
                    : CompleteCommandRouteTail(working, current, image, operation, action, force, at);
            }
            else if (action is >= 4 and <= 7)
            {
                working = MaintainAttackCommand(working, current, image, operation, action, force, at);
            }
            else if (action == 16)
            {
                working = MaintainFireArmyCommand(working, current, image, operation, force, at);
            }
            else if (action == 14)
            {
                working = MaintainAreaOccupationCommand(working, current, image, operation, force, at);
            }
            else if (action is not (11 or 12))
            {
                if (action is not (1 or 13)) throw MaintenanceMissing("ordinary_action_switch");
                if (OwnedArmyLifecycleTargets.Read(working, receiver.ReceiverId) is not { Kind38: 9, Identity3A: ushort goal } || goal >= 48400)
                    throw MaintenanceMissing("current_Hex_target");
                if (action == 1 && goal == at && ArmyMarchSourceAdapter.EncodeDestination(image.March?.Target) is null)
                    throw MaintenanceMissing("arrival_first_waypoint");
                if (action == 1 && goal == at && image.March?.Target == ArmyMarchDestination.None)
                {
                    if (IsPlayerRulerLedDistrict(working, current, image)) working = AddMaintenanceReport(working, current, image, operation, 0xC35A);
                    if (image.NativeCommandChoice41 == 2)
                        working = AppendArmyCommandSpeech(working, current, image, operation,
                            CombatSpeechEvent.ArmyDestinationReachedHolding);
                    if (control != 0)
                    {
                        lifecycle = working.ReadArmyLifecycle()!;
                        var sources = lifecycle.Source.Slots.ToArray();
                        sources[physical] = sources[physical] with { CommandControl140 = 0 };
                        image = image with { NativeCommandControl140 = 0 };
                        working = (working with { Units = new(working.Units) { [image.Id] = image } }).WithArmyLifecycle(lifecycle with
                        { Receivers = lifecycle.Receivers.With(lifecycle.Receivers.Find(receiver.ReceiverId)! with { SavedCommand = ArmySavedCommandState.Cleared }), Source = lifecycle.Source with { Slots = sources } });
                        working = working with { ResourceState = CampaignSourceKnowledge.From(working.ResourceState)
                            .WithArmySavedTargetLookupAlias(receiver.ReceiverId, null) };
                        var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw MaintenanceMissing("player_force");
                        if (player.PhysicalSlot == force) working = AddMaintenanceReport(working, current, image, operation, 0xC96F);
                    }
                    //2725E0 reaches2A11A0(0) after the control/C96F tail.
                    //Reuse its actual choice0/choice2 policy and holding speech.
                    working = FinishNoPathArmyCommand(working, current, image, operation);
                }
                else working = CompleteCommandRouteTail(working, current, image, operation, action, force, at);
            }
            else working = CompleteCommandRouteTail(working, current, image, operation, action, force, at);
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
        return operationCurrent is not null ? working : working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
            { Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() });
    }

    private GameState ClearTrackedArmyOverride(GameState state, SourceWorldContext current, UnitState image)
    {
        if ((image.NativeTacticRuntime ?? throw MaintenanceMissing("tracking_runtime")).Abnormal25 != 0) return state;
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("tracking_actor_physical");
        var lifecycle = state.ReadArmyLifecycle() ?? throw MaintenanceMissing("tracking_lifecycle");
        var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical].ReceiverId)
            ?? throw MaintenanceMissing("tracking_receiver");
        image = ArmyOverrideTargetWrites.Apply(image with
        {
            March = (image.March ?? throw MaintenanceMissing("tracking_march")) with { NextStep = ArmyMarchDestination.None }
        }, ArmyOverrideTargetState.Cleared);
        state = (state with { Units = new(state.Units) { [image.Id] = image } }).WithArmyLifecycle(lifecycle with
        {
            Receivers = lifecycle.Receivers.With(receiver with { OverrideTarget = image.OverrideTarget })
        });
        state = ArmyOverrideTargetSourceAdapter.ReconcileOwner(state, image);
        current.ClearArmyCommandOverride(physical);
        return FRoute(state, image.Id, true, current, false);
    }

    private GameState MaintainAttackCommand(GameState working, SourceWorldContext current, UnitState image,
        FacilityOperation operation, byte action, int force, ushort at)
    {
        int physical = image.NativeArmyOrigin!.NativeSlotIndex!.Value;
        var target = OwnedArmyLifecycleTargets.Read(working, working.ReadArmyLifecycle()!.ReceiverSlots()[physical].ReceiverId)
            ?? throw MaintenanceMissing("attack_target");
        ushort identity = MaintenanceKnown(target.Identity3A, "attack_target_identity");
        bool valid;
        int targetForce;
        if (action is 4 or 5)
        {
            var selected = action == 4 && target.Kind38 == 5
                ? current.ReadCityTableReference(identity <= 51 ? identity : 0)
                : action == 5 && target.Kind38 == 6
                    ? current.ReadGateTableReference(identity <= 10 ? identity : 0)
                    : throw MaintenanceMissing("attack_Base_kind");
            if (selected is null) throw MaintenanceMissing("attack_Base_pointer");
            targetForce = MaintenanceKnown(current.ReadBaseForceSlot(selected), "attack_Base_Force");
            valid = MaintenanceKnown(current.ReadBaseNativeValidity(selected), "attack_Base_validity");
            if (!valid || !MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(working, force, targetForce), "attack_Base_relation"))
            {
                if (targetForce != force || MaintenanceKnown(image.NativeMapUiByte13C, "attack_Base_MapUi13C") != 0)
                    return FinishInvalidArmyCommand(working, current, image, operation);
                //A captured target becomes an Enter command on this same
                //receiver. Preserve its target, waypoints, saved order and credit.
                if (MaintenanceKnown(current.ReadArmyUnitNativeValidity(new(physical)), "friendly_Base_actor_validity") &&
                    MaintenanceKnown(ArmySavedCommandKnowledge.ReadControl(working, image), "friendly_Base_control140") == 0)
                {
                    action = action == 4 ? (byte)2 : (byte)3;
                    image = image with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action),
                        MilitaryPlan = image.MilitaryPlan is { } plan ? plan with
                            { Action = plan.Action with { ActionKey = "enter" } } : null };
                    working = working with { Units = new(working.Units) { [image.Id] = image },
                        ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, image.Id, action) };
                }
                image = image with { March = (image.March ?? throw MaintenanceMissing("friendly_Base_march")) with
                    { NextStep = ArmyMarchDestination.None } };
                working = working with { Units = new(working.Units) { [image.Id] = image } };
                //301DD0(mode0,refresh0) follows the Next48 clear.
                working = FRoute(working, image.Id, false, current, false);
                image = working.Units[image.Id];
                if (MaintenanceKnown(current.ReadBaseNativeValidity(selected), "friendly_Base_report_validity") &&
                    IsPlayerRulerLedDistrict(working, current, image))
                    working = AddMaintenanceReport(working, current, image, operation, 0xC35F, targetBase: selected);
            }
        }
        else if (action == 6)
        {
            if (target.Kind38 != 11) throw MaintenanceMissing("attack_Object_kind");
            var selected = current.ReadObjectTableReference(identity <= 3000 ? identity : 0)
                ?? throw MaintenanceMissing("attack_Object_pointer");
            valid = MaintenanceKnown(current.ReadObjectNativeValidity(selected), "attack_Object_validity");
            if (!valid || identity == 0) return FinishInvalidArmyCommand(working, current, image, operation);
            targetForce = MaintenanceKnown(current.ReadObjectForceSlot13(selected), "attack_Object_Force");
            if (!MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(working, force, targetForce), "attack_Object_relation"))
                return FinishInvalidArmyCommand(working, current, image, operation);
        }
        else
        {
            if (target.Kind38 != 27) throw MaintenanceMissing("attack_Army_kind");
            var selected = current.ReadArmyUnitTableReference(identity <= 500 ? identity : 0)
                ?? throw MaintenanceMissing("attack_Army_pointer");
            valid = MaintenanceKnown(current.ReadArmyUnitNativeValidity(selected), "attack_Army_validity");
            if (!valid) return FinishInvalidArmyCommand(working, current, image, operation);
            int targetPhysical = selected.PhysicalSlot!.Value;
            if (MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, targetPhysical), "attack_Army_excluded"))
                return FinishInvalidArmyCommand(working, current, image, operation);
            var queries = new ArmyMapGroupingCurrentQueries(working, current, _scenario, _governorInputs,
                Seed.Language, SimulationRuleIdentity.Version);
            if (MaintenanceKnown(queries.IsAtHome2C96E0(targetPhysical), "attack_Army_home") || identity == 0)
                return FinishInvalidArmyCommand(working, current, image, operation);
            targetForce = current.ReadArmyUnitRawForceSlot(selected);
            if (!MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(working, force, targetForce), "attack_Army_relation"))
                return FinishInvalidArmyCommand(working, current, image, operation);
        }
        return CompleteCommandRouteTail(working, current, image, operation, action, force, at);
    }

    private GameState PreserveSavedCommandAtValidObject(GameState state, UnitState image)
    {
        var current = CaptureWritableTransportRouteCurrent(state);
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("restoration_receiver");
        var army = new ArmyUnitReference(physical);
        var fields = current.ReadArmyReturnTraitFields(army) ?? throw MaintenanceMissing("restoration_traits36");
        foreach (ushort trait in fields.Traits64)
        {
            if (trait is 0 or > 355) continue;
            var definition = current.ReadPersonalityTableReference(trait) ?? throw MaintenanceMissing("restoration_trait_pointer");
            if (!MaintenanceKnown(current.ReadSkillPersonalityValidity(definition), "restoration_trait_validity")) continue;
            if (MaintenanceKnown(current.ReadPersonalityByte(definition, 0xB6), "restoration_effect1") == 117 ||
                MaintenanceKnown(current.ReadPersonalityByte(definition, 0xBA), "restoration_effect2") == 117)
                throw MaintenanceMissing("restoration_trait117_release");
        }
        var nearby = current.CreateNearbyTemporary();
        try
        {
            _ = NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, army, 1, nearby);
            var node = current.ReadRegistryHead(new(nearby.Identity)) ?? throw MaintenanceMissing("restoration_nearby_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(node) ?? throw MaintenanceMissing("restoration_nearby_army");
                if (payload.Kind != RegistryKind.Army) throw MaintenanceMissing("restoration_nearby_type");
                var other = new ArmyUnitReference(payload.PhysicalSlot);
                int otherForce = MaintenanceKnown(current.ReadArmyUnitForceReference(other)?.PhysicalSlot, "restoration_other_force");
                int ownForce = MaintenanceKnown(current.ReadArmyUnitForceReference(army)?.PhysicalSlot, "restoration_own_force");
                if (other.PhysicalSlot != physical && otherForce == ownForce)
                    throw MaintenanceMissing("restoration_nearby_ally_release");
                node = current.ReadRegistryNodeNext(node) ?? throw MaintenanceMissing("restoration_nearby_next");
            }
        }
        finally { current.ClearNearbyTemporary(nearby, true); }
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() });
    }


    private static bool IsPlayerRulerLedDistrict(GameState state, SourceWorldContext current, UnitState image)
    {
        var army = new ArmyUnitReference(image.NativeArmyOrigin?.NativeSlotIndex
            ?? throw MaintenanceMissing("report_Army_receiver"));
        //22BC70 reads the current Army12 and that Person's E0. E0 is a
        //logical District lookup operand, never an assumed physical slot.
        byte raw = ReadFacilityHistoryDistrict(current, army);
        var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
            ?? throw MaintenanceMissing("report_District_lookup");
        return BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, MaintenanceMissing);
    }
    private GameState AddMaintenanceReport(GameState state, SourceWorldContext current, UnitState image,
        FacilityOperation operation, int root, ArmyUnitReference? targetArmy = null, BaseReference? targetBase = null)
    {
        var army = new ArmyUnitReference(image.NativeArmyOrigin?.NativeSlotIndex
            ?? throw MaintenanceMissing("report_Army_receiver"));
        HistoryMessageOperands operands;
        if (root == 0xC35A)
        {
            byte choice = MaintenanceKnown(image.NativeCommandChoice41, "report_choice41");
            operands = new(Army0: army, String0: ReadHistoryChoiceName(state, choice));
        }
        else if (root == 0xC96F) operands = new(Army0: army);
        else if (root == 0xC35D && targetArmy is not null)
            operands = new(Army0: army, Army1: targetArmy);
        else if (root == 0xC35E && targetArmy is not null)
            operands = new(Army0: army, Army1: targetArmy,
                String0: ReadHistoryChoiceName(state, MaintenanceKnown(image.NativeCommandChoice41, "retreat_target_choice41")));
        else if (root == 0xC35C)
            operands = new(Army0: army, String0: ReadHistoryChoiceName(state,
                MaintenanceKnown(image.NativeCommandChoice41, "override_report_choice41")));
        else if (root == 0xC35F && targetBase is not null)
            //30DCF0 binds the current embedded Base name as string0.
            operands = new(Army0: army, String0: UnitDetailBaseName(state, current, targetBase.Kind,
                MaintenanceKnown(targetBase.PhysicalSlot, "friendly_Base_report_physical")));
        else if (root == 0xC360)
        {
            //2A11A0 selects this key using its cached action before binding
            //the fresh action/choice strings from the reached caller.
            byte cachedAction = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "finish_cached_action37");
            int key = cachedAction switch
            {
                8 => ReadMaintenanceConstructionFailureText(state, current, image),
                14 or 16 => 0x63A,
                _ => 0x638
            };
            byte action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "finish_report_action37");
            string actionName = ReadHistoryDataText(0, 259 + action);
            byte choice = MaintenanceKnown(image.NativeCommandChoice41, "finish_report_choice41");
            string choiceName = ReadHistoryChoiceName(state, choice);
            string resultName = ReadHistoryDataText(1, key);
            operands = new(Army0: army, String0: actionName, String1: choiceName, String2: resultName);
        }
        else throw MaintenanceMissing("report_source_variant");

        var formatted = FormatHistoryReport(state, current, root, operands);
        state = formatted.State;
        int commander = MaintenanceKnown(current.ReadArmyUnitCommanderId(army), "report_current_Commander12");
        int hex, day;
        //C360 reads Army2A before450; C35A/C96F read450 before Army2A.
        if (root == 0xC360)
        {
            hex = MaintenanceKnown(current.ReadArmyUnitRawHex2A(army), "report_current_Hex2A");
            day = operation.ReadCampaignDay(state);
        }
        else
        {
            day = operation.ReadCampaignDay(state);
            hex = MaintenanceKnown(current.ReadArmyUnitRawHex2A(army), "report_current_Hex2A");
        }
        int district = ReadFacilityHistoryDistrict(current, army);
        var requests = new List<CampaignEffectRequest>();
        state = WriteHistory(state, current, new ReportCommand(5, district, hex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), commander, MarkBit4: 1), requests);
        return requests.Count == 0 ? state : state with
        {
            CampaignEffectRequests = (state.CampaignEffectRequests ?? throw MaintenanceMissing("report_effect_owner"))
                .Append(CampaignEffectSource.ArmyCommandMaintenance, operation.Revision, requests)
        };
    }
    private int ReadMaintenanceConstructionFailureText(GameState state, SourceWorldContext current, UnitState image)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("construction_report_Army");
        var army = new ArmyUnitReference(physical);
        //2A11A0 reads selected definition, current home and force in this order.
        var selected = FacilityConstructionDefinitions.Selected(state, current, image);
        var home = current.FindArmyUnitCurrentBase(army);
        var force = current.ReadArmyUnitForceReference(army);
        int cost = FacilityConstructionCost.Read(state, current, force,
            FacilityConstructionDefinitions.Read(current, selected), _baseCapacityInputs);
        int gold = BaseCurrencyAdditionSource.ReadCurrentGold(home, current);
        return cost <= gold ? 0x63A : 0x639;
    }

    private static T MaintenanceKnown<T>(T? value, string input) where T : struct => value ?? throw MaintenanceMissing(input);
    private static InvalidDataException MaintenanceMissing(string input) => new("army.command-maintenance." + input);
}

public sealed partial class SourceWorldContext
{
    //The command's two adjacent stores have no intermediate consumer. Keep
    //the shared current view synchronized without recapturing its old owner.
    internal void ClearArmyCommandOverride(int physical) => WriteOverrideSource(physical, new(255, 0));
    internal void SetArmyCommandOverride(int physical, ArmyOverrideTargetSourceFields fields)
        => WriteOverrideSource(physical, fields);
}
