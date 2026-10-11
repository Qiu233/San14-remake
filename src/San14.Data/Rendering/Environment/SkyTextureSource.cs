using San14.Data.Formats;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record SkyTextureIdentity(string ResourceUri, string SourceSha256,
    int ImageIndex, uint NativeViewFormat);

/// <summary>One imported image owns its encoded mip chain and samplers. Parsed
/// resource graphs stay at the import boundary; callers receive detached bytes.</summary>
public sealed class SkyTextureSource
{
    private readonly byte[] encodedChain, sourceSampler, sampler;
    private readonly (int Offset, int Length)[] mipRanges;
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
    public IReadOnlyList<string> MipSha256 { get; }

    private SkyTextureSource(SkyTextureIdentity identity, G1TextureImage image,
        byte[] chain, byte[] originalSampler, byte[] effectiveSampler, string[] mipHashes,
        (int Offset, int Length)[] mipRanges)
    {
        Identity=identity;PixelFormat=image.PixelFormat;Width=image.Width;Height=image.Height;
        Depth=image.Depth;MipCount=image.MipCount;SourceDimension=image.TextureSystem==2?8u:4u;
        encodedChain=chain;sourceSampler=originalSampler;sampler=effectiveSampler;
        SamplerDescriptorSha256=Convert.ToHexString(SHA256.HashData(sampler));
        MipSha256=Array.AsReadOnly(mipHashes);
        this.mipRanges = mipRanges;
    }

    public static SkyTextureSource Import(LoadedTextureContainer source, int imageIndex,
        uint nativeViewFormat, ReadOnlyMemory<byte>? retainedSampler=null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if((uint)imageIndex>=source.Container.Textures.Count)
            throw new InvalidDataException("Sky texture image index is outside its parsed container.");
        var image=source.Container.Textures[imageIndex];
        if(image.TextureSystem is not (0 or 2 or 3) || image.LayerCount!=1 || image.Width<=0 || image.Height<=0 || image.Depth<=0 ||
            image.MipCount<1 || image.Mips.Count!=image.MipCount ||
            (image.TextureSystem==2
                ? image.SubresourceLayout!=G1TextureSubresourceLayout.VolumeMipChain
                : image.Depth!=1 || image.SubresourceLayout!=G1TextureSubresourceLayout.SingleLayerMipChain))
            throw new InvalidDataException("Sky texture requires a complete planar or volume mip chain.");
        if((image.PixelFormat,nativeViewFormat) is not
            ((0x01,87) or (0x01,91) or (0x03,10) or (0x06,71) or (0x06,72) or (0x59,71) or
             (0x59,72) or (0x5F,98) or (0x5F,99)))
            throw new InvalidDataException("Sky texture encoding does not match its selected native view.");
        if(retainedSampler is {Length:not 52})
            throw new InvalidDataException("Sky texture requires a complete 52-byte retained sampler.");
        byte[] chain=image.EncodedDataRange.Slice(source.Source.Bytes).ToArray();
        var hashes=new string[image.MipCount];
        var ranges = new (int Offset, int Length)[image.MipCount];
        int position=0;
        for(int level=0;level<image.MipCount;level++)
        {
            var mip=image.Mips[level];var range=mip.EncodedRange;
            if(mip.Level!=level || range is null || range.Length<=0 ||
                range.Offset!=(long)image.EncodedDataRange.Offset+position ||
                range.Length>chain.Length-position)
                throw new InvalidDataException("Sky texture mip ranges do not cover their encoded chain.");
            hashes[level]=Convert.ToHexString(SHA256.HashData(chain.AsSpan(position,range.Length)));
            ranges[level] = (position, range.Length);
            position+=range.Length;
        }
        if(position!=chain.Length)
            throw new InvalidDataException("Sky texture contains bytes outside its selected mip chain.");
        byte[] originalSampler=G1TextureSamplerReader.ReadRetainedSource(image).EncodeD3D11Descriptor();
        byte[] sampler=retainedSampler?.ToArray()??originalSampler.ToArray();
        return new(new(source.Source.ResourceUri,source.Source.DecodedSha256,imageIndex,nativeViewFormat),
            image,chain,originalSampler,sampler,hashes,ranges);
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
