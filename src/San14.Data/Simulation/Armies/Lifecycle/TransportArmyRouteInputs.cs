using San14.Core;

namespace San14.Data;

/// <summary>Project transport route rules. Installation never repairs omitted saved fields.</summary>
public sealed class TransportArmyRouteInputs
{
    private const int WallPenaltyPercent = 5000;
    private const int BlockingAbnormalThreshold = 20;
    private const int FriendlyOccupantPenalty = 1000;
    private const int NonblockingOccupantPenalty = 100;
    private const int BlockingOccupantPenalty = 1500;
    public const string VersionKey = "ce-transport-route-current:version";
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<BattleRemakeCurrentParameterField> Fields { get; }
    private TransportArmyRouteInputs(string language, SourceProvenance source,
        BattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static TransportArmyRouteInputs Load(GameResourceRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/transport-route/v1", language);
        // Project rules install their named keys in the saved parameter bank.
        BattleRemakeCurrentParameterField[] fields = [
            new(SimulationParameterKeys.TransportRoute.WallPenaltyPercent, WallPenaltyPercent),
            new(SimulationParameterKeys.TransportRoute.BlockingAbnormalThreshold, BlockingAbnormalThreshold),
            new(SimulationParameterKeys.TransportRoute.FriendlyOccupantPenalty, FriendlyOccupantPenalty),
            new(SimulationParameterKeys.TransportRoute.NonblockingOccupantPenalty, NonblockingOccupantPenalty),
            new(SimulationParameterKeys.TransportRoute.BlockingOccupantPenalty, BlockingOccupantPenalty)
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
            string key = field.Key;
            if (simulation.Parameters.Float32Values.ContainsKey(key) || values.TryGetValue(key, out int old) && old != field.Value)
                throw new InvalidDataException("Transport route parameter conflicts with an existing source field.");
            values.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static int? ReadCurrent(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } owner && owner.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        owner.Int32Values.TryGetValue(key, out int value) ? value : null;
}
