using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One original CubeRT declaration. Width is its only source extent;
/// the native loader creates one square cube with six faces.</summary>
public sealed record CubeRenderTextureDefinition(LoadedObjects Source, DokObject Object,
    int Width, float MipLodBias, byte LevelNum, byte MinMipLevel, byte PixelFormat,
    byte ColorSpace, byte MultiSample, byte AddressU, byte AddressV, byte MagFilter,
    byte MinFilter, byte MipFilter, byte AnisoSampleNum, byte LocationHint, byte OutputColorSpace);

public readonly record struct CubeRenderSurface(uint Face, uint MipLevel, uint Extent);

public static class CubeRenderTextureResources
{
    public const uint ClassId = 0xF4ADC3E6;

    public static CubeRenderTextureDefinition Read(LoadedObjects source, uint objectNameHash)
    {
        ArgumentNullException.ThrowIfNull(source);
        DokObject[] matches = source.Document.Objects.Where(o => o.ObjectId == objectNameHash).ToArray();
        if (objectNameHash == 0 || matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != ClassId)
            throw new InvalidDataException($"{source.Source.ResourceUri}: cube target {objectNameHash:X8} requires one inline F4ADC3E6 object.");
        DokObject obj = matches[0];
        string context = $"{source.Source.ResourceUri} cube {objectNameHash:X8}";
        ReadOnlySpan<byte> Field(uint name, uint type, int width)
        {
            DokProperty[] rows = obj.Properties.Where(p => p.NameHash == name).ToArray();
            if (rows.Length != 1 || rows[0].TypeCode != type || rows[0].ElementCount != 1 || rows[0].RawValue.Length != width)
                throw new InvalidDataException($"{context}: property {name:X8} requires one explicit typed scalar.");
            return rows[0].RawValue;
        }
        byte B(uint name) => Field(name, 1, 1)[0];
        return new(source, obj, BinaryPrimitives.ReadInt32LittleEndian(Field(0xB809B0DA, 4, 4)),
            BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(Field(0x766BCC06, 8, 4))),
            B(0x7744A442), B(0xC936F7DE), B(0x308EB4A3), B(0xC68F94C3), B(0x178FF7BD),
            B(0x5B1B8441), B(0xEF5FF342), B(0x62150DD5), B(0x62185A76), B(0x62194334),
            B(0x4A72D9C4), B(0x6619527C), B(0x8B4A54E4));
    }

    /// <summary>The native loader indexes RTVs as face*LevelNum+mip.
    /// Source MinMipLevel is retained but does not shift this loader's SRV/RTVs.</summary>
    public static CubeRenderSurface ResolveSurface(CubeRenderTextureDefinition source, uint surfaceId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width <= 0 || source.LevelNum == 0 || source.MultiSample != 0 || !float.IsFinite(source.MipLodBias))
            throw new InvalidDataException("Cube target has no supported finite single-sample extent/mip declaration.");
        int maximumLevels = 1 + System.Numerics.BitOperations.Log2((uint)source.Width);
        if (source.LevelNum > maximumLevels || surfaceId >= 6u * source.LevelNum)
            throw new InvalidDataException("Cube target SurfaceID or mip count exceeds the declared face layout.");
        uint mip = surfaceId % source.LevelNum;
        return new(surfaceId / source.LevelNum, mip, Math.Max(1u, (uint)source.Width >> (int)mip));
    }
}
