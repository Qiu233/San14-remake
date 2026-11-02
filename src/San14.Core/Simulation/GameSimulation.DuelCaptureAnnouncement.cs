namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Completes the actual capture-announcement head from the exact
    /// world read by its text/audio host. No generic personnel ACK is used.</summary>
    public bool TryCompleteDuelCaptureAnnouncement(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, DuelCaptureAnnouncementRequest expectedRequest,
        CampaignAppRandomState? appRandomSnapshot = null)
    {
        if (_progress is { ArmyProgress: null }) return false;
        var pending = _normalPeriod;
        var owner = SourcePresentationOwner;
        bool retainedOwner = pending is not null || _progress?.ArmyProgress is not null;
        bool hiredCaptureWait = owner.ArmyDefeatContinuation is
            { ResumePoint: ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement,
                HiredCapture: { Phase: HiredArmyCapturePhase.AwaitingAnnouncement } hired } &&
            hired.AnnouncementBatchSequence == batchSequence;
        if (retainedOwner && hiredCaptureWait && appRandomSnapshot is null) return false;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !DuelCaptureAnnouncementPlaybackRules.TryComplete(owner, expectedSource, batchSequence,
                requestIndex, expectedRequest, out var next)) return false;
        if (retainedOwner && hiredCaptureWait)
            next = next with { CampaignAppRandom32 = appRandomSnapshot! };
        PublishSourcePresentationOwner(next, pending);
        return true;
    }
}
