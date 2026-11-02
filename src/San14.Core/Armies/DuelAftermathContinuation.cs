using System.Collections.Immutable;

namespace San14.Core;

public enum DuelAftermathResumePoint { AfterDefeat, AfterMoraleRouting }

/// <summary>The unapplied tail of the registered duel's campaign aftermath.
/// The duel service retains its already prepared pair, result and source date.</summary>
public sealed record DuelAftermathContinuation(long DuelSequence, DuelAftermathResumePoint ResumePoint,
    ImmutableArray<CampaignDuelInjuryNotice> Injuries);

public sealed partial record GameState
{
    public DuelAftermathContinuation? DuelAftermathContinuation { get; init; }
}

internal static class DuelAftermathContinuationValidation
{
    internal static void Validate(GameState state, CampaignOperationWorldContext? operationContext = null)
    {
        if (state.DuelAftermathContinuation is not { } value)
        {
            if (state.NativePeriodContinuation?.Cursor is { StageIndex: 5 } ||
                operationContext?.Moment is { SchedulerStageIndex: 5 })
                throw new InvalidDataException("The duel aftermath has no retained continuation.");
            return;
        }
        var cursor = state.NativePeriodContinuation?.Cursor;
        var external = state.NativePeriodContinuation is null && operationContext?.Moment is { SchedulerStageIndex: 5 }
            ? operationContext.Moment : null;
        var date = cursor is { StageIndex: 5 } ? cursor.Date : external?.PreWriterThirdDate;
        if (date is null ||
            state.ArmyDefeatContinuation is not { SettlementIndex: -1 } ||
            state.NormalCombatImmediateContinuation is not null ||
            state.CampaignDuelService is not { Active: true, Busy: true, Registered: { } registered } ||
            value.DuelSequence <= 0 || registered.Sequence != value.DuelSequence || registered.Date != date ||
            external is not null && registered.CalendarCycleOrdinal != external.CalendarCycleOrdinal ||
            registered.Result is not { Actor: { Outcome: CampaignDuelOutcome.Won or CampaignDuelOutcome.Lost } } result ||
            (value.ResumePoint == DuelAftermathResumePoint.AfterDefeat
                ? result.Aftermath is not (CampaignDuelAftermath.Death or CampaignDuelAftermath.Capture)
                : result.Aftermath != CampaignDuelAftermath.None) ||
            !Enum.IsDefined(value.ResumePoint) || value.Injuries.IsDefault)
            throw new InvalidDataException("The duel aftermath has no matching registered duel or stage cursor.");
    }
}
