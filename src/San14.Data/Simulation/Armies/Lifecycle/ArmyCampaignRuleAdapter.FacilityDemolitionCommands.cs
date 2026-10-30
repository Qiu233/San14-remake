using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyFacilityWorkCommandRules, IPlayableArmyStructureAttackRules
{
    public CommandResult IssueStructureAttack(GameState state, UnitState army, int structureId,
        int approachHexId, IReadOnlyList<int> waypointHexIds)
        => IssueFacilityObjectCommand(state, army, structureId, approachHexId, waypointHexIds, 6);

    /// <summary>The shared player/AI command supplies a reachable adjacent
    /// destination. The actual order retains the selected Object lookup,
    /// independently of the coarse route's final adjacent Hex.</summary>
    public CommandResult IssueFacilityDemolition(GameState state, UnitState army, int structureId,
        int approachHexId, IReadOnlyList<int> waypointHexIds)
        => IssueFacilityObjectCommand(state, army, structureId, approachHexId, waypointHexIds, 10);

    public CommandResult IssueFacilityRepair(GameState state, UnitState army, int structureId,
        int approachHexId, IReadOnlyList<int> waypointHexIds)
        => IssueFacilityObjectCommand(state, army, structureId, approachHexId, waypointHexIds, 9);

    private CommandResult IssueFacilityObjectCommand(GameState state, UnitState army, int structureId,
        int approachHexId, IReadOnlyList<int> waypointHexIds, byte action)
    {
        if (!state.Units.TryGetValue(army.Id, out var actual) || actual != army ||
            army.Mission is null or { Kind: UnitMissionKind.Transport or UnitMissionKind.Retreat } ||
            approachHexId is < 0 or >= 48400 ||
            waypointHexIds is null || waypointHexIds.Count > PlayableWaypointMarch.MaximumWaypoints ||
            waypointHexIds.Any(hex => hex is < 0 or >= 48400))
            return CommandResult.Reject("The current army or facility target is invalid.", "structure.target");
        if (state.Structures is not { } structures || !structures.TryGetValue(structureId, out var structure) ||
            action == 10 && structure.ForceId != army.ForceId)
            return CommandResult.Reject("The selected facility does not belong to this army's force.", "structure.control");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw DemolitionMissing("command_Army_receiver");
            var actor = new ArmyUnitReference(physical);
            if (!DemolitionKnown(current.ReadArmyUnitNativeValidity(actor), "command_Army_validity") ||
                army.NativeTacticRuntime is not { Status10: 1 } runtime)
                return CommandResult.Reject("Only a valid ordinary field army may work on a facility.", "structure.army");
            if (DemolitionKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "command_control140") != 0)
                throw DemolitionMissing("command_saved_order_replacement");
            var lifecycle = state.ReadArmyLifecycle() ?? throw DemolitionMissing("command_lifecycle");
            var receiver = lifecycle.Receivers.FindUnit(army.Id) ?? throw DemolitionMissing("command_Army_owner");
            var objectOwner = state.MapObjects ?? throw DemolitionMissing("command_Object_owner");
            var selected = objectOwner.Receivers.FirstOrDefault(row => row.Subject?.StructureId == structureId);
            if (selected is not { Subject: { Category: MapObjectCategory.Facility or MapObjectCategory.Trap or MapObjectCategory.Obstacle } })
                return CommandResult.Reject("The selected target is not a current facility.", "structure.target");
            var directory = current.CurrentMapObjectDirectory ?? throw DemolitionMissing("command_Object_directory");
            int objectPhysical = directory.PhysicalOf(selected.Id);
            var obj = new ObjectReference(objectPhysical);
            if (!DemolitionKnown(current.ReadObjectNativeValidity(obj), "command_Object_validity"))
                return CommandResult.Reject("The selected facility is no longer present.", "structure.target");
            int targetHex = DemolitionKnown(current.ReadObjectHexIndex(obj), "command_Object_hex");
            if (targetHex != structure.HexId || targetHex is < 0 or >= 48400)
                throw DemolitionMissing("command_Object_current_position");
            byte faction = DemolitionKnown(current.ReadObjectForceSlot13(obj), "command_Object_faction");
            if (action == 6)
            {
                if (!DemolitionKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                        current.ReadArmyUnitRawForceSlot(actor), faction), "command_hostile_relation"))
                    return CommandResult.Reject("The selected facility is not hostile.", "structure.control");
                var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
                if (!DemolitionKnown(current.ReadObjectDefinitionValidity(definition), "command_definition_validity") ||
                    DemolitionKnown(current.ReadObjectDefinitionRaw2C(definition), "command_definition_category") is not (2 or 3))
                    return CommandResult.Reject("This object is not an ordinary attack target.", "structure.attack_target");
            }
            else
            {
                var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
                if (!DemolitionKnown(current.ReadObjectDefinitionValidity(definition), "command_definition_validity"))
                    return CommandResult.Reject("The selected object is not a facility.", "structure.target");
                int category = DemolitionKnown(current.ReadObjectDefinitionRaw2C(definition), "command_definition_category");
                //The ordinary repair item admits neutral obstacles. Facilities
                //and traps retain their actual own-force gate; demolition does too.
                if (!(action == 9 && category == 3))
                {
                    if (category is not (2 or 4) || faction != current.ReadArmyUnitRawForceSlot(actor))
                        return CommandResult.Reject("The selected facility does not belong to this army's force.", "structure.control");
                }
                //7EB310 does not reject a full facility. The actual stage16
                //predicate skips it and command maintenance completes it.
            }
            bool adjacent = false;
            for (int direction = 1; direction <= 6; direction++)
                if (DemolitionNeighbor(targetHex, direction) == approachHexId) { adjacent = true; break; }
            if (!adjacent)
                return CommandResult.Reject("A facility order must approach an adjacent cell.", "structure.destination");
            //All Object commands use22DFF0's physical identity, independently
            //of the Hex lookup alias used when discovering its current Object.
            ushort lookup = checked((ushort)objectPhysical);

            byte formerAction = DemolitionKnown(ArmyConfigurationSourceAdapter.ReadAction(state, army), "command_previous_action");
            var march = army.March ?? throw DemolitionMissing("command_march");
            var waypoints = waypointHexIds.Select(ArmyMarchDestination.AtHex).ToArray();
            var changed = army with
            {
                DestinationHexId = approachHexId,
                FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action),
                Mission = action == 6 ? new(UnitMissionKind.Combat) :
                    new(action == 9 ? UnitMissionKind.RepairStructure : UnitMissionKind.DemolishStructure, TargetStructureId: structureId),
                MilitaryPlan = new(waypointHexIds.ToArray(), 0,
                    new(action == 6 ? "attack" : action == 9 ? "repair" : "demolish", new(DeploymentTargetKind.Structure, structureId))),
                March = march with { Target = waypoints.FirstOrDefault() ?? ArmyMarchDestination.None,
                    FollowingWaypoints = new(waypoints.Skip(1).Cast<ArmyMarchDestination?>().ToArray()),
                    NextStep = ArmyMarchDestination.None },
                OverrideTarget = runtime.Abnormal25 == 0 ? ArmyOverrideTargetState.Cleared : army.OverrideTarget
            };
            var working = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(11, lookup));
            lifecycle = working.ReadArmyLifecycle()!;
            receiver = receiver with
            {
                FalseReportDestination = formerAction == 17 ? ArmyMarchDestination.None : receiver.FalseReportDestination,
                OverrideTarget = changed.OverrideTarget,
                FacilityConstruction = action == 9
                    ? (receiver.FacilityConstruction ?? FacilityConstructionIntent.None) with
                        { ContinuationSite = ArmyMarchDestination.None }
                    : receiver.FacilityConstruction
            };
            // The selected construction definition remains independent. A later
            // move/stay command can still retain it, as the existing submitter does.
            working = (working with { Units = new(working.Units) { [army.Id] = changed },
                ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, army.Id, action) })
                .WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver),
                    Indexes = current.ReadOwnedCurrentListPool()!.ExportOwnedState() });
            working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
            var appended = ArmyChangedListsSource.Append(working, physical, current);
            if (appended.State is not { } result)
                throw DemolitionMissing(appended.UnresolvedOwner ?? "command_changed_list");
            return CommandResult.Accept(result);
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable(action == 6 ? "rules.army_structure_attack" :
                action == 9 ? "rules.army_repair" : "rules.army_demolition", error.Message);
        }
    }
}
