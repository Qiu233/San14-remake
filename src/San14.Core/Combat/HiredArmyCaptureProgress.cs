namespace San14.Core;

public enum HiredArmyCapturePhase { AwaitingAnnouncement, ReadyToRelease }

/// <summary>The reached hired-commander capture announcement retains its
/// actual outbox sequence. The completed defeat prefix is never replayed.</summary>
public sealed record HiredArmyCaptureProgress(HiredArmyCapturePhase Phase, long AnnouncementBatchSequence)
{
    public static bool IsAwaitingAnnouncement(GameState state) => state.ArmyDefeatContinuation is
        { ResumePoint: ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement,
            HiredCapture: { Phase: HiredArmyCapturePhase.AwaitingAnnouncement } };

    public static bool IsPresentationPending(GameState state) => state.ArmyDefeatContinuation is
        { ResumePoint: ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement,
            HiredCapture: { Phase: HiredArmyCapturePhase.AwaitingAnnouncement or HiredArmyCapturePhase.ReadyToRelease } };
}
