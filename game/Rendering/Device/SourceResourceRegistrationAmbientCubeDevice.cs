using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Security.Cryptography;

/// <summary>The exact Game HDRI static5A53AD58 BC6H cube. Source mip-major
/// face blocks are gathered into six device face mip chains without decoding.
/// This owns its texture and source sampler; it is independent of SkyCubeRTs.</summary>
public sealed class SourceResourceRegistrationAmbientCubeDevice : IAsyncDisposable
{
    public StaticTextureBinding Source { get; }
    public G1TextureImage Image { get; }
    public uint NativeViewFormat => 95;
    public Rid TextureRid { get; }
    public Rid SamplerRid { get; }
    public IReadOnlyList<string> SubresourceSha256 { get; }
    public string SourceProvenance { get; }
    private readonly bool allowReadback;
    private readonly object lifetimeGate=new();
    private Task? disposal;

    private SourceResourceRegistrationAmbientCubeDevice(StaticTextureBinding source,
        G1TextureImage image,Rid texture,Rid sampler,string[] hashes,string provenance,bool readback)
        =>(Source,Image,TextureRid,SamplerRid,SubresourceSha256,SourceProvenance,allowReadback)=
            (source,image,texture,sampler,Array.AsReadOnly(hashes),provenance,readback);

    public static async Task<SourceResourceRegistrationAmbientCubeDevice> BuildAsync(
        StaticTextureBinding source,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(source);
        SourceResourceRegistrationAmbientCube.Validate(source);
        var image=SourceResourceRegistrationAmbientCube.ReadGameDefault(
            source.Definitions,source.Resource.Source).Image;
        if(image.TextureSystem!=1||image.LayerCount!=6||image.PixelFormat!=0x5E||image.Width!=512||
            image.Height!=512||image.Depth!=1||image.MipCount!=7||
            image.SubresourceLayout!=G1TextureSubresourceLayout.MipMajorArray)
            throw new InvalidDataException("Static ambient cube differs from its exact512 BC6H seven-mip source recipe.");
        var hashes=new string[42];var faces=new Godot.Collections.Array<byte[]>();
        for(int face=0;face<6;face++)
        {
            using var chain=new MemoryStream();
            for(int mip=0;mip<7;mip++)
            {
                var encoded=image.GetEncodedMip(source.Resource.Source.Bytes,mip,face);
                int width=512>>mip;
                if(encoded.Length!=((width+3)/4)*((width+3)/4)*16)
                    throw new InvalidDataException("Static ambient cube block byte count differs from its native subresource.");
                hashes[mip+face*7]=Convert.ToHexString(SHA256.HashData(encoded));chain.Write(encoded);
            }
            faces.Add(chain.ToArray());
        }
        byte[] samplerBytes=SourceResourceRegistrationAmbientCube.GetSamplerDescriptor(source);
        string provenance=$"{source.Definitions.Source.ResourceUri}@{source.Definitions.Source.DecodedSha256}#object={source.Definition.ObjectId:X8};"+
            $"{source.Resource.Source.ResourceUri}@{source.Resource.Source.DecodedSha256}/TID={source.ImageIndex};native-SRV95;mip-major-to-face-chains;"+
            $"sampler-sha256={Convert.ToHexString(SHA256.HashData(samplerBytes))}";
        var made=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            const RenderingDevice.DataFormat format=RenderingDevice.DataFormat.Bc6HUfloatBlock;
            var usage=RenderingDevice.TextureUsageBits.SamplingBit;
            if(allowReadback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
            if(!rd.TextureIsFormatSupportedForUsage(format,usage))
                throw new NotSupportedException("Device cannot retain the exact native BC6H unsigned static cube.");
            Rid texture=default,sampler=default;
            try
            {
                using var shape=new RDTextureFormat{Format=format,Width=512,Height=512,Depth=1,ArrayLayers=6,
                    Mipmaps=7,TextureType=RenderingDevice.TextureType.Cube,UsageBits=usage};
                using var view=new RDTextureView();texture=rd.TextureCreate(shape,view,faces);
                if(!texture.IsValid)throw new InvalidDataException("Device rejected the original compressed ambient cube.");
                using var actual=rd.TextureGetFormat(texture);
                if(actual.Format!=format||actual.Width!=512||actual.Height!=512||actual.Depth!=1||
                    actual.ArrayLayers!=6||actual.Mipmaps!=7||actual.TextureType!=RenderingDevice.TextureType.Cube)
                    throw new InvalidDataException("Device changed the original static ambient cube encoding or layout.");
                sampler=DeviceSampler.Create(rd,samplerBytes);
                if(!sampler.IsValid)throw new InvalidDataException("Device rejected the original static cube source sampler.");
                return(texture,sampler);
            }
            catch
            {if(sampler.IsValid)rd.FreeRid(sampler);if(texture.IsValid)rd.FreeRid(texture);throw;}
        });
        return new(source,image,made.texture,made.sampler,hashes,provenance,allowReadback);
    }

    public Task<byte[]> ReadEncodedFaceAsync(uint face)
    {
        lock(lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(disposal is not null,this);
            if(!allowReadback)throw new InvalidOperationException("Encoded source cube readback requires its explicit validation flag.");
            if(face>=6)throw new ArgumentOutOfRangeException(nameof(face));
            return TerrainDeviceTexture.OnRenderThreadAsync(rd=>rd.TextureGetData(TextureRid,face));
        }
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetimeGate)
            return new(disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {rd.FreeRid(SamplerRid);rd.FreeRid(TextureRid);return true;}));
    }
}
