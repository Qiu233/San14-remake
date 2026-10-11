using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record SourceResourceRegistrationDeviceEntry(uint SourceObjectKey,
    StaticTextureBinding Source, Rid TextureRid, Rid SamplerRid,
    uint NativeViewFormat, RenderingDevice.TextureType TextureType, RenderingDevice.DataFormat Format,
    IReadOnlyList<string> EncodedChainSha256, string OwnerProvenance);

/// <summary>The five real Static owners looked up by the Game HDRI loader.
/// Owns its three fixed2D/array resources and HdrLut volume; borrows the actual
/// static ambient cube and its sampler. Keep this owner and cube alive until
/// every scene draw/provider using their construction handles has retired.</summary>
public sealed class SourceResourceRegistrationDevice : IAsyncDisposable
{
    public IReadOnlyDictionary<uint, SourceResourceRegistrationDeviceEntry> Entries { get; }
    public SourceResourceRegistrationGameDefault Source { get; }
    private readonly SourceResourceRegistrationAmbientCubeDevice cube;
    private readonly OwnedTexture[] owned;
    private readonly IReadOnlyList<MainLightConstructionResourceBinding> constructionBindings;
    private readonly bool allowReadback;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetimeGate = new();
    private volatile bool retiring;
    private Task? disposal;
    public bool IsRetiring => retiring;

    private SourceResourceRegistrationDevice(SourceResourceRegistrationGameDefault source,
        SourceResourceRegistrationAmbientCubeDevice cube,
        OwnedTexture[] owned, SourceResourceRegistrationDeviceEntry cubeEntry, bool allowReadback)
    {
        this.cube = cube; this.owned = owned; this.allowReadback = allowReadback;
        Source = source;
        Entries = new ReadOnlyDictionary<uint, SourceResourceRegistrationDeviceEntry>(
            owned.Select(value => value.Entry).Append(cubeEntry).ToDictionary(entry => entry.SourceObjectKey));
        constructionBindings = Array.AsReadOnly(Entries.Values.OrderBy(entry => entry.SourceObjectKey)
            .Select(entry => new MainLightConstructionResourceBinding(entry.SourceObjectKey,
                entry.TextureRid.Id, entry.OwnerProvenance, true, MainLightConstruction.EnvmapInterfaceClassId)).ToArray());
    }

    /// <summary>Upload exact source layers/volume. The caller selects one
    /// supported original BGRA SRV format87 or91; cube95 remains source-fixed.</summary>
    public static async Task<SourceResourceRegistrationDevice> BuildAsync(GameResourceRepository repository,
        SourceResourceRegistrationAmbientCubeDevice borrowedCube, uint explicitBgraNativeViewFormat,
        bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(borrowedCube);
        if (explicitBgraNativeViewFormat is not (87 or 91))
            throw new InvalidDataException("Static registration requires an explicit original BGRA view87 or91.");
        borrowedCube.RequireAvailable();
        var sources = SourceResourceRegistration.LoadGameDefault(repository);
        var fixedSources = sources.FixedSources;
        var lutSource = sources.HdrLut;
        var owned = new List<OwnedTexture>();
        try
        {
            foreach (var binding in fixedSources)
            {
                var image = binding.Image;
                if (image.TextureSystem is not (0 or 3) || image.Depth != 1)
                    throw new NotSupportedException("Default registered textures require planar or array images.");
                byte[][] chains = Enumerable.Range(0, image.LayerCount)
                    .Select(layer => image.GetEncodedLayerChain(binding.Resource.Source.Bytes, layer)).ToArray();
                owned.Add(new(await UploadAsync(binding, chains, G1TextureSamplerReader.ReadRetainedSource(image),
                    image.TextureSystem == 3 ? RenderingDevice.TextureType.Type2DArray : RenderingDevice.TextureType.Type2D,
                    explicitBgraNativeViewFormat, allowReadback)));
            }
            var lutImage = lutSource.Image;
            owned.Add(new(await UploadAsync(lutSource,
                [lutImage.EncodedDataRange.Slice(lutSource.Resource.Source.Bytes).ToArray()],
                G1TextureSamplerReader.ReadRetainedSource(lutImage), RenderingDevice.TextureType.Type3D,
                explicitBgraNativeViewFormat, allowReadback)));
            var cubeEntry = new SourceResourceRegistrationDeviceEntry(borrowedCube.Source.Definition.ObjectId,
                borrowedCube.Source, borrowedCube.TextureRid, borrowedCube.SamplerRid, borrowedCube.NativeViewFormat,
                RenderingDevice.TextureType.Cube, RenderingDevice.DataFormat.Bc6HUfloatBlock,
                Array.AsReadOnly(Enumerable.Range(0, 6).Select(face => Convert.ToHexString(SHA256.HashData(
                    GatherCubeFace(borrowedCube.Source, face)))).ToArray()), borrowedCube.SourceProvenance);
            var result = new SourceResourceRegistrationDevice(sources, borrowedCube, owned.ToArray(), cubeEntry, allowReadback);
            result.RequireAvailable();
            return result;
        }
        catch
        {
            await ReleaseOwnedAsync(owned);
            throw;
        }
    }

