namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Completes the actual capture-announcement head from the exact
    /// world read by its text/audio host. No generic personnel ACK is used.</summary>
    public bool TryCompleteDuelCaptureAnnouncement(GameState expectedOwner, CampaignEffectSource expectedSource,
        long batchSequence, int requestIndex, DuelCaptureAnnouncementRequest expectedRequest,
        CampaignAppRandomState? appRandomSnapshot = null)
    {
        if (_progress is not null) return false;
        var pending = _normalPeriod;
        var owner = pending?.Candidate ?? State;
        bool hiredCaptureWait = owner.ArmyDefeatContinuation is
            { ResumePoint: ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement,
                HiredCapture: { Phase: HiredArmyCapturePhase.AwaitingAnnouncement } hired } &&
            hired.AnnouncementBatchSequence == batchSequence;
        if (pending is not null && hiredCaptureWait && appRandomSnapshot is null) return false;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !DuelCaptureAnnouncementPlaybackRules.TryComplete(owner, expectedSource, batchSequence,
                requestIndex, expectedRequest, out var next)) return false;
        if (pending is not null && hiredCaptureWait)
            next = next with { CampaignAppRandom32 = appRandomSnapshot! };
        if (pending is not null) _normalPeriod = pending with { Candidate = next };
        else State = next;
        return true;
    }
}
