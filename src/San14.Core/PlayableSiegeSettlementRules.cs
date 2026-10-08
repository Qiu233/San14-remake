namespace San14.Core;

public enum PlayableSiegeSettlementOutcome { NotApplicable, Committed, Unresolved }

/// <summary>Current Base and Person owners written after encounter arithmetic,
/// before the shared movement, wall, attacker and capture settlement.</summary>
public sealed record PlayableSiegeSettlementResult(PlayableSiegeSettlementOutcome Outcome,
    GameState State, PlayableSiegeResult Damage, string? RuleId = null)
{
    public IReadOnlyList<PeriodReportEntry> Reports { get; init; } = [];
    /// <summary>The source operation already transferred the Base and ran
    /// its Army/person callbacks. Core must retain that resulting garrison
    /// and skip its provisional takeover/arrival/disposition a second time.</summary>
    public bool BaseCaptureHandled { get; init; }
    public string? UnresolvedOwner { get; init; }
}

/// <summary>Separates damage calculation from Base wounds, morale and rewards.
/// A reached unresolved operation must stop the encounter without publishing it.</summary>
public interface IPlayableSiegeSettlementRules
{
    PlayableSiegeSettlementResult Apply(GameState state, UnitState attacker,
        int locationId, PlayableSiegeResult damage, int period);
}
