using Godot;
using San14.Data;

/// <summary>Owns one registered planar image and its sampler. Image upload is
/// shared with other encoded textures; binding queries only check its lifetime.</summary>
public sealed class RegisteredTextureDevice : IAsyncDisposable
{
    public SourceResourceRegistrationDeviceEntry Entry { get; }
    public StaticTextureBinding Source => Entry.Source;
    public Rid TextureRid => Entry.TextureRid;
    public Rid SamplerRid => Entry.SamplerRid;
    public bool IsRetiring => retiring;
    private readonly SkyTextureDevice image;
    private readonly DirectionalLightResourceBinding binding;
    private readonly bool readback;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetimeGate = new();
    private volatile bool retiring;
    private bool samplerReleased;
    private Task? disposal;

    private RegisteredTextureDevice(SourceResourceRegistrationDeviceEntry entry,
        SkyTextureDevice image, bool allowReadback)
    {
        Entry = entry; this.image = image; readback = allowReadback;
        binding = new(entry.SourceObjectKey, entry.TextureRid.Id, entry.OwnerProvenance, true,
            StaticTextureResources.TextureInterfaceClassId);
    }

    public static async Task<RegisteredTextureDevice> BuildAsync(StaticTextureBinding source,
        uint? explicitNativeViewFormat = null, bool allowReadback = false)
    {
        var imported = StaticTextureResources.Import2D(source, explicitNativeViewFormat);
        var image = await SkyTextureDevice.BuildAsync(imported, allowReadback);
        Rid sampler = default;
        try
        {
            sampler = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
                DeviceSampler.Create(rd, imported.GetSamplerDescriptor()));
            var provenance = $"{source.Definitions.Source.ResourceUri}#{source.Definition.ObjectId:X8}@{source.Definitions.Source.DecodedSha256};" +
                $"{imported.ResourceUri}/TID={imported.ImageIndex}@{imported.SourceSha256};native-SRV={imported.NativeViewFormat};" +
                $"sampler={imported.SamplerDescriptorSha256};retained-encoded-mips";
            var entry = new SourceResourceRegistrationDeviceEntry(source.Definition.ObjectId, source,
                image.TextureRid, sampler, imported.NativeViewFormat, RenderingDevice.TextureType.Type2D,
                image.Format, imported.MipSha256, provenance);
            return new(entry, image, allowReadback);
        }
        catch
        {
            try
            {
                if (sampler.IsValid)
                    await TerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(sampler); return true; });
            }
            finally { await image.DisposeAsync(); }
            throw;
        }
    }

    public void RequireAvailable() => ObjectDisposedException.ThrowIf(retiring, this);

    public DirectionalLightResourceBinding GetBinding()
    {
        RequireAvailable();
        return binding;
    }

    public async Task<byte[]> ReadEncodedChainAsync()
    {
        if (!readback) throw new InvalidOperationException("The registered image was not created for readback.");
        await gate.WaitAsync();
        try { RequireAvailable(); return await image.ReadEncodedChainAsync(); }
        finally { gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate)
        {
            retiring = true;
            if (disposal is null || disposal.IsFaulted || disposal.IsCanceled)
                disposal = DisposeCoreAsync();
            return new(disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            try
            {
                if (!samplerReleased)
                {
                    await TerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(SamplerRid); return true; });
                    samplerReleased = true;
                }
            }
            finally { await image.DisposeAsync(); }
        }
        finally { gate.Release(); }
    }
}
