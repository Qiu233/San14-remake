namespace San14.Core;

/// <summary>Routes supported original commands through explicitly supplied, independently calibrated rules.</summary>
public sealed class OriginalOrderRules(OriginalAdministrationRules administration, OriginalDeploymentRules deployment,
    OriginalGovernanceRules? governance = null) : IOrderRules
{
    private readonly OriginalAdministrationRules _administration = administration ?? throw new ArgumentNullException(nameof(administration));
    private readonly OriginalDeploymentRules _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
    private readonly OriginalGovernanceRules? _governance = governance;

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context) => order switch
    {
        SetAreaAdministrationOrder => _administration.Apply(state, force, order, context),
        PrepareDeploymentOrder or CancelPreparedDeploymentOrder => _deployment.Apply(state, force, order, context),
        SetGovernanceOrder => _governance?.Apply(state, force, order, context) ??
            CommandResult.RuleUnavailable("rules.governance_command", "Governance command rules are unavailable."),
        _ => CommandResult.Reject("Original command type has not been implemented.", "command.unsupported")
    };
}
