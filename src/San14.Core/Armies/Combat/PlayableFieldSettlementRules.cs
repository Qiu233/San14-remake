namespace San14.Core;

public sealed record PlayableFieldSettlementResult(PlayableSiegeSettlementOutcome Outcome,
    GameState State, PlayableFieldCombatResult Damage, string? RuleId = null)
{
    public IReadOnlyList<PeriodReportEntry> Reports { get; init; } = [];
}

/// <summary>Owns encounter side effects after arithmetic. A committed source
/// death removes its Army here; movement must not retire that Army again.</summary>
public interface IPlayableFieldSettlementRules
{
    PlayableFieldSettlementResult Apply(GameState state, UnitState attacker,
        UnitState defender, PlayableFieldCombatResult damage, int period);
}
