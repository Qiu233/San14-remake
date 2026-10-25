namespace San14.Core;

public enum DoctrineResearchOperation { Research, Redefine }
public sealed record DoctrineResearchStep(DoctrineResearchOperation Operation, int? PanelId = null);
public sealed record CommitDoctrineResearchOrder(int ForceId,
    IReadOnlyList<DoctrineResearchStep> Steps) : GameOrder(ForceId);

public interface IDoctrineResearchCommandRules
{
    CommandResult ApplyDoctrineResearch(GameState state, ForceState force,
        IReadOnlyList<DoctrineResearchStep> steps, CommandContext context);
}

/// <summary>Publishes a complete ordered page transaction. The real provider
/// replays eligibility, point changes, reset refunds and final costs in order.</summary>
public sealed class DoctrineResearchOrders(IOrderRules next, IDoctrineResearchCommandRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not CommitDoctrineResearchOrder research) return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Research requires direct strategy control.", "research.scope");
        if (research.Steps is not { Count: > 0 } || research.Steps.Any(step => step is null ||
            (step.Operation switch
            {
                DoctrineResearchOperation.Research => step.PanelId is null or <= 0,
                DoctrineResearchOperation.Redefine => step.PanelId is not null,
                _ => true
            })))
            return CommandResult.Reject("The research operation trace is invalid.", "research.steps");
        // A panel may be researched again after a real redefinition. Source
        // eligibility, rather than a global Distinct filter, owns this rule.
        return rules?.ApplyDoctrineResearch(state, force, research.Steps, context) ??
            CommandResult.RuleUnavailable("research.command_rules", "Research rules are unavailable.");
    }
}
