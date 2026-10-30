namespace San14.Data;

/// <summary>The fifth course's ordered instruction calls and actual input gates.</summary>
public static class TutorialFifthLessonPlan
{
    public const int PlayerCityId = 7;
    public const int AllianceCityId = 6;
    public const int AllianceForceId = 15;
    public const int RequiredOfficerId = 147;
    public const int DestructionTargetHexId = 9386;
    public const int FalseReportDestinationHexId = 9831;
    public static IReadOnlyList<int> ConstructionSiteHexIds { get; } = Values(10500,10719,9831,9609);
    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();
    private static readonly int[] MaximumPhases = [1,1,1,1,1,1,1,1,1,0,1,1,1,1,1,1,0,1,1,1,2,6,6,8,1,1,2,2,1,0,1,1];

    public static TutorialParagraphPlan Read(int paragraphIndex, int phase)
    {
        if ((uint)paragraphIndex >= 32 || phase < 0 || phase > MaximumPhases[paragraphIndex])
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        var events = (paragraphIndex,phase) switch
        {
            (0,0) => Values(178), (1,0) => Values(179), (3,0) => Values(180),
            (5,0) => Values(181), (7,0) => Values(183), (8,0) => Values(182),
            (10,0) => Values(184), (11,0) => Values(185), (12,0) => Values(186),
            (17,0) => Values(187), (18,0) => Values(188), (19,0) => Values(189),
            (20,2) => Values(190), (21,5) => Values(191), (25,0) => Values(192),
            (26,0) => Values(193,194,195,196), (26,1) => Values(197,198),
            (27,2) => Values(199), (28,0) => Values(200), (30,0) => Values(201),
            _ => Empty
        };
        int? info = paragraphIndex switch { 1 => 40,12 => 37,_ => null };
        return new(paragraphIndex,phase,events,paragraphIndex == 20 && phase <= 1 ? 0 : null, info,
            paragraphIndex is 10 or 17 && phase == 1 || paragraphIndex == 23 && phase == 8 || paragraphIndex == 27 && phase == 2)
        {
            CallbackPhase = events.Count == 0 || paragraphIndex is 20 or 21 or 27 ? phase : phase+1,
            UsesInstructionCallback = events.Count != 0 && paragraphIndex is not (0 or 30),
            EnabledCommandMask = (paragraphIndex,phase) switch
            {
                (0,0) or (11,0) or (23,8) => 0,
                (20,0) => 1,
                (28,1) => 3,
                _ => null
            },
            ResetLastInformationId = paragraphIndex is 1 or 12 && phase == 1 ? 51 : null,
            ResetLastCommandId = paragraphIndex == 20 && phase == 0 ? 13 : null,
            BeforeStoryClearEventFlags = (paragraphIndex,phase) switch
                { (21,0) => Values(74), (27,0) => Values(58), _ => Empty },
        };
    }

    public static bool ShouldRunStories(int paragraphIndex,int phase,TutorialFifthLessonObservation observation)
        => Read(paragraphIndex,phase).StoryEventIds.Count != 0 && TutorialFifthLessonRules.ShouldRunStories(paragraphIndex,phase,observation);
    public static int? ReadDraftOrderKind(DeploymentDraftRow? row) => TutorialFourthLessonPlan.ReadDraftOrderKind(row);
    private static IReadOnlyList<int> Values(params int[] values) => Array.AsReadOnly(values);
}
