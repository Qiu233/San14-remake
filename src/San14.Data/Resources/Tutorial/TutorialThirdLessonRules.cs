namespace San14.Data;

/// <summary>Current third-lesson observations, read from the actual dialogs and world.</summary>
public sealed record TutorialThirdLessonObservation(TutorialLessonObservation Common)
{
    public bool? UnitActionDropdownVisible { get; init; }
    public int? SummaryCount { get; init; }
    public int? TargetSelectionCount { get; init; }
    public bool? OfficerSelectionVisible { get; init; }
    public int? SelectedSummaryOfficerId { get; init; }
    public bool? SelectedSummaryPresent { get; init; }
    public bool? UnitConfigurationPresent { get; init; }
    public bool? UnitConfigurationActive { get; init; }
    public int? SelectedLandFormationId { get; init; }
    public bool? KanuSummaryActive { get; init; }
    public int? KanuSummaryOrderKind { get; init; }
    public bool? ChouhiSummaryActive { get; init; }
    public int? ChouhiSummaryOrderKind { get; init; }
    /// <summary>Native validity of the enemy officer at physical slot 149.</summary>
    public bool? EnemyOfficerValid { get; init; }
    /// <summary>Native validity of that officer's actual commander Army.</summary>
    public bool? EnemyArmyValid { get; init; }
    public bool? EnemyArmyCommanderValid { get; init; }
    public int? EnemyArmyCommanderStatus { get; init; }
    /// <summary>The actual enemy Army byte 26 bit 1, without reversing its source meaning.</summary>
    public bool? EnemyArmySupplyFlag { get; init; }
    public bool? KanuAttacksEnemy { get; init; }
    /// <summary>Valid Army of person 147 and valid camera target denote the same actual Hex.</summary>
    public bool? AreaSupportFocusMatchesArmyHex { get; init; }
    public int? CameraFocusHexId { get; init; }
    public uint? AreaSupportElapsedMilliseconds { get; init; }
    public uint? CurrentMilliseconds { get; init; }
    public uint? AreaSupportFocusStartedMilliseconds { get; init; }
    /// <summary>The actual 2AFC20 result for person 147's Army contains commander identity 656.</summary>
    public bool? KanuRelatedArmiesContainChouhi { get; init; }
    public bool? ProgressStateExists { get; init; }
}

/// <summary>One actual Tactics paragraph update, retaining the original counter-before-callback order.</summary>
public sealed record TutorialThirdTacticsUpdate(int Counter, bool RequestInstruction);

public static class TutorialThirdLessonRules
{
    public const uint AreaSupportHoldMilliseconds = 2000;
    private static readonly int[] Stages = [38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50];
    private static readonly int[] FollowingStages = [39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 0];

    public static int NextStage(int paragraphIndex)
    {
        CheckIndex(paragraphIndex);
        return FollowingStages[paragraphIndex];
    }

