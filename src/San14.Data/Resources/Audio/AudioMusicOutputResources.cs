using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

public sealed record AudioMusicRoute(uint OutputKey, float Gain, uint Kind);
public sealed record AudioMusicClassification(uint Id, IReadOnlyList<AudioMusicRoute> Routes,
    IReadOnlyList<uint> GainKeys);
public sealed record AudioMusicOutput(int Index, uint Key, string Name, uint NodeKind, uint Variant,
    uint Flags, IReadOnlyList<int> DownstreamIndices, float StaticGain, IReadOnlyList<uint> GainKeys);
/// <summary>The resource's internal mix layout and ordered output graph. Device
/// channels and current control gains belong to the actual playback host.</summary>
public sealed record AudioMusicOutputGraph(LoadedRdbAsset Source, int MixChannels, int RouteCapacity,
    AudioMusicClassification Classification, IReadOnlyList<AudioMusicOutput> Outputs);

/// <summary>Reads the actual CGRS master/output/classification records used by
/// legacy music. It does not bind a missing output, execute a DSP or flatten its
/// graph into one gain. Unknown sections remain present in Source.</summary>
public static class AudioMusicOutputResources
{
    public static AudioMusicOutputGraph Read(LoadedRdbAsset source, uint classificationId)
    {
        ArgumentNullException.ThrowIfNull(source);
        var reader = new Reader(source.Bytes);
        if (reader.Length < 80 || !source.Bytes.AsSpan(0, 8).SequenceEqual("CGRS\0\0\0\0"u8) ||
            reader.U64(8) != (ulong)reader.Length || !source.Bytes.AsSpan(16, 4).SequenceEqual("KTSR"u8) ||
            reader.U32(20) != 0x9EE638C6 || reader.U32(40) != reader.Length - 16 || reader.U32(44) != reader.Length - 16)
            throw new InvalidDataException("Music output resource has an invalid CGRS container.");
        Section? master = null, classification = null;
        int position = 80;
        while (position < reader.Length)
        {
            reader.Range(position, 8);
            uint length = reader.U32(position + 4);
            if (length < 8 || length > reader.Length - position)
                throw new InvalidDataException("Music output resource has an incomplete section.");
            var section = new Section(reader, position, (int)length);
            uint type = section.U32(0);
            if (type == 0xE0736214)
            {
                if (master is not null) throw new InvalidDataException("Music master section is ambiguous.");
                section.Range(0, 0x60); master = section;
            }
            else if (type == 0xF96787ED)
            {
                section.Range(0, 0x24);
                if (section.U32(8) == classificationId)
                {
                    if (classification is not null) throw new InvalidDataException("Music classification key is ambiguous.");
                    classification = section;
                }
            }
            position += (int)length;
        }
        if (master is null || classification is null)
            throw new InvalidDataException("Music output resource lacks its master or requested classification.");
        var m = master.Value; var c = classification.Value;
        int mixChannels = checked((int)m.U32(8)), capacity = checked((int)m.U32(0x14));
        if (mixChannels is < 1 or > 8 || capacity is < 1 or > 255)
            throw new NotSupportedException("Music internal mix channels or route capacity are unsupported.");
        uint outputCount = m.U32(0x0C);
        if (outputCount > 255) throw new NotSupportedException("Music output count exceeds byte-index routing.");
        int outputTable = outputCount == 0 ? 0 : m.Relative(m.U32(0x10), checked((int)outputCount * 4));
        var outputs = new AudioMusicOutput[outputCount];
        for (int index = 0; index < outputs.Length; index++)
        {
            int record = m.Relative(m.U32(outputTable + index * 4), 0x50);
            var nameBytes = m.Bytes(record + 4, 32);
            int nul = nameBytes.IndexOf((byte)0);
            if (nul < 0) throw new InvalidDataException("Music output name is not terminated in its source field.");
            string name;
            try { name = new UTF8Encoding(false, true).GetString(nameBytes[..nul]); }
            catch (DecoderFallbackException e) { throw new InvalidDataException("Music output name is not UTF-8.", e); }
            uint edgeCount = m.U32(record + 0x30);
            if (edgeCount > 4) throw new NotSupportedException("Music output exceeds its four downstream descriptor fields.");
            var edges = new int[edgeCount];
            for (int edge = 0; edge < edges.Length; edge++) edges[edge] = unchecked((int)m.U32(record + 0x34 + edge * 4));
            uint gainCount = m.U32(record + 0x48), gainRelative = m.U32(record + 0x4C);
            var gains = ReadKeys(m, record, gainCount, gainRelative);
            outputs[index] = new(index, m.U32(record), name, m.U32(record + 0x24), m.U32(record + 0x28),
                m.U32(record + 0x2C), Array.AsReadOnly(edges), m.Nonnegative(record + 0x44), gains);
        }
        uint routeCount = c.U32(0x0C);
        if (routeCount > 1024) throw new InvalidDataException("Music classification route count is invalid.");
        int keys = routeCount == 0 ? 0 : c.Relative(c.U32(0x10), checked((int)routeCount * 4));
        int amplitudes = routeCount == 0 ? 0 : c.Relative(c.U32(0x14), checked((int)routeCount * 4));
        int kinds = routeCount == 0 ? 0 : c.Relative(c.U32(0x18), checked((int)routeCount * 4));
        var routes = new AudioMusicRoute[routeCount];
        for (int route = 0; route < routes.Length; route++)
            routes[route] = new(c.U32(keys + route * 4), c.Nonnegative(amplitudes + route * 4), c.U32(kinds + route * 4));
        return new(source, mixChannels, capacity,
            new(classificationId, Array.AsReadOnly(routes), ReadKeys(c, 0, c.U32(0x1C), c.U32(0x20))),
            Array.AsReadOnly(outputs));
    }

