using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public readonly record struct SpriteUv(float U, float V);
public readonly record struct SpriteRectangle(float X, float Y, float Width, float Height);

public sealed record SpriteDefinition(string Name, uint Value64,
    SpriteUv TopLeft, SpriteUv TopRight, SpriteUv BottomLeft, SpriteUv BottomRight,
    uint Value100)
{
    public SpriteRectangle GetRectangle(int textureWidth, int textureHeight)
    {
        const float tolerance = 0.00001f;
        if (textureWidth < 1 || textureHeight < 1 ||
            Math.Abs(TopLeft.V - TopRight.V) > tolerance ||
            Math.Abs(BottomLeft.V - BottomRight.V) > tolerance ||
            Math.Abs(TopLeft.U - BottomLeft.U) > tolerance ||
            Math.Abs(TopRight.U - BottomRight.U) > tolerance ||
            TopLeft.U < -tolerance || TopLeft.V < -tolerance ||
            BottomRight.U > 1 + tolerance || BottomRight.V > 1 + tolerance ||
            BottomRight.U <= TopLeft.U || BottomRight.V <= TopLeft.V)
            throw new InvalidDataException($"Sprite {Name} does not have supported rectangular UV coordinates.");
        return new SpriteRectangle(TopLeft.U * textureWidth, TopLeft.V * textureHeight,
            (BottomRight.U - TopLeft.U) * textureWidth,
            (BottomRight.V - TopLeft.V) * textureHeight);
    }
}

/// <summary>Reads the named UV records used by ScreenLayout sprite metadata.</summary>
public static class SpriteAtlasReader
{
    public const uint RdbTypeId = 0xF20DE437;
    private static readonly Encoding NameEncoding = new UTF8Encoding(false, true);

    public static IReadOnlyList<SpriteDefinition> Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
            throw new InvalidDataException("Sprite atlas header is truncated.");
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (count < 1 || bytes.Length != 4L + count * 104L)
            throw new InvalidDataException("Sprite atlas record count or length differs.");
        var sprites = new List<SpriteDefinition>(count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(4 + i * 104, 104);
            int end = row[..64].IndexOf((byte)0);
            if (end < 1)
                throw new InvalidDataException($"Sprite atlas name {i} is missing or unterminated.");
            string name = NameEncoding.GetString(row[..end]);
            if (!names.Add(name))
                throw new InvalidDataException($"Sprite atlas name {name} occurs more than once.");
            sprites.Add(new SpriteDefinition(name, BinaryPrimitives.ReadUInt32LittleEndian(row[64..]),
                ReadUv(row[68..]), ReadUv(row[76..]), ReadUv(row[84..]), ReadUv(row[92..]),
                BinaryPrimitives.ReadUInt32LittleEndian(row[100..])));
        }
        return sprites;
    }

    private static SpriteUv ReadUv(ReadOnlySpan<byte> bytes)
    {
        float u = BinaryPrimitives.ReadSingleLittleEndian(bytes);
        float v = BinaryPrimitives.ReadSingleLittleEndian(bytes[4..]);
        if (!float.IsFinite(u) || !float.IsFinite(v))
            throw new InvalidDataException("Sprite atlas UV coordinates are not finite.");
        return new SpriteUv(u, v);
    }
}
