namespace San14.Data;

/// <summary>Actual command-rail readiness and bounded battle facts for course seven.</summary>
public sealed record TutorialSeventhLessonObservation(TutorialLessonObservation Common)
{
    public bool? StrategyControlReady { get; init; }
    public uint? CurrentMilliseconds { get; init; }
    /// <summary>Relative source time since the first real ready rail observation.</summary>
    public uint? HomeCommandElapsedMilliseconds { get; init; }
    /// <summary>499A40: physical156's commander Army/commander is gone, or captive.</summary>
    public bool? AttackObjectiveComplete { get; init; }
    public bool? AttackReturnArmyValid { get; init; }
}

public static class TutorialSeventhLessonRules
{
    private static readonly int[] Stages =
        [126,127,128,129,130,131,132,133,134,135,136,137,138,139,140,141,142,143,144,145,146,147,148,149,150,151,152,153,154,155,156,157,158,159,160,177,178];
    public static int NextStage(int index) { Check(index); return index == 36 ? 0 : Stages[index + 1]; }
    public static bool CanStart(int index, TutorialSeventhLessonObservation observation)
    {
        Check(index); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive || o.Stage != Stages[index]) return false;
        return index switch
        {
            0 or 1 or 7 or 15 or 23 or 31 => o.ActionStackTop == 1,
            2 or 5 => o.ActionStackTop is 44 or 43,
            3 or 4 => o.ActionStackTop is 43 or 45,
            6 => o.ActionStackTop is 44 or 1,
            8 or 9 => o.ActionStackTop is 46 or 47,
            10 or 11 => o.ActionStackTop is 46 or 48,
            12 => o.ActionStackTop is 46 or 1,
            13 or 14 or 21 or 22 or 29 or 30 or 35 or 36 => o.ActionStackTop is 1 or 2,
            16 or 17 => o.ActionStackTop is 23 or 24,
            18 or 19 => o.ActionStackTop is 23 or 25,
            20 => o.ActionStackTop is 23 or 1,
            24 or 25 => o.ActionStackTop is 49 or 33,
            26 or 27 => o.ActionStackTop is 49 or 34,
            28 => o.ActionStackTop is 49 or 1,
            32 or 33 => o.ActionStackTop is 1 or 2 or 6,
            34 => o.ActionStackTop is 10 or 11 or 1,
            _ => false,
        };
    }

    public static int NextPhase(int index, int phase, TutorialSeventhLessonObservation observation)
    {
        CheckPhase(index, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return phase;
        if (index == 1)
        {
            if (phase == 0) return Returned(o) ? 1 : 0;
            if (phase == 1 && !Information(o, 29) && Need(observation.StrategyControlReady, nameof(observation.StrategyControlReady)) &&
                observation.HomeCommandElapsedMilliseconds is uint elapsed && elapsed > 2000) return 2;
            return phase == 2 && Returned(o) ? 3 : phase;
        }
        if (index is 13 or 21)
            return phase switch { 0 or 2 => Setup(o) ? phase + 1 : phase,
                1 when ProgressStarted(o) => 2, _ => phase };
        if (index == 22) return phase == 1 ? 0 : Returned(o) ? 1 : 0;
        if (index == 23) return phase < 2 && Returned(o) ? phase + 1 : phase;
        if (index == 27) return phase switch { 0 when Flag(o, 36) => 1, 1 when Returned(o) => 2, _ => phase };
        if (phase != 0) return phase;
        if (index == 4 && !Flag(o, 84) || index == 11 && !Flag(o, 87) || index == 27 && !Flag(o, 36)) return 0;
        if (index == 14) return SpyReportReady(o) ? 1 : 0;
        if (index == 30) return o.ActionStackTop == 1 ? 1 : 0;
        if (index == 33) return o.ActionStackTop == 1 && Returned(o) ? 1 : 0;
        return TutorialSeventhLessonPlan.IsSynchronousSetup(index, phase) ? Setup(o) ? 1 : 0 : Returned(o) ? 1 : 0;
    }

    public static bool CanComplete(int index, int phase, TutorialSeventhLessonObservation observation)
    {
        CheckPhase(index, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        return index switch
        {
            0 or 4 or 11 or 35 or 36 => phase == 1,
            1 => phase >= 1 && Information(o, 29),
            2 => o.ActionStackTop == 43,
            3 => phase == 1 && o.ActionStackTop == 45,
            5 => Flag(o, 82),
            6 => o.ActionStackTop == 1,
            7 => phase == 1 && Information(o, 30),
            8 => phase == 1 && o.ActionStackTop == 47,
            9 => o.ActionStackTop == 46,
            10 => phase == 1 && o.ActionStackTop == 48,
            12 => Flag(o, 85),
            13 or 21 => phase == 3 && ProgressStarted(o),
            14 => phase == 1 && SpyReportReady(o),
            15 => phase == 1 && Information(o, 40),
            16 => o.ActionStackTop == 24,
            17 => o.ActionStackTop == 23,
            18 => o.ActionStackTop == 25,
            19 => Flag(o, 28),
            20 => phase == 1 && Flag(o, 26),
            22 => AllianceReportReady(o),
            23 => phase == 2 && Information(o, 36),
            24 => phase == 1 && o.ActionStackTop == 33,
            25 => Flag(o, 35),
            26 => phase == 1 && o.ActionStackTop == 34,
            27 => phase == 2,
            28 => Flag(o, 93),
            29 => phase == 1 && ProgressStarted(o),
            30 => phase == 1 && o.ActionStackTop == 1,
            31 => phase == 1,
            32 => Need(observation.AttackObjectiveComplete, nameof(observation.AttackObjectiveComplete)),
            33 => phase == 1 && !Need(observation.AttackReturnArmyValid, nameof(observation.AttackReturnArmyValid)),
            34 => phase == 1 && o.ActionStackTop == 1,
            _ => false,
        };
    }

    internal static bool SpyReportReady(TutorialLessonObservation o) => Flag(o, 89) && o.ActionStackTop == 1;
    internal static bool AllianceReportReady(TutorialLessonObservation o) => Flag(o, 34) && o.ActionStackTop == 1;
    private static bool Information(TutorialLessonObservation o, int id) => Need(o.LastInformationId, nameof(o.LastInformationId)) == id;
    private static bool ProgressStarted(TutorialLessonObservation o) => Flag(o, 4) && o.ActionStackTop == 2;
    private static bool Returned(TutorialLessonObservation o) => Need(o.InstructionSequenceCompleted, nameof(o.InstructionSequenceCompleted));
    private static bool Setup(TutorialLessonObservation o) => Need(o.ParagraphSetupCompleted, nameof(o.ParagraphSetupCompleted));
    internal static bool Flag(TutorialLessonObservation o, int flag) => (o.ReadTutorialEventFlag ?? throw Missing("event_reader"))(flag);
    internal static void CheckPhase(int index, int phase)
    { Check(index); if (phase < 0 || phase > TutorialSeventhLessonPlan.MaximumPhase(index)) throw new ArgumentOutOfRangeException(nameof(phase)); }
    private static void Check(int index) { if ((uint)index >= 37) throw new ArgumentOutOfRangeException(nameof(index)); }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("tutorial.seventh_lesson.observation." + field);
}
