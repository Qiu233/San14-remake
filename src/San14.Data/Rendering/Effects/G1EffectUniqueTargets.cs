namespace San14.Data;

/// <summary>One actual registered UI offscreen target constructor. Format and
/// usage are source wrapper enums, not DXGI values. Resizing retains its key.</summary>
public sealed record G1EffectUniqueTargetSource(int Width, int Height,
    int MipCount, uint NativeFormatCode, uint NativeUsageCode,
    ReadOnlyMemory<byte> SamplerKey, G1EffectGpuSampler Sampler,
    RenderTextureFormats Formats);

public sealed record G1EffectUniqueTargetInputs(G1EffectUniqueTargetSource Color,
    G1EffectUniqueTargetSource Depth, G1EffectUniqueTargetSource Capture,
    G1EffectUniqueTargetSource Working)
{
    // D32BE0 selects mode1 only for the packed RGB depth format1. The
    // registered native5 D24S8 wrapper selects mode3, a single depth channel.
    public int DepthSamplingMode => 3;
}

/// <summary>The registered cutin commands carry their own Color/Depth wrappers.
/// They do not select the renderer's fallback target declarations.</summary>
internal sealed class G1EffectUniqueTargetSources
{
    private readonly byte[] key;
    private readonly G1EffectGpuSampler colorSampler;
    private readonly G1EffectGpuSampler depthSampler;
    private readonly G1EffectGpuSampler captureSampler;
    private readonly G1EffectGpuSampler workingSampler;

    internal G1EffectUniqueTargetSources(GameResourceRepository repository, string locale)
    {
        var native = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        // E15615/E15651 call 94E990 with mipcount1, and 94EA60 invokes
        // the 96AFC0 wrapper constructor. 96BD40 changes the depth-view word
        // only; it does not replace the sampler key. C0D330 retains it on resize.
        key = RenderSamplerSourceAdapters.RenderTextureKey(native, 1);
        var descriptor = RenderSamplerSourceAdapters.DecodeKey(native, key).EncodeD3D11Descriptor();
        colorSampler = new(descriptor);
        depthSampler = new(descriptor);
        captureSampler = new(descriptor);
        workingSampler = new(descriptor);
    }

    internal G1EffectUniqueTargetInputs CreateFrame(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        // The registered entry retains Color at D8 and Depth at E0. E11A20
        // publishes both in the offscreen command before E85F70 consumes it.
        // C0E000 allocates two separate format3 forward wrappers. E85F70's
        // two-wrapper branch draws into CPU+8 and installs CPU+10 as RTT;
        // they share a format/key, not a texture. E1 initialization binds
        // their resolution property to the actual pane size.
        // These finite format joins come from the normal device factory;
        // target view45 is a DSV, whereas the other two are color RTVs.
        return new(new(width, height, 1, 0, 1, key, colorSampler, new(27, 28, 28)),
            new(width, height, 1, 5, 0, key, depthSampler, new(44, 46, 45)),
            new(width, height, 1, 3, 0, key, captureSampler, new(9, 10, 10)),
            new(width, height, 1, 3, 0, key, workingSampler, new(9, 10, 10)));
    }
}
