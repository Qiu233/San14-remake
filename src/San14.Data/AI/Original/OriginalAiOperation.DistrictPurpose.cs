namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus SetDistrictPurpose(sbyte purpose)
    {
        if (Actor.AgentKind == OriginalAiAgentKind.Force) return OriginalAiNodeStatus.Success;
        var preparation = adapter.PrepareOriginalAiDistrictPurpose(InputsCurrent, Actor, purpose);
        if (!preparation.Success) return OriginalAiNodeStatus.Failure;
        // The source unconditionally returns success for its known null/invalid district.
        if (preparation.Order is not { } order) return OriginalAiNodeStatus.Success;
        var result = planning.Apply(order, rules.OrderRules, commandContext, authority);
        LastCommand = result;
        if (result.MissingRuleKey is { } missing)
            throw new InvalidDataException($"original_ai.command.{missing}: {result.Error}");
        return result.Accepted ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }
}
