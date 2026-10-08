using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

public sealed record OriginalTacticRemakeParameterSeedField(int SemanticScRva,
    int PhysicalRva, int Value, string Sha256);

/// <summary>Source seed bytes and an explicit producer of this game's OWN
/// current parameter allocation. This is never a read of original process state.</summary>
public sealed class OriginalTacticRemakeParameterSeed
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalTacticRemakeParameterSeedField> Fields { get; }

    private OriginalTacticRemakeParameterSeed(string language, SourceProvenance source,
        OriginalTacticRemakeParameterSeedField[] fields)
    { Language = language; Source = source; Fields = Array.AsReadOnly(fields); }

    public static string Key(int semanticScRva) => $"sc-rva:{semanticScRva:X8}";

    //Source consumer identities: current waiting, activation, creation, scalar
    //effects and their current factors. TC .data fields are exactly SC-1030.
    private static readonly int[] Rvas =
    [
        0x1A4A080, 0x1A4A08C, 0x1A4A098, 0x1A4A0A4, 0x1A4A0B0,
        0x1A4A094, 0x1A4A04C, 0x1A4A058, 0x1A4A064,
        0x1A4AA64, 0x1A4AA70, 0x1A4AA08, 0x1A4AC30,
        0x1A4A9FC, 0x1A4AA14, 0x1A4AA20, 0x1A4AA2C, 0x1A4AAAC,
        0x1A4AAB4, 0x1A4AB14, 0x1A4AB1C, 0x1A4AB54,
        0x1A4AAF0, 0x1A4AAFC, 0x1A4AB08,
        0x1A4AB30, 0x1A4AB3C, 0x1A4AB48, 0x1A4ABEC,
        0x1A4ABB4, 0x1A4ABA8, 0x1A4AC18, 0x1A4ABC0,
        0x1A4ABCC, 0x1A4ABD8, 0x1A4ABE0, 0x1A4C4EC,
        0x1A4B79C, 0x1A4B7A8, 0x1A4AFBC, 0x1A49D4C,
        0x1A4BD44, 0x1A4BD54, 0x1A4BD60, 0x1A4BD6C, 0x1A4BD78, 0x1A4BD84, 0x1A4BD90
    ];

    public static OriginalTacticRemakeParameterSeed LoadPinnedSeed(OriginalGameRepository repository,
        string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string expected = language switch
        {
            "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Owned tactic parameter seed language is unsupported.")
        };
        string relative = $"SAN14CE_{language}.exe";
        string path = Path.GetFullPath(Path.Combine(repository.SourceDirectory, relative));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 4096 or > 128 * 1024 * 1024)
            throw new InvalidDataException("Owned tactic parameter source exceeds its bounded profile.");
        byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (hash != expected) throw new InvalidDataException("Owned tactic parameter seed executable differs.");
        using var pe = new PEReader(new MemoryStream(bytes, writable: false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Owned tactic parameter seed requires the pinned AMD64 PE32+ ABI.");
        var fields = new List<OriginalTacticRemakeParameterSeedField>();
        foreach (int rva in Rvas)
        {
            int physical = language == "TC" ? rva - 0x1030 : rva;
            var sections = pe.PEHeaders.SectionHeaders.Where(section => section.Name == ".data" &&
                physical >= section.VirtualAddress &&
                (long)physical + 4 <= (long)section.VirtualAddress + section.SizeOfRawData).ToArray();
            if (sections.Length != 1) throw new InvalidDataException("Owned parameter seed has no single raw .data field.");
            long offset = (long)sections[0].PointerToRawData + physical - sections[0].VirtualAddress;
            if (offset < 0 || offset > bytes.Length - 4) throw new InvalidDataException("Owned parameter source field exceeds the image.");
            ReadOnlySpan<byte> value = bytes.AsSpan((int)offset, 4);
            fields.Add(new(rva, physical, BinaryPrimitives.ReadInt32LittleEndian(value),
                Convert.ToHexString(SHA256.HashData(value))));
        }
        return new(language, new(relative, bytes.Length, hash, expected)
        { ResourceUri = GameResourceId.File(relative).ToString(), PhysicalPath = path }, fields.ToArray());
    }

    /// <summary>Explicit copy into a new current owner. Later writes occur in
    /// that owner's dictionaries and are not reset from source at query time.</summary>
    public NativeArmyParameterState AllocateOwnedCurrentParameters(string revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Owned parameter allocation revision is missing.");
        string fingerprint = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join(";", Fields.Select(field => $"{field.SemanticScRva:X8}={field.Value.ToString(CultureInfo.InvariantCulture)}")))));
        return new($"{revision}:source-seed:{fingerprint}", Language, Source.Sha256,
            Fields.ToDictionary(field => Key(field.SemanticScRva), field => field.Value), []);
    }

    /// <summary>Only a fresh, empty owned Army session can construct these
    /// Force/World/manager records. Save decode never calls this producer.</summary>
    public GameState CreateFreshOwnedSimulation(GameState state, string revision)
    {
        if (state.NativeArmySimulation is not null || state.NativeClock is not null || state.Units.Count != 0)
            throw new InvalidDataException("Fresh owned native simulation cannot replace a saved or populated instance.");
        GameState initialized = PlayableArmyCreation.InitializeClock(state, revision);
        return initialized with { NativeArmySimulation = new(AllocateOwnedCurrentParameters(revision),
            initialized.Forces.ToDictionary(pair => pair.Key, pair => new NativeArmyForceState(0)
            {
                Flags12 = 0, Flags194 = 0,
                RelationsEA = pair.Value.SourceIndexedBytesEA?.ToArray(),
                ActivePolicyBitmap1CB = new byte[10], SuppressedPolicyBitmap1D5 = new byte[10]
            }),
            new NativeArmyWorldState(0) { Flags165C = 0, PendingArmyIds = [], ObjectPairs = [] }) };
    }
}
