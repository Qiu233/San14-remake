namespace San14.Data;

/// <summary>Actual map and city-information control facts for the sixth course.</summary>
public sealed record TutorialSixthLessonObservation(TutorialLessonObservation Common)
{
    /// <summary>The independent54FF80 city-information action receiver, reset to5.</summary>
    public int? LastCityInformationActionId { get; init; }
    /// <summary>The actual user map-display mode; zero is ordinary map display.</summary>
    public int? MapDisplayMode { get; init; }
}

public static class TutorialSixthLessonRules
{
    private static readonly int[] Stages = [100,101,103,104,105,106,107,108,109,110,111,112,113,114,115,116,117,118,119,120,121,122,123,124,125];
    public static int NextStage(int paragraphIndex)
    {
        CheckIndex(paragraphIndex);
        return paragraphIndex == 24 ? 0 : Stages[paragraphIndex + 1];
    }
    public static bool CanStart(int paragraphIndex, TutorialSixthLessonObservation observation)
    {
        CheckIndex(paragraphIndex); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive || o.Stage != Stages[paragraphIndex]) return false;
        return paragraphIndex switch
        {
            0 or 3 or 12 or 13 or 14 or 23 => o.ActionStackTop == 1,
            1 or 2 => o.ActionStackTop is 1 or 31,
            4 or 5 => o.ActionStackTop is 37 or 24,
            6 or 7 => o.ActionStackTop is 37 or 25,
            8 => o.ActionStackTop is 37 or 28 or 29 or 30,
            9 => o.ActionStackTop is 37 or 1,
            10 or 11 or 20 or 21 or 22 => o.ActionStackTop is 1 or 2,
            15 or 16 => o.ActionStackTop is 38 or 39,
            17 or 18 => o.ActionStackTop is 38 or 40,
            19 => o.ActionStackTop is 38 or 1,
            24 => true,
            _ => false,
        };
    }
    public static int NextPhase(int paragraphIndex, int phase, TutorialSixthLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return phase;
        if (paragraphIndex is 20 or 21)
        {
            if (phase == 1) return Setup(o) ? 2 : 1;
            if (phase == 2) return Returned(o) ? 3 : 2;
            if (phase == 0 && (paragraphIndex == 20 || ArrivalReady(o))) return Returned(o) ? 1 : 0;
            return phase;
        }
        if (phase != 0) return phase;
        if (paragraphIndex == 11 && !GoodwillReportReady(o) ||
            paragraphIndex == 22 && !CaravanReportReady(o) ||
            paragraphIndex == 13 && Need(observation.MapDisplayMode, nameof(observation.MapDisplayMode)) == 0)
            return 0;
        return paragraphIndex is 1 or 5 or 6 or 7 or 8 or 9 or 16 or 18 or 19 or 24
            ? Setup(o) ? 1 : 0 : Returned(o) ? 1 : 0;
    }
    public static bool CanComplete(int paragraphIndex, int phase, TutorialSixthLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        //4A9B00 tests the actual entered-mode flag before its first instruction.
        if (paragraphIndex == 12 && Flag(o, 42)) return true;
        if (phase == 0) return false;
        return paragraphIndex switch
        {
            0 or 23 or 24 => true,
            1 => Need(observation.LastCityInformationActionId, nameof(observation.LastCityInformationActionId)) == 2,
            2 => o.ActionStackTop != 31,
            3 => Need(o.LastInformationId, nameof(o.LastInformationId)) == 39,
            4 => o.ActionStackTop == 24,
            5 => o.ActionStackTop == 37,
            6 => o.ActionStackTop == 25,
            7 => Flag(o, 28),
            8 => Flag(o, 30) || Flag(o, 31) || Flag(o, 32),
            9 => Flag(o, 40),
            10 => Flag(o, 4) && o.ActionStackTop == 2,
            11 => GoodwillReportReady(o),
            12 => Flag(o, 42),
            13 => Need(observation.MapDisplayMode, nameof(observation.MapDisplayMode)) == 0,
            14 => Need(o.LastInformationId, nameof(o.LastInformationId)) == 50,
            15 => o.ActionStackTop == 39,
            16 => o.ActionStackTop == 38,
            17 => o.ActionStackTop == 40,
            18 => Flag(o, 45),
            19 => Flag(o, 43),
            20 => phase == 3 && Flag(o, 4) && o.ActionStackTop == 2,
            21 => phase == 3,
            22 => CaravanReportReady(o),
            _ => false,
        };
    }
    internal static bool GoodwillReportReady(TutorialLessonObservation o) => Flag(o, 41) && o.ActionStackTop == 1;
    internal static bool ArrivalReady(TutorialLessonObservation o) => Flag(o, 49) && o.ActionStackTop == 1;
    internal static bool CaravanReportReady(TutorialLessonObservation o) => Flag(o, 50) && o.ActionStackTop == 1;
    private static bool Setup(TutorialLessonObservation o) => Need(o.ParagraphSetupCompleted, nameof(o.ParagraphSetupCompleted));
    private static bool Returned(TutorialLessonObservation o) => Need(o.InstructionSequenceCompleted, nameof(o.InstructionSequenceCompleted));
    private static bool Flag(TutorialLessonObservation o, int index) => (o.ReadTutorialEventFlag ?? throw new InvalidDataException("tutorial.sixth_lesson.event_reader"))(index);
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw new InvalidDataException("tutorial.sixth_lesson.observation." + name);
    private static void CheckIndex(int index) { if ((uint)index >= 25) throw new ArgumentOutOfRangeException(nameof(index)); }
    private static void CheckPhase(int index, int phase)
    {
        CheckIndex(index);
        int maximum = index is 20 or 21 ? 3 : 1;
        if (phase < 0 || phase > maximum) throw new ArgumentOutOfRangeException(nameof(phase));
    }
}
