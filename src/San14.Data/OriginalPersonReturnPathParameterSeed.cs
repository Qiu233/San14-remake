using San14.Core;

namespace San14.Data;

/// <summary>Project person return path rules. Current queries read the saved parameter bank.</summary>
public sealed class OriginalPersonReturnPathParameterSeed
{
    private const int LandAdvantageDivisor = 2;
    private const int JourneyCoefficient = 35;
    private const int PersonalityJourneyMultiplier = 1;
    public const string VersionKey = "ce-person-return-path-current:version";
    public static readonly int[] TcRvas = [0x1A49BAC, 0x1A4A9D0, 0x1A49570];
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalPersonReturnPathParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static OriginalPersonReturnPathParameterSeed Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/person-return-path/v1", language);
        // Legacy addresses identify existing saved keys; the values are project rules.
        OriginalBattleRemakeCurrentParameterField[] fields = [
            new(0x1A4ABDC, language == "TC" ? 0x1A49BAC : 0x1A4ABDC, LandAdvantageDivisor, string.Empty),
            new(0x1A4BA00, language == "TC" ? 0x1A4A9D0 : 0x1A4BA00, JourneyCoefficient, string.Empty),
            new(0x1A4A5A0, language == "TC" ? 0x1A49570 : 0x1A4A5A0, PersonalityJourneyMultiplier, string.Empty)
        ];
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != Source.RuleVersion || simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Fresh person return parameters cannot replace a current saved allocation.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var field in Fields) {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (simulation.Parameters.Float32Values.ContainsKey(key) || values.TryGetValue(key, out int old) && old != field.Value)
                throw new InvalidDataException("Person return parameter conflicts with an installed source field.");
            values.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static int? ReadCurrentTc(GameState state, int tcRva) =>
        state.NativeArmySimulation?.Parameters is { } owner && owner.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        owner.Int32Values.TryGetValue(OriginalTacticRemakeParameterSeed.Key(tcRva + 0x1030), out int value) ? value : null;
}
