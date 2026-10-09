using San14.Core;

namespace San14.Data;

/// <summary>Project transport route rules. Installation never repairs omitted saved fields.</summary>
public sealed class OriginalTransportArmyRouteInputs
{
    private const int WallPenaltyPercent = 5000;
    private const int BlockingAbnormalThreshold = 20;
    private const int FriendlyOccupantPenalty = 1000;
    private const int NonblockingOccupantPenalty = 100;
    private const int BlockingOccupantPenalty = 1500;
    public const string VersionKey = "ce-transport-route-current:version";
    public static readonly int[] TcRvas = [0x1A4B1BC, 0x1A4B1C8, 0x1A4B1D4, 0x1A4B1E0, 0x1A4B1EC];
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalTransportArmyRouteInputs(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static OriginalTransportArmyRouteInputs Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/transport-route/v1", language);
        // Legacy addresses identify existing saved keys; the values are project rules.
        OriginalBattleRemakeCurrentParameterField[] fields = [
            new(0x1A4C1EC, language == "TC" ? 0x1A4B1BC : 0x1A4C1EC, WallPenaltyPercent, string.Empty),
            new(0x1A4C1F8, language == "TC" ? 0x1A4B1C8 : 0x1A4C1F8, BlockingAbnormalThreshold, string.Empty),
            new(0x1A4C204, language == "TC" ? 0x1A4B1D4 : 0x1A4C204, FriendlyOccupantPenalty, string.Empty),
            new(0x1A4C210, language == "TC" ? 0x1A4B1E0 : 0x1A4C210, NonblockingOccupantPenalty, string.Empty),
            new(0x1A4C21C, language == "TC" ? 0x1A4B1EC : 0x1A4C21C, BlockingOccupantPenalty, string.Empty)
        ];
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != Source.RuleVersion || simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Fresh transport route fields cannot replace a saved owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var field in Fields) {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (simulation.Parameters.Float32Values.ContainsKey(key) || values.TryGetValue(key, out int old) && old != field.Value)
                throw new InvalidDataException("Transport route parameter conflicts with an existing source field.");
            values.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static int? ReadCurrentTc(GameState state, int tcRva) =>
        state.NativeArmySimulation?.Parameters is { } owner && owner.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        owner.Int32Values.TryGetValue(OriginalTacticRemakeParameterSeed.Key(tcRva + 0x1030), out int value) ? value : null;
}
