using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit current native results whose complete world queries are
/// outside the fixed definition table. Every omitted callback stays unknown.</summary>
public sealed record OriginalTacticEffectSourceCurrentQueries
{
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, OriginalTacticInstructionTargetsSource?>? InstructionTargets { get; init; }
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        TacticUseEligibility?>? PrimaryTargetEligibility { get; init; }
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, ulong?>? FinishArmyAbnormalClear { get; init; }
    public Func<GameState, UnitState, int?>? HealingSelectionCapacity { get; init; }
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, OriginalTacticEffectQueryResult<OriginalTacticHealingSourceInput>?>? ArmyHealing { get; init; }
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, OriginalTacticEffectQueryResult<OriginalTacticAbnormalStrengthSourceInput>?>? ArmyAbnormalStrength { get; init; }
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, int, int,
        OriginalTacticEffectQueryResult<OriginalTacticAbnormalSourceInput>?>? ArmyAbnormalStatus { get; init; }
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, int, OriginalTacticAbnormalSourceAdjustment, ulong?>? FinishArmyAbnormalAdjustment { get; init; }
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, OriginalTacticEffectQueryResult<OriginalTacticCapturedOwnerSource>?>? ArmyAbnormalCapturedOwner { get; init; }
    public Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, int, OriginalTacticAbnormalSourceResult, ulong?>? FinishArmyAbnormalStatus { get; init; }
}
