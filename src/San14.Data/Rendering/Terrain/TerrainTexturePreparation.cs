using San14.Data.Formats;

namespace San14.Data;

/// <summary>Owned encoded planes or layer mip chains, ready for a texture uploader.</summary>
public sealed class TerrainTextureUpload
{
    public TerrainTextureBinding Binding { get; }
    public G1TextureImage Image { get; }
    public bool Srgb { get; }
    public IReadOnlyList<ReadOnlyMemory<byte>> Layers { get; }

    internal TerrainTextureUpload(TerrainTextureBinding binding, bool srgb, byte[][] layers)
    {
        Binding = binding;
        Image = binding.Image;
        Srgb = srgb;
        Layers = Array.AsReadOnly(layers.Select(bytes => (ReadOnlyMemory<byte>)bytes).ToArray());
    }
}

/// <summary>Prepares source texture bytes without a graphics device or a second import.</summary>
public static class TerrainTexturePreparation
{
    public static TerrainTextureBinding Select(LoadedTerrainTextureGroup group, uint slot)
    {
        ArgumentNullException.ThrowIfNull(group);
        TerrainTextureBinding? selected = null;
        foreach (TerrainTextureBinding binding in group.Bindings)
        {
            if (binding.Slot != slot) continue;
            if (selected is not null)
                throw new InvalidDataException($"Terrain table {group.TableSource.ResourceUri} repeats slot {slot}.");
            selected = binding;
        }
        return selected ?? throw new InvalidDataException($"Terrain table {group.TableSource.ResourceUri} has no slot {slot}.");
    }

    public static void RequireLayer(TerrainTextureBinding binding, int layer)
    {
        if ((uint)layer >= GetImage(binding).LayerCount) throw new ArgumentOutOfRangeException(nameof(layer));
    }

    public static TerrainTextureUpload PrepareDevice(TerrainTextureBinding binding,
        bool srgb = false, bool allowBc1RgbView = false)
    {
        G1TextureImage image = GetImage(binding);
        if (image.PixelFormat is not (0x00 or 0x01 or 0x02 or 0x59 or 0x5B) || image.SubresourceLayout is not
            (G1TextureSubresourceLayout.MipMajorArray or G1TextureSubresourceLayout.SingleLayerMipChain))
            throw new InvalidDataException($"Terrain texture {binding.Resource.Source.ResourceUri} has no supported exact mip/layer layout.");
        if (image.PixelFormat == 0x02 && srgb)
            throw new InvalidDataException("Terrain float textures cannot use an sRGB view.");
        if (image.PixelFormat == 0x59 && !allowBc1RgbView)
            throw new NotSupportedException("BC1 device upload requires an explicit RGB-only consumer.");

        var layers = new byte[image.LayerCount][];
        int chainLength = image.Mips.Sum(mip => mip.BytesPerLayer);
        for (int layer = 0; layer < layers.Length; layer++)
        {
            byte[] chain = new byte[chainLength];
            int offset = 0;
            for (int level = 0; level < image.MipCount; level++)
            {
                ReadOnlySpan<byte> mip = image.GetEncodedMip(binding.Resource.Source.Bytes, level, layer);
                if (mip.Length != image.Mips[level].BytesPerLayer)
                    throw new InvalidDataException("Terrain mip range differs from its imported byte length.");
                mip.CopyTo(chain.AsSpan(offset));
                offset += mip.Length;
            }
            layers[layer] = chain;
        }
        return new(binding, srgb, layers);
    }

    /// <summary>Compatibility textures use base mip only; only BC3 arrays have a supported array view.</summary>
    public static TerrainTextureUpload PrepareCompatibility(TerrainTextureBinding binding, int selectedLayer = 0)
    {
        RequireLayer(binding, selectedLayer);
        G1TextureImage image = binding.Image;
        if (image.PixelFormat is not (0x00 or 0x01 or 0x59 or 0x5B))
            throw new InvalidDataException($"Unsupported preview color format {image.PixelFormat:X2}.");
        if (image.LayerCount > 1 && (image.SubresourceLayout != G1TextureSubresourceLayout.MipMajorArray || image.PixelFormat != 0x5B))
            throw new InvalidDataException("Compatibility terrain arrays require the BC3 mip-major profile.");

        var layers = new byte[image.LayerCount > 1 ? image.LayerCount : 1][];
        for (int layer = 0; layer < layers.Length; layer++)
        {
            byte[] bytes = image.GetEncodedMip(binding.Resource.Source.Bytes, 0,
                image.LayerCount > 1 ? layer : selectedLayer).ToArray();
            if (image.PixelFormat == 0x01)
                for (int at = 0; at < bytes.Length; at += 4)
                    (bytes[at], bytes[at + 2]) = (bytes[at + 2], bytes[at]);
            layers[layer] = bytes;
        }
        return new(binding, false, layers);
    }

    private static G1TextureImage GetImage(TerrainTextureBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if ((uint)binding.TextureIndex >= binding.Resource.Container.Textures.Count)
            throw new InvalidDataException("Terrain image selector exceeds its imported texture container.");
        return binding.Image;
    }
}
