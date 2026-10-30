namespace San14.Data;

/// <summary>Actual fifth-lesson controls and business facts, sampled by the
/// host and the lesson's world reader. Null is unavailable, not a false flag.</summary>
public sealed record TutorialFifthLessonObservation(TutorialLessonObservation Common)
{
    public bool? AllianceForceSelectionPresent { get; init; }
    public bool? AllianceOfficerSelectionPresent { get; init; }
    public bool? ConstructionOfficerSelectionActive { get; init; }
    /// <summary>Exactly three actual deployment summary rows, each with a
    /// positive selected officer identity.</summary>
    public bool? ConstructionDeploymentReady { get; init; }
    /// <summary>The selected SAUnitTargetParam's field6C is nonzero. This is
    /// the summary captured by520640, not a formal order confirmation.</summary>
    public bool? SelectedConstructionSummaryActive { get; init; }
    /// <summary>The actual facility dialog belongs to the active control list
    /// (86FFC0/86FC80), independently of merely being allocated or visible.</summary>
    public bool? FacilitySelectionActive { get; init; }
    /// <summary>Ordered summary slots0..2. A null element is a known absent
    /// summary; a null list means the registry observation is unavailable.</summary>
    public IReadOnlyList<int?>? ConstructionSummaryOrderKinds { get; init; }
    /// <summary>All four specified Hex receivers10500,10719,9831,9609 have a
    /// valid Facility whose actual byte16 bit0 is set.</summary>
    public bool? AllConstructionSitesReady { get; init; }
    public bool? EnemyOfficerValid { get; init; }
    /// <summary>The actual commander Army of physical person149.</summary>
    public bool? EnemyArmyValid { get; init; }
    public bool? EnemyArmyCommanderValid { get; init; }
    public int? EnemyArmyCommanderStatus { get; init; }
    /// <summary>Person1000's Army has order17,229EE0 target equals person149's
    /// Army, and its actual word13E equals0x2667.</summary>
    public bool? FakeNewsOrderMatches { get; init; }
    /// <summary>The enemy Army's actual byte140 is nonzero (2C5B40).</summary>
    public bool? EnemyArmyFalseReportActive { get; init; }
}

/// <summary>The32 original tut_p04 paragraph admission and completion rules.
/// World setup, scripted instructions, and accepted UI events have separate
/// owners; none is simulated by these pure predicates.</summary>
public static class TutorialFifthLessonRules
{
    private static readonly int[] Stages =
        [67,68,69,70,71,72,73,74,75,77,78,79,80,81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99];
    private static readonly int[] MaximumPhases =
        [1,1,1,1,1,1,1,1,1,0,1,1,1,1,1,1,0,1,1,1,2,6,6,8,1,1,2,2,1,0,1,1];

    public static int NextStage(int paragraphIndex)
    {
        CheckIndex(paragraphIndex);
        return paragraphIndex == 31 ? 0 : Stages[paragraphIndex + 1];
    }

