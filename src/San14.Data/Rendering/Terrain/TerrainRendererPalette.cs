using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The six addressed renderer columns, before shader reflection or GPU packing.</summary>
public sealed record TerrainRendererPaletteEntry(int ParameterIndex, uint DetailTextureNameIndex,
    uint BlendSharpnessBits, uint ScaleUBits, uint ScaleVBits, uint TessScaleBits, uint TessBiasBits);

/// <summary>Original renderer descriptor metadata; stage/type codes remain raw.</summary>
public sealed record TerrainRendererPaletteColumn(int NativeParameterSlot, string SourceName,
    ulong RawDescriptorWord, int RendererCpuOffset)
{
    public byte RawStageMask => (byte)RawDescriptorWord;
    public byte RawTypeCode => (byte)(RawDescriptorWord >> 8);
    public ushort NativeElementCount => (ushort)(RawDescriptorWord >> 16);
}

public sealed record TerrainRendererPaletteProjection(IReadOnlyList<TerrainRendererPaletteEntry> Entries,
    IReadOnlyList<string> UnresolvedInputs)
{
    public bool Resolved => UnresolvedInputs.Count == 0;
}

/// <summary>
/// Projects the current CE Landscape draw packet through its renderer setter.
/// This is a CPU staging layout, not a texture-layer binding or GPU constant buffer.
/// The original reader retains all source bits even when this projection is unsupported.
/// </summary>
public static class TerrainRendererPalette
{
    public const int NativeCapacity = 255;
    public const int DrawPacketStride = 28;
    public const uint MaximumBlendSharpnessBits = 0x3F7D70A4;

    public static IReadOnlyList<TerrainRendererPaletteColumn> Columns { get; } =
        Array.AsReadOnly(new TerrainRendererPaletteColumn[]
        {
            new(4, "vDtlTexIdx", 0x00FF050B, 0x14E0),
            new(5, "vDtlBldSrp", 0x00FF010A, 0x18DC),
            new(6, "vDtlScaleU", 0x00FF010B, 0x1CD8),
            new(7, "vDtlScaleV", 0x00FF010B, 0x20D4),
            new(8, "vDtlTssScl", 0x00FF0109, 0x24D0),
            new(9, "vDtlTssBis", 0x00FF0109, 0x28CC)
        });

    /// <summary>Original 24-byte fields followed by the first-name lookup u32.</summary>
    public static byte[] EncodeDrawPacket(IReadOnlyList<TerrainDetailPaletteEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        byte[] packet = new byte[checked(entries.Count * DrawPacketStride)];
        for (int i = 0; i < entries.Count; i++)
        {
            TerrainDetailPaletteEntry entry = entries[i];
            TerrainPaletteEntry fields = entry.Fields;
            Span<byte> row = packet.AsSpan(i * DrawPacketStride, DrawPacketStride);
            Write(row, 0, fields.MaterialNameHash);
            Write(row, 4, fields.BlendSharpnessBits);
            Write(row, 8, fields.ScaleUBits);
            Write(row, 12, fields.ScaleVBits);
            Write(row, 16, fields.TessScaleBits);
            Write(row, 20, fields.TessBiasBits);
            Write(row, 24, entry.DetailTextureNameIndex);
        }
        return packet;
    }

    /// <summary>
    /// Applies the proven finite-input arithmetic using float32, round-to-nearest.
    /// Unknown inputs produce an explicit unresolved result, never substituted rows.
    /// Untouched Tess fields keep their exact bits, including nonfinite values.
    /// </summary>
    public static TerrainRendererPaletteProjection Project(IReadOnlyList<TerrainDetailPaletteEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count > NativeCapacity)
            return new([], [$"Palette has {entries.Count} rows; this CE renderer supports at most {NativeCapacity}."]);

