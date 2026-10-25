using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState MaintainFacilityConstructionCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation, byte action)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("maintenance_Army_identity");
        var army = new ArmyUnitReference(physical);
        var receiver = state.ReadArmyLifecycle()!.Receivers.FindUnit(image.Id) ?? throw Missing("maintenance_receiver");
        if (K(ArmySavedCommandKnowledge.ReadControl(state, image), "maintenance_control") != 0)
            throw Missing("maintenance_saved_construction_order");
        var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId) ?? throw Missing("maintenance_target");
        ushort raw = K(target.Identity3A, "maintenance_target_identity");
        var working = state;
        if (action == 8)
        {
            if (K(target.Kind38, "maintenance_build_target_kind") != 9) throw Missing("maintenance_build_Hex_target_scope");
            var site = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("maintenance_site_pointer");
            if (site.PhysicalSlot is null) return working;
            var selected = Selected(working, current, image);
            var existing = FacilityConstructionLegality.ObjectAt(current, site);
            if (K(current.ReadObjectNativeValidity(existing), "maintenance_existing_validity") &&
                selected == FacilityConstructionLegality.DefinitionOf(current, existing) &&
                K(current.ReadObjectRawField14(existing), "maintenance_existing_HP") <=
                    K(FacilityRepairCapacity.ReadCapacity(working, current, existing, _baseCapacityInputs), "maintenance_existing_capacity") &&
                (K(current.ReadObjectForceSlot13(existing), "maintenance_existing_Force") == current.ReadArmyUnitRawForceSlot(army) ||
                    K(current.ReadObjectDefinitionRaw2C(selected), "maintenance_selected_category") == 3))
            {
                if (!K(current.ReadArmyUnitNativeValidity(army), "maintenance_transition_validity") ||
                    !K(current.ReadArmyUnitNativeValidity(army), "maintenance_transition_revalidation")) return working;
                int objectPhysical = K(existing.PhysicalSlot, "maintenance_transition_Object_identity");
                var subject = working.MapObjects?.Find(current.CurrentMapObjectDirectory!.ReceiverAtPhysical(objectPhysical))?.Subject
                    ?? throw Missing("maintenance_transition_subject");
                if (subject.StructureId is not int structure) throw Missing("maintenance_transition_structure");
                var updated = image with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(9),
                    Mission = new(UnitMissionKind.RepairStructure, TargetStructureId: structure),
                    MilitaryPlan = image.MilitaryPlan is { } plan ? plan with
                        { Action = new("repair", new(DeploymentTargetKind.Structure, structure)) } : null };
                working = OwnedArmyLifecycleTargets.Write(working, receiver.ReceiverId, new(11, checked((ushort)objectPhysical)));
                working = working with { Units = new(working.Units) { [image.Id] = updated },
                    ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, image.Id, 9) };
                OwnedArmyLifecycleTargets.BindCurrent(working, current);
                //The source really re-enters the full2725E0 prefix. Borrow the
                //same current/pool so both outer descriptors remain live.
                return MaintainOrdinaryArmyCommand(working, updated, operation, null, current);
            }
            var people = current.CreateNearbyTemporary();
            try
            {
                int commander = K(current.ReadArmyUnitCommanderId(army), "maintenance_commander");
                int person = K((current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                    ?? throw Missing("maintenance_Person_pointer")).PhysicalSlot, "maintenance_Person_identity");
                current.AppendConstructionPersonTemporary(people, person);
                bool legal = FacilityConstructionLegality.Check(working, current, people, site, selected);
                byte category = K(current.ReadObjectDefinitionRaw2C(selected), "maintenance_selected_category");
                if (category == 3) throw Missing("maintenance_continuous_obstacle");
                if (!legal)
                {
                    working = ClearConstructionContinuation(working, image.Id);
                    return FinishFacilityConstructionCommand(working, current, image, operation, reportFailure: true);
                }
                int own = K(ArmyMarchSourceAdapter.ReadCurrentHex(working, image), "maintenance_current_Hex");
                if (ArmyRouteSearch.Distance(own, K(site.PhysicalSlot, "maintenance_site_identity")) == 1 &&
                    !ConstructionWaypoints(image).Any(value => value < 48400))
                {
                    var home = current.FindArmyUnitCurrentBase(army);
                    int cost = FacilityConstructionCost.Read(working, current, current.ReadArmyUnitForceReference(army), Read(current, selected), _baseCapacityInputs);
                    if (BaseCurrencyAdditionSource.ReadCurrentGold(home, current) < cost)
                    {
                        working = ClearConstructionContinuation(working, image.Id);
                        return FinishFacilityConstructionCommand(working, current, image, operation, reportFailure: true);
                    }
                }
            }
            finally { current.ClearNearbyTemporary(people, true); }
        }
        else
        {
            if (K(target.Kind38, "maintenance_repair_target_kind") != 11) throw Missing("maintenance_repair_Object_target_scope");
            var obj = current.ReadObjectTableReference(raw <= 3000 ? raw : 0) ?? throw Missing("maintenance_repair_Object_pointer");
            if (!K(current.ReadObjectNativeValidity(obj), "maintenance_repair_Object_validity") ||
                ConstructionObjectIsRetired(current, obj))
                return FinishFacilityConstructionCommand(working, current, image, operation, reportFailure: true);
            var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
            int before = K(current.ReadObjectRawField14(obj), "maintenance_repair_HP");
            int capacity = K(FacilityRepairCapacity.ReadCapacity(working, current, obj, _baseCapacityInputs), "maintenance_repair_capacity");
            if (before == capacity)
            {
                if (!K(current.ReadObjectDefinitionValidity(definition), "maintenance_repair_definition_validity"))
                    return FinishFacilityConstructionCommand(working, current, image, operation, reportFailure: true);
                if (K(current.ReadObjectDefinitionRaw2C(definition), "maintenance_repair_category") == 3)
                    throw Missing("maintenance_continuous_obstacle");
                working = ClearConstructionContinuation(working, image.Id);
                return FinishFacilityConstructionCommand(working, current, image, operation, reportFailure: false);
            }
        }
        return MaintainConstructionRouteTail(working, current, image, operation);
    }

    private static bool ConstructionObjectIsRetired(SourceWorldContext current, ObjectReference obj)
    {
        var pending = current.ReadOwnedCurrentListPool()?.PendingObjectRetirements ?? throw Missing("maintenance_repair_F0");
        var directory = current.CurrentMapObjectDirectory ?? throw Missing("maintenance_repair_directory");
        var receiver = directory.ReceiverAtPhysical(K(obj.PhysicalSlot, "maintenance_repair_Object_identity"));
        foreach (var entry in pending)
        {
            if (entry is null) throw Missing("maintenance_repair_F0_pointer");
            if (entry == receiver) return true;
        }
        return false;
    }

    private static GameState ClearConstructionContinuation(GameState state, int unit)
    {
        var lifecycle = state.ReadArmyLifecycle()!;
        var receiver = lifecycle.Receivers.FindUnit(unit) ?? throw Missing("continuation_receiver");
        var intent = receiver.FacilityConstruction ?? throw Missing("continuation_current_owner");
        return state.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver with
            { FacilityConstruction = intent with { ContinuationSite = ArmyMarchDestination.None } }) });
    }

    private GameState MaintainConstructionRouteTail(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation)
    {
        ushort next = K(ArmyMarchSourceAdapter.EncodeDestination(image.March?.NextStep), "maintenance_next");
        if (next != 48400)
        {
            if (K(ArmyMarchSourceAdapter.EncodeDestination(image.March?.Target), "maintenance_first_waypoint") == next)
                throw Missing("maintenance_waypoint_Object_or_occupant_consumption");
            return state;
        }
        if (K(ArmyConfigurationSourceAdapter.ReadAction(state, image), "maintenance_tail_action") == 13) return state;
        var working = FRoute(state, image.Id, false, current, false);
        image = working.Units[image.Id];
        if (image.March?.NextStep != ArmyMarchDestination.None) return working;
        ushort goal = OrdinaryArmyRouteSource.ReadCurrentTarget(working, image) ?? throw Missing("maintenance_route_goal");
        var site = current.ReadHexTableReference(goal < 48400 ? goal : 0) ?? throw Missing("maintenance_route_Hex_pointer");
        var obj = FacilityConstructionLegality.ObjectAt(current, site);
        bool valid = K(current.ReadObjectNativeValidity(obj), "maintenance_route_Object_validity");
        int required = valid && obj.PhysicalSlot is >= 1 and <= 51 ? 2 : 1;
        if (ArmyRouteSearch.Distance(image.HexId, goal) == required) return working;
        var descriptor = current.CreateNearbyTemporary();
        try
        {
            var target = OrdinaryArmyRouteSource.ReadEffectiveSearchTarget(working, image) ?? throw Missing("maintenance_route_descriptor");
            var path = ArmyRouteSearch.Search(working, current, image, image.HexId, goal, target.Kind, target.Identity, null, out _, 0x71);
            int force = current.ReadArmyUnitRawForceSlot(new(image.NativeArmyOrigin!.NativeSlotIndex));
            foreach (int key in path)
            {
                var hex = current.ReadHexTableReference(key) ?? throw Missing("maintenance_obstacle_Hex");
                var obstacle = FacilityConstructionLegality.ObjectAt(current, hex);
                if (!K(current.ReadObjectNativeValidity(obstacle), "maintenance_obstacle_validity") || obstacle.PhysicalSlot is >= 1 and <= 61) continue;
                var definition = FacilityConstructionLegality.DefinitionOf(current, obstacle);
                if (K(current.ReadObjectDefinitionRaw2C(definition), "maintenance_obstacle_category") == 4) continue;
                if (K(ArmyLogisticsPreparation.ReadNativeRelationGate(working, force,
                    K(current.ReadObjectForceSlot13(obstacle), "maintenance_obstacle_Force")), "maintenance_obstacle_relation"))
                    throw Missing("maintenance_hostile_Object_override");
            }
            return FinishFacilityConstructionCommand(working, current, image, operation, reportFailure: false);
        }
        finally { current.ClearNearbyTemporary(descriptor, true); }
    }

    private GameState FinishFacilityConstructionCommand(GameState state, SourceWorldContext current,
        UnitState image, NativeArmyProgressStageMoment moment, bool reportFailure)
        => FinishFacilityConstructionCommand(state, current, image, (FacilityOperation)moment, reportFailure);

    private GameState FinishFacilityConstructionCommand(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation, bool reportFailure)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("finish_Army_identity");
        byte action = K(ArmyConfigurationSourceAdapter.ReadAction(state, image), "finish_action");
        byte flags = K(current.ReadArmyUnitFlags150(new(physical)), "finish_flags150");
        var working = state;
        if ((flags & 4) == 0 && action is 8 or 9)
        {
            if (state.PersonnelCommands is null) throw Missing("finish_personnel_owner");
            PersonReturnCommands.BindCurrent(working, current);
            int person = current.ResolveOfficerSource(image.OfficerId) ?? throw Missing("finish_Person_pointer");
            int position = PersonReturnCommands.ResolvePersonPosition(working, current, person, operation.ReadDate()).PhysicalSlot ?? 48400;
            int identity = K(current.ReadPersonIdentity(person), "finish_Person_identity");
            working = working with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw Missing("finish_personnel_publication"),
                CampaignEffectRequests = (working.CampaignEffectRequests ?? throw Missing("finish_effect_owner"))
                    .Append(CampaignEffectSource.ArmyCommandMaintenance, operation.Revision,
                        [new(new(CombatParticipantKind.Army, image.Id, identity, CombatSpeechEvent.ArmyOrderFinished,
                            position, null, null, null, null, null), null)]) };
            if (reportFailure && IsPlayerRulerLedDistrict(working, current, image))
            {
                string reason = "army_repair_order_unavailable";
                if (action == 8)
                {
                    var home = current.FindArmyUnitCurrentBase(new(physical));
                    var definition = Read(current, Selected(working, current, image));
                    int cost = FacilityConstructionCost.Read(working, current, current.ReadArmyUnitForceReference(new(physical)), definition, _baseCapacityInputs);
                    reason = cost <= BaseCurrencyAdditionSource.ReadCurrentGold(home, current)
                        ? "army_construction_site_unavailable" : "army_construction_insufficient_gold";
                }
                byte choice = K(image.NativeCommandChoice41, "finish_report_Choice41");
                int district = K(current.ReadPersonDistrictSlot(person), "finish_report_District");
                var report = new PeriodReportEntry(reason, current.ReadArmyUnitRawForceSlot(new(physical)),
                    OfficerId: image.OfficerId, ToHexId: image.HexId)
                { UnitId = image.Id, DistrictId = district == 0 ? null : district, CommandChoiceId = choice,
                    CampaignDay = operation.ReadCampaignDay(working), RuleId = "army.progress.command-maintenance.C360" };
                working = working with { PendingCommandReports = [.. working.PendingCommandReports, report] };
            }
        }
        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 2, false)) throw Missing("finish_flags150_write");
        working = working.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(working.ReadArmyLifecycle()!, current));
        if (K(image.NativeMapUiByte13C, "finish_MapUi13C") != 0) throw Missing("finish_MapUi13C_positive_branch");
        int force = K(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(new(physical))), "finish_Force");
        if (force is < 1 or > 45) throw Missing("finish_special_Force");
        byte disposition = K(image.NativeCommandChoice41, "finish_Choice41");
        if (disposition == 0) return working;
        if (disposition != 2) throw Missing("finish_return_Choice41");
        working = HoldArmyPositionAfterCommand(working, current, working.Units[image.Id], operation);
        var held = working.Units[image.Id];
        return working with { Units = new(working.Units) { [held.Id] = held with { DestinationHexId = held.HexId,
            Mission = new(UnitMissionKind.Combat), MilitaryPlan = new([], 0, new("stay", new(DeploymentTargetKind.Hex, held.HexId))) } } };
    }
}
