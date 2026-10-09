using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyContainerDeclaredMaterialGroup(uint GroupHash, uint MaterialObjectId);
public sealed record ArmyContainerDrawMaterialGroup(int SourceRawDrawIndex, uint RawDrawMaterialIndex12,
    uint ModelGroupHash, uint? DeclaredContainerGroupOrdinal, uint? DeclaredDefaultMaterialObjectId, ushort NativeGroupMode6);

/// <summary>Load-time WorldPQ and G1M group mapping, before current container
/// material assignments. These declarations do not certify that DA4AA0 or a
/// controller ran, nor that a default material reached prepared Primitive18.</summary>
public sealed class ArmyContainerMaterialGroupSource
{
    private readonly IReadOnlyDictionary<int, ArmyContainerDrawMaterialGroup> draws;
    private ArmyContainerMaterialGroupSource(LoadedArmyCatalog catalog, CeArmyModelCategory category,
        LoadedRdbAsset mapping, IReadOnlyList<ArmyContainerDeclaredMaterialGroup> groups,
        IReadOnlyList<ArmyContainerDrawMaterialGroup> rows)
    {
        Catalog = catalog; Category = category; GroupMappingSource = mapping; DeclaredGroups = groups;
        Draws = rows; draws = rows.ToDictionary(r => r.SourceRawDrawIndex);
    }
    public LoadedArmyCatalog Catalog { get; }
    public CeArmyModelCategory Category { get; }
    public LoadedRdbAsset GroupMappingSource { get; }
    public IReadOnlyList<ArmyContainerDeclaredMaterialGroup> DeclaredGroups { get; }
    public IReadOnlyList<ArmyContainerDrawMaterialGroup> Draws { get; }
    public string SourceEvidence => Catalog.Catalog.Source.ResourceUri + "@" + Catalog.Catalog.Source.DecodedSha256 +
        ";" + GroupMappingSource.ResourceUri + "@" + GroupMappingSource.DecodedSha256 +
        ";F50A00/F54700/C19860-original10008.RawIndex12→Model110;DA6C70-ord51/52-or34;DA4AA0-groupHash-match";

    public static ArmyContainerMaterialGroupSource Load(GameResourceRepository repository,
        LoadedArmyCatalog actualCatalog, int category, G1ModelGeometry actualGeometry)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualCatalog);
        ArgumentNullException.ThrowIfNull(actualGeometry);
        if (category is not (0 or 1 or 2 or 3)) throw new NotSupportedException("Army material grouping covers the source ordinary/linked flag categories0..3.");
        var known = CeArmyModels.KnownSourceCategories[category];
        var definition = actualCatalog.Definitions.Single(d => d.Definition.ObjectId == known.DefinitionId);
        if (definition.ModelObject.ObjectId != known.ModelObjectId)
            throw new InvalidDataException("Army group mapping belongs to another declared source model.");
        uint U(DokObject obj, uint hash)
        {
            var p = obj.Properties.Single(p => p.NameHash == hash);
            if (p.TypeCode != 5 || p.ElementCount != 1 || p.RawValue.Length != 4)
                throw new InvalidDataException("Army source group mapping reference is not its native DWORD.");
            return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
        }
        uint[] Words(int index, uint hash)
        {
            var p = definition.Definition.Properties[index];
            if (p.Index != index || p.NameHash != hash || p.TypeCode != 5 || p.ElementSize != 4 || p.RawValue.Length != checked(p.ElementCount * 4))
                throw new InvalidDataException("Army WorldPQ material group field is not its actual complete DWORD array.");
            return p.RawValue.Chunk(4).Select(v => BinaryPrimitives.ReadUInt32LittleEndian(v)).ToArray();
        }
        uint[] hashes = Words(51, 0xCEE5DDAE), materials = Words(52, 0x5EDA237E), fallback = Words(34, 0x34CF9E5C);
        var declared = new List<ArmyContainerDeclaredMaterialGroup>();
        if (hashes.Length != materials.Length)
            throw new NotSupportedException("Army material group parallel arrays need their actual failed-loader/fallback receipt.");
        if (hashes.Length != 0)
            for (int i = 0; i < hashes.Length; i++) declared.Add(new(hashes[i], materials[i]));
        else
        {
            if (fallback.Length % 2 != 0) throw new InvalidDataException("Army fallback material group pairs are incomplete.");
            for (int i = 0; i < fallback.Length; i += 2) declared.Add(new(fallback[i], fallback[i+1]));
        }
        var mapping = repository.LoadRdbAsset("CharacterEditor", U(definition.ModelObject, 0x7F0DE9A3));
        var members = ReadSourceMembers(mapping.Bytes);
        var geometryDraws = G1ModelDrawReader.Read(actualGeometry);
        var rows = new List<ArmyContainerDrawMaterialGroup>();
        foreach (var draw in geometryDraws.Draws)
        {
            if (draw.RawIndex12 > byte.MaxValue)
                throw new NotSupportedException("Army native group mapping needs the reached wider/truncated raw material-index branch.");
            // Supported actual Army tables have a positive match for each
            // raw draw. Unknown unmatched native behavior is not filled zero.
            var match = members.FirstOrDefault(g => g.Members.Contains((byte)draw.RawIndex12));
            if (match.Members is null) throw new NotSupportedException("Army raw draw has no proven material-group member match.");
            uint hash = match.Hash;
            int ordinal = declared.FindIndex(g => g.GroupHash == hash);
            var rawGroup = geometryDraws.Groups.Single(g => g.DrawIndices.Contains(draw.Index));
            rows.Add(new(draw.Index, draw.RawIndex12, hash, ordinal >= 0 ? (uint)ordinal : null,
                ordinal >= 0 ? declared[ordinal].MaterialObjectId : null,
                BinaryPrimitives.ReadUInt16LittleEndian(rawGroup.RawBytes.AsSpan(16, 2))));
        }
        return new(actualCatalog, known, mapping, declared.AsReadOnly(), rows.AsReadOnly());
    }
    public ArmyContainerDrawMaterialGroup ReadDeclaredDraw(int actualCanonicalRawDrawIndex)
        => draws.TryGetValue(actualCanonicalRawDrawIndex, out var row) ? row :
            throw new InvalidDataException("Army source group lookup has no such original raw draw.");

    private static IReadOnlyList<(uint Hash, byte[] Members)> ReadSourceMembers(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16) throw new InvalidDataException("Army material group table header is incomplete.");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        if (count > byte.MaxValue || count > (bytes.Length - 16) / 8)
            throw new NotSupportedException("Army material group table needs the reached native larger-count branch.");
        int at = 16;
        var result = new List<(uint, byte[])>();
        for (int i = 0; i < count; i++)
        {
            if (bytes.Length - at < 8) throw new InvalidDataException("Army material group row is truncated.");
            uint hash = BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+4)..]); at += 8;
            if (size > byte.MaxValue || size > (bytes.Length - at) / 4)
                throw new NotSupportedException("Army material group members need the reached native larger-count branch.");
            var values = new byte[size];
            for (int j = 0; j < size; j++, at += 4)
            {
                uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
                if (value > byte.MaxValue) throw new NotSupportedException("Army material member needs its actual native byte truncation branch.");
                values[j] = (byte)value;
            }
            result.Add((hash, values));
        }
        if (at != bytes.Length) throw new InvalidDataException("Army material group table has trailing unindexed source bytes.");
        return result.AsReadOnly();
    }
}
