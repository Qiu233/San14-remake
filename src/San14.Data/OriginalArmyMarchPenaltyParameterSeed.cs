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
        // Defaults use the shared simulation parameter vocabulary.
        OriginalBattleRemakeCurrentParameterField[] fields = [
            new(SimulationParameterKeys.ArmyMarchPenalty.RadiusOneArmyWeight, RadiusOneArmyWeight),
            new(SimulationParameterKeys.ArmyMarchPenalty.RadiusTwoArmyWeight, RadiusTwoArmyWeight),
            new(SimulationParameterKeys.ArmyMarchPenalty.TerritoryWeight, TerritoryWeight),
            new(SimulationParameterKeys.ArmyMarchPenalty.RadiusThreeArmyWeight, RadiusThreeArmyWeight)
        ];
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.RuleVersion != Source.RuleVersion || Fields.Any(f =>
                simulation.Parameters.Int32Values.ContainsKey(f.Key) ||
                simulation.Parameters.Float32Values.ContainsKey(f.Key)))
            throw new InvalidDataException("Fresh penalty allocation cannot replace current or omitted saved owners.");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var field in Fields) integers.Add(field.Key, field.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }
}
