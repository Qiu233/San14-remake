using System.Buffers.Binary;

namespace San14.Data.Formats;

public enum G1AttributeEncoding
{
    Float32x2, Float32x3, Float32x4, UInt8x4, Float16x2, Float16x4, UNorm8x4,
    Float32x1, SInt16x4
}

public sealed record G1DecodedAttribute(int Index, int BufferIndex, G1VertexAttribute Source,
    G1AttributeEncoding Encoding, int ComponentCount, int ElementCount, float[] Values)
{
    public byte UsageCode => (byte)Source.SemanticCode;
    public byte UsageIndex => (byte)(Source.SemanticCode >> 8);

    public ReadOnlySpan<float> GetElement(int index)
    {
        if ((uint)index >= ElementCount) throw new ArgumentOutOfRangeException(nameof(index));
        return Values.AsSpan(index * ComponentCount, ComponentCount);
    }
}

public sealed record G1DecodedVertexLayout(int Index, IReadOnlyList<G1DecodedAttribute> Attributes);
public sealed record G1ModelVertexData(IReadOnlyList<G1DecodedVertexLayout> Layouts);

/// <summary>
/// Decodes verified numeric storage in installed FieldEditor4 and CharacterEditor models.
/// Usage and layer bytes remain source codes: shader-specific channels and additional streams are not
/// relabelled, expanded or transformed. Vector magnitudes are preserved; UNorm storage alone uses /255.
/// Geometry retains all original buffer bytes.
/// </summary>
public static class G1ModelVertexReader
{
    public static G1ModelVertexData Read(G1ModelGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var layouts = new List<G1DecodedVertexLayout>(geometry.Layouts.Count);
        foreach (G1VertexLayout layout in geometry.Layouts)
        {
            var attributes = new List<G1DecodedAttribute>(layout.Attributes.Count);
            for (int index = 0; index < layout.Attributes.Count; index++)
            {
                G1VertexAttribute attribute = layout.Attributes[index];
                string context = $"G1M layout {layout.Index} attribute {index}";
                (G1AttributeEncoding encoding, int components, int width) = attribute.TypeCode switch
                {
                    // G1M ABC0A0 copies the original eight-byte declarations
                    // into the native layout creator. Its type table maps 0 to
                    // DXGI41 R32_FLOAT and 7 to DXGI14 R16G16B16A16_SINT.
                    0 => (G1AttributeEncoding.Float32x1, 1, 4),
                    1 => (G1AttributeEncoding.Float32x2, 2, 8),
                    2 => (G1AttributeEncoding.Float32x3, 3, 12),
                    3 => (G1AttributeEncoding.Float32x4, 4, 16),
                    5 => (G1AttributeEncoding.UInt8x4, 4, 4),
                    7 => (G1AttributeEncoding.SInt16x4, 4, 8),
                    10 => (G1AttributeEncoding.Float16x2, 2, 4),
                    11 => (G1AttributeEncoding.Float16x4, 4, 8),
                    13 => (G1AttributeEncoding.UNorm8x4, 4, 4),
                    _ => throw new InvalidDataException($"{context} has unsupported numeric storage {attribute.TypeCode:X4}.")
                };
                if ((uint)attribute.BufferReferenceSlot >= layout.BufferReferences.Count)
                    throw new InvalidDataException($"{context} references a missing buffer slot.");
                int bufferIndex = layout.BufferReferences[attribute.BufferReferenceSlot];
                if ((uint)bufferIndex >= geometry.VertexBuffers.Count)
                    throw new InvalidDataException($"{context} references a missing vertex buffer.");
                G1VertexBuffer buffer = geometry.VertexBuffers[bufferIndex];
                if (buffer.VertexCount < 0 || buffer.Stride <= 0 ||
                    (long)buffer.VertexCount * buffer.Stride != buffer.Data.Length ||
                    attribute.ByteOffset < 0 || attribute.ByteOffset > buffer.Stride - width ||
                    (long)buffer.VertexCount * components > int.MaxValue)
                    throw new InvalidDataException($"{context} numeric width or element count exceeds its buffer.");
                var values = new float[buffer.VertexCount * components];
                for (int element = 0; element < buffer.VertexCount; element++)
                {
                    ReadOnlySpan<byte> bytes = buffer.Data.AsSpan(element * buffer.Stride + attribute.ByteOffset, width);
                    for (int component = 0; component < components; component++)
                    {
                        float value = attribute.TypeCode switch
                        {
                            0 or 1 or 2 or 3 => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(component * 4, 4)),
                            10 or 11 => (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(component * 2, 2))),
                            5 => bytes[component],
                            7 => BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(component * 2, 2)),
                            13 => bytes[component] / 255f,
                            _ => throw new InvalidOperationException("Numeric storage was not checked.")
                        };
                        if (!float.IsFinite(value))
                            throw new InvalidDataException($"{context} element {element} component {component} is not finite.");
                        values[element * components + component] = value;
                    }
                }
                attributes.Add(new(index, bufferIndex, attribute, encoding, components, buffer.VertexCount, values));
            }
            layouts.Add(new(layout.Index, attributes));
        }
        return new(layouts);
    }
}
