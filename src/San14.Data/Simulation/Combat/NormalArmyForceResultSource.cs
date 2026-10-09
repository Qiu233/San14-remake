using San14.Core;

namespace San14.Data;

public sealed record NormalArmyForceResultParameters(int TroopLossScale, int KilledAddend);
public sealed record NormalArmyForceResultSourceField(string Key, int Value);
public sealed class NormalArmyForceResultSourceParameters
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public BattleUnitSourceProfile SourceCodeProfile { get; }
    public IReadOnlyList<NormalArmyForceResultSourceField> Fields { get; }
    public NormalArmyForceResultParameters Values { get; }
    public string? CurrentRevision { get; }
    internal NormalArmyForceResultSourceParameters(string language, SourceProvenance source,
        BattleUnitSourceProfile profile, IReadOnlyList<NormalArmyForceResultSourceField> fields,
        NormalArmyForceResultParameters values, string? revision)
    { Language=language;Source=source;SourceCodeProfile=profile;Fields=fields;Values=values;CurrentRevision=revision; }
}

public sealed record NormalArmyForceResultReceipt(bool NativeReturn,
    bool WorldGate, bool ResultGate, byte? FlagsAfter, int? ProgressAfter,
    int ProgressDelta, string Revision);
public enum NormalArmyForceResultOutcome { Committed, ResolvedWithoutWrite, CurrentInputsUnresolved }
public sealed record NormalArmyForceResultApplication(NormalArmyForceResultOutcome Outcome,
    GameState State, NormalArmyForceResultReceipt? Receipt);
public interface INormalArmyForceResultDefinitionQueries
{
    int? ReadResultDefinitionIdentity(int locationId);
}

/// <summary>Adapts ordinary troop-loss and destruction rewards to current
/// source eligibility and location ownership. The enclosing battle owns
/// death, trait and report processing; this leaf does not publish that battle.</summary>
public static class NormalArmyForceResultSource
{
    private const int TroopLossScale = 10;
    private const int KilledAddend = 100;
    /// <summary>Publishes the reached status flag and combat objective progress
    /// after current inputs resolve. Unknown input leaves the caller unchanged.
    /// A successful standalone result owns its mutable world collections.</summary>
    public static NormalArmyForceResultApplication TryApply(GameState input, int attackerForceSlot,
        int targetForceSlot, int cappedLoss, bool killed, SourceWorldContext definitions,
        INormalArmyForceResultDefinitionQueries registry,
        NormalArmyForceResultSourceParameters source, string revision)
        => Apply(input, attackerForceSlot, targetForceSlot, cappedLoss, killed, definitions, registry, source, revision, false);

    internal static NormalArmyForceResultApplication ApplyWithinArmyOperation(GameState input, int attackerForceSlot,
        int targetForceSlot, int cappedLoss, bool killed, SourceWorldContext definitions,
        INormalArmyForceResultDefinitionQueries registry, NormalArmyForceResultSourceParameters source, string revision)
        => Apply(input, attackerForceSlot, targetForceSlot, cappedLoss, killed, definitions, registry, source, revision, true);

