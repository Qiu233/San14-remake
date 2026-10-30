namespace San14.Data;

/// <summary>The complete ordered instructions and successful-operation gates of tut_p06.</summary>
public static class TutorialSeventhLessonPlan
{
    public const int RequiredSpyForceId = 4;
    public const int HomeSpyOfficerPhysicalSlot = 147;
    public const int AwaySpyOfficerPhysicalSlot = 952;
    public const int RequiredAllianceForceId = 17;
    public const int AllianceOfficerPhysicalSlot = 952;
    public const int PlacateOfficerPhysicalSlot = 952;
    public const int PlacateAreaPhysicalSlot = 44;
    public const int EnemyOfficerPhysicalSlot = 156;
    public const int AttackOfficerPhysicalSlot = 656;
    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();
    public static int MaximumPhase(int index) => index is 1 or 13 or 21 ? 3 : index is 23 or 27 ? 2 :
        (uint)index < 37 ? 1 : throw new ArgumentOutOfRangeException(nameof(index));
    public static TutorialParagraphPlan Read(int index, int phase)
    {
        TutorialSeventhLessonRules.CheckPhase(index, phase);
        var stories = (index, phase) switch
        {
            (0,0) => Values(222), (1,0) => Values(223), (1,2) => Values(224),
            (4,0) => Values(225), (7,0) => Values(226), (11,0) => Values(227),
            (15,0) => Values(228), (22,0) => Values(229),
            (23,0) => Values(230), (23,1) => Values(231), (27,1) => Values(232),
            (31,0) => Values(233), (33,0) => Values(234), (35,0) => Values(235,236),
            _ => Empty,
        };
        int? information = index switch { 1 => 29, 7 => 30, 15 => 40, 23 => 36, _ => null };
        bool resetProgress = index is 13 or 21 && phase is 0 or 2 || index is 29 or 32 && phase == 0 || index == 33 && phase == 1;
        return new(index, phase, stories, null, information, resetProgress)
        {
            CallbackPhase = stories.Count == 0 ? phase : phase + 1,
            UsesInstructionCallback = stories.Count != 0 && index is not (0 or 35),
            EnabledCommandMask = index == 0 && phase == 0 || index is 1 or 7 or 15 && phase == 1 || index == 23 && phase == 2 ? 0 : null,
            ResetLastInformationId = information is not null && (index == 1 && phase == 3 || index is 7 or 15 && phase == 1 || index == 23 && phase == 2) ? 51 : null,
            AfterStoryClearEventFlags = index == 22 && phase == 0 ? Values(91) : index == 27 && phase == 1 ? Values(97) : Empty,
        };
    }

    public static bool ShouldRunStories(int index, int phase, TutorialSeventhLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (Read(index, phase).StoryEventIds.Count == 0) return false;
        var o = observation.Common;
        return index switch
        {
            4 => TutorialSeventhLessonRules.Flag(o, 84),
            11 => TutorialSeventhLessonRules.Flag(o, 87),
            22 => !TutorialSeventhLessonRules.Flag(o, 34) && TutorialSeventhLessonRules.Flag(o, 91),
            27 => TutorialSeventhLessonRules.Flag(o, 36),
            33 => o.ActionStackTop == 1,
            _ => true,
        };
    }
    public static bool IsSynchronousSetup(int index, int phase) =>
        phase == 0 && index is 2 or 3 or 5 or 6 or 8 or 9 or 10 or 12 or 13 or 16 or 17 or 18 or 19 or 20 or 21 or 24 or 25 or 26 or 28 or 29 or 32 or 34 or 36 ||
        index is 13 or 21 && phase == 2;
    public static bool IsInstructionCue(int index, int phase, int instructionIndex) =>
        index == 23 && phase == 1 && instructionIndex == 0 || index == 31 && phase == 0 && instructionIndex == 1;
    public static bool CanAcceptSpyForce(int forceId) => forceId == RequiredSpyForceId;
    public static bool CanAcceptAllianceForce(int forceId) => forceId == RequiredAllianceForceId;
    public static bool CanAcceptPlacateArea(int areaId) => areaId == PlacateAreaPhysicalSlot;
    private static IReadOnlyList<int> Values(params int[] values) => Array.AsReadOnly(values);
}
