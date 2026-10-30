namespace San14.Data;

/// <summary>Source instruction and input-gate operands for the second lesson.</summary>
public static class TutorialSecondLessonPlan
{
    public const int RequiredCityId = 7;
    public const int RequiredOfficerId = 952;
    public const int CapitalAreaId = 51;
    public const int RequiredFormationId = 5;
    public static IReadOnlyList<int> ConquerAreaIds { get; } = Events(49, 48, 50);

    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();
    private static readonly IReadOnlyList<int>[] InitialStories =
    [
        Events(127, 128), Empty, Events(129), Events(130), Empty,
        Events(131), Events(132), Empty, Events(134), Empty,
        Events(137, 138, 139), Events(140, 141), Events(143), Empty,
    ];

    public static TutorialParagraphPlan Read(int paragraphIndex, int phase)
    {
        if ((uint)paragraphIndex >= (uint)InitialStories.Length)
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        int maximum = paragraphIndex switch { 6 => 3, 9 => 4, 11 => 2, _ => 1 };
        if (phase < 0 || phase > maximum) throw new ArgumentOutOfRangeException(nameof(phase));
        var stories = phase == 0 ? InitialStories[paragraphIndex] : Empty;
        if (paragraphIndex == 6 && phase == 2) stories = Events(133);
        if (paragraphIndex == 9) stories = phase switch { 1 => Events(135), 3 => Events(136), _ => Empty };
        if (paragraphIndex == 11 && phase == 1) stories = Events(142);
        return new TutorialParagraphPlan(paragraphIndex, phase, stories,
            paragraphIndex == 1 ? 0 : null, null, paragraphIndex == 9 && phase == 4)
        {
            CallbackPhase = paragraphIndex == 9 && phase == 1 ? 1 : stories.Count != 0 ? phase + 1 : phase,
            UsesInstructionCallback = paragraphIndex is 9 or 10 or 11 && stories.Count != 0,
            EnabledCommandMask = paragraphIndex switch
            {
                0 when phase == 0 => 0,
                1 when phase == 0 => 1u << 0,
                9 when phase == 2 => 0,
                11 when phase == 2 => 0xFu,
                _ => null,
            },
            ResetLastCommandId = paragraphIndex == 1 && phase == 0 ? 13 : null,
            BeforeStoryClearEventFlags = paragraphIndex == 1 && phase == 0 ? Events(60) : Empty,
            CorrectionGameMessageId = paragraphIndex == 4 ? 0x9C62 : null,
        };
    }

    /// <summary>The conditional capital-target explanation runs once, without advancing phase 1.</summary>
    public static bool ShouldRunStories(int paragraphIndex, int phase, TutorialSecondLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var plan = Read(paragraphIndex, phase);
        if (plan.StoryEventIds.Count == 0) return false;
        if (paragraphIndex == 2 && observation.Common.ActionStackTop == 5) return false;
        if (paragraphIndex == 9 && phase == 1)
            return !Need(observation.TargetOrderMatchesCapital, nameof(observation.TargetOrderMatchesCapital)) &&
                Need(observation.TargetInformationVisible, nameof(observation.TargetInformationVisible)) &&
                !Need(observation.TargetExplanationShown, nameof(observation.TargetExplanationShown));
        return paragraphIndex != 11 || TutorialSecondLessonRules.AreaInstructionsAvailable(observation);
    }

    private static T Need<T>(T? value, string owner) where T : struct => value ??
        throw new InvalidDataException($"Tutorial second lesson is missing the actual {owner} observation.");
    private static IReadOnlyList<int> Events(params int[] ids) => Array.AsReadOnly(ids);
}
