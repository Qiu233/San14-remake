namespace San14.Data;

/// <summary>Actual fourth-course controls and business facts. World facts are filled by the rule adapter.</summary>
public sealed record TutorialFourthLessonObservation(TutorialLessonObservation Common)
{
    public int? SelectedStrategistId { get; init; }
    public bool? GovernmentEffectDialogVisible { get; init; }
    public bool? ConstructionFacilityDialogVisible { get; init; }
    public int? ConstructionFacilityId { get; init; }
    public bool? ConstructionExplanationShown { get; init; }
    public int? ConstructionInstructionKind { get; init; }
    public int? ConstructionCommanderId { get; init; }
    public int? ConstructionTargetHex { get; init; }
    public int? DeploymentRowCount { get; init; }
    public int? DeploymentSelectorMemberCount { get; init; }
    public bool? ConstructionCompleted { get; init; }
    /// <summary>A real queued player Army capture of City5 (5613A0), before settlement.</summary>
    public bool? AttackCityOrderPresent { get; init; }
    public bool? TrackedAttackArmyValid { get; init; }
    public bool? TrackedArmyAttacksTargetCity { get; init; }
    public bool? TargetCityHexOnFire { get; init; }
    public bool? TargetCityPlayerOwned { get; init; }
    public bool? NanpiExplanationReady { get; init; }
}

/// <summary>The sixteen source paragraphs, with their independent local phases.</summary>
public static class TutorialFourthLessonRules
{
    public static int NextStage(int paragraphIndex)
    {
        CheckIndex(paragraphIndex);
        return paragraphIndex == 15 ? 0 : 52 + paragraphIndex;
    }

