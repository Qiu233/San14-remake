using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns the actual fresh font-cache BGRA8/87 image. CPU source writes
/// decode the G1N glyph before clearing its native border. Complete initial
/// caller bytes outside that region remain explicit, independently of original
/// VRAM. Consumers must retire their uniform sets before disposing this owner.</summary>
public sealed class RemakeArmyFlagGlyphAtlas:IAsyncDisposable
{
    public LoadedArmyFlagGlyphAtlasPrefix Source { get; }
    public Rid TextureRid { get; }
    public byte[] NativeSamplerDescriptor52 { get; }
    private Task? _disposal;
    private RemakeArmyFlagGlyphAtlas(LoadedArmyFlagGlyphAtlasPrefix source,Rid texture,byte[] sampler)
        =>(Source,TextureRid,NativeSamplerDescriptor52)=(source,texture,sampler);

    public static async Task<RemakeArmyFlagGlyphAtlas> BuildAsync(LoadedArmyFlagGlyphAtlasPrefix source,
        LoadedDeferredSamplerNativeSource samplerNative,bool allowNumericReadback=false)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(samplerNative);
        byte[] bytes=source.Bytes.ToArray();
        if(bytes.Length!=ArmyFlagGlyphAtlasPrefix.AtlasByteLength||source.NativeShaderViewFormat!=87||source.NativeResourceFormat!=90||
            source.MipCount!=1||source.SourceGlyphCount!=1)
            throw Error("requires the owned first-insertion1024x1024/mip1/BGRA8 image");
        byte[] sampler=RenderSamplerSourceAdapters.DecodeKey(samplerNative,source.SourceSamplerKey).EncodeD3D11Descriptor();
        Rid texture=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var usage=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanUpdateBit;
            if(allowNumericReadback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
            using var format=new RDTextureFormat{Format=RenderingDevice.DataFormat.B8G8R8A8Unorm,Width=1024,Height=1024,
                Depth=1,ArrayLayers=1,Mipmaps=1,TextureType=RenderingDevice.TextureType.Type2D,Samples=RenderingDevice.TextureSamples.Samples1,UsageBits=usage};
            using var view=new RDTextureView();
            Rid value=rd.TextureCreate(format,view,new Godot.Collections.Array<byte[]>{bytes});
            if(!value.IsValid)throw Error("device rejected the actual source font cache image");return value;
        });
        return new(source,texture,sampler);
    }

    public ModelDeviceBinding Borrow()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        return new("Texture",0,TextureRid,TextureRid,Source.Font.Source.ResourceUri+
            $"#fresh-native-atlas/face{Source.Glyph.FaceIndex}/palette{Source.Glyph.PaletteIndex}/glyph{Source.Glyph.Record.Index}@{Source.WrittenAtlasSha256}");
    }
    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(TextureRid);return true;}));
    private static InvalidDataException Error(string message)=>new("Original Army flag glyph atlas "+message+".");
}
