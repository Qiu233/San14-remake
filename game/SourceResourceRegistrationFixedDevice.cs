using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>One actual fixed Static white resource used by the Directional
/// loader's fallback lookup80F9DB7B. It owns only this source texture and its
/// original sampler, independently of HDRI and seasonal source73 owners.</summary>
public sealed class SourceResourceRegistrationFixedDevice : IAsyncDisposable
{
    public const uint WhiteObjectKey = 0x80F9DB7B;
    public SourceResourceRegistrationDeviceEntry Entry { get; }
    public OriginalStaticTextureBinding Source => Entry.Source;
    public Rid TextureRid => Entry.TextureRid;
    public Rid SamplerRid => Entry.SamplerRid;
    public bool IsRetiring => retiring;
    private readonly OriginalSkyTextureDevice texture;
    private readonly bool readback;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetimeGate = new();
    private volatile bool retiring;
    private Task? disposal;
    private static SourceResourceRegistrationFixedIdentity Identity =>
        SourceResourceRegistrationFixed.Identities.Single(value => value.ObjectKey == WhiteObjectKey);

    private SourceResourceRegistrationFixedDevice(SourceResourceRegistrationDeviceEntry entry,
        OriginalSkyTextureDevice texture, bool readback)
        => (Entry, this.texture, this.readback) = (entry, texture, readback);

    /// <summary>Resolve only the literal white definition and its original G1T.
    /// Does not load the other fixed textures, HdrLut or any ambient cube.</summary>
    public static Task<SourceResourceRegistrationFixedDevice> BuildAsync(OriginalGameRepository repository,
        uint explicitNativeViewFormat = 87, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = OriginalStaticTextureResources.Load(repository,
            repository.LoadObjects(SourceResourceRegistrationFixed.Archive, Identity.DefinitionResourceId),
            SourceResourceRegistrationFixed.Archive, [WhiteObjectKey]).Single();
        return BuildAsync(source, explicitNativeViewFormat, allowReadback);
    }

