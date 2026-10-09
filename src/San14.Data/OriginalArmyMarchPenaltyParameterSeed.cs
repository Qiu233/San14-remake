using San14.Core;

namespace San14.Data;

/// <summary>Project march penalty rules, installed only into a fresh saved Army parameter allocation.</summary>
public sealed class OriginalArmyMarchPenaltyParameterSeed
{
    private const int RadiusOneArmyWeight = 300;
    private const int RadiusTwoArmyWeight = 5;
    private const int TerritoryWeight = 5;
    private const int RadiusThreeArmyWeight = 0;
    public const string Revision = "san14:ce-current-march-penalty-weights-20261003";
    public const int RadiusOneWeightScRva = 0x1A4AC3C;
    public const int RadiusTwoWeightScRva = 0x1A4AC48;
    public const int RadiusThreeWeightScRva = 0x27D47A4;
    public const int TerritoryWeightScRva = 0x1A4AC5C;
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalArmyMarchPenaltyParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields)
    { Language = language; Source = source; Fields = Array.AsReadOnly(fields); }

    public static OriginalArmyMarchPenaltyParameterSeed Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/march-penalty/v1", language);
        // Legacy addresses identify existing saved keys; the values are project rules.
        OriginalBattleRemakeCurrentParameterField[] fields = [
            new(0x1A4AC3C, language == "TC" ? 0x1A49C0C : 0x1A4AC3C, RadiusOneArmyWeight, string.Empty),
            new(0x1A4AC48, language == "TC" ? 0x1A49C18 : 0x1A4AC48, RadiusTwoArmyWeight, string.Empty),
            new(0x1A4AC5C, language == "TC" ? 0x1A49C2C : 0x1A4AC5C, TerritoryWeight, string.Empty),
            new(0x27D47A4, language == "TC" ? 0x27D3744 : 0x27D47A4, RadiusThreeArmyWeight, string.Empty)
        ];
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != Source.RuleVersion || Fields.Any(f =>
                simulation.Parameters.Int32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(f.SemanticScRva)) ||
                simulation.Parameters.Float32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(f.SemanticScRva))))
            throw new InvalidDataException("Fresh penalty allocation cannot replace current or omitted saved owners.");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var field in Fields) integers.Add(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva), field.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }
}
