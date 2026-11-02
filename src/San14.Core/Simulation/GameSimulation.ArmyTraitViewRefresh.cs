namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Consumes the first queued trait-view refresh after the actual
    /// render owner has refreshed or retired that Army's view.</summary>
    public bool TryCompleteArmyTraitViewRefresh(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex)
    {
        if (_progress is { ArmyProgress: null }) return false;
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !ArmyViewPresentationPlaybackRules.TryCompleteTrait(owner, expectedSource, batchSequence, requestIndex, out var completed)) return false;
        PublishSourcePresentationOwner(completed, _normalPeriod);
        return true;
    }
}
