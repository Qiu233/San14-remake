using San14.Core;

namespace San14.Data;

/// <summary>The independently sourced TC/SC format66 initial catalog for a
/// newly allocated campaign. Restore never repairs missing old values.</summary>
internal static class CaptiveReleaseInitialParameters
{
    // Offline TC initial data is signed32 50. This is an initial allocation
    // rule, not a claim about an original running process or an older save.
    private const int FreshTcResentmentRetentionPercent = 50;
    // Independent SC source2E77B0 reads its own signed32 scalar1A4B9E8.
    private const int FreshScResentmentRetentionPercent = 50;

    internal static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        string key = ArmyCampaignRuleAdapter.CaptiveResentmentRetentionPercentKey;
        string language = source.Snapshot.Locale;
        if (language is not ("TC" or "SC") || source.Metadata.FormatMarker != 0x66) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Language != language || simulation.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(language) ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            simulation.Parameters.Float32Values.ContainsKey(key))
            throw new InvalidDataException("person-captive-release.initial_parameter.fresh_owner");
        if (simulation.Parameters.Int32Values.ContainsKey(key)) return state;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values)
            { [key] = language == "TC" ? FreshTcResentmentRetentionPercent : FreshScResentmentRetentionPercent };
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
