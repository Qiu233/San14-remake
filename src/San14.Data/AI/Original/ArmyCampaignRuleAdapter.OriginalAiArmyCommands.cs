using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IArmyInstructionExecution
{
    internal ArmyInstructionTarget? PrepareOriginalAiArmyInstructionTarget(GameState state, SourceWorldContext current,
        UnitState army, OriginalAiEntity? entity, OriginalAiHex? hex)
    {
        byte action = AiArmyKnown(ArmyConfigurationSourceAdapter.ReadAction(state, army), "target_Action");
        if (action is 1 or 14)
            return new(ArmyInstructionTargetKind.Hex, hex?.Id ?? throw AiArmyMissing("target_Hex_variable"));
        if (action is 2 or 4 or 11)
        {
            if (entity is not { Kind: WorldEntityKind.City or WorldEntityKind.Gate }) throw AiArmyMissing("target_Base_variable");
            return new(ArmyInstructionTargetKind.City, entity.Value.Kind == WorldEntityKind.City && entity.Value.Id <= 51
                ? entity.Value.Id : 0);
        }
        if (action is 3 or 5 or 12)
        {
            if (entity is not { Kind: WorldEntityKind.City or WorldEntityKind.Gate }) throw AiArmyMissing("target_Base_variable");
            return new(ArmyInstructionTargetKind.Gate, entity.Value.Kind == WorldEntityKind.Gate && entity.Value.Id is >= 52 and <= 61
                ? entity.Value.Id : 0);
        }
        if (action == 6)
        {
            if (entity is not { Kind: WorldEntityKind.MapObject }) throw AiArmyMissing("target_Facility_variable");
            return new(ArmyInstructionTargetKind.Facility, entity.Value.Id);
        }
        if (action == 7)
        {
            if (entity is not { Kind: WorldEntityKind.Army }) throw AiArmyMissing("target_Army_variable");
            return new(ArmyInstructionTargetKind.Army, entity.Value.Id);
        }
        //The native is genuinely successful for actions without a target writer.
        return null;
    }

    CommandResult IArmyInstructionExecution.SetAction(GameState state, ForceState force,
        SetArmyInstructionActionOrder order, CommandContext context)
        => ApplyAiArmyInstruction(state, order.UnitId, (current, army) =>
        {
            if (AiArmyKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "control140") != 0) return state;
            byte action = checked((byte)Math.Clamp(order.ActionId, 0, 17));
            return state with
            {
                Units = new(state.Units) { [army.Id] = army with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action) } },
                ResourceState = ArmyConfigurationSourceAdapter.WriteAction(state.ResourceState, army.Id, action)
            };
        });

    CommandResult IArmyInstructionExecution.SetTarget(GameState state, ForceState force,
        SetArmyInstructionTargetOrder order, CommandContext context)
        => ApplyAiArmyInstruction(state, order.UnitId, (current, army) =>
        {
            if (AiArmyKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "control140") != 0) return state;
            var target = order.Target;
            ArmyInstructionTargetKind? expected = AiArmyKnown(ArmyConfigurationSourceAdapter.ReadAction(state, army), "target_Action") switch
            {
                1 or 14 => ArmyInstructionTargetKind.Hex,
                2 or 4 or 11 => ArmyInstructionTargetKind.City,
                3 or 5 or 12 => ArmyInstructionTargetKind.Gate,
                6 => ArmyInstructionTargetKind.Facility,
                7 => ArmyInstructionTargetKind.Army,
                _ => null
            };
            if (expected is null) return state;
            if (target.Kind != expected)
                throw new AiArmyInstructionRejected("The selected target kind does not match the current Army action.");
            byte kind;
            int identity;
            switch (target.Kind)
            {
                case ArmyInstructionTargetKind.Hex:
                    kind = 9; identity = target.Id;
                    if ((uint)identity >= 48400) throw AiArmyMissing("target_Hex_range");
                    break;
                case ArmyInstructionTargetKind.City:
                    kind = 5; identity = target.Id;
                    if ((uint)identity > 51) throw AiArmyMissing("target_City_range");
                    break;
                case ArmyInstructionTargetKind.Gate:
                    kind = 6; identity = target.Id == 0 ? 0 : target.Id - 51;
                    if ((uint)identity > 10) throw AiArmyMissing("target_Gate_range");
                    break;
                case ArmyInstructionTargetKind.Facility:
                    kind = 11; identity = target.Id == 0 ? 0 : (current.CurrentMapObjectDirectory
                        ?? throw AiArmyMissing("target_Facility_directory")).PhysicalOf(new(target.Id));
                    if ((uint)identity > 3000) throw AiArmyMissing("target_Facility_range");
                    break;
                case ArmyInstructionTargetKind.Army:
                    kind = 27;
                    identity = target.Id == 0 ? 0 : WorldIndexContext.FindArmyPhysical(state, target.Id)
                        ?? throw AiArmyMissing("target_Army_receiver");
                    break;
                default: throw AiArmyMissing("target_kind");
            }
            int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
            var receiver = state.ReadArmyLifecycle()!.Slots[physical].ReceiverId;
            return OwnedArmyLifecycleTargets.Write(state, receiver, new(kind, checked((ushort)identity)));
        });

    CommandResult IArmyInstructionExecution.SetRelay(GameState state, ForceState force,
        SetArmyInstructionRelayOrder order, CommandContext context)
        => ApplyAiArmyInstruction(state, order.UnitId, (current, army) =>
        {
            //No2EE4B0 call occurs for an empty source list, even under control140.
            if (order.HexIds.Count == 0) return state;
            bool transport = army.NativeTacticRuntime?.Status10 == 2;
            ushort[] points;
            if (AiArmyKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "control140") != 0)
                points = Enumerable.Repeat((ushort)48400, 5).ToArray();
            else
            {
                points = transport ? TransportRouteSourceAdapter.ReadWaypoints(state, army)
                    ?? throw AiArmyMissing("transport_relay_owner") : ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(army.March)
                    ?? throw AiArmyMissing("relay_owner");
                //Only the supplied prefix is replaced. The source deliberately retains the tail.
                for (int index = 0; index < order.HexIds.Count; index++) points[index] = checked((ushort)order.HexIds[index]);
            }
            var changed = transport ? army : army with { March = (army.March ?? throw AiArmyMissing("March")) with
                { Target = ArmyMarchSourceAdapter.DecodeDestination(points[0]),
                    FollowingWaypoints = new(points.Skip(1).Select(value => (ArmyMarchDestination?)ArmyMarchSourceAdapter.DecodeDestination(value)).ToArray()) } };
            var result = state with { Units = new(state.Units) { [army.Id] = changed } };
            if (!transport) return result;
            var resource = result.ResourceState;
            if (TransportRouteSourceAdapter.ReadChoice(state, army) is byte choice)
                resource = TransportRouteSourceAdapter.Write(resource, army.Id, points, choice);
            return result with { Units = new(result.Units) { [army.Id] = changed with { TransportRoute = TransportRouteSourceAdapter.Decode(points) } },
                ResourceState = resource };
        });

    CommandResult IArmyInstructionExecution.Retreat(GameState state, ForceState force,
        RetreatArmyInstructionOrder order, CommandContext context)
        => ApplyAiArmyInstruction(state, order.UnitId, (current, army) =>
        {
            int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
            var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
                Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            if (AiArmyKnown(grouping.IsAtHome2C96E0(physical), "at_home"))
            {
                if (AiArmyKnown(ArmySavedCommandKnowledge.ReadControl(state, army), "control140") != 0) return state;
                int at = AiArmyKnown(current.ReadArmyUnitRawHex2A(new(physical)), "current_Hex");
                var changed = army with { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(13) };
                var result = state with { Units = new(state.Units) { [army.Id] = changed },
                    ResourceState = ArmyConfigurationSourceAdapter.WriteAction(state.ResourceState, army.Id, 13) };
                return OwnedArmyLifecycleTargets.Write(result, result.ReadArmyLifecycle()!.Slots[physical].ReceiverId, new(9, checked((ushort)at)));
            }
            //The source's search-storage index2 selects temporary workspace only.
            //The shared search owns its own workspace, with identical flags1.
            return ApplyCampaignFieldWithdrawalCurrent(state, current, army, FacilityOperation.Playable(state));
        });

    private CommandResult ApplyAiArmyInstruction(GameState state, int unitId,
        Func<SourceWorldContext, UnitState, GameState> apply)
    {
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var army = ReadOriginalAiInstructionArmyCurrent(state, current, unitId);
            if (army is null) return CommandResult.Reject("Army instruction receiver is no longer valid.", "army.instruction.receiver");
            return CommandResult.Accept(apply(current, army));
        }
        catch (AiArmyInstructionRejected error) { return CommandResult.Reject(error.Message, "army.instruction.target_action"); }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, "Current Army instruction inputs are unavailable."); }
        catch (ArmyRouteSearch.MissingOwner error) { return CommandResult.RuleUnavailable(error.Message, "Army instruction route inputs are unavailable."); }
    }

    private sealed class AiArmyInstructionRejected(string message) : Exception(message);
}
