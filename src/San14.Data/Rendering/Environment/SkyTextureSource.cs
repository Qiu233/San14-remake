using San14.Data.Formats;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record SkyTextureIdentity(string ResourceUri, string SourceSha256,
    int ImageIndex, uint NativeViewFormat);

/// <summary>Views and sampler bindings borrow one immutable image imported from
/// their resource container. Callers receive detached bytes; a new container
/// starts a separate import lifetime.</summary>
public sealed class SkyTextureSource
{
    private static readonly ConditionalWeakTable<LoadedTextureContainer, ImportedContainer> Imported = new();
    private readonly byte[] encodedChain, sourceSampler, sampler;
    private readonly (int Offset, int Length)[] mipRanges;
    private readonly Lazy<IReadOnlyList<string>> mipHashes;
    public SkyTextureIdentity Identity { get; }
    public string ResourceUri => Identity.ResourceUri;
    public string SourceSha256 => Identity.SourceSha256;
    public int ImageIndex => Identity.ImageIndex;
    public uint NativeViewFormat => Identity.NativeViewFormat;
    public byte PixelFormat { get; }
    public int Width { get; }
    public int Height { get; }
    public int Depth { get; }
    public int MipCount { get; }
    public uint SourceDimension { get; }
    public string SamplerDescriptorSha256 { get; }
    public IReadOnlyList<string> MipSha256 => mipHashes.Value;

    private SkyTextureSource(SkyTextureIdentity identity, G1TextureImage image,
        byte[] chain, byte[] originalSampler,
        (int Offset, int Length)[] mipRanges)
    {
        Identity=identity;PixelFormat=image.PixelFormat;Width=image.Width;Height=image.Height;
        Depth=image.Depth;MipCount=image.MipCount;SourceDimension=image.TextureSystem==2?8u:4u;
        encodedChain=chain;sourceSampler=originalSampler;sampler=originalSampler;
        SamplerDescriptorSha256=Convert.ToHexString(SHA256.HashData(sampler));
        this.mipRanges = mipRanges;
        mipHashes = new(() => Array.AsReadOnly(mipRanges.Select(range =>
            Convert.ToHexString(SHA256.HashData(encodedChain.AsSpan(range.Offset, range.Length)))).ToArray()));
    }

    private SkyTextureSource(SkyTextureSource image, uint nativeViewFormat, byte[]? retainedSampler = null)
    {
        Identity = image.Identity with { NativeViewFormat = nativeViewFormat };
        PixelFormat = image.PixelFormat; Width = image.Width; Height = image.Height;
        Depth = image.Depth; MipCount = image.MipCount; SourceDimension = image.SourceDimension;
        encodedChain = image.encodedChain; sourceSampler = image.sourceSampler;
        mipRanges = image.mipRanges; mipHashes = image.mipHashes;
        sampler = retainedSampler ?? sourceSampler;
        SamplerDescriptorSha256 = retainedSampler is null ? image.SamplerDescriptorSha256
            : Convert.ToHexString(SHA256.HashData(sampler));
    }

    public static SkyTextureSource Import(LoadedTextureContainer source, int imageIndex,
        uint nativeViewFormat, ReadOnlyMemory<byte>? retainedSampler=null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Imported.GetValue(source, static _ => new()).Import(source, imageIndex, nativeViewFormat, retainedSampler);
    }

    private sealed class ImportedContainer
    {
        private readonly object gate = new();
        private readonly Dictionary<int, SkyTextureSource> images = new();
        private readonly Dictionary<(int ImageIndex, uint ViewFormat), SkyTextureSource> views = new();

