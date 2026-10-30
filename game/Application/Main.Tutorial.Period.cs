using San14.Core;
using San14.Data;

public partial class Main
{
    // The simulation worker is stopped for this entire lease. The lesson
    // borrows its actual image; the committed session/draft baseline remains
    // unchanged until the ordinary period transaction is adopted.
    private void BeginTutorialPeriodBorrow(GameState candidate)
    {
        if (_tutorialLesson is not { } owner || !IsTutorialLessonCurrent(owner)) return;
        if (owner.PeriodBorrowed) throw new InvalidDataException("tutorial.period.borrow_active");
        owner.Lesson.BeginPeriodBorrow(candidate);
        owner.PeriodBorrowed = true;
        owner.LastUi = null;
    }

    private (GameState State, bool Hold) TickTutorialPeriodBorrow(GameState candidate,
        uint sourceMilliseconds, bool actualTutorialUpdate)
    {
        if (_tutorialLesson is not { PeriodBorrowed: true } owner) return (candidate, false);
        if (!IsTutorialLessonCurrent(owner)) return (candidate, false);
        owner.Lesson.RefreshPeriodBorrowWorld(candidate);
        owner.SourceMilliseconds = sourceMilliseconds;
        bool tacticsUpdate = actualTutorialUpdate && owner.Lesson.Progress is
            { CourseIndex: 2, ActiveParagraphIndex: 9, Story: null } && owner.Story is null;
        ObserveTutorialLesson(owner, force: tacticsUpdate, periodBorrow: true,
            actualTutorialUpdate: tacticsUpdate);
        if (owner.Failed) throw new InvalidDataException("tutorial.period.presentation_failed");
        bool hold = owner.Story is not null || owner.NoticeRequest is not null ||
            owner.Lesson.CurrentRequest is { Kind: TutorialLessonRequestKind.Narration or
                TutorialLessonRequestKind.InstructionCue } or
            { Guidance.InstructionCallbackPending: true };
        return (owner.Lesson.ReadPeriodBorrowWorld(), hold);
    }

    private GameState EndTutorialPeriodBorrow(GameState candidate)
    {
        if (_tutorialLesson is not { PeriodBorrowed: true } owner) return candidate;
        try { return owner.Lesson.EndPeriodBorrow(candidate); }
        finally
        {
            owner.PeriodBorrowed = false;
            owner.LastUi = null;
            // A normal release has already completed its story. A cancelled
            // worker also releases the actual UI instead of stranding input.
            CloseTutorialStory(owner);
        }
    }
}
