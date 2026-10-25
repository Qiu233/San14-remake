namespace San14.Core;

/// <summary>The attached tutorial paragraph's saved failure progress.
/// Ordinary campaigns have no such owner.</summary>
public sealed record TutorialForceDefeatState(int ExitParagraphIndex, bool FailureHandled);

public enum TutorialForceDefeatPresentationOperation
{
    SuspendControls, ResumeControls, SelectExitParagraph, CloseControlsAndStop
}

/// <summary>Actual tutorial host work awaiting its own consumer.</summary>
public sealed record TutorialForceDefeatPresentationRequest(
    TutorialForceDefeatPresentationOperation Operation, int? ExitParagraphIndex);

public sealed partial record GameState
{
    public TutorialForceDefeatState? TutorialForceDefeat { get; init; }
}
