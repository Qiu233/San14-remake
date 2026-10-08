using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G1GeometrySection(int Index, uint Id, int Offset, int Length,
    int ItemCount, byte[] RawBytes);
public sealed record G1VertexBuffer(int Index, uint UnknownValue0, int Stride,
    int VertexCount, uint UnknownValue12, byte[] Data, byte[] RawHeader);
public sealed record G1VertexAttribute(int BufferReferenceSlot, int ByteOffset,
    ushort TypeCode, ushort SemanticCode, byte[] RawBytes);
public sealed record G1VertexLayout(int Index, IReadOnlyList<int> BufferReferences,
    IReadOnlyList<G1VertexAttribute> Attributes, byte[] RawBytes);
public sealed record G1IndexBuffer(int Index, int IndexCount, int BitsPerIndex,
    uint UnknownValue8, byte[] Data, byte[] Padding, byte[] RawHeader);
public sealed record G1ModelGeometry(G1ModelHeaders Headers, byte[] RawGeometryHeader,
    IReadOnlyList<G1GeometrySection> Sections, IReadOnlyList<G1VertexBuffer> VertexBuffers,
    IReadOnlyList<G1VertexLayout> Layouts, IReadOnlyList<G1IndexBuffer> IndexBuffers);

/// <summary>
/// Reads verified GM1G4400 raw structures in FieldEditor4 and CE CharacterEditor models.
/// Attribute meanings, draw topology, material association and coordinates are not inferred.
/// </summary>
public static class G1ModelGeometryReader
{
    public static G1ModelGeometry Read(ReadOnlySpan<byte> model, long indexedDecodedSize)
    {
        G1ModelHeaders headers = G1ModelHeaderReader.Read(model, indexedDecodedSize);
        if (model.Length != indexedDecodedSize)
            throw new InvalidDataException("Geometry requires the complete decoded model.");
        G1ModelChunkHeader chunk = headers.Chunks[3];
        ReadOnlySpan<byte> gm = model.Slice(chunk.Offset, chunk.Length);
        if (gm.Length < 48 || !gm.Slice(12, 4).SequenceEqual("DX11"u8) || Int(gm, 44) != 9)
            throw new InvalidDataException("Unsupported or truncated G1M geometry header.");
        var sections = new List<G1GeometrySection>(9);
        int position = 48;
        for (int i = 0; i < 9; i++)
        {
            Require(gm, position, 12, "section header");
            uint id = UInt(gm, position);
            int length = Int(gm, position + 4), count = Int(gm, position + 8);
            if (id != 0x10001u + i || length < 12 || count < 0 || count > (gm.Length - position) / 4)
                throw new InvalidDataException($"Unsupported or invalid G1M section {i}.");
            Require(gm, position, length, "section body");
            sections.Add(new(i, id, position, length, count, gm.Slice(position, length).ToArray()));
            position += length;
        }
        if (position != gm.Length) throw new InvalidDataException("G1M geometry has unindexed trailing bytes.");
        List<G1VertexBuffer> buffers = ReadVertices(sections[3]);
        List<G1VertexLayout> layouts = ReadLayouts(sections[4], buffers);
        List<G1IndexBuffer> indices = ReadIndices(sections[6]);
        return new(headers, gm[..48].ToArray(), sections, buffers, layouts, indices);
    }

    private static List<G1VertexBuffer> ReadVertices(G1GeometrySection section)
    {
        ReadOnlySpan<byte> data = section.RawBytes;
        var result = new List<G1VertexBuffer>(section.ItemCount);
        int position = 12;
        for (int i = 0; i < section.ItemCount; i++)
        {
            Require(data, position, 16, "vertex buffer header");
            uint unknown0 = UInt(data, position), unknown12 = UInt(data, position + 12);
            int stride = Int(data, position + 4), count = Int(data, position + 8);
            if (stride is not (32 or 36 or 40 or 44 or 48 or 52 or 56 or 60 or 64 or 72) || count < 0 ||
                (long)stride * count > data.Length - position - 16)
                throw new InvalidDataException("Unsupported stride or invalid vertex buffer boundary.");
            int length = stride * count;
            result.Add(new(i, unknown0, stride, count, unknown12,
                data.Slice(position + 16, length).ToArray(), data.Slice(position, 16).ToArray()));
            position += 16 + length;
        }
        if (position != data.Length) throw new InvalidDataException("Vertex section has unindexed bytes.");
        return result;
    }

    private static List<G1VertexLayout> ReadLayouts(G1GeometrySection section, IReadOnlyList<G1VertexBuffer> buffers)
    {
        ReadOnlySpan<byte> data = section.RawBytes;
        var result = new List<G1VertexLayout>(section.ItemCount);
        int position = 12;
        for (int i = 0; i < section.ItemCount; i++)
        {
            int start = position;
            Require(data, position, 4, "layout buffer count");
            int count = Int(data, position); position += 4;
            if (count < 1 || count > (data.Length - position) / 4)
                throw new InvalidDataException("Invalid layout buffer count.");
            var references = new List<int>(count);
            for (int j = 0; j < count; j++)
            {
                int id = Int(data, position); position += 4;
                if (id < 0 || id >= buffers.Count) throw new InvalidDataException("Layout references a missing vertex buffer.");
                references.Add(id);
            }
            Require(data, position, 4, "layout attribute count");
            int attributeCount = Int(data, position); position += 4;
            if (attributeCount < 0 || attributeCount > (data.Length - position) / 8)
                throw new InvalidDataException("Invalid layout attribute count.");
            var attributes = new List<G1VertexAttribute>(attributeCount);
            for (int j = 0; j < attributeCount; j++)
            {
                ReadOnlySpan<byte> raw = data.Slice(position, 8);
                ushort slot = UShort(raw, 0), offset = UShort(raw, 2);
                if (slot >= references.Count || offset >= buffers[references[slot]].Stride)
                    throw new InvalidDataException("Attribute references a missing buffer or a byte outside its stride.");
                attributes.Add(new(slot, offset, UShort(raw, 4), UShort(raw, 6), raw.ToArray()));
                position += 8;
            }
            result.Add(new(i, references, attributes, data.Slice(start, position - start).ToArray()));
        }
        if (position != data.Length) throw new InvalidDataException("Layout section has unindexed bytes.");
        return result;
    }

    private static List<G1IndexBuffer> ReadIndices(G1GeometrySection section)
    {
        ReadOnlySpan<byte> data = section.RawBytes;
        var result = new List<G1IndexBuffer>(section.ItemCount);
        int position = 12;
        for (int i = 0; i < section.ItemCount; i++)
        {
            Require(data, position, 12, "index buffer header");
            int count = Int(data, position), bits = Int(data, position + 4);
            uint unknown = UInt(data, position + 8);
            if (bits != 16 || count < 0 || (long)count * 2 > data.Length - position - 12)
                throw new InvalidDataException("Unsupported index format or invalid index buffer boundary.");
            int length = count * 2, end = position + 12 + length;
            int padding = (4 - (end & 3)) & 3;
            Require(data, end, padding, "index alignment padding");
            result.Add(new(i, count, bits, unknown, data.Slice(position + 12, length).ToArray(),
                data.Slice(end, padding).ToArray(), data.Slice(position, 12).ToArray()));
            position = end + padding;
        }
        if (position != data.Length) throw new InvalidDataException("Index section has unindexed bytes.");
        return result;
    }

    private static void Require(ReadOnlySpan<byte> bytes, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            throw new InvalidDataException($"G1M {label} exceeds its section.");
    }
    private static int Int(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    private static uint UInt(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static ushort UShort(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
}
