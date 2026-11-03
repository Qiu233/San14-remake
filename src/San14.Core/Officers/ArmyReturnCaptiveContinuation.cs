using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>The original four batch dispositions, independently of a table's
/// visible sort. A reply selects an intention, never an execution receipt.</summary>
public enum ArmyReturnCaptiveDisposition { Recruit, Execute, Release, Keep }
public enum ArmyReturnCaptivePhase
{
    AwaitingIntroduction,
    AwaitingSelection,
    AwaitingExecutionConfirmation,
    ApplyingBatch,
    AwaitingBatchPresentation,
    AwaitingPersonDeath,
    ReadyToFinishReturn
}

/// <summary>The one real recruitment trial performed when this batch owner
/// opens. Subsequent selection, redraw and save/load do not resample it.</summary>
public sealed record ArmyReturnCaptiveTrial(int OfficerId, bool RecruitmentAccepted,
    int SampleAfter, int? Probability, bool ExecutionProtected)
{
    public bool FailedRecruitNoticeShown { get; init; }
    public bool ExecutionProtectionNoticeShown { get; init; }
    public string? AdviceTextAtSource { get; init; }
}

public enum ArmyReturnCaptiveCaller { Unspecified, OrdinaryEntry, MovementArrival, BaseCapture, CampaignWithdrawal }
public sealed record ArmyReturnCaptiveArrivalTail(byte Action, ushort GoalHexId, int DestinationLocationId,
    NativeArmyProgressStageMoment Moment)
{
    //Older saved arrivals already ran the march caller before the batch
    //closed. New arrivals retain that unfinished work with this real owner.
    public bool MarchCallerTailPending { get; init; }
}
public sealed record ArmyReturnCaptiveCaptureTail(BaseCaptureRequest Request, int WinningForceId,
    int WinningDistrictId, ImmutableArray<int> StatePhysicalSlots, ImmutableArray<int> RegionPhysicalSlots,
    int BeforeStateCount, int BeforeRegionCount)
{
    public bool CompletingEmperorCapture { get; init; }
    public BaseCaptureOperationContext? Operation { get; init; }
    public int SettlementIndex { get; init; } = -1;
}
public sealed record ArmyReturnCaptiveDeploymentCancellationTail(int CityId, int RetainedCommanderPhysicalSlot,
    NativeMapSeasonDate OperationDate, long OperationRevision, int OperationCampaignDay);
public sealed record ArmyReturnCaptiveCampaignTail(int CurrentExpeditionId, ImmutableArray<int> RemainingExpeditionIds,
    NativeMapSeasonDate OperationDate, long OperationRevision)
{
    [JsonRequired] public ImmutableArray<int> RemainingArmyPhysicalSlots { get; init; }
    [JsonRequired] public int RetainedCommanderPhysicalSlot { get; init; }
    [JsonRequired] public int OperationCampaignDay { get; init; }
}

public sealed record ArmyReturnCaptiveBatchRequest(ArmyReceiverId BatchId, int Round,
    int CapturingForceId, int ReturningCommanderId, SettlementBase Destination,
    ImmutableArray<int> RemainingOfficerIds,
    ImmutableArray<ArmyReturnCaptiveTrial> RecruitmentTrials);
public sealed record ArmyReturnCaptiveExecutionConfirmation(ArmyReceiverId BatchId, int Round,
    int CapturingForceId, ImmutableArray<int> SelectedOfficerIds, string TextAtSource);
public sealed record ArmyReturnCaptivePresentation(ArmyReceiverId BatchId, int Round,
    int CapturingForceId, long EffectBatchSequence);

public sealed record ConfirmArmyReturnCaptiveBatchOrder(int ForceId, ArmyReceiverId BatchId,
    int Round, ArmyReturnCaptiveDisposition Disposition,
    ImmutableArray<int> OfficerIds) : GameOrder(ForceId);
public sealed record ConfirmArmyReturnCaptiveExecutionOrder(int ForceId, ArmyReceiverId BatchId,
    int Round, bool Confirmed) : GameOrder(ForceId);
public sealed record CompleteArmyReturnCaptivePresentationOrder(int ForceId,
    ArmyReceiverId BatchId, int Round) : GameOrder(ForceId);

