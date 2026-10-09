using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct DeferredAuxiliaryExtent(int Width, int Height);
public sealed record DeferredAuxiliarySurface(uint SurfaceId, uint MipLevel,
    DeferredAuxiliaryExtent Extent);
public sealed record DeferredAuxiliaryTargetSource(RenderTextureDefinition Texture,
    DeferredAuxiliaryExtent NativeFrameExtent, DeferredAuxiliaryExtent Extent,
    uint MostDetailedMip, uint ViewMipCount, IReadOnlyList<DeferredAuxiliarySurface> Surfaces)
{
    public uint MipCount => Texture.LevelNum;
}

/// <summary>Actual four F103 auxiliary declarations plus the paired native
/// relative-size and full-mip2D creator rules. Allocation establishes no content.</summary>
public static class DeferredAuxiliaryTargetSources
{
    private sealed record LoadedDeclarations(IReadOnlyDictionary<uint, RenderTextureDefinition> Textures);
    private static readonly ConditionalWeakTable<LoadedRdbAsset, LoadedDeclarations> Sources = new();
    public const string ActualObjectsResourceUri="san14://rdb/system/93FB3280";
    public const string ActualObjectsSha256="4082FCB18D13F992407B2168F0105B9C86F53D095E2BA630802AD0BCDF19FF12";
    private static readonly (uint Id, int Width, int Height, byte Pixel, byte Levels)[] Actual =
    [ (0x45A0F6C7, -1, -1, 0x6B, 7), (0x93637E41, 0, 0, 0x6B, 7),
      (0x18C86C69, -1, -1, 0x02, 1), (0xB6169FDB, 0, 0, 0x4E, 1) ];
    private static readonly (uint Id, int Width, int Height, byte Pixel, byte Levels)[] Intermediates =
    [ (0xDD50021F, -2, -2, 0x6B, 1), (0x13D2F0E5, -1, -1, 0x6B, 1),
      (0xD50F0811, -1, -1, 0x02, 1), (0xF07C5979, -1, -1, 0x6B, 1) ];

    public static IReadOnlyList<DeferredAuxiliaryTargetSource> Read(LoadedObjects objects,
        int nativeFrameWidth3E8, int nativeFrameHeight3EC)
        => ReadSet(objects, nativeFrameWidth3E8, nativeFrameHeight3EC, Actual);

    /// <summary>Exact node137 filter intermediates and node97 copy intermediate.
    /// These are source allocations and establish no completed pixel writes.</summary>
    public static IReadOnlyList<DeferredAuxiliaryTargetSource> ReadIntermediates(LoadedObjects objects,
        int nativeFrameWidth3E8, int nativeFrameHeight3EC)
        => ReadSet(objects, nativeFrameWidth3E8, nativeFrameHeight3EC, Intermediates);

    private static IReadOnlyList<DeferredAuxiliaryTargetSource> ReadSet(LoadedObjects objects,
        int nativeFrameWidth3E8, int nativeFrameHeight3EC,
        IReadOnlyList<(uint Id, int Width, int Height, byte Pixel, byte Levels)> declarations)
    {
        ArgumentNullException.ThrowIfNull(objects);
        if (nativeFrameWidth3E8 <= 0 || nativeFrameHeight3EC <= 0)
            throw new InvalidDataException("Auxiliary creation requires an explicit current native frame extent.");
        var loaded=Sources.GetValue(objects.Source, LoadDeclarations);
        var result = new List<DeferredAuxiliaryTargetSource>();
        foreach (var actual in declarations)
        {
            RenderTextureDefinition texture = loaded.Textures[actual.Id];
            var extent = new DeferredAuxiliaryExtent(ResolveNativeDimension(texture.Width, nativeFrameWidth3E8),
                ResolveNativeDimension(texture.Height, nativeFrameHeight3EC));
            if (extent.Width <= 0 || extent.Height <= 0 || texture.LevelNum > MaximumMipCount(extent))
                throw new InvalidDataException($"Native relative extent cannot allocate the declared complete mip chain: target={actual.Id:X8}, frame={nativeFrameWidth3E8}x{nativeFrameHeight3EC}, declared={texture.Width}x{texture.Height}, resolved={extent.Width}x{extent.Height}, levels={texture.LevelNum}.");
            var surfaces = Enumerable.Range(0, texture.LevelNum).Select(level => new DeferredAuxiliarySurface((uint)level,
                (uint)level, new(Math.Max(1, extent.Width >> level), Math.Max(1, extent.Height >> level)))).ToArray();
            result.Add(new(texture, new(nativeFrameWidth3E8, nativeFrameHeight3EC), extent, 0, texture.LevelNum,
                Array.AsReadOnly(surfaces)));
        }
        return result.AsReadOnly();
    }

