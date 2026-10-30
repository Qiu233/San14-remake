using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class GameplaySession
{
    public bool TryPrepareFireAnnouncement(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence, int requestIndex, CampaignEffectRequest expectedRequest,
        IFireAnnouncementRules rules, out FireAnnouncementPreparationResult? result,
        CampaignAppRandomState? appRandomSnapshot = null) =>
        _simulation.TryPrepareFireAnnouncement(expectedOwner, source, batchSequence, requestIndex,
            expectedRequest, rules, out result, appRandomSnapshot);

    public bool TryCompleteFirePresentation(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence, int requestIndex, CampaignEffectRequest expectedRequest) =>
        _simulation.TryCompleteFirePresentation(expectedOwner, source, batchSequence, requestIndex, expectedRequest);

    public bool TryCompleteDamagePresentation(GameState expectedOwner, CampaignEffectSource source,
        long batchSequence, int requestIndex, CampaignEffectRequest expectedRequest) =>
        _simulation.TryCompleteDamagePresentation(expectedOwner, source, batchSequence, requestIndex, expectedRequest);
}
