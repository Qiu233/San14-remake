namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Retires only the reached repair request after its actual
    /// model, label or sound work has been accepted by the host.</summary>
    public bool TryCompleteFacilityRepairPresentation(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence, int requestIndex, FacilityRepairPresentationOperation operation)
    {
        if (source != CampaignEffectSource.ArmyFacilityRepair || !Enum.IsDefined(operation) ||
            !TryReadSourcePresentationHead(expectedOwner, source, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.FacilityRepairPresentation?.Operation != operation) return false;
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }
}
