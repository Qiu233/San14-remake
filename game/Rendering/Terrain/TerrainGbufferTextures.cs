using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record TerrainGbufferDeviceTexture(uint SourceRegister, Rid Texture, Rid Sampler,
    G1TextureSamplerRecipe Recipe, string SourceUri, bool GeneratedInitialControl);

/// <summary>Original full mip/layer inputs for the stock terrain shader pairs.
/// Height and generated initial control have their proven point/clamp consumer state.</summary>
public sealed class TerrainGbufferTextures : IAsyncDisposable
{
    public LoadedTerrainGbiMapResources Source { get; }
    public IReadOnlyDictionary<uint, TerrainGbufferDeviceTexture> Pixel { get; }
    public TerrainGbufferDeviceTexture Height { get; }
    private readonly IReadOnlyList<TerrainDeviceTexture> _sourceTextures;
    private readonly IReadOnlyList<Rid> _owned;
    private Task? _disposal;
    private TerrainGbufferTextures(LoadedTerrainGbiMapResources source,
        Dictionary<uint, TerrainGbufferDeviceTexture> pixel, TerrainGbufferDeviceTexture height,
        List<TerrainDeviceTexture> textures, List<Rid> owned)
        => (Source, Pixel, Height, _sourceTextures, _owned) = (source, pixel, height, textures, owned);

    public static async Task<TerrainGbufferTextures> BuildAsync(LoadedTerrainGbiMapResources source,
        LoadedTerrainInitialHeightResources initialHeight)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(initialHeight);
        if (source.Field.TableSource.DecodedSha256 != initialHeight.Field.TableSource.DecodedSha256 ||
            source.Field.TableSource.ResourceUri != initialHeight.Field.TableSource.ResourceUri)
            throw new InvalidDataException("Terrain shader height and texture groups differ.");
        var textures = new List<TerrainDeviceTexture>(); var owned = new List<Rid>();
        var pixel = new Dictionary<uint, TerrainGbufferDeviceTexture>();
        try
        {
            async Task<TerrainGbufferDeviceTexture> Upload(LoadedTerrainTextureGroup group, uint slot, uint register,
                bool rgbOnly = false)
            {
                TerrainTextureBinding binding = group.Bindings.Single(b => b.Slot == slot);
                bool srgb = (binding.Image.RawHeader[19] & 1) != 0;
                var texture = await TerrainDeviceTexture.BuildAsync(group, slot, srgb, rgbOnly);
                textures.Add(texture);
                var recipe = G1TextureSamplerReader.ReadRetainedSource(binding.Image);
                Rid sampler = await TerrainDeviceTexture.OnRenderThreadAsync(rd => DeviceSampler.Create(rd, recipe));
                owned.Add(sampler);
                return new(register, texture.DeviceTextureRid, sampler, recipe, binding.Resource.Source.ResourceUri, false);
            }
            TerrainGbufferDeviceTexture height = await Upload(initialHeight.Field, 0, 8);
            // This is the recovered zero-brush generated output, not the raw BGRA Field1 input.
            Rid control = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                using var format = new RDTextureFormat {
                    Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
                    TextureType = RenderingDevice.TextureType.Type2D,
                    Width = (uint)source.InitialControl.SourceMap.Width, Height = (uint)source.InitialControl.SourceMap.Height,
                    Depth = 1, ArrayLayers = 1, Mipmaps = 1,
                    UsageBits = RenderingDevice.TextureUsageBits.SamplingBit };
                using var view = new RDTextureView();
                Rid rid = rd.TextureCreate(format, view, [source.InitialControl.RgbaPixels]);
                if (!rid.IsValid) throw new InvalidDataException("Generated control texture creation failed.");
                return rid;
            });
            owned.Add(control);
            // Normal terrain consumes height and generated control with identical point/clamp state.
            Rid controlSampler = await TerrainDeviceTexture.OnRenderThreadAsync(rd => DeviceSampler.Create(rd, height.Recipe));
            owned.Add(controlSampler);
            pixel.Add(3, new(3, control, controlSampler, height.Recipe,
                source.InitialControl.Binding.Resource.Source.ResourceUri, true));
            pixel.Add(4, await Upload(source.Field, 2, 4));
            pixel.Add(5, await Upload(source.Field, 3, 5));
            pixel.Add(6, await Upload(source.Field, 4, 6));
            pixel.Add(7, await Upload(source.Root, 0, 7));
            pixel.Add(8, await Upload(source.Root, 1, 8));
            pixel.Add(9, await Upload(source.Root, 2, 9));
            if (source.Auxiliary is not null) pixel.Add(10, await Upload(source.Auxiliary, 0, 10, true));
            return new(source, pixel, height, textures, owned);
        }
        catch
        {
            await Release(owned, textures);
            throw;
        }
    }

    private static async Task Release(IReadOnlyList<Rid> owned, IReadOnlyList<TerrainDeviceTexture> textures)
    {
        await TerrainDeviceTexture.OnRenderThreadAsync(rd => {
            for (int i = owned.Count - 1; i >= 0; i--) rd.FreeRid(owned[i]); return true; });
        for (int i = textures.Count - 1; i >= 0; i--) await textures[i].DisposeAsync();
    }
    public ValueTask DisposeAsync() => new(_disposal ??= Release(_owned, _sourceTextures));
}