    private static byte[] GatherCubeFace(StaticTextureBinding source, int face)
    {
        using var stream = new MemoryStream();
        for (int mip = 0; mip < source.Image.MipCount; mip++)
            stream.Write(SourceResourceRegistrationAmbientCube.GetNativeFaceMip(source, mip, face));
        return stream.ToArray();
    }

    private static async Task<SourceResourceRegistrationDeviceEntry> UploadAsync(StaticTextureBinding source,
        byte[][] chains, G1TextureSamplerRecipe sampler, RenderingDevice.TextureType kind,
        uint nativeFormat, bool allowReadback)
    {
        var image = source.Image;
        if (image.PixelFormat != 1 || nativeFormat is not (87 or 91))
            throw new NotSupportedException("Default light uploads require BGRA8 images and a compatible BGRA view.");
        var format = nativeFormat == 87 ? RenderingDevice.DataFormat.B8G8R8A8Unorm : RenderingDevice.DataFormat.B8G8R8A8Srgb;
        byte[] samplerDescriptor = sampler.EncodeD3D11Descriptor();
        string[] hashes = chains.Select(chain => Convert.ToHexString(SHA256.HashData(chain))).ToArray();
        string provenance = $"{source.Definitions.Source.ResourceUri}#{source.Definition.ObjectId:X8}@{source.Definitions.Source.DecodedSha256};" +
            $"{source.Resource.Source.ResourceUri}/TID={source.ImageIndex}@{source.Resource.Source.DecodedSha256};native-SRV={nativeFormat};" +
            $"sampler={Convert.ToHexString(SHA256.HashData(samplerDescriptor))};retained-source-bytes";
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var usage = RenderingDevice.TextureUsageBits.SamplingBit;
            if (allowReadback) usage |= RenderingDevice.TextureUsageBits.CanCopyFromBit;
            if (!rd.TextureIsFormatSupportedForUsage(format, usage))
                throw new NotSupportedException("Device cannot retain this original Static BGRA view and usage.");
            Rid texture = default, deviceSampler = default;
            try
            {
                using var shape = new RDTextureFormat
                {
                    Format = format, TextureType = kind, Width = (uint)image.Width, Height = (uint)image.Height,
                    Depth = (uint)image.Depth, ArrayLayers = (uint)image.LayerCount, Mipmaps = (uint)image.MipCount,
                    UsageBits = usage
                };
                using var view = new RDTextureView();
                var data = new Godot.Collections.Array<byte[]>(chains);
                texture = rd.TextureCreate(shape, view, data); data.Clear();
                if (!texture.IsValid || !rd.TextureIsValid(texture))
                    throw new InvalidDataException("Device rejected the exact original Static texture payload.");
                using var actual = rd.TextureGetFormat(texture);
                if (actual.TextureType != kind || actual.Width != image.Width || actual.Height != image.Height ||
                    actual.Depth != image.Depth || actual.ArrayLayers != image.LayerCount || actual.Mipmaps != image.MipCount || actual.Format != format)
                    throw new InvalidDataException("Device changed the original Static texture dimensions, layers, volume or format.");
                if (allowReadback)
                    for (uint layer = 0; layer < chains.Length; layer++)
                        if (!rd.TextureGetData(texture, layer).AsSpan().SequenceEqual(chains[layer]))
                            throw new InvalidDataException("Static texture GPU readback differs from the exact encoded source chain.");
                deviceSampler = DeviceSampler.Create(rd, samplerDescriptor);
                return new SourceResourceRegistrationDeviceEntry(source.Definition.ObjectId, source, texture,
                    deviceSampler, nativeFormat, kind, format, Array.AsReadOnly(hashes), provenance);
            }
            catch
            {
                try { if (deviceSampler.IsValid) rd.FreeRid(deviceSampler); }
                finally { if (texture.IsValid) rd.FreeRid(texture); }
                throw;
            }
        });
    }

    /// <summary>Device resources are fixed for this generation. Format and
    /// shape are checked during upload; borrowing only checks owner lifetime.</summary>
    public void RequireAvailable()
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        cube.RequireAvailable();
    }

    /// <summary>Return cached bindings to the same created resource generation.
    /// Handles are opaque remake RIDs, never original process addresses.</summary>
    public MainLightConstructionInputs GetConstructionInputs(uint explicitContextFlags3E4,
        string explicitRetainedTemplateProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explicitRetainedTemplateProvenance);
        RequireAvailable();
        return new(explicitContextFlags3E4, explicitRetainedTemplateProvenance, constructionBindings);
    }

    public void ValidateConstructionSource(MapSourceCreatedLightingSource lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting); RequireAvailable();
        if (!lighting.AllResourceBindingsReady || lighting.ResourceBindings.Count != Entries.Count ||
            lighting.ResourceBindings.Any(binding => !Entries.TryGetValue(binding.SourceObjectKey, out var entry) ||
                binding.DataHandle != entry.TextureRid.Id || binding.OwnerProvenance != entry.OwnerProvenance ||
                !binding.CpuDataReady || binding.InterfaceClassId != MainLightConstruction.EnvmapInterfaceClassId))
            throw new InvalidDataException("Constructed lighting handles differ from this actual five-resource device generation.");
    }

    public async Task<byte[]> ReadEncodedChainAsync(uint sourceObjectKey, uint layer = 0)
    {
        if (!allowReadback) throw new InvalidOperationException("Registration owner was not created with readback enabled.");
        await gate.WaitAsync();
        try
        {
            RequireAvailable();
            if (!Entries.TryGetValue(sourceObjectKey, out var entry) || layer >= entry.Source.Image.LayerCount)
                throw new ArgumentOutOfRangeException(nameof(sourceObjectKey));
            if (entry.TextureType == RenderingDevice.TextureType.Cube) return await cube.ReadEncodedFaceAsync(layer);
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd => rd.TextureGetData(entry.TextureRid, layer));
        }
        finally { gate.Release(); }
    }

    // Keep partial cleanup progress with the resource that was created.
    // A failed sampler release must not prevent its image or later owners from retiring.
    private sealed class OwnedTexture(SourceResourceRegistrationDeviceEntry entry)
    {
        public SourceResourceRegistrationDeviceEntry Entry { get; } = entry;
        private bool samplerReleased;
        private bool textureReleased;

        public Task ReleaseAsync() => TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            List<Exception> failures = [];
            try
            {
                if (!samplerReleased) { rd.FreeRid(Entry.SamplerRid); samplerReleased = true; }
            }
            catch (Exception error) { failures.Add(error); }
            try
            {
                if (!textureReleased) { rd.FreeRid(Entry.TextureRid); textureReleased = true; }
            }
            catch (Exception error) { failures.Add(error); }
            if (failures.Count != 0) throw new AggregateException("Registered texture cleanup failed.", failures);
            return true;
        });
    }

    private static async Task ReleaseOwnedAsync(IEnumerable<OwnedTexture> resources)
    {
        List<Exception> failures = [];
        foreach (var resource in resources.Reverse())
            try { await resource.ReleaseAsync(); }
            catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0) throw new AggregateException("Resource registration cleanup failed.", failures);
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
        try { await ReleaseOwnedAsync(owned); }
        finally { gate.Release(); }
    }
}
