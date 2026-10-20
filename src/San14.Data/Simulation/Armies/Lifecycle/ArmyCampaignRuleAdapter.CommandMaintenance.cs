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
            var initial = CaptureWritableTransportRouteCurrent(state);
            var order = initial.ReadRegistryReferences(RegistryKind.Army) ?? throw MaintenanceMissing("manager60");
            var working = state;
            var checks = new List<ArmyCommandStageCheck>();
            foreach (var entry in order)
            {
                int slot = entry is { Kind: RegistryKind.Army, PhysicalSlot: int physical } ? physical : throw MaintenanceMissing("manager_pointer");
                var image = NotificationArmy(working, slot);
                byte action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "action37");
                byte? control = ArmySavedCommandKnowledge.ReadControl(working, image);
                working = MaintainOrdinaryArmyCommand(working, image, moment);
                checks.Add(new(slot, image.Id, null, action, control));
            }
            return new(working, checks.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
        catch (ArmyRouteSearch.MissingOwner error) { return new(null, [], error.Message); }
    }

    private GameState MaintainOrdinaryArmyCommand(GameState state, UnitState image, NativeArmyProgressStageMoment moment,
        SourceWorldContext? operationCurrent = null)
    {
        byte action = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "action37");
        // These source pre-validity writers are not proved by the ordinary leaf.
        if (action is 11 or 12 || image.NativeTacticRuntime?.Status10 == 2) throw MaintenanceMissing("return_or_transport_prefix");
        var current = operationCurrent ?? CaptureWritableTransportRouteCurrent(state);
        if (action is 8 or 9) BindFacilityConstructionInputs(state, current);
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("army_receiver");
        var army = new ArmyUnitReference(physical);
        if (!MaintenanceKnown(current.ReadArmyUnitNativeValidity(army), "native_validity")) return state;
        //2C96E0 itself checks233560 before reading the current affiliation Base;
        //its caller checks the same exclusion after the returned home predicate.
        bool excluded = MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "manager233560");
        if (excluded) return state;
        int force = current.ReadArmyUnitRawForceSlot(army);
        if (force is < 1 or > 45) throw MaintenanceMissing("special_force_home_gate");
        var context = Context(current);
        int home = MaintenanceKnown(new ArmyLogisticsPreparation(context, current, _map, _terrain).ReadCommanderBaseHex(state, image), "current_home");
        ushort at = MaintenanceKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, image), "current_hex");
        if (home == at) return state;
        if (image.NativeTacticRuntime is not { Status10: 1, Abnormal23: 0, Abnormal24: 0, Abnormal25: 0 } runtime || image.OverrideTarget?.Kind is not (ArmyOverrideTargetKind.None or ArmyOverrideTargetKind.ArmyReceiver))
            throw MaintenanceMissing("ordinary_clear_abnormal_and_override");
        var lifecycle = state.ReadArmyLifecycle()!;
        var receiver = lifecycle.ReceiverSlots()[physical].Receiver;
        byte control = MaintenanceKnown(ArmySavedCommandKnowledge.ReadControl(state, image), "saved_control");
        if (control != 0 && receiver.SavedCommand is not { Kind: ArmySavedCommandKind.Stored, Command: { Action.Id: 13, Target.Kind: ArmyTargetKind.Hex } })
            throw MaintenanceMissing("saved_stay_scope");
        if (control != 0 && action == 13) throw MaintenanceMissing("saved_current_stay_304A20");

        // 2728ED: a saved command with zero25 rewrites the clear override and
        // route BEFORE the temporary near-Army list and action switch.
        var working = state;
        if (control != 0)
        {
            int upper = MaintenanceKnown(ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "231480_cap");
            if (upper < 0) throw MaintenanceMissing("negative_abnormal_cap");
            image = ArmyOverrideTargetWrites.Apply(image with { March = image.March! with { NextStep = ArmyMarchDestination.None } }, ArmyOverrideTargetState.Cleared);
            working = (working with { Units = new(working.Units) { [image.Id] = image } }).WithArmyLifecycle(working.ReadArmyLifecycle()! with
            { Receivers = working.ReadArmyLifecycle()!.Receivers.With(working.ReadArmyLifecycle()!.Receivers.Find(receiver.ReceiverId)! with
                { OverrideTarget = image.OverrideTarget }) });
            working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, image);
            working = FRoute(working, image.Id);
            image = working.Units[image.Id];
            current = CaptureWritableTransportRouteCurrent(working);
        }
        byte category = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadCategory(working, image), "category40");
        var temporary = current.CreateNearbyTemporary();
        try
        {
            //272Axx creates the outer descriptor even when the gate is false.
            if (category == 2 && ((runtime.Flags26 & 1) == 0 ||
                (MaintenanceKnown(current.ReadArmyUnitFlags150(army), "automatic_flags150_bit4") & 16) != 0) &&
                control == 0 && action is not (11 or 12))
            {
                FillAutomaticNearbyTargets(working, current, image, temporary);
                if (MaintenanceKnown(current.ReadTemporaryHexListCount(temporary), "automatic_count") > 0)
                {
                    working = ApplyAutomaticNearbyTarget(working, current, image, moment, temporary);
                    image = working.Units[image.Id];
                }
            }
            if (control == 0 && (runtime.Flags26 & 4) != 0)
            {
                _baseCapacityInputs.BindCurrent(working, current);
                var resources = current.ReadArmyReturnResourceFields(army) ?? throw MaintenanceMissing("automatic_retreat_resources");
                int upper = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
                int morale = Math.Min(upper, MaintenanceKnown(resources.Morale1A, "automatic_retreat_morale1A"));
                if (morale < ProgressLowMorale || MaintenanceKnown(resources.Ready16, "automatic_retreat_troops16") < ProgressLowTroops ||
                    (runtime.Flags26 & 2) == 0) throw MaintenanceMissing("automatic_retreat_2EA480");
            }
            //A newly selected candidate is valid/nonexcluded, and bit3==0
            //excluded retreat actions in the actual candidate filter. Existing
            //overrides must still pass these current tracking checks.
            var trackingFields = ArmyOverrideTargetSourceAdapter.Read(working, image);
            if ((runtime.Flags26 & 8) == 0 && trackingFields.Kind3C == 27)
            {
                ushort tracking = MaintenanceKnown(trackingFields.Identity3E, "tracking3E");
                if (tracking != 0)
                {
                    int tracked = tracking <= 500 ? tracking : 0;
                    var targetArmy = current.ReadArmyUnitTableReference(tracked) ?? throw MaintenanceMissing("tracking_pointer");
                    if (!MaintenanceKnown(current.ReadArmyUnitNativeValidity(targetArmy), "tracking_validity") ||
                        MaintenanceKnown(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, tracked), "tracking233560"))
                        throw MaintenanceMissing("tracking_invalid_clear26CDC0");
                    if (MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, FArmy(working, tracked)), "tracking_action37") is 11 or 12)
                        throw MaintenanceMissing("tracking_retreat_clear_C35D");
                }
            }
            if (action == 17)
            {
                ValidateFinishedFalseReport(current, working, receiver);
                var parentTarget = OwnedArmyLifecycleTargets.Read(working, receiver.ReceiverId)!;
                var parent = FArmy(working, parentTarget.Identity3A!.Value <= 500 ? parentTarget.Identity3A.Value : 0);
                byte parentAction = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(working, parent), "tracking_parent_action");
                if (parentAction is 11 or 12 && (runtime.Flags26 & 8) != 8) throw MaintenanceMissing("tracking_retreat_parent_finish");
                // An invalid purpose Object reaches27344A, then2A11A0(1).
                // It is not the ordinary no-write tail, even with a known Next.
                working = FinishFalseReportCommand(working, current, image, moment);
            }
            else if (action is 8 or 9)
            {
                working = MaintainFacilityConstructionCommand(working, current, image, moment, action);
            }
            else
            {
                if (action is not (1 or 13)) throw MaintenanceMissing("ordinary_action_switch");
                if (OwnedArmyLifecycleTargets.Read(working, receiver.ReceiverId) is not { Kind38: 9, Identity3A: ushort goal } || goal >= 48400)
                    throw MaintenanceMissing("current_Hex_target");
                if (action == 1 && goal == at && ArmyMarchSourceAdapter.EncodeDestination(image.March?.Target) is null)
                    throw MaintenanceMissing("arrival_first_waypoint");
                if (action == 1 && goal == at && image.March?.Target == ArmyMarchDestination.None)
                {
                    if (image.NativeCommandChoice41 != 0 || image.NativeMapUiByte13C != 0) throw MaintenanceMissing("arrival_choice0_MapUi0");
                    if (IsPlayerRulerLedDistrict(working, current, image)) working = AddMaintenanceReport(working, current, image, moment, 0xC35A);
                    if (control != 0)
                    {
                        lifecycle = working.ReadArmyLifecycle()!;
                        var sources = lifecycle.Source.Slots.ToArray();
                        sources[physical] = sources[physical] with { CommandControl140 = 0 };
                        image = image with { NativeCommandControl140 = 0 };
                        working = (working with { Units = new(working.Units) { [image.Id] = image } }).WithArmyLifecycle(lifecycle with
                        { Receivers = lifecycle.Receivers.With(lifecycle.Receivers.Find(receiver.ReceiverId)! with { SavedCommand = ArmySavedCommandState.Cleared }), Source = lifecycle.Source with { Slots = sources } });
                        var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw MaintenanceMissing("player_force");
                        if (player.PhysicalSlot == force) working = AddMaintenanceReport(working, current, image, moment, 0xC96F);
                    }
                    // 2A11A0(0): action1 emits no order-finished speech; source
                    // flags150 bit2 is cleared, ordinary choice0 writes no order.
                    if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 2, false)) throw MaintenanceMissing("finish_flags150");
                    working = working.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(working.ReadArmyLifecycle()!, current));
                }
                else if (MaintenanceKnown(ArmyMarchSourceAdapter.EncodeDestination(image.March?.NextStep), "next_word48") is ushort nextWord && nextWord != 48400)
                {
                    ushort firstWaypoint = MaintenanceKnown(ArmyMarchSourceAdapter.EncodeDestination(image.March?.Target), "first_waypoint2C");
                    if (firstWaypoint == nextWord) throw MaintenanceMissing("next_waypoint_Object_or_occupant_consumption");
                }
                else if (action != 13)
                {
                    working = FRoute(working, image.Id, false, current, false);
                    image = working.Units[image.Id];
                    if (image.March?.NextStep == ArmyMarchDestination.None)
                    {
                        ushort effectiveGoal = OrdinaryArmyRouteSource.ReadCurrentTarget(working, image)
                            ?? throw MaintenanceMissing("no_next_effective_goal");
                        var goalHex = current.ReadHexTableReference(effectiveGoal < 48400 ? effectiveGoal : 0) ?? throw MaintenanceMissing("no_next_goal_Hex");
                        ushort link = MaintenanceKnown(current.ReadHexObjectLink(goalHex), "no_next_goal_Object");
                        var goalObject = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw MaintenanceMissing("no_next_Object_pointer");
                        bool objectValid = MaintenanceKnown(current.ReadObjectNativeValidity(goalObject), "no_next_Object_validity");
                        int requiredDistance = objectValid && goalObject.PhysicalSlot is >= 1 and <= 51 ? 2 : 1;
                        if (FireCommandDistance(at, effectiveGoal) != requiredDistance)
                        {
                            var routeList = current.CreateTemporaryHexList() ?? throw MaintenanceMissing("obstacle_route_descriptor");
                            int[] path;
                            try
                            {
                                var descriptorTarget = OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(working, image)
                                    ?? throw MaintenanceMissing("obstacle_effective_descriptor");
                                path = ArmyRouteSearch.Search(working, current, image, at, effectiveGoal,
                                    descriptorTarget.Kind, descriptorTarget.Identity, null, out _, 0x71);
                            }
                            finally { current.ClearTemporaryHexList(routeList); current.ReleaseTemporaryHexList(routeList); }
                            foreach (int hexId in path)
                            {
                                var hex = current.ReadHexTableReference(hexId) ?? throw MaintenanceMissing("obstacle_Hex");
                                ushort obstacleLink = MaintenanceKnown(current.ReadHexObjectLink(hex), "obstacle_Object_link");
                                var obstacle = current.ReadObjectTableReference(obstacleLink <= 3000 ? obstacleLink : 0) ?? throw MaintenanceMissing("obstacle_Object");
                                if (!MaintenanceKnown(current.ReadObjectNativeValidity(obstacle), "obstacle_validity") || obstacle.PhysicalSlot is >= 1 and <= 61) continue;
                                var data = current.ReadPhysicalObject(obstacle.PhysicalSlot ?? throw MaintenanceMissing("obstacle_identity")) ?? throw MaintenanceMissing("obstacle_fields");
                                var definition = current.ReadObjectDefinitionReference(data.RawField12 <= 30 ? data.RawField12 : 0) ?? throw MaintenanceMissing("obstacle_definition");
                                if (MaintenanceKnown(current.ReadObjectDefinitionRaw2C(definition), "obstacle_category") == 4) continue;
                                int obstacleForce = MaintenanceKnown(current.ReadObjectForceSlot13(obstacle), "obstacle_force");
                                if (MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(working, force, obstacleForce), "obstacle_relation"))
                                    throw MaintenanceMissing("hostile_generic_Object_override_owner");
                            }
                            working = FinishNoPathArmyCommand(working, current, image, moment);
                        }
                    }
                }
            }
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
        return operationCurrent is not null ? working : working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
            { Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() });
    }

    private GameState FinishFalseReportCommand(GameState state, SourceWorldContext current, UnitState image,
        NativeArmyProgressStageMoment moment)
    {
        if (image.NativeMapUiByte13C != 0 || image.NativeCommandChoice41 != 0)
            throw MaintenanceMissing("false_report_finish_MapUi0_choice0");
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw MaintenanceMissing("finish_Army_receiver");
        byte flags = MaintenanceKnown(current.ReadArmyUnitFlags150(new(physical)), "finish_flags150");
        var working = state;
        if ((flags & 4) == 0)
        {
            int person = current.ResolveOfficerSource(image.OfficerId) ?? throw MaintenanceMissing("finish_person");
            int hex = PersonReturnCommands.ResolvePersonPosition(state, current, person, moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
            int officer = MaintenanceKnown(current.ReadPersonIdentity(person), "finish_person_identity");
            var speech = new CombatSpeechRequest(CombatParticipantKind.Army, image.Id, officer,
                CombatSpeechEvent.ArmyOrderFinished, hex, null, null, null, null, null);
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw MaintenanceMissing("finish_effect_owner"))
                .Append(CampaignEffectSource.ArmyCommandMaintenance, moment.CalendarCycleOrdinal, [new(speech, null)]) };
            if (IsPlayerRulerLedDistrict(working, current, image))
                working = AddMaintenanceReport(working, current, image, moment, 0xC360);
        }
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 2, false)) throw MaintenanceMissing("finish_flag_write");
        return working.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(working.ReadArmyLifecycle()!, current));
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

    private static void ValidateFinishedFalseReport(SourceWorldContext current, GameState state, ArmyReceiverState receiver)
    {
        var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId) ?? throw MaintenanceMissing("false_report_target");
        if (target is not { Kind38: 27, Identity3A: ushort raw }) throw MaintenanceMissing("false_report_parent_Army");
        var parent = current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw MaintenanceMissing("false_report_parent_pointer");
        ushort parentPosition = MaintenanceKnown(current.ReadArmyUnitRawHex2A(parent), "false_report_parent_position");
        _ = current.ReadHexTableReference(parentPosition < 48400 ? parentPosition : 0) ?? throw MaintenanceMissing("false_report_parent_Hex");
        ushort purpose = ArmyMarchSourceAdapter.EncodeDestination(receiver.FalseReportDestination) ?? throw MaintenanceMissing("false_report_purpose");
        var hex = current.ReadHexTableReference(purpose < 48400 ? purpose : 0) ?? throw MaintenanceMissing("false_report_purpose_Hex");
        ushort link = MaintenanceKnown(current.ReadHexObjectLink(hex), "false_report_purpose_Object");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw MaintenanceMissing("false_report_purpose_pointer");
        if (MaintenanceKnown(current.ReadObjectNativeValidity(obj), "false_report_purpose_validity")) throw MaintenanceMissing("false_report_unconsumed_purpose");
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
        NativeArmyProgressStageMoment moment, int root)
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
        else if (root == 0xC360)
        {
            //2A11A0 selects this key using its cached action before binding
            //the fresh action/choice strings. The current caller is action17.
            byte cachedAction = MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "finish_cached_action37");
            int key = cachedAction switch
            {
                8 => throw MaintenanceMissing("finish_action8_capacity_2AB7D0"),
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
            day = StatusCampaignDay(state, moment);
        }
        else
        {
            day = StatusCampaignDay(state, moment);
            hex = MaintenanceKnown(current.ReadArmyUnitRawHex2A(army), "report_current_Hex2A");
        }
        int district = ReadFacilityHistoryDistrict(current, army);
        var requests = new List<CampaignEffectRequest>();
        state = WriteHistory(state, current, new ReportCommand(5, district, hex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), commander, MarkBit4: 1), requests);
        return requests.Count == 0 ? state : state with
        {
            CampaignEffectRequests = (state.CampaignEffectRequests ?? throw MaintenanceMissing("report_effect_owner"))
                .Append(CampaignEffectSource.ArmyCommandMaintenance, moment.CalendarCycleOrdinal, requests)
        };
    }
    private static T MaintenanceKnown<T>(T? value, string input) where T : struct => value ?? throw MaintenanceMissing(input);
    private static InvalidDataException MaintenanceMissing(string input) => new("army.command-maintenance." + input);
}