/// <summary>Held operands for the unexecuted return suffix. Home/resources,
/// item ownership, merit and report prefix are already in the candidate World.
/// No draft context, callback, temporary list or copy of World is serialized.</summary>
public sealed record ArmyReturnCaptiveContinuation(ArmyReceiverId BatchId,
    int ArmyUnitId, int ArmyPhysicalSlot, int CapturingForceId, int ReturningCommanderId,
    SettlementBase Destination, int SavedCommanderDistrictId, int SavedTargetDistrictId,
    bool ReturningTransportPerson, NativeMapSeasonDate SourceDate, long SourceRevision,
    int SampleDay, int SampleSalt, ImmutableArray<int> OriginalOfficerIds,
    ImmutableArray<int> RemainingOfficerIds, ImmutableArray<ArmyReturnCaptiveTrial> RecruitmentTrials,
    ArmyReturnCaptivePhase Phase, int Round)
{
    public ArmyReturnCaptiveDisposition? ActiveDisposition { get; init; }
    public ImmutableArray<int> ActiveBatchOfficerIds { get; init; } = [];
    public int ActiveBatchIndex { get; init; }
    public ArmyReturnCaptiveExecutionConfirmation? ExecutionConfirmation { get; init; }
    public ArmyReturnCaptivePresentation? PendingPresentation { get; init; }
    public int? AwaitingPersonDeathOfficerId { get; init; }
    public ImmutableArray<int> RequestedOfficerIds { get; init; } = [];
    public TalentCommandPresentationRequest? FirstResultPresentation { get; init; }
    public bool FirstResultQueued { get; init; }
    public ArmyReturnCaptiveCaller Caller { get; init; }
    public ArmyReturnCaptiveArrivalTail? ArrivalTail { get; init; }
    public ArmyReturnCaptiveCaptureTail? CaptureTail { get; init; }
    public ArmyReturnCaptiveCampaignTail? CampaignTail { get; init; }
    public string? ImmediateEntryReportRuleId { get; init; }
    public ArmyReturnCaptiveDeploymentCancellationTail? DeploymentCancellationTail { get; init; }

    [JsonIgnore] public ArmyReturnCaptiveBatchRequest? PendingSelection => Phase == ArmyReturnCaptivePhase.AwaitingSelection
        ? new(BatchId, Round, CapturingForceId, ReturningCommanderId, Destination,
            RemainingOfficerIds, RecruitmentTrials) : null;
}

public interface IArmyReturnCaptiveRules
{
    CommandResult ConfirmArmyReturnCaptiveBatch(GameState state,
        ConfirmArmyReturnCaptiveBatchOrder order, CommandContext context);
    CommandResult ConfirmArmyReturnCaptiveExecution(GameState state,
        ConfirmArmyReturnCaptiveExecutionOrder order, CommandContext context);
    CommandResult CompleteArmyReturnCaptivePresentation(GameState state,
        CompleteArmyReturnCaptivePresentationOrder order, CommandContext context);
}

public sealed partial record GameState
{
    public ArmyReturnCaptiveContinuation? ArmyReturnCaptiveContinuation { get; init; }
}

