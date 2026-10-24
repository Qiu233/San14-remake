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

    /// <summary>Retained TC initial-image inputs, not a restore fallback or a
    /// claim about other locale profiles. The base30 and step10 are distinct
    /// inputs established by the authorized eight-byte TC data read retained
    /// in parameter-values20.json, not derived from appointment pending10.</summary>
    internal static GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } native || native.Parameters.Language != "TC") return state;
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 })
            throw new InvalidDataException("Governance parameters require an unused TC campaign.");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var (key, value) in new[] { (NewAppointmentPending, 10), (AdvisorIntelligenceMinimum, 70),
            (PolicyCostLimitBase, 30), (PendingReductionPerTurn, 10) })
            if (native.Parameters.Float32Values.ContainsKey(key) || !values.TryAdd(key, value))
                throw new InvalidDataException("Governance parameters already have an owner.");
        return state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };
    }
}
