namespace San14.Core;

public sealed partial class GameSimulation
{
    public CommandResult IssueManualTacticCommand(GameState expectedOwner,IssueManualTacticInterventionOrder order,
        IManualTacticCommandRules rules,CommandContext context,IOrderAuthority authority,
        bool? progressAllowsReplacement,GameState? preparedCandidate=null)
    {
        if(HasPendingOperation||!ReferenceEquals(State,expectedOwner))
            return CommandResult.Reject("The manual tactic command owner has changed.","manual-tactic.stale_owner");
        if(preparedCandidate is not null)CheckAuxiliaryTransition(State,preparedCandidate);
        var result=ManualTacticCommandApplication.Apply(preparedCandidate??State,order,rules,context,authority,progressAllowsReplacement);
        if(result.Accepted)State=result.State!;
        return result;
    }
}
