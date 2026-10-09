using San14.Core;

namespace San14.Data;

/// <summary>Project logistics morale rules and explicit fresh saved Base morale owners. Queries continue to read current saved values.</summary>
public sealed class OriginalArmyMoraleParameterSeed
{
    private const int LogisticsMoraleGain = 15;
    private const int NegativeMoraleAdjustmentScale = 1;
    public string Language { get; }
    public SourceProvenance Source { get; }
    public int LogisticsGain { get; }
    public int NegativeAdjustmentScale { get; }
    private OriginalArmyMoraleParameterSeed(string language,SourceProvenance source,int gain,int adjustmentScale)
        =>(Language,Source,LogisticsGain,NegativeAdjustmentScale)=(language,source,gain,adjustmentScale);
    public static OriginalArmyMoraleParameterSeed LoadPinnedSeed(OriginalGameRepository repository,string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(language, SimulationRuleSources.Provenance("san14-remake://rules/army-morale/v1", language),
            LogisticsMoraleGain, NegativeMoraleAdjustmentScale);
    }

    public GameState InstallFresh(GameState state,OriginalEventInitializationContext freshDefinitions,string revision)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(freshDefinitions);
        string adjustmentKey=SimulationParameterKeys.Morale.NegativeAdjustmentScale;
        if(state.NativeArmySimulation?.Parameters is not { } parameters||state.NativeClock is not { UpdateOrdinal:0,UpdatesIntoPeriod:0 } clock||
            state.Units.Count!=0||parameters.Int32Values.Keys.Any(key=>key.StartsWith("native-base-morale44:",StringComparison.Ordinal))||
            parameters.Int32Values.ContainsKey(adjustmentKey)||parameters.Float32Values.ContainsKey(adjustmentKey)||
            parameters.Language!=Language||parameters.RuleVersion!=Source.RuleVersion||string.IsNullOrWhiteSpace(revision)||
            freshDefinitions.StaticSource is not { } definitionSource||string.IsNullOrWhiteSpace(definitionSource.ResourceUri)||
            state.ContentHashes.GetValueOrDefault(definitionSource.ResourceUri)!=definitionSource.Sha256||
            state.NativeArmySimulation.World.SamplerEpoch is { } epoch&&
                (epoch.NativeClockRevision!=clock.Revision||epoch.SourceScenarioUri!=definitionSource.ResourceUri||epoch.SourceScenarioSha256!=definitionSource.Sha256))
            throw new InvalidDataException("Fresh Army morale owners require the same source scenario/clock and matching initial parameter allocation.");
        var values=new Dictionary<string,int>(parameters.Int32Values);string gainKey=SimulationParameterKeys.Morale.LogisticsGain;
        values.TryAdd(gainKey,LogisticsGain);
        values.Add(adjustmentKey,NegativeAdjustmentScale);
        for(int slot=0;slot<52;slot++)Copy(freshDefinitions.ReadCityTableReference(slot));
        for(int slot=0;slot<11;slot++)Copy(freshDefinitions.ReadGateTableReference(slot));
        void Copy(OriginalBaseReference? reference)
        {
            if(reference?.PhysicalSlot is null)return;
            if(freshDefinitions.ReadBaseMorale44(reference) is byte value)values.TryAdd(OriginalArmyMoraleSource.BaseMorale44Key(reference),value);
        }
        return state with{NativeArmySimulation=state.NativeArmySimulation with{
            Parameters=parameters with{Revision=revision,Int32Values=values}}};
    }
}
