namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Receipts only the reached departure after its actual model and
    /// known layout work have both been accepted by the view owner.</summary>
    public bool TryCompleteArmyDeparturePresentation(GameState expectedOwner, long batchSequence, int requestIndex)
    {
        if (!TryReadSourcePresentationHead(expectedOwner, CampaignEffectSource.ArmyMovement,
            batchSequence, requestIndex, out var owner, out var pending, out var effects,
            out int batch, out var request) || request.ArmyDeparture is not { RebuildInfoLayout: not null, SourceSnapshot: not null }) return false;
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }
}
