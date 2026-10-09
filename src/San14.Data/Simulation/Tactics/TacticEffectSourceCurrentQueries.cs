using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit current native results whose complete world queries are
/// outside the fixed definition table. Every omitted callback stays unknown.</summary>
public sealed record TacticEffectSourceCurrentQueries
{
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        S14TacticEffectInstruction, TacticInstructionTargetsSource?>? InstructionTargets { get; init; }
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        TacticUseEligibility?>? PrimaryTargetEligibility { get; init; }
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        S14TacticEffectInstruction, ulong?>? FinishArmyAbnormalClear { get; init; }
    public Func<GameState, UnitState, int?>? HealingSelectionCapacity { get; init; }
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        S14TacticEffectInstruction, TacticEffectQueryResult<TacticHealingSourceInput>?>? ArmyHealing { get; init; }
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        S14TacticEffectInstruction, TacticEffectQueryResult<TacticAbnormalStrengthSourceInput>?>? ArmyAbnormalStrength { get; init; }
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        S14TacticEffectInstruction, int, int,
        TacticEffectQueryResult<TacticAbnormalSourceInput>?>? ArmyAbnormalStatus { get; init; }
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        S14TacticEffectInstruction, int, TacticAbnormalSourceAdjustment, ulong?>? FinishArmyAbnormalAdjustment { get; init; }
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        S14TacticEffectInstruction, TacticEffectQueryResult<TacticCapturedOwnerSource>?>? ArmyAbnormalCapturedOwner { get; init; }
    public Func<GameState, UnitState, UnitState, TacticSourceRow,
        S14TacticEffectInstruction, int, TacticAbnormalSourceResult, ulong?>? FinishArmyAbnormalStatus { get; init; }
}
