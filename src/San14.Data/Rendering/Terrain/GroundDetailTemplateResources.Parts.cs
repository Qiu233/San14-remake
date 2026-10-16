using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GroundDetailTemplateResources
{
    private sealed record PartsRow(uint NameHash, uint[] PrimitiveCounts,
        uint[] GroupStarts, uint[] GroupCounts);

    private static IReadOnlyList<IReadOnlyList<int>> ReadLods(GameResourceRepository repository,
        MapStaticModelAsset asset, Dictionary<uint, PartsRow[]> cache)
    {
        var geometry = asset.RawTransforms.Geometry;
        var directory = G1ModelDrawReader.Read(geometry);
        var part = geometry.Sections.Single(s => s.Id == 0x10009);
        if (part.ItemCount != 1 || part.RawBytes.Length < 48 ||
            BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(16)) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(28)) != 0)
            throw new NotSupportedException("Ground Parts requires one ordinary classification-zero source part.");
        uint file = Scalar(asset.TextureResources.ModelObject, 0x3BBFD9A5);
        uint containerId = Scalar(asset.TextureResources.ModelObject, 0x78C06C23);
        if (file == 0 || containerId == 0)
            throw new NotSupportedException("Ground template requires its declared Parts binder and LOD container.");
        if (!cache.TryGetValue(file, out var binder))
        {
            binder = ReadPartsBinder(repository.LoadRdbAsset("FieldEditor4", file).Bytes);
            cache.Add(file, binder);
        }
        var objects = asset.TextureResources.Dictionary.Document.Objects;
        var container = objects.Single(o => o.ObjectId == containerId && o.Kind == DokObjectKind.Inline && o.ClassId == 0x6C01215E);
        if (container.Properties.Count < 3)
            throw new InvalidDataException("Ground Parts container has no complete Hi/Mid/Low directory.");
        var lods = new IReadOnlyList<int>[3];
        for (int lod = 0; lod < lods.Length; lod++)
        {
            uint setId = Scalar(container.Properties[lod]);
            uint[] names = [];
            if (setId != 0)
            {
                var set = objects.Single(o => o.ObjectId == setId && o.Kind == DokObjectKind.Inline && o.ClassId == 0xA880E514);
                var property = set.Properties.Single(p => p.NameHash == 0x59207B00);
                if (property.TypeCode != 5 || property.RawValue.Length != property.ElementCount * 4)
                    throw new InvalidDataException("Ground PartsSet names require a complete uint directory.");
                names = Enumerable.Range(0, property.ElementCount).Select(i =>
                    BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue.AsSpan(i * 4, 4))).ToArray();
            }
            var selected = names.Select(name => binder.Single(row => row.NameHash == name)).ToArray();
            if (selected.Any(row => row.GroupCounts[1] != 0 || row.GroupCounts[2] != 0))
                throw new NotSupportedException("Ground Parts category one/two requires a separate primitive directory.");
            var groups = selected.SelectMany(row => Enumerable.Range(checked((int)row.GroupStarts[0]),
                checked((int)row.GroupCounts[0]))).ToArray();
            if (groups.Any(group => (uint)group >= directory.Groups.Count))
                throw new InvalidDataException("Ground Parts group exceeds the actual model directory.");
            int[] draws = groups.SelectMany(group => directory.Groups[group].DrawIndices).ToArray();
            if (selected.Sum(row => (long)row.PrimitiveCounts[0]) != draws.Length ||
                draws.Any(draw => (uint)draw >= asset.Draws.Count))
                throw new InvalidDataException("Ground Parts primitive count differs from its source groups.");
            lods[lod] = Array.AsReadOnly(draws);
        }
        return Array.AsReadOnly(lods);
    }

    // The binder resource consists of packed 32-byte model-group records.
    // Native category counts and cumulative group offsets use ten-bit fields.
    private static PartsRow[] ReadPartsBinder(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length % 32 != 0)
            throw new InvalidDataException("Ground Parts binder requires complete 32-byte records.");
        var rows = new PartsRow[bytes.Length / 32];
        uint[] cumulative = [0, 0, 0];
        for (int index = 0; index < rows.Length; index++)
        {
            var raw = bytes.Slice(index * 32, 32);
            uint primitives = unchecked(((Word(raw, 16) << 10) | Word(raw, 12)) << 10) | Word(raw, 8);
            uint previous = Pack(cumulative);
            for (int category = 0; category < 3; category++)
                cumulative[category] = unchecked(cumulative[category] + Word(raw, 20 + category * 4));
            uint[] starts = Unpack(previous), ends = Unpack(Pack(cumulative));
            rows[index] = new(Word(raw, 0), Unpack(primitives), starts,
                [unchecked(ends[0] - starts[0]), unchecked(ends[1] - starts[1]), unchecked(ends[2] - starts[2])]);
        }
        return rows;
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static uint Pack(uint[] values) => unchecked(((values[2] << 10) | values[1]) << 10) | values[0];
    private static uint[] Unpack(uint packed) => [packed & 0x3FF, (packed >> 10) & 0x3FF, (packed >> 20) & 0x3FF];
    private static uint Scalar(DokObject source, uint hash) => Scalar(source.Properties.Single(p => p.NameHash == hash));
    private static uint Scalar(DokProperty property) => property.TypeCode == 5 && property.ElementCount == 1 && property.RawValue.Length == 4
        ? BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue)
        : throw new InvalidDataException("Ground template declaration requires a scalar uint field.");
}
