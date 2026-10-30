using San14.Core;

namespace San14.Data;

/// <summary>Locale rule data for the existing disaster lifecycle and damage
/// consumers. Their saved integer parameter readers take precedence.</summary>
internal static class NormalTurnDisasterParameters
{
    internal static int? ReadGenerationDefault(string language, string name, int? knownTc)
    {
        if (language == "TC") return knownTc;
        if (language != "SC") return null;
        // These independent SC file-backed integers are read by recovery,
        // spread, new-season selection and harvest, not borrowed from TC.
        return name switch
        {
            "recovery-security-divisor" or "second-recovery-security-divisor" => 3,
            "spread-population-divisor" => 30000,
            "spread-maximum" => 10,
            "second-spread-random-bound" => 15,
            "occurrence-wounded-divisor" => 20000,
            "city-choice-limit" or "second-city-choice-limit" => 4,
            "new-area-limit" => 28,
            "second-new-area-limit" => 20,
            "spread-city-limit" or "second-spread-city-limit" => 10,
            "second-winter-rate" => 12,
            "second-spring-rate" => 17,
            "second-summer-rate" => 1,
            "second-autumn-rate" => 4,
            "harvest-probability" => 40,
            "harvest-preference-probability" => 50,
            "harvest-low-count" => 2,
            "harvest-middle-count" => 5,
            "harvest-high-count" => 7,
            _ => null
        };
    }

    internal static int? ReadConsequenceDefault(string language, string name, int? knownTc)
    {
        if (language == "TC") return knownTc;
        if (language != "SC") return null;
        // Independent SC plague, locust and illness consumer data.
        return name switch
        {
            "loss-random-bound" => 6,
            "plague-base-cap" => 3000,
            "advanced-plague-base-cap" => 10000,
            "plague-unit-cap" => 500,
            "advanced-plague-unit-cap" => 2500,
            "advanced-plague-population-cap" => 5000,
            "plague-security-random-bound" => 4,
            "locust-security-random-bound" => 4,
            "plague-illness-rate" => 6,
            "locust-base-food-cap" => 10000,
            "advanced-locust-base-food-cap" => 40000,
            _ => null
        };
    }

    internal static GameState InstallFresh(GameState state, string language)
    {
        if (language is not ("TC" or "SC")) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } native || native.Parameters.Language != language ||
            native.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(language))
            throw new InvalidDataException("normal-turn.disasters.fresh_parameter_owner");

        // Both actual edition owners independently have these fresh Int32
        // values under the accepted complete POD-owner/CRT-boundary evidence.
        // This producer is not a query fallback or a later immutability claim.
        (int SpreadMinimum, int SecondSpreadOffset) initial = language switch
        {
            "TC" => (0, 0),
            "SC" => (0, 0),
            _ => throw new InvalidOperationException("Unreachable disaster initializer locale.")
        };
        Dictionary<string, int>? values = null;
        Install("spread-minimum", initial.SpreadMinimum);
        Install("second-spread-offset", initial.SecondSpreadOffset);
        return values is null ? state : state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };

        void Install(string name, int value)
        {
            string key = "simulation.normal-turn.disaster." + name;
            if (native.Parameters.Float32Values.ContainsKey(key))
                throw new InvalidDataException("normal-turn.disasters.integer_parameter_" + name);
            if (native.Parameters.Int32Values.ContainsKey(key)) return;
            (values ??= new Dictionary<string, int>(native.Parameters.Int32Values, StringComparer.Ordinal)).Add(key, value);
        }
    }
}
