using Godot;
using San14.Data;

/// <summary>The successful source renderer initialization creates and uploads
/// one white BGRA8 texel, then publishes it through the saved view template.
/// This owner instantiates that exact first initialization route.</summary>
public sealed class MapFinalOutputNoiseOwner : IAsyncDisposable
{
    public LoadedMapFinalOutputResources Source { get; }
    public ModelDeviceBinding Binding { get; }
    public string OwnerIdentity { get; }="original-successful-initialized-noise:"+Guid.NewGuid().ToString("N");
    public ReadOnlyMemory<byte> NativeSamplerKey => Convert.FromHexString(MapFinalOutputShaft.WhiteNoiseSamplerKeyHex);
    private volatile bool retiring;
    private readonly RenderDeviceResources resources;
    private MapFinalOutputNoiseOwner(LoadedMapFinalOutputResources source,Rid texture,RenderDeviceResources owned)
    {
        resources=owned;
        Source=source;Binding=new("Texture",0,texture,texture,source.StockScreen.Native.ResourceUri+
            "#BF93D0/BFD045..BFD0E1;successful-first-init;saved-template-899F101B;1x1/native1/FFFFFFFF");
    }
    public static async Task<MapFinalOutputNoiseOwner> BuildSuccessfulFirstInitializationAsync(LoadedMapFinalOutputResources source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var resources=new RenderDeviceResources("Source initialized white-noise texture failed.");
        try
        {
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                using var format=new RDTextureFormat{Format=RenderingDevice.DataFormat.B8G8R8A8Unorm,Width=1,Height=1,
                    Depth=1,ArrayLayers=1,Mipmaps=1,TextureType=RenderingDevice.TextureType.Type2D,
                    UsageBits=RenderingDevice.TextureUsageBits.SamplingBit};
                using var view=new RDTextureView();Rid texture=resources.Own(rd.TextureCreate(format,view,[new byte[]{255,255,255,255}]));
                return new MapFinalOutputNoiseOwner(source,texture,resources);
            });
        }
        catch(Exception failure)
        {
            var pending=NativeMapBorrowerRetirementPackage.CreatePending("Final-noise-partial",
                resources.Count>0?[resources]:[],(failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pending);}
            catch(Exception retirementFailure)
            {throw new NativeMapBorrowerConstructionException(failure,retirementFailure,pending!);}
            throw;
        }
    }
    public void RequireSuccessfulFirstInitialization()
    {
        ObjectDisposedException.ThrowIf(retiring,this);
        if(!Binding.Resource.IsValid||Binding.OwnerTexture!=Binding.Resource)
            throw new InvalidDataException("Source saved-template initialized noise owner is not live.");
    }
    public ValueTask DisposeAsync()
    {
        retiring=true;
        return resources.DisposeAsync();
    }
}
