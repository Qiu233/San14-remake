using San14.Core;

namespace San14.Data;

/// <summary>Project person return path rules. Current queries read the saved parameter bank.</summary>
public sealed class PersonReturnPathParameterSeed
{
    private const int LandAdvantageDivisor = 2;
    private const int JourneyCoefficient = 35;
    private const int PersonalityJourneyMultiplier = 1;
    public const string VersionKey = "ce-person-return-path-current:version";
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<BattleRemakeCurrentParameterField> Fields { get; }
    private PersonReturnPathParameterSeed(string language, SourceProvenance source,
        BattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static PersonReturnPathParameterSeed Load(GameResourceRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/person-return-path/v1", language);
        // Project rules install their named keys in the saved parameter bank.
        BattleRemakeCurrentParameterField[] fields = [
            new(SimulationParameterKeys.PersonReturn.LandAdvantageDivisor, LandAdvantageDivisor),
            new(SimulationParameterKeys.PersonReturn.JourneyCoefficient, JourneyCoefficient),
            new(SimulationParameterKeys.PersonReturn.PersonalityJourneyMultiplier, PersonalityJourneyMultiplier)
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
            string key = field.Key;
            if (simulation.Parameters.Float32Values.ContainsKey(key) || values.TryGetValue(key, out int old) && old != field.Value)
                throw new InvalidDataException("Person return parameter conflicts with an installed source field.");
            values.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static int? ReadCurrent(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } owner && owner.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        owner.Int32Values.TryGetValue(key, out int value) ? value : null;
}
