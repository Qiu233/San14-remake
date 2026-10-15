using System.Numerics;

namespace San14.Data;

/// <summary>Fresh ordinary terrain parameter owner. Imported defaults are
/// separate from its current lifetime; an external setter retires this owner.</summary>
public sealed class TerrainGbufferSourceParameters
{
    public const uint CliffXBits = 0x3F13B646;
    public const uint CliffYBits = 0;
    private readonly Vector2 nearFar;
    private bool retained = true;
    private TerrainGbufferSourceParameters(LoadedTerrainGbiMapResources source, string locale)
    {
        Locale = locale; RuleVersion = RenderRuleIdentity.Version; nearFar = source.NearFar;
        SourceProvenance = $"ordinary-terrain-parameters/{locale}/{RuleVersion};" +
            $"{source.Shader.Shader.Source.ResourceUri}@{source.Shader.Shader.Source.DecodedSha256};" +
            $"scene={source.Parameters.Scene.Scene.Source.ResourceUri}@{source.Parameters.Scene.Scene.Source.DecodedSha256};" +
            "constructor-cliff-float2;ordinary-draw-retains-cliff;no-external-setter-history";
    }
    public string Locale { get; }
    public string RuleVersion { get; }
    public string SourceProvenance { get; }
    public bool RetainsOwnedConstructorCliffInfo => retained;

    public static TerrainGbufferSourceParameters CreateFresh(string locale, LoadedTerrainGbiMapResources material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (locale is not ("SC" or "TC"))
            throw new InvalidDataException("Ordinary terrain parameter owner requires SC or TC.");
        // The material factory has already resolved source links, selected the
        // actual programs and imported their layouts and NearFar values.
        return new(material, locale);
    }
    public Vector2 PrepareCliffInfo()
    {
        RequireRetained();
        return new(BitConverter.UInt32BitsToSingle(CliffXBits), BitConverter.UInt32BitsToSingle(CliffYBits));
    }
    public Vector2 PrepareNearFar() { RequireRetained(); return nearFar; }
    public void MarkImportedOrEdited(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter); retained = false; }
    private void RequireRetained()
    {
        if (!retained)
            throw new InvalidDataException("Terrain parameters were replaced; complete current values are required.");
    }
}
