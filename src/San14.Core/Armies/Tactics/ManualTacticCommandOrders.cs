namespace San14.Core;

/// <summary>A player's selected tactical command. The target is the retained
/// physical Hex, independent of a moving Army or Base presentation.</summary>
public sealed record IssueManualTacticInterventionOrder(int ForceId,int UnitId,int TacticId,int TargetHexId)
    :GameOrder(ForceId);

public interface IManualTacticCommandRules
{
    /// <summary>Returns a new singleton owner. It must not mutate borrowed
    /// world collections, execute effects or manufacture a progress moment.</summary>
    CommandResult IssueManualTacticCommand(GameState state,UnitState actor,int tacticId,int targetHexId,
        bool? progressAllowsReplacement);
}

/// <summary>The same authority and domain submission for a committed session
/// or a genuinely stopped worker's borrowed playback candidate.</summary>
public static class ManualTacticCommandApplication
{
    public static CommandResult Apply(GameState owner,IssueManualTacticInterventionOrder order,
        IManualTacticCommandRules rules,CommandContext context,IOrderAuthority authority,
        bool? progressAllowsReplacement)
    {
        ArgumentNullException.ThrowIfNull(owner);ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(rules);ArgumentNullException.ThrowIfNull(authority);
        var denied=GameEngine.CheckOrder(owner,order,context,authority);
        if(denied is not null)return denied;
        if(order.TacticId is <0 or >200||order.TargetHexId is <0 or >=48400)
            return CommandResult.Reject("The manual tactic or target is invalid.","manual-tactic.target");
        if(!PlayableUnitControl.TryResolve(owner,owner.Forces[order.ForceId],order.UnitId,context,
            out var actor,out denied))return denied!;
        var result=rules.IssueManualTacticCommand(owner,actor!,order.TacticId,order.TargetHexId,
            progressAllowsReplacement);
        if(result.Accepted)
        {
            //This provider owns only the immutable singleton replacement.
            //It neither clones the whole world nor advances its clock.
            var next=result.State!;
            GameEngine.ValidateWorldIdentity(owner,next);
            if(next.Phase!=owner.Phase||next.Date!=owner.Date||next.PeriodNumber!=owner.PeriodNumber||
                next.NativeClock!=owner.NativeClock)
                throw new InvalidDataException("Manual tactic submission changed the simulation boundary.");
        }
        return result;
    }
}
