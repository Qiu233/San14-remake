using System.Collections.Immutable;

namespace San14.Core;

public sealed record BaseCaptureOperationContext(BaseCaptureRequest Request, int PreviousForceId,
    int WinnerForceId, int WinnerDistrictId, int CommanderOfficerId, int WinnerArmyPhysicalSlot,
    ArmyReceiverId WinnerReceiverId, int ReturnedTroops, int? SampleDay, int? SampleSalt);

public sealed record BaseCapturePeopleContinuation(ImmutableArray<int> HomeArmySlots,
    ImmutableArray<int> HomeOfficerIds, ImmutableArray<int> CapturedOfficerIds,
    ImmutableArray<int> SpecialCapturedOfficerIds, ImmutableArray<int> NoncapturedOfficerIds, int CapturedIndex);

/// <summary>The occupation prefix and preceding people have already settled
/// in the candidate. Only the captured person's decision and the remaining tail resume.</summary>
public sealed record BaseCaptureContinuation(int SettlementIndex, BaseCaptureOperationContext Operation,
    BaseCapturePeopleContinuation People)
{
    public CapturedRulerTreatmentRequest? PendingCapturedRulerTreatment { get; init; }
    public CapturedRulerDisposition? ChosenDisposition { get; init; }
}

public sealed partial record GameState
{
    public BaseCaptureContinuation? BaseCaptureContinuation { get; init; }
}

internal static class BaseCaptureContinuationValidation
{
    internal static void Validate(GameState state)
    {
        if (state.BaseCaptureContinuation is not { } value) return;
        bool immediate = state.NativePeriodContinuation?.Cursor is { StageIndex: 13 } && state.NormalCombatImmediateContinuation is not null ||
            state.ManualTacticExecutionContinuation is not null && state.NativePeriodContinuation?.Cursor is null or { StageIndex: 6 };
        if (state.ArmyDefeatContinuation is not null || value.Operation is not { } operation ||
            operation.Request is not { } request ||
            (immediate ? value.SettlementIndex != -1 || !MatchesImmediateTarget(state, request.Target) :
                state.NativePeriodContinuation?.Cursor is not { StageIndex: 25 } || value.SettlementIndex < 0 ||
                state.PendingCombatSettlements?.Requests is not { } queue || value.SettlementIndex >= queue.Count ||
                queue[value.SettlementIndex] != request) || request.Target is null || !Enum.IsDefined(request.Target.Kind) ||
            request.Target.Id <= 0 || request.WinnerUnitId is <= 0 ||
            operation.PreviousForceId is < 0 or > 51 || operation.WinnerForceId is < 0 or > 51 ||
            operation.WinnerDistrictId is < 0 or > 51 || operation.CommanderOfficerId is < 0 or > 6000 ||
            operation.WinnerArmyPhysicalSlot < 0 || operation.WinnerReceiverId.Value <= 0 ||
            (operation.SampleDay is null) != (operation.SampleSalt is null) || value.People is not { } people ||
            people.HomeArmySlots.IsDefault || people.HomeOfficerIds.IsDefault || people.CapturedOfficerIds.IsDefault ||
            people.SpecialCapturedOfficerIds.IsDefault || people.NoncapturedOfficerIds.IsDefault ||
            people.CapturedIndex < 0 || people.CapturedIndex >= people.CapturedOfficerIds.Length ||
            value.ChosenDisposition is { } chosen && !Enum.IsDefined(chosen))
            throw new InvalidDataException("The Base capture continuation has no matching occupation or current person.");
        if (value.PendingCapturedRulerTreatment is { } pending &&
            (pending.CapturingForceId != operation.WinnerForceId || pending.OfficerId is < 1 or > 6000 ||
             pending.CapturingRulerId is < 1 or > 6000 || pending.TextAtSource is null || value.ChosenDisposition is not null))
            throw new InvalidDataException("The Base capture has an invalid captive ruler decision.");
    }

    private static bool MatchesImmediateTarget(GameState state, SettlementBase target)
    {
        if (state.ManualTacticExecutionContinuation is { } manual) return manual.Execution.TargetBase == target;
        if (state.NormalCombatImmediateContinuation is not { } combat) return false;
        if (combat.AutomaticExecution is { } army) return army.TargetBase == target;
        if (combat.BaseAutomaticExecution is { } governor) return governor.TargetBase == target;
        if (combat.PairIndex < 0 || combat.PairIndex >= combat.OrderedPairs.Length) return false;
        var victim = combat.OrderedPairs[combat.PairIndex].Target;
        return victim.Kind switch
        {
            5 => target == new SettlementBase(SettlementBaseKind.City, victim.PhysicalSlot),
            6 => target == new SettlementBase(SettlementBaseKind.Gate, victim.PhysicalSlot + 51),
            _ => false
        };
    }
}
