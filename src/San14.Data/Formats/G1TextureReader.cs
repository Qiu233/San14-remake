using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record RgbaTexture(int Width, int Height, byte[] Pixels);

/// <summary>Decodes supported G1T texture types into ordinary RGBA pixels.</summary>
public static class G1TextureReader
{
    public static RgbaTexture Decode(ReadOnlySpan<byte> data, int textureIndex = 0)
    {
        if (data.Length < 32 || !data[..4].SequenceEqual("GT1G"u8))
            throw new InvalidDataException("G1T header is missing.");
        int declaredLength = ReadInt(data, 8);
        int tableOffset = ReadInt(data, 12);
        int count = ReadInt(data, 16);
        if (declaredLength != data.Length || count < 1 || count > 10_000 ||
            textureIndex < 0 || textureIndex >= count || tableOffset < 28 ||
            tableOffset > data.Length - 4L * count)
            throw new InvalidDataException("G1T header size, table, or texture index is invalid.");
        int offset = ReadInt(data, tableOffset + textureIndex * 4);
        int endOffset = textureIndex + 1 < count
            ? ReadInt(data, tableOffset + (textureIndex + 1) * 4)
            : data.Length - tableOffset;
        if (offset < count * 4 || endOffset <= offset || endOffset > data.Length - tableOffset)
            throw new InvalidDataException("G1T texture offset is outside the file.");
        ReadOnlySpan<byte> image = data.Slice(tableOffset + offset, endOffset - offset);
        if (image.Length < 8) throw new InvalidDataException("G1T texture header is truncated.");
        if ((image[0] & 15) != 0 || image[3] != 0)
            throw new InvalidDataException("G1T pixel decoding supports ordinary 2D textures only; texture system or depth is unsupported.");
        int width = 1 << (image[2] & 15);
        int height = 1 << (image[2] >> 4);
        int headerLength = 8;
        int extraCount = image[7] >> 4;
        if (extraCount > 1)
            throw new InvalidDataException("G1T pixel decoding does not support multiple extra headers.");
        if (extraCount != 0)
        {
            if (image.Length < 12) throw new InvalidDataException("G1T extended header is truncated.");
            int extra = ReadInt(image, 8);
            if (extra is not (12 or 16 or 20) || extra > image.Length - 8)
                throw new InvalidDataException("G1T extended header length is invalid.");
            headerLength += extra;
            ushort facesAndArrays = BinaryPrimitives.ReadUInt16LittleEndian(image[16..18]);
            if (facesAndArrays != 0 || image[18] != 0)
                throw new InvalidDataException("G1T pixel decoding does not support faces, arrays or swizzle.");
            if (extra >= 16) width = ExplicitDimension(image, 20, width);
            if (extra >= 20) height = ExplicitDimension(image, 24, height);
        }
        if (width > 8192 || height > 8192 || (long)width * height > 32_000_000)
            throw new InvalidDataException("G1T texture dimensions exceed the supported limit.");
        ReadOnlySpan<byte> encoded = image[headerLength..];
        byte[] pixels = new byte[checked(width * height * 4)];
        switch (image[1])
        {
            case 0x00: // RGBA8.
            case 0x01: // BGRA8 in installed Windows GT1G0600 resources.
                if (encoded.Length < pixels.Length)
                    throw new InvalidDataException("G1T RGBA image is truncated.");
                encoded[..pixels.Length].CopyTo(pixels);
                if (image[1] == 0x01)
                    for (int i = 0; i < pixels.Length; i += 4)
                        (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                break;
            case 0x59: // BC1
                DecodeBlocks(encoded, pixels, width, height, false);
                break;
            case 0x5B: // Installed ScreenLayout textures use 16-byte BC3-compatible blocks.
                DecodeBlocks(encoded, pixels, width, height, true);
                break;
            default:
                throw new InvalidDataException($"Unsupported G1T texture type {image[1]:X2}.");
        }
        return new RgbaTexture(width, height, pixels);
    }

    private static void DecodeBlocks(ReadOnlySpan<byte> source, byte[] destination,
        int width, int height, bool withAlpha)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        int blockSize = withAlpha ? 16 : 8;
        if (source.Length < (long)blocksX * blocksY * blockSize)
            throw new InvalidDataException("G1T block-compressed image is truncated.");
        Span<byte> alpha = stackalloc byte[8];
        Span<byte> palette = stackalloc byte[16];
        for (int blockY = 0; blockY < blocksY; blockY++)
        for (int blockX = 0; blockX < blocksX; blockX++)
        {
            ReadOnlySpan<byte> block = source.Slice((blockY * blocksX + blockX) * blockSize, blockSize);
            if (withAlpha)
            {
                alpha[0] = block[0];
                alpha[1] = block[1];
                if (alpha[0] > alpha[1])
                    for (int i = 1; i <= 6; i++)
                        alpha[i + 1] = (byte)(((7 - i) * alpha[0] + i * alpha[1]) / 7);
                else
                {
                    for (int i = 1; i <= 4; i++)
                        alpha[i + 1] = (byte)(((5 - i) * alpha[0] + i * alpha[1]) / 5);
                    alpha[6] = 0;
                    alpha[7] = 255;
                }
                block = block[8..];
            }
            ushort color0 = BinaryPrimitives.ReadUInt16LittleEndian(block);
            ushort color1 = BinaryPrimitives.ReadUInt16LittleEndian(block[2..]);
            Expand565(color0, palette);
            Expand565(color1, palette[4..]);
            bool fourColors = withAlpha || color0 > color1;
            for (int channel = 0; channel < 3; channel++)
            {
                palette[8 + channel] = (byte)((fourColors
                    ? 2 * palette[channel] + palette[4 + channel]
                    : palette[channel] + palette[4 + channel]) / (fourColors ? 3 : 2));
                palette[12 + channel] = (byte)(fourColors
                    ? (palette[channel] + 2 * palette[4 + channel]) / 3
                    : 0);
            }
            palette[3] = palette[7] = palette[11] = 255;
            palette[15] = fourColors ? (byte)255 : (byte)0;
            uint colorIndices = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);
            ulong alphaIndices = withAlpha
                ? (ulong)source[(blockY * blocksX + blockX) * blockSize + 2]
                : 0;
            if (withAlpha)
                for (int i = 1; i < 6; i++)
                    alphaIndices |= (ulong)source[(blockY * blocksX + blockX) * blockSize + 2 + i] << (8 * i);
            for (int pixel = 0; pixel < 16; pixel++)
            {
                int x = blockX * 4 + pixel % 4;
                int y = blockY * 4 + pixel / 4;
                if (x >= width || y >= height) continue;
                int paletteOffset = (int)((colorIndices >> (pixel * 2)) & 3) * 4;
                int destinationOffset = (y * width + x) * 4;
                destination[destinationOffset] = palette[paletteOffset];
                destination[destinationOffset + 1] = palette[paletteOffset + 1];
                destination[destinationOffset + 2] = palette[paletteOffset + 2];
                destination[destinationOffset + 3] = withAlpha
                    ? alpha[(int)((alphaIndices >> (pixel * 3)) & 7)]
                    : palette[paletteOffset + 3];
            }
        }
    }

    private static void Expand565(ushort value, Span<byte> rgba)
    {
        int r = (value >> 11) & 31;
        int g = (value >> 5) & 63;
        int b = value & 31;
        rgba[0] = (byte)((r << 3) | (r >> 2));
        rgba[1] = (byte)((g << 2) | (g >> 4));
        rgba[2] = (byte)((b << 3) | (b >> 2));
    }

    private static int ReadInt(ReadOnlySpan<byte> data, int at) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Slice(at, 4));

    private static int ExplicitDimension(ReadOnlySpan<byte> data, int at, int fallback)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(at, 4));
        if (value > int.MaxValue)
            throw new InvalidDataException("G1T explicit dimension exceeds the supported integer range.");
        return value == 0 ? fallback : (int)value;
    }
}
