using San14.Core;

namespace San14.Data;

/// <summary>Project mobility rules for a fresh saved Army parameter allocation. Current queries continue to read their saved values.</summary>
public sealed class ArmyMobilityParameterSeed
{
    private const int FormationFiveMobilityBonus = 2;
    private const int TraitMobilityLimit = 10;
    private const int TraitMobilityStep = 5;
    private const int ForceTerrainPercent = 200;
    private const int EnvironmentTerrainPercent = 130;
    private const int FormationTerrainPercent = 150;
    private const int FormationSixTerrainPercent = 150;
    private const float TerrainNormalization = 240f;
    private const float PercentDenominator = 100f;
    private const float MultiplicativeIdentity = 1f;
    public const string ProfileId = "san14:ce-owned-army-mobility-parameters-20261003";
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<BattleRemakeCurrentParameterField> Fields { get; }
    public float BaselineTerrainNormalization { get; }
    public float Percent { get; }
    public float One { get; }
    private ArmyMobilityParameterSeed(string language, SourceProvenance source,
        BattleRemakeCurrentParameterField[] fields, float normalization, float percent, float one)
    { Language = language; Source = source; Fields = Array.AsReadOnly(fields);
        BaselineTerrainNormalization = normalization; Percent = percent; One = one; }

    public static ArmyMobilityParameterSeed Load(GameResourceRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/army-mobility/v1", language);
        // Defaults use the shared simulation parameter vocabulary.
        BattleRemakeCurrentParameterField[] fields = [
            new(SimulationParameterKeys.ArmyMobility.FormationFiveMobilityBonus, FormationFiveMobilityBonus),
            new(SimulationParameterKeys.ArmyMobility.TraitMobilityLimit, TraitMobilityLimit),
            new(SimulationParameterKeys.ArmyMobility.TraitMobilityStep, TraitMobilityStep),
            new(SimulationParameterKeys.ArmyMobility.ForceTerrainPercent, ForceTerrainPercent),
            new(SimulationParameterKeys.ArmyMobility.EnvironmentTerrainPercent, EnvironmentTerrainPercent),
            new(SimulationParameterKeys.ArmyMobility.FormationTerrainPercent, FormationTerrainPercent),
            new(SimulationParameterKeys.ArmyMobility.FormationSixTerrainPercent, FormationSixTerrainPercent)
        ];
        return new(language, source, fields, TerrainNormalization, PercentDenominator, MultiplicativeIdentity);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Units.Count != 0 || simulation.Parameters.Language != Language || simulation.Parameters.RuleVersion != Source.RuleVersion ||
            Fields.Any(f => simulation.Parameters.Int32Values.ContainsKey(f.Key) ||
                simulation.Parameters.Float32Values.ContainsKey(f.Key)))
            throw new InvalidDataException("Fresh mobility scalars cannot replace a current owner or repair omitted saved values.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var field in Fields) values.Add(field.Key, field.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
