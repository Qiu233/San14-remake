namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryPrepareFacilityArmySpeech(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, IFacilityArmySpeechRules rules,
        out FacilityArmySpeechPreparationResult? result, CampaignAppRandomState? appRandomSnapshot = null)
    {
        result = null;
        if (!TryReadSourcePresentationHead(expectedOwner, expectedSource, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.Speech is not { } speech) return false;
        bool supported = expectedSource switch
        {
            CampaignEffectSource.ArmyFacilityConstruction => speech.Event == CombatSpeechEvent.ArmyFacilityConstructionStarted,
            CampaignEffectSource.ArmyFacilityRepair => speech.Event == CombatSpeechEvent.ArmyFacilityCompleted,
            CampaignEffectSource.FacilityEffects => speech.Event is CombatSpeechEvent.ConfusionApplied or
                CombatSpeechEvent.ImmobilizationApplied ||
                speech.Event == CombatSpeechEvent.ArmyUnderFacilityAttack && speech.FacilityDefinitionId is 5 or 6 or 15,
            CampaignEffectSource.FacilityTraps => speech.Event is CombatSpeechEvent.TrapPitfall or
                CombatSpeechEvent.TrapRockfall or CombatSpeechEvent.TrapCountered or
                CombatSpeechEvent.ConfusionApplied or CombatSpeechEvent.ImmobilizationApplied,
            _ => false
        };
        if (!supported) return false;
        ArgumentNullException.ThrowIfNull(rules);
        var working = pending is null && appRandomSnapshot is not null
            ? owner with { CampaignAppRandom32 = appRandomSnapshot } : owner;
        result = rules.PrepareFacilityArmySpeech(working, speech);
        if (!result.Handled || result.UnresolvedOwner is not null) return false;
        CompleteSourcePresentationHead(result.State, pending, effects, batch, requestIndex,
            result.State.ActiveMonthlyScene, result.State.CoalitionCameraPresentation);
        result = result with { State = _normalPeriod?.Candidate ?? State };
        return true;
    }
}
