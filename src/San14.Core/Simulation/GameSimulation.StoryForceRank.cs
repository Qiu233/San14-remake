namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TrySelectNormalTurnStoryKingdomName(GameState expectedOwner, long sequence,
        CampaignStoryForceRankContinuation expectedRequest, int? selectedId)
    {
        if (_progress is not null || _normalPeriod is not { } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) ||
            pending.Candidate.NativeNormalTurnContinuation is not { NextAction: 48, StoryPlayback: { } playback } turn ||
            playback.ExecutionCursor is not { ForceRankContinuation: { } request } cursor ||
            !ReferenceEquals(request, expectedRequest) || request.StorySequence != sequence || playback.Story.Sequence != sequence ||
            request.Source != playback.Story.Source || request.Stage != CampaignStoryForceRankStage.KingdomName ||
            request.KingdomSelectionResolved ||
            selectedId is int selected && !request.KingdomChoices.Any(choice => choice.Id == selected))
            return false;
        var next = request with { KingdomSelectionResolved = true, SelectedKingdomNameId = selectedId };
        _normalPeriod = pending with { Candidate = pending.Candidate with
        {
            NativeNormalTurnContinuation = turn with
            { StoryPlayback = playback with { ExecutionCursor = cursor with { ForceRankContinuation = next } } }
        } };
        return true;
    }

    public bool TryCompleteNormalTurnStoryForceRankMessage(GameState expectedOwner, long sequence,
        CampaignStoryForceRankContinuation expectedRequest)
    {
        if (_progress is not null || _normalPeriod is not { } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) ||
            pending.Candidate.NativeNormalTurnContinuation is not { NextAction: 48, StoryPlayback: { } playback } turn ||
            playback.ExecutionCursor is not { ForceRankContinuation: { } request } cursor ||
            !ReferenceEquals(request, expectedRequest) || request.StorySequence != sequence || playback.Story.Sequence != sequence ||
            request.Source != playback.Story.Source || request.Stage != CampaignStoryForceRankStage.Messages ||
            request.MessagesCompleted || request.NextMessage >= request.Messages.Length)
            return false;
        int index = request.NextMessage + 1;
        var next = request with { NextMessage = index, MessagesCompleted = index == request.Messages.Length };
        _normalPeriod = pending with { Candidate = pending.Candidate with
        {
            NativeNormalTurnContinuation = turn with
            { StoryPlayback = playback with { ExecutionCursor = cursor with { ForceRankContinuation = next } } }
        } };
        return true;
    }
}
