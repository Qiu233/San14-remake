using San14.Core;

namespace San14.Data;

/// <summary>Source TC initial scalar used by the actual weighted-strength
/// consumer. Installation belongs to fresh construction, never a query or
/// legacy archive migration.</summary>
internal static class OriginalAiStrategyParameters
{
    internal static GameState InstallFresh(GameState state)
    {
        var simulation = state.NativeArmySimulation
            ?? throw new InvalidDataException("original_ai.parameters.current_owner");
        if (simulation.Parameters.Language != "TC") return state;
        // Offline initial-table data: signed DAT141A49014 = 1000.
        // Keep it in the existing current parameter owner so later genuine
        // parameter changes are observed instead of hidden by a constant query.
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        values.Add(OriginalAiCrisisSource.StrengthDivisorKey, 1000);
        // Independent signed source globals used by the two Force evaluation
        // consumers; these are not Army common-strength coefficients.
        values.Add("original-ai.force-defense-personality-percent", 150);
        values.Add("original-ai.force-relationship-personality-percent", 150);
        // Independent TC initial coefficients for the group resource readers.
        // They remain saved current parameters; old archives are not repaired.
        values.Add("original-ai.troops-ready-personality-percent", 150);
        values.Add("original-ai.troops-gold-preservation-percent", 150);
        values.Add("original-ai.troops-supplies-preservation-percent", 150);
        //Independent TC information-node globals, extracted from the installed
        //offline data table. They remain current saved values, never query defaults.
        values.Add("original-ai.base-currency-preservation-percent", 150);
        values.Add("original-ai.base-ready-personality-percent", 150);
        values.Add("original-ai.district-budget-ratio-scale", 100);
        values.Add("original-ai.district-budget-low", 25);
        values.Add("original-ai.district-budget-middle", 50);
        values.Add("original-ai.district-budget-high", 75);
        values.Add("original-ai.credit-near-threshold", 1000);
        //TC034E60 retreat admission uses signed DAT141A49EC0 (offline
        //initial data40). Legacy/SC queries never manufacture this knowledge.
        values.Add("original-ai.departure-retreat-morale-minimum", 40);
        //Actual TC initial parameters for the three special Strategy services.
        //Old/SC owners are never filled by queries or archive migration.
        foreach (var (name, value) in OriginalAiSpecialForceParameters.FreshTc)
            values.Add(OriginalAiSpecialForceParameters.Prefix + name, value);
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