    /// <summary>AttackKangai skips Tactics when its actual enemy commander Army is no longer valid.</summary>
    public static int NextStage(int paragraphIndex, TutorialThirdLessonObservation observation)
    {
        CheckIndex(paragraphIndex);
        ArgumentNullException.ThrowIfNull(observation);
        return paragraphIndex == 8 && !Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid))
            ? 48 : FollowingStages[paragraphIndex];
    }

    public static bool CanStart(int paragraphIndex, TutorialThirdLessonObservation observation)
    {
        CheckIndex(paragraphIndex);
        ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        // Unlike lesson two, these flag alternatives remain inside their Stage gate.
        if (!o.TutorialActive || o.Stage != Stages[paragraphIndex]) return false;
        return paragraphIndex switch
        {
            0 or 1 or 6 or 7 or 11 => o.ActionStackTop == 1,
            2 => Flag(o, 5) || o.ActionStackTop == 1,
            3 => Flag(o, 0) || o.ActionStackTop is 4 or 5,
            4 => o.ActionStackTop == 6,
            5 => Flag(o, 3) || o.ActionStackTop == 6,
            8 => o.ActionStackTop is 1 or 6 or 2,
            9 => o.ActionStackTop == 2,
            10 => o.ActionStackTop is 1 or 2 or 6,
            12 => o.ActionStackTop is 1 or 2,
            _ => false,
        };
    }

    public static int NextPhase(int paragraphIndex, int phase, TutorialThirdLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase);
        ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return phase;
        if (paragraphIndex == 3)
        {
            return phase switch
            {
                0 or 3 or 7 => InstructionsReturned(o) ? phase + 1 : phase,
                1 => Need(observation.UnitActionDropdownVisible, nameof(observation.UnitActionDropdownVisible)) ? 2 : 1,
                2 => Need(observation.UnitActionDropdownVisible, nameof(observation.UnitActionDropdownVisible)) ? 2 :
                    Need(observation.SummaryCount, nameof(observation.SummaryCount)) == 1 ? 3 : 1,
                4 => o.ActionStackTop == 5 ? 5 : 4,
                5 => o.ActionStackTop == 5 ? 5 :
                    Need(observation.SummaryCount, nameof(observation.SummaryCount)) == 2 ? 6 : 4,
                6 => Need(observation.SelectedSummaryPresent, nameof(observation.SelectedSummaryPresent)) &&
                    Need(observation.SelectedSummaryOfficerId, nameof(observation.SelectedSummaryOfficerId)) == 656 ? 7 : 6,
                8 => Need(observation.UnitConfigurationPresent, nameof(observation.UnitConfigurationPresent)) &&
                    Need(observation.UnitConfigurationActive, nameof(observation.UnitConfigurationActive)) &&
                    Need(observation.SelectedLandFormationId, nameof(observation.SelectedLandFormationId)) == 2 ? 9 : 8,
                9 or 10 => Need(observation.UnitConfigurationPresent, nameof(observation.UnitConfigurationPresent)) &&
                    InstructionsReturned(o) ? phase + 1 : phase,
                _ => phase,
            };
        }
        if (paragraphIndex == 4)
            return phase switch
            {
                0 => SetupCompleted(o) ? 1 : 0,
                1 => InstructionsReturned(o) ? 2 : 1,
                2 => Need(observation.KanuSummaryActive, nameof(observation.KanuSummaryActive)) ? 3 : 2,
                3 => observation.KanuSummaryOrderKind == 1 ? 3 :
                    Need(observation.TargetSelectionCount, nameof(observation.TargetSelectionCount)) == 0 ? 2 : 3,
                _ => phase,
            };
        if (paragraphIndex == 5)
            return phase switch
            {
                0 => SetupCompleted(o) ? 1 : 0,
                1 => Need(observation.ChouhiSummaryActive, nameof(observation.ChouhiSummaryActive)) ? 2 : 1,
                2 => observation.ChouhiSummaryOrderKind == 1 ? 3 :
                    Need(observation.TargetSelectionCount, nameof(observation.TargetSelectionCount)) == 0 ? 1 : 2,
                3 => Flag(o, 3) ? 4 : 3,
                _ => phase,
            };
        if (paragraphIndex == 6)
            return phase switch
            {
                0 => InstructionsReturned(o) ? 1 : 0,
                1 => IsAreaSupportHoldComplete(observation) ? 2 : 1,
                _ => phase,
            };
        if (paragraphIndex == 8)
            return phase switch
            {
                0 => SetupCompleted(o) ? 1 : 0,
                1 => Need(observation.KanuAttacksEnemy, nameof(observation.KanuAttacksEnemy)) ? 2 : 1,
                _ => phase,
            };
        // Tactics owns a counter, not a sequence of instruction phases. Its host
        // consumes ReadTacticsUpdate once per actual paragraph update, never in
        // a synchronous phase-advance loop.
        if (paragraphIndex == 9) return phase;
        if (phase != 0) return phase;
        if (paragraphIndex == 2 || paragraphIndex is 10 or 12)
            return SetupCompleted(o) ? 1 : 0;
        if (paragraphIndex == 7 && !ShouldRunCutSupplyInstruction(observation)) return 0;
        return InstructionsReturned(o) ? 1 : 0;
    }

    public static bool CanComplete(int paragraphIndex, int phase, TutorialThirdLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase);
        ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        if (paragraphIndex is 7 or 8 or 9 &&
            !Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid))) return true;
        if (paragraphIndex == 10) return IsFreePlayComplete(observation);
        return paragraphIndex switch
        {
            0 or 1 or 11 or 12 => phase == 1,
            2 => phase == 1 && Need(o.LastCommandId, nameof(o.LastCommandId)) == 0,
            3 => phase == 11 && Flag(o, 0),
            4 => phase == 3 && observation.KanuSummaryOrderKind == 1,
            5 => phase == 4 && Flag(o, 4) && o.ActionStackTop == 2,
            6 => phase == 2 && InstructionsReturned(o),
            7 => phase == 1,
            8 => phase == 2 && Flag(o, 4) && o.ActionStackTop == 2,
            9 => phase == 45 && InstructionsReturned(o),
            _ => false,
        };
    }

    /// <summary>The selector's real accepted set is exactly persons 147 and 656.</summary>
    public static bool CanAcceptOfficerSelection(IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(officerIds);
        return officerIds.Count == 2 && officerIds.Contains(147) && officerIds.Contains(656);
    }

    public static bool ShouldRunCutSupplyInstruction(TutorialThirdLessonObservation observation) =>
        Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid)) &&
        !Need(observation.EnemyArmySupplyFlag, nameof(observation.EnemyArmySupplyFlag));

    public static bool ShouldRunStories(int paragraphIndex, int phase,
        TutorialThirdLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase);
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.Common.TutorialActive) return false;
        return paragraphIndex switch
        {
            0 or 1 or 11 => phase == 0,
            3 => phase is 0 or 3 or 7 || phase is 9 or 10 &&
                Need(observation.UnitConfigurationPresent, nameof(observation.UnitConfigurationPresent)),
            4 => phase == 1,
            6 => phase is 0 or 2,
            7 => phase == 0 && ShouldRunCutSupplyInstruction(observation),
            9 => ReadTacticsUpdate(phase, observation).RequestInstruction,
            _ => false,
        };
    }

    /// <summary>Returns the paragraph timer after this actual observation; no focus match resets it.</summary>
    public static uint ReadAreaSupportFocusStart(uint previousStartMilliseconds,
        TutorialThirdLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!Need(observation.AreaSupportFocusMatchesArmyHex, nameof(observation.AreaSupportFocusMatchesArmyHex))) return 0;
        return previousStartMilliseconds != 0 ? previousStartMilliseconds :
            Need(observation.CurrentMilliseconds, nameof(observation.CurrentMilliseconds));
    }

    public static bool IsAreaSupportHoldComplete(TutorialThirdLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!Need(observation.AreaSupportFocusMatchesArmyHex, nameof(observation.AreaSupportFocusMatchesArmyHex))) return false;
        return observation.AreaSupportElapsedMilliseconds is uint elapsed && elapsed > AreaSupportHoldMilliseconds;
    }

    /// <summary>Counter advances on a matching update; instruction is requested on a later matching update.</summary>
    public static TutorialThirdTacticsUpdate ReadTacticsUpdate(int counter,
        TutorialThirdLessonObservation observation)
    {
        if ((uint)counter > 45) throw new ArgumentOutOfRangeException(nameof(counter));
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.Common.TutorialActive || observation.Common.Stage != 47 || observation.Common.ActionStackTop != 2 ||
            !Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid)) ||
            !Need(observation.KanuRelatedArmiesContainChouhi, nameof(observation.KanuRelatedArmiesContainChouhi)))
            return new(counter, false);
        return counter < 45 ? new(counter + 1, false) :
            new(counter, Need(observation.ProgressStateExists, nameof(observation.ProgressStateExists)));
    }

    public static bool IsFreePlayComplete(TutorialThirdLessonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!Need(observation.EnemyOfficerValid, nameof(observation.EnemyOfficerValid)) ||
            !Need(observation.EnemyArmyValid, nameof(observation.EnemyArmyValid)) ||
            !Need(observation.EnemyArmyCommanderValid, nameof(observation.EnemyArmyCommanderValid))) return true;
        return Need(observation.EnemyArmyCommanderStatus, nameof(observation.EnemyArmyCommanderStatus)) == 6;
    }

    private static bool InstructionsReturned(TutorialLessonObservation observation) =>
        Need(observation.InstructionSequenceCompleted, nameof(observation.InstructionSequenceCompleted));
    private static bool SetupCompleted(TutorialLessonObservation observation) =>
        Need(observation.ParagraphSetupCompleted, nameof(observation.ParagraphSetupCompleted));
    private static bool Flag(TutorialLessonObservation observation, int index) =>
        (observation.ReadTutorialEventFlag ?? throw new InvalidDataException(
            "Tutorial third lesson is missing the actual event-flag reader."))(index);
    private static T Need<T>(T? value, string owner) where T : struct => value ??
        throw new InvalidDataException($"Tutorial third lesson is missing the actual {owner} observation.");
    private static void CheckIndex(int index)
    {
        if ((uint)index >= (uint)Stages.Length) throw new ArgumentOutOfRangeException(nameof(index));
    }
    private static void CheckPhase(int index, int phase)
    {
        CheckIndex(index);
        int maximum = index switch { 3 => 11, 4 => 3, 5 => 4, 6 or 8 => 2, 9 => 45, _ => 1 };
        if (phase < 0 || phase > maximum) throw new ArgumentOutOfRangeException(nameof(phase));
    }
}
