using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Rebinds the actual saved legacy transaction to the campaign's
    /// existing rule services. It neither begins a block nor creates a World.</summary>
    public CampaignNativeProgressRules CreateLegacyCampaignProgressRules(GameState candidate,
        CampaignStoryBindings stories, Action? checkCancellation = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(stories);
        var stages = new LegacyStageSource(this, candidate);
        return new CampaignNativeProgressRules(this, Tactics, stages, stories,
            manualPresentation: _automaticPresentationAdmission,
            checkCancellation: checkCancellation)
        { CreateMovement = CreateLegacyMovementOperation };
    }

    private sealed class LegacyStageSource(ArmyCampaignRuleAdapter package, GameState candidate)
        : IPlayableNativeArmyProgressStageSource
    {
        public IPlayableNativeArmyProgressStageRules BeginOperation() => new OrdinaryArmyCombatStages(
            package, new NativePeriodRemainder(package),
            new NormalArmyForceResultDefinitions(package.CaptureWritableTransportRouteCurrent(candidate)), useMixedCombat: true);
    }
}