    public static bool CanStart(int paragraphIndex, TutorialFourthLessonObservation observation)
    {
        CheckIndex(paragraphIndex); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive || o.Stage != 51 + paragraphIndex) return false;
        return paragraphIndex switch
        {
            0 or 1 or 8 or 10 or 14 or 15 => o.ActionStackTop == 1,
            2 or 4 => o.ActionStackTop == 18,
            3 => o.ActionStackTop is 18 or 19,
            6 or 11 or 13 => o.ActionStackTop is 1 or 2,
            7 => o.ActionStackTop is 1 or 18 or 21,
            5 or 9 or 12 => true,
            _ => false,
        };
    }

    public static int NextPhase(int paragraphIndex, int phase, TutorialFourthLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return phase;
        if (paragraphIndex == 3) return phase switch
        {
            0 when o.ActionStackTop == 19 => 2,
            2 when o.ActionStackTop == 18 && observation.SelectedStrategistId != 147 => 0,
            _ => phase,
        };
        if (paragraphIndex == 7) return phase switch
        {
            0 or 2 or 5 => Returned(o) ? phase + 1 : phase,
            1 when Need(o.LastInformationId, nameof(o.LastInformationId)) == 12 => 2,
            3 when EffectVisible(observation) && Returned(o) => 4,
            4 when o.ActionStackTop == 18 => 5,
            _ => phase,
        };
        if (paragraphIndex == 9) return phase switch
        {
            0 when Need(o.LastCommandId, nameof(o.LastCommandId)) == 0 => 1,
            1 when o.ActionStackTop == 6 => 2,
            2 when Setup(o) => 3,
            3 when FacilityVisible(observation) => 4,
            4 when o.ActionStackTop == 22 => 5,
            5 when o.ActionStackTop == 6 => observation.ConstructionInstructionKind == 8 ? 6 : 2,
            6 when o.ActionStackTop == 1 => 7,
            _ => phase,
        };
        if (paragraphIndex == 12)
        {
            if (o.ActionStackTop is not (1 or 2 or 6)) return phase;
            return phase switch
            {
                0 when o.ActionStackTop == 1 && Returned(o) => 1,
                1 when o.ActionStackTop != 2 && (Need(observation.TrackedAttackArmyValid, nameof(observation.TrackedAttackArmyValid)) ||
                    Need(observation.AttackCityOrderPresent, nameof(observation.AttackCityOrderPresent))) => 2,
                2 when o.ActionStackTop != 2 && !Need(observation.TrackedAttackArmyValid, nameof(observation.TrackedAttackArmyValid)) => 1,
                2 when o.ActionStackTop != 2 && Need(observation.TrackedArmyAttacksTargetCity, nameof(observation.TrackedArmyAttacksTargetCity)) => 3,
                3 when Flag(o, 4) && o.ActionStackTop == 2 => 4,
                4 when !Need(observation.TrackedAttackArmyValid, nameof(observation.TrackedAttackArmyValid)) => 1,
                _ => phase,
            };
        }
        if (paragraphIndex == 13) return phase switch
        {
            0 when Setup(o) => 1,
            1 when Need(observation.NanpiExplanationReady, nameof(observation.NanpiExplanationReady)) && Returned(o) => 2,
            _ => phase,
        };
        if (phase != 0) return phase;
        if (paragraphIndex == 10) return Need(observation.ConstructionCompleted, nameof(observation.ConstructionCompleted)) && Returned(o) ? 1 : 0;
        if (paragraphIndex == 11) return o.ActionStackTop == 1 && Setup(o) ? 1 : 0;
        if (paragraphIndex is 1 or 6 or 15) return Setup(o) ? 1 : 0;
        return Returned(o) ? 1 : 0;
    }

    public static bool CanComplete(int paragraphIndex, int phase, TutorialFourthLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase); ArgumentNullException.ThrowIfNull(observation);
        var o = observation.Common;
        if (!o.TutorialActive) return false;
        return paragraphIndex switch
        {
            0 or 2 or 8 or 10 or 14 or 15 => phase == 1,
            1 => phase == 1 && Need(o.LastInformationId, nameof(o.LastInformationId)) == 12,
            3 => phase == 2 && o.ActionStackTop == 18 && observation.SelectedStrategistId == 147,
            4 => phase == 1 && Flag(o, 76),
            5 => phase == 1 && o.ActionStackTop != 18,
            6 => phase == 1 && Flag(o, 4) && o.ActionStackTop == 2,
            7 => phase == 6 && o.ActionStackTop == 1,
            9 => phase == 7 && Flag(o, 4) && o.ActionStackTop == 2,
            11 => phase == 1 && o.ActionStackTop == 1 && Need(observation.AttackCityOrderPresent, nameof(observation.AttackCityOrderPresent)),
            12 => phase == 4 && Need(observation.TrackedAttackArmyValid, nameof(observation.TrackedAttackArmyValid)) && Need(observation.TargetCityHexOnFire, nameof(observation.TargetCityHexOnFire)),
            13 => phase == 2 && Need(observation.TargetCityPlayerOwned, nameof(observation.TargetCityPlayerOwned)),
            _ => false,
        };
    }

    internal static bool EffectVisible(TutorialFourthLessonObservation o) => o.Common.ActionStackTop == 21 && Need(o.GovernmentEffectDialogVisible, nameof(o.GovernmentEffectDialogVisible));
    internal static bool FacilityVisible(TutorialFourthLessonObservation o) => o.Common.ActionStackTop == 6 && Need(o.ConstructionFacilityDialogVisible, nameof(o.ConstructionFacilityDialogVisible));
    private static bool Setup(TutorialLessonObservation o) => Need(o.ParagraphSetupCompleted, nameof(o.ParagraphSetupCompleted));
    private static bool Returned(TutorialLessonObservation o) => Need(o.InstructionSequenceCompleted, nameof(o.InstructionSequenceCompleted));
    private static bool Flag(TutorialLessonObservation o, int index) => (o.ReadTutorialEventFlag ?? throw new InvalidDataException("tutorial.fourth_lesson.event_reader"))(index);
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw new InvalidDataException("tutorial.fourth_lesson.observation." + name);
    private static void CheckIndex(int index) { if ((uint)index >= 16) throw new ArgumentOutOfRangeException(nameof(index)); }
    private static void CheckPhase(int index, int phase)
    {
        CheckIndex(index);
        int maximum = index switch { 3 => 2, 7 => 6, 9 => 7, 12 => 4, 13 => 2, _ => 1 };
        if (phase < 0 || phase > maximum) throw new ArgumentOutOfRangeException(nameof(phase));
    }
}
