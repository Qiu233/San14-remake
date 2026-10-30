namespace San14.Core;

public enum BaseCapturePersonPrefixStage
{
    AwaitingSpecialCaptureNotice,
    ReadyToMergeCapturedPeople,
    CapturedCallbacks
}

/// <summary>The reached 2851B0 people prefix. Null on an old active capture
/// means source-time progress is unavailable, never a guessed callback stage.</summary>
public sealed record BaseCapturePersonPrefixProgress(BaseCapturePersonPrefixStage Stage,
    int OrdinaryCapturedCount, long? AnnouncementBatchSequence, string? AnnouncementTextAtSource)
{
    public static bool IsAwaitingNotice(GameState state) => state.BaseCaptureContinuation?.PersonPrefixProgress is
        { Stage: BaseCapturePersonPrefixStage.AwaitingSpecialCaptureNotice };

    //The same host stream and candidate remain owned between the actual
    //notice receipt and the worker's next merge/callback transaction.
    public static bool IsPresentationPending(GameState state) => state.BaseCaptureContinuation?.PersonPrefixProgress is
        { Stage: BaseCapturePersonPrefixStage.AwaitingSpecialCaptureNotice or
            BaseCapturePersonPrefixStage.ReadyToMergeCapturedPeople };
}
