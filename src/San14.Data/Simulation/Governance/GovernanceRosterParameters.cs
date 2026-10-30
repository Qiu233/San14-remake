using San14.Core;

namespace San14.Data;

/// <summary>Independent mutable inputs. A restored bank missing a key remains
/// unknown; these names do not introduce a second personnel/configuration owner.</summary>
internal static class GovernanceRosterParameters
{
    internal const string NewAppointmentPending = "simulation.governance.new-appointment-pending";
    internal const string PendingReductionPerTurn = "simulation.governance.pending-reduction-per-turn";
    internal const string PolicyCostLimitBase = "simulation.governance.policy-cost-limit-base";
    internal const string AdvisorIntelligenceMinimum = "simulation.governance.advisor-intelligence-minimum";

    /// <summary>Independently established TC and SC initial-image inputs.
    /// Each edition's four actual scalar consumers and file-backed values are
    /// qualified separately. This installs a fresh bank only; restored missing
    /// keys remain unknown and loaded overrides are never overwritten.</summary>
    internal static GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } native || native.Parameters.Language is not ("TC" or "SC")) return state;
        if (native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(native.Parameters.Language) ||
            state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 })
            throw new InvalidDataException("Governance parameters require a matching unused campaign.");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var (key, value) in new[] { (NewAppointmentPending, 10), (AdvisorIntelligenceMinimum, 70),
            (PolicyCostLimitBase, 30), (PendingReductionPerTurn, 10) })
            if (native.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value))
                throw new InvalidDataException("Governance parameters already have an owner.");
        return state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };
    }
}