    private static IReadOnlyList<uint> ReadKeys(Section section, int record, uint count, uint relative)
    {
        if (count > 1024) throw new InvalidDataException("Music gain key count is invalid.");
        var keys = new uint[count];
        if (count == 0) return Array.AsReadOnly(keys);
        long start = (long)record + relative;
        if (start > int.MaxValue) throw new InvalidDataException("Music gain pointer exceeds int32.");
        section.Range((int)start, checked((int)count * 4));
        for (int key = 0; key < keys.Length; key++) keys[key] = section.U32((int)start + key * 4);
        return Array.AsReadOnly(keys);
    }
    private sealed class Reader(byte[] bytes)
    {
        internal int Length => bytes.Length;
        internal void Range(int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > bytes.Length || count > bytes.Length - offset)
                throw new InvalidDataException("Music resource read is outside its source.");
        }
        internal uint U32(int offset) { Range(offset, 4); return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)); }
        internal ulong U64(int offset) { Range(offset, 8); return BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8)); }
        internal ReadOnlySpan<byte> Bytes(int offset, int count) { Range(offset, count); return bytes.AsSpan(offset, count); }
    }
    private readonly record struct Section(Reader Reader, int Start, int Length)
    {
        internal void Range(int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > Length || count > Length - offset)
                throw new InvalidDataException("Music resource pointer is outside its section.");
        }
        internal uint U32(int offset) { Range(offset, 4); return Reader.U32(Start + offset); }
        internal ReadOnlySpan<byte> Bytes(int offset, int count) { Range(offset, count); return Reader.Bytes(Start + offset, count); }
        internal int Relative(uint offset, int count)
        {
            if (offset > int.MaxValue) throw new InvalidDataException("Music resource pointer exceeds int32.");
            Range((int)offset, count); return (int)offset;
        }
        internal float Nonnegative(int offset)
        {
            float value = BitConverter.Int32BitsToSingle(unchecked((int)U32(offset)));
            if (!float.IsFinite(value) || value < 0) throw new InvalidDataException("Music amplitude is not finite and nonnegative.");
            return value;
        }
    }
}
