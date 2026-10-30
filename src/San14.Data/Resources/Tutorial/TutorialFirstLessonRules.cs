namespace San14.Data;

/// <summary>Actual tutorial and UI observations, sampled by the lesson host.</summary>
public sealed record TutorialLessonObservation(bool TutorialActive, int Stage,
    int? ActionStackTop, Func<int, bool> ReadTutorialEventFlag)
{
    public TutorialSecondLessonObservation? SecondLesson { get; init; }
    public TutorialThirdLessonObservation? ThirdLesson { get; init; }
    public TutorialFourthLessonObservation? FourthLesson { get; init; }
    public TutorialFifthLessonObservation? FifthLesson { get; init; }
    public TutorialSixthLessonObservation? SixthLesson { get; init; }
    public TutorialSeventhLessonObservation? SeventhLesson { get; init; }
    public TutorialEighthLessonObservation? EighthLesson { get; init; }
    public bool? DomesticSelectionOpen { get; init; }
    public bool? ProposalDetailVisible { get; init; }
    public bool? ProposalListVisible { get; init; }
    public bool? CityFirstSelectorVisible { get; init; }
    public bool? CitySecondSelectorVisible { get; init; }

    /// <summary>The current paragraph's instruction callback has actually returned.</summary>
    public bool? InstructionSequenceCompleted { get; init; }
    /// <summary>The current paragraph's synchronous UI and input-gate initialization was applied.</summary>
    public bool? ParagraphSetupCompleted { get; init; }
    public int? LastCommandId { get; init; }
    public int? RequiredCommandId { get; init; }
    public int? LastInformationId { get; init; }
    public int? RequiredInformationId { get; init; }
}

/// <summary>First-lesson admission and completion rules, independent of world mutation.</summary>
public static class TutorialFirstLessonRules
{
    private static readonly int[] Stages =
        [1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23];
    private static readonly int[] FollowingStages =
        [2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 0];

    /// <summary>
    /// Tutorial permission for one of the thirteen command entries. A permitted bit
    /// does not override the command's ordinary eligibility or disabled state.
    /// </summary>
    public static bool IsCommandAllowed(int commandId, uint enabledCommandMask)
    {
        if ((uint)commandId >= 13)
            throw new ArgumentOutOfRangeException(nameof(commandId));
        return (enabledCommandMask & (1u << commandId)) != 0;
    }

    public static int NextStage(int paragraphIndex)
    {
        CheckIndex(paragraphIndex);
        return FollowingStages[paragraphIndex];
    }

    /// <summary>Original paragraph vslot 2. This admits a paragraph; it does not finish it.</summary>
    public static bool CanStart(int paragraphIndex, TutorialLessonObservation observation)
    {
        CheckIndex(paragraphIndex);
        ArgumentNullException.ThrowIfNull(observation);
        var o = observation;

        // These two source predicates inspect an actual dialog before the stage gate.
        if (paragraphIndex == 3)
        {
            if (o.DomesticSelectionOpen == true) return true;
            if (o.TutorialActive && o.Stage == 4 && o.ActionStackTop == 1) return true;
            return Need(o.DomesticSelectionOpen, nameof(o.DomesticSelectionOpen));
        }
        if (paragraphIndex == 15)
        {
            if (o.ProposalDetailVisible == true) return true;
            if (o.TutorialActive && o.Stage == 17 && o.ActionStackTop == 12 &&
                Need(o.ProposalListVisible, nameof(o.ProposalListVisible))) return true;
            return Need(o.ProposalDetailVisible, nameof(o.ProposalDetailVisible));
        }

        if (!o.TutorialActive) return false;
        if (paragraphIndex == 4) return Flag(o, 11) || o.Stage == 5;
        if (paragraphIndex == 8)
            return Flag(o, 15) || o.Stage == 10 && o.ActionStackTop == 10;
        if (o.Stage != Stages[paragraphIndex]) return false;

        return paragraphIndex switch
        {
            7 or 13 => o.ActionStackTop is 1 or 2,
            9 or 19 => Flag(o, 16) || o.ActionStackTop == 11,
            11 => o.ActionStackTop is 1 or 16,
            12 => o.ActionStackTop == 16,
            16 => o.ActionStackTop == 12,
            17 => o.ActionStackTop is 1 or 12,
            18 => true,
            _ => o.ActionStackTop == 1,
        };
    }

