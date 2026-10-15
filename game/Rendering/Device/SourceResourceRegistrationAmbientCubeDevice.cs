using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Security.Cryptography;

/// <summary>The default light's declared BC6H cube. Source mip-major
/// face blocks are gathered into six device face mip chains without decoding.
/// This owns its texture and source sampler; it is independent of SkyCubeRTs.</summary>
public sealed class SourceResourceRegistrationAmbientCubeDevice : IAsyncDisposable
{
    public StaticTextureBinding Source { get; }
    public G1TextureImage Image { get; }
    public uint NativeViewFormat => 95;
    public Rid TextureRid { get; }
    public Rid SamplerRid { get; }
    public IReadOnlyList<string> EncodedChainSha256 { get; }
    public string SourceProvenance { get; }
    private readonly bool allowReadback;
    private readonly RenderDeviceResources resources;
    private readonly object lifetimeGate=new();
    private readonly SemaphoreSlim gate=new(1,1);
    private volatile bool retiring;
    private Task? disposal;

    private SourceResourceRegistrationAmbientCubeDevice(StaticTextureBinding source,
        G1TextureImage image,Rid texture,Rid sampler,string[] hashes,string provenance,bool readback,RenderDeviceResources resources)
        =>(Source,Image,TextureRid,SamplerRid,EncodedChainSha256,SourceProvenance,allowReadback,this.resources)=
            (source,image,texture,sampler,Array.AsReadOnly(hashes),provenance,readback,resources);

    public static async Task<SourceResourceRegistrationAmbientCubeDevice> BuildAsync(
        StaticTextureBinding source,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(source);
        var image=source.Image;
        if(image.TextureSystem!=1||image.LayerCount!=6||image.PixelFormat!=0x5E||image.Width!=image.Height||
            image.Depth!=1||
            image.SubresourceLayout!=G1TextureSubresourceLayout.MipMajorArray)
            throw new NotSupportedException("Static ambient device requires a six-face BC6H unsigned cube mip chain.");
        var hashes=new string[6];var faces=new Godot.Collections.Array<byte[]>();
        for(int face=0;face<6;face++)
        {
            byte[] chain=image.GetEncodedLayerChain(source.Resource.Source.Bytes,face);
            hashes[face]=Convert.ToHexString(SHA256.HashData(chain));
            faces.Add(chain);
        }
        byte[] samplerBytes=SourceResourceRegistrationAmbientCube.GetSamplerDescriptor(source);
        string provenance=$"{source.Definitions.Source.ResourceUri}@{source.Definitions.Source.DecodedSha256}#object={source.Definition.ObjectId:X8};"+
            $"{source.Resource.Source.ResourceUri}@{source.Resource.Source.DecodedSha256}/TID={source.ImageIndex};native-SRV95;mip-major-to-face-chains;"+
            $"sampler-sha256={Convert.ToHexString(SHA256.HashData(samplerBytes))}";
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var resources=new RenderDeviceResources("Device rejected the original compressed ambient cube.");
            try
            {
                const RenderingDevice.DataFormat format=RenderingDevice.DataFormat.Bc6HUfloatBlock;
                var usage=RenderingDevice.TextureUsageBits.SamplingBit;
                if(allowReadback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
                if(!rd.TextureIsFormatSupportedForUsage(format,usage))
                    throw new NotSupportedException("Device cannot retain the exact native BC6H unsigned static cube.");
                using var shape=new RDTextureFormat{Format=format,Width=(uint)image.Width,Height=(uint)image.Height,
                    Depth=1,ArrayLayers=6,Mipmaps=(uint)image.MipCount,
                    TextureType=RenderingDevice.TextureType.Cube,UsageBits=usage};
                using var view=new RDTextureView();Rid texture=resources.Own(rd.TextureCreate(shape,view,faces));
                using var actual=rd.TextureGetFormat(texture);
                if(actual.Format!=format||actual.Width!=image.Width||actual.Height!=image.Height||actual.Depth!=1||
                    actual.ArrayLayers!=6||actual.Mipmaps!=image.MipCount||actual.TextureType!=RenderingDevice.TextureType.Cube)
                    throw new InvalidDataException("Device changed the original static ambient cube encoding or layout.");
                Rid sampler=DeviceSampler.Create(rd,samplerBytes);
                if(!sampler.IsValid)throw new InvalidDataException("Device rejected the original static cube source sampler.");
                resources.Own(sampler);
                return new SourceResourceRegistrationAmbientCubeDevice(source,image,texture,sampler,hashes,provenance,allowReadback,resources);
            }
            catch(Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,"Registered-ambient-cube-partial");
                throw;
            }
        });
    }

    public void RequireAvailable() => ObjectDisposedException.ThrowIf(retiring, this);

    public async Task<byte[]> ReadEncodedFaceAsync(uint face)
    {
        if(!allowReadback)throw new InvalidOperationException("Encoded source cube readback requires its explicit validation flag.");
        if(face>=6)throw new ArgumentOutOfRangeException(nameof(face));
        await gate.WaitAsync();
        try
        {
            RequireAvailable();
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>rd.TextureGetData(TextureRid,face));
        }
        finally { gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetimeGate)
        {
            retiring=true;
            if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)
                disposal=DisposeCoreAsync();
            return new(disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try { await resources.DisposeAsync(); }
        finally { gate.Release(); }
    }
}
