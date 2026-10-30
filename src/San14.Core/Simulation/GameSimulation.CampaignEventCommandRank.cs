namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TrySelectCampaignEventCommandKingdomName(GameState expectedOwner, long sequence,
        CampaignStoryForceRankContinuation expectedRequest, int? selectedId)
    {
        if (!EventCommandOwner(expectedOwner) || State.CampaignEventCommandPlayback?.Invocation is not { } playback ||
            playback.ExecutionCursor is not { ForceRankContinuation: { } request } cursor ||
            !ReferenceEquals(request, expectedRequest) || request.StorySequence != sequence || playback.Story.Sequence != sequence ||
            request.Source != playback.Story.Source || request.Stage != CampaignStoryForceRankStage.KingdomName ||
            request.KingdomSelectionResolved ||
            selectedId is int selected && !request.KingdomChoices.Any(choice => choice.Id == selected))
            return false;
        var next = request with { KingdomSelectionResolved = true, SelectedKingdomNameId = selectedId };
        StoreEventCommandInvocation(playback with { ExecutionCursor = cursor with { ForceRankContinuation = next } });
        return true;
    }

    public bool TryCompleteCampaignEventCommandForceRankMessage(GameState expectedOwner, long sequence,
        CampaignStoryForceRankContinuation expectedRequest)
    {
        if (!EventCommandOwner(expectedOwner) || State.CampaignEventCommandPlayback?.Invocation is not { } playback ||
            playback.ExecutionCursor is not { ForceRankContinuation: { } request } cursor ||
            !ReferenceEquals(request, expectedRequest) || request.StorySequence != sequence || playback.Story.Sequence != sequence ||
            request.Source != playback.Story.Source || request.Stage != CampaignStoryForceRankStage.Messages ||
            request.MessagesCompleted || request.NextMessage >= request.Messages.Length)
            return false;
        int index = request.NextMessage + 1;
        var next = request with { NextMessage = index, MessagesCompleted = index == request.Messages.Length };
        StoreEventCommandInvocation(playback with { ExecutionCursor = cursor with { ForceRankContinuation = next } });
        return true;
    }
}
