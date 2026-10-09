using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record FloatTexture(int Width, int Height, float[] Values)
{
    public float GetSample(int column, int row)
    {
        if ((uint)column >= Width || (uint)row >= Height)
            throw new ArgumentOutOfRangeException(nameof(column), "Float texture sample is outside the image.");
        return Values[row * Width + column];
    }
}

/// <summary>
/// Decodes type-02 little-endian scalar float storage, verified against both complete terrain height maps.
/// Values are preserved without color conversion, height normalization, interpolation or axis changes.
/// </summary>
public static class G1FloatTextureReader
{
    public static FloatTexture Read(G1TextureImage image, ReadOnlySpan<byte> source, int mipLevel = 0, int layer = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.PixelFormat != 0x02)
            throw new InvalidDataException($"G1T scalar float decoding requires type 02, received {image.PixelFormat:X2}.");
        ReadOnlySpan<byte> encoded = image.GetEncodedMip(source, mipLevel, layer);
        G1TextureMip mip = image.Mips[mipLevel];
        if (mip.Width <= 0 || mip.Height <= 0 || (long)mip.Width * mip.Height * 4 != encoded.Length)
            throw new InvalidDataException("G1T scalar float dimensions do not cover the encoded mip.");
        var values = new float[encoded.Length / 4];
        for (int i = 0; i < values.Length; i++)
        {
            float value = BinaryPrimitives.ReadSingleLittleEndian(encoded.Slice(i * 4, 4));
            if (!float.IsFinite(value)) throw new InvalidDataException($"G1T scalar float sample {i} is not finite.");
            values[i] = value;
        }
        return new(mip.Width, mip.Height, values);
    }
}
