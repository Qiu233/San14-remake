using San14.Core;
using San14.Data;
using San14.Data.AI.Original;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    private readonly OriginalAiStrategyHostConfiguration? _originalAiStrategy;

    private static void RequireUnambiguousPlanner(OriginalAiStrategyHostConfiguration? original,
        Func<PlayableRuleSet, int, ICommandPlanner>? prePeriodPlanner)
    {
        if (original is { Enabled: true } && prePeriodPlanner is not null)
            throw new InvalidOperationException("Choose either the original Strategy lifecycle or a pre-period command planner.");
    }

    // The default composition neither loads nor installs original policies.
    // Explicit selection compiles all roots before changing the candidate;
    // installation refuses an unbound tree without executing any root.
    private GameState AttachOriginalAiStrategy(GameState world, ArmyCampaignRuleAdapter army,
        PlayableRuleSet rules, bool restorePolicyOwner)
    {
        if (_originalAiStrategy is not { Enabled: true } selected) return world;
        if (world.TutorialLessonProgress is not null)
            throw new InvalidOperationException("Original Strategy policy requires a normal campaign.");
        if (army.HasOriginalAiStrategyInstallation) return world;
        return army.InstallOriginalAiStrategyPolicyCurrent(world, _repository.LoadOriginalAiPolicy(),
            selected, rules, restorePolicyOwner);
    }
}
