using System.Buffers.Binary;
using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Immutable CE94 source row and the inspected fixed-loader field projection.
/// Native offsets are structural identities, not effect meanings or runtime formula results.</summary>
public sealed class TacticSourceRow
{
    private readonly byte[] _raw;
    private readonly IReadOnlyDictionary<int, byte> _fixedBytes;
    public int Slot { get; }
    public string Name { get; }
    public uint FormationMask { get; }
    public IReadOnlyList<int> FormationSlots { get; }
    public string DecodedSha256 { get; }
    public SourceProvenance Source { get; }
    public byte[] RawBytes => _raw.ToArray();

    internal TacticSourceRow(S14TacticDefinition row, string decodedSha256,
        SourceProvenance source)
    {
        Slot = row.Slot; Name = row.Name; FormationMask = row.FormationMask;
        FormationSlots = Array.AsReadOnly(row.FormationSlots.ToArray());
        DecodedSha256 = decodedSha256; Source = source; _raw = row.RawBytes.ToArray();
        var bytes = new Dictionary<int, byte>();
        foreach ((int input, int target, int size) in TacticSourceParameters.CeFixedFields)
            for (int index = 0; index < size; index++) bytes[target + index] = _raw[input + index];
        _fixedBytes = new ReadOnlyDictionary<int, byte>(bytes);
    }

    public byte ReadNativeFixedByte(int offset) => _fixedBytes.TryGetValue(offset, out byte value)
        ? value : throw new InvalidDataException($"CE tactic field +{offset:X} has no inspected fixed-loader source.");

    public ushort ReadNativeFixedUInt16(int offset) => (ushort)(ReadNativeFixedByte(offset) |
        (ReadNativeFixedByte(checked(offset + 1)) << 8));

    public uint ReadNativeFixedUInt32(int offset) => ReadNativeFixedByte(offset) |
        ((uint)ReadNativeFixedByte(checked(offset + 1)) << 8) |
        ((uint)ReadNativeFixedByte(checked(offset + 2)) << 16) |
        ((uint)ReadNativeFixedByte(checked(offset + 3)) << 24);
}

public sealed record TacticSourceCatalog(string DecodedSha256, SourceProvenance Source,
    IReadOnlyList<TacticSourceRow> Rows);

/// <summary>Source/override-aware fixed tactic input. Section13 is not decoded as a tactic effect dispatch table.</summary>
public static class TacticSourceParameters
{
    // Same inspected CE fixed loader projection as OriginalEventInitializationContext.SkillDefinitions.
    internal static readonly (int Source, int Native, int Bytes)[] CeFixedFields =
    [
        (0,0x10,10), (10,0x1A,38), (48,0x40,1), (49,0x42,2),
        (51,0x44,1), (52,0x46,18), (70,0x58,10), (80,0x62,2),
        (82,0x64,2), (84,0x66,2), (86,0x68,2), (88,0x6A,1),
        (89,0x6C,4), (93,0x88,1)
    ];

    public static TacticSourceCatalog Load(GameResourceRepository repository, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(repository);
        LoadedSkillDefinitions loaded = repository.LoadSkillDefinitions(relativePath);
        return Read(loaded.Formations, loaded.Source);
    }

    public static TacticSourceCatalog Read(S14FormationCatalog catalog, SourceProvenance source)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(source);
        if (catalog.FormatMarker != 0x66 || catalog.TacticRecordOffset != 188569 ||
            catalog.TacticRecordStride != 94 || catalog.Tactics.Count != 201 ||
            !Sha256(catalog.DecodedSha256) || !Sha256(source.Sha256) ||
            string.IsNullOrWhiteSpace(source.ResourceUri))
            throw new InvalidDataException("CE tactic source profile, hash or provenance differs.");
        var rows = new List<TacticSourceRow>(catalog.Tactics.Count);
        foreach (S14TacticDefinition row in catalog.Tactics)
        {
            if (row.Slot != rows.Count || row.RawBytes is not { Length: 94 } ||
                row.RawParameterBytes is not { Length: 46 } ||
                !row.RawBytes.AsSpan(48).SequenceEqual(row.RawParameterBytes) ||
                BinaryPrimitives.ReadUInt32LittleEndian(row.RawBytes.AsSpan(89, 4)) != row.FormationMask ||
                row.FormationMask >> 21 != 0 || !row.FormationSlots.SequenceEqual(
                    Enumerable.Range(0, 21).Where(slot => (row.FormationMask & (1u << slot)) != 0)))
                throw new InvalidDataException("CE tactic source row or formation reference contradicts its bytes.");
            rows.Add(new(row, catalog.DecodedSha256, source));
        }
        return new(catalog.DecodedSha256, source, rows.AsReadOnly());
    }

    private static bool Sha256(string hash) => hash.Length == 64 && hash.All(Uri.IsHexDigit);
}
