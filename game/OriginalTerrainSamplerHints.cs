using San14.Data.Formats;

/// <summary>
/// Godot declarations for the verified, retained-source sampler recipes.
/// A declaration is an adapter to a supplied original recipe, not a global default.
/// Mixed/mirrored/border addressing, bias, mip clamps and anisotropy need RD state.
/// </summary>
public static class OriginalTerrainSamplerHints
{
    public static string Create(G1TextureSamplerRecipe recipe,bool sourceColor=false,uint configuredAnisotropy=1)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if((recipe.MaxAnisotropy!=1 && (recipe.Filter!=0x55 || recipe.MaxAnisotropy!=configuredAnisotropy)) || recipe.MipLodBiasBits!=0 || recipe.MinimumLodBits!=0 ||
            recipe.ComparisonFunction!=4 || recipe.AddressU!=recipe.AddressV || recipe.AddressU is not (1 or 3))
            throw new NotSupportedException("This original sampler requires explicit RenderingDevice state.");
        string filter=recipe.Filter switch
        {
            0x00 when recipe.MaximumLodBits==0 => "filter_nearest",
            0x14 when recipe.MaximumLodBits==0 => "filter_linear",
            0x15 when recipe.MaximumLodBits==0x7F7FFFFF => "filter_linear_mipmap",
            0x55 when recipe.MaximumLodBits==0x7F7FFFFF && recipe.MaxAnisotropy==configuredAnisotropy => "filter_linear_mipmap_anisotropic",
            _ => throw new NotSupportedException("This original filter/LOD range requires explicit RenderingDevice state.")
        };
        // Sampler W is not a spatial coordinate for 2D or 2DArray shaders.
        // The original W mode remains in the recipe and D3D descriptor.
        return (sourceColor ? "source_color, " : "")+filter+
            (recipe.AddressU==1 ? ", repeat_enable" : ", repeat_disable");
    }
}
