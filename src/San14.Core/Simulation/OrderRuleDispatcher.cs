namespace San14.Core;

/// <summary>Routes supported original commands through explicitly supplied, independently calibrated rules.</summary>
public sealed class OrderRuleDispatcher(AreaAdministrationRules administration, DeploymentCommandRules deployment,
    GovernanceCommandRules? governance = null, IGovernanceRosterRules? governanceRoster = null) : IOrderRules
{
    private readonly AreaAdministrationRules _administration = administration ?? throw new ArgumentNullException(nameof(administration));
    private readonly DeploymentCommandRules _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
    private readonly GovernanceCommandRules? _governance = governance;

    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context) => order switch
    {
        SaveDeploymentDefaultsOrder defaults => DeploymentDefaultsCommand.Apply(state, defaults, context),
        SetAreaAdministrationOrder or ApplyAreaAdministrationOrder => _administration.Apply(state, force, order, context),
        PrepareDeploymentOrder or CancelPreparedDeploymentOrder => _deployment.Apply(state, force, order, context),
        ExpandGovernanceDepartmentOrder expansion => governanceRoster is IGovernanceExpansionRules expansionRules
            ? expansionRules.ApplyGovernanceExpansion(state, force, expansion, context)
            : CommandResult.RuleUnavailable("rules.governance_expansion", "Department expansion rules are unavailable."),
        SetGovernanceRosterOrder roster => governanceRoster?.ApplyGovernanceRoster(state, force, roster, context) ??
            CommandResult.RuleUnavailable("rules.governance_roster", "Ordered governance rules are unavailable."),
        CompleteGovernanceNarrationOrder narration => governanceRoster?.CompleteGovernanceNarration(state, force, narration, context) ??
            CommandResult.RuleUnavailable("rules.governance_roster", "Ordered governance rules are unavailable."),
        SetGovernanceOrder when governanceRoster is not null && state.NativeArmySimulation is not null =>
            CommandResult.RuleUnavailable("rules.governance_legacy_slots", "Native campaigns use the ordered department roster; legacy six-slot appointments cannot change its saved policy bank."),
        SetGovernanceOrder => _governance?.Apply(state, force, order, context) ??
            CommandResult.RuleUnavailable("rules.governance_command", "Governance command rules are unavailable."),
        _ => CommandResult.Reject("Original command type has not been implemented.", "command.unsupported")
    };
}
