namespace San14.Core;

public enum ArmyInstructionTargetKind { Hex, City, Gate, Facility, Army }
/// <summary>A known selected target. Zero retains the real placeholder for
/// entities; Gate identities otherwise use the campaign's locations52..61.</summary>
public sealed record ArmyInstructionTarget(ArmyInstructionTargetKind Kind, int Id);
public sealed record SetArmyInstructionActionOrder(int ForceId, int UnitId, int ActionId) : GameOrder(ForceId);
public sealed record SetArmyInstructionTargetOrder(int ForceId, int UnitId, ArmyInstructionTarget Target) : GameOrder(ForceId);
public sealed record SetArmyInstructionRelayOrder(int ForceId, int UnitId, IReadOnlyList<int> HexIds) : GameOrder(ForceId);
public sealed record RetreatArmyInstructionOrder(int ForceId, int UnitId) : GameOrder(ForceId);
public sealed record ClearArmyAiActionOrder(int ForceId, int UnitId) : GameOrder(ForceId);
public sealed record AvoidArmyPoisonOrder(int ForceId, int UnitId) : GameOrder(ForceId);

public sealed record RecoverArmyLogisticsOrder(int ForceId, int UnitId) : GameOrder(ForceId);
public sealed record ExcludeArmyFromPlayerFootholdOrder(int ForceId, int UnitId) : GameOrder(ForceId);

public sealed record OccupyArmyNeighborCapitalOrder(int ForceId, int UnitId) : GameOrder(ForceId);
public sealed record OccupyArmyNeighborSceneryOrder(int ForceId, int UnitId) : GameOrder(ForceId);

public sealed record MoveArmyToAdvantagePositionOrder(int ForceId, int UnitId) : GameOrder(ForceId);
public sealed record MoveArmyToObjectAttackPositionOrder(int ForceId, int UnitId) : GameOrder(ForceId);

public interface IArmyInstructionExecution
{
    CommandResult SetAction(GameState state, ForceState force, SetArmyInstructionActionOrder order, CommandContext context);
    CommandResult SetTarget(GameState state, ForceState force, SetArmyInstructionTargetOrder order, CommandContext context);
    CommandResult SetRelay(GameState state, ForceState force, SetArmyInstructionRelayOrder order, CommandContext context);
    CommandResult Retreat(GameState state, ForceState force, RetreatArmyInstructionOrder order, CommandContext context);
    CommandResult ClearAction(GameState state, ForceState force, ClearArmyAiActionOrder order, CommandContext context);
    CommandResult AvoidPoison(GameState state, ForceState force, AvoidArmyPoisonOrder order, CommandContext context);
    CommandResult RecoverLogistics(GameState state, ForceState force, RecoverArmyLogisticsOrder order, CommandContext context);
    CommandResult ExcludePlayerFoothold(GameState state, ForceState force, ExcludeArmyFromPlayerFootholdOrder order, CommandContext context);
    CommandResult OccupyNeighborCapital(GameState state, ForceState force, OccupyArmyNeighborCapitalOrder order, CommandContext context);
    CommandResult OccupyNeighborScenery(GameState state, ForceState force, OccupyArmyNeighborSceneryOrder order, CommandContext context);
    CommandResult MoveToAdvantagePosition(GameState state, ForceState force, MoveArmyToAdvantagePositionOrder order, CommandContext context);
    CommandResult MoveToObjectAttackPosition(GameState state, ForceState force, MoveArmyToObjectAttackPositionOrder order, CommandContext context);
}

internal sealed class ArmyInstructionOrders(IOrderRules inner, IArmyInstructionExecution? execution) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        int id;
        switch (order)
        {
            case SetArmyInstructionActionOrder action: id = action.UnitId; break;
            case SetArmyInstructionTargetOrder target: id = target.UnitId; break;
            case SetArmyInstructionRelayOrder relay: id = relay.UnitId; break;
            case RetreatArmyInstructionOrder retreat: id = retreat.UnitId; break;
            case ClearArmyAiActionOrder clear: id = clear.UnitId; break;
            case AvoidArmyPoisonOrder avoid: id = avoid.UnitId; break;
            case RecoverArmyLogisticsOrder recover: id = recover.UnitId; break;
            case ExcludeArmyFromPlayerFootholdOrder exclude: id = exclude.UnitId; break;
            case OccupyArmyNeighborCapitalOrder capital: id = capital.UnitId; break;
            case OccupyArmyNeighborSceneryOrder scenery: id = scenery.UnitId; break;
            case MoveArmyToAdvantagePositionOrder advantage: id = advantage.UnitId; break;
            case MoveArmyToObjectAttackPositionOrder objectAttack: id = objectAttack.UnitId; break;
            default: return inner.Apply(state, force, order, context);
        }
        if (state.Phase != TurnPhase.Strategy)
            return CommandResult.Reject("Army instructions require the strategy command phase.", "army.instruction.phase");
        if (!PlayableUnitControl.TryResolve(state, force, id, context, out _, out var denied)) return denied!;
        if (execution is null)
            return CommandResult.RuleUnavailable("rules.army_instruction", "Current army instruction rules are unavailable.");
        if (order is SetArmyInstructionRelayOrder points &&
            (points.HexIds is null || points.HexIds.Count > 5 || points.HexIds.Any(hex => hex < 0 || hex >= (long)state.MapWidth * state.MapHeight)))
            return CommandResult.Reject("Army instructions accept at most five actual intermediate hexes.", "army.instruction.relay");
        if (order is SetArmyInstructionTargetOrder selected &&
            (selected.Target is null || !Enum.IsDefined(selected.Target.Kind) || selected.Target.Id < 0))
            return CommandResult.Reject("Army instruction target is invalid.", "army.instruction.target");
        return order switch
        {
            SetArmyInstructionActionOrder action => execution.SetAction(state, force, action, context),
            SetArmyInstructionTargetOrder target => execution.SetTarget(state, force, target, context),
            SetArmyInstructionRelayOrder relay => execution.SetRelay(state, force, relay, context),
            RetreatArmyInstructionOrder retreat => execution.Retreat(state, force, retreat, context),
            ClearArmyAiActionOrder clear => execution.ClearAction(state, force, clear, context),
            AvoidArmyPoisonOrder avoid => execution.AvoidPoison(state, force, avoid, context),
            RecoverArmyLogisticsOrder recover => execution.RecoverLogistics(state, force, recover, context),
            ExcludeArmyFromPlayerFootholdOrder exclude => execution.ExcludePlayerFoothold(state, force, exclude, context),
            OccupyArmyNeighborCapitalOrder capital => execution.OccupyNeighborCapital(state, force, capital, context),
            OccupyArmyNeighborSceneryOrder scenery => execution.OccupyNeighborScenery(state, force, scenery, context),
            MoveArmyToAdvantagePositionOrder advantage => execution.MoveToAdvantagePosition(state, force, advantage, context),
            MoveArmyToObjectAttackPositionOrder objectAttack => execution.MoveToObjectAttackPosition(state, force, objectAttack, context),
            _ => throw new InvalidOperationException()
        };
    }
}
