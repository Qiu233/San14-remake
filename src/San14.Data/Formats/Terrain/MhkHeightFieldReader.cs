using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record MhkHeightField(int CellWidth, int CellHeight, float SampleSpacing,
    float MinimumHeight, float MaximumHeight, uint[] PackedSamples, byte[] RawHeader)
{
    public int SampleWidth => CellWidth + 1;
    public int SampleHeight => CellHeight + 1;

    public uint GetPackedSample(int column, int row)
    {
        if ((uint)column >= SampleWidth || (uint)row >= SampleHeight)
            throw new ArgumentOutOfRangeException(nameof(column), "Height sample lies outside the source grid.");
        return PackedSamples[row * SampleWidth + column];
    }

    public uint GetQuantizedHeight(int column, int row) => GetPackedSample(column, row) >> 8;
    public byte GetRawFlags(int column, int row) => (byte)GetPackedSample(column, row);

    /// <summary>
    /// Reconstructs the normalized 24-bit height. Source LOD sample extrema agree
    /// within their decimal precision; native runtime floating-point rounding is unverified.
    /// </summary>
    public double GetHeight(int column, int row) => MinimumHeight +
        (GetQuantizedHeight(column, row) / 16777215.0) * ((double)MaximumHeight - MinimumHeight);

    /// <summary>
    /// Interpolates adjacent packed MHK samples at source spacing. The current
    /// CE terrain query selects the diagonal from the top-left sample's low
    /// byte. Runtime local-origin offsets and native float32 rounding remain
    /// outside this data-level adapter.
    /// </summary>
    public double InterpolateSurfaceHeight(double x, double z)
    {
        double column = x / SampleSpacing, row = z / SampleSpacing;
        if (!double.IsFinite(column) || !double.IsFinite(row) || column < 0 || row < 0 ||
            column > CellWidth || row > CellHeight)
            throw new InvalidDataException("Projected point lies outside the source MHK height grid.");
        int c = Math.Min((int)Math.Floor(column), CellWidth - 1);
        int r = Math.Min((int)Math.Floor(row), CellHeight - 1);
        double u = column - c, v = row - r;
        double a = GetHeight(c, r), b = GetHeight(c + 1, r);
        double other = GetHeight(c, r + 1), d = GetHeight(c + 1, r + 1);
        double result = GetRawFlags(c, r) == 0
            ? (u + v < 1 ? a * (1 - u - v) + b * u + other * v
                : d * (u + v - 1) + b * (1 - v) + other * (1 - u))
            : (v < u ? a * (1 - u) + b * (u - v) + d * v
                : a * (1 - v) + other * (v - u) + d * u);
        if (!double.IsFinite(result))
            throw new InvalidDataException("Interpolated source MHK height is not finite.");
        return result;
    }
}

/// <summary>
/// Reads the verified MHK0100 packed height grid. Low-byte sample flags are retained
/// without assigning gameplay or drawing semantics. Grid rows are not logical Hex rows.
/// </summary>
public static class MhkHeightFieldReader
{
    public static MhkHeightField Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 32 || !data[..8].SequenceEqual("_MHK0100"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[8..12]) != 10 ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[12..16]) != data.Length)
            throw new InvalidDataException("Unsupported MHK header or invalid total length.");
        int width = BinaryPrimitives.ReadUInt16LittleEndian(data[16..18]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(data[18..20]);
        long count = (width + 1L) * (height + 1L);
        if (width == 0 || height == 0 || 32 + count * 4 != data.Length)
            throw new InvalidDataException("MHK sample dimensions do not cover the source data.");
        float spacing = BinaryPrimitives.ReadSingleLittleEndian(data[20..24]);
        float minimum = BinaryPrimitives.ReadSingleLittleEndian(data[24..28]);
        float maximum = BinaryPrimitives.ReadSingleLittleEndian(data[28..32]);
        if (!float.IsFinite(spacing) || spacing <= 0 || !float.IsFinite(minimum) ||
            !float.IsFinite(maximum) || maximum < minimum)
            throw new InvalidDataException("MHK sample spacing or height range is invalid.");
        var samples = new uint[(int)count];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(32 + i * 4, 4));
        return new(width, height, spacing, minimum, maximum, samples, data[..32].ToArray());
    }
}
