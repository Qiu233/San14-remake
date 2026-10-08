using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

/// <summary>Fresh CE2318F0 scalar allocation. Current path queries read the
/// saved parameter bank; they do not reopen the PE.</summary>
public sealed class OriginalPersonReturnPathParameterSeed
{
    public const string VersionKey = "ce-person-return-path-current:version";
    public static readonly int[] TcRvas = [0x1A49BAC, 0x1A4A9D0, 0x1A49570];
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalPersonReturnPathParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static OriginalPersonReturnPathParameterSeed Load(OriginalGameRepository repository, string language)
    {
        string expected = language switch {
            "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Person return path requires the SC or TC CE profile.") };
        string relative = $"SAN14CE_{language}.exe", path = Path.Combine(repository.SourceDirectory, relative);
        byte[] bytes = File.ReadAllBytes(path);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != expected)
            throw new InvalidDataException("Person return globals require the pinned CE executable.");
        using var pe = new PEReader(new MemoryStream(bytes, false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Person return path PE has another ABI.");
        var fields = TcRvas.Select(tc => {
            int semantic = tc + 0x1030, rva = language == "SC" ? semantic : tc;
            var section = pe.PEHeaders.SectionHeaders.Single(s => s.Name == ".data" && rva >= s.VirtualAddress &&
                (long)rva + 4 <= (long)s.VirtualAddress + s.SizeOfRawData);
            ReadOnlySpan<byte> raw = bytes.AsSpan(section.PointerToRawData + rva - section.VirtualAddress, 4);
            return new OriginalBattleRemakeCurrentParameterField(semantic, rva,
                BinaryPrimitives.ReadInt32LittleEndian(raw), Convert.ToHexString(SHA256.HashData(raw)));
        }).ToArray();
        return new(language, new(relative, bytes.LongLength, expected, expected) {
            PhysicalPath = path, ResourceUri = GameResourceId.File(relative).ToString() }, fields);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.ExecutableSha256 != Source.Sha256 || simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Fresh person return parameters cannot replace a current saved allocation.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var field in Fields) {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (simulation.Parameters.Float32Values.ContainsKey(key) || values.TryGetValue(key, out int old) && old != field.Value)
                throw new InvalidDataException("Person return parameter conflicts with an installed source field.");
            values.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static int? ReadCurrentTc(GameState state, int tcRva) =>
        state.NativeArmySimulation?.Parameters is { } owner && owner.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        owner.Int32Values.TryGetValue(OriginalTacticRemakeParameterSeed.Key(tcRva + 0x1030), out int value) ? value : null;
}
