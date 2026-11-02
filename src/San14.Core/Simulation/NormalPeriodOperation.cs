namespace San14.Core;

/// <summary>The normal ten-day transaction awaiting an actual player decision.
/// The committed session world remains unchanged until this transaction finishes.</summary>
public sealed record NormalPeriodOperation(GameState Candidate)
{
    /// <summary>An issued alliance break or a completed negotiation can continue
    /// after its pending presentation. Only an actual undecided negotiation waits
    /// for a player response; its arrival prefix must not be replayed.</summary>
    public bool RequiresAutomaticDiplomacyContinuation =>
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 0, AwaitingCaravanTaskId: null, AwaitingStrategicTaskId: null, AwaitingIntelligenceTaskId: null } cursor &&
        Candidate.PersonnelCommands?.Diplomacies?.GetValueOrDefault(cursor.AwaitingDiplomacyTaskId)
            is not { AwaitingNegotiation: true, NativeKind: not 6 };

    public bool RequiresAutomaticStrategicContinuation =>
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 0, AwaitingStrategicTaskId: not null } &&
        Candidate.AutomaticEmperorCapture?.PendingDecision is null &&
        !AutomaticEmperorCaptureLifecycle.IsPresentationPending(Candidate);

    public int? PendingCaravanReportTaskId =>
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 0, AwaitingCaravanTaskId: int task } &&
        Candidate.PersonnelCommands?.Caravans?.GetValueOrDefault(task)?.PendingReport is not null ? task : null;

    public bool RequiresAutomaticCaravanContinuation =>
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 0, AwaitingCaravanTaskId: int task } &&
        Candidate.PersonnelCommands?.Caravans?.GetValueOrDefault(task) is { PendingReport: null };

    public int? PendingIntelligenceNarrationTaskId =>
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 0, AwaitingIntelligenceTaskId: int id } &&
        Candidate.PersonnelCommands?.Intelligences?.GetValueOrDefault(id)?.NarrationProgress?.Pending is
            { Phase: PersonnelIntelligenceNarrationPhase.AwaitingNarration } ? id : null;

    public bool RequiresAutomaticIntelligenceContinuation =>
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 0, AwaitingIntelligenceTaskId: int id } &&
        Candidate.PersonnelCommands?.Intelligences?.GetValueOrDefault(id)?.NarrationProgress?.Pending is
            { Phase: PersonnelIntelligenceNarrationPhase.ReadyToResume };

    [System.Text.Json.Serialization.JsonIgnore]
    public ArmyReturnCaptiveBatchRequest? PendingArmyReturnCaptiveBatch => Candidate.ArmyReturnCaptiveContinuation?.PendingSelection;
    [System.Text.Json.Serialization.JsonIgnore]
    public ArmyReturnCaptiveExecutionConfirmation? PendingArmyReturnCaptiveExecution => Candidate.ArmyReturnCaptiveContinuation?.ExecutionConfirmation;
    [System.Text.Json.Serialization.JsonIgnore]
    public ArmyReturnCaptivePresentation? PendingArmyReturnCaptivePresentation => Candidate.ArmyReturnCaptiveContinuation?.PendingPresentation;
    public bool RequiresAutomaticArmyReturnContinuation => Candidate.ArmyReturnCaptiveContinuation is { } returning &&
        (returning.Phase == ArmyReturnCaptivePhase.ReadyToFinishReturn ||
         returning.Phase == ArmyReturnCaptivePhase.AwaitingPersonDeath && Candidate.PersonDeathContinuation is null);

    public CapturedRulerTreatmentRequest? PendingCapturedRulerTreatment =>
        Candidate.ArmyDefeatContinuation?.PendingCapturedRulerTreatment ?? Candidate.BaseCaptureContinuation?.PendingCapturedRulerTreatment;

    public EmperorCaptureDecisionRequest? PendingEmperorCaptureDecision =>
        AutomaticEmperorCaptureLifecycle.PendingDecision(Candidate);

    public RulerSuccessionRequest? PendingRulerSuccession =>
        Candidate.PersonDeathContinuation?.PendingSuccession;

    public bool RequiresAutomaticArmyDefeatContinuation =>
        !HiredArmyCaptureProgress.IsAwaitingAnnouncement(Candidate) &&
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 5 or 13 or 25 } &&
        Candidate.ArmyDefeatContinuation is not null &&
        PendingCapturedRulerTreatment is null && PendingRulerSuccession is null &&
        Candidate.AutomaticEmperorCapture?.PendingDecision is null && !AutomaticEmperorCaptureLifecycle.IsPresentationPending(Candidate);

    public bool RequiresAutomaticBaseCaptureContinuation =>
        !BaseCapturePersonPrefixProgress.IsAwaitingNotice(Candidate) &&
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 6 or 13 or 25 } &&
        (Candidate.BaseCaptureContinuation is not null || Candidate.CityCaptureEmperorTail is not null || Candidate.BaseWinnerEmperorTail is not null) &&
        PendingCapturedRulerTreatment is null && PendingRulerSuccession is null &&
        Candidate.AutomaticEmperorCapture?.PendingDecision is null && !AutomaticEmperorCaptureLifecycle.IsPresentationPending(Candidate);

    public bool RequiresAutomaticOrdinaryTacticContinuation =>
        RequiresAutomaticTacticPresentationContinuation;

    public bool RequiresAutomaticTacticPresentationContinuation =>
        !HiredArmyCaptureProgress.IsAwaitingAnnouncement(Candidate) &&
        !BaseCapturePersonPrefixProgress.IsAwaitingNotice(Candidate) &&
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 6 } &&
        Candidate.ManualTacticExecutionContinuation is null &&
        Candidate.ManualTacticIntervention is { Directions.Length: 0 } manager &&
        (!manager.EffectBusy || manager.Effects.Length == 0 && manager.OrdinaryPlaybacks.IsEmpty) &&
        PendingCapturedRulerTreatment is null && PendingRulerSuccession is null &&
        Candidate.AutomaticEmperorCapture?.PendingDecision is null && !AutomaticEmperorCaptureLifecycle.IsPresentationPending(Candidate);

    public bool RequiresAutomaticManualTacticContinuation =>
        !HiredArmyCaptureProgress.IsAwaitingAnnouncement(Candidate) &&
        !BaseCapturePersonPrefixProgress.IsAwaitingNotice(Candidate) &&
        Candidate.NativePeriodContinuation?.Cursor is { StageIndex: 6 } &&
        Candidate.ManualTacticExecutionContinuation is not null &&
        PendingCapturedRulerTreatment is null && PendingRulerSuccession is null &&
        Candidate.AutomaticEmperorCapture?.PendingDecision is null && !AutomaticEmperorCaptureLifecycle.IsPresentationPending(Candidate);

    public SelfRecommendationRequest? PendingSelfRecommendation =>
        Candidate.NativeNormalTurnContinuation?.PendingSelfRecommendation;

    public CoalitionInvitationRequest? PendingCoalitionInvitation =>
        Candidate.NativeNormalTurnContinuation?.PendingCoalitionInvitation;

    public CampaignStoryRequest? PendingStory =>
        Candidate.NativeNormalTurnContinuation?.StoryPlayback?.Story is { } story && story.NextStep < story.Steps.Length
            ? story : null;

    public NormalTurnStoryResultSummary? PendingStoryResult =>
        Candidate.NativeNormalTurnContinuation?.StoryPlayback is
            { Phase: NormalTurnStoryResultPhase.Result, ResultSummary: { } result } ? result : null;

    public NormalTurnStoryPlayback? PendingStoryAudioEntry =>
        Candidate.NativeNormalTurnContinuation?.StoryPlayback is
            { Phase: NormalTurnStoryResultPhase.Entry } entry ? entry : null;

    public NormalTurnStoryPlayback? PendingStoryAudioExit =>
        Candidate.NativeNormalTurnContinuation?.StoryPlayback is
            { Phase: NormalTurnStoryResultPhase.Exiting } exiting ? exiting : null;

    public CampaignStoryForceRankContinuation? PendingStoryForceRank =>
        Candidate.NativeNormalTurnContinuation?.StoryPlayback?.ExecutionCursor?.ForceRankContinuation is
            { IsAwaitingInput: true } rank ? rank : null;

    public bool RequiresAutomaticNormalTurnContinuation =>
        Candidate.NativeNormalTurnContinuation is { PendingSelfRecommendation: null, PendingCoalitionInvitation: null } &&
        PendingStory is null && PendingRulerSuccession is null && PendingStoryResult is null &&
        PendingStoryAudioEntry is null && PendingStoryAudioExit is null && PendingStoryForceRank is null &&
        !CampaignStoryStateControlProgress.IsAwaitingPresentation(Candidate) &&
        Candidate.AutomaticEmperorCapture?.PendingDecision is null && !AutomaticEmperorCaptureLifecycle.IsPresentationPending(Candidate);

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
        RulerDecisionContinuationValidation.Validate(Candidate);
        ArmyReturnCaptiveContinuationValidation.Validate(Candidate);
        NativeArmyMovementContinuationValidation.Validate(Candidate);
        if (Candidate.NativeNormalTurnContinuation is { } turn)
        {
            if (Candidate.NativePeriodContinuation.Cursor is not null || turn.NextAction is not (3 or 7 or 8 or 12 or 16 or 25 or 26 or 48 or 49) ||
                turn.Grouping?.Roots is not { Length: 1503 } ||
                (turn.NextAction is 3 or 7 or 8) != (turn.RemainingDeathPersonPhysicalSlots is not null) ||
                turn.RemainingDeathPersonPhysicalSlots is { } deaths && (deaths.IsDefault || deaths.Any(id => id is < 0 or > 6000)) ||
                turn.RemainingOfficerIds.IsDefault || turn.RemainingOfficerIds.Any(id => id is < 0 or > 6000) ||
                turn.NextAction != 16 && (!turn.RemainingOfficerIds.IsEmpty || turn.PendingSelfRecommendation is not null) ||
                turn.NextAction == 25 && turn.PendingCoalitionInvitation is null ||
                turn.NextAction != 25 && turn.PendingCoalitionInvitation is not null ||
                (turn.NextAction == 48) != (turn.StoryPlayback is not null) ||
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
            turn.StoryPlayback?.Validate();
            return;
        }
        if (Candidate.NativePeriodContinuation.Cursor is not { } cursor ||
            cursor.StageIndex is not (0 or 5 or 6 or 8 or 13 or 25) || cursor.CycleIndex is < 0 or > 2 ||
            cursor.StageIndex == 0 && (cursor.Date.Hour != (cursor.AwaitingCaravanTaskId is null ? 8 : 22) || cursor.PersonnelAfterSlot < 0 ||
                (cursor.AwaitingIntelligenceTaskId is int intelligenceTask
                    ? !IsIntelligenceCursorValid(cursor, intelligenceTask)
                    : cursor.AwaitingStrategicTaskId is int strategicTask
                        ? strategicTask < 0 || cursor.AwaitingCaravanTaskId is not null || cursor.AwaitingDiplomacyTaskId != -1 ||
                            Candidate.PersonnelCommands?.Strategies?.ContainsKey(strategicTask) != true
                        : cursor.AwaitingCaravanTaskId is null ? cursor.AwaitingDiplomacyTaskId < 0 :
                            cursor.AwaitingCaravanTaskId is < 0 || cursor.AwaitingDiplomacyTaskId != -1 ||
                            Candidate.PersonnelCommands?.Caravans?.GetValueOrDefault(cursor.AwaitingCaravanTaskId.Value) is not { NativeKind: 18 })) ||
            cursor.StageIndex != 0 && (cursor.AwaitingCaravanTaskId is not null || cursor.AwaitingStrategicTaskId is not null ||
                cursor.AwaitingIntelligenceTaskId is not null) ||
            cursor.StageIndex == 25 && Candidate.ArmyDefeatContinuation is null && Candidate.BaseCaptureContinuation is null && Candidate.CityCaptureEmperorTail is null && Candidate.BaseWinnerEmperorTail is null &&
                Candidate.ArmyReturnCaptiveContinuation is not { Caller: ArmyReturnCaptiveCaller.BaseCapture, CaptureTail: not null } ||
            cursor.StageIndex == 5 && (Candidate.DuelAftermathContinuation is null || Candidate.ArmyDefeatContinuation is null) ||
            cursor.StageIndex == 6 && Candidate.ManualTacticIntervention is null ||
            cursor.StageIndex == 13 && (Candidate.NormalCombatImmediateContinuation is null ||
                Candidate.ArmyDefeatContinuation is null && Candidate.BaseCaptureContinuation is null && Candidate.CityCaptureEmperorTail is null && Candidate.BaseWinnerEmperorTail is null &&
                Candidate.ArmyReturnCaptiveContinuation is not { Caller: ArmyReturnCaptiveCaller.BaseCapture, CaptureTail: not null }) ||
            Candidate.NativeMapSeason is not { } season ||
            cursor.Date != (season.ThirdDate with { Hour = (sbyte)(season.ThirdDate.Hour + 2 * cursor.CycleIndex) }) ||
            cursor.Grouping.Roots is not { Length: 1503 })
            throw new InvalidDataException("The normal period continuation is incomplete.");
    }

    private bool IsIntelligenceCursorValid(NativePeriodCursor cursor, int id) =>
        id >= 0 && cursor.AwaitingDiplomacyTaskId == -1 && cursor.AwaitingCaravanTaskId is null &&
        cursor.AwaitingStrategicTaskId is null &&
        Candidate.PersonnelCommands?.Intelligences?.GetValueOrDefault(id) is
            { Started: true, IsRetiring: false, IsAssociated: true, NarrationProgress.Pending: { } wait } task &&
        task.DispatchSlot == cursor.PersonnelAfterSlot && wait.SourceDate == cursor.Date &&
        wait.NarrationBatchSequence > 0 && wait.MainTextAtSource is not null &&
        wait.Phase is PersonnelIntelligenceNarrationPhase.AwaitingNarration or PersonnelIntelligenceNarrationPhase.ReadyToResume;

}

/// <summary>Once-only Core preparation retained across Army stage0 and normal Turn waits.</summary>
public sealed record NormalPeriodPreparation(PersonnelReportEntry[] PersonnelReports,
    PeriodReportEntry[] Reports, TacticReportEntry[] TacticReports, int[] PossibleArrivalOfficers);

/// <summary>Stage0 personnel, stage6 ordinary effects and stage5/13/25 defeat decisions suspend Army Progress. Completed
/// personnel slots are skipped on resume, while retirements remain deferred
/// until the actual manager traversal finishes.</summary>
public sealed record NativePeriodCursor(int CycleIndex, byte StageIndex, NativeMapSeasonDate Date,
    int PersonnelAfterSlot, int AwaitingDiplomacyTaskId, CampaignGroupingCheckpoint Grouping,
    NativeNormalArmyCombatPhaseDiagnostic[] Diagnostics, int DueBeforeBlock)
{
    public int? AwaitingCaravanTaskId { get; init; }
    public int? AwaitingIntelligenceTaskId { get; init; }
    public int? AwaitingStrategicTaskId { get; init; }
    public NativeArmyMovementContinuation? MovementContinuation { get; init; }
}

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
