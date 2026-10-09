using Godot;
using System.Runtime.CompilerServices;
using San14.Data;

/// <summary>The actual selected material primary RT and its original sampler.
/// The owner supports the proven packed-tint clear stage and the subsequent
/// source glyph renderer. A clear alone never marks the full producer ready.</summary>
public sealed class RemakeArmyFlagPaneImage : IAsyncDisposable
{
    public ArmyFlagMaterialTextureUse Source { get; }
    public RenderTextureDefinition Definition=>Source.Slot.RenderTarget!;
    public Rid StorageRid { get; }
    public Rid ShaderViewRid { get; }
    public Rid Framebuffer { get; }
    public Rid SamplerRid { get; }
    public uint? LastSourceClearPacked { get; private set; }
    public bool CompleteGlyphStageVerified { get; private set; }
    public bool CompleteProducerReady=>LastSourceClearPacked.HasValue&&CompleteGlyphStageVerified;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;

    private RemakeArmyFlagPaneImage(ArmyFlagMaterialTextureUse source,Rid storage,Rid shader,
        Rid framebuffer,Rid sampler,IReadOnlyList<Rid> owned)
        =>(Source,StorageRid,ShaderViewRid,Framebuffer,SamplerRid,_owned)=(source,storage,shader,framebuffer,sampler,owned);

    public static Task<RemakeArmyFlagPaneImage> BuildAsync(ArmyFlagMaterialTextureUse source,
        LoadedDeferredSamplerNativeSource samplerNative,bool allowNumericReadback=false)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(samplerNative);
        var definition=source.Slot.RenderTarget??throw Error("requires the actual selected Material RT proxy");
        var canonical=RenderTextureResources.Read(definition.Source,definition.Object.ObjectId);
        if(canonical!=definition||definition.Kind!=RenderTextureKind.Texture2D||definition.Width!=128||definition.Height!=64||
            definition.LevelNum!=1||definition.MinMipLevel!=0||definition.MultiSample!=0||source.TextureRegister!=0)
            throw Error("source primary image declaration differs from the finite 128x64 single-level profile");
        var native=RenderTextureFormatMath.Resolve(definition);
        RenderingDevice.DataFormat Format(uint value)=>value switch{28=>RenderingDevice.DataFormat.R8G8B8A8Unorm,
            29=>RenderingDevice.DataFormat.R8G8B8A8Srgb,_=>throw Error("has no exact RGBA8 native shader/target view")};
        var shaderFormat=Format(native.ShaderViewFormat);var targetFormat=Format(native.TargetViewFormat);
        byte[] sampler=ArmyFlagMaterialVariations.ReadProducerSampler(samplerNative,source).EncodeD3D11Descriptor();
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();
            Rid Own(Rid value){if(!value.IsValid)throw Error("device rejected the source image/view/sampler");owned.Add(value);return value;}
            try
            {
                var usage=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.ColorAttachmentBit;
                if(allowNumericReadback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
                if(!rd.TextureIsFormatSupportedForUsage(targetFormat,usage))throw Error("source target format/usage is unsupported");
                using var format=new RDTextureFormat{Format=targetFormat,Width=128,Height=64,Depth=1,ArrayLayers=1,Mipmaps=1,
                    TextureType=RenderingDevice.TextureType.Type2D,Samples=RenderingDevice.TextureSamples.Samples1,UsageBits=usage};
                foreach(var item in new[]{targetFormat,shaderFormat}.Distinct())format.AddShareableFormat(item);
                using var view=new RDTextureView();Rid storage=Own(rd.TextureCreate(format,view));Rid shader=storage;
                if(shaderFormat!=targetFormat){using var shared=new RDTextureView{FormatOverride=shaderFormat};shader=Own(rd.TextureCreateShared(shared,storage));}
                Rid framebuffer=Own(rd.FramebufferCreate(new Godot.Collections.Array<Rid>{storage}));
                Rid samplerRid=Own(DeviceSampler.Create(rd,sampler));
                return new RemakeArmyFlagPaneImage(source,storage,shader,framebuffer,samplerRid,owned.AsReadOnly());
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    /// <summary>B09E70 with the explicit-image and clear flags issues a packed
    /// source tint clear before the main glyph draw. RGBA decode follows the
    /// native clear packet, independently of territorial map alpha.</summary>
    public async Task ClearSourceTintAsync(uint explicitSourcePackedTint,string explicitColorSource)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(string.IsNullOrWhiteSpace(explicitColorSource))throw Error("clear requires an explicit original force color source");
        float reciprocal=BitConverter.UInt32BitsToSingle(0x3B808081);
        var color=new Color(Multiply(explicitSourcePackedTint&255,reciprocal),Multiply((explicitSourcePackedTint>>8)&255,reciprocal),
            Multiply((explicitSourcePackedTint>>16)&255,reciprocal),Multiply((explicitSourcePackedTint>>24)&255,reciprocal));
        await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            long list=rd.DrawListBegin(Framebuffer,RenderingDevice.DrawFlags.ClearColorAll,
                [color]);rd.DrawListEnd();return list;
        });
        LastSourceClearPacked=explicitSourcePackedTint;CompleteGlyphStageVerified=false;
    }

    public RemakeArmyFlagRuntimeTexture BorrowCompletedProducer(string glyphProducerSource)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(!CompleteProducerReady||string.IsNullOrWhiteSpace(glyphProducerSource))
            throw Error("full source glyph producer has not completed this image");
        string uri=Definition.Source.Source.ResourceUri+$"#TextPane-primary/{Source.Slot.ProxyObjectId:X8}";
        return new(Source.Slot.ProxyObjectId,new("Texture",0,ShaderViewRid,StorageRid,uri),
            new("Sampler",0,SamplerRid,null,uri+"#source-sampler"),glyphProducerSource)
            {RequireActualCurrentOwner=RequireCompletedProducer};
    }

    private void RequireCompletedProducer()
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(!CompleteProducerReady)throw Error("borrowed primary image is no longer a completed producer resource");
    }

    internal void PublishCompletedSourceGlyph(LoadedArmyFlagFreshPaneLayout layout)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(LastSourceClearPacked!=layout.SourceColors.BackgroundTint.PanePacked||Definition.Width!=layout.NativeLayoutWidth||
            Definition.Height!=layout.NativeLayoutHeight||layout.SourceText.TwoUtf16Units.Length!=2||layout.SourceText.TwoUtf16Units[1]!='\0')
            throw Error("source glyph completion differs from this primary image clear/layout");
        CompleteGlyphStageVerified=true;
    }

    internal void PublishCompletedSourceEmpty(LoadedArmyFlagEmptyPaneLayout layout)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null,this);
        if(LastSourceClearPacked!=layout.SourceColors.BackgroundTint.PanePacked||
            Definition.Width!=layout.NativeLayoutWidth||Definition.Height!=layout.NativeLayoutHeight||
            layout.NativeGlyphRecordCount!=0||layout.SourceText.TwoUtf16Units!="\0\0")
            throw Error("completed source zero-glyph job differs from this primary clear/NUL layout");
        CompleteGlyphStageVerified=true;
    }

    public ValueTask DisposeAsync()=>new(_disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {for(int i=_owned.Count-1;i>=0;i--)rd.FreeRid(_owned[i]);return true;}));
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float left,float right)=>left*right;
    private static InvalidDataException Error(string message)=>new("Original Army flag TextPane image "+message+".");
}
