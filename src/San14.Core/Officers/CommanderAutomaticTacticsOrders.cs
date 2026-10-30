namespace San14.Core;

/// <summary>The selected commander's source automatic-tactic checkmark.</summary>
public sealed record ToggleCommanderAutomaticTacticsOrder(int ForceId, int OfficerId) : GameOrder(ForceId);

public interface ICommanderAutomaticTacticsCommandRules
{
    CommandResult ToggleCommanderAutomaticTactics(GameState state, ForceState force, int officerId, CommandContext context);
}

public static class CommanderAutomaticTacticsCommandApplication
{
    /// <summary>The same authority boundary for a genuinely stopped period
    /// candidate. No clock or presentation acknowledgement is manufactured.</summary>
    public static CommandResult Apply(GameState owner, ToggleCommanderAutomaticTacticsOrder order,
        ICommanderAutomaticTacticsCommandRules rules, CommandContext context, IOrderAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(rules); ArgumentNullException.ThrowIfNull(authority);
        var denied = GameEngine.CheckOrder(owner, order, context, authority);
        if (denied is not null) return denied;
        var result = ApplyValidated(owner, owner.Forces[order.ForceId], order, context, rules);
        if (result.Accepted)
        {
            var next = result.State!;
            GameEngine.ValidateWorldIdentity(owner, next);
            if (next.Phase != owner.Phase || next.Date != owner.Date || next.PeriodNumber != owner.PeriodNumber ||
                next.NativeClock != owner.NativeClock)
                throw new InvalidDataException("Commander automatic tactics changed the simulation boundary.");
        }
        return result;
    }

    internal static CommandResult ApplyValidated(GameState state, ForceState force,
        ToggleCommanderAutomaticTacticsOrder order, CommandContext context, ICommanderAutomaticTacticsCommandRules rules)
    {
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Commander tactics require direct force control.", "commander-automatic.scope");
        if (order.OfficerId is < 1 or > 6000)
            return CommandResult.Reject("The commander identity is invalid.", "commander-automatic.person");
        return rules.ToggleCommanderAutomaticTactics(state, force, order.OfficerId, context);
    }
}
