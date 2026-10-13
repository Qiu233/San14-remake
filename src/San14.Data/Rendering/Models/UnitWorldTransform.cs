using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>WorldPQ fields after an explicit constructor/bind/update prefix.
/// The native constructor establishes no parent at +1C0. This finite profile
/// excludes a later parent binding; callers retain and publish current fields.</summary>
public sealed record UnitWorldTransformState(Vector3 Position18, Quaternion Quaternion24,
    Vector3 Scale34, string EvidenceUri, string EvidenceSha256, string Basis)
{
    public Matrix4x4 PositionMatrix() => UnitWorldTransformMath.ScaleQuaternionPositionA23210(
        Scale34, Quaternion24, Position18);

    public float[] PositionShaderRows() => MapStaticModelTransforms.ToShaderAffineRows(PositionMatrix());

    /// <summary>Native draw prefix creates mL2W, transposes its three affine
    /// rows, then prepares mL2WT through the separately rounded scalar inverse.
    /// A determinant-zero input needs an established previous normal buffer and
    /// cannot create a fresh complete renderer transform.</summary>
    public MapStaticModelTransform ShaderTransform()
    {
        float[] positionRows = PositionShaderRows();
        return MapStaticModelTransforms.FromExplicitShaderRows(positionRows,
            UnitWorldTransformNormal.FromPositionShaderRows(positionRows),
            EvidenceUri, EvidenceSha256, Basis + "; native A23210 mL2W / 97C620 mL2WT preparation");
    }

    /// <summary>Rigid primitive slot9/type4 draw preparation. Its explicit
    /// twelve LOCAL shader words are extended with the native literal last row;
    /// native 17ADD0 receives WORLD shader rows first, LOCAL second. mL2WT is
    /// prepared only after that original position product has completed.</summary>
    public MapStaticModelTransform ShaderTransformWithPrimitiveLocalRows(ReadOnlySpan<float> primitiveLocalRows,
        string localEvidenceUri, string localEvidenceSha256)
    {
        float[] localRows = primitiveLocalRows.ToArray();
        MapStaticModelTransforms.ValidateRows(localRows, 12);
        if (string.IsNullOrWhiteSpace(localEvidenceUri) || localEvidenceSha256 is not { Length: 64 } sha || !sha.All(Uri.IsHexDigit))
            throw new InvalidDataException("Primitive LOCAL shader rows require their actual source URI and SHA-256.");
        Matrix4x4 result = UnitWorldTransformMath.ComposeAffine17ADD0(
            ShaderMatrix(PositionShaderRows()), ShaderMatrix(localRows));
        float[] positionRows = [result.M11, result.M12, result.M13, result.M14,
            result.M21, result.M22, result.M23, result.M24,
            result.M31, result.M32, result.M33, result.M34];
        return MapStaticModelTransforms.FromExplicitShaderRows(positionRows,
            UnitWorldTransformNormal.FromPositionShaderRows(positionRows),
            EvidenceUri, EvidenceSha256, Basis + "; native DBA3C0 WORLDshader * LOCALshader; LOCAL " + localEvidenceUri + " SHA256=" + sha);
    }

    private static Matrix4x4 ShaderMatrix(ReadOnlySpan<float> rows) => new(
        rows[0], rows[1], rows[2], rows[3], rows[4], rows[5], rows[6], rows[7],
        rows[8], rows[9], rows[10], rows[11], 0f, 0f, 0f, 1f);

    /// <summary>Unparented native +68/+70/+78 setters preserve raw float bits.
    /// None of these setters normalizes a quaternion.</summary>
    public UnitWorldTransformState PublishPosition(Vector3 value) => this with { Position18 = value };
    public UnitWorldTransformState PublishQuaternion(Quaternion value) => this with { Quaternion24 = value };
    public UnitWorldTransformState PublishScale(Vector3 value) => this with { Scale34 = value };
}

