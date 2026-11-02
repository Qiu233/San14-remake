using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public CampaignEventCommandPlayback? PendingCampaignEventCommand => _simulation.PendingCampaignEventCommand;

    public bool TryBeginCampaignEventCommand(GameState expectedOwner, int eventId,
        ICampaignEventCommandRules rules, out string? unresolvedOwner,
        CampaignAppRandomState? appRandomSnapshot = null) =>
        _simulation.TryBeginCampaignEventCommand(expectedOwner, eventId, rules, out unresolvedOwner, appRandomSnapshot);

    public bool TryContinueCampaignEventCommand(GameState expectedOwner,
        ICampaignEventCommandRules rules, out string? unresolvedOwner) =>
        _simulation.TryContinueCampaignEventCommand(expectedOwner, rules, out unresolvedOwner);

    // The manual command stores its invocation on the actual session World.
    // The existing normal APIs keep their private-period candidate semantics.
    public bool TryPrepareCampaignStoryStep(GameState expectedOwner, ICampaignStoryPreparationRules rules,
        out CampaignStoryPreparationResult result) => PendingProgressCandidate is not null
        ? _simulation.TryPrepareLegacyStoryStep(expectedOwner, rules, out result)
        : PendingCampaignEventCommand is not null
        ? _simulation.TryPrepareCampaignEventCommandStep(expectedOwner, rules, out result)
        : TryPrepareNormalTurnStoryStep(expectedOwner, rules, out result);

    public bool TryCompleteCampaignStoryStep(GameState expectedOwner, long sequence, int step) =>
        TryCompleteCampaignStoryStep(expectedOwner, new CampaignStoryStepReceipt(sequence, step));

    public bool TryCompleteCampaignStoryStep(GameState expectedOwner, CampaignStoryStepReceipt receipt) =>
        PendingProgressCandidate is not null
            ? _simulation.TryCompleteLegacyStoryStep(expectedOwner, receipt)
            : PendingCampaignEventCommand is not null
            ? _simulation.TryCompleteCampaignEventCommandStep(expectedOwner, receipt)
            : TryCompleteNormalTurnStoryStep(expectedOwner, receipt);

    public bool TrySelectCampaignStoryKingdomName(GameState expectedOwner, long sequence,
        CampaignStoryForceRankContinuation expectedRequest, int? selectedId) =>
        PendingCampaignEventCommand is not null
            ? _simulation.TrySelectCampaignEventCommandKingdomName(expectedOwner, sequence, expectedRequest, selectedId)
            : TrySelectNormalTurnStoryKingdomName(expectedOwner, sequence, expectedRequest, selectedId);

    public bool TryCompleteCampaignStoryForceRankMessage(GameState expectedOwner, long sequence,
        CampaignStoryForceRankContinuation expectedRequest) => PendingCampaignEventCommand is not null
        ? _simulation.TryCompleteCampaignEventCommandForceRankMessage(expectedOwner, sequence, expectedRequest)
        : TryCompleteNormalTurnStoryForceRankMessage(expectedOwner, sequence, expectedRequest);

    public bool TryBeginCampaignStoryAudio(GameState expectedOwner, long sequence,
        NormalTurnStoryAudioReturn snapshot) => PendingCampaignEventCommand is not null
        ? _simulation.TryBeginCampaignEventCommandAudio(expectedOwner, sequence, snapshot)
        : TryBeginNormalTurnStoryAudio(expectedOwner, sequence, snapshot);

    public bool TryCompleteCampaignStoryAudio(GameState expectedOwner, long sequence,
        NormalTurnStoryAudioReturn snapshot) => PendingCampaignEventCommand is not null
        ? _simulation.TryCompleteCampaignEventCommandAudio(expectedOwner, sequence, snapshot)
        : TryCompleteNormalTurnStoryAudio(expectedOwner, sequence, snapshot);

    public bool TryCompleteCampaignStoryResult(GameState expectedOwner, long sequence,
        NormalTurnStoryResultSummary expectedSummary) => PendingCampaignEventCommand is not null
        ? _simulation.TryCompleteCampaignEventCommandResult(expectedOwner, sequence, expectedSummary)
        : TryCompleteNormalTurnStoryResult(expectedOwner, sequence, expectedSummary);
}
