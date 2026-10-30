namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Consumes the exact source operation only after the real facility
    /// model, notice, connection or sound owner has accepted it.</summary>
    public bool TryCompleteFacilityConstructionPresentation(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence, int requestIndex, FacilityConstructionPresentationOperation operation)
    {
        if (source != CampaignEffectSource.ArmyFacilityConstruction || !Enum.IsDefined(operation) ||
            !TryReadSourcePresentationHead(expectedOwner, source, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.FacilityConstructionPresentation?.Operation != operation) return false;
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }
}
