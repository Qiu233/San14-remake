using System.Collections.Immutable;

namespace San14.Core;

public sealed record TutorialLessonCompletionNotice(string TextAtSource, int? SoundId = null);

/// <summary>Reached lesson paragraphs and their independent local phases.
/// Application UI observations and handles belong to the tutorial host.</summary>
public sealed record TutorialLessonProgress(int CourseIndex, int Stage,
    ImmutableArray<int> ParagraphPhases, ulong CompletedParagraphMask, long Sequence,
    CampaignStoryRequest? Story)
{
    public ushort EnabledCommandMask { get; init; }
    public int? ActiveParagraphIndex { get; init; }
    public int? CompletionCourseIndex { get; init; }
    public int InstructionIndex { get; init; }
    public CampaignStoryMessageContext? MessageContext { get; init; }
    public ImmutableArray<TutorialLessonCompletionNotice>? CompletionNotices { get; init; }
    public int CompletionNoticeIndex { get; init; }
    public CampaignStoryPreparedText? CorrectionText { get; init; }
    /// <summary>Null before installation; zero is the observed null Army.
    /// Positive values retain the physical receiver captured by the lesson.</summary>
    public int? RestrictedArmyPhysicalSlot { get; init; }

    /// <summary>Continuous active-source focus time; null means no matching focus.</summary>
    public ImmutableArray<int>? RestrictedArmyPhysicalSlots { get; init; }
    public int? FourthConstructionCommanderId { get; init; }
    public int? TerritoryTransferCount { get; init; }
    public uint? AreaSupportElapsedMilliseconds { get; init; }
    /// <summary>Active source time after the seventh lesson's Home command
    /// first observes a ready strategy controller.</summary>
    public uint? HomeCommandElapsedMilliseconds { get; init; }
    public int FourthAttackReinforcementCount { get; init; }
    public bool FourthAttackRefreshRequested { get; init; }

    public static int ParagraphCount(int courseIndex) => courseIndex switch
    {
        0 => 22, 1 => 14, 2 => 13, 3 => 16, 4 => 32, 5 => 25, 6 => 37, 7 => 18,
        _ => throw new ArgumentOutOfRangeException(nameof(courseIndex))
    };
}

internal static class TutorialLessonProgressValidation
{
    internal static void Validate(TutorialLessonProgress? progress)
    {
        if (progress is null) return;
        if (progress.CourseIndex is < 0 or > 7)
            throw new InvalidDataException("Tutorial lesson index is invalid.");
        int paragraphCount = TutorialLessonProgress.ParagraphCount(progress.CourseIndex);
        if (progress.Stage < 0 ||
            progress.ParagraphPhases.IsDefault || progress.ParagraphPhases.Length != paragraphCount ||
            progress.ParagraphPhases.Any(phase => phase < 0) || progress.CompletedParagraphMask >= 1UL << paragraphCount ||
            progress.Sequence < 0 || progress.InstructionIndex < 0 ||
            progress.RestrictedArmyPhysicalSlot is < 0 or > 500 ||
            progress.FourthAttackReinforcementCount is < 0 or > 5 ||
            progress.TerritoryTransferCount is < 0 ||
            progress.FourthConstructionCommanderId is < 0 or > 6000 ||
            progress.ActiveParagraphIndex is int active && (active < 0 || active >= paragraphCount) ||
            progress.CompletionCourseIndex is < 0 or > 7)
            throw new InvalidDataException("Tutorial lesson progress is incomplete or invalid.");
        if (progress.RestrictedArmyPhysicalSlots is { } slots &&
            (slots.IsDefault || slots.Length > 501 || slots.Any(slot => slot is < 0 or > 500)))
            throw new InvalidDataException("Tutorial Army restrictions are invalid.");
        if (progress.CompletionNoticeIndex < 0 ||
            (progress.CompletionNotices is not { } notices ? progress.CompletionNoticeIndex != 0 :
                notices.IsDefault || progress.CompletionNoticeIndex > notices.Length ||
                notices.Any(notice => notice is null || notice.TextAtSource is null || notice.SoundId is not (null or 5))))
            throw new InvalidDataException("Tutorial completion notices are incomplete or invalid.");
        progress.Story?.Validate();
        progress.MessageContext?.Validate();
        CampaignStoryPreparationValidation.ValidateText(progress.CorrectionText);
    }
}

public sealed partial record GameState
{
    public TutorialLessonProgress? TutorialLessonProgress { get; init; }
}
