using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

/// <summary>Five mutable globals reached by2B09B0. An explicit fresh saved
/// allocation is separate from the immutable PE data and from Area caches.</summary>
public sealed class OriginalBattleRemakeCurrentRegionParameterSeed
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalBattleRemakeCurrentRegionParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) =
            (language, source, Array.AsReadOnly(fields));

    public static OriginalBattleRemakeCurrentRegionParameterSeed Load(OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string expected = language switch { "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Current region source locale is unsupported.") };
        string name = $"SAN14CE_{language}.exe", path = Path.GetFullPath(Path.Combine(repository.SourceDirectory, name));
        byte[] image = File.ReadAllBytes(path);
        if (image.Length is < 4096 or > 128 * 1024 * 1024 || Convert.ToHexString(SHA256.HashData(image)) != expected)
            throw new InvalidDataException("Current region source image differs from its pinned profile.");
        using var pe = new PEReader(new MemoryStream(image, false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Current region globals require the pinned AMD64 image.");
        var fields = new List<OriginalBattleRemakeCurrentParameterField>();
        foreach (int semantic in new[] { 0x1A4C6C8, 0x1A49D10, 0x1A49D34, 0x1A49D1C, 0x1A49D28 })
        {
            int rva = semantic - (language == "TC" ? 0x1030 : 0);
            var sections = pe.PEHeaders.SectionHeaders.Where(section => section.Name == ".data" &&
                rva >= section.VirtualAddress && (long)rva + 4 <= (long)section.VirtualAddress + section.SizeOfRawData).ToArray();
            if (sections.Length != 1) throw new InvalidDataException("Current region global has no unique raw data backing.");
            int offset = checked(sections[0].PointerToRawData + rva - sections[0].VirtualAddress);
            if (offset < 0 || offset > image.Length - 4) throw new InvalidDataException("Current region global exceeds image bounds.");
            ReadOnlySpan<byte> raw = image.AsSpan(offset, 4);
            fields.Add(new(semantic, rva, BinaryPrimitives.ReadInt32LittleEndian(raw), Convert.ToHexString(SHA256.HashData(raw))));
        }
        return new(language, new(name, image.Length, expected, expected)
            { ResourceUri = GameResourceId.File(name).ToString(), PhysicalPath = path }, fields.ToArray());
    }

    public GameState InstallFresh(GameState state, string revision)
    {
        if (state.NativeArmySimulation is not { Parameters: { } current } simulation ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } clock || clock.AnchorDate != state.Date ||
            state.Units.Count != 0 || current.Language != Language || current.ExecutableSha256 != Source.Sha256 ||
            string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(current.Revision))
            throw new InvalidDataException("Current region globals require an empty fresh matching owner.");
        foreach (var field in Fields)
            if (current.Int32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva)) ||
                current.Float32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva)))
                throw new InvalidDataException("Current region seed cannot replace an existing or partial saved allocation.");
        var values = new Dictionary<string, int>(current.Int32Values, current.Int32Values.Comparer);
        foreach (var field in Fields) values.Add(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva), field.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = current with {
            Revision = $"{current.Revision}|{revision}:region-2B09B0", Int32Values = values,
            Float32Values = new(current.Float32Values, current.Float32Values.Comparer) } } };
    }
}
