using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

/// <summary>Explicit new current scalar allocation for the paired ordinary
///2B0660 producer. Queries never read these image values to repair a save.</summary>
public sealed class OriginalArmyMobilityParameterSeed
{
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
        // Reuse the existing exact executable/literal verification. The seven
        // additional mutable fields are absent from the older battle installer.
        var shared = OriginalBattleRemakeCurrentParameterSeed.Load(repository, language);
        byte[] bytes = File.ReadAllBytes(shared.Source.PhysicalPath);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != shared.Source.Sha256)
            throw new InvalidDataException("Mobility source changed during pinned image reads.");
        using var pe = new PEReader(new MemoryStream(bytes, writable: false));
        var fields = new List<OriginalBattleRemakeCurrentParameterField>();
        foreach (int canonical in new[] { 0x1A4AB98, 0x1A49C40, 0x1A4A8F4, 0x1A49C58,
            0x1A49C64, 0x1A49C4C, 0x1A4C6EC })
        {
            int physical = canonical - (language == "TC" ? 0x1030 : 0);
            var raw = Read(physical, ".data");
            fields.Add(new(canonical, physical, BinaryPrimitives.ReadInt32LittleEndian(raw),
                Convert.ToHexString(SHA256.HashData(raw))));
        }
        var literal = Read(0x13B0914 - (language == "TC" ? 0x1000 : 0), ".rdata");
        float normalization = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(literal));
        if (normalization != 240f) throw new InvalidDataException("Pinned mobility Terrain normalization differs.");
        return new(language, shared.Source, fields.ToArray(), normalization, shared.PercentDenominator, shared.One);

        ReadOnlySpan<byte> Read(int rva, string expectedSection)
        {
            var matches = pe.PEHeaders.SectionHeaders.Where(s => s.Name == expectedSection &&
                rva >= s.VirtualAddress && (long)rva + 4 <= (long)s.VirtualAddress + s.SizeOfRawData).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Pinned mobility field is outside its source section.");
            long offset = (long)matches[0].PointerToRawData + rva - matches[0].VirtualAddress;
            if (offset < 0 || offset > bytes.Length - 4) throw new InvalidDataException("Pinned mobility field exceeds source bytes.");
            return bytes.AsSpan((int)offset, 4);
        }
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Units.Count != 0 || simulation.Parameters.Language != Language || simulation.Parameters.ExecutableSha256 != Source.Sha256 ||
            Fields.Any(f => simulation.Parameters.Int32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(f.SemanticScRva)) ||
                simulation.Parameters.Float32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(f.SemanticScRva))))
            throw new InvalidDataException("Fresh mobility scalars cannot replace a current owner or repair omitted saved values.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var field in Fields) values.Add(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva), field.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
}
