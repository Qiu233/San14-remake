namespace San14.Core;

/// <summary>The selected manual activation suspended inside a real destruction
/// decision. Its current and linked actor plans reuse the automatic executor's
/// captured operands without inventing a mixed combat pair.</summary>
public sealed record ManualTacticExecutionContinuation(ManualTacticRequest Request, int SelectedHexId,
    int? PrimaryUnitId, int? PrimaryBaseLocationId, AutomaticTacticExecutionContinuation Execution,
    int SampleDay, int SampleSalt);

public sealed partial record GameState
{
    public ManualTacticExecutionContinuation? ManualTacticExecutionContinuation { get; init; }
}

internal static class ManualTacticExecutionContinuationValidation
{
    internal static void Validate(GameState state)
    {
        if (state.ManualTacticExecutionContinuation is not { } manual) return;
        if (state.NativePeriodContinuation?.Cursor is { StageIndex: not 6 } ||
            state.NativeNormalTurnContinuation is not null || state.NormalCombatImmediateContinuation is not null ||
            state.DuelAftermathContinuation is not null ||
            state.ArmyDefeatContinuation is not { SettlementIndex: -1 } && state.BaseCaptureContinuation is not { SettlementIndex: -1 } ||
            state.ManualTacticIntervention?.Pending != manual.Request ||
            manual.Request is not { Actor.Value: > 0, TacticId: >= 1 and <= 200,
                Target: { Kind: ManualTacticTargetKind.Hex } target } ||
            manual.SelectedHexId is < 0 or >= 48400 || target.Id != manual.SelectedHexId ||
            manual.PrimaryUnitId is <= 0 || manual.PrimaryBaseLocationId is < 1 or > 61 ||
            manual.PrimaryUnitId is not null && manual.PrimaryBaseLocationId is not null || manual.Execution is null)
            throw new InvalidDataException("The manual tactic execution has no matching stage or defeat decision.");
        NormalCombatImmediateContinuationValidation.ValidateAutomaticExecution(manual.Execution);
    }
}
