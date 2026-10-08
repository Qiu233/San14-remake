using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Owns the actual fresh font-cache BGRA8/87 image. CPU source writes
/// decode the G1N glyph before clearing its native border. Complete initial
/// caller bytes outside that region remain explicit, independently of original
/// VRAM. Consumers must retire their uniform sets before disposing this owner.</summary>
public sealed class OriginalRemakeArmyFlagGlyphAtlas:IAsyncDisposable
{
    public LoadedOriginalArmyFlagGlyphAtlasPrefix Source { get; }
    public Rid TextureRid { get; }
    public byte[] NativeSamplerDescriptor52 { get; }
    private Task? _disposal;
    private OriginalRemakeArmyFlagGlyphAtlas(LoadedOriginalArmyFlagGlyphAtlasPrefix source,Rid texture,byte[] sampler)
        =>(Source,TextureRid,NativeSamplerDescriptor52)=(source,texture,sampler);

    public static async Task<OriginalRemakeArmyFlagGlyphAtlas> BuildAsync(LoadedOriginalArmyFlagGlyphAtlasPrefix source,
        LoadedOriginalDeferredSamplerNativeSource samplerNative,bool allowNumericReadback=false)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(samplerNative);
        byte[] bytes=source.Bytes.ToArray();
        if(bytes.Length!=OriginalArmyFlagGlyphAtlasPrefix.AtlasByteLength||source.NativeShaderViewFormat!=87||source.NativeResourceFormat!=90||
            source.MipCount!=1||source.SourceGlyphCount!=1||Convert.ToHexString(SHA256.HashData(bytes))!=source.WrittenAtlasSha256)
            throw Error("requires the exact source first-insertion1024x1024/mip1/BGRA8 prefix");
        var canonical=OriginalArmyFlagGlyphAtlasPrefix.WriteFirst(source.Font,source.Glyph,bytes,
            source.CallerInitialTemplateSource,true);
        if(canonical.LogicalRectangle!=source.LogicalRectangle||canonical.NativeRowWriteRectangle!=source.NativeRowWriteRectangle||
            canonical.UvRectangle!=source.UvRectangle||!canonical.Bytes.AsSpan().SequenceEqual(bytes))
            throw Error("source glyph/UV/border writes differ from the canonical first insertion");
        byte[] sampler=OriginalDeferredSamplerSources.DecodeKey(samplerNative,source.SourceSamplerKey).EncodeD3D11Descriptor();
        Rid texture=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
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

    public OriginalModelDeviceBinding Borrow()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        return new("Texture",0,TextureRid,TextureRid,Source.Font.Source.ResourceUri+
            $"#fresh-native-atlas/face{Source.Glyph.FaceIndex}/palette{Source.Glyph.PaletteIndex}/glyph{Source.Glyph.Record.Index}@{Source.WrittenAtlasSha256}");
    }
    public ValueTask DisposeAsync()=>new(_disposal??=OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(TextureRid);return true;}));
    private static InvalidDataException Error(string message)=>new("Original Army flag glyph atlas "+message+".");
}
