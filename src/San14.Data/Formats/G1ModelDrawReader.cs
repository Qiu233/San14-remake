using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record G1DrawRecord(int Index, uint Flags, int LayoutIndex, uint UnknownValue8,
    uint RawIndex12, uint UnknownValue16, uint RawIndex20,
    uint RawIndex24, int IndexBufferIndex, uint UnknownValue32, uint PrimitiveCode,
    int VertexStart, int VertexCount, int IndexStart, int IndexCount,
    int PrimaryVertexBufferIndex, IReadOnlyList<int> AdditionalBufferReferences, byte[] RawBytes);
public sealed record G1DrawGroup(int Index, string Label, uint Flags, uint UnknownValue20,
    IReadOnlyList<int> DrawIndices, byte[] RawBytes);
public sealed record G1ModelDrawBindings(IReadOnlyList<G1DrawRecord> Draws,
    IReadOnlyList<G1DrawGroup> Groups, byte[] RawGroupPrefix);

/// <summary>
/// Reads the verified GM1G4400 draw references and label groups.
/// Primitive codes, additional vertex streams and the three raw indices
/// remain raw: no topology, shader, transform or material semantics are inferred.
/// </summary>
public static class G1ModelDrawReader
{
    public static G1ModelDrawBindings Read(G1ModelGeometry geometry)
    {
        G1GeometrySection drawSection = geometry.Sections.Single(x => x.Id == 0x10008);
        if (drawSection.ItemCount < 0 || drawSection.Length != 12L + drawSection.ItemCount * 56L ||
            drawSection.RawBytes.Length != drawSection.Length)
            throw new InvalidDataException("Unsupported G1M draw record size or count.");
        var draws = new List<G1DrawRecord>(drawSection.ItemCount);
        for (int i = 0; i < drawSection.ItemCount; i++)
        {
            ReadOnlySpan<byte> row = drawSection.RawBytes.AsSpan(12 + i * 56, 56);
            int layoutIndex = Reference(row, 4, geometry.Layouts.Count, "layout");
            // All three local section counts happen to match in the original models.
            // Range coincidence therefore cannot establish the target of these indices.
            uint rawIndex12 = UInt(row, 12), rawIndex20 = UInt(row, 20), rawIndex24 = UInt(row, 24);
            int indexBufferIndex = Reference(row, 28, geometry.IndexBuffers.Count, "index buffer");
            G1VertexLayout layout = geometry.Layouts[layoutIndex];
            G1VertexAttribute[] primaryAttributes = layout.Attributes.Where(a => a.SemanticCode == 0).ToArray();
            if (primaryAttributes.Length != 1 || primaryAttributes[0].BufferReferenceSlot != 0 ||
                primaryAttributes[0].ByteOffset != 0 || primaryAttributes[0].TypeCode != 2 || layout.BufferReferences.Count < 1)
                throw new InvalidDataException("Unsupported primary vertex layout in G1M draw binding.");
            int vertexBufferIndex = layout.BufferReferences[primaryAttributes[0].BufferReferenceSlot];
            G1VertexBuffer vertexBuffer = geometry.VertexBuffers[vertexBufferIndex];
            G1IndexBuffer indexBuffer = geometry.IndexBuffers[indexBufferIndex];
            int vertexStart = Value(row, 40), vertexCount = Value(row, 44);
            int indexStart = Value(row, 48), indexCount = Value(row, 52);
            if ((long)vertexStart + vertexCount > vertexBuffer.VertexCount ||
                (long)indexStart + indexCount > indexBuffer.IndexCount)
                throw new InvalidDataException("G1M draw window exceeds its explicitly referenced buffer.");
            for (int j = indexStart; j < indexStart + indexCount; j++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(indexBuffer.Data.AsSpan(j * 2, 2));
                if (index < vertexStart || (long)index >= (long)vertexStart + vertexCount)
                    throw new InvalidDataException("G1M draw index lies outside its declared vertex window.");
            }
            draws.Add(new(i, UInt(row, 0), layoutIndex, UInt(row, 8), rawIndex12, UInt(row, 16),
                rawIndex20, rawIndex24, indexBufferIndex, UInt(row, 32), UInt(row, 36),
                vertexStart, vertexCount, indexStart, indexCount, vertexBufferIndex,
                layout.BufferReferences.Skip(1).ToArray(), row.ToArray()));
        }

        G1GeometrySection groupSection = geometry.Sections.Single(x => x.Id == 0x10009);
        ReadOnlySpan<byte> data = groupSection.RawBytes;
        if (groupSection.ItemCount != 1 || data.Length < 48 || data.Length != groupSection.Length)
            throw new InvalidDataException("Unsupported or truncated G1M draw group header.");
        int groupCount = Value(data, 24), position = 48;
        if (groupCount > (data.Length - position) / 28)
            throw new InvalidDataException("G1M draw group count exceeds its section.");
        var groups = new List<G1DrawGroup>(groupCount);
        var referenced = new HashSet<int>();
        for (int i = 0; i < groupCount; i++)
        {
            int start = position;
            Require(data, position, 28, "group header");
            ReadOnlySpan<byte> name = data.Slice(position, 16);
            int terminator = name.IndexOf((byte)0);
            ReadOnlySpan<byte> label = terminator < 0 ? name : name[..terminator];
            foreach (byte b in label)
                if (b is < 32 or > 126) throw new InvalidDataException("G1M draw group label is not printable ASCII.");
            if (terminator >= 0)
                foreach (byte b in name[terminator..])
                    if (b != 0) throw new InvalidDataException("G1M draw group label has data after its terminator.");
            uint flags = UInt(data, position + 16), unknown = UInt(data, position + 20);
            int count = Value(data, position + 24); position += 28;
            if (count > (data.Length - position) / 4)
                throw new InvalidDataException("G1M group draw references exceed their section.");
            var refs = new List<int>(count);
            for (int j = 0; j < count; j++)
            {
                int draw = Reference(data, position, draws.Count, "group draw"); position += 4;
                if (!referenced.Add(draw)) throw new InvalidDataException("Unsupported repeated G1M group draw reference.");
                refs.Add(draw);
            }
            groups.Add(new(i, Encoding.ASCII.GetString(label), flags, unknown, refs,
                data.Slice(start, position - start).ToArray()));
        }
        if (position != data.Length || referenced.Count != draws.Count)
            throw new InvalidDataException("G1M draw groups have unindexed bytes or missing draw references.");
        return new(draws, groups, data[..48].ToArray());
    }

    private static int Reference(ReadOnlySpan<byte> bytes, int offset, int count, string label)
    {
        int value = Value(bytes, offset);
        if (value >= count) throw new InvalidDataException($"G1M draw references a missing {label}.");
        return value;
    }
    private static int Value(ReadOnlySpan<byte> bytes, int offset)
    {
        uint value = UInt(bytes, offset);
        if (value > int.MaxValue) throw new InvalidDataException("G1M draw count, offset or reference exceeds the supported range.");
        return (int)value;
    }
    private static uint UInt(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static void Require(ReadOnlySpan<byte> bytes, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            throw new InvalidDataException($"G1M {label} exceeds its section.");
    }
}
