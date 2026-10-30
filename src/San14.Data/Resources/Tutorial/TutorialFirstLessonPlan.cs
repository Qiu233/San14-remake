namespace San14.Data;

/// <summary>One original first-lesson paragraph phase and its instruction/setup inputs.</summary>
public sealed record TutorialParagraphPlan(int ParagraphIndex, int EntryPhase,
    IReadOnlyList<int> StoryEventIds, int? RequiredCommandId,
    int? RequiredInformationId, bool ResetProgressRequested)
{
    public int CallbackPhase { get; init; }
    public bool UsesInstructionCallback { get; init; }
    public uint? EnabledCommandMask { get; init; }
    public int? ResetLastCommandId { get; init; }
    public int? ResetLastInformationId { get; init; }
    public IReadOnlyList<int> BeforeStoryClearEventFlags { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> AfterStoryClearEventFlags { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> CompletionClearEventFlags { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> CompletionSetEventFlags { get; init; } = Array.Empty<int>();

    /// <summary>An actual corrective Game message, separate from the instruction event list.</summary>
    public int? CorrectionGameMessageId { get; init; }
}

/// <summary>
/// Instruction order and gate operands from the first lesson's actual paragraph
/// bodies, callback bodies and initializers. These are not computed from paragraph indices.
/// </summary>
public static class TutorialFirstLessonPlan
{
    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();
    private static readonly IReadOnlyList<int>[] InitialStories =
    [
        Events(100),                   // Opening: 4BAC50
        Events(101, 102, 103, 104),    // SummaryCity: 4C39F0
        Empty,                        // SelectDomestic: command gate 54FA60
        Events(105),                  // SelectDomesticArea callback 4847B0
        Events(106),                  // SelectDomesticPerson callback 4884A0
        Events(107, 108, 109, 110),    // Priority callback 4871B0, in this exact order
        Empty,                        // DomesticDecide: accepted action flag
        Events(111),                  // Progress callback 489C80
        Events(112),                  // ReportAction callback 48C3B0
        Events(113),                  // ReportIncome1 callback 484630
        Events(114, 115, 116),         // CityDomesticIntro: 4AC470
        Empty,                        // CityDomesticSelectCommand: command gate
        Events(117),                  // CityDomesticSetup phase 0 callback 48A6D0
        Empty,                        // CityDomesticProgress: direct setup
        Events(121, 122),             // Proposal callback 48A2C0
        Events(123),                  // ProposalExplain callback 48A0E0
        Empty,                        // ProposalOfficer: direct setup/accepted action
        Events(124),                  // ProposalExit callback 484EF0
        Empty,                        // ProposalProgress: direct setup
        Events(125),                  // ReportIncome callback 48C100
        Events(126),                  // Ending: 4B3A40
        Empty,                        // Exit: real cleanup 4C65F0
    ];
    private static readonly IReadOnlyList<int> CitySecondInstructions = Events(118);
    private static readonly IReadOnlyList<int> CityFinalInstructions = Events(119, 120);
    private static readonly IReadOnlyList<int> ClearPriorityUpdate = Events(12);
    private static readonly IReadOnlyList<int> ClearActionReport = Events(15);
    private static readonly IReadOnlyList<int> ClearIncomeReport = Events(16);
    private static readonly IReadOnlyList<int> ClearDomesticRestriction = Events(66);
    private static readonly IReadOnlyList<int> SetDomesticRestriction = Events(66);

    /// <summary>
    /// Reads the plan for the current local phase. Waiting phases have no stories;
    /// callers must not restart phase-entry resets on each observation poll.
    /// </summary>
    public static TutorialParagraphPlan Read(int paragraphIndex, int phase)
    {
        if ((uint)paragraphIndex >= (uint)InitialStories.Length)
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        if (phase < 0 || phase > (paragraphIndex == 12 ? 7 : 1))
            throw new ArgumentOutOfRangeException(nameof(phase));

        var stories = phase == 0 ? InitialStories[paragraphIndex] : Empty;
        if (paragraphIndex == 12)
            stories = phase switch
            {
                0 => InitialStories[12],
                3 => CitySecondInstructions,
                6 => CityFinalInstructions,
                _ => Empty,
            };

        int? requiredCommand = paragraphIndex switch { 2 => 3, 11 => 4, _ => null };
        int? requiredInformation = paragraphIndex == 14 ? 46 : null;
        bool resetProgress = (paragraphIndex == 7 && phase == 1) ||
            ((paragraphIndex is 13 or 18) && phase == 0);
        var plan = new TutorialParagraphPlan(paragraphIndex, phase, stories,
            requiredCommand, requiredInformation, resetProgress)
        {
            CallbackPhase = stories.Count != 0 ? phase + 1 : phase,
            UsesInstructionCallback = stories.Count != 0 &&
                paragraphIndex is 3 or 4 or 5 or 7 or 8 or 9 or 12 or 14 or 15 or 17 or 19,
            // Source 4E7CA0 and 4E68F0 admit command2 as the alternative path.
            EnabledCommandMask = phase == 0 ? paragraphIndex switch
            {
                2 => (1u << 3) | (1u << 2),
                11 => (1u << 4) | (1u << 2),
                _ => null,
            } : null,
            ResetLastCommandId = phase == 0 && requiredCommand.HasValue ? 13 : null,
            // 54FC40 first runs after the Proposal instruction callback returns.
            ResetLastInformationId = paragraphIndex == 14 && phase == 1 ? 51 : null,
            BeforeStoryClearEventFlags = paragraphIndex == 8 && phase == 0
                ? ClearActionReport : Empty,
            AfterStoryClearEventFlags = paragraphIndex == 5 && phase == 0
                ? ClearPriorityUpdate : Empty,
            CompletionClearEventFlags = paragraphIndex switch
            {
                6 => ClearDomesticRestriction,
                18 => ClearIncomeReport,
                _ => Empty,
            },
            CompletionSetEventFlags = paragraphIndex is 2 or 5
                ? SetDomesticRestriction : Empty,
            CorrectionGameMessageId = paragraphIndex switch
            {
                4 => 0x9CD7,
                5 => 0x9C5E,
                _ => null,
            },
        };
        return plan;
    }

    private static IReadOnlyList<int> Events(params int[] ids) => Array.AsReadOnly(ids);
}
