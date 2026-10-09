using San14.Core;

namespace San14.Data;

/// <summary>Project mobility rules for a fresh saved Army parameter allocation. Current queries continue to read their saved values.</summary>
public sealed class OriginalArmyMobilityParameterSeed
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
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    public float BaselineTerrainNormalization { get; }
    public float Percent { get; }
    public float One { get; }
    private OriginalArmyMobilityParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields, float normalization, float percent, float one)
    { Language = language; Source = source; Fields = Array.AsReadOnly(fields);
        BaselineTerrainNormalization = normalization; Percent = percent; One = one; }

    public static OriginalArmyMobilityParameterSeed Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/army-mobility/v1", language);
        // Legacy addresses identify existing saved keys; the values are project rules.
        OriginalBattleRemakeCurrentParameterField[] fields = [
            new(0x1A4AB98, language == "TC" ? 0x1A49B68 : 0x1A4AB98, FormationFiveMobilityBonus, string.Empty),
            new(0x1A49C40, language == "TC" ? 0x1A48C10 : 0x1A49C40, TraitMobilityLimit, string.Empty),
            new(0x1A4A8F4, language == "TC" ? 0x1A498C4 : 0x1A4A8F4, TraitMobilityStep, string.Empty),
            new(0x1A49C58, language == "TC" ? 0x1A48C28 : 0x1A49C58, ForceTerrainPercent, string.Empty),
            new(0x1A49C64, language == "TC" ? 0x1A48C34 : 0x1A49C64, EnvironmentTerrainPercent, string.Empty),
            new(0x1A49C4C, language == "TC" ? 0x1A48C1C : 0x1A49C4C, FormationTerrainPercent, string.Empty),
            new(0x1A4C6EC, language == "TC" ? 0x1A4B6BC : 0x1A4C6EC, FormationSixTerrainPercent, string.Empty)
        ];
        return new(language, source, fields, TerrainNormalization, PercentDenominator, MultiplicativeIdentity);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Units.Count != 0 || simulation.Parameters.Language != Language || simulation.Parameters.RuleVersion != Source.RuleVersion ||
            Fields.Any(f => simulation.Parameters.Int32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(f.SemanticScRva)) ||
                simulation.Parameters.Float32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(f.SemanticScRva))))
            throw new InvalidDataException("Fresh mobility scalars cannot replace a current owner or repair omitted saved values.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var field in Fields) values.Add(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva), field.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
