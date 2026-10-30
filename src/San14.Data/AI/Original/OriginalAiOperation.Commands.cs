using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>A source node may legitimately finish without an order. Unknown
/// inputs throw before constructing this result; they are never a known skip.</summary>
public readonly record struct OriginalAiCommandPreparation(bool Success, GameOrder? Order);

public sealed partial class OriginalAiOperation
{
    /// <summary>All restored native commands use the current borrowed planning
    /// transaction and ordinary authority. No original-policy exemption exists.</summary>
    internal OriginalAiNodeStatus ApplyNativeCommand(GameOrder order)
    {
        if (order.ForceId != Actor.ForceId) return OriginalAiNodeStatus.Failure;
        var result = planning.Apply(order, rules.OrderRules, commandContext, authority);
        LastCommand = result;
        if (result.MissingRuleKey is { } missing)
            throw new InvalidDataException($"original_ai.command.{missing}: {result.Error}");
        return result.Accepted ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }
}
