using San14.Core;

namespace San14.Data;

/// <summary>Project capture resource rules, installed once in a fresh parameter
/// bank. Current capture operations continue to read saved values.</summary>
public sealed class BaseCaptureResourceInputs
{
    public const string VersionKey = "ce-base-capture-resources:version";
    private const int MinimumResourceKeepPercent = 10;
    private const int CommanderCharmDivisor = 3;
    private const int CapturedCityGoldFloor = 5000;
    private const int CentralTerritoryRadius = 3;
    private const int CapturedCitySuppliesFloor = 20000;
    private const int CapturedWallFloorPercent = 40;
    public static readonly string[] ParameterKeys = [SimulationParameterKeys.BaseCapture.MinimumResourceKeepPercent, SimulationParameterKeys.BaseCapture.CommanderCharmDivisor, SimulationParameterKeys.BaseCapture.CapturedCityGoldFloor, SimulationParameterKeys.BaseCapture.CentralTerritoryRadius, SimulationParameterKeys.BaseCapture.CapturedCitySuppliesFloor, SimulationParameterKeys.BaseCapture.CapturedWallFloorPercent];
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<BattleRemakeCurrentParameterField> Fields { get; }
    private BaseCaptureResourceInputs(string language, SourceProvenance source,
        BattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static BaseCaptureResourceInputs Load(GameResourceRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-rule://simulation/base-capture-resources", language);
        int[] rules = [MinimumResourceKeepPercent, CommanderCharmDivisor, CapturedCityGoldFloor,
            CentralTerritoryRadius, CapturedCitySuppliesFloor, CapturedWallFloorPercent];
        // Project rules use named keys independent of locale.
        var fields = ParameterKeys.Select((key, index) =>
            new BattleRemakeCurrentParameterField(key, rules[index])).ToArray();
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != Source.RuleVersion || simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Fresh Base capture parameters cannot replace current saved values.");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var field in Fields)
        {
            string key = field.Key;
            if (simulation.Parameters.Float32Values.ContainsKey(key) || integers.TryGetValue(key, out int old) && old != field.Value)
                throw new InvalidDataException("Base capture parameters disagree with an installed source field.");
            integers.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }
    public static int? ReadCurrent(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } bank && bank.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        bank.Int32Values.TryGetValue(key, out int value) ? value : null;
}
