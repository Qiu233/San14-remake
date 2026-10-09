using System.Security.Cryptography;
using Godot;
using San14.Data;

/// <summary>Owns the actual seasonal Directional source73 FE8 Static texture.
/// Uploads the original BC1 blocks without decoding, packaging or new mipmaps.
/// No HDRI packet identity or shared device-owner lifetime is required.</summary>
public sealed class SourceResourceRegistrationDirectionalDiffuseDevice : IAsyncDisposable
{
    public SourceResourceRegistrationDeviceEntry Entry { get; }
    public StaticTextureBinding Source => Entry.Source;
    public Rid TextureRid => Entry.TextureRid;
    public Rid SamplerRid => Entry.SamplerRid;
    public bool IsRetiring => retiring;
    private readonly bool readback;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object lifetimeGate=new();
    private volatile bool retiring;
    private Task? disposal;
    private SourceResourceRegistrationDirectionalDiffuseDevice(SourceResourceRegistrationDeviceEntry entry,bool allowReadback)
        =>(Entry,readback)=(entry,allowReadback);

    public static Task<SourceResourceRegistrationDirectionalDiffuseDevice> BuildAsync(GameResourceRepository repository,
        uint explicitNativeViewFormat=71,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return BuildAsync(SourceResourceRegistrationDirectionalDiffuse.Load(repository),explicitNativeViewFormat,allowReadback);
    }
    public static async Task<SourceResourceRegistrationDirectionalDiffuseDevice> BuildAsync(StaticTextureBinding source,
        uint explicitNativeViewFormat=71,bool allowReadback=false)
    {
        SourceResourceRegistrationDirectionalDiffuse.Validate(source);
        if(explicitNativeViewFormat is not (71 or 72))
            throw new InvalidDataException("Directional source73 requires an explicit original BC1 view71 or72.");
        byte[] blocks=SourceResourceRegistrationDirectionalDiffuse.GetNativeBody(source).ToArray();
        var sampler=SourceResourceRegistrationDirectionalDiffuse.SamplerRecipe(source);
        string hash=Convert.ToHexString(SHA256.HashData(blocks));
        string provenance=$"{source.Definitions.Source.ResourceUri}#{source.Definition.ObjectId:X8}@{source.Definitions.Source.DecodedSha256};"+
            $"{source.Resource.Source.ResourceUri}/TID=0@{source.Resource.Source.DecodedSha256};native-SRV={explicitNativeViewFormat};"+
            $"sampler={Convert.ToHexString(SHA256.HashData(sampler.EncodeD3D11Descriptor()))};retained-BC1-source-blocks";
        var entry=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var format=explicitNativeViewFormat==71?RenderingDevice.DataFormat.Bc1RgbaUnormBlock:RenderingDevice.DataFormat.Bc1RgbaSrgbBlock;
            var usage=RenderingDevice.TextureUsageBits.SamplingBit;
            if(allowReadback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
            if(!rd.TextureIsFormatSupportedForUsage(format,usage))
                throw new NotSupportedException($"GPU {rd.GetDeviceName()} cannot sample/read the original Directional BC1 view{explicitNativeViewFormat}.");
            Rid texture=default,deviceSampler=default;
            try
            {
                using var shape=new RDTextureFormat{Format=format,TextureType=RenderingDevice.TextureType.Type2D,
                    Width=2048,Height=2048,Depth=1,ArrayLayers=1,Mipmaps=1,UsageBits=usage};
                using var view=new RDTextureView();var data=new Godot.Collections.Array<byte[]>{blocks};
                texture=rd.TextureCreate(shape,view,data);data.Clear();
                if(!texture.IsValid||!rd.TextureIsValid(texture))throw new InvalidDataException("GPU rejected the original source73 BC1 blocks.");
                CheckShape(rd,texture,format,allowReadback);
                if(allowReadback&&!rd.TextureGetData(texture,0).AsSpan().SequenceEqual(blocks))
                    throw new InvalidDataException("Directional GPU compressed readback differs from the original BC1 blocks.");
                deviceSampler=DeviceSampler.Create(rd,sampler);
                return new SourceResourceRegistrationDeviceEntry(SourceResourceRegistrationDirectionalDiffuse.ObjectKey,source,
                    texture,deviceSampler,explicitNativeViewFormat,RenderingDevice.TextureType.Type2D,format,
                    Array.AsReadOnly(new[]{hash}),provenance);
            }
            catch {if(deviceSampler.IsValid)rd.FreeRid(deviceSampler);if(texture.IsValid)rd.FreeRid(texture);throw;}
        });
        return new(entry,allowReadback);
    }
    private static void CheckShape(RenderingDevice rd,Rid texture,RenderingDevice.DataFormat format,bool allowReadback)
    {
        using var actual=rd.TextureGetFormat(texture);
        if(actual.Format!=format||actual.TextureType!=RenderingDevice.TextureType.Type2D||actual.Width!=2048||actual.Height!=2048||
            actual.Depth!=1||actual.ArrayLayers!=1||actual.Mipmaps!=1||!actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit)||
            (allowReadback&&!actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyFromBit)))
            throw new InvalidDataException("Directional GPU resource changed its exact BC1 source shape/usage.");
    }
    private Task<bool> ValidateOnDeviceAsync()
    {
        SourceResourceRegistrationDirectionalDiffuse.Validate(Source);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            if(!TextureRid.IsValid||!SamplerRid.IsValid||!rd.TextureIsValid(TextureRid))
                throw new InvalidDataException("Directional source73 GPU receipt has retired.");
            CheckShape(rd,TextureRid,Entry.Format,readback);return true;
        });
    }
    public async Task ValidateAsync()
    {
        await gate.WaitAsync();try{ObjectDisposedException.ThrowIf(retiring,this);await ValidateOnDeviceAsync();}
        finally{gate.Release();}
    }
    public async Task<DirectionalLightResourceBinding> GetBindingAsync()
    {
        await gate.WaitAsync();try
        {
            ObjectDisposedException.ThrowIf(retiring,this);await ValidateOnDeviceAsync();
            return new(Entry.SourceObjectKey,TextureRid.Id,Entry.OwnerProvenance,true,SourceResourceRegistrationDirectionalDiffuse.TextureInterfaceClassId);
        }
        finally{gate.Release();}
    }
    public async Task<byte[]> ReadEncodedBodyAsync()
    {
        if(!readback)throw new InvalidOperationException("Directional source73 owner was not built for compressed readback.");
        await gate.WaitAsync();try
        {
            ObjectDisposedException.ThrowIf(retiring,this);await ValidateOnDeviceAsync();
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>rd.TextureGetData(TextureRid,0));
        }
        finally{gate.Release();}
    }
    public ValueTask DisposeAsync()
    {lock(lifetimeGate){retiring=true;return new(disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();try
        {await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(SamplerRid);rd.FreeRid(TextureRid);return true;});}
        finally{gate.Release();}
    }
}
