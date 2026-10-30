namespace San14.Core;

/// <summary>Cancel an actual Army still at the selected City's deployment
/// source. Army identifies its reusable receiver, not a logical Unit ID or
/// an entry in PreparedDeployments.</summary>
public sealed record CancelCityArmyDeploymentOrder(int ForceId, int CityId, ArmyReceiverId Army)
    : GameOrder(ForceId);

/// <summary>The current-world provider resolves the receiver in the actual
/// roster, rechecks the City's cancellation candidates and owns the complete
/// return, pending retirement and command-book refund transaction.</summary>
public interface IPlayableDeploymentCancellationRules
{
    CommandResult CancelDeployment(GameState state, ForceState force,
        CancelCityArmyDeploymentOrder order, CommandContext context);
}

/// <summary>The ordinary City command uses its current District authority.
/// This wrapper does not require a logical Unit: a genuine pending receiver
/// can still be a source-backed cancellation candidate.</summary>
public sealed class CityArmyDeploymentCancellationOrders(IOrderRules next,
    IPlayableDeploymentCancellationRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not CancelCityArmyDeploymentOrder cancel)
            return next.Apply(state, force, order, context);
        if (state.Phase != TurnPhase.Strategy || !force.IsActive || cancel.ForceId != force.Id)
            return CommandResult.Reject("Army deployment cancellation requires active strategy control.", "deployment-cancel.scope");
        if (!state.Cities.TryGetValue(cancel.CityId, out var city) || city.ForceId != force.Id)
            return CommandResult.Reject("City is not controlled by this force.", "deployment-cancel.city_control");
        if (city.DistrictId is not int districtId || !state.Districts.TryGetValue(districtId, out var district))
            return CommandResult.RuleUnavailable("rules.deployment_cancellation_district", "City's current district is unresolved.");
        if (district.ForceId != force.Id ||
            (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1))
            return CommandResult.Reject("City is outside the authorized district.", "deployment-cancel.district_scope");
        // Receiver identities are not bounded by the native roster's physical
        // capacity. The provider performs that actual receiver-to-slot lookup.
        if (cancel.Army.Value <= 0)
            return CommandResult.Reject("Army receiver identity is invalid.", "deployment-cancel.army_receiver");

        // The return can change City/District ownership. Refund accounting
        // therefore belongs to the provider's actual post-return owner, not
        // the district used here for pre-command permission.
        return rules?.CancelDeployment(state, force, cancel, context) ??
            CommandResult.RuleUnavailable("rules.deployment_cancellation", "Current Army deployment cancellation rules are unavailable.");
    }
}
