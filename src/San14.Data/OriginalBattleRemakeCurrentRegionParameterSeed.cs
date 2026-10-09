using San14.Core;

namespace San14.Data;

/// <summary>Project region factor rules, installed only into a fresh saved parameter allocation.</summary>
public sealed class OriginalBattleRemakeCurrentRegionParameterSeed
{
    private const int BaselineRegionCoefficient = 4;
    private const int DifficultyThreeNonplayerCoefficient = 16;
    private const int HigherDifficultyNonplayerCoefficient = 24;
    private const int RegionMultiplierModeOnePercent = 120;
    private const int RegionMultiplierModeTwoPercent = 140;
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalBattleRemakeCurrentRegionParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) =
            (language, source, Array.AsReadOnly(fields));

    public static OriginalBattleRemakeCurrentRegionParameterSeed Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/battle-region/v1", language);
        // Defaults use the shared simulation parameter vocabulary.
        OriginalBattleRemakeCurrentParameterField[] fields = [
            new(SimulationParameterKeys.BattleRegion.BaselineRegionCoefficient, BaselineRegionCoefficient),
            new(SimulationParameterKeys.BattleRegion.DifficultyThreeNonplayerCoefficient, DifficultyThreeNonplayerCoefficient),
            new(SimulationParameterKeys.BattleRegion.HigherDifficultyNonplayerCoefficient, HigherDifficultyNonplayerCoefficient),
            new(SimulationParameterKeys.BattleRegion.RegionMultiplierModeOnePercent, RegionMultiplierModeOnePercent),
            new(SimulationParameterKeys.BattleRegion.RegionMultiplierModeTwoPercent, RegionMultiplierModeTwoPercent)
        ];
        return new(language, source, fields);
    }

    public GameState InstallFresh(GameState state, string revision)
    {
        if (state.NativeArmySimulation is not { Parameters: { } current } simulation ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } clock || clock.AnchorDate != state.Date ||
            state.Units.Count != 0 || current.Language != Language || current.RuleVersion != Source.RuleVersion ||
            string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(current.Revision))
            throw new InvalidDataException("Current region globals require an empty fresh matching owner.");
        foreach (var field in Fields)
            if (current.Int32Values.ContainsKey(field.Key) ||
                current.Float32Values.ContainsKey(field.Key))
                throw new InvalidDataException("Current region seed cannot replace an existing or partial saved allocation.");
        var values = new Dictionary<string, int>(current.Int32Values, current.Int32Values.Comparer);
        foreach (var field in Fields) values.Add(field.Key, field.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = current with {
            Revision = $"{current.Revision}|{revision}:region-2B09B0", Int32Values = values,
            Float32Values = new(current.Float32Values, current.Float32Values.Comparer) } } };
    }
}