        public SkyTextureSource Import(LoadedTextureContainer source, int imageIndex,
            uint nativeViewFormat, ReadOnlyMemory<byte>? retainedSampler)
        {
            lock (gate)
            {
                if (!images.TryGetValue(imageIndex, out var image))
                {
                    image = ImportImage(source, imageIndex, nativeViewFormat, retainedSampler);
                    images.Add(imageIndex, image);
                    views.Add((imageIndex, nativeViewFormat), image);
                }
                else
                {
                    RequireView(image.PixelFormat, nativeViewFormat);
                    RequireSampler(retainedSampler);
                }
                if (retainedSampler is { } descriptor)
                    return new(image, nativeViewFormat, descriptor.ToArray());
                if (!views.TryGetValue((imageIndex, nativeViewFormat), out var view))
                {
                    view = new(image, nativeViewFormat);
                    views.Add((imageIndex, nativeViewFormat), view);
                }
                return view;
            }
        }
    }

    private static SkyTextureSource ImportImage(LoadedTextureContainer source, int imageIndex,
        uint nativeViewFormat, ReadOnlyMemory<byte>? retainedSampler)
    {
        if((uint)imageIndex>=source.Container.Textures.Count)
            throw new InvalidDataException("Sky texture image index is outside its parsed container.");
        var image=source.Container.Textures[imageIndex];
        if(image.TextureSystem is not (0 or 2 or 3) || image.LayerCount!=1 || image.Width<=0 || image.Height<=0 || image.Depth<=0 ||
            image.MipCount<1 || image.Mips.Count!=image.MipCount ||
            (image.TextureSystem==2
                ? image.SubresourceLayout!=G1TextureSubresourceLayout.VolumeMipChain
                : image.Depth!=1 || image.SubresourceLayout!=G1TextureSubresourceLayout.SingleLayerMipChain))
            throw new InvalidDataException("Sky texture requires a complete planar or volume mip chain.");
        RequireView(image.PixelFormat, nativeViewFormat);
        RequireSampler(retainedSampler);
        byte[] chain=image.EncodedDataRange.Slice(source.Source.Bytes).ToArray();
        var ranges = new (int Offset, int Length)[image.MipCount];
        int position=0;
        for(int level=0;level<image.MipCount;level++)
        {
            var mip=image.Mips[level];var range=mip.EncodedRange;
            if(mip.Level!=level || range is null || range.Length<=0 ||
                range.Offset!=(long)image.EncodedDataRange.Offset+position ||
                range.Length>chain.Length-position)
                throw new InvalidDataException("Sky texture mip ranges do not cover their encoded chain.");
            ranges[level] = (position, range.Length);
            position+=range.Length;
        }
        if(position!=chain.Length)
            throw new InvalidDataException("Sky texture contains bytes outside its selected mip chain.");
        byte[] originalSampler=G1TextureSamplerReader.ReadRetainedSource(image).EncodeD3D11Descriptor();
        return new(new(source.Source.ResourceUri,source.Source.DecodedSha256,imageIndex,nativeViewFormat),
            image,chain,originalSampler,ranges);
    }

    private static void RequireView(byte pixelFormat, uint nativeViewFormat)
    {
        if ((pixelFormat, nativeViewFormat) is not
            ((0x01,87) or (0x01,91) or (0x03,10) or (0x06,71) or (0x06,72) or (0x59,71) or
             (0x59,72) or (0x5F,98) or (0x5F,99)))
            throw new InvalidDataException("Sky texture encoding does not match its selected native view.");
    }

    private static void RequireSampler(ReadOnlyMemory<byte>? retainedSampler)
    {
        if (retainedSampler is { Length: not 52 })
            throw new InvalidDataException("Sky texture requires a complete 52-byte retained sampler.");
    }

    public byte[] GetEncodedChain() => encodedChain.ToArray();
    /// <summary>Returns the selected encoded mip from the ranges imported
    /// with this image. The caller owns the returned bytes.</summary>
    public byte[] GetEncodedMip(int level)
    {
        if ((uint)level >= mipRanges.Length)
            throw new ArgumentOutOfRangeException(nameof(level));
        var range = mipRanges[level];
        return encodedChain.AsSpan(range.Offset, range.Length).ToArray();
    }
    public byte[] GetSamplerDescriptor() => sampler.ToArray();
    public byte[] GetSourceSamplerDescriptor() => sourceSampler.ToArray();
}