/// <summary>Only the actual pending batch can receive a reply. Source kernels
/// retain their own current person/holder admission and all treatment writes.</summary>
public sealed class ArmyReturnCaptiveOrders(IOrderRules next, IArmyReturnCaptiveRules? rules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is not (ConfirmArmyReturnCaptiveBatchOrder or ConfirmArmyReturnCaptiveExecutionOrder or
            CompleteArmyReturnCaptivePresentationOrder))
        {
            if (state.ArmyReturnCaptiveContinuation is not null &&
                !(order is ConfirmRulerSuccessionOrder && state.PersonDeathContinuation is not null))
                return CommandResult.Reject("The army return is awaiting captive treatment.", "army-return.captive_batch_busy");
            return next.Apply(state, force, order, context);
        }
        var pending = state.ArmyReturnCaptiveContinuation;
        if (context.DelegatedDistrictId is not null || pending is null ||
            pending.CapturingForceId != force.Id || order.ForceId != force.Id)
            return CommandResult.Reject("No matching captive batch is awaiting this reply.", "army-return.captive_batch_owner");
        if (rules is null) return CommandResult.RuleUnavailable("army-return.captive_rules", "Captive batch rules are unavailable.");
        return order switch
        {
            ConfirmArmyReturnCaptiveBatchOrder selected when selected.BatchId == pending.BatchId && selected.Round == pending.Round &&
                pending.Phase == ArmyReturnCaptivePhase.AwaitingSelection && Enum.IsDefined(selected.Disposition) &&
                !selected.OfficerIds.IsDefaultOrEmpty && selected.OfficerIds.Distinct().Count() == selected.OfficerIds.Length &&
                selected.OfficerIds.All(pending.RemainingOfficerIds.Contains)
                    => rules.ConfirmArmyReturnCaptiveBatch(state, selected, context),
            ConfirmArmyReturnCaptiveExecutionOrder execution when execution.BatchId == pending.BatchId && execution.Round == pending.Round &&
                pending.Phase == ArmyReturnCaptivePhase.AwaitingExecutionConfirmation && pending.ExecutionConfirmation is not null
                    => rules.ConfirmArmyReturnCaptiveExecution(state, execution, context),
            CompleteArmyReturnCaptivePresentationOrder completed when completed.BatchId == pending.BatchId && completed.Round == pending.Round &&
                pending.PendingPresentation is not null && pending.Phase is (ArmyReturnCaptivePhase.AwaitingIntroduction or
                    ArmyReturnCaptivePhase.AwaitingBatchPresentation) &&
                state.CampaignEffectRequests?.PendingBatches.Any(batch => batch.Sequence == pending.PendingPresentation.EffectBatchSequence) == false
                    => rules.CompleteArmyReturnCaptivePresentation(state, completed, context),
            _ => CommandResult.Reject("This captive batch reply is stale or invalid.", "army-return.captive_batch_reply")
        };
    }
}

