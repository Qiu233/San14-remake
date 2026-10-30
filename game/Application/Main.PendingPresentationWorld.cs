using San14.Core;

public partial class Main
{
    // These source waits own a stopped candidate, not a committed command
    // world. Only their map presentation borrows it across a load or receipt.
    private GameState ResidentPresentationWorld(GameState committedRoot)
    {
        if (_periodPlayback is not null || _session is not { } session ||
            !ReferenceEquals(session.World, committedRoot) || session.PendingNormalPeriod is not { } pending)
            return committedRoot;
        return pending.PendingIntelligenceNarrationTaskId is not null ||
            pending.RequiresAutomaticIntelligenceContinuation ||
            BaseCapturePersonPrefixProgress.IsPresentationPending(pending.Candidate) ||
            HiredArmyCaptureProgress.IsPresentationPending(pending.Candidate)
            ? pending.Candidate : committedRoot;
    }
}
