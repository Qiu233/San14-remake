using San14.Data.Formats;

namespace San14.Data;

/// <summary>Stock screen initialization, with unit scales, white color, no alpha test/blend/custom VS.</summary>
public sealed record LoadedTerrainInitialControlResources(TerrainTextureBinding Binding,
    G1FieldIndexMap SourceMap, byte[] RgbaPixels)
{
    public const string StateProfile = "ce-stock-screen-initialization-v1";
}

public static class TerrainInitialControlResources
{

    /// <summary>This initializes the replica's stock state; it does not sample mutable original context state.</summary>
    public static LoadedTerrainInitialControlResources LoadStockDefault(LoadedTerrainTextureGroup field)
    {
        ArgumentNullException.ThrowIfNull(field);
        LoadedTerrainFieldIndexMap source = TerrainTextureResources.ReadFieldIndexMap(field);
        TerrainTextureBinding binding = source.Binding;
        G1TextureSamplerRecipe sampler = G1TextureSamplerReader.ReadRetainedSource(binding.Image);
        if (sampler.Filter != 0x14 ||
            sampler.AddressU is not (1 or 3) || sampler.AddressV is not (1 or 3) ||
            sampler.MipLodBiasBits != 0 || sampler.MinimumLodBits != 0 || sampler.MaximumLodBits != 0 ||
            sampler.MaxAnisotropy != 1 || (binding.Image.RawHeader[19] & 1) != 0)
            throw new InvalidDataException("Initial control requires a linear UNORM source with one mip and stock addressing.");
        return new(binding, source.Map, ConvertStockCenterBgraToRgba(source.Map.CopyRawMip()));
    }

    // Pixel centers preserve the source RGB under the verified stock 1:1 viewport.
    // Stock PS1 writes white vertex alpha, independently of source texture alpha.
    public static byte[] ConvertStockCenterBgraToRgba(ReadOnlySpan<byte> bgra)
    {
        if (bgra.Length == 0 || bgra.Length % 4 != 0) throw new InvalidDataException("Incomplete initial control texels.");
        byte[] rgba = new byte[bgra.Length];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = bgra[i + 2]; rgba[i + 1] = bgra[i + 1]; rgba[i + 2] = bgra[i]; rgba[i + 3] = 255;
        }
        return rgba;
    }
}
