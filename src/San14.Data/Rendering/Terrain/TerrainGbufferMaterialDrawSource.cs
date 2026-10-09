using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

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
    internal TerrainGbufferMaterialDrawSource(string locale, string ruleVersion, Vector4 value, uint flags)
    {
        Locale = locale; RuleVersion = ruleVersion;
        specular = value; materialFlags = flags;
        SourceProvenance = $"{ruleVersion};" +
            "ordinary-terrain-material;flags-bit4-skips-material-modifiers;" +
            "material-field20/count1/spec-bit100/group-index2;" +
            "san14://rdb/system/DC52FD1C";
    }

    public string Locale { get; }
    public string RuleVersion { get; }
    public string SourceProvenance { get; }
    public uint MaterialModeCount => 1;

    public static TerrainGbufferMaterialDrawSource Load(GameResourceRepository repository,
        string locale, LoadedTerrainGbiMapResources material)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(material);
        var shader = material.Shader.Shader.Source;
        if (shader.FileId != 0xDC52FD1C)
            throw new InvalidDataException("Ordinary F8 material draw requires its inspected DC52 shader source.");
        var container = G2ShaderContainerReader.Read(shader.Bytes);
        // Module+84 is a straight copy of source file+54, not a mode inferred
        // from selected shader names. Count1 enables the generic material setter.
        if (BinaryPrimitives.ReadUInt32LittleEndian(container.RawHeader.AsSpan(0x54, 4)) != 1)
            throw new InvalidDataException("Ordinary F8 material draw requires the source single material capability.");
        foreach (int pixel in new[] { 52, 54 })
        {
            var rdef = DxbcResourceDefinitionReader.Read(container.GetProgramBytes(shader.Bytes, pixel));
            var variable = rdef.Buffers.Single().Variables.Single(v => v.Name.Text == "cSpec");
            var type = rdef.Types.Single(t => t.Offset == variable.TypeOffset);
            if (variable.StartOffset != (pixel == 52 ? 16 : 0) || variable.ByteSize != 16 ||
                type.Class != 1 || type.BaseType != 3 || type.Rows != 1 || type.Columns != 4 || type.Elements != 0)
                throw new InvalidDataException("Ordinary near/coarse cSpec is not the source material float4.");
        }
        return RenderSamplerSourceAdapters.ReadOrdinaryTerrainMaterialDrawSource(repository.SourceDirectory, locale);
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

public static partial class RenderSamplerSourceAdapters
{
    internal static TerrainGbufferMaterialDrawSource ReadOrdinaryTerrainMaterialDrawSource(
        string sourceDirectory, string locale)
    {
        string ruleVersion = RenderRuleIdentity.For("terrain-gbuffer-material-draw", locale);
        return new(locale, ruleVersion, new Vector4(1,1,1,2), 0x100);
    }
}
