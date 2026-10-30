namespace San14.Core;

public sealed record SetArmyGroupPurposeOrder(int ForceId, int GroupId, ArmyGroupPurpose Purpose)
    : GameOrder(ForceId);
public sealed record SetArmyGroupRelayHexesOrder(int ForceId, int GroupId, IReadOnlyList<int> HexIds)
    : GameOrder(ForceId);

public interface IArmyGroupPurposeExecution
{
    CommandResult SetArmyGroupPurpose(GameState state, ForceState force,
        SetArmyGroupPurposeOrder order, CommandContext context);
    CommandResult SetArmyGroupRelayHexes(GameState state, ForceState force,
        SetArmyGroupRelayHexesOrder order, CommandContext context);
}

public sealed class ArmyGroupPurposeOrders(IOrderRules next, IArmyGroupPurposeExecution? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (SetArmyGroupPurposeOrder or SetArmyGroupRelayHexesOrder))
            return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || order.ForceId != force.Id)
            return CommandResult.Reject("Army group planning requires active strategy control.", "army-group.scope");
        int groupId = order is SetArmyGroupPurposeOrder purpose ? purpose.GroupId : ((SetArmyGroupRelayHexesOrder)order).GroupId;
        int hexCount = checked(state.MapWidth * state.MapHeight);
        if (groupId is < 1 or > 500 || order is SetArmyGroupPurposeOrder selected &&
                !ArmyGroupObjectives.IsPurposeValid(selected.Purpose, hexCount) ||
            order is SetArmyGroupRelayHexesOrder relaySelection && (relaySelection.HexIds is not { Count: <= 10 } ||
                relaySelection.HexIds.Any(hex => hex < 0 || hex >= hexCount)))
            return CommandResult.Reject("Army group purpose or relay selection is invalid.", "army-group.selection");
        // Actual registered-group validity, member-derived district/force and
        // delegated scope belong to the same provider as the source operation.
        // Group zero is only writable inside the real transport creation tail.
        if (rules is null)
            return CommandResult.RuleUnavailable("army-group.purpose_rules", "Army group planning rules are unavailable.");
        return order switch
        {
            SetArmyGroupPurposeOrder goal => rules.SetArmyGroupPurpose(state, force, goal, context),
            SetArmyGroupRelayHexesOrder relay => rules.SetArmyGroupRelayHexes(state, force, relay, context),
            _ => throw new InvalidOperationException("Army group command changed during dispatch.")
        };
    }
}
