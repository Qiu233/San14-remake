using System.Numerics;

namespace San14.Data;

/// <summary>The source TerrainType2 callback seam published by CE BB1CF0.
/// Coordinates arrive in world X/Z because both wrapper-offset flags are false.
/// Retains the current Field grid; declaration checks occur once when binding.</summary>
public sealed class MapProceduralTerrainCallbacksSource
{
    private readonly MapStaticModelGroundHeightSource _field;
    private readonly Func<float, float, Vector3>? _actualNormalA0B120;

    private MapProceduralTerrainCallbacksSource(
        MapStaticModelGroundHeightSource field,
        Func<float, float, Vector3>? actualNormalA0B120, bool normalValuesRequired)
    {
        _field = field;
        _actualNormalA0B120 = actualNormalA0B120;
        NormalValuesRequired = normalValuesRequired;
    }

    /// <summary>BC6540 samples normal only for element bit0 (alignment) or
    /// bit2 (slope admission). Actual CE scene 811F81AF has flags8 throughout.</summary>
    public bool NormalValuesRequired { get; }
    public bool HeightWrapperOffset => false;
    public bool NormalWrapperOffset => false;
    public MapStaticModelGroundHeightSource CurrentField => _field;
    public string SourceEvidence => "CE:BB1CF0(TerrType2;offsetfalse);" +
        "typed/references:BC8B03->A0A830;BC8BC5->A0B120;" +
        "BC6540-normal-only-elementFlags&5";

    public static MapProceduralTerrainCallbacksSource BindCeTerrainType2(
        ProceduralPlacementDeclaration declaration,
        MapStaticModelGroundHeightSource currentField,
        Func<float, float, Vector3>? actualNormalA0B120 = null)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(currentField);
        if (declaration.TerrainType != 2)
            throw new InvalidDataException("CE landscape callback publication requires source TerrainType2.");
        // These are source-load identity/directory checks, not per-query tokens.
        if (!currentField.Scene.Dependencies.Any(d =>
                d.Resource.Source.ResourceUri == declaration.OwnerSource.Source.ResourceUri &&
                d.Resource.Source.DecodedSha256 == declaration.OwnerSource.Source.DecodedSha256))
            throw new InvalidDataException("Procedural owner and current terrain Field belong to different scenes.");
        bool requiresNormal = declaration.Elements.Any(element => (element.Flags & 5u) != 0);
        if (requiresNormal && actualNormalA0B120 is null)
            throw new InvalidDataException("This population samples the native A0B120 normal; its actual producer is required.");
        return new(currentField, actualNormalA0B120, requiresNormal);
    }

    /// <summary>Published BC8AC0 reaches CE A0A830 (SC A0B830). Height uses
    /// packed24 samples, original low-byte diagonals and native scalar order.
    /// No wrapper offset, renderer mip or detail-normal texture is applied.</summary>
    public float QueryHeight(float worldX, float worldZ) =>
        UnitWorldGroundHeight.QueryA0B830(_field.Grid, worldX, worldZ);

    /// <summary>Published BC8B70 reaches CE A0B120. Its five-percent edge
    /// blending is an independent CPU producer, not the VS1 four-sample kernel.
    /// Actual flags8 populations do not call this method. A caller adding
    /// normal-dependent elements must supply that producer at binding.</summary>
    public Vector3 QueryNormal(float worldX, float worldZ) =>
        _actualNormalA0B120 is { } normal ? normal(worldX, worldZ) :
        throw new InvalidOperationException("Native A0B120 normal is not supplied; source flags8 do not sample it.");
}
