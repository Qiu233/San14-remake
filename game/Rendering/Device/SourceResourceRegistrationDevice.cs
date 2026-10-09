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
    private readonly SourceResourceRegistrationDeviceEntry[] owned;
    private readonly bool allowReadback;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetimeGate = new();
    private volatile bool retiring;
    private Task? disposal;
    public bool IsRetiring => retiring;

    private SourceResourceRegistrationDevice(SourceResourceRegistrationGameDefault source,
        SourceResourceRegistrationAmbientCubeDevice cube,
        SourceResourceRegistrationDeviceEntry[] owned, SourceResourceRegistrationDeviceEntry cubeEntry, bool allowReadback)
    {
        this.cube = cube; this.owned = owned; this.allowReadback = allowReadback;
        Source = source;
        Entries = new ReadOnlyDictionary<uint, SourceResourceRegistrationDeviceEntry>(
            owned.Append(cubeEntry).ToDictionary(entry => entry.SourceObjectKey));
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
        SourceResourceRegistrationAmbientCube.Validate(borrowedCube.Source);
        var sources = SourceResourceRegistration.LoadGameDefault(repository);
        var fixedSources = sources.FixedSources;
        var lutSource = sources.HdrLut;
        var owned = new List<SourceResourceRegistrationDeviceEntry>();
        try
        {
            foreach (var binding in fixedSources)
            {
                var identity = SourceResourceRegistrationFixed.Identities.Single(value => value.ObjectKey == binding.Definition.ObjectId);
                SourceResourceRegistrationFixed.Validate(binding, identity);
                var image = binding.Image;
                byte[][] chains = Enumerable.Range(0, image.LayerCount)
                    .Select(layer => SourceResourceRegistrationFixed.GetNativeLayerBytes(binding, layer).ToArray()).ToArray();
                owned.Add(await UploadAsync(binding, chains, G1TextureSamplerReader.ReadRetainedSource(image),
                    image.LayerCount == 1 ? RenderingDevice.TextureType.Type2D : RenderingDevice.TextureType.Type2DArray,
                    explicitBgraNativeViewFormat, allowReadback));
            }
            // This source has a narrow validated system2 parser. Re-reading it
            // through a broader G1T loader would lose its closed source profile.
            SourceResourceRegistrationLut.Validate(lutSource);
            owned.Add(await UploadAsync(lutSource, [SourceResourceRegistrationLut.GetNativeBody(lutSource).ToArray()],
                SourceResourceRegistrationLut.ReadSampler(lutSource), RenderingDevice.TextureType.Type3D,
                explicitBgraNativeViewFormat, allowReadback));
            var cubeEntry = new SourceResourceRegistrationDeviceEntry(borrowedCube.Source.Definition.ObjectId,
                borrowedCube.Source, borrowedCube.TextureRid, borrowedCube.SamplerRid, borrowedCube.NativeViewFormat,
                RenderingDevice.TextureType.Cube, RenderingDevice.DataFormat.Bc6HUfloatBlock,
                Array.AsReadOnly(Enumerable.Range(0, 6).Select(face => Convert.ToHexString(SHA256.HashData(
                    GatherCubeFace(borrowedCube.Source, face)))).ToArray()), borrowedCube.SourceProvenance);
            var result = new SourceResourceRegistrationDevice(sources, borrowedCube, owned.ToArray(), cubeEntry, allowReadback);
            await result.ValidateAsync();
            return result;
        }
        catch
        {
            foreach (var entry in owned.AsEnumerable().Reverse()) await FreeAsync(entry);
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
        var format = nativeFormat == 87 ? RenderingDevice.DataFormat.B8G8R8A8Unorm : RenderingDevice.DataFormat.B8G8R8A8Srgb;
        string[] hashes = chains.Select(chain => Convert.ToHexString(SHA256.HashData(chain))).ToArray();
        string provenance = $"{source.Definitions.Source.ResourceUri}#{source.Definition.ObjectId:X8}@{source.Definitions.Source.DecodedSha256};" +
            $"{source.Resource.Source.ResourceUri}/TID={source.ImageIndex}@{source.Resource.Source.DecodedSha256};native-SRV={nativeFormat};" +
            $"sampler={Convert.ToHexString(SHA256.HashData(sampler.EncodeD3D11Descriptor()))};retained-source-bytes";
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
                deviceSampler = DeviceSampler.Create(rd, sampler);
                return new SourceResourceRegistrationDeviceEntry(source.Definition.ObjectId, source, texture,
                    deviceSampler, nativeFormat, kind, format, Array.AsReadOnly(hashes), provenance);
            }
            catch
            {
                if (deviceSampler.IsValid) rd.FreeRid(deviceSampler);
                if (texture.IsValid) rd.FreeRid(texture);
                throw;
            }
        });
    }

    public async Task ValidateAsync()
    {
        await gate.WaitAsync();
        try { ObjectDisposedException.ThrowIf(retiring, this); await ValidateOnDeviceAsync(); }
        finally { gate.Release(); }
    }

    private Task<bool> ValidateOnDeviceAsync()
    {
        SourceResourceRegistrationAmbientCube.Validate(cube.Source);
        foreach (var entry in owned)
            if (entry.SourceObjectKey == SourceResourceRegistrationLut.ObjectKey) SourceResourceRegistrationLut.Validate(entry.Source);
            else SourceResourceRegistrationFixed.Validate(entry.Source,
                SourceResourceRegistrationFixed.Identities.Single(identity => identity.ObjectKey == entry.SourceObjectKey));
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            foreach (var entry in Entries.Values)
            {
                if (!entry.TextureRid.IsValid || !entry.SamplerRid.IsValid || !rd.TextureIsValid(entry.TextureRid))
                    throw new InvalidDataException($"Static source owner{entry.SourceObjectKey:X8} has retired.");
                using var actual = rd.TextureGetFormat(entry.TextureRid);
                var image = entry.Source.Image;
                if (actual.TextureType != entry.TextureType || actual.Format != entry.Format || actual.Width != image.Width ||
                    actual.Height != image.Height || actual.Depth != image.Depth || actual.ArrayLayers != image.LayerCount ||
                    actual.Mipmaps != image.MipCount || !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                    throw new InvalidDataException("Registered source texture no longer has its original shape and sampled encoding.");
            }
            return true;
        });
    }

    /// <summary>Only actual ready device images become loader Data handles.
    /// The values are opaque remake RIDs, never original process addresses.</summary>
    public async Task<MainLightConstructionInputs> GetConstructionInputsAsync(uint explicitContextFlags3E4,
        string explicitRetainedTemplateProvenance)
    {
        if (string.IsNullOrWhiteSpace(explicitRetainedTemplateProvenance))
            throw new InvalidDataException("Source registration requires the complete retained template provenance.");
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring, this); await ValidateOnDeviceAsync();
            var bindings = Entries.Values.OrderBy(entry => entry.SourceObjectKey).Select(entry =>
                new MainLightConstructionResourceBinding(entry.SourceObjectKey, entry.TextureRid.Id,
                    entry.OwnerProvenance, true, MainLightConstruction.EnvmapInterfaceClassId)).ToArray();
            return new(explicitContextFlags3E4, explicitRetainedTemplateProvenance, Array.AsReadOnly(bindings));
        }
        finally { gate.Release(); }
    }

    public void ValidateConstructionSource(MapSourceCreatedLightingSource lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting); ObjectDisposedException.ThrowIf(retiring, this);
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
            ObjectDisposedException.ThrowIf(retiring, this); await ValidateOnDeviceAsync();
            if (!Entries.TryGetValue(sourceObjectKey, out var entry) || layer >= entry.Source.Image.LayerCount)
                throw new ArgumentOutOfRangeException(nameof(sourceObjectKey));
            if (entry.TextureType == RenderingDevice.TextureType.Cube) return await cube.ReadEncodedFaceAsync(layer);
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd => rd.TextureGetData(entry.TextureRid, layer));
        }
        finally { gate.Release(); }
    }

    private static Task<bool> FreeAsync(SourceResourceRegistrationDeviceEntry entry) =>
        TerrainDeviceTexture.OnRenderThreadAsync(rd => { rd.FreeRid(entry.SamplerRid); rd.FreeRid(entry.TextureRid); return true; });
    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate) { retiring = true; return new(disposal ??= DisposeCoreAsync()); }
    }
    private async Task DisposeCoreAsync()
    {
        await gate.WaitAsync();
        try { foreach (var entry in owned.Reverse()) await FreeAsync(entry); }
        finally { gate.Release(); }
    }
}
