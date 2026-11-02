namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Admits the exact source-head command speech and retires its
    /// intent atomically. Actual playback remains owned by ArmyMapSpeech.</summary>
    public bool TryPrepareArmyCommandSpeech(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, IArmyCommandSpeechRules rules,
        out ArmyCommandSpeechPreparationResult? result, CampaignAppRandomState? appRandomSnapshot = null)
    {
        result = null;
        if (!TryReadSourcePresentationHead(expectedOwner, expectedSource, batchSequence, requestIndex,
                out var owner, out var pending, out var effects, out int batch, out var request) ||
            request.Speech is not { SpeakerKind: CombatParticipantKind.Army } speech) return false;
        bool supported = expectedSource switch
        {
            CampaignEffectSource.ArmyCommandMaintenance => speech.Event is CombatSpeechEvent.ArmyOrderFinished or
                CombatSpeechEvent.ArmyHoldingPosition or CombatSpeechEvent.ArmyDestinationReachedHolding,
            CampaignEffectSource.ArmyMovement => speech.Event == CombatSpeechEvent.ArmyEnteredBase &&
                speech.BaseArrival is not null,
            _ => false
        };
        if (!supported) return false;
        ArgumentNullException.ThrowIfNull(rules);
        var working = pending is null && appRandomSnapshot is not null
            ? owner with { CampaignAppRandom32 = appRandomSnapshot } : owner;
        result = rules.PrepareArmyCommandSpeech(working, speech);
        if (!result.Handled || result.UnresolvedOwner is not null) return false;
        CompleteSourcePresentationHead(result.State, pending, effects, batch, requestIndex,
            result.State.ActiveMonthlyScene, result.State.CoalitionCameraPresentation);
        result = result with { State = SourcePresentationOwner };
        return true;
    }
}
