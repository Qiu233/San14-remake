using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>The real suspended stage5,8,13 or25 of one legacy atomic block.
/// Candidate owns the already executed prefix. Movement is present only when
/// the original stage8 actor selection still has an unapplied suffix.</summary>
public sealed record CampaignArmyProgressOperation(string Revision,
    CampaignProgressRequest Request, int BlockIndex, GameState Candidate,
    CampaignOperationWorldContext Context, NativeNormalArmyCombatCalendarState Calendar,
    NativeArmyMovementContinuation? Movement, long LastStorySequence,
    SavedArmyCombatCapture[] Captures, CampaignGroupingCheckpoint Grouping,
    CampaignServiceChanges ServiceChanges)
{
    [JsonIgnore] public bool CanContinue => !IsAwaitingReply(Candidate) &&
        (Context.Moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Movement
            ? Candidate.ArmyReturnCaptiveContinuation is
                { Caller: ArmyReturnCaptiveCaller.MovementArrival, Phase: ArmyReturnCaptivePhase.ReadyToFinishReturn }
            : Context.Moment.SchedulerStageIndex is (byte)NativeArmyProgressStage.DuelOpportunity or (byte)NativeArmyProgressStage.Combat or (byte)NativeArmyProgressStage.Defeat);

    private static bool IsAwaitingReply(GameState state) =>
        state.PersonDeathContinuation?.PendingSuccession is not null ||
        state.ArmyDefeatContinuation?.PendingCapturedRulerTreatment is not null ||
        state.BaseCaptureContinuation?.PendingCapturedRulerTreatment is not null ||
        state.AutomaticEmperorCapture?.PendingDecision is not null ||
        AutomaticEmperorCaptureLifecycle.IsPresentationPending(state) ||
        HiredArmyCaptureProgress.IsAwaitingAnnouncement(state) || BaseCapturePersonPrefixProgress.IsAwaitingNotice(state) ||
        state.ArmyReturnCaptiveContinuation is { Phase: not ArmyReturnCaptivePhase.ReadyToFinishReturn };

    public CampaignArmyProgressOperation Copy() => this with
    {
        Candidate = GameStateCopy.Copy(Candidate),
        Captures = [], //Historical diagnostic payload, never capture reward state.
        Grouping = Grouping with { Roots = Grouping.Roots.ToArray() },
        ServiceChanges = ServiceChanges.Copy()
    };

    public void Validate(GameState committed)
    {
        if (string.IsNullOrWhiteSpace(Revision) || Request is null || Candidate is null || Context is null ||
            Calendar is null || Captures is null || Grouping is null || ServiceChanges is null ||
            Request.BlockCount is < 1 or > 40 || BlockIndex < 0 || BlockIndex >= Request.BlockCount ||
            Context.Moment is not { SchedulerStageIndex: (byte)NativeArmyProgressStage.DuelOpportunity or
                (byte)NativeArmyProgressStage.Movement or
                (byte)NativeArmyProgressStage.Combat or (byte)NativeArmyProgressStage.Defeat } moment || LastStorySequence < 0 ||
            committed.NativeMapSeason is null || Request.StartCalendarCycleOrdinal != committed.NativeMapSeason.AdvancedCalendarSteps ||
            Context.BlockStartSeason.AdvancedCalendarSteps != Request.StartCalendarCycleOrdinal + 3L * BlockIndex ||
            Calendar.NextCalendarCycleOrdinal != Context.BlockStartSeason.AdvancedCalendarSteps ||
            Grouping.Roots is not { Length: 1503 } || Grouping.Roots.Any(value => value > 500) ||
            (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Movement) != (Movement is not null) ||
            Candidate.NativePeriodContinuation is not null)
            throw new InvalidDataException("Legacy Army continuation is incomplete.");
        Request.ValidateNormalPeriod(committed);
        GameEngine.ValidateWorldIdentity(committed, Candidate);
        Context.Validate(Candidate);
        ServiceChanges.Validate();
        RulerDecisionContinuationValidation.Validate(Candidate, Context);
        ArmyReturnCaptiveContinuationValidation.Validate(Candidate);
        AutomaticEmperorCaptureValidation.Validate(Candidate);
        if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.DuelOpportunity)
        {
            if (Candidate.DuelAftermathContinuation is null || Candidate.NormalCombatImmediateContinuation is not null)
                throw new InvalidDataException("Legacy duel stage has no retained registered aftermath.");
        }
        else if (Movement is { } movement)
            NativeArmyMovementContinuationValidation.Validate(Candidate, movement, moment);
        else if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Combat)
        {
            if (Candidate.NormalCombatImmediateContinuation is null || moment.PreWriterThirdDate.Hour != 10)
                throw new InvalidDataException("Legacy combat continuation has no retained mixed pair.");
        }
        else if (Candidate.NormalCombatImmediateContinuation is not null ||
            Candidate.ArmyDefeatContinuation is null && Candidate.BaseCaptureContinuation is null &&
            !AutomaticEmperorCaptureLifecycle.HasCaptureTail(Candidate) &&
            Candidate.ArmyReturnCaptiveContinuation is not { Caller: ArmyReturnCaptiveCaller.BaseCapture, CaptureTail: not null })
            throw new InvalidDataException("Legacy settlement continuation has no retained actual request.");
        if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Defeat)
        {
            var held = Candidate.ArmyDefeatContinuation;
            var capture = Candidate.BaseCaptureContinuation;
            var city = Candidate.CityCaptureEmperorTail;
            var source = Candidate.BaseWinnerEmperorTail;
            var returned = Candidate.ArmyReturnCaptiveContinuation?.CaptureTail;
            int index = held?.SettlementIndex ?? capture?.SettlementIndex ?? returned?.SettlementIndex ??
                city?.SettlementIndex ?? source?.SettlementIndex ?? -1;
            CombatSettlementRequest? request = held?.Defeat ?? (CombatSettlementRequest?)capture?.Operation.Request ??
                returned?.Request ?? city?.Operation.Request ?? source?.Request;
            if (index < 0 || request is null || Candidate.PendingCombatSettlements?.Requests is not { } queue ||
                index >= queue.Count || queue[index] != request)
                throw new InvalidDataException("Legacy settlement continuation has no matching queue cursor.");
        }
        //Old session fields remain readable, but contain no input or result
        //used by this driver and are removed on the next Copy/save.
    }
}

public interface ICampaignArmyProgressRules : ICampaignProgressRules
{
    CampaignProgressTransition RetryArmy(CampaignArmyProgressOperation pending);
}
