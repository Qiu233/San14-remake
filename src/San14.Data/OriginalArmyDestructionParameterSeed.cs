using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

/// <summary>One-time inert CE data projection for259360,27D900 and27E870.
/// Values are installed into this campaign's saved mutable parameter owner;
/// ordinary destruction queries do not reopen or hash the executable.</summary>
public sealed class OriginalArmyDestructionParameterSeed
{
    public const string Revision = "san14:ce-owned-army-destruction-parameters-20261007";
    public const string VersionKey = "ce-army-destruction-current:version";
    public const int CaptureAdjustmentTcRva = 0x27D37AC;
    public const int CaptureAdjustmentScRva = 0x27D480C;
    public const int RelationFirstThresholdTcRva = 0x27D3834;
    public const int RelationFirstThresholdScRva = 0x27D4894;
    public const int LordReleaseRelationThresholdTcRva = 0x27D37D0;
    public const int LordReleaseRelationThresholdScRva = 0x27D4830;
    public const int LordExecuteRelationPenaltyTcRva = 0x27D37D4;
    public const int LordExecuteRelationPenaltyScRva = 0x27D4834;
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalArmyDestructionParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields) => (Language, Source, Fields) = (language, source, fields);

    public static OriginalArmyDestructionParameterSeed Load(OriginalGameRepository repository, string language)
    {
        string expected = language switch
        {
            "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Destruction parameter language is unsupported.")
        };
        string relative = $"SAN14CE_{language}.exe";
        string path = Path.GetFullPath(Path.Combine(repository.SourceDirectory, relative));
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length is < 4096 or > 128 * 1024 * 1024 || Convert.ToHexString(SHA256.HashData(bytes)) != expected)
            throw new InvalidDataException("Destruction parameters require their pinned CE executable.");
        using var pe = new PEReader(new MemoryStream(bytes, false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Destruction parameter source is not the inspected AMD64 PE.");
        var fields = new List<OriginalBattleRemakeCurrentParameterField>();
        foreach (int tcRva in DataTcRvas)
        {
            //This is the same proved CE .data mapping used by existing tactic,
            //damage, mobility and Base sources, independent of code addresses.
            int semantic = tcRva + 0x1030;
            int physical = language == "TC" ? tcRva : semantic;
            var section = pe.PEHeaders.SectionHeaders.Single(s => s.Name == ".data" && physical >= s.VirtualAddress &&
                (long)physical + 4 <= (long)s.VirtualAddress + s.SizeOfRawData);
            int offset = checked(section.PointerToRawData + physical - section.VirtualAddress);
            ReadOnlySpan<byte> raw = bytes.AsSpan(offset, 4);
            fields.Add(new(semantic, physical, BinaryPrimitives.ReadInt32LittleEndian(raw),
                Convert.ToHexString(SHA256.HashData(raw))));
        }
        foreach (var scalar in new[] { (Tc: CaptureAdjustmentTcRva, Sc: CaptureAdjustmentScRva),
            (Tc: RelationFirstThresholdTcRva, Sc: RelationFirstThresholdScRva),
            (Tc: LordReleaseRelationThresholdTcRva, Sc: LordReleaseRelationThresholdScRva),
            (Tc: LordExecuteRelationPenaltyTcRva, Sc: LordExecuteRelationPenaltyScRva) })
        {
            int physical = language == "TC" ? scalar.Tc : scalar.Sc;
            if (pe.PEHeaders.SectionHeaders.Count(s => physical >= (long)s.VirtualAddress + s.SizeOfRawData &&
                (long)physical + 4 <= (long)s.VirtualAddress + s.VirtualSize) != 1)
                throw new InvalidDataException("Destruction scalar is not the pinned PE loader zero-fill allocation.");
            //27E870 and2A6EE0 have reads but no reached source writer in the
            //typed image. Loader zero is installed only in a fresh allocation.
            fields.Add(new(scalar.Sc, physical, 0, Convert.ToHexString(SHA256.HashData(new byte[4]))));
        }
        return new(language, new(relative, bytes.LongLength, expected, expected)
            { ResourceUri = GameResourceId.File(relative).ToString(), PhysicalPath = path }, fields.ToArray());
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.ExecutableSha256 != Source.Sha256 || simulation.Parameters.Int32Values.ContainsKey(VersionKey))
            throw new InvalidDataException("Fresh destruction parameters cannot replace a saved current allocation.");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var field in Fields)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (simulation.Parameters.Float32Values.ContainsKey(key) || integers.TryGetValue(key, out int existing) && existing != field.Value)
                throw new InvalidDataException("Destruction parameter disagrees with an already installed source field.");
            integers.TryAdd(key, field.Value);
        }
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }

    public static int? ReadCurrentTc(GameState state, int tcRva) =>
        state.NativeArmySimulation?.Parameters is { } owner && owner.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        owner.Int32Values.TryGetValue(OriginalTacticRemakeParameterSeed.Key(tcRva switch {
            CaptureAdjustmentTcRva => CaptureAdjustmentScRva,
            RelationFirstThresholdTcRva => RelationFirstThresholdScRva,
            LordReleaseRelationThresholdTcRva => LordReleaseRelationThresholdScRva,
            LordExecuteRelationPenaltyTcRva => LordExecuteRelationPenaltyScRva,
            _ => tcRva + 0x1030 }), out int value) ? value : null;

    //Actual reached int32 fields. No strings, vtables or presentation constants
    //are inferred as mutable mechanics parameters.
    private static readonly int[] DataTcRvas =
    [
        0x1A49FB8, 0x1A49060, 0x1A490B4, 0x1A49BB8, 0x1A49794,
        0x1A4B220, 0x1A4B22C, 0x1A4B234, 0x1A4B248, 0x1A4B254, 0x1A4B25C,
        0x1A4B268, 0x1A4B274, 0x1A4B280, 0x1A4B28C, 0x1A4B298, 0x1A4B2A4, 0x1A4B2B0,
        0x1A4B480, 0x1A4B48C, 0x1A4B498, 0x1A4B4A4,
        0x1A4B4F0, 0x1A4B4FC, 0x1A4B508, 0x1A4B514, 0x1A4B520, 0x1A4B528,
        0x1A4B530, 0x1A4B53C, 0x1A4B548, 0x1A4B554, 0x1A4B560, 0x1A4B56C,
        0x1A4B578, 0x1A4B584, 0x1A4B590, 0x1A4B5A8,
        0x1A4B5B4, 0x1A4B5C0, 0x1A4B5CC, 0x1A4B5D8,
        0x1A4B5E4, 0x1A4B5F0, 0x1A4B5FC, 0x1A4B608,
        0x1A4A81C, 0x1A4A824, 0x1A4A830, 0x1A4A83C, 0x1A4A848, 0x1A4A854, 0x1A4A860,
        0x1A4ABB0, 0x1A4AC5C, 0x1A498EC, 0x1A498A4, 0x1A49180, 0x1A4918C,
        0x1A49914, 0x1A4906C, 0x1A49078, 0x1A49084,
        0x1A4ACF4, 0x1A4ACFC, 0x1A4AD08,
        0x1A4AD44, 0x1A4ADB8, 0x1A4947C, 0x1A4AA28, 0x1A4AA34, 0x1A4B14C, 0x1A4B1C4, 0x1A4B074, 0x1A49C28
    ];
}
