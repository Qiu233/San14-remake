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
    internal static void Validate(GameState state)
    {
        if (state.DuelAftermathContinuation is not { } value)
        {
            if (state.NativePeriodContinuation?.Cursor is { StageIndex: 5 })
                throw new InvalidDataException("The duel aftermath has no retained continuation.");
            return;
        }
        if (state.NativePeriodContinuation?.Cursor is not { StageIndex: 5 } cursor ||
            state.ArmyDefeatContinuation is not { SettlementIndex: -1 } ||
            state.NormalCombatImmediateContinuation is not null ||
            state.CampaignDuelService is not { Active: true, Busy: true, Registered: { } registered } ||
            value.DuelSequence <= 0 || registered.Sequence != value.DuelSequence || registered.Date != cursor.Date ||
            registered.Result is not { Actor: { Outcome: CampaignDuelOutcome.Won or CampaignDuelOutcome.Lost } } result ||
            (value.ResumePoint == DuelAftermathResumePoint.AfterDefeat
                ? result.Aftermath is not (CampaignDuelAftermath.Death or CampaignDuelAftermath.Capture)
                : result.Aftermath != CampaignDuelAftermath.None) ||
            !Enum.IsDefined(value.ResumePoint) || value.Injuries.IsDefault)
            throw new InvalidDataException("The duel aftermath has no matching registered duel or stage cursor.");
    }
}
