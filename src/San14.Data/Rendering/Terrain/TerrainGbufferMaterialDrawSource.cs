using System.Numerics;

namespace San14.Data;

public sealed record TerrainGbufferMaterialDrawPreparation(Vector4 Specular,
    uint RawMaterialFlags, uint NextCachedFlags);

/// <summary>The ordinary Landscape F8 draw's material write, after the generic
/// owner's constructor. This source is loaded once. A selected F8 call skips
/// the intervening modifier body and submits its complete cSpec float4, even
/// when the previous material flags already equal 100h. The float4 setter may
/// deduplicate an equal value; it does not rescale roughness.</summary>
public sealed class TerrainGbufferMaterialDrawSource
{
    private readonly Vector4 specular;
    private readonly uint materialFlags;
    private TerrainGbufferMaterialDrawSource(string locale, string ruleVersion, LoadedTerrainGbiMapResources material)
    {
        Locale = locale; RuleVersion = ruleVersion;
        specular = new(1, 1, 1, 2); materialFlags = 0x100;
        SourceProvenance = $"{ruleVersion};" +
            "ordinary-terrain-material;flags-bit4-skips-material-modifiers;" +
            "material-field20/count1/spec-bit100/group-index2;" +
            material.Shader.Shader.Source.ResourceUri;
    }

    public string Locale { get; }
    public string RuleVersion { get; }
    public string SourceProvenance { get; }
    public uint MaterialModeCount => 1;

    public static TerrainGbufferMaterialDrawSource CreateFresh(string locale, LoadedTerrainGbiMapResources material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return new(locale, RenderRuleIdentity.For("terrain-gbuffer-material-draw", locale), material);
    }

    /// <summary>For a successfully selected ordinary F8 renderer, with the
    /// caller's actual current call flags. The renderer selection/override
    /// lifecycle belongs to its caller. Other routes can execute named/material
    /// modifiers and require their own source producer.</summary>
    public TerrainGbufferMaterialDrawPreparation PrepareSelectedOrdinaryF8(byte currentCallFlags)
    {
        if ((currentCallFlags & 0x10) == 0 || (currentCallFlags & (4 | 8)) != 0)
            throw new NotSupportedException("Material producer covers the selected ordinary F8 bit4-set/bit2-bit3-clear route.");
        return new(specular, materialFlags, materialFlags);
    }
}
