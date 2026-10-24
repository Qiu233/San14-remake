using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyFireCommandRules
{
    public CommandResult IssueArmiesFire(GameState state, int actorForceId, IReadOnlyList<UnitState> armies,
        int clickedHexId, int targetHexId, IReadOnlyList<int> waypointHexIds)
    {
        if (armies is null || armies.Count is < 1 or > 30 ||
            armies.Select(army => army.Id).Distinct().Count() != armies.Count ||
            armies.Any(army => state.Units.GetValueOrDefault(army.Id) != army || army.ForceId != actorForceId ||
                army.Mission is null or { Kind: UnitMissionKind.Transport }) ||
            clickedHexId is < 0 or >= 48400 || targetHexId is < 0 or >= 48400 || waypointHexIds is null || waypointHexIds.Count > 5 ||
            waypointHexIds.Any(hex => hex is < 0 or >= 48400))
            return CommandResult.Reject("The selected ordinary armies, Fire target or fresh route are invalid.", "unit.fire_selection");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            var admission = EvaluateArmyFireCommand(state, current, actorForceId, armies, clickedHexId, targetHexId, waypointHexIds);
            if (admission.Allowed is null)
                return CommandResult.RuleUnavailable("rules.army_fire_command", admission.UnresolvedInput ?? "Current Fire permission is unresolved.");
            if (admission.Allowed == false)
                return CommandResult.Reject("Fire is unavailable at the current target.", "unit.fire_target");

            // Read every native packet owner before any row submission. The
            // shared current leaf above validates active receiver membership,
            // status, commander, partition and the acting Force boundary.
            var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("fire_command_lifecycle");
            var slots = lifecycle.ReceiverSlots();
            var prepared = new List<FireOrderRow>(armies.Count);
            foreach (var army in armies)
            {
                int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("fire_command_Army_receiver");
                if ((uint)physical >= (uint)slots.Length || slots[physical].ActiveUnitId != army.Id ||
                    slots[physical].Receiver is not { Phase: ArmyReceiverPhase.Active } receiver)
                    throw Missing("fire_command_active_owner");
                var runtime = army.NativeTacticRuntime ?? throw Missing("fire_command_runtime");
                byte control = K(ArmySavedCommandKnowledge.ReadControl(state, army), "fire_command_control140");
                byte action = K(ArmyConfigurationSourceAdapter.ReadAction(state, army), "fire_command_action37");
                _ = army.March ?? throw Missing("fire_command_march");
                byte kind = 9; ushort identity = checked((ushort)targetHexId);
                int destination = targetHexId;
                var mission = new UnitMission(UnitMissionKind.Combat);
                DeploymentAction? projection = new("fire", new(DeploymentTargetKind.Hex, targetHexId));
                if (control != 0)
                {
                    var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId)
                        ?? throw Missing("fire_command_locked_target");
                    kind = K(target.Kind38, "fire_command_locked_kind");
                    identity = K(target.Identity3A, "fire_command_locked_identity");
                    destination = army.DestinationHexId;
                    mission = army.Mission!;
                    projection = army.MilitaryPlan?.Action;
                }
                prepared.Add(new(army, receiver, control != 0, control == 0 ? (byte)16 : action,
                    kind, identity, destination, mission, projection));
            }

            var candidate = state;
            foreach (var row in prepared)
            {
                //7E8D80 retains the actual construction intent, including
                //unknown fields. The action16 packet never restores row32,
                //even when140 retains a former native action17.
                var result = SubmitCurrentConstructionCommand(candidate, current, row.Army, row.Receiver,
                    row.Action, row.Destination, row.Locked ? [] : waypointHexIds,
                    row.Receiver.FacilityConstruction, row.Mission, row.Projection, (row.Kind, row.Identity));
                if (!result.Accepted) return result;
                candidate = result.State!;
                if (row.Locked && row.Army.MilitaryPlan is { } retained)
                {
                    // The five native points are cleared, but this lock does
                    // not reset engagement/completion or the old terminal.
                    var changed = candidate.Units[row.Army.Id] with
                    { MilitaryPlan = retained with { WaypointHexIds = [], NextWaypointIndex = 0 } };
                    candidate = candidate with { Units = new(candidate.Units) { [changed.Id] = changed } };
                }
            }
            return CommandResult.Accept(candidate);
        }
        catch (InvalidDataException error)
        {
            return CommandResult.RuleUnavailable("rules.army_fire_command", error.Message);
        }
    }

    private sealed record FireOrderRow(UnitState Army, ArmyReceiverState Receiver, bool Locked,
        byte Action, byte Kind, ushort Identity, int Destination, UnitMission Mission, DeploymentAction? Projection);
}
