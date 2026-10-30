using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Calendar work uses the actual pre-writer date; the movement boundary
    // can create a newly reached site but never invent elapsed repair ticks.
    internal GameState CompletePlayableFacilityWork(GameState state) =>
        CompletePlayableFacilityWork(state, FacilityOperation.Playable(state), false);

    private GameState CompletePlayableFacilityWork(GameState state, FacilityOperation operation, bool atHour10)
    {
        bool hasStructureAttack = atHour10 && state.Units.Values.Any(unit => unit.NativeArmyOrigin is not null &&
            unit.MilitaryPlan is { Completed: false, Action: { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Structure } });
        if (!hasStructureAttack && !state.Units.Values.Any(unit => unit.NativeArmyOrigin is not null &&
            unit.Mission?.Kind is UnitMissionKind.BuildStructure or UnitMissionKind.RepairStructure or UnitMissionKind.DemolishStructure))
            return state;
        if (atHour10 && state.Units.Values.Any(unit => unit.Mission?.Kind == UnitMissionKind.DemolishStructure))
        {
            var demolished = ConsumeArmyDemolitions(state, operation.Revision, operation.ReadDate,
                operation.ReadCampaignDay, true, null);
            state = demolished.State ?? throw new InvalidDataException(
                "Playable facility demolition is unresolved: " + demolished.UnresolvedOwner);
            if (demolished.CompletedUnitIds.Count != 0)
            {
                foreach (int id in demolished.CompletedUnitIds)
                {
                    if (!state.Units.TryGetValue(id, out var image)) continue;
                    state = state with { Units = new(state.Units) { [id] = image with
                        { Mission = new(UnitMissionKind.Combat), DestinationHexId = image.HexId,
                          MilitaryPlan = image.MilitaryPlan is { } plan ? plan with
                          { Completed = true, NextWaypointIndex = plan.WaypointHexIds.Length,
                            IncidentalEnemyId = null, RestingHexId = image.HexId } : null } } };
                }
            }
        }
        // Actual12→13→15 order at this reached10h cut. Position remains the
        // current coarse operation state, never a later period snapshot.
        if (hasStructureAttack) state = CompletePlayableStructureAttacks(state, operation);
        SourceWorldContext? facilityCurrent = null;
        SourceWorldContext Current() => facilityCurrent ??= CaptureFacilityConstructionCurrent(state);
        if (state.Units.Values.Any(unit => unit.Mission?.Kind == UnitMissionKind.BuildStructure &&
            unit.MilitaryPlan is { Action.Target.Kind: DeploymentTargetKind.Hex } plan &&
            ArmyRouteSearch.Distance(unit.HexId, plan.Action.Target.Id) == 1))
        {
            var created = ConsumeFacilityConstruction(state, operation, Current());
            state = created.State ?? throw Missing(created.UnresolvedOwner ?? "playable_construction");
            state = TransitionPlayableFacilityCommands(state, operation, finishCompleted: false, Current());
        }
        if (atHour10 && state.Units.Values.Any(unit => unit.Mission?.Kind == UnitMissionKind.RepairStructure))
        {
            var repaired = ConsumeFacilityRepair(state, operation, Current());
            state = repaired.State ?? throw Missing(repaired.UnresolvedOwner ?? "playable_repair");
            state = TransitionPlayableFacilityCommands(state, operation, finishCompleted: true, Current());
        }

        return state;
    }

    private GameState TransitionPlayableFacilityCommands(GameState state, FacilityOperation operation,
        bool finishCompleted, SourceWorldContext current)
    {
        var entries = current.ReadRegistryReferences(RegistryKind.Army) ?? throw Missing("playable_manager60");
        foreach (var entry in entries)
        {
            int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int at } ? at : throw Missing("playable_Army_pointer");
            var image = state.Units.Values.FirstOrDefault(unit => unit.NativeArmyOrigin?.NativeSlotIndex == physical);
            if (image is null || image.Mission?.Kind is not (UnitMissionKind.BuildStructure or UnitMissionKind.RepairStructure)) continue;
            var receiver = state.ReadArmyLifecycle()!.Receivers.FindUnit(image.Id) ?? throw Missing("playable_receiver");
            var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId) ?? throw Missing("playable_target");
            byte action = K(ArmyConfigurationSourceAdapter.ReadAction(state, image), "playable_action");
            if (action == 8 && !finishCompleted)
            {
                if (K(target.Kind38, "playable_build_target") != 9) throw Missing("playable_build_Hex");
                ushort raw = K(target.Identity3A, "playable_build_identity");
                var site = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("playable_site");
                var obj = FacilityConstructionLegality.ObjectAt(current, site);
                if (!K(current.ReadObjectNativeValidity(obj), "playable_existing_validity")) continue;
                var selected = Selected(state, current, image);
                if (selected != FacilityConstructionLegality.DefinitionOf(current, obj) ||
                    K(current.ReadObjectRawField14(obj), "playable_existing_HP") >
                    K(FacilityRepairCapacity.ReadCapacity(state, current, obj, _baseCapacityInputs), "playable_existing_capacity") ||
                    (K(current.ReadObjectForceSlot13(obj), "playable_existing_Force") !=
                    current.ReadArmyUnitRawForceSlot(new(physical)) &&
                    K(current.ReadObjectDefinitionRaw2C(selected), "playable_existing_category") != 3)) continue;
                if (!K(current.ReadArmyUnitNativeValidity(new(physical)), "playable_transition_validity")) continue;
                int objectPhysical = K(obj.PhysicalSlot, "playable_Object_identity");
                var subject = state.MapObjects?.Find(current.CurrentMapObjectDirectory!.ReceiverAtPhysical(objectPhysical))?.Subject
                    ?? throw Missing("playable_Object_subject");
                int structure = subject.StructureId ?? throw Missing("playable_structure");
                var updated = image with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(9),
                    Mission = new(UnitMissionKind.RepairStructure, TargetStructureId: structure),
                    MilitaryPlan = image.MilitaryPlan is { } plan ? plan with
                        { Action = new("repair", new(DeploymentTargetKind.Structure, structure)) } : null };
                state = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(11, checked((ushort)objectPhysical)));
                state = state with { Units = new(state.Units) { [image.Id] = updated },
                    ResourceState = ArmyConfigurationSourceAdapter.WriteAction(state.ResourceState, image.Id, 9) };
                OwnedArmyLifecycleTargets.BindCurrent(state, current);
            }
            else if (action == 9 && finishCompleted)
            {
                ushort raw = K(target.Identity3A, "playable_repair_identity");
                var obj = current.ReadObjectTableReference(raw <= 3000 ? raw : 0) ?? throw Missing("playable_repair_Object");
                if (!K(current.ReadObjectNativeValidity(obj), "playable_repair_validity")) continue;
                if (K(current.ReadObjectRawField14(obj), "playable_repair_HP") !=
                    K(FacilityRepairCapacity.ReadCapacity(state, current, obj, _baseCapacityInputs), "playable_repair_capacity")) continue;
                var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
                if (!K(current.ReadObjectDefinitionValidity(definition), "playable_repair_definition_validity")) continue;
                state = CompleteFacilityRepairCommand(state, current, image, obj, definition, operation);
            }
        }
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with
            { Indexes = current.ReadOwnedCurrentListPool()!.ProjectOwnedRoles() });
    }
}
