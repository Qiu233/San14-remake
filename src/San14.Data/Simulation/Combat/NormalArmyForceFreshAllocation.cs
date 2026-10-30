using San14.Core;

namespace San14.Data;

/// <summary>Actual CForce ctor/reset then static-prefix load, for a newly
/// allocated OWN remake Force bank. The static serializer skips runtime15F /
///160 /162. Request/runtime-copy writers must update this saved current owner;
/// this is not a claim about the original first-player's later initialization.</summary>
public static class NormalArmyForceFreshAllocation
{
    public const string Revision = "san14:ce-force-ctor-static-result-fields-20261006";
    public const string VersionKey = "ce-force-ctor-static-result-fields:version";

    public static GameState InstallFresh(GameState state, LoadedScenario staticScenario,
        NormalArmyForceResultSourceParameters source)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(staticScenario);
        if (!source.IsInitialAllocationSource() || staticScenario.Metadata.FormatMarker != 0x66 ||
            staticScenario.Snapshot.Locale != source.Language ||
            !staticScenario.Snapshot.Forces.Select(force => force.Slot).Order().SequenceEqual(Enumerable.Range(0, 52)) ||
            staticScenario.Snapshot.Forces.Any(force => force.RemainingBytesHex.Length != 654) ||
            state.ContentHashes.GetValueOrDefault(staticScenario.ScenarioSource.ResourceUri) != staticScenario.ScenarioSource.Sha256 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Units.Count != 0 || state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Language != source.Language ||
            simulation.Parameters.RuleVersion != source.Source.RuleVersion ||
            simulation.Parameters.Int32Values.ContainsKey(VersionKey) ||
            simulation.Parameters.Float32Values.ContainsKey(VersionKey) ||
            state.PlaceholderForceAdministrationFlags is not null ||
            state.UnassignedForcePolicyEffects is not null ||
            !simulation.Forces.Keys.Order().SequenceEqual(state.Forces.Keys.Order()) ||
            state.Forces.Values.Any(force => force.CombatObjective is not null))
            throw new InvalidDataException("Force result constructor fields require the fresh unmodified owned static allocation.");
        //SC2213C0→2272E0 writes word15E=000B and qword160=0. The
        //SC237F20 static serializer writes only byte15E, then238282/238289
        //jumps to23862D without consuming15F/160/162. TC is paired in source.
        var forces = state.Forces.ToDictionary(pair => pair.Key, pair => pair.Value with {
            CombatObjective = ForceCombatObjective.Inactive });
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        //227070 resets word193 to0 for all52 source receivers. Static237CC0
        //skips byte193 in8C!=0 mode; table-zero keeps its own saved byte.
        //227070 also clears both ten-byte doctrine activation/suppression
        //sets. They are runtime-only in237CC0, so the static load preserves
        //this known empty receiver; later restoration never invokes this hook.
        return state with { Forces = forces, PlaceholderForceAdministrationFlags = 0,
            UnassignedForcePolicyEffects = ForcePolicyEffects.Empty,
            NativeArmySimulation = simulation with {
            Parameters = simulation.Parameters with { Int32Values = integers } } };
    }

    private static bool IsInitialAllocationSource(this NormalArmyForceResultSourceParameters source) =>
        source.CurrentRevision is null && source.Language is ("SC" or "TC");

    public static BattleUnitSourceProfile ForLanguage(string language) =>
        new(language, SimulationRuleIdentity.ForLocale(language));
}
