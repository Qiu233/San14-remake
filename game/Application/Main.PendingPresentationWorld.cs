using San14.Core;

public partial class Main
{
    // These source waits own a stopped candidate, not a committed command
    // world. Only their map presentation borrows it across a load or receipt.
    private static bool HasPendingMapTacticPresentation(GameState candidate)
        => candidate.ManualTacticIntervention is { } manager &&
            (manager.EffectBusy || manager.Effects.Length != 0 || manager.Directions.Length != 0);

    private GameState ResidentPresentationWorld(GameState committedRoot)
    {
        if (_periodPlayback is not null || _session is not { } session ||
            !ReferenceEquals(session.World, committedRoot))
            return committedRoot;
        if(session.PendingNormalPeriod is not {} pending)
            return session.PendingManualTacticCandidate ?? session.PendingArmyProgressCandidate ?? session.PendingProgressCandidate ?? committedRoot;
        return pending.Candidate.ArmyReturnCaptiveContinuation is not null ||
            pending.PendingIntelligenceNarrationTaskId is not null ||
            pending.RequiresAutomaticIntelligenceContinuation ||
            BaseCapturePersonPrefixProgress.IsPresentationPending(pending.Candidate) ||
            HiredArmyCaptureProgress.IsPresentationPending(pending.Candidate) ||
            HasPendingMapTacticPresentation(pending.Candidate)
            ? pending.Candidate : committedRoot;
    }
}
