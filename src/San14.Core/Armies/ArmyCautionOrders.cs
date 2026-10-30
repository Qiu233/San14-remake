namespace San14.Core;

/// <summary>Create a caution relation targeting the current army and the
/// resource map-object identity of a City/Gate. BaseMapObjectId is not a
/// reusable MapObjectReceiverId or a Gate table ordinal.</summary>
public sealed record CreateArmyCautionOrder(int ForceId, int UnitId, int BaseMapObjectId)
    : GameOrder(ForceId);

/// <summary>Clear invalid caution relations from this ordered selection.
/// This retires relations, never their target armies. Repeated IDs retain
/// their caller order and are rechecked by the current-world provider.</summary>
public sealed record ClearInvalidArmyCautionsOrder(int ForceId, IReadOnlyList<int> RelationIds)
    : GameOrder(ForceId);

/// <summary>Transient relation-creation outcome, never saved as world state.
/// A false TargetLinked still preserves the accepted relation reset and its
/// actual registry membership; it does not reject that world mutation.</summary>
public sealed record ArmyCautionCreation(int RelationId, bool TargetLinked);

public interface IArmyCautionCommandRules
{
    CommandResult CreateArmyCaution(GameState state, ForceState force,
        CreateArmyCautionOrder order, CommandContext context);
    CommandResult ClearInvalidArmyCautions(GameState state, ForceState force,
        ClearInvalidArmyCautionsOrder order, CommandContext context);
}

/// <summary>Shared actor authority is checked by GameEngine. The target army
/// may belong to another force. Actual target validity, allocation, registration
/// and the base-force relationship gate belong to the current-world provider.</summary>
public sealed class ArmyCautionOrders(IOrderRules next, IArmyCautionCommandRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (CreateArmyCautionOrder or ClearInvalidArmyCautionsOrder))
            return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || order.ForceId != force.Id)
            return CommandResult.Reject("Army cautions require active strategy control.", "army-caution.scope");
        if (order is ClearInvalidArmyCautionsOrder clear)
        {
            if (clear.RelationIds is null || clear.RelationIds.Any(id => id is < 0 or > 1000))
                return CommandResult.Reject("Army caution relation selection is invalid.", "army-caution.relations");
            return rules?.ClearInvalidArmyCautions(state, force, clear, context) ??
                CommandResult.RuleUnavailable("army-caution.command_rules", "Army caution clearing rules are unavailable.");
        }
        var caution = (CreateArmyCautionOrder)order;
        if (caution.UnitId < 0 || !state.Units.ContainsKey(caution.UnitId) ||
            caution.BaseMapObjectId is < 0 or > 61)
            return CommandResult.Reject("Army caution target references are invalid.", "army-caution.targets");
        // Resource Base zero is allowed through the format boundary; its real
        // validity and ownership are resolved once by the source-backed provider.
        // Never reinterpret that resource identity as a map-object receiver.
        return rules?.CreateArmyCaution(state, force, caution, context) ??
            CommandResult.RuleUnavailable("army-caution.command_rules", "Army caution creation rules are unavailable.");
    }
}
