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

    public SelfRecommendationRequest? PendingSelfRecommendation =>
        Candidate.NativeNormalTurnContinuation?.PendingSelfRecommendation;

    public CoalitionInvitationRequest? PendingCoalitionInvitation =>
        Candidate.NativeNormalTurnContinuation?.PendingCoalitionInvitation;

    public bool RequiresAutomaticNormalTurnContinuation =>
        Candidate.NativeNormalTurnContinuation is { PendingSelfRecommendation: null, PendingCoalitionInvitation: null };

    public static bool HasContinuation(GameState state) =>
        state.NativePeriodContinuation?.Cursor is not null || state.NativeNormalTurnContinuation is not null;

    public NormalPeriodOperation Copy() => new(GameStateCopy.Copy(Candidate));

    public void Validate(GameState committed)
    {
        if (Candidate.ScenarioKey != committed.ScenarioKey || Candidate.RulesetId != committed.RulesetId ||
            Candidate.MapSha256 != committed.MapSha256 || committed.Phase != TurnPhase.Strategy ||
            Candidate.Phase != committed.Phase || Candidate.Date != committed.Date ||
            Candidate.PeriodNumber != committed.PeriodNumber ||
            Candidate.NativePeriodContinuation?.Preparation is not { } preparation ||
            preparation.PersonnelReports is null || preparation.Reports is null ||
            preparation.TacticReports is null || preparation.PossibleArrivalOfficers is null)
            throw new InvalidDataException("The normal period continuation is incomplete.");
        if (Candidate.NativeNormalTurnContinuation is { } turn)
        {
            if (Candidate.NativePeriodContinuation.Cursor is not null || turn.NextAction is not (16 or 25 or 26) ||
                turn.Grouping?.Roots is not { Length: 1503 } ||
                turn.RemainingOfficerIds.IsDefault || turn.RemainingOfficerIds.Any(id => id is < 0 or > 6000) ||
                turn.NextAction == 16 && turn.PendingCoalitionInvitation is not null ||
                turn.NextAction != 16 && (!turn.RemainingOfficerIds.IsEmpty || turn.PendingSelfRecommendation is not null) ||
                turn.NextAction == 25 && turn.PendingCoalitionInvitation is null ||
                turn.NextAction == 26 && turn.PendingCoalitionInvitation is not null ||
                turn.PendingSelfRecommendation is { } offer &&
                (offer.OfficerId is < 0 or > 6000 || offer.ForceId is < 0 or > 51 ||
                 offer.DistrictId is < 0 or > 51 || offer.RulerOfficerId is < 0 or > 6000 || offer.TextAtSource is null))
                throw new InvalidDataException("The normal Turn decision continuation is incomplete.");
            if (turn.PendingCoalitionInvitation is { } invitation &&
                (invitation.TargetForceId is < 0 or > 51 || invitation.PlayerForceId is < 0 or > 51 ||
                 invitation.PlayerRulerId is < 0 or > 6000 || invitation.SpeakerOfficerId is < 0 or > 6000 ||
                 invitation.OrderedMemberForceIds.IsDefault || invitation.OrderedMemberForceIds.Any(id => id is < 0 or > 51) ||
                 invitation.QuestionTextAtSource is null || invitation.CameraHexId is < 0 or > 48400))
                throw new InvalidDataException("The normal Turn coalition invitation is incomplete.");
            return;
        }
        if (Candidate.NativePeriodContinuation.Cursor is not { } cursor ||
            cursor.StageIndex != 0 || cursor.CycleIndex is < 0 or > 2 || cursor.Date.Hour != 8 ||
            cursor.PersonnelAfterSlot < 0 || cursor.AwaitingDiplomacyTaskId < 0 ||
            Candidate.NativeMapSeason is not { } season ||
            cursor.Date != (season.ThirdDate with { Hour = (sbyte)(season.ThirdDate.Hour + 2 * cursor.CycleIndex) }) ||
            cursor.Grouping.Roots is not { Length: 1503 })
            throw new InvalidDataException("The normal period continuation is incomplete.");
    }

}

/// <summary>Once-only Core preparation retained across Army stage0 and normal Turn waits.</summary>
public sealed record NormalPeriodPreparation(PersonnelReportEntry[] PersonnelReports,
    PeriodReportEntry[] Reports, TacticReportEntry[] TacticReports, int[] PossibleArrivalOfficers);

/// <summary>Only stage0 can suspend Army Progress. Its completed
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
