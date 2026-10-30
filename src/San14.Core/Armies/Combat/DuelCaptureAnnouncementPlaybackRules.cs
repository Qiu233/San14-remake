namespace San14.Core;

/// <summary>Receipt issued only after the host completed the actual capture
/// text and sound. Both normal and borrowed period owners use this source FIFO.</summary>
public static class DuelCaptureAnnouncementPlaybackRules
{
    public static bool TryComplete(GameState owner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, DuelCaptureAnnouncementRequest expectedRequest,
        out GameState next)
    {
        next = owner;
        if (expectedSource != CampaignEffectSource.ArmyBattleDefeat || expectedRequest is null || expectedRequest.TextAtSource is null ||
            owner.CampaignEffectRequests is not { } effects) return false;
        for (int batchIndex = 0; batchIndex < effects.PendingBatches.Length; batchIndex++)
        {
            var batch = effects.PendingBatches[batchIndex];
            if (batch.Source != expectedSource) continue;
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                var request = batch.Requests[index];
                //History is already committed. Every other earlier request,
                //including an unknown kind, still belongs to its real consumer.
                if (request.HistoryNotification is not null) continue;
                if (batch.Sequence != batchSequence || index != requestIndex ||
                    !ReferenceEquals(request.DuelCaptureAnnouncement, expectedRequest)) return false;
                var defeat = owner.ArmyDefeatContinuation;
                var hired = defeat?.HiredCapture;
                bool completesHiredCapture = hired?.AnnouncementBatchSequence == batchSequence;
                if (completesHiredCapture &&
                    (defeat!.ResumePoint != ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement ||
                        hired!.Phase != HiredArmyCapturePhase.AwaitingAnnouncement || batch.Requests.Length != 1 ||
                        expectedRequest.MessageRoot != 0x34AA)) return false;
                CampaignEffectBatch[] batches;
                if (batch.Requests.Length == 1)
                    batches = effects.PendingBatches.Where((_, at) => at != batchIndex).ToArray();
                else
                {
                    batches = (CampaignEffectBatch[])effects.PendingBatches.Clone();
                    batches[batchIndex] = batch with
                    { Requests = batch.Requests.Where((_, at) => at != index).ToArray() };
                }
                next = owner with
                {
                    CampaignEffectRequests = effects with { PendingBatches = batches },
                    ArmyDefeatContinuation = completesHiredCapture
                        ? defeat! with { HiredCapture = hired! with { Phase = HiredArmyCapturePhase.ReadyToRelease } }
                        : defeat
                };
                return true;
            }
        }
        return false;
    }
}
