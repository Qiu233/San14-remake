namespace San14.Data;

/// <summary>Source story events, callbacks and input gates for course index3.</summary>
public static class TutorialFourthLessonPlan
{
    public const int RequiredCityId = 7;
    public const int TargetCityId = 5;
    public const int ConstructionHexId = 10708;
    public const int RequiredStrategistId = 147;
    public const int RequiredFacilityKind = 5;
    public const int RequiredBuildInstructionKind = 8;
    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();
    public static int? ReadDraftOrderKind(DeploymentDraftRow? row)
    {
        if (row?.Request.Actions is not [var action]) return null;
        return action.ActionKey switch
        {
            "move" => 1, "stay" => 13, "build" => 8, "fire" => 16,
            "occupy" => 14, "repair" => 9, "demolish" => 10, "capture" => 17,
            "attack" => action.Target.Kind == San14.Core.DeploymentTargetKind.Unit ? 7 :
                action.Target.Kind == San14.Core.DeploymentTargetKind.Location ? action.Target.Id < 52 ? 4 : 5 : null,
            "enter" => action.Target.Kind == San14.Core.DeploymentTargetKind.Location ? action.Target.Id < 52 ? 2 : 3 : null,
            _ => null,
        };
    }

    public static TutorialParagraphPlan Read(int paragraphIndex, int phase)
    {
        if ((uint)paragraphIndex >= 16) throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        int maximum = paragraphIndex switch { 3 => 2, 7 => 6, 9 => 7, 12 => 4, 13 => 2, _ => 1 };
        if (phase < 0 || phase > maximum) throw new ArgumentOutOfRangeException(nameof(phase));
        var stories = (paragraphIndex, phase) switch
        {
            (0, 0) => Events(160), (2, 0) => Events(161, 162, 163, 164),
            (4, 0) => Events(165), (5, 0) => Events(166),
            (7, 0) => Events(167), (7, 2) => Events(168), (7, 3) => Events(169),
            (7, 5) => Events(170, 171), (8, 0) => Events(172),
            (9, 4) => Events(173), (10, 0) => Events(174), (12, 0) => Events(175),
            (13, 1) => Events(176), (14, 0) => Events(177), _ => Empty,
        };
        return new(paragraphIndex, phase, stories,
            paragraphIndex == 9 && phase == 0 ? 0 : null,
            paragraphIndex == 1 || paragraphIndex == 7 && phase == 1 ? 12 : null,
            paragraphIndex == 6 && phase == 0 || paragraphIndex == 9 && phase == 7 || paragraphIndex == 12 && phase == 3)
        {
            CallbackPhase = paragraphIndex == 9 && phase == 4 ? 4 : stories.Count > 0 ? phase + 1 : phase,
            UsesInstructionCallback = paragraphIndex is 4 or 5 or 7 or 9 or 10 or 12 or 13 && stories.Count > 0,
            EnabledCommandMask = (paragraphIndex, phase) switch
            {
                (0, 0) or (6, 0) or (9, 7) or (12, 2) => 0,
                (9, 0) => 1, (11, 0) or (12, 3) => 31, _ => null,
            },
            ResetLastCommandId = paragraphIndex == 9 && phase == 0 ? 13 : null,
            ResetLastInformationId = paragraphIndex == 1 && phase == 0 || paragraphIndex == 7 && phase == 1 ? 51 : null,
            BeforeStoryClearEventFlags = paragraphIndex == 9 && phase == 0 || paragraphIndex == 1 && phase == 0 || paragraphIndex == 7 && phase == 1 ? Events(60) : Empty,
            AfterStoryClearEventFlags = paragraphIndex == 4 && phase == 0 ? Events(76) : Empty,
            CompletionClearEventFlags = paragraphIndex == 4 ? Events(75) : paragraphIndex == 5 ? Events(4) : paragraphIndex == 12 ? Events(69, 70) : Empty,
        };
    }

    public static bool ShouldRunStories(int paragraphIndex, int phase, TutorialFourthLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (Read(paragraphIndex, phase).StoryEventIds.Count == 0) return false;
        return (paragraphIndex, phase) switch
        {
            (7, 3) => TutorialFourthLessonRules.EffectVisible(observation),
            (9, 4) => TutorialFourthLessonRules.FacilityVisible(observation) &&
                observation.ConstructionFacilityId == RequiredFacilityKind &&
                !Need(observation.ConstructionExplanationShown, nameof(observation.ConstructionExplanationShown)),
            (10, 0) => Need(observation.ConstructionCompleted, nameof(observation.ConstructionCompleted)),
            (12, 0) => observation.Common.ActionStackTop == 1,
            (13, 1) => Need(observation.NanpiExplanationReady, nameof(observation.NanpiExplanationReady)),
            _ => true,
        };
    }
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw new InvalidDataException("tutorial.fourth_lesson.observation." + name);
    private static IReadOnlyList<int> Events(params int[] ids) => Array.AsReadOnly(ids);
}
