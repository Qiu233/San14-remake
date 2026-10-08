using San14.Core;

namespace San14.Data;

/// <summary>Actual fixed section0 State2E bytes, installed once into the
/// existing saved field bank. Queries never reimport missing legacy values.</summary>
public sealed class OriginalBaseCaptureRewardInputs
{
    public const string VersionKey = "ce-base-capture-reward-current:version";
    public static string StateRegionKey(int physical) => $"ce-base-capture-reward-current:state:{physical}:2E";
    private readonly LoadedScenario _scenario;
    private readonly LoadedGrowthModifierDefinitions _definitions;
    private OriginalBaseCaptureRewardInputs(LoadedScenario scenario, LoadedGrowthModifierDefinitions definitions)
        => (_scenario, _definitions) = (scenario, definitions);
    public static OriginalBaseCaptureRewardInputs Load(OriginalGameRepository repository, LoadedScenario scenario)
    {
        var definitions = repository.LoadGrowthModifierDefinitions(scenario.FixedDataSource.SourcePath);
        if (definitions.Source.Sha256 != scenario.FixedDataSource.Sha256 || definitions.Catalog.States.Count != 23 ||
            definitions.Catalog.States.Where((row, slot) => row.Slot != slot || row.RawBytes.Length != 32).Any())
            throw new InvalidDataException("Base capture State2E source differs from the complete CE section0.");
        return new(scenario, definitions);
    }
    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) != _scenario.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(_definitions.Source.ResourceUri) != _definitions.Source.Sha256 ||
            simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Base capture reward fields need an unused fresh source campaign.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        //23FBC0 serializes native2E immediately before30. The complete32B
        //CE row's last two bytes are2E and30, respectively.
        foreach (var row in _definitions.Catalog.States) values.Add(StateRegionKey(row.Slot), row.RawBytes[30]);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    public static byte? ReadStateRegion(GameState state, int physical) => physical is >= 0 and < 23 &&
        state.NativeArmySimulation?.Parameters.Int32Values is { } values && values.GetValueOrDefault(VersionKey) == 1 &&
        values.TryGetValue(StateRegionKey(physical), out int raw) && raw is >= 0 and <= 255 ? (byte)raw : null;
}
