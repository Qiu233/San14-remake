namespace San14.Core;

/// <summary>The normal ten-day transaction awaiting an actual player decision.
/// The committed session world remains unchanged until this transaction finishes.</summary>
public sealed record NormalPeriodOperation(GameState Candidate)
{
    /// <summary>An issued alliance break or a completed negotiation can continue
    /// after its pending presentation. Only an actual undecided negotiation waits
    /// for a player response; its arrival prefix must not be replayed.</summary>
    public bool RequiresAutomaticDiplomacyContinuation =>
        Candidate.NativePeriodContinuation?.Cursor is { } cursor &&
        Candidate.PersonnelCommands?.Diplomacies?.GetValueOrDefault(cursor.AwaitingDiplomacyTaskId)
            is not { AwaitingNegotiation: true, NativeKind: not 6 };

    public NormalPeriodOperation Copy() => new(GameStateCopy.Copy(Candidate));

    public void Validate(GameState committed)
    {
        if (Candidate.ScenarioKey != committed.ScenarioKey || Candidate.RulesetId != committed.RulesetId ||
            Candidate.MapSha256 != committed.MapSha256 ||
            committed.Phase != TurnPhase.Strategy || Candidate.Phase != committed.Phase ||
            Candidate.Date != committed.Date || Candidate.PeriodNumber != committed.PeriodNumber ||
            Candidate.NativePeriodContinuation is not { Cursor: { } cursor } ||
            cursor.StageIndex != 0 || cursor.CycleIndex is < 0 or > 2 || cursor.Date.Hour != 8 ||
            cursor.PersonnelAfterSlot < 0 || cursor.AwaitingDiplomacyTaskId < 0 ||
            Candidate.NativeMapSeason is not { } season ||
            cursor.Date != (season.ThirdDate with { Hour = (sbyte)(season.ThirdDate.Hour + 2 * cursor.CycleIndex) }) ||
            cursor.Grouping.Roots is not { Length: 1503 })
            throw new InvalidDataException("The normal period continuation is incomplete.");
    }

}

/// <summary>Once-only Core preparation retained across native stage0 waits.</summary>
public sealed record NormalPeriodPreparation(PersonnelReportEntry[] PersonnelReports,
    PeriodReportEntry[] Reports, TacticReportEntry[] TacticReports, int[] PossibleArrivalOfficers);

/// <summary>Only stage0 can currently suspend the normal period. Its completed
/// personnel slots are skipped on resume, while retirements remain deferred
/// until the actual manager traversal finishes.</summary>
public sealed record NativePeriodCursor(int CycleIndex, byte StageIndex, NativeMapSeasonDate Date,
    int PersonnelAfterSlot, int AwaitingDiplomacyTaskId, CampaignGroupingCheckpoint Grouping,
    NativeNormalArmyCombatPhaseDiagnostic[] Diagnostics, int DueBeforeBlock);

public sealed record NormalPeriodContinuation(NormalPeriodPreparation Preparation, NativePeriodCursor? Cursor)
{
    public NormalPeriodContinuation Copy() => this with
    {
        Preparation = Preparation with
        {
            PersonnelReports = Preparation.PersonnelReports.ToArray(),
            Reports = Preparation.Reports.ToArray(),
            TacticReports = Preparation.TacticReports.ToArray(),
            PossibleArrivalOfficers = Preparation.PossibleArrivalOfficers.ToArray()
        },
        Cursor = Cursor is null ? null : Cursor with
        {
            Diagnostics = Cursor.Diagnostics.ToArray(),
            Grouping = Cursor.Grouping with { Roots = Cursor.Grouping.Roots.ToArray() }
        }
    };
}

public sealed partial record GameState
{
    public NormalPeriodContinuation? NativePeriodContinuation { get; init; }
}