        var issues = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            TerrainPaletteEntry fields = entries[i].Fields;
            if (fields.ParameterIndex != i)
                issues.Add($"Palette row {i} has source ordinal {fields.ParameterIndex}.");
            CheckFinite(fields.BlendSharpnessBits, i, "BlendSharpness", issues);
            CheckScale(fields.ScaleUBits, i, "ScaleU", issues);
            CheckScale(fields.ScaleVBits, i, "ScaleV", issues);
            // COMISS/MAXSS can treat a denormal as zero depending on the original
            // MXCSR. Scale denormals always fall below the final 1.0 lower bound.
            if ((fields.BlendSharpnessBits & 0x7F800000) == 0 &&
                (fields.BlendSharpnessBits & 0x007FFFFF) != 0)
                issues.Add($"Palette row {i} BlendSharpness is subnormal; original MXCSR is unresolved.");
        }
        if (issues.Count != 0) return new([], issues.AsReadOnly());

        var projected = new TerrainRendererPaletteEntry[entries.Count];
        float maximum = BitConverter.UInt32BitsToSingle(MaximumBlendSharpnessBits);
        for (int i = 0; i < projected.Length; i++)
        {
            TerrainDetailPaletteEntry entry = entries[i];
            TerrainPaletteEntry fields = entry.Fields;
            float sharpness = BitConverter.UInt32BitsToSingle(fields.BlendSharpnessBits);
            uint sharpnessBits = sharpness >= maximum ? MaximumBlendSharpnessBits
                : sharpness < 0 ? 0u : fields.BlendSharpnessBits;
            // MAXSS chooses its second operand for equal zeros, retaining -0.
            projected[i] = new(i, entry.DetailTextureNameIndex, sharpnessBits,
                Scale(fields.ScaleUBits), Scale(fields.ScaleVBits), fields.TessScaleBits, fields.TessBiasBits);
        }
        return new(Array.AsReadOnly(projected), []);
    }

    /// <summary>
    /// Encodes only the addressed prefixes of six columns in slot 4–9 order.
    /// No guessed padding or initialization of the unused native 255-row tail.
    /// </summary>
    public static byte[] EncodeColumnPrefixes(TerrainRendererPaletteProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (!projection.Resolved)
            throw new InvalidDataException("Terrain renderer palette projection is unresolved: " +
                string.Join(" ", projection.UnresolvedInputs));
        byte[] bytes = new byte[checked(projection.Entries.Count * 6 * sizeof(uint))];
        int columnBytes = projection.Entries.Count * sizeof(uint);
        for (int i = 0; i < projection.Entries.Count; i++)
        {
            TerrainRendererPaletteEntry row = projection.Entries[i];
            Write(bytes, i * 4, row.DetailTextureNameIndex);
            Write(bytes, columnBytes + i * 4, row.BlendSharpnessBits);
            Write(bytes, columnBytes * 2 + i * 4, row.ScaleUBits);
            Write(bytes, columnBytes * 3 + i * 4, row.ScaleVBits);
            Write(bytes, columnBytes * 4 + i * 4, row.TessScaleBits);
            Write(bytes, columnBytes * 5 + i * 4, row.TessBiasBits);
        }
        return bytes;
    }

    private static uint Scale(uint bits)
    {
        float product = BitConverter.UInt32BitsToSingle(bits) * 100f;
        return BitConverter.SingleToUInt32Bits(product < 1f ? 1f : product);
    }

    private static void CheckFinite(uint bits, int row, string name, List<string> issues)
    {
        if (!float.IsFinite(BitConverter.UInt32BitsToSingle(bits)))
            issues.Add($"Palette row {row} {name} is nonfinite; original exceptional arithmetic is unresolved.");
    }

    private static void CheckScale(uint bits, int row, string name, List<string> issues)
    {
        CheckFinite(bits, row, name, issues);
        float value = BitConverter.UInt32BitsToSingle(bits);
        if (float.IsFinite(value) && !float.IsFinite(value * 100f))
            issues.Add($"Palette row {row} {name} overflows float32 multiplication; original exception state is unresolved.");
    }

    private static void Write(Span<byte> bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)), value);
}
