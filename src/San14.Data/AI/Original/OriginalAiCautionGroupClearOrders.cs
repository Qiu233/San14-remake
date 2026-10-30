using San14.Core;

namespace San14.Data.AI.Original;

internal sealed record ClearOriginalAiCautionGroupOrder(int ForceId, int GroupId) : GameOrder(ForceId);

/// <summary>The actual clear is a World mutation (members, registry and
/// matching military objectives), unlike the three planning metadata writes.</summary>
internal sealed class OriginalAiCautionGroupClearRules(ArmyCampaignRuleAdapter adapter) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not ClearOriginalAiCautionGroupOrder clear || clear.ForceId != force.Id ||
            state.Phase != TurnPhase.Strategy || (uint)clear.GroupId >= 501)
            return CommandResult.Reject("Caution group clear has an invalid source scope.", "original-ai.caution-clear.scope");
        try
        {
            var current = adapter.CaptureOriginalAiDomesticCurrent(state);
            return CommandResult.Accept(adapter.CancelOriginalAiCautionGroupStrategyCurrent(state, current, clear.GroupId));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }
}

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus ClearCautionGroup(uint input)
    {
        if (!TryEntity(input, out var group) || group.Kind != WorldEntityKind.ArmyCautionGroup)
            return OriginalAiNodeStatus.Failure;
        var result = planning.Apply(new ClearOriginalAiCautionGroupOrder(Actor.ForceId, group.Id),
            new OriginalAiCautionGroupClearRules(Adapter), commandContext, authority);
        LastCommand = result;
        if (result.MissingRuleKey is { } missing) throw new InvalidDataException(missing + ": " + result.Error);
        return result.Accepted ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }
}