    /// <summary>An invalid enemy Army skips the false-report waiting paragraph.</summary>
    public static int NextStage(int paragraphIndex, TutorialFifthLessonObservation observation)
    {
        CheckIndex(paragraphIndex); ArgumentNullException.ThrowIfNull(observation);
        return paragraphIndex == 27 && !Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid))
            ? 97 : NextStage(paragraphIndex);
    }

    public static bool CanStart(int paragraphIndex, TutorialFifthLessonObservation observation)
    {
        CheckIndex(paragraphIndex); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive || o.Stage != Stages[paragraphIndex]) return false;
        return paragraphIndex switch
        {
            0 or 1 or 12 or 19 or 24 or 25 or 26 or 28 or 30 => o.ActionStackTop == 1,
            2 or 3 => o.ActionStackTop is 23 or 24,
            4 or 5 => o.ActionStackTop is 23 or 25,
            6 or 7 => o.ActionStackTop is 23 or 26,
            8 => o.ActionStackTop is 23 or 27,
            9 => o.ActionStackTop is 23 or 1,
            10 or 11 or 17 => o.ActionStackTop is 1 or 2,
            13 => o.ActionStackTop is 32 or 33,
            14 or 15 => o.ActionStackTop is 32 or 34,
            16 => o.ActionStackTop is 32 or 1,
            18 => o.ActionStackTop is 35 or 2 or 1,
            20 or 31 => true,
            21 => o.ActionStackTop is 6 or 22,
            22 or 23 => Flag(o, 3) || o.ActionStackTop is 6 or 22,
            27 => o.ActionStackTop is 1 or 6 or 36 or 2,
            29 => o.ActionStackTop is 1 or 2 or 6,
            _ => false,
        };
    }

    public static int NextPhase(int paragraphIndex, int phase, TutorialFifthLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return phase;
        //48B410 creates the enemy before193..196, and only creates the three
        //friendly Armies after those instructions, before197..198.
        if (paragraphIndex == 26)
            return phase < 2 && Returned(o) ? phase + 1 : phase;
        if (paragraphIndex == 20)
            return phase switch
            {
                0 when Setup(o) => ConstructionCommandAccepted(o) ? 2 : 1,
                1 when ConstructionCommandAccepted(o) => 2,
                _ => phase,
            };
        if (paragraphIndex is 21 or 22 or 23)
            return phase switch
            {
                0 or 1 or 3 => Setup(o) ? phase + 1 : phase,
                2 => Need(observation.SelectedConstructionSummaryActive,
                    nameof(observation.SelectedConstructionSummaryActive)) ? 3 : 2,
                4 => o.ActionStackTop == 6 && Need(observation.FacilitySelectionActive,
                    nameof(observation.FacilitySelectionActive)) ? 5 : 4,
                5 => o.ActionStackTop == 22 ? 6 : 5,
                6 when o.ActionStackTop == 6 => ConstructionOrder(observation, paragraphIndex - 21) == 8
                    ? paragraphIndex == 23 ? 7 : 6 : 3,
                7 when paragraphIndex == 23 => Flag(o, 3) ? 8 : 7,
                _ => phase,
            };
        if (paragraphIndex == 27)
        {
            if (!Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid))) return phase;
            return phase switch
            {
                0 => Setup(o) ? 1 : 0,
                1 => Need(observation.FakeNewsOrderMatches, nameof(observation.FakeNewsOrderMatches)) ? 2 : 1,
                _ => phase,
            };
        }
        if (phase != 0) return phase;
        return paragraphIndex switch
        {
            0 or 1 or 10 or 12 or 17 or 19 or 25 or 30 => Returned(o) ? 1 : 0,
            2 or 4 or 6 or 13 or 14 or 15 or 31 => Setup(o) ? 1 : 0,
            24 => Need(observation.AllConstructionSitesReady,
                nameof(observation.AllConstructionSitesReady)) ? 1 : 0,
            3 => o.ActionStackTop != 23 && Need(observation.AllianceForceSelectionPresent,
                nameof(observation.AllianceForceSelectionPresent)) && Returned(o) ? 1 : 0,
            5 => !Flag(o,28) && Need(observation.AllianceOfficerSelectionPresent,
                nameof(observation.AllianceOfficerSelectionPresent)) && Returned(o) ? 1 : 0,
            7 => !Flag(o,29) && o.ActionStackTop == 26 && Returned(o) ? 1 : 0,
            8 => !Flag(o,33) && o.ActionStackTop == 23 && Returned(o) ? 1 : 0,
            11 => Flag(o,34) && o.ActionStackTop == 1 && Returned(o) ? 1 : 0,
            18 => Flag(o,38) && Returned(o) ? 1 : 0,
            28 => !Need(observation.EnemyArmyFalseReportActive,
                nameof(observation.EnemyArmyFalseReportActive)) && Returned(o) ? 1 : 0,
            _ => phase,
        };
    }

    public static bool CanComplete(int paragraphIndex, int phase, TutorialFifthLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        return paragraphIndex switch
        {
            0 or 19 or 25 or 30 or 31 => phase == 1,
            26 => phase == 2,
            1 or 12 => phase == 1 && Need(o.LastInformationId, nameof(o.LastInformationId)) ==
                Need(o.RequiredInformationId, nameof(o.RequiredInformationId)),
            2 => o.ActionStackTop == 24,
            3 => o.ActionStackTop == 23,
            4 => o.ActionStackTop == 25,
            5 => Flag(o,28),
            6 => o.ActionStackTop == 26,
            7 => Flag(o,29),
            8 => Flag(o,33),
            9 => Flag(o,26),
            10 or 17 => phase == 1 && Flag(o,4) && o.ActionStackTop == 2,
            11 => phase == 1 && Flag(o,34) && o.ActionStackTop == 1,
            13 => Flag(o,35),
            14 => o.ActionStackTop == 34,
            15 => Flag(o,36),
            16 => Flag(o,37),
            18 => phase == 1 && Flag(o,38) && o.ActionStackTop == 1,
            20 => phase == 2 && o.ActionStackTop == 6,
            21 or 22 => phase == 6 && o.ActionStackTop == 6 &&
                ConstructionOrder(observation, paragraphIndex - 21) == 8,
            23 => phase == 8 && Flag(o,4) && o.ActionStackTop == 2,
            24 => phase == 1 && Need(observation.AllConstructionSitesReady, nameof(observation.AllConstructionSitesReady)),
            27 => !Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid)) ||
                phase == 2 && Returned(o) && Flag(o,4) && o.ActionStackTop == 2,
            28 => phase == 1 && !Need(observation.EnemyArmyFalseReportActive, nameof(observation.EnemyArmyFalseReportActive)),
            29 => IsFreePlayComplete(observation),
            _ => false,
        };
    }

    /// <summary>Whether this update reaches an actual instruction call. Some
    /// paragraphs wait for their real dialog/event before requesting a story.</summary>
    public static bool ShouldRunStories(int paragraphIndex, int phase, TutorialFifthLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        return paragraphIndex switch
        {
            0 or 1 or 10 or 12 or 17 or 19 or 25 or 30 => phase == 0,
            26 => phase is 0 or 1,
            3 => phase == 0 && o.ActionStackTop != 23 && Need(observation.AllianceForceSelectionPresent,
                nameof(observation.AllianceForceSelectionPresent)),
            5 => phase == 0 && !Flag(o,28) && Need(observation.AllianceOfficerSelectionPresent,
                nameof(observation.AllianceOfficerSelectionPresent)),
            7 => phase == 0 && !Flag(o,29) && o.ActionStackTop == 26,
            8 => phase == 0 && !Flag(o,33) && o.ActionStackTop == 23,
            11 => phase == 0 && Flag(o,34) && o.ActionStackTop == 1,
            18 => phase == 0 && Flag(o,38),
            20 => phase == 2 && o.ActionStackTop == 5 && Need(observation.ConstructionOfficerSelectionActive,
                nameof(observation.ConstructionOfficerSelectionActive)),
            21 => phase == 5 && o.ActionStackTop == 6 && Need(observation.FacilitySelectionActive,
                nameof(observation.FacilitySelectionActive)),
            27 => phase == 2 && Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid)),
            28 => phase == 0 && !Need(observation.EnemyArmyFalseReportActive, nameof(observation.EnemyArmyFalseReportActive)),
            _ => false,
        };
    }

    public static bool IsConstructionOfficerSelectionReady(IReadOnlyList<int> selectedOfficerIds)
    {
        ArgumentNullException.ThrowIfNull(selectedOfficerIds);
        return selectedOfficerIds.Count == 3;
    }

    /// <summary>Construction only enables its deployment confirmation when
    /// exactly three actual summary rows have positive officer identities.</summary>
    public static bool AreConstructionDeploymentRowsReady(IReadOnlyList<int> summaryOfficerIds)
    {
        ArgumentNullException.ThrowIfNull(summaryOfficerIds);
        return summaryOfficerIds.Count == 3 && summaryOfficerIds.All(id => id > 0);
    }

    public static bool IsFreePlayComplete(TutorialFifthLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!Need(observation.EnemyOfficerValid, nameof(observation.EnemyOfficerValid))) return true;
        if (!Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid))) return true;
        if (!Need(observation.EnemyArmyCommanderValid, nameof(observation.EnemyArmyCommanderValid))) return true;
        return Need(observation.EnemyArmyCommanderStatus, nameof(observation.EnemyArmyCommanderStatus)) == 6;
    }

    private static int? ConstructionOrder(TutorialFifthLessonObservation observation, int slot)
    {
        var rows = observation.ConstructionSummaryOrderKinds ?? throw Missing(nameof(observation.ConstructionSummaryOrderKinds));
        return slot < rows.Count ? rows[slot] : null;
    }
    //4E69B0 stores a qword zero at20, so54FA60(object+18)'s required
    //command at+C is0. Its second command slot atobject+28 is the13 sentinel.
    private static bool ConstructionCommandAccepted(TutorialLessonObservation observation) =>
        Need(observation.LastCommandId, nameof(observation.LastCommandId)) == 0;
    private static bool Returned(TutorialLessonObservation observation) =>
        Need(observation.InstructionSequenceCompleted, nameof(observation.InstructionSequenceCompleted));
    private static bool Setup(TutorialLessonObservation observation) =>
        Need(observation.ParagraphSetupCompleted, nameof(observation.ParagraphSetupCompleted));
    private static bool Flag(TutorialLessonObservation observation, int index) =>
        (observation.ReadTutorialEventFlag ?? throw Missing("event_flag_reader"))(index);
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("Tutorial fifth lesson is missing the actual " + name + " observation.");
    private static void CheckIndex(int paragraphIndex)
    {
        if ((uint)paragraphIndex >= 32) throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
    }
    private static void CheckPhase(int paragraphIndex, int phase)
    {
        CheckIndex(paragraphIndex);
        if ((uint)phase > (uint)MaximumPhases[paragraphIndex]) throw new ArgumentOutOfRangeException(nameof(phase));
    }
}
