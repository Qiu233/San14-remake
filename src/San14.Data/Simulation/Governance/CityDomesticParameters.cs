using San14.Core;

namespace San14.Data;

/// <summary>One saved current bank. Only an actual fresh allocation seeds it.</summary>
public static class CityDomesticParameters
{
    private const string Prefix = "ce-city-domestic-current:";
    public static string Key(string name) => Prefix + name;
    public static int? Read(GameState state, string name)
    {
        var p = state.NativeArmySimulation?.Parameters;
        if (p is null || p.Language is not ("TC" or "SC") || p.RuleVersion != SimulationRuleIdentity.ForLocale(p.Language) ||
            p.Float32Values.ContainsKey(Key("version")) || p.Int32Values.GetValueOrDefault(Key("version")) != 1 ||
            p.Float32Values.ContainsKey(Key(name))) return null;
        return p.Int32Values.TryGetValue(Key(name), out int value) ? value : null;
    }
    public static GameState InstallFresh(GameState state, LoadedCityDomesticParameters loaded, LoadedScenario scenario)
    {
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("city-domestic.saved_owner");
        var p = simulation.Parameters;
        CityDomesticParameterCatalog.RequireScenario(scenario, loaded.Catalog.Locale);
        if (p.Language != loaded.Catalog.Locale || p.RuleVersion != SimulationRuleIdentity.ForLocale(p.Language) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            p.Int32Values.Keys.Concat(p.Float32Values.Keys).Any(k => k.StartsWith(Prefix, StringComparison.Ordinal)) ||
            state.ContentHashes.ContainsKey(loaded.Source.ResourceUri)) throw new InvalidDataException("city-domestic.unused_fresh_owner");
        if (state.ContentHashes.GetValueOrDefault(scenario.ScenarioSource.ResourceUri) != scenario.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(scenario.FixedDataSource.ResourceUri) != scenario.FixedDataSource.Sha256)
            throw new InvalidDataException("city-domestic.fresh_source_binding");
        var values = new Dictionary<string, int>(p.Int32Values) { [Key("version")] = 1 };
        foreach (var (name, value) in loaded.Catalog.Values) values.Add(Key(name), value);
        var hashes = new Dictionary<string, string>(state.ContentHashes) { [loaded.Source.ResourceUri] = loaded.Source.Sha256 };
        return state with { ContentHashes = hashes, NativeArmySimulation = simulation with { Parameters = p with { Int32Values = values } } };
    }
    /// <summary>Returns no new ledger for legacy saves. Does not seed any key.</summary>
    public static SourceProvenance? ValidateRestore(GameState state, LoadedScenario scenario, GameResourceRepository repository)
    {
        var p = state.NativeArmySimulation?.Parameters;
        bool ledger = state.ContentHashes.Keys.Any(k => k.StartsWith("san14://file/rules/ce/city-domestic-defaults/", StringComparison.Ordinal));
        bool feature = p is not null && p.Int32Values.Keys.Concat(p.Float32Values.Keys).Any(k => k.StartsWith(Prefix, StringComparison.Ordinal));
        if (!ledger && !feature) return null;
        if (Read(state, "version") != 1) throw new InvalidDataException("city-domestic.restore_version");
        CityDomesticParameterCatalog.RequireScenario(scenario, p!.Language);
        string uri = CityDomesticParameterCatalog.UriFor(p.Language);
        if (!state.ContentHashes.ContainsKey(uri)) throw new InvalidDataException("city-domestic.restore_ledger_missing");
        var loaded = repository.LoadCityDomesticParameters(p.Language);
        if (state.ContentHashes[uri] != loaded.Source.Sha256) throw new InvalidDataException("city-domestic.restore_content: " + uri);
        return loaded.Source;
    }
}