    private static NormalArmyForceResultApplication Apply(GameState input, int attackerForceSlot,
        int targetForceSlot, int cappedLoss, bool killed, SourceWorldContext definitions,
        INormalArmyForceResultDefinitionQueries registry, NormalArmyForceResultSourceParameters source, string revision,
        bool owned)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        if (attackerForceSlot is < 0 or > 51) throw new InvalidDataException("Invalid attacking native Force slot.");
        if (input.NativeArmySimulation is not { Parameters: { } parameters } simulation ||
            parameters.Language != source.Language || parameters.RuleVersion != source.Source.RuleVersion ||
            definitions.StaticSource is not { } scenario ||
            input.ContentHashes.GetValueOrDefault(scenario.ResourceUri) != scenario.Sha256)
            return new(NormalArmyForceResultOutcome.CurrentInputsUnresolved,input,null);
        if (cappedLoss is < 0 or > ushort.MaxValue || targetForceSlot is < 0 or > 51)
            throw new InvalidDataException("Ordinary Force result requires capped Army loss and a native Force slot.");
        var resolved = ForceCombatObjectiveRules.Resolve(new ForceCombatObjectiveCurrent(input, attackerForceSlot,
            definitions, registry, () => ReadCurrent(input, source) is { } values
                ? new(values.TroopLossScale, values.KilledAddend) : null), targetForceSlot, cappedLoss, killed);
        if (resolved is null) return new(NormalArmyForceResultOutcome.CurrentInputsUnresolved,input,null);
        byte? flags = resolved.MarkSelectedForceDefeated
            ? (byte)(simulation.Forces[attackerForceSlot].Flags12!.Value | 4) : null;
        var receipt = new NormalArmyForceResultReceipt(resolved.Triggered, resolved.SelectedForceMatched,
            resolved.ObjectiveMatched, flags, resolved.ProgressAfter, resolved.ProgressDelta,
            $"{revision}:{parameters.Revision}:{scenario.Sha256}");
        if (receipt.FlagsAfter is null && receipt.ProgressAfter is null)
            return new(NormalArmyForceResultOutcome.ResolvedWithoutWrite,input,receipt);
        if (!simulation.Forces.ContainsKey(attackerForceSlot))
            return new(NormalArmyForceResultOutcome.CurrentInputsUnresolved,input,null);
        var detached = owned ? input : GameStateCopy.Copy(input);
        var owner = detached.NativeArmySimulation!;
        var before = owner.Forces[attackerForceSlot];
        var forces = new Dictionary<int,NativeArmyForceState>(owner.Forces)
        { [attackerForceSlot] = before with { Flags12 = receipt.FlagsAfter ?? before.Flags12 } };
        var domainForces = detached.Forces;
        if (receipt.ProgressAfter is int progress)
            domainForces = new(domainForces) { [attackerForceSlot] = domainForces[attackerForceSlot] with
                { CombatObjective = domainForces[attackerForceSlot].CombatObjective! with { Progress = progress } } };
        GameState result = detached with { NativeArmySimulation = owner with { Forces = forces }, Forces = domainForces };
        return new(NormalArmyForceResultOutcome.Committed,result,receipt);
    }

    /// <summary>Project rules for the two parameters used by the ordinary Army Force result callback.</summary>
    public static NormalArmyForceResultSourceParameters LoadInitial(GameResourceRepository repository,
        string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = SimulationRuleSources.Provenance("san14-remake://rules/normal-army-force-result/v1", language);
        NormalArmyForceResultSourceField[] fields = [
            new(SimulationParameterKeys.ForceCombatResult.TroopLossScale, TroopLossScale),
            new(SimulationParameterKeys.ForceCombatResult.KilledAddend, KilledAddend)
        ];
        return new(language, source, ForLanguage(language), Array.AsReadOnly(fields),
            new(TroopLossScale, KilledAddend), null);
    }

    public static GameState InstallFresh(GameState state, NormalArmyForceResultSourceParameters source, string revision)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        if (source.CurrentRevision is not null || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Units.Count != 0 || state.NativeArmySimulation is not { Parameters: { } parameters } simulation ||
            parameters.Language != source.Language || parameters.RuleVersion != source.Source.RuleVersion)
            throw new InvalidDataException("Force result seed needs a fresh matching empty simulation.");
        var integers = new Dictionary<string,int>(parameters.Int32Values, parameters.Int32Values.Comparer);
        foreach (var field in source.Fields)
        {
            string key = field.Key;
            if (integers.Keys.Concat(parameters.Float32Values.Keys).Any(k => string.Equals(k,key,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Force result seed cannot overwrite a current parameter.");
            integers.Add(key,field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = parameters with
        { Int32Values = integers, Float32Values = new(parameters.Float32Values,parameters.Float32Values.Comparer),
          Revision = $"{parameters.Revision}|{revision}:normal-force-result:{source.Source.RuleVersion}" } } };
    }

    public static NormalArmyForceResultParameters? ReadCurrent(GameState state,
        NormalArmyForceResultSourceParameters source)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != source.Language ||
            parameters.RuleVersion != source.Source.RuleVersion || string.IsNullOrWhiteSpace(parameters.Revision)) return null;
        int[] values = new int[2];
        for (int i = 0; i < 2; i++)
        {
            string key = source.Fields[i].Key;
            if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key,out values[i])) return null;
        }
        return new(values[0],values[1]);
    }

    public static BattleUnitSourceProfile ForLanguage(string language)
    {
        return new(language, SimulationRuleIdentity.ForLocale(language));
    }

}
