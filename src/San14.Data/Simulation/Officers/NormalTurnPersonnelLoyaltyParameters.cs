using San14.Core;

namespace San14.Data;

/// <summary>Concrete rule data for the existing monthly departure/loyalty
/// consumers. Saved integers are read first by their common parameter reader.</summary>
internal static class NormalTurnPersonnelLoyaltyParameters
{
    private const string Prefix = "simulation.normal-turn.personnel-loyalty.";

    internal static int? ReadImmutableDefault(string language, string name, int? knownTc)
    {
        if (language == "TC") return knownTc;
        if (language != "SC") return null;
        // Each SC signed integer comes from its actual corresponding consumer,
        // independently of the TC call-site value. Mutable fresh-owner fields
        // below deliberately have no query-time fallback.
        return name switch
        {
            "defection-start-period" => 18,
            "defection-loyalty-limit" => 85,
            "defection-affinity-divisor" => 5,
            "defection-probability-limit" => 50,
            "loyalty-start-period" => 36,
            "loyalty-month-divisor" => 3,
            "stable-affinity-limit" => 20,
            "loyalty-protection-month-divisor" => 2,
            "loss-affinity-divisor" => 18,
            "loss-preference-divisor" => 10,
            "loss-disposition-divisor" => 10,
            "captive-loss-addition" => 10,
            "land-advantage-protection" => 2,
            "captive-extra" => 2,
            "captive-disliked-extra" => 4,
            _ => null
        };
    }

    internal static GameState InstallFresh(GameState state, string language)
    {
        if (language is not ("TC" or "SC")) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } native || native.Parameters.Language != language ||
            native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(language))
            throw new InvalidDataException("normal-turn.personnel-loyalty.fresh_parameter_owner");

        // The accepted complete static-owner/noescape and actual CRT object
        // boundaries independently qualify these initial TC and SC integers.
        // This is a fresh producer only, never a claim that later values are
        // immutable or that old missing saved keys can be reconstructed.
        (int DispositionMinimum, int PreferenceMaximum) initial = language switch
        {
            "TC" => (0, 0),
            "SC" => (0, 0),
            _ => throw new InvalidOperationException("Unreachable loyalty initializer locale.")
        };
        Dictionary<string, int>? values = null;
        Install("stable-disposition-minimum", initial.DispositionMinimum);
        Install("stable-preference-maximum", initial.PreferenceMaximum);
        return values is null ? state : state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };

        void Install(string name, int value)
        {
            string key = Prefix + name;
            if (native.Parameters.Float32Values.ContainsKey(key))
                throw new InvalidDataException("normal-turn.personnel-loyalty.integer_parameter_" + name);
            if (native.Parameters.Int32Values.ContainsKey(key)) return;
            (values ??= new Dictionary<string, int>(native.Parameters.Int32Values, StringComparer.Ordinal)).Add(key, value);
        }
    }
}