    public static async Task<SourceResourceRegistrationFixedDevice> BuildAsync(OriginalStaticTextureBinding source,
        uint explicitNativeViewFormat = 87, bool allowReadback = false)
    {
        SourceResourceRegistrationFixed.Validate(source, Identity);
        if (explicitNativeViewFormat is not (87 or 91))
            throw new InvalidDataException("Fixed white registration requires an explicit original BGRA view87 or91.");
        // Reparse independent retained bytes; a caller's subsequent array edits
        // cannot change the source declaration of this uploaded generation.
        source = Snapshot(source);
        byte[] original = SourceResourceRegistrationFixed.GetNativeLayerBytes(source, 0);
        if (!original.AsSpan().SequenceEqual(new byte[] { 255, 255, 255, 255 }))
            throw new InvalidDataException("Original fixed80F9DB7B is not its exact encoded white pixel.");
        var recipe = G1TextureSamplerReader.ReadRetainedSource(source.Image);
        string sourceHash = Convert.ToHexString(SHA256.HashData(original));
        string provenance = $"{source.Definitions.Source.ResourceUri}#{WhiteObjectKey:X8}@{source.Definitions.Source.DecodedSha256};" +
            $"{source.Resource.Source.ResourceUri}/TID=0@{source.Resource.Source.DecodedSha256};native-SRV={explicitNativeViewFormat};" +
            $"sampler={Convert.ToHexString(SHA256.HashData(recipe.EncodeD3D11Descriptor()))};retained-fixed-white-source";
        // The shared upload primitive owns the actual supplied G1T identity;
        // no Sky or HDRI source is substituted by using this upload helper.
        var uploaded = await OriginalSkyTextureDevice.BuildAsync(source.Resource, 0, explicitNativeViewFormat);
        Rid sampler = default;
        try
        {
            sampler = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => OriginalDeviceSampler.Create(rd, recipe));
            if (!sampler.IsValid) throw new InvalidDataException("Device rejected the original fixed-white sampler.");
            var entry = new SourceResourceRegistrationDeviceEntry(WhiteObjectKey, source, uploaded.TextureRid, sampler,
                explicitNativeViewFormat, RenderingDevice.TextureType.Type2D,
                explicitNativeViewFormat == 87 ? RenderingDevice.DataFormat.B8G8R8A8Unorm : RenderingDevice.DataFormat.B8G8R8A8Srgb,
                Array.AsReadOnly(new[] { sourceHash }), provenance);
            var owner = new SourceResourceRegistrationFixedDevice(entry, uploaded, allowReadback);
            await owner.ValidateAsync();
            if (allowReadback && !(await owner.ReadEncodedChainAsync()).AsSpan().SequenceEqual(original))
                throw new InvalidDataException("Fixed white GPU readback differs from its original encoded pixel.");
            return owner;
        }
        catch
        {
            if (sampler.IsValid) await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(sampler); return true; });
            await uploaded.DisposeAsync();
            throw;
        }
    }

    private static OriginalStaticTextureBinding Snapshot(OriginalStaticTextureBinding supplied)
    {
        var definitionsAsset = supplied.Definitions.Source with { Bytes = supplied.Definitions.Source.Bytes.ToArray() };
        var definitions = new LoadedObjects(DokReader.Read(definitionsAsset.Bytes), definitionsAsset);
        var definition = definitions.Document.Objects.Single(value => value.ObjectId == WhiteObjectKey);
        var textureAsset = supplied.Resource.Source with { Bytes = supplied.Resource.Source.Bytes.ToArray() };
        var resource = new LoadedTextureContainer(G1TextureContainerReader.Read(textureAsset.Bytes), textureAsset);
        var result = new OriginalStaticTextureBinding(definitions, definition,
            definition.Properties.Single(value => value.NameHash == 0x6C7321D2),
            definition.Properties.Single(value => value.NameHash == 0x00200571), resource, 0);
        SourceResourceRegistrationFixed.Validate(result, Identity);
        return result;
    }

    private Task<bool> ValidateOnDeviceAsync()
    {
        SourceResourceRegistrationFixed.Validate(Source, Identity);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            if (!TextureRid.IsValid || !SamplerRid.IsValid || !rd.TextureIsValid(TextureRid))
                throw new InvalidDataException("Fixed-white device generation has retired.");
            using var actual = rd.TextureGetFormat(TextureRid);
            if (actual.Format != Entry.Format || actual.TextureType != RenderingDevice.TextureType.Type2D ||
                actual.Width != 1 || actual.Height != 1 || actual.Depth != 1 || actual.ArrayLayers != 1 || actual.Mipmaps != 1 ||
                !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit) ||
                !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyFromBit))
                throw new InvalidDataException("Fixed white GPU resource changed its exact original shape/encoding.");
            return true;
        });
    }

    public async Task ValidateAsync()
    {
        await gate.WaitAsync();
        try { ObjectDisposedException.ThrowIf(retiring, this); await ValidateOnDeviceAsync(); }
        finally { gate.Release(); }
    }

    /// <summary>Actual ready texture RID as an opaque remake Data handle.
    /// InterfaceB672FAA2 is the Directional loader's texture contract.</summary>
    public async Task<OriginalDirectionalLightResourceBinding> GetDirectionalBindingAsync()
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring, this); await ValidateOnDeviceAsync();
            return new(WhiteObjectKey, TextureRid.Id, Entry.OwnerProvenance, true,
                SourceResourceRegistrationFixed.TextureInterfaceClassId);
        }
        finally { gate.Release(); }
    }

    public async Task<byte[]> ReadEncodedChainAsync()
    {
        if (!readback) throw new InvalidOperationException("Fixed white registration was not built for readback.");
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring, this); await ValidateOnDeviceAsync();
            return await texture.ReadEncodedChainAsync();
        }
        finally { gate.Release(); }
    }

    public ValueTask DisposeAsync()
    { lock (lifetimeGate) { retiring = true; return new(disposal ??= DisposeCoreAsync()); } }
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try
        {
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(SamplerRid); return true; });
            await texture.DisposeAsync();
        }
        finally { gate.Release(); }
    }
}