    private static LoadedDeclarations LoadDeclarations(LoadedRdbAsset source)
    {
        if(source.OverrideSource is not null||source.ResourceUri!=ActualObjectsResourceUri||
            source.DecodedSha256!=ActualObjectsSha256||Convert.ToHexString(SHA256.HashData(source.Bytes))!=source.DecodedSha256)
            throw new InvalidDataException("Auxiliary creation requires exact original loaded objects.");
        var objects=new LoadedObjects(DokReader.Read(source.Bytes),source);
        var textures=new Dictionary<uint,RenderTextureDefinition>();
        foreach(var actual in Actual.Concat(Intermediates))
        {
            var texture=RenderTextureResources.Read(objects,actual.Id);
            if(texture.Kind!=RenderTextureKind.Texture2D||texture.Width!=actual.Width||texture.Height!=actual.Height||
                texture.PixelFormat!=actual.Pixel||texture.LevelNum!=actual.Levels||texture.MinMipLevel!=0||
                texture.MultiSample!=0||texture.ColorSpace!=0||texture.OutputColorSpace!=(actual.Pixel is 0x02 or 0x4E?2:0))
                throw new InvalidDataException("Auxiliary declaration differs from its exact source recipe.");
            _=RenderTextureFormatMath.Resolve(texture);
            textures.Add(actual.Id,texture);
        }
        return new(textures);
    }

    /// <summary>C06F04/C083A4:0 inherits backend3E8/3EC; negative uses the
    /// low byte of its negation as a logical32 shift count. No upward rounding.</summary>
    public static int ResolveNativeDimension(int declared, int currentNativeDimension)
    {
        if (currentNativeDimension <= 0) throw new InvalidDataException("Current native frame dimension is unknown or invalid.");
        return declared > 0 ? declared : declared == 0 ? currentNativeDimension :
            unchecked((int)((uint)currentNativeDimension >> (unchecked((byte)-declared) & 31)));
    }
    private static uint MaximumMipCount(DeferredAuxiliaryExtent extent)
    {
        uint result = 1;
        for (int largest = Math.Max(extent.Width, extent.Height); largest > 1; largest >>= 1) result++;
        return result;
    }
    public static void Validate(DeferredAuxiliaryTargetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var declarations = Actual.Concat(Intermediates).ToArray();
        DeferredAuxiliaryTargetSource? canonical = ReadSet(source.Texture.Source, source.NativeFrameExtent.Width,
            source.NativeFrameExtent.Height, declarations).SingleOrDefault(value => value.Texture.Object.ObjectId == source.Texture.Object.ObjectId);
        if (canonical is null)
            throw new InvalidDataException("Auxiliary creation is outside the finite original source declarations.");
        if (canonical.Extent != source.Extent || canonical.MostDetailedMip != source.MostDetailedMip ||
            canonical.ViewMipCount != source.ViewMipCount || canonical.MipCount != source.MipCount ||
            !canonical.Surfaces.SequenceEqual(source.Surfaces) ||
            canonical.Texture.Object.ClassId != source.Texture.Object.ClassId ||
            canonical.Texture.Object.Kind != source.Texture.Object.Kind ||
            canonical.Texture with { Source=source.Texture.Source, Object=source.Texture.Object } != source.Texture)
            throw new InvalidDataException("Auxiliary creation recipe was changed after exact source parsing.");
    }
}
