namespace San14.Data;

/// <summary>Facts from the ordinary credit, doctrine, search and scenic-site controls.</summary>
public sealed record TutorialEighthLessonObservation(TutorialLessonObservation Common)
{
    public int? CreditSelectedOfficerCount { get; init; }
    public bool? CreditOfficerSelectorActive { get; init; }
    public bool? CreditRequiredChoicesSelected { get; init; }
    public IReadOnlySet<int>? ResearchedDoctrineLayoutSlots { get; init; }
    public int? SelectedDoctrineLayoutSlot { get; init; }
    public bool? SearchCitySelectorActive { get; init; }
    public bool? SearchOfficerSelectorActive { get; init; }
    /// <summary>The actual scenic-site details control registered at340.</summary>
    public bool? ScenerySelectorActive { get; init; }
    /// <summary>The ordinary investment layout348 and footer350 both exist.</summary>
    public bool? SceneryInvestmentDialogActive { get; init; }
    /// <summary>The details dialog340 has its real virtual1C0 panel.</summary>
    public bool? SceneryInvestmentControlsReady { get; init; }
    /// <summary>The accepted scenic hex-menu action;3 is its reset sentinel.</summary>
    public int? LastSceneryActionId { get; init; }
}

public static class TutorialEighthLessonRules
{
    private static readonly int[] Stages = [179,180,181,182,183,184,185,186,187,188,190,191,192,193,194,195,196,197];
    public static int NextStage(int index) { CheckIndex(index); return index == 17 ? 0 : Stages[index + 1]; }
    public static bool CanStart(int index, TutorialEighthLessonObservation observation)
    {
        CheckIndex(index); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        //4C0CC0 admits the actual domestic draft independently of the map top.
        if (index is 4 or 7 && o.DomesticSelectionOpen == true) return true;
        if (o.Stage != Stages[index]) return false;
        return index switch
        {
            2 => o.ActionStackTop is 1 or 53 or 54,
            5 or 8 => true,
            13 => o.ActionStackTop is 1 or 55,
            14 => o.ActionStackTop is 1 or 50 or 51 or 52 or 2 or 10,
            15 => o.ActionStackTop is 1 or 2 or 10 or 56,
            16 => o.ActionStackTop is 1 or 56,
            17 => o.ActionStackTop is 1 or 2,
            _ => o.ActionStackTop == 1
        };
    }
    public static int NextPhase(int index, int phase, TutorialEighthLessonObservation observation)
    {
        CheckPhase(index,phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return phase;
        if (index == 2) return phase switch
        {
            0 => Setup(o) ? 1 : 0,
            1 => o.LastInformationId == 26 ? 2 : 1,
            2 => Need(observation.CreditSelectedOfficerCount,nameof(observation.CreditSelectedOfficerCount)) > 2 ? 3 : 2,
            3 => Returned(o) ? 4 : 3, _ => phase
        };
        if (index == 13) return phase switch
        {
            0 or 2 or 5 or 7 => Returned(o) ? phase + 1 : phase,
            1 => Researched(observation,6) ? 2 : 1,
            3 => Researched(observation,12) ? 4 : 3,
            4 => Researched(observation,27) ? 5 : 4,
            6 => o.ActionStackTop != 55 ? 7 : 6,
            _ => phase
        };
        if (index == 14) return phase switch
        {
            0 => Returned(o) ? 1 : 0,
            1 => o.LastInformationId == 19 ? 2 : 1,
            2 => Need(observation.SearchCitySelectorActive,nameof(observation.SearchCitySelectorActive)) ? 3 : 2,
            3 => o.ActionStackTop == 50 ? 4 : 3,
            4 => Need(observation.SearchOfficerSelectorActive,nameof(observation.SearchOfficerSelectorActive)) ? 5 : 4,
            5 => o.ActionStackTop == 50 ? 6 : 5,
            6 => Flag(o,95) ? 7 : 6,
            7 => ProgressStarted(o) ? 8 : 7,
            _ => phase
        };
        //Expanded phases preserve all three source counter values and each
        //dialog-entry latch in the already saved per-paragraph phase.
        if (index == 15) return phase switch
        {
            0 or 3 or 7 or 11 or 14 => Returned(o) ? phase + 1 : phase,
            1 or 12 => observation.LastSceneryActionId == 2 ? phase + 1 : phase,
            2 or 13 => Need(observation.SceneryInvestmentControlsReady,nameof(observation.SceneryInvestmentControlsReady)) ? phase + 1 : phase,
            4 or 15 => !Need(observation.ScenerySelectorActive,nameof(observation.ScenerySelectorActive)) ? phase + 1 : phase,
            5 => observation.LastSceneryActionId == 0 ? 6 : 5,
            6 => Need(observation.SceneryInvestmentDialogActive,nameof(observation.SceneryInvestmentDialogActive)) ? 7 : 6,
            8 => !Need(observation.SceneryInvestmentDialogActive,nameof(observation.SceneryInvestmentDialogActive)) ? 9 : 8,
            9 => ProgressStarted(o) ? 10 : 9,
            10 => o.ActionStackTop == 1 ? 11 : 10,
            _ => phase
        };
        if (phase != 0) return phase;
        return TutorialEighthLessonPlan.IsSynchronousSetup(index,phase)
            ? Setup(o) ? 1 : 0 : Returned(o) ? 1 : 0;
    }
    public static bool CanComplete(int index, int phase, TutorialEighthLessonObservation observation)
    {
        CheckPhase(index,phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive || phase == 0) return false;
        return index switch
        {
            0 or 1 or 11 or 16 or 17 => true,
            2 => phase == 4 && o.ActionStackTop != 53,
            3 => o.LastCommandId == 3,
            4 or 7 => Need(o.DomesticSelectionOpen,nameof(o.DomesticSelectionOpen)),
            5 or 8 => Flag(o,11),
            6 or 9 => Flag(o,13),
            10 => Flag(o,14),
            12 => o.LastInformationId == 14,
            13 => phase == 8,
            14 => phase == 8 && o.ActionStackTop == 1,
            15 => phase == 16 && o.ActionStackTop == 1,
            _ => false
        };
    }
    internal static int MaximumPhase(int index) { CheckIndex(index); return index switch { 2 => 4,13 or 14 => 8,15 => 16,_ => 1 }; }
    private static bool Researched(TutorialEighthLessonObservation o,int slot) =>
        (o.ResearchedDoctrineLayoutSlots ?? throw new InvalidDataException("tutorial.eighth_lesson.research_mask")).Contains(slot);
    private static bool ProgressStarted(TutorialLessonObservation o) => Flag(o,4) && o.ActionStackTop == 2;
    private static bool Returned(TutorialLessonObservation o) => Need(o.InstructionSequenceCompleted,nameof(o.InstructionSequenceCompleted));
    private static bool Setup(TutorialLessonObservation o) => Need(o.ParagraphSetupCompleted,nameof(o.ParagraphSetupCompleted));
    private static bool Flag(TutorialLessonObservation o,int flag) => (o.ReadTutorialEventFlag ?? throw new InvalidDataException("tutorial.eighth_lesson.event_reader"))(flag);
    private static T Need<T>(T? value,string name) where T:struct => value ?? throw new InvalidDataException("tutorial.eighth_lesson.observation." + name);
    private static void CheckIndex(int index) { if ((uint)index >= 18) throw new ArgumentOutOfRangeException(nameof(index)); }
    internal static void CheckPhase(int index,int phase) { if (phase < 0 || phase > MaximumPhase(index)) throw new ArgumentOutOfRangeException(nameof(phase)); }
}
