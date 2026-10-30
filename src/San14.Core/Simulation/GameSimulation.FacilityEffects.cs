namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Consumes only the real host's accepted periodic facility or
    /// trap request, including a source-known cull. Unknown work stays at its
    /// source head, and history retains its own notification consumer.</summary>
    public bool TryCompleteFacilityEffectsPresentation(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence, int requestIndex, CampaignEffectRequest expectedRequest)
    {
        if (source is not (CampaignEffectSource.FacilityEffects or CampaignEffectSource.FacilityTraps) ||
            !TryReadSourcePresentationHead(expectedOwner, source, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            !ReferenceEquals(request, expectedRequest)) return false;
        bool supported = source == CampaignEffectSource.FacilityEffects
            ? request.FacilityAuraReset is not null || request.MilitaryMusicPulse is not null ||
                request.FacilityProjectile is not null || request.FacilityStatisticPresentation is not null ||
                request.FacilitySupportAura is not null || request.ArmyFacilityDamagePresentation is not null || request.ArmyTrapPresentation is
                    { Operation: ArmyTrapPresentationOperation.TargetViewRefresh, DefinitionId: 5 or 6 or 15 } or
                    { Operation: ArmyTrapPresentationOperation.AbnormalConfusionStarted or ArmyTrapPresentationOperation.AbnormalImmobilizationStarted, DefinitionId: 8 }
            : request.FacilityTrapPresentation is not null || request.ArmyTrapPresentation is not null ||
                request.ArmyFacilityDamagePresentation is not null;
        if (!supported) return false;
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }
}
