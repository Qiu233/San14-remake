using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The mode3 common packet writer. Prepare every selected owner
    /// first, then publish only the complete batch. No trial or resource debit.</summary>
    public CommandResult IssueFalseReports(GameState state, IReadOnlyList<UnitState> actors,
        UnitState target, int destinationHexId, IReadOnlyList<int> waypointHexIds)
    {
        if (actors is null || actors.Count is < 1 or > 30 ||
            actors.Select(value => value.Id).Distinct().Count() != actors.Count ||
            actors.Any(value => state.Units.GetValueOrDefault(value.Id) != value || value.ForceId != actors[0].ForceId ||
                value.Mission is null or { Kind: UnitMissionKind.Transport } || value.NativeTacticRuntime?.Status10 != 1 || value.Id == target.Id) ||
            state.Units.GetValueOrDefault(target.Id) != target || destinationHexId is < 0 or >= 48400 ||
            waypointHexIds is null || waypointHexIds.Count > 5 || waypointHexIds.Any(value => value is < 0 or >= 48400))
            return CommandResult.Reject("The false-report selection, target or route is invalid.", "army.false_report_selection");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            var targetArmy = new ArmyUnitReference(target.NativeArmyOrigin?.NativeSlotIndex
                ?? throw FalseReportDestinationMissing("command_target_receiver"));
            var own = new ForceReference(actors[0].ForceId);
            if (!FalseReportTargetAllowed(state, current, own, targetArmy) ||
                !ReadFalseReportDestinations(current, own, targetArmy).Any(value => value.HexId == destinationHexId))
                return CommandResult.Reject("The false-report target or inducement destination is no longer available.", "army.false_report_target");
            var slots = (state.ReadArmyLifecycle() ?? throw FalseReportDestinationMissing("command_lifecycle")).ReceiverSlots();
            int raw = FRKnown(current.ReadArmyUnitRawHex2A(targetArmy), "command_target_Hex2A");
            int goal = FRKnown(current.ReadHexTableReference(raw < 48400 ? raw : 0)?.PhysicalSlot, "command_target_Hex_pointer");
            var prepared = new List<FalseReportOrderRow>(actors.Count);
            foreach (var actor in actors)
            {
                int physical = actor.NativeArmyOrigin?.NativeSlotIndex ?? throw FalseReportDestinationMissing("command_actor_receiver");
                if ((uint)physical >= (uint)slots.Length || slots[physical].ActiveUnitId != actor.Id ||
                    slots[physical].Receiver is not { Phase: ArmyReceiverPhase.Active } receiver ||
                    !FRKnown(current.ReadArmyUnitNativeValidity(new(physical)), "command_actor_validity") ||
                    FRKnown(current.ReadArmyUnitCommanderId(new(physical)), "command_actor_Commander") != actor.OfficerId ||
                    current.ReadArmyUnitForceReference(new(physical)) != own)
                    return CommandResult.Reject("The acting Army is no longer an active member of this force.", "army.false_report_actor");
                byte control = FRKnown(ArmySavedCommandKnowledge.ReadControl(state, actor), "command_actor_control140");
                byte action = FRKnown(ArmyConfigurationSourceAdapter.ReadAction(state, actor), "command_actor_action37");
                _ = actor.March ?? throw FalseReportDestinationMissing("command_actor_march");
                byte kind = 27;
                ushort identity = checked((ushort)FRKnown(targetArmy.PhysicalSlot, "command_target_identity"));
                int end = goal;
                var mission = new UnitMission(UnitMissionKind.Combat);
                DeploymentAction? projection = new("capture", new(DeploymentTargetKind.Unit, target.Id))
                { FalseReportDestinationHexId = destinationHexId };
                if (control != 0)
                {
                    var prior = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId)
                        ?? throw FalseReportDestinationMissing("command_locked_target");
                    kind = FRKnown(prior.Kind38, "command_locked_kind");
                    identity = FRKnown(prior.Identity3A, "command_locked_identity");
                    end = actor.DestinationHexId;
                    mission = actor.Mission!;
                    projection = actor.MilitaryPlan?.Action;
                }
                prepared.Add(new(actor, receiver, control != 0, control == 0 ? (byte)17 : action,
                    kind, identity, end, mission, projection));
            }
            var working = state;
            foreach (var row in prepared)
            {
                var submitted = SubmitCurrentConstructionCommand(working, current, row.Army, row.Receiver,
                    row.Action, row.Goal, row.Locked ? [] : waypointHexIds, row.Receiver.FacilityConstruction,
                    row.Mission, row.Projection, (row.Kind, row.Identity));
                if (!submitted.Accepted) return submitted;
                working = submitted.State!;
                //F6870's packet17 writes13E even when control140 keeps the
                //old action/target. It follows the shared old17-clear prefix.
                var lifecycle = working.ReadArmyLifecycle()!;
                var receiver = lifecycle.Receivers.Find(row.Receiver.ReceiverId)!;
                working = working.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver with
                { FalseReportDestination = ArmyMarchDestination.AtHex(destinationHexId) }) });
                if (row.Locked && row.Army.MilitaryPlan is { } retained)
                {
                    var changed = working.Units[row.Army.Id] with
                    { MilitaryPlan = retained with { WaypointHexIds = [], NextWaypointIndex = 0 } };
                    working = working with { Units = new(working.Units) { [changed.Id] = changed } };
                }
            }
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("rules.army_false_report", error.Message); }
    }

    private sealed record FalseReportOrderRow(UnitState Army, ArmyReceiverState Receiver, bool Locked,
        byte Action, byte Kind, ushort Identity, int Goal, UnitMission Mission, DeploymentAction? Projection);
}
