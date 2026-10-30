namespace San14.Data;

/// <summary>Actual second-lesson UI and world facts, supplied by the lesson host.</summary>
public sealed record TutorialSecondLessonObservation(TutorialLessonObservation Common)
{
    /// <summary>The actual AUTab is registered in the UI manager's active-control lists.</summary>
    public bool? FormationSelectionActive { get; init; }
    public int? SelectedFormationId { get; init; }
    /// <summary>Actual land-troop threshold marker, calculated by the ordinary draft figures.</summary>
    public DeploymentTroopMarker? TroopMarker { get; init; }
    public bool? TargetOrderMatchesCapital { get; init; }
    public bool? TargetInformationVisible { get; init; }
    public bool? TargetExplanationShown { get; init; }
    public bool? CapitalHexBelongsToPlayer { get; init; }
    public int? CapitalOccupation { get; init; }
    public int? CapitalMaximumOccupation { get; init; }
    public bool? LessonMapStateVisible { get; init; }
    public bool? InstructionOverlayActive { get; init; }
    /// <summary>All three actual Areas are valid, player-owned and at maximum occupation.</summary>
    public bool? ConquerAreasComplete { get; init; }
}

/// <summary>Admission and local phase rules from the fourteen second-lesson paragraphs.</summary>
public static class TutorialSecondLessonRules
{
    private static readonly int[] Stages =
        [24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37];
    private static readonly int[] FollowingStages =
        [25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 0];

    public static int NextStage(int paragraphIndex)
    {
        CheckIndex(paragraphIndex);
        return FollowingStages[paragraphIndex];
    }

    public static bool CanStart(int paragraphIndex, TutorialSecondLessonObservation observation)
    {
        CheckIndex(paragraphIndex);
        ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        // These source predicates accept their actual event before testing Stage.
        if (paragraphIndex == 1 && Flag(o, 5) || paragraphIndex == 4 && Flag(o, 1) ||
            paragraphIndex == 7 && Flag(o, 0) || paragraphIndex == 9 && Flag(o, 3)) return true;
        if (o.Stage != Stages[paragraphIndex]) return false;
        return paragraphIndex switch
        {
            0 or 1 or 12 => o.ActionStackTop == 1,
            2 => o.ActionStackTop is 4 or 5,
            3 or 4 => o.ActionStackTop == 5,
            5 or 6 or 7 => o.ActionStackTop == 4,
            8 or 9 => o.ActionStackTop == 6,
            10 => Need(observation.CapitalHexBelongsToPlayer, nameof(observation.CapitalHexBelongsToPlayer)) &&
                o.ActionStackTop is 1 or 6,
            11 or 13 => true,
            _ => false,
        };
    }

    /// <summary>Pure phase progression; the caller owns all corresponding entry writes and stories.</summary>
    public static int NextPhase(int paragraphIndex, int phase, TutorialSecondLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase);
        ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return phase;
        if (paragraphIndex == 11 && !AreaInstructionsAvailable(observation)) return phase;
        if (paragraphIndex == 6)
        {
            return phase switch
            {
                0 or 2 => InstructionsReturned(o) ? phase + 1 : phase,
                1 => Need(observation.TroopMarker, nameof(observation.TroopMarker)) ==
                    DeploymentTroopMarker.AtSecondaryThreshold ? 2 : 1,
                _ => phase,
            };
        }
        if (paragraphIndex == 9)
        {
            return phase switch
            {
                0 => SetupCompleted(o) ? 1 : 0,
                1 => Need(observation.TargetOrderMatchesCapital, nameof(observation.TargetOrderMatchesCapital)) ? 2 : 1,
                2 => Flag(o, 3) ? 3 : 2,
                3 => InstructionsReturned(o) ? 4 : 3,
                _ => phase,
            };
        }
        if (paragraphIndex == 11)
            return phase is 0 or 1 && InstructionsReturned(o) ? phase + 1 : phase;
        if (phase != 0) return phase;
        if (paragraphIndex == 2 && o.ActionStackTop == 5) return 1;
        if (paragraphIndex is 1 or 4 or 7 or 13)
            return SetupCompleted(o) ? 1 : 0;
        return InstructionsReturned(o) ? 1 : 0;
    }

    public static bool CanComplete(int paragraphIndex, int phase, TutorialSecondLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase);
        ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        // These source actions can already be complete when their paragraph is admitted.
        if (paragraphIndex == 2 && o.ActionStackTop == 5 ||
            paragraphIndex == 4 && Flag(o, 1) || paragraphIndex == 7 && Flag(o, 0)) return true;
        if (phase == 0) return false;
        return paragraphIndex switch
        {
            0 or 3 or 8 or 12 or 13 => true,
            1 => Need(o.LastCommandId, nameof(o.LastCommandId)) == 0,
            2 => o.ActionStackTop == 5,
            4 => Flag(o, 1),
            5 => Need(observation.FormationSelectionActive, nameof(observation.FormationSelectionActive)) &&
                Need(observation.SelectedFormationId, nameof(observation.SelectedFormationId)) == 5,
            6 => phase == 3,
            7 => Flag(o, 0),
            9 => phase == 4 && Flag(o, 4) && o.ActionStackTop == 2,
            10 => Need(observation.CapitalOccupation, nameof(observation.CapitalOccupation)) >=
                Need(observation.CapitalMaximumOccupation, nameof(observation.CapitalMaximumOccupation)),
            11 => phase == 2 && AreaInstructionsAvailable(observation) &&
                Need(observation.ConquerAreasComplete, nameof(observation.ConquerAreasComplete)),
            _ => false,
        };
    }

    internal static bool AreaInstructionsAvailable(TutorialSecondLessonObservation observation) =>
        !Need(observation.InstructionOverlayActive, nameof(observation.InstructionOverlayActive)) &&
        Need(observation.LessonMapStateVisible, nameof(observation.LessonMapStateVisible));

    private static bool SetupCompleted(TutorialLessonObservation observation) =>
        Need(observation.ParagraphSetupCompleted, nameof(observation.ParagraphSetupCompleted));
    private static bool InstructionsReturned(TutorialLessonObservation observation) =>
        Need(observation.InstructionSequenceCompleted, nameof(observation.InstructionSequenceCompleted));
    private static bool Flag(TutorialLessonObservation observation, int index) =>
        (observation.ReadTutorialEventFlag ?? throw new InvalidDataException(
            "Tutorial second lesson is missing the actual event-flag reader."))(index);
    private static T Need<T>(T? value, string owner) where T : struct => value ??
        throw new InvalidDataException($"Tutorial second lesson is missing the actual {owner} observation.");
    private static void CheckIndex(int index)
    {
        if ((uint)index >= (uint)Stages.Length) throw new ArgumentOutOfRangeException(nameof(index));
    }
    private static void CheckPhase(int index, int phase)
    {
        CheckIndex(index);
        int maximum = index switch { 6 => 3, 9 => 4, 11 => 2, _ => 1 };
        if (phase < 0 || phase > maximum) throw new ArgumentOutOfRangeException(nameof(phase));
    }
}