    /// <summary>
    /// Advances only paragraph-local phases. Instruction callbacks, gate initialization,
    /// and accepted commands remain owned by the host; this method performs none of them.
    /// </summary>
    public static int NextPhase(int paragraphIndex, int phase, TutorialLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase);
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.TutorialActive) return phase;
        if (paragraphIndex == 12)
        {
            return phase switch
            {
                0 or 3 or 6 => InstructionsReturned(observation) ? phase + 1 : phase,
                1 => Need(observation.CityFirstSelectorVisible,
                    nameof(observation.CityFirstSelectorVisible)) ? 2 : 1,
                2 => Flag(observation, 23) ? 3 : 2,
                4 => Need(observation.CitySecondSelectorVisible,
                    nameof(observation.CitySecondSelectorVisible)) ? 5 : 4,
                5 => Flag(observation, 24) ? 6 : 5,
                _ => phase,
            };
        }
        if (phase != 0) return phase;

        // These source bodies initialize their UI/gate directly, without a 515C60
        // instruction callback. The host must perform that initialization before
        // publishing this phase, including each helper's source input reset.
        if (paragraphIndex is 2 or 6 or 11 or 13 or 16 or 18)
            return Need(observation.ParagraphSetupCompleted,
                nameof(observation.ParagraphSetupCompleted)) ? 1 : 0;
        return InstructionsReturned(observation) ? 1 : 0;
    }

    /// <summary>
    /// Original completion checks after the corresponding paragraph setup/callback.
    /// An admission predicate or an initial bitmap bit cannot stand in for that setup.
    /// </summary>
    public static bool CanComplete(int paragraphIndex, int phase, TutorialLessonObservation observation)
    {
        CheckPhase(paragraphIndex, phase);
        ArgumentNullException.ThrowIfNull(observation);
        if (!observation.TutorialActive || phase == 0) return false;

        return paragraphIndex switch
        {
            2 or 11 => Need(observation.LastCommandId, nameof(observation.LastCommandId)) ==
                Need(observation.RequiredCommandId, nameof(observation.RequiredCommandId)),
            3 => Need(observation.DomesticSelectionOpen, nameof(observation.DomesticSelectionOpen)),
            4 => Flag(observation, 11),
            5 => Flag(observation, 13),
            6 => Flag(observation, 14),
            7 or 13 or 18 => Flag(observation, 4) && observation.ActionStackTop == 2,
            8 => Flag(observation, 15),
            9 or 19 => Flag(observation, 16),
            12 => phase == 7 && Flag(observation, 25),
            14 => Need(observation.LastInformationId, nameof(observation.LastInformationId)) ==
                Need(observation.RequiredInformationId, nameof(observation.RequiredInformationId)),
            15 => Need(observation.ProposalDetailVisible, nameof(observation.ProposalDetailVisible)),
            16 => Flag(observation, 18),
            17 => observation.ActionStackTop == 1,
            // Synchronous instruction bodies advance only after their real return.
            0 or 1 or 10 or 20 or 21 => true,
            _ => false,
        };
    }

    private static bool InstructionsReturned(TutorialLessonObservation observation) =>
        Need(observation.InstructionSequenceCompleted, nameof(observation.InstructionSequenceCompleted));

    private static bool Flag(TutorialLessonObservation observation, int index)
    {
        if (observation.ReadTutorialEventFlag is null)
            throw new InvalidDataException("Tutorial first lesson is missing the actual event-flag reader.");
        return observation.ReadTutorialEventFlag(index);
    }

    private static T Need<T>(T? value, string owner) where T : struct =>
        value ?? throw new InvalidDataException($"Tutorial first lesson is missing the actual {owner} observation.");

    private static void CheckIndex(int paragraphIndex)
    {
        if ((uint)paragraphIndex >= (uint)Stages.Length)
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
    }

    private static void CheckPhase(int paragraphIndex, int phase)
    {
        CheckIndex(paragraphIndex);
        if (phase < 0 || phase > (paragraphIndex == 12 ? 7 : 1))
            throw new ArgumentOutOfRangeException(nameof(phase));
    }
}
