namespace San14.Data;

/// <summary>Sixth-course instruction order and the original current-control gates.</summary>
public static class TutorialSixthLessonPlan
{
    public const int InformationCityId = 47;
    public const int SourceCityId = 7;
    public const int RequiredGoodwillForceId = 46;
    public const int GoodwillOfficerPhysicalSlot = 952;
    public const int CaravanOfficerPhysicalSlot = 147;
    public const int RequiredCountryId = 4;
    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();
    public static TutorialParagraphPlan Read(int paragraphIndex, int phase)
    {
        if ((uint)paragraphIndex >= 25) throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        if (phase < 0 || phase > (paragraphIndex is 20 or 21 ? 3 : 1)) throw new ArgumentOutOfRangeException(nameof(phase));
        var stories = (paragraphIndex, phase) switch
        {
            (0,0) => Events(202,203), (2,0) => Events(204), (3,0) => Events(205),
            (4,0) => Events(206), (10,0) => Events(207), (11,0) => Events(208),
            (12,0) => Events(209), (13,0) => Events(210), (14,0) => Events(211),
            (15,0) => Events(212), (17,0) => Events(213,214),
            (20,0) => Events(216), (20,2) => Events(217),
            (21,0) => Events(218), (21,2) => Events(219),
            (22,0) => Events(220), (23,0) => Events(221), _ => Empty,
        };
        return new(paragraphIndex, phase, stories, null,
            paragraphIndex == 3 ? 39 : paragraphIndex == 14 ? 50 : null,
            paragraphIndex == 10 && phase == 1 || paragraphIndex == 20 && phase == 3)
        {
            CallbackPhase = stories.Count > 0 ? phase + 1 : phase,
            UsesInstructionCallback = paragraphIndex is 3 or 4 or 10 or 11 or 12 or 13 or 14 or 20 or 21 or 22 && stories.Count > 0,
            EnabledCommandMask = paragraphIndex == 0 && phase == 0 ? 0 : null,
            ResetLastInformationId = paragraphIndex is 3 or 14 && phase == 1 ? 51 : null,
            BeforeStoryClearEventFlags = paragraphIndex is 3 or 14 && phase == 1 ? Events(60) : Empty,
            CompletionClearEventFlags = paragraphIndex == 23 ? Events(54) : Empty,
        };
    }
    public static bool ShouldRunStories(int paragraphIndex, int phase, TutorialSixthLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (Read(paragraphIndex, phase).StoryEventIds.Count == 0) return false;
        var o = observation.Common;
        return paragraphIndex switch
        {
            11 => TutorialSixthLessonRules.GoodwillReportReady(o),
            12 => !(o.ReadTutorialEventFlag ?? throw new InvalidDataException("tutorial.sixth_lesson.event_reader"))(42),
            13 => observation.MapDisplayMode is int mode ? mode != 0 : throw new InvalidDataException("tutorial.sixth_lesson.map_mode"),
            21 when phase == 0 => TutorialSixthLessonRules.ArrivalReady(o),
            22 => TutorialSixthLessonRules.CaravanReportReady(o),
            _ => true,
        };
    }
    public static bool IsSynchronousSetup(int index, int phase) =>
        phase == 0 && index is 1 or 5 or 6 or 7 or 8 or 9 or 16 or 18 or 19 or 24 ||
        index is 20 or 21 && phase == 1;
    public static bool CanAcceptGoodwillForce(int forceId) => forceId == RequiredGoodwillForceId;
    public static bool CanAcceptCaravanCountry(int countryId) => countryId == RequiredCountryId;
    private static IReadOnlyList<int> Events(params int[] ids) => Array.AsReadOnly(ids);
}
