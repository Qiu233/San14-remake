using San14.Core;

namespace San14.Data;

/// <summary>Project capture area-radius rules and
/// independent current World1654. Explicit fresh installation only; current
/// captures read the saved bank and current Emperor.</summary>
public sealed class OriginalCityCaptureTerritoryInputs
{
    public const string VersionKey = "ce-city-capture-territory:version";
    public const string Emperor1654Key = "ce-world-current-emperor:word1654";
    private const int MaximumCaptureAreaRadius = 4;
    private const int RankRadiusDivisor = 4;
    private const int LeaderCharmRadiusThreshold = -40;
    private const int SquaredCharmRadiusDivisor = 9000;
    public static readonly int[] TcRvas = [0x1A4A7D8, 0x1A4A7E4, 0x1A4A7F0, 0x1A4A7FC];
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalCityCaptureTerritoryInputs(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static OriginalCityCaptureTerritoryInputs Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-rule://simulation/city-capture-territory", language);
        int[] rules = [MaximumCaptureAreaRadius, RankRadiusDivisor, LeaderCharmRadiusThreshold,
            SquaredCharmRadiusDivisor];
        // Legacy addresses retain the existing saved parameter keys.
        var fields = TcRvas.Select((tc, index) =>
        {
            int semantic = tc + 0x1030, physical = language == "SC" ? semantic : tc;
            return new OriginalBattleRemakeCurrentParameterField(semantic, physical, rules[index], string.Empty);
        }).ToArray();
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state, LoadedScenario scenario)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != Source.RuleVersion || scenario.Snapshot.Locale != Language ||
            scenario.Snapshot.WorldParameters is not { } world || simulation.Parameters.Int32Values.ContainsKey(VersionKey) ||
            simulation.Parameters.Int32Values.ContainsKey(Emperor1654Key)) throw Missing("fresh_only_source_allocation");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1, [Emperor1654Key] = world.RawEmperorPersonId };
        foreach (var field in Fields)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (simulation.Parameters.Float32Values.ContainsKey(key) || values.TryGetValue(key, out int old) && old != field.Value)
                throw Missing("inconsistent_source_scalar");
            values.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    public static int? ReadCurrentTc(GameState state, int tcRva) => state.NativeArmySimulation?.Parameters is { } bank &&
        bank.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        bank.Int32Values.TryGetValue(OriginalTacticRemakeParameterSeed.Key(tcRva + 0x1030), out int value) ? value : null;
    public static ushort? ReadEmperor1654(GameState state) => state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
        values.GetValueOrDefault(VersionKey) == 1 && values.TryGetValue(Emperor1654Key, out int id) && id is >= 0 and <= 65535 ? (ushort)id : null;
    public static void BindCurrentEmperor(GameState state, OriginalEventInitializationContext current) => current.RawEmperorId = ReadEmperor1654(state);
    private static InvalidDataException Missing(string field) => new("current.City_capture_inputs." + field);
}
