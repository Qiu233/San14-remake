using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using San14.Core;

namespace San14.Data;

public sealed record OriginalNormalArmyForceResultParameters(int TroopLossScale, int KilledAddend);
public sealed record OriginalNormalArmyForceResultSourceField(int SemanticScRva, int PhysicalRva,
    int Value, string Sha256);
public sealed class OriginalNormalArmyForceResultSourceParameters
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public OriginalBattleUnitSourceProfile SourceCodeProfile { get; }
    public IReadOnlyList<OriginalNormalArmyForceResultSourceField> Fields { get; }
    public OriginalNormalArmyForceResultParameters Values { get; }
    public string? CurrentRevision { get; }
    internal OriginalNormalArmyForceResultSourceParameters(string language, SourceProvenance source,
        OriginalBattleUnitSourceProfile profile, IReadOnlyList<OriginalNormalArmyForceResultSourceField> fields,
        OriginalNormalArmyForceResultParameters values, string? revision)
    { Language=language;Source=source;SourceCodeProfile=profile;Fields=fields;Values=values;CurrentRevision=revision; }
}

/// <summary>Reached current native reads inside ordinary kind27/null-second-Force
/// SC257420. Definition identity is the actual registry object's virtual B0 result,
/// not a Core force ID. Missing values stop only if their branch is reached.</summary>
public interface IOriginalNormalArmyForceResultCurrent
{
    bool? ReadAttackerForceValidity();
    byte? ReadWorldFlags165C();
    byte? ReadAttackerFlags12();
    byte? ReadWorldSelectedForce1656();
    byte? ReadAttackerResultGate15F();
    ushort? ReadAttackerResultValue162();
    byte? ReadAttackerResultDefinition160();
    int? ReadResultDefinitionIdentity(byte physicalSlot);
    OriginalNormalArmyForceResultParameters? ReadCurrentParameters();
}

public sealed record OriginalNormalArmyForceResultReceipt(bool NativeReturn,
    bool WorldGate, bool ResultGate, byte? Flags12After, ushort? Value162After,
    int Delta162, string Revision);
public enum OriginalNormalArmyForceResultOutcome { Committed, ResolvedWithoutWrite, CurrentInputsUnresolved }
public sealed record OriginalNormalArmyForceResultApplication(OriginalNormalArmyForceResultOutcome Outcome,
    GameState State, OriginalNormalArmyForceResultReceipt? Receipt);
public interface IOriginalNormalArmyForceResultDefinitionQueries
{
    int? ReadResultDefinitionIdentity(byte physicalSlot);
}

