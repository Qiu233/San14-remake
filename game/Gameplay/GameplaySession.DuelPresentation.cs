using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public bool TryCompleteDuelCaptureAnnouncement(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, DuelCaptureAnnouncementRequest expectedRequest) =>
        _simulation.TryCompleteDuelCaptureAnnouncement(expectedOwner, expectedSource,
            batchSequence, requestIndex, expectedRequest);

    public bool TryCompleteDuelPresentation(GameState expectedOwner,long sequence,GameState preparedState) =>
        _simulation.TryCompleteDuelPresentation(expectedOwner,sequence,preparedState);
}
