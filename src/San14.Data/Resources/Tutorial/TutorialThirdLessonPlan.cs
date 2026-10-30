using San14.Core;

namespace San14.Data;

/// <summary>Ordered instruction resources and source inputs for the third lesson.</summary>
public static class TutorialThirdLessonPlan
{
    public const int RequiredCityId = 7;
    public const int KanuOfficerId = 147;
    public const int ChouhiOfficerId = 656;
    public const int EnemyOfficerPhysicalSlot = 149;
    public const int ChouhiLandFormationId = 2;
    public const int ChouhiTroops = 3000;
    public static IReadOnlyList<int> KanuTargetAreaIds { get; } = Events(50, 51);
    public static IReadOnlyList<int> ChouhiTargetAreaIds { get; } = Events(48, 44);
    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();

    public static TutorialParagraphPlan Read(int paragraphIndex, int phase)
    {
        if ((uint)paragraphIndex >= 13) throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        int maximum = paragraphIndex switch { 3 => 11, 4 => 3, 5 => 4, 6 or 8 => 2, 9 => 45, _ => 1 };
        if (phase < 0 || phase > maximum) throw new ArgumentOutOfRangeException(nameof(phase));
        var stories = (paragraphIndex, phase) switch
        {
            (0, 0) => Events(144),
            (1, 0) => Events(145, 146),
            (3, 0) => Events(147),
            (3, 3) => Events(148),
            (3, 7) => Events(149),
            (3, 9) => Events(150),
            (3, 10) => Events(151, 152),
            (4, 1) => Events(153, 154),
            (6, 0) => Events(155),
            (6, 2) => Events(156),
            (7, 0) => Events(157),
            (9, 45) => Events(158),
            (11, 0) => Events(159),
            _ => Empty,
        };
        return new(paragraphIndex, phase, stories, paragraphIndex == 2 ? 0 : null, null,
            paragraphIndex == 5 && phase == 4 || paragraphIndex == 8 && phase == 2)
        {
            CallbackPhase = stories.Count == 0 ? phase : paragraphIndex == 6 && phase == 2 || paragraphIndex == 9
                ? phase : phase + 1,
            UsesInstructionCallback = paragraphIndex is 4 or 6 or 7 or 9 && stories.Count != 0,
            EnabledCommandMask = (paragraphIndex, phase) switch
            {
                (0, 0) => 0,
                (2, 0) => 1u,
                (2, 1) => 0,
                (5, 4) => 0,
                _ => null,
            },
            ResetLastCommandId = paragraphIndex == 2 && phase == 0 ? 13 : null,
            BeforeStoryClearEventFlags = (paragraphIndex, phase) switch
            {
                (2, 0) => Events(60),
                (4, 0) => Events(74),
                _ => Empty,
            },
            CompletionClearEventFlags = Empty,
            CompletionSetEventFlags = paragraphIndex switch
            {
                6 => Events(67),
                _ => Empty,
            },
        };
    }

    public static bool ShouldRunStories(int paragraphIndex, int phase, TutorialThirdLessonObservation observation)
        => TutorialThirdLessonRules.ShouldRunStories(paragraphIndex, phase, observation);

    /// <summary>The source's UnitMake selector permits precisely these two identities.</summary>
    public static bool CanAcceptOfficerSelection(IReadOnlyList<int> officerIds)
        => TutorialThirdLessonRules.CanAcceptOfficerSelection(officerIds);

    /// <summary>Reads the actual draft command. A row without a chosen action
    /// has no assigned order yet; it cannot satisfy the Move predicate.</summary>
    public static int? ReadDraftOrderKind(DeploymentDraftRow? row) => row?.Request.Actions is [var action]
        ? action.ActionKey switch { "move" => 1, "stay" => 13, "occupy" => 14,
            "fire" => 16, "build" => 8, "repair" => 9, "demolish" => 10,
            _ => null } : null;

    private static IReadOnlyList<int> Events(params int[] ids) => Array.AsReadOnly(ids);
}