/// <summary>Source WorldPQ constructor and primary City/Gate/Object publication
/// prefixes. This does not execute descriptor allocation, secondary flag nodes,
/// parent binding, resource retirement or a whole native update.</summary>
public static class UnitWorldTransform
{
    public const string NativeContractId = "ce-20261002-model-worldpq-no-parent";
    private static readonly Quaternion UnitModelConstructorQuaternion = new(0, 0, 0, 1);
    // These templates contain only the loaded constructor's value fields. A
    // new document/placement imports its source again; current world setters,
    // ground queries, primitive matrices and visibility are prepared separately.
    private static readonly ConditionalWeakTable<LoadedObjects,
        ConditionalWeakTable<DokObject, UnitWorldTransformState>> DefinitionTemplates = new();
    private static readonly ConditionalWeakTable<ScenePlacement, UnitWorldTransformState> PlacementTemplates = new();

    /// <summary>D9F540 copies the source definition position, XYZW quaternion and
    /// scale verbatim; +1C0 is explicitly cleared by that constructor.</summary>
    public static UnitWorldTransformState ConstructDefinition(LoadedObjects catalog, DokObject definition)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(definition);
        return DefinitionTemplates.GetValue(catalog, _ => new()).GetValue(definition,
            sourceDefinition => ReadDefinitionTemplate(catalog, sourceDefinition));
    }

    private static UnitWorldTransformState ReadDefinitionTemplate(LoadedObjects catalog, DokObject definition)
    {
        SceneTransformValues declaration = WorldTransformDeclarationReader.ReadDefinition(catalog, definition);
        Vector4 q = declaration.Quaternion ??
            throw new InvalidDataException("WorldPQ definition requires an explicit quaternion.");
        return new(declaration.Position, new(q.X, q.Y, q.Z, q.W), declaration.Scale,
            catalog.Source.ResourceUri, catalog.Source.DecodedSha256,
            NativeContractId + $"; D9F540 definition {definition.ObjectId:X8}; explicit constructor no-parent state");
    }

    public static UnitWorldTransformState ConstructDefinition(LoadedModelTextureResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return ConstructDefinition(resources.Catalog ?? throw new InvalidDataException("Model catalog source is unknown."),
            resources.CatalogDefinition ?? throw new InvalidDataException("Model WorldPQ definition source is unknown."));
    }

    /// <summary>Finite scene prefix with an explicit identity definition. Missing
    /// instance Q takes that definition's actual identity Q; this is not a
    /// general RDOK inheritance algorithm. The original consumer uses raw XYZW.</summary>
    public static UnitWorldTransformState ConstructScenePlacement(ScenePlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return PlacementTemplates.GetValue(placement, ReadPlacementTemplate);
    }

    private static UnitWorldTransformState ReadPlacementTemplate(ScenePlacement placement)
    {
        if (placement.Source.Definition.ClassId != ScenePlacementReader.ModelWorldPqClass || placement.Model is null)
            throw new InvalidDataException("WorldPQ scene constructor requires a source Model placement.");
        UnitWorldTransformState definition = ConstructDefinition(placement.Source.DefinitionDocument, placement.Source.Definition);
        if (definition.Position18 != Vector3.Zero || definition.Quaternion24 != UnitModelConstructorQuaternion || definition.Scale34 != Vector3.One)
            throw new InvalidDataException("Scene placement profile requires an explicit identity definition.");
        WorldTransformDeclarationReader.RequireOwnedObject(placement.Source.InstanceDocument, placement.Source.Object);
        DokObject obj = placement.Source.Object;
        if (obj.Kind != DokObjectKind.Reference || obj.Reference is not { } reference ||
            reference.ResourceId != placement.Source.DefinitionDocument.Source.FileId || reference.ObjectId != placement.Source.Definition.ObjectId)
            throw new InvalidDataException("Scene instance reference differs from its explicit definition.");
        SceneTransformValues declaration = placement.InstanceTransform;
        Quaternion rotation = declaration.Quaternion is { } q ? new(q.X, q.Y, q.Z, q.W) : definition.Quaternion24;
        return definition with
        {
            Position18 = declaration.Position,
            Quaternion24 = rotation,
            Scale34 = declaration.Scale,
            EvidenceUri = placement.Source.InstanceDocument.Source.ResourceUri,
            EvidenceSha256 = placement.Source.InstanceDocument.Source.DecodedSha256,
            Basis = NativeContractId + $"; source scene instance {placement.Source.Object.ObjectId:X8}; raw XYZW; explicit identity definition {placement.Source.Definition.ObjectId:X8}"
        };
    }

    /// <summary>Fresh CUnitCity constructor + bind + successful terrain refresh
    /// through the primary descriptor publication prefix. City+20 is the native
    /// explicit constructor identity; node scale is copied from its definition.</summary>
    public static UnitWorldTransformState CreateCityPrimary(LoadedModelTextureResources resources, int hexId,
        UnitGeometry geometry, Func<float, float, float?> groundHeight)
        => GroundPrimary(ConstructDefinition(resources), hexId, geometry, groundHeight,
            UnitModelConstructorQuaternion, false, null, "CUnitCity 1C2B00 / 1C8320 / 1CDC61..1CDC8B");

    /// <summary>Eligible fresh CUnitGate binding uses the actual gate definition
    /// word+70 degrees. The primary node keeps its definition scale.</summary>
    public static UnitWorldTransformState CreateGatePrimary(LoadedModelTextureResources resources, int hexId,
        ushort gateDefinitionHeading70, UnitGeometry geometry, Func<float, float, float?> groundHeight)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        float radians = gateDefinitionHeading70 * BitConverter.Int32BitsToSingle(0x40490FDB);
        radians = radians / 180f;
        Vector4 q = geometry.NativeQuaternion1C58E0(new(0, radians, 0));
        return GroundPrimary(ConstructDefinition(resources), hexId, geometry, groundHeight,
            new(q.X, q.Y, q.Z, q.W), false, null, "CUnitGate 1C83E0 / 1CE1C1..1CE1E8; actual definition word70");
    }

    /// <summary>Fresh CUnitObject primary publication refreshes Y, applies its
    /// explicit 445 lower bound, then publishes the source constant scale1.5.</summary>
    public static UnitWorldTransformState CreateFacilityPrimary(LoadedModelTextureResources resources, int hexId,
        UnitGeometry geometry, Func<float, float, float?> groundHeight)
        => GroundPrimary(ConstructDefinition(resources), hexId, geometry, groundHeight,
            UnitModelConstructorQuaternion, true, new Vector3(1.5f), "CUnitObject 1C2CF0 / 1C84F0 / 1CE3F0; source445 clamp / source1.5 scale");

    /// <summary>Refresh an already established primary transform with current
    /// quaternion/scale preserved. Applies the facility clamp only when named.</summary>
    public static UnitWorldTransformState RefreshGroundHeight(UnitWorldTransformState current,
        Func<float, float, float?> groundHeight, bool facilityLowerBound445 = false)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(groundHeight);
        float y = groundHeight(current.Position18.X, current.Position18.Z) ??
            throw new InvalidDataException("Current source terrain ground height is unknown.");
        if (!float.IsFinite(y)) throw new InvalidDataException("Current source terrain ground height is nonfinite.");
        if (facilityLowerBound445 && y < 445f) y = 445f;
        return current.PublishPosition(new(current.Position18.X, y, current.Position18.Z));
    }

    private static UnitWorldTransformState GroundPrimary(UnitWorldTransformState state, int hexId,
        UnitGeometry geometry, Func<float, float, float?> groundHeight, Quaternion rotation,
        bool clamp445, Vector3? scale, string basis)
    {
        ArgumentNullException.ThrowIfNull(geometry); ArgumentNullException.ThrowIfNull(groundHeight);
        // Bind's X/Z helper accepts its integer input verbatim; current source
        // validity and native fallback-to-Hex483... remain caller responsibilities.
        ModelHexPosition p = geometry.NativeHexPosition187A40(hexId);
        state = state.PublishPosition(new(p.X, state.Position18.Y, p.Z)).PublishQuaternion(rotation);
        state = RefreshGroundHeight(state, groundHeight, clamp445);
        if (scale is { } s) state = state.PublishScale(s);
        return state with { Basis = state.Basis + "; " + basis + "; refreshed native terrain query" };
    }

}
