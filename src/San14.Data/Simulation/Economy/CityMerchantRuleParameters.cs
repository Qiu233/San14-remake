using San14.Core;

namespace San14.Data;

/// <summary>Immutable merchant rule data qualified independently for both CE
/// editions. Current saved integers take precedence; this reader does not
/// initialize missing historical City C8/C9 or change the random stream.</summary>
internal sealed class CityMerchantRuleParameters
{
    private const string Prefix = "simulation.city-merchant.";
    private readonly NativeArmyParameterState parameters;

    internal CityMerchantRuleParameters(GameState state, string? expectedLanguage = null)
    {
        parameters = RequireProfile(state, expectedLanguage);
    }

    internal static NativeArmyParameterState RequireProfile(GameState state, string? expectedLanguage = null)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw CityMerchantSource.Missing("parameter_owner");
        if (parameters.Language is not ("SC" or "TC") ||
            expectedLanguage is not null && parameters.Language != expectedLanguage ||
            parameters.RuleVersion != SimulationRuleIdentity.ForLocale(parameters.Language))
            throw CityMerchantSource.Missing("rule_profile");
        return parameters;
    }

    internal int Read(string name)
    {
        string key = Prefix + name;
        if (parameters.Float32Values.ContainsKey(key)) throw CityMerchantSource.Missing("integer_parameter_" + name);
        if (parameters.Int32Values.TryGetValue(key, out int saved)) return saved;
        // These values come from each edition's own file-backed operands.
        // A default here is immutable rule data, not a mutable world initializer.
        return ReadImmutable(name);
    }

    //The same table is qualified independently by both editions' actual
    //file-backed operands; source identities stay in the offline evidence.
    private static int ReadImmutable(string name) => name switch
    {
        "appearance-base" => 10,
        "appearance-size-scale" => 20,
        "price-winter-base" => 40,
        "price-spring-base" => 30,
        "price-june-base" => 20,
        "price-summer-base" => 60,
        "price-autumn-base" => 50,
        "price-wide-random-bound" => 3,
        "price-narrow-random-bound" => 2,
        "price-random-step" => 10,
        "price-drought-percent" => 90,
        "price-locust-percent" => 120,
        _ => throw CityMerchantSource.Missing("parameter_" + name)
    };

}