internal static partial class ArmyReturnCaptiveContinuationValidation
{
    //Decode/continuation admission only. This is never a frame or redraw scan.
    internal static void Validate(GameState state)
    {
        if (state.ArmyReturnCaptiveContinuation is not { } value) return;
        ValidateHeldSuffix(state, value);
        if (value.BatchId.Value <= 0 || value.ArmyUnitId <= 0 || value.ArmyPhysicalSlot is < 1 or > 500 ||
            value.CapturingForceId is < 1 or > 45 || value.ReturningCommanderId is < 1 or > 6000 ||
            value.Destination is not { Id: > 0 } destination || !Enum.IsDefined(destination.Kind) ||
            value.SavedCommanderDistrictId is < 0 or > 51 || value.SavedTargetDistrictId is < 0 or > 51 ||
            value.SourceDate is null || value.SourceRevision < 0 || value.SampleDay < 0 ||
            value.OriginalOfficerIds.IsDefaultOrEmpty || value.RemainingOfficerIds.IsDefault ||
            value.RecruitmentTrials.IsDefault || value.ActiveBatchOfficerIds.IsDefault || value.Round < 0 ||
            !Enum.IsDefined(value.Phase) || value.ActiveDisposition is { } chosen && !Enum.IsDefined(chosen) ||
            value.ActiveBatchIndex < 0 || value.ActiveBatchIndex > value.ActiveBatchOfficerIds.Length ||
            value.OriginalOfficerIds.Any(id => id is < 1 or > 6000 || !state.Officers.ContainsKey(id)) ||
            value.OriginalOfficerIds.Distinct().Count() != value.OriginalOfficerIds.Length ||
            value.RemainingOfficerIds.Distinct().Count() != value.RemainingOfficerIds.Length ||
            value.RemainingOfficerIds.Any(id => !value.OriginalOfficerIds.Contains(id)) ||
            value.ActiveBatchOfficerIds.Distinct().Count() != value.ActiveBatchOfficerIds.Length ||
            value.ActiveBatchOfficerIds.Any(id => !value.OriginalOfficerIds.Contains(id)) ||
            value.RecruitmentTrials.Length != value.OriginalOfficerIds.Length ||
            !value.RecruitmentTrials.Select(row => row.OfficerId).SequenceEqual(value.OriginalOfficerIds) ||
            value.RecruitmentTrials.Any(row => row.SampleAfter is < -1 or > 99 || row.Probability is < 0 or > 100))
            throw Invalid();
        if (!state.Units.TryGetValue(value.ArmyUnitId, out var army) || army.ForceId != value.CapturingForceId ||
            army.NativeArmyOrigin?.NativeSlotIndex != value.ArmyPhysicalSlot ||
            state.ArmyLifecycle?.Receivers.Find(value.BatchId) is not { ActiveUnitId: int active } receiver ||
            active != value.ArmyUnitId || receiver.ReceiverId != value.BatchId)
            throw Invalid();
        if (value.DeploymentCancellationTail is { } cancelled &&
            (value.Caller != ArmyReturnCaptiveCaller.OrdinaryEntry || value.ImmediateEntryReportRuleId is not null ||
                cancelled.CityId != value.Destination.Id || value.Destination.Kind != SettlementBaseKind.City ||
                cancelled.RetainedCommanderPhysicalSlot is < 1 or > 6000 || cancelled.OperationDate is null ||
                cancelled.OperationRevision < 0 || cancelled.OperationCampaignDay < 0)) throw Invalid();
        if (value.CampaignTail is { } tail &&
            (value.Caller != ArmyReturnCaptiveCaller.CampaignWithdrawal || tail.RemainingExpeditionIds.IsDefault ||
                tail.RemainingArmyPhysicalSlots.IsDefault || tail.RemainingArmyPhysicalSlots.Any(slot => slot is < 1 or > 500) ||
                tail.RetainedCommanderPhysicalSlot is < 1 or > 6000 || tail.OperationDate is null ||
                tail.OperationRevision < 0 || tail.OperationCampaignDay < 0 ||
                state.CampaignExpeditions?.Expeditions.ContainsKey(tail.CurrentExpeditionId) != true ||
                tail.RemainingExpeditionIds.Any(id => state.CampaignExpeditions?.Expeditions.ContainsKey(id) != true)))
            throw Invalid();
        if (value.Caller == ArmyReturnCaptiveCaller.CampaignWithdrawal && value.CampaignTail is null ||
            value.ImmediateEntryReportRuleId is not null && (value.Caller != ArmyReturnCaptiveCaller.OrdinaryEntry ||
                string.IsNullOrWhiteSpace(value.ImmediateEntryReportRuleId))) throw Invalid();
        foreach (int id in value.RemainingOfficerIds)
            if (id != value.AwaitingPersonDeathOfficerId &&
                !OfficerServiceRules.IsCaptive(state.Officers[id].ServiceStatus)) throw Invalid();
        if ((value.Phase == ArmyReturnCaptivePhase.AwaitingExecutionConfirmation) != (value.ExecutionConfirmation is not null) ||
            (value.Phase is ArmyReturnCaptivePhase.AwaitingIntroduction or ArmyReturnCaptivePhase.AwaitingBatchPresentation) !=
                (value.PendingPresentation is not null) ||
            (value.Phase == ArmyReturnCaptivePhase.AwaitingPersonDeath) != (value.AwaitingPersonDeathOfficerId is not null) ||
            value.Phase == ArmyReturnCaptivePhase.ReadyToFinishReturn && !value.RemainingOfficerIds.IsEmpty ||
            value.Phase == ArmyReturnCaptivePhase.AwaitingSelection && value.RemainingOfficerIds.IsEmpty)
            throw Invalid();
        if (value.ExecutionConfirmation is { } confirm &&
            (confirm.BatchId != value.BatchId || confirm.Round != value.Round || confirm.CapturingForceId != value.CapturingForceId ||
                confirm.TextAtSource is null || value.ActiveDisposition != ArmyReturnCaptiveDisposition.Execute ||
                confirm.SelectedOfficerIds.Length < 2 || !confirm.SelectedOfficerIds.SequenceEqual(value.ActiveBatchOfficerIds))) throw Invalid();
        if (value.PendingPresentation is { } presentation &&
            (presentation.BatchId != value.BatchId || presentation.Round != value.Round ||
                presentation.CapturingForceId != value.CapturingForceId || presentation.EffectBatchSequence <= 0 ||
                presentation.EffectBatchSequence > state.CampaignEffectRequests?.LastBatchSequence)) throw Invalid();
    }
    private static InvalidDataException Invalid() => new("The saved army-return captive continuation is invalid.");
}
