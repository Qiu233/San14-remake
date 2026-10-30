using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private OriginalAiStrategyInstallation? originalAiStrategyInstallation;

    /// <summary>Composition selects this explicitly. The ordinary host leaves
    /// it false and continues using its replaceable basic ICommandPlanner.</summary>
    public bool HasOriginalAiStrategyInstallation => originalAiStrategyInstallation is not null;

    /// <summary>Optional installation against the current rules/normal
    /// resources. Four roots are compiled and must be completely bound before
    /// either this adapter or the candidate world is installed. No tree runs.
    /// Restored/worker adapters use the known saved owner; they never create
    /// unknown policy state or set an application load flag.</summary>
    public GameState InstallOriginalAiStrategyPolicyCurrent(GameState state, LoadedOriginalAiPolicy policy,
        OriginalAiStrategyHostConfiguration configuration, PlayableRuleSet rules, bool restorePolicyOwner)
    {
        if (!configuration.Enabled) throw new ArgumentException("Original policy installation must be explicit.", nameof(configuration));
        if (!ReferenceEquals(rules.OrdinaryDeploymentRules, this)) throw InstallMissing("rules_adapter");
        if (originalAiStrategyInstallation is not null) throw InstallMissing("already_installed");
        var descriptors = policy.Behavior.Document.Bundles;
        if (descriptors.Length != 4) throw InstallMissing("four_normal_root_descriptors");
        // 01E1F0's descriptor order and the four actual registration callers
        // identify these roles. Resource keys are read from this loaded file.
        var roots = new OriginalAiStrategyRoots(descriptors[0].ResourceKey, descriptors[1].ResourceKey,
            descriptors[2].ResourceKey, descriptors[3].ResourceKey);
        var installation = new OriginalAiStrategyInstallation(this, policy, roots, configuration, rules);
        var next = restorePolicyOwner ? installation.RequireRestoredOwner(state)
            : ConstructOriginalAiPolicyCurrent(state, policy, roots);
        originalAiStrategyInstallation = installation;
        return next;
    }

    public void DetachOriginalAiStrategyPolicy() => originalAiStrategyInstallation = null;

    private sealed class OriginalAiStrategyInstallation
    {
        private readonly ArmyCampaignRuleAdapter adapter;
        private readonly ICommandPlanner beforePlayer;
        private readonly ICommandPlanner afterPlayer;
        private readonly OriginalAiPolicySource source;
        private readonly OriginalAiPolicyRootBundles roots;

        internal OriginalAiStrategyInstallation(ArmyCampaignRuleAdapter adapter, LoadedOriginalAiPolicy policy,
            OriginalAiStrategyRoots roots, OriginalAiStrategyHostConfiguration configuration,
            PlayableRuleSet rules)
        {
            this.adapter = adapter;
            source = new(policy.Behavior.Source.ResourceUri, policy.Behavior.Source.Sha256,
                policy.Scripts.Source.ResourceUri, policy.Scripts.Source.Sha256);
            this.roots = new(roots.Force, roots.District, roots.MilitaryGroup, roots.Army);
            // Compile-cache lookups only. Neither factory invokes a root.
            beforePlayer = OriginalAiStrategyQueuedPlanner.CreateBeforePlayerForRules(policy, roots, configuration, rules);
            afterPlayer = OriginalAiStrategyQueuedPlanner.CreateAfterPlayerForRules(policy, roots, configuration, rules);
        }

        internal GameState RequireRestoredOwner(GameState state)
        {
            if (state.OriginalAiPolicyExecution is not { } saved || saved.Source != source || saved.RootBundles != roots)
                throw InstallMissing("saved_policy_source_and_roots");
            if (state.OriginalAiStrategySchedule is null) throw InstallMissing("saved_strategy_schedule");
            return state;
        }

        internal GameState EnterNewStrategy(GameState state)
        {
            // The existing ordinary load lifecycle is the actual2220 gate:
            // successful load sets it; the normal Progress initializer clears
            // it. Worker adapters borrow this same application owner.
            var application = adapter.applicationGameplay ?? throw InstallMissing("application_load_lifecycle");
            var prepared = adapter.ConstructOriginalAiStrategyScheduleCurrent(state, application.StrategyLoadResume);
            return CommandPlanningContext.PlanOwned(prepared, beforePlayer);
        }

        internal GameState ExitPlayerStrategy(GameState state)
        {
            return CommandPlanningContext.PlanOwned(state, afterPlayer);
        }
    }

    private static InvalidDataException InstallMissing(string owner) => new("original_ai.strategy_installation." + owner);
}