/// <summary>Complete ordinary Army Force callback only. The caller passes a
/// resolved null second Force pointer, target raw Force slot, capped loss and
/// killed flag. Death/trait/report callbacks in the enclosing application are
/// distinct; this method does not publish an enclosing battle result.</summary>
public static class OriginalNormalArmyForceResultSource
{
    /// <summary>Writes only reached Force12/162 after every current input has
    /// resolved. Missing owners preserve the entire input. Native validity uses
    /// the matching current source definition snapshot; no UI alive inference.</summary>
    public static OriginalNormalArmyForceResultApplication TryApply(GameState input, int attackerForceSlot,
        int targetForceSlot, int cappedLoss, bool killed, OriginalEventInitializationContext definitions,
        IOriginalNormalArmyForceResultDefinitionQueries registry,
        OriginalNormalArmyForceResultSourceParameters source, string revision)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        if (attackerForceSlot is < 0 or > 51) throw new InvalidDataException("Invalid attacking native Force slot.");
        if (input.NativeArmySimulation is not { Parameters: { } parameters } simulation ||
            parameters.Language != source.Language || parameters.ExecutableSha256 != source.Source.Sha256 ||
            definitions.StaticSource is not { } scenario ||
            input.ContentHashes.GetValueOrDefault(scenario.ResourceUri) != scenario.Sha256)
            return new(OriginalNormalArmyForceResultOutcome.CurrentInputsUnresolved,input,null);
        var receipt = Resolve(new OwnedCurrent(input,attackerForceSlot,definitions,registry,source),
            targetForceSlot,cappedLoss,killed,$"{revision}:{parameters.Revision}:{scenario.Sha256}");
        if (receipt is null) return new(OriginalNormalArmyForceResultOutcome.CurrentInputsUnresolved,input,null);
        if (receipt.Flags12After is null && receipt.Value162After is null)
            return new(OriginalNormalArmyForceResultOutcome.ResolvedWithoutWrite,input,receipt);
        if (!simulation.Forces.ContainsKey(attackerForceSlot))
            return new(OriginalNormalArmyForceResultOutcome.CurrentInputsUnresolved,input,null);
        var detached = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(input))
            ?? throw new InvalidDataException("Force result current state failed to detach.");
        var owner = detached.NativeArmySimulation!;
        var before = owner.Forces[attackerForceSlot];
        var forces = new Dictionary<int,NativeArmyForceState>(owner.Forces)
        { [attackerForceSlot] = before with { Flags12 = receipt.Flags12After ?? before.Flags12,
            ResultValue162 = receipt.Value162After ?? before.ResultValue162 } };
        GameState result = detached with { NativeArmySimulation = owner with { Forces = forces } };
        NativeSubdayStateValidation.Validate(result);
        return new(OriginalNormalArmyForceResultOutcome.Committed,result,receipt);
    }

    public static OriginalNormalArmyForceResultReceipt? Resolve(IOriginalNormalArmyForceResultCurrent current,
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

    /// <summary>Only the two parameters reached by ordinary kind27 with null
    /// second Force are seeded. Unreached ruler/rank and Base-kind constants
    /// are retained in the complete source window, not imported as guesses.</summary>
    public static OriginalNormalArmyForceResultSourceParameters LoadInitial(OriginalGameRepository repository,
        string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var profile = ForLanguage(language);
        string relative = $"SAN14CE_{language}.exe";
        string path = Path.GetFullPath(Path.Combine(repository.SourceDirectory, relative));
        using var stream = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if (stream.Length is < 4096 or > 128*1024*1024)
            throw new InvalidDataException("Force result executable exceeds its inspected bound.");
        byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (hash != profile.ExecutableSha256) throw new InvalidDataException("Force result image identity changed.");
        using var pe = new PEReader(new MemoryStream(bytes, writable: false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Force result requires the inspected AMD64 PE32+ source.");
        var fields = new List<OriginalNormalArmyForceResultSourceField>();
        foreach (var (semantic, expected) in new[] { (0x1A4A920, 10), (0x1A4A92C, 100) })
        {
            int physical = semantic - (language == "TC" ? 0x1030 : 0);
            var sections = pe.PEHeaders.SectionHeaders.Where(s => s.Name == ".data" &&
                physical >= s.VirtualAddress && (long)physical + 4 <= (long)s.VirtualAddress + s.SizeOfRawData).ToArray();
            if (sections.Length != 1) throw new InvalidDataException("Force result parameter is not source backed.");
            int offset = sections[0].PointerToRawData + physical - sections[0].VirtualAddress;
            ReadOnlySpan<byte> raw = bytes.AsSpan(offset, 4);
            int value = BinaryPrimitives.ReadInt32LittleEndian(raw);
            if (value != expected) throw new InvalidDataException("Force result parameter initial value changed.");
            fields.Add(new(semantic, physical, value, Convert.ToHexString(SHA256.HashData(raw))));
        }
        var provenance = new SourceProvenance(relative, bytes.Length, hash, profile.ExecutableSha256)
        { ResourceUri = GameResourceId.File(relative).ToString(), PhysicalPath = path, IsOverride = false };
        return new(language, provenance, profile, Array.AsReadOnly(fields.ToArray()), new(fields[0].Value, fields[1].Value), null);
    }

    public static GameState InstallFresh(GameState state, OriginalNormalArmyForceResultSourceParameters source, string revision)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        if (source.CurrentRevision is not null || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Units.Count != 0 || state.NativeArmySimulation is not { Parameters: { } parameters } simulation ||
            parameters.Language != source.Language || parameters.ExecutableSha256 != source.Source.Sha256)
            throw new InvalidDataException("Force result seed needs a fresh matching empty simulation.");
        var integers = new Dictionary<string,int>(parameters.Int32Values, parameters.Int32Values.Comparer);
        foreach (var field in source.Fields)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (integers.Keys.Concat(parameters.Float32Values.Keys).Any(k => string.Equals(k,key,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Force result seed cannot overwrite a current parameter.");
            integers.Add(key,field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = parameters with
        { Int32Values = integers, Float32Values = new(parameters.Float32Values,parameters.Float32Values.Comparer),
          Revision = $"{parameters.Revision}|{revision}:normal-force-result:{source.Source.Sha256}" } } };
    }

    public static OriginalNormalArmyForceResultParameters? ReadCurrent(GameState state,
        OriginalNormalArmyForceResultSourceParameters source)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != source.Language ||
            parameters.ExecutableSha256 != source.Source.Sha256 || string.IsNullOrWhiteSpace(parameters.Revision)) return null;
        int[] values = new int[2];
        for (int i = 0; i < 2; i++)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(source.Fields[i].SemanticScRva);
            if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key,out values[i])) return null;
        }
        return new(values[0],values[1]);
    }

    public static OriginalBattleUnitSourceProfile ForLanguage(string language) => language switch
    {
        "SC" => new("SC", "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6", Array.AsReadOnly<OriginalBattleUnitSourceWindow>([
            new("forceResult",0x257420,0x2576CA,"2E4CCAF4794B091976337024A8B35BB00396E1A0DC3FA3C0A4B32A1436393067"),
            new("ordinaryCaller",0x1791E0,0x17921B,"75CE61CB114DA3425A45A980737F5E5AE2FB75B860D0800636692EA04FE84176")])),
        "TC" => new("TC", "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766", Array.AsReadOnly<OriginalBattleUnitSourceWindow>([
            new("forceResult",0x257670,0x25791A,"D0AA220CC0999EB5F641CC1299F8708BC420158E78B2AD83C2A600519C6036E7"),
            new("ordinaryCaller",0x179480,0x1794BB,"AE41B2BD1E0E3209849BD998280794AC2EE8DDB62E01D72BEB15F3C75FDC5FD2")])),
        _ => throw new InvalidDataException("Unsupported Force result source locale.")
    };

    private sealed class OwnedCurrent(GameState state, int slot, OriginalEventInitializationContext definitions,
        IOriginalNormalArmyForceResultDefinitionQueries registry, OriginalNormalArmyForceResultSourceParameters source)
        : IOriginalNormalArmyForceResultCurrent
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
        public OriginalNormalArmyForceResultParameters? ReadCurrentParameters() => ReadCurrent(state,source);
    }
}
