using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>The four storage bytes of one original Field slot 1 texel, before BGRA conversion.</summary>
public readonly record struct FieldIndexTexel(byte Byte0, byte Byte1, byte Byte2, byte Byte3);

/// <summary>
/// A lossless copy of the original single-layer Field slot 1 image. This type does
/// not assign palette, material-layer or shader-channel meanings to its bytes.
/// </summary>
public sealed class G1FieldIndexMap
{
    private readonly byte[] _rawMip;
    private readonly Lazy<string> rawMipSha256;

    internal G1FieldIndexMap(int width, int height, byte[] rawMip)
    {
        Width = width;
        Height = height;
        _rawMip = rawMip;
        rawMipSha256 = new(() => Convert.ToHexString(SHA256.HashData(_rawMip)));
    }

    public int Width { get; }
    public int Height { get; }
    public string RawMipSha256 => rawMipSha256.Value;

    public FieldIndexTexel GetTexel(int column, int row)
    {
        if ((uint)column >= (uint)Width || (uint)row >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(column), "Field index texel lies outside the source image.");
        int offset = checked((row * Width + column) * 4);
        return new(_rawMip[offset], _rawMip[offset + 1], _rawMip[offset + 2], _rawMip[offset + 3]);
    }

    public byte[] CopyRawMip() => _rawMip.ToArray();
}

public static class G1FieldIndexMapReader
{
    public static G1FieldIndexMap Read(G1TextureImage image, ReadOnlySpan<byte> container)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.PixelFormat != 0x01 || image.LayerCount != 1 || image.MipCount != 1 ||
            image.SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain ||
            image.Width <= 0 || image.Height <= 0)
            throw new InvalidDataException("Field index image requires one type-01 BGRA layer and one mip.");
        ReadOnlySpan<byte> encoded = image.GetEncodedMip(container, 0);
        if (encoded.Length != checked(image.Width * image.Height * 4))
            throw new InvalidDataException("Field index image byte count differs from its source dimensions.");
        return new(image.Width, image.Height, encoded.ToArray());
    }
}
