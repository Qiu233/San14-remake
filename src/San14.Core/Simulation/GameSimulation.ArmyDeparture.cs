namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Receipts only the reached departure after its actual model and
    /// known layout work have both been accepted by the view owner.</summary>
    public bool TryCompleteArmyDeparturePresentation(GameState expectedOwner, long batchSequence, int requestIndex)
    {
        var owner = SourcePresentationOwner;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !ArmyViewPresentationPlaybackRules.TryCompleteDeparture(owner, batchSequence, requestIndex, out var completed)) return false;
        PublishSourcePresentationOwner(completed, _normalPeriod);
        return true;
    }
}
