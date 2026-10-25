using System.Buffers.Binary;

namespace San14.Data.Formats;

/// <summary>An effect input declaration. Static source declarations use slot zero;
/// the CPU particle stream uses slot one with one element per instance.</summary>
public sealed record G1EffectModelAttribute(int InputSlot, int ByteOffset, byte NativeFormat,
    byte SemanticCode, byte SemanticIndex, string SemanticName, int DxgiFormat,
    G1AttributeEncoding Encoding, int ComponentCount, int ByteWidth)
{
    public bool PerInstance => InputSlot != 0;
    public int InstanceStepRate => PerInstance ? 1 : 0;
}

public sealed record G1EffectModelMesh(int Index, int FileOffset,
    ReadOnlyMemory<byte> SourceAttributes, ReadOnlyMemory<byte> RawDescriptor,
    int VertexCount, int VertexStride, ReadOnlyMemory<byte> VertexBytes,
    int IndexCount, int NativeIndexFormat, int IndexStride, int IndexDxgiFormat,
    ReadOnlyMemory<byte> IndexBytes, IReadOnlyList<G1EffectModelAttribute> StaticLayout,
    IReadOnlyList<G1EffectModelAttribute> ShortLayout,
    IReadOnlyList<G1EffectModelAttribute> FullLayout)
{
    public uint GetIndex(int index)
    {
        if ((uint)index >= (uint)IndexCount) throw new ArgumentOutOfRangeException(nameof(index));
        ReadOnlySpan<byte> value = IndexBytes.Span.Slice(index * IndexStride, IndexStride);
        return IndexStride switch
        {
            1 => value[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(value),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(value),
            _ => throw new InvalidOperationException("Invalid effect model index stride.")
        };
    }
}

/// <summary>Indices address the complete static mesh buffer. VertexStart describes
/// the referenced range; it is not a base vertex or a vertex-buffer byte offset.</summary>
public sealed record G1EffectModelSubdraw(int Index, int MeshIndex, int NativeTopology,
    int D3D11Topology, int VertexStart, int VertexCount, int IndexStart, int IndexCount,
    ReadOnlyMemory<byte> RawBytes)
{
    public int BaseVertex => 0;
    public int VertexByteOffset => 0;
}

public sealed record G1EffectModel(uint Version, uint UnknownWord8, ReadOnlyMemory<byte> Bytes,
    IReadOnlyList<G1EffectModelMesh> Meshes, IReadOnlyList<G1EffectModelSubdraw> Subdraws,
    int ConsumedByteLength, ReadOnlyMemory<byte> OpaqueTail);

/// <summary>Reads the installed modern G1EM static geometry prefix. The two particle
/// layouts declare slot one, but its bytes and active subdraw selection belong to
/// the live effect instance. The resource remainder is preserved without interpretation.</summary>
public static class G1EffectModelReader
{
    public static G1EffectModel Read(ReadOnlyMemory<byte> bytes) => new Reader(bytes).Read();

    // These are input declarations, not particle values. The short CPU layout uses
    // the first five declarations; the full layout extends the instance data to 112 bytes.
    private static readonly G1EffectModelAttribute[] InstanceAttributes =
    [
        Attribute(1, 0, 3, 1, 0), Attribute(1, 16, 3, 2, 0), Attribute(1, 32, 3, 7, 0),
        Attribute(1, 48, 7, 10, 1), Attribute(1, 56, 7, 5, 3),
        Attribute(1, 64, 3, 5, 4), Attribute(1, 80, 7, 5, 5),
        Attribute(1, 88, 7, 10, 2), Attribute(1, 96, 7, 10, 3), Attribute(1, 104, 1, 5, 6)
    ];

    private static G1EffectModelAttribute Attribute(int slot, int offset, byte format,
        byte semantic, byte semanticIndex)
    {
        (int dxgi, G1AttributeEncoding encoding, int components, int width) = format switch
        {
            1 => (16, G1AttributeEncoding.Float32x2, 2, 8),
            3 => (2, G1AttributeEncoding.Float32x4, 4, 16),
            7 => (14, G1AttributeEncoding.SInt16x4, 4, 8),
            10 => (34, G1AttributeEncoding.Float16x2, 2, 4),
            11 => (10, G1AttributeEncoding.Float16x4, 4, 8),
            13 => (28, G1AttributeEncoding.UNorm8x4, 4, 4),
            _ => throw new InvalidDataException($"Unsupported G1EM attribute format {format}.")
        };
        string name = semantic switch
        {
            0 => "POSITION", 1 => "BLENDWEIGHT", 2 => "BLENDINDICES", 3 => "NORMAL",
            5 => "TEXCOORD", 6 => "TANGENT", 7 => "BINORMAL", 10 => "COLOR",
            _ => throw new InvalidDataException($"Unsupported G1EM semantic {semantic}.")
        };
        return new(slot, offset, format, semantic, semanticIndex, name, dxgi, encoding, components, width);
    }

    private sealed class Reader(ReadOnlyMemory<byte> bytes)
    {
        internal G1EffectModel Read()
        {
            Require(0, 16);
            // Serialized words are little endian (the on-disk magic reads ME1G).
            if (U(0) != 0x4731454D || U(4) != 0x30303031)
                throw new InvalidDataException("Unsupported G1EM signature or version.");
            int meshCount = H(12), subdrawCount = H(14), cursor = 16;
            var meshes = new List<G1EffectModelMesh>(meshCount);
            for (int index = 0; index < meshCount; index++)
            {
                int start = cursor;
                Require(cursor, 4);
                int attributeCount = N(cursor); cursor += 4;
                if (attributeCount > 6) throw new InvalidDataException("Unsupported G1EM attribute count.");
                Require(cursor, attributeCount * 8);
                ReadOnlyMemory<byte> sourceAttributes = bytes.Slice(cursor, attributeCount * 8);
                var attributes = new List<G1EffectModelAttribute>(attributeCount);
                for (int attribute = 0; attribute < attributeCount; attribute++)
                {
                    int at = cursor + attribute * 8;
                    if (bytes.Span[at + 5] != 0)
                        throw new InvalidDataException("Unsupported G1EM attribute padding.");
                    // The loader clears the serialized slot, retaining the original bytes above.
                    attributes.Add(Attribute(0, H(at + 2), bytes.Span[at + 4],
                        bytes.Span[at + 6], bytes.Span[at + 7]));
                }
                cursor += attributeCount * 8;
                Require(cursor, 16);
                ReadOnlyMemory<byte> descriptor = bytes.Slice(cursor, 16);
                int vertexCount = N(cursor), stride = N(cursor + 4), indexCount = N(cursor + 8),
                    indexFormat = N(cursor + 12);
                cursor += 16;
                if (stride <= 0 || indexFormat > 2)
                    throw new InvalidDataException("Invalid G1EM vertex stride or index format.");
                foreach (G1EffectModelAttribute attribute in attributes)
                    if (attribute.ByteOffset > stride - attribute.ByteWidth)
                        throw new InvalidDataException("G1EM attribute exceeds the vertex stride.");
                int vertexLength = Product(vertexCount, stride), indexStride = 1 << indexFormat,
                    indexLength = Product(indexCount, indexStride);
                Require(cursor, vertexLength);
                ReadOnlyMemory<byte> vertices = bytes.Slice(cursor, vertexLength); cursor += vertexLength;
                Require(cursor, indexLength);
                ReadOnlyMemory<byte> indices = bytes.Slice(cursor, indexLength); cursor += indexLength;
                meshes.Add(new(index, start, sourceAttributes, descriptor, vertexCount, stride, vertices,
                    indexCount, indexFormat, indexStride, indexFormat switch { 0 => 62, 1 => 57, _ => 42 },
                    indices, attributes.AsReadOnly(), InstanceAttributes.Take(5).Concat(attributes).ToArray(),
                    InstanceAttributes.Concat(attributes).ToArray()));
            }
            var subdraws = new List<G1EffectModelSubdraw>(subdrawCount);
            for (int index = 0; index < subdrawCount; index++)
            {
                Require(cursor, 24);
                int mesh = N(cursor), topology = N(cursor + 4), vertexStart = N(cursor + 8),
                    vertexCount = N(cursor + 12), indexStart = N(cursor + 16), indexCount = N(cursor + 20);
                if (mesh >= meshes.Count || topology > 4)
                    throw new InvalidDataException("G1EM subdraw references an unsupported mesh or topology.");
                G1EffectModelMesh geometry = meshes[mesh];
                if (vertexStart > geometry.VertexCount || vertexCount > geometry.VertexCount - vertexStart ||
                    indexStart > geometry.IndexCount || indexCount > geometry.IndexCount - indexStart)
                    throw new InvalidDataException("G1EM subdraw exceeds its mesh buffers.");
                subdraws.Add(new(index, mesh, topology, topology + 1, vertexStart, vertexCount,
                    indexStart, indexCount, bytes.Slice(cursor, 24)));
                cursor += 24;
            }
            return new(U(4), U(8), bytes, meshes.AsReadOnly(), subdraws.AsReadOnly(), cursor, bytes[cursor..]);
        }

        private void Require(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
                throw new InvalidDataException("G1EM structure exceeds the resource boundary.");
        }
        private int N(int offset)
        {
            uint value = U(offset);
            if (value > int.MaxValue) throw new InvalidDataException("G1EM count or offset is too large.");
            return (int)value;
        }
        private static int Product(int count, int stride)
        {
            long length = (long)count * stride;
            if (length > int.MaxValue) throw new InvalidDataException("G1EM buffer is too large.");
            return (int)length;
        }
        private uint U(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span.Slice(offset, 4));
        private ushort H(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Span.Slice(offset, 2));
    }
}
