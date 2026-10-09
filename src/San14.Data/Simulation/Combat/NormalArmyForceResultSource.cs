using System.Text.Json;
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

/// <summary>Reached current native reads inside ordinary kind27/null-second-Force
/// SC257420. Definition identity is the actual registry object's virtual B0 result,
/// not a Core force ID. Missing values stop only if their branch is reached.</summary>
public interface INormalArmyForceResultCurrent
{
    bool? ReadAttackerForceValidity();
    byte? ReadWorldFlags165C();
    byte? ReadAttackerFlags12();
    byte? ReadWorldSelectedForce1656();
    byte? ReadAttackerResultGate15F();
    ushort? ReadAttackerResultValue162();
    byte? ReadAttackerResultDefinition160();
    int? ReadResultDefinitionIdentity(byte physicalSlot);
    NormalArmyForceResultParameters? ReadCurrentParameters();
}

public sealed record NormalArmyForceResultReceipt(bool NativeReturn,
    bool WorldGate, bool ResultGate, byte? Flags12After, ushort? Value162After,
    int Delta162, string Revision);
public enum NormalArmyForceResultOutcome { Committed, ResolvedWithoutWrite, CurrentInputsUnresolved }
public sealed record NormalArmyForceResultApplication(NormalArmyForceResultOutcome Outcome,
    GameState State, NormalArmyForceResultReceipt? Receipt);
public interface INormalArmyForceResultDefinitionQueries
{
    int? ReadResultDefinitionIdentity(byte physicalSlot);
}

/// <summary>Complete ordinary Army Force callback only. The caller passes a
/// resolved null second Force pointer, target raw Force slot, capped loss and
/// killed flag. Death/trait/report callbacks in the enclosing application are
/// distinct; this method does not publish an enclosing battle result.</summary>
public static class NormalArmyForceResultSource
{
    private const int TroopLossScale = 10;
    private const int KilledAddend = 100;
    /// <summary>Writes only reached Force12/162 after every current input has
    /// resolved. Missing owners preserve the entire input. Native validity uses
    /// the matching current source definition snapshot; no UI alive inference.</summary>
    public static NormalArmyForceResultApplication TryApply(GameState input, int attackerForceSlot,
        int targetForceSlot, int cappedLoss, bool killed, SourceWorldContext definitions,
        INormalArmyForceResultDefinitionQueries registry,
        NormalArmyForceResultSourceParameters source, string revision)
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
        var receipt = Resolve(new OwnedCurrent(input,attackerForceSlot,definitions,registry,source),
            targetForceSlot,cappedLoss,killed,$"{revision}:{parameters.Revision}:{scenario.Sha256}");
        if (receipt is null) return new(NormalArmyForceResultOutcome.CurrentInputsUnresolved,input,null);
        if (receipt.Flags12After is null && receipt.Value162After is null)
            return new(NormalArmyForceResultOutcome.ResolvedWithoutWrite,input,receipt);
        if (!simulation.Forces.ContainsKey(attackerForceSlot))
            return new(NormalArmyForceResultOutcome.CurrentInputsUnresolved,input,null);
        var detached = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(input))
            ?? throw new InvalidDataException("Force result current state failed to detach.");
        var owner = detached.NativeArmySimulation!;
        var before = owner.Forces[attackerForceSlot];
        var forces = new Dictionary<int,NativeArmyForceState>(owner.Forces)
        { [attackerForceSlot] = before with { Flags12 = receipt.Flags12After ?? before.Flags12,
            ResultValue162 = receipt.Value162After ?? before.ResultValue162 } };
        GameState result = detached with { NativeArmySimulation = owner with { Forces = forces } };
        NativeSubdayStateValidation.Validate(result);
        return new(NormalArmyForceResultOutcome.Committed,result,receipt);
    }

    public static NormalArmyForceResultReceipt? Resolve(INormalArmyForceResultCurrent current,
        int targetForceSlot, int cappedLoss, bool killed, string revision)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        if (cappedLoss is < 0 or > ushort.MaxValue || targetForceSlot is < 0 or > 51)
            throw new InvalidDataException("Ordinary Force result requires capped Army loss and a native Force slot.");
        if (current.ReadAttackerForceValidity() is not bool valid) return null;
        if (!valid) return new(false, false, false, null, null, 0, revision);
        if (current.ReadWorldFlags165C() is not byte worldFlags) return null;
        bool worldGate = false;
        byte? flags = null;
        if (worldFlags > 0)
        {
            if (current.ReadAttackerFlags12() is not byte forceFlags) return null;
            flags = forceFlags;
            if ((forceFlags & 6) == 2)
            {
                if (current.ReadWorldSelectedForce1656() is not byte selected) return null;
                worldGate = targetForceSlot == selected;
            }
        }
        if (current.ReadAttackerResultGate15F() is not byte gate) return null;
        bool resultGate = false;
        ushort? before = null;
        if (gate is >= 1 and <= 51)
        {
            if (current.ReadAttackerResultValue162() is not ushort value) return null;
            before = value;
            if (value < 60000)
            {
                if (current.ReadAttackerResultDefinition160() is not byte definition ||
                    current.ReadResultDefinitionIdentity(definition) is not int identity) return null;
                resultGate = targetForceSlot == identity && (cappedLoss > 0 || killed);
            }
        }
        if (!worldGate && !resultGate) return new(false, false, false, null, null, 0, revision);
        byte? flagsAfter = worldGate && killed ? (byte)(flags!.Value | 4) : null;
        if (!resultGate) return new(true, worldGate, false, flagsAfter, null, 0, revision);
        if (current.ReadCurrentParameters() is not { } parameters) return null;
        int delta = cappedLoss > 0 ? unchecked(parameters.TroopLossScale * cappedLoss) / 10 : 0;
        if (killed) delta = unchecked(delta + parameters.KilledAddend);
        int sum = unchecked(before!.Value + delta);
        ushort after = (ushort)Math.Clamp(sum, 0, 60000);
        return new(true, worldGate, true, flagsAfter, after, delta, revision);
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

    private sealed class OwnedCurrent(GameState state, int slot, SourceWorldContext definitions,
        INormalArmyForceResultDefinitionQueries registry, NormalArmyForceResultSourceParameters source)
        : INormalArmyForceResultCurrent
    {
        private NativeArmyForceState? Force => state.NativeArmySimulation?.Forces.GetValueOrDefault(slot);
        public bool? ReadAttackerForceValidity() => definitions.ReadForceNativeValidity(new(slot));
        public byte? ReadWorldFlags165C() => state.NativeArmySimulation?.World.Flags165C;
        public byte? ReadAttackerFlags12() => Force?.Flags12;
        public byte? ReadWorldSelectedForce1656() => state.NativeArmySimulation?.World.SelectedForce1656;
        public byte? ReadAttackerResultGate15F() => Force?.ResultGate15F;
        public ushort? ReadAttackerResultValue162() => Force?.ResultValue162;
        public byte? ReadAttackerResultDefinition160() => Force?.ResultDefinition160;
        public int? ReadResultDefinitionIdentity(byte physicalSlot) => registry.ReadResultDefinitionIdentity(physicalSlot);
        public NormalArmyForceResultParameters? ReadCurrentParameters() => ReadCurrent(state,source);
    }
}
