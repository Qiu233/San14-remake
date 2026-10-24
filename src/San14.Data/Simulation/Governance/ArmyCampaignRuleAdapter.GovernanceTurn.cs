using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary-period integration for the governance Turn action.
    /// Missing transition or derived-policy knowledge preserves the entire
    /// input numerical state and records an explicit unresolved period entry.</summary>
    internal GameState CompletePlayableGovernanceTurn(GameState state)
    {
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            var pool = current.ReadOwnedCurrentListPool()
                ?? throw new InvalidDataException("governance.turn_pending.current_pool");
            return GovernancePendingSource.ApplyCurrent(state, current, pool);
        }
        catch (InvalidDataException missing)
        {
            // Zero here identifies a global diagnostic if the saved player
            // itself is unknown; it is not a synthetic player or Force result.
            int reportForce = OwnedCurrentPlayerForce.ReadRawForce3A(state) is byte player ? player : 0;
            return state with { PeriodReports = [.. state.PeriodReports,
                new("governance_turn_unresolved", reportForce)
                {
                    RuleId = "governance.turn_pending",
                    MissingRuleKeys = [missing.Message]
                }] };
        }
    }
}
