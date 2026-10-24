using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyMovementCommandRules
{
    public CommandResult IssueArmyMovementCommand(GameState state, UnitState army,
        DeploymentAction action, IReadOnlyList<int> waypointHexIds)
    {
        if (state.Units.GetValueOrDefault(army.Id) != army ||
            action is not { ActionKey: "move" or "stay" or "enter" or "attack" or "occupy" } ||
            waypointHexIds is null || waypointHexIds.Count > 5 || waypointHexIds.Any(hex => hex is < 0 or >= 48400))
            return CommandResult.Reject("The selected army, action or intermediate route is invalid.", "unit.action");
        if (army.NativeArmyOrigin is null)
            return CommandResult.RuleUnavailable("rules.army_movement_command",
                "The current army receiver identity is unavailable.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            int physical = army.NativeArmyOrigin.NativeSlotIndex ?? throw Missing("movement_command_Army_receiver");
            if (!K(current.ReadArmyUnitNativeValidity(new(physical)), "movement_command_Army_validity") ||
                army.NativeTacticRuntime is not { Status10: 1 })
                return CommandResult.Reject("Only a current ordinary army may receive this order.", "unit.action_mission");
            if (K(ArmySavedCommandKnowledge.ReadControl(state, army), "movement_command_control140") != 0)
                throw Missing("movement_command_saved_order_replacement");
            var arena = state.ReadArmyLifecycle()!;
            var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
                ?? throw Missing("movement_command_Army_receiver_owner");
            byte nativeAction, kind;
            ushort identity;
            int goal;
            if (action.ActionKey is "move" or "stay")
            {
                if (action.Target is not { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 } ||
                    action.StructureKindId is not null)
                    return CommandResult.Reject("The selected movement Hex is invalid.", "unit.destination");
                nativeAction = action.ActionKey == "move" ? (byte)1 : (byte)13;
                kind = 9; identity = checked((ushort)action.Target.Id); goal = identity;
                _ = current.ReadHexTableReference(goal) ?? throw Missing("movement_command_Hex_pointer");
            }
            else if (action.ActionKey == "occupy")
            {
                if (action.Target is not { Kind: DeploymentTargetKind.Area, Id: >= 1 and <= 500 } || action.StructureKindId is not null)
                    return CommandResult.Reject("The selected occupation area is invalid.", "unit.occupation_target");
                var area = new AreaReference(action.Target.Id);
                if (!K(current.ReadAreaNativeValidity(area), "occupation_command_Area_validity"))
                    return CommandResult.Reject("The occupation area is no longer valid.", "unit.occupation_target");
                ushort raw = K(current.ReadAreaHexIndex(area), "occupation_command_Area_representative");
                var center = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("occupation_command_Hex_pointer");
                goal = K(center.PhysicalSlot, "occupation_command_Hex_identity");
                bool admitted = new UnitTargetMenuReader(this, state, army, goal, new(true, waypointHexIds.Count),
                    [army], current, army.ForceId).ReadOccupationCommand(action.Target.Id);
                if (!admitted) return CommandResult.Reject("This army cannot occupy the selected area.", "unit.occupation_target");
                nativeAction = 14; kind = 9; identity = checked((ushort)goal);
            }
            else
            {
                var actorForce = current.ReadArmyUnitForceReference(new(physical))
                    ?? throw Missing("movement_command_source_Force_pointer");
                var targetError = ReadCurrentTargetCommand(state, current, actorForce, army.Id, action, out var target);
                if (targetError is not null) return targetError;
                var resolved = target ?? throw Missing("movement_command_resolved_target");
                nativeAction = resolved.Action;
                kind = resolved.Fields.Kind38 ?? throw Missing("movement_command_resolved_kind");
                identity = resolved.Fields.Identity3A ?? throw Missing("movement_command_resolved_identity");
                goal = resolved.GoalHex;
            }
            //7F6870 writes the same action/target/five points/override/Next
            //and ChangedList fields used by construction replacement. Keep
            //that single setter body; a coarse MilitaryPlan is its projection.
            return SubmitCurrentConstructionCommand(state, current, army, receiver, nativeAction, goal,
                waypointHexIds, receiver.FacilityConstruction,
                action.ActionKey == "occupy" ? new(UnitMissionKind.OccupyArea, TargetAreaId: action.Target.Id) : new(UnitMissionKind.Combat),
                action, (kind, identity));
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable("rules.army_movement_command", error.Message);
        }
    }

}
