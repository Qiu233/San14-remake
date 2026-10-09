using Godot;
using San14.Data;

/// <summary>Owns the sampler for early68's actual selected Lit descriptor.
/// The selected texture/frame and its completed producer remain borrowed.
/// Retire borrowing draws, then this sampler, then the texture/frame owner.</summary>
public sealed class DeferredEarlyLitSamplerOwner : IAsyncDisposable
{
    private readonly Action requireActualCurrentSelectionAndProducer;
    private Task? disposal;
    private DeferredEarlyLitSamplerOwner(LoadedDeferredEarlyLitSource source,
        DeferredEarlyIndirectProgram program, RenderTargetFrame frame,
        RenderTargetDeviceTexture texture, DeferredEarlyLitPixelBinding binding,
        Rid sampler, Action requireActualCurrentSelectionAndProducer)
        => (Source,Program,Frame,Texture,Binding,SamplerRid,this.requireActualCurrentSelectionAndProducer) =
            (source,program,frame,texture,binding,sampler,requireActualCurrentSelectionAndProducer);
    public LoadedDeferredEarlyLitSource Source { get; }
    public DeferredEarlyIndirectProgram Program { get; }
    public RenderTargetFrame Frame { get; }
    public RenderTargetDeviceTexture Texture { get; }
    public DeferredEarlyLitPixelBinding Binding { get; }
    public Rid SamplerRid { get; }
    public string SourceEvidence=>Source.SourceEvidence+";selected-owned-wrapper="+Texture.EffectiveSource.OwnerIdentity+
        ";borrowed-actual-selected-texture-and-producer;owned-source-key-sampler";

    /// <summary>Called after the normal owner chooses the actual wrapper,
    /// follows its actual alias/CPU.v28, and finishes the selected producer.
    /// This method never treats EA36C27D's declaration as a current texture.
    /// Physical descriptor identities here are the owned device RID identities.
    /// An original null command needs its separately proved device transport.</summary>
    public static Task<DeferredEarlyLitSamplerOwner> BuildAsync(
        LoadedDeferredEarlyLitSource source, DeferredEarlyIndirectProgram actualProgram,
        RenderTargetFrame actualSelectedFrame, RenderTargetDeviceTexture actualSelectedTexture,
        DeferredEarlyRender2DMetadata? actualSelectedMetadata,
        Action requireActualCurrentSelectionAndProducer)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(actualSelectedFrame);
        ArgumentNullException.ThrowIfNull(actualSelectedTexture);ArgumentNullException.ThrowIfNull(requireActualCurrentSelectionAndProducer);
        requireActualCurrentSelectionAndProducer();
        var binding=source.PrepareActualPixelBinding(actualProgram,actualSelectedMetadata);
        if(!binding.ActualMetadataPresent||binding.Sampler is null||binding.ShaderView==0)
            throw new NotSupportedException("Actual early68 null texture command requires its source-null device transport; no replacement texture is supplied.");
        RequireOwnedTexture(actualSelectedFrame,actualSelectedTexture,binding);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            requireActualCurrentSelectionAndProducer();
            RequireOwnedTexture(actualSelectedFrame,actualSelectedTexture,binding);
            if(!rd.TextureIsValid(actualSelectedTexture.StorageRid)||!rd.TextureIsValid(actualSelectedTexture.ShaderViewRid))
                throw new ObjectDisposedException(nameof(actualSelectedFrame));
            Rid sampler=DeviceSampler.Create(rd,binding.Sampler);
            return new DeferredEarlyLitSamplerOwner(source,actualProgram,actualSelectedFrame,actualSelectedTexture,
                binding,sampler,requireActualCurrentSelectionAndProducer);
        });
    }

    public IReadOnlyList<DeferredDeviceBinding> ReadDeviceBindings()
    {
        RequireCurrent();
        return Array.AsReadOnly(new[] {
            new DeferredDeviceBinding("Texture",Binding.Register,Texture.ShaderViewRid,Texture.StorageRid,SourceEvidence),
            new DeferredDeviceBinding("Sampler",Binding.Register,SamplerRid,null,SourceEvidence)
        });
    }

    public void RequireCurrent()
    {
        ObjectDisposedException.ThrowIf(disposal is not null,this);
        requireActualCurrentSelectionAndProducer();RequireOwnedTexture(Frame,Texture,Binding);
    }
    private static void RequireOwnedTexture(RenderTargetFrame actualFrame,
        RenderTargetDeviceTexture actualTexture,DeferredEarlyLitPixelBinding actualBinding)
    {
        if(!actualFrame.Textures.Values.Any(t=>ReferenceEquals(t,actualTexture))||
            actualTexture.EffectiveSource.Kind!=RenderTextureKind.Texture2D||
            actualTexture.EffectiveSource.ArrayDepth is not null||
            actualTexture.StorageRid.Id!=actualBinding.PhysicalResource||actualTexture.ShaderViewRid.Id!=actualBinding.ShaderView)
            throw new InvalidDataException("Early68 actual selected metadata does not belong to this retained physical texture/view.");
    }
    public ValueTask DisposeAsync()=>new(disposal??=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
    { if(SamplerRid.IsValid)rd.FreeRid(SamplerRid);return true; }));
}
