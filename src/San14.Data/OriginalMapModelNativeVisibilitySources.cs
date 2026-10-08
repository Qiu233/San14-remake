using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One original declared FrustumViewCullingType. Source template
/// values require an explicit current publication before use in a frame.</summary>
public sealed record OriginalMapModelNativeFrustumCullingSource(uint Value,
    string EvidenceUri, string EvidenceSha256, string Basis);

/// <summary>Declared WorldPQ constructor flag bytes. Native D9F540 combines
/// Flag into +44 bits0..7 and DynamicFlag into bits8..15. Later state writers
/// and the independently selected noBound argument remain current inputs.</summary>
public sealed record OriginalMapModelNativeFlagSource(byte Flag, byte DynamicFlag,
    string EvidenceUri, string EvidenceSha256, string Basis)
{
    public bool MergeChildren44Bit4 => (Flag & 0x10) != 0;
}

/// <summary>Publication facts for the supported fresh single-node prefix.
/// Eligibility and custom-culler results are resolved separately for each
/// current View. The full constructor's final positive slot writer is handled
/// by OriginalMapModelNativeCurrentVisibilitySources; later overrides stay explicit.</summary>
public sealed record OriginalMapModelNativeFreshVisibilityPublication(string CurrentSourceIdentity,
    bool? ParentBound, bool? ModelResourceBindingIsCurrent, bool? SourceDefinitionFlagsAreCurrent);

public sealed record OriginalMapModelNativeFreshVisibilityPrefix(OriginalMapModelNativeFlagSource DeclaredFlags,
    OriginalMapModelNativeFrustumCullingSource DeclaredType,
    OriginalMapModelNativeBoundsPublication BoundsPublication, string Basis)
{
    public uint ConstructorFlags44 => (uint)DeclaredFlags.Flag | ((uint)DeclaredFlags.DynamicFlag << 8);
    public uint ConstructorFrustumCullingType33C => uint.MaxValue;
    public bool ConstructorBit31 => false;
    public bool UseModelBounds => (DeclaredFlags.Flag & 4) == 0;
}

/// <summary>Original ModelWorldPQ -> per-View oriented-box visibility kernel.
/// This consumes the native bound writer's box and the shader's same camera.
/// Queue masks, animation visibility, child bounds and custom cullers remain
/// explicitly resolved inputs. No G1MG-header-to-AABB approximation is used.</summary>
public static class OriginalMapModelNativeVisibilitySources
{
    public const string NativeContractId = "ce-20261002-model-worldpq-view-oriented-visibility";
    public const uint FrustumViewCullingTypeHash = 0xA1A63083;
    private static ReadOnlySpan<int> PlaneOrder => [0, 1, 4, 2, 3, 5];

    /// <summary>Original C3AA51..C3AA7A: unsigned instance values >=4 inherit
    /// the explicitly selected current CommonViewSetting+64. The absent
    /// CommonView branch writes literal0; use ResolveWithAbsentCommonView only
    /// when absence is established, rather than when its state is unknown.</summary>
    public static uint ResolveFrustumCullingType(uint instanceType, uint currentCommonViewType)
        => instanceType < 4 ? instanceType : currentCommonViewType;

    public static uint ResolveWithAbsentCommonView(uint instanceType)
        => ResolveFrustumCullingType(instanceType, 0);

    /// <summary>SC C3A870. Returns the native cull/reject boolean. Additional
    /// results are the complete resolved calls from View+1C08 in native order;
    /// an empty list explicitly means no applicable custom culler. Modes1/2
    /// require the separate edge/face separating-axis path and are unsupported.
    /// Mode3 skips the frustum and still consumes every applicable custom gate.</summary>
    public static bool IsCulled(TerrainFrustumPlanes frustum,
        OriginalMapModelNativeOrientedBounds bound, uint resolvedFrustumCullingType,
        IReadOnlyList<bool> additionalCullingRejects)
    {
        ArgumentNullException.ThrowIfNull(additionalCullingRejects);
        ArgumentNullException.ThrowIfNull(frustum);
        OriginalMapModelNativeBoundsSources.Validate(bound);
        if (resolvedFrustumCullingType is not (0 or 3))
            throw Error("resolved FrustumViewCullingType requires the unsupported detailed box path");
        if (resolvedFrustumCullingType == 0 && !IntersectsOrientedBox(frustum, bound)) return true;
        foreach (bool rejects in additionalCullingRejects)
            if (rejects) return true;
        return false;
    }

    /// <summary>SC1092700 ->1093B90 ->106CD80/106CCE0 ->A11FD0.
    /// It chooses a support point in -normal for each original box axis, then
    /// performs the scalar plane reduction. Exact contact passes: dot<=0.
    /// There is no culler tolerance and no conversion to an enclosing AABB.</summary>
    public static bool IntersectsOrientedBox(TerrainFrustumPlanes frustum,
        OriginalMapModelNativeOrientedBounds bound)
    {
        ArgumentNullException.ThrowIfNull(frustum);
        OriginalMapModelNativeBoundsSources.Validate(bound);
        if (frustum.Planes is null || frustum.Planes.Count != 6)
            throw Error("the original View requires all six plane equations");
        foreach (int index in PlaneOrder)
        {
            Vector4 plane = frustum.Planes[index].Equation;
            if (!Finite(plane)) throw Error("nonfinite current View plane");
            Vector3 support = SupportPointCore(bound, new(-plane.X, -plane.Y, -plane.Z));
            float y = support.Y * plane.Y, x = support.X * plane.X, z = support.Z * plane.Z;
            float dot = ((y + x) + z) + plane.W;
            if (!float.IsFinite(dot)) throw Error("plane reduction exceeds finite float arithmetic");
            if (!(dot <= 0f)) return false;
        }
        return true;
    }

    /// <summary>Original A11FD0 support writer; direction does not need unit
    /// length. Zero direction dot chooses -extent, including negative zero.</summary>
    public static Vector3 SupportPoint(OriginalMapModelNativeOrientedBounds bound, Vector3 direction)
    {
        OriginalMapModelNativeBoundsSources.Validate(bound);
        if (!Finite(direction)) throw Error("nonfinite support direction");
        return SupportPointCore(bound, direction);
    }

    private static Vector3 SupportPointCore(OriginalMapModelNativeOrientedBounds bound, Vector3 direction)
    {
        float x = bound.Center0.X, y = bound.Center0.Y, z = bound.Center0.Z;
        AddAxis(bound.Basis10, bound.HalfExtents40.X);
        AddAxis(bound.Basis20, bound.HalfExtents40.Y);
        AddAxis(bound.Basis30, bound.HalfExtents40.Z);
        var result = new Vector3(x, y, z);
        if (!Finite(result)) throw Error("support writer exceeds finite float arithmetic");
        return result;

        void AddAxis(Vector4 axis, float extent)
        {
            float yTerm = axis.Y * direction.Y, xTerm = axis.X * direction.X, zTerm = axis.Z * direction.Z;
            float dot = (yTerm + xTerm) + zTerm;
            if (!float.IsFinite(dot)) throw Error("axis direction reduction exceeds finite float arithmetic");
            float signedExtent = extent * (dot <= 0f ? -1f : 1f);
            x = signedExtent * axis.X + x;
            y = signedExtent * axis.Y + y;
            z = signedExtent * axis.Z + z;
        }
    }

    public static OriginalMapModelNativeFrustumCullingSource ReadWorldPqDeclaration(LoadedObjects source, uint objectId)
        => ReadDeclaration(source, objectId, OriginalScenePlacements.ModelWorldPqClass,
            "WorldPQ declaration; native descriptor DB0ACF; current publication required");

    public static OriginalMapModelNativeFrustumCullingSource ReadCommonViewTemplate(LoadedObjects source, uint objectId)
        => ReadDeclaration(source, objectId, 0xEF3D849D,
            "explicitly selected CommonViewSetting template; loader D24045 ->+64; runtime frame copy and overrides remain explicit");

    public static OriginalMapModelNativeFlagSource ReadWorldPqSourceFlags(LoadedObjects source, uint objectId)
    {
        DokObject item = ReadObject(source, objectId, OriginalScenePlacements.ModelWorldPqClass);
        byte Byte(uint hash)
        {
            DokProperty[] values = item.Properties.Where(p => p.NameHash == hash).ToArray();
            // DA7CC8/DA7CCC initialise the two loader locals to native zero.
            if (values.Length == 0) return 0;
            if (values.Length != 1 || values[0].TypeCode != 1 || values[0].ElementCount != 1 || values[0].RawValue.Length != 1)
                throw Error("WorldPQ constructor flag requires one original type1 byte");
            return values[0].RawValue[0];
        }
        return new(Byte(0x05D92ACC), Byte(0xBAE0F275), source.Source.ResourceUri + $"#object/{objectId:X8}",
            source.Source.DecodedSha256, "WorldPQ declared constructor bytes; DA7C02 / DA8D3A / D9F5AC / D9F5B8; current setters remain explicit");
    }

    /// <summary>Reads the selected source definition already retained by the
    /// asset. It verifies the actual typed Model reference and its scene instance
    /// override bytes. Only declared Flag01/Dynamic00 and inherited-frustum
    /// FFFFFFFF are covered. Other source variants remain explicit unsupported
    /// profiles; Flag00 is not silently converted to a disabled instance.</summary>
    public static OriginalMapModelNativeFreshVisibilityPrefix PrepareFreshSingleNode(
        OriginalMapStaticModelInstance instance, OriginalMapModelNativeFreshVisibilityPublication publication)
    {
        ArgumentNullException.ThrowIfNull(instance); ArgumentNullException.ThrowIfNull(publication);
        if (string.IsNullOrWhiteSpace(publication.CurrentSourceIdentity) || publication.CurrentSourceIdentity != instance.CurrentSourceIdentity ||
            publication.ParentBound != false || publication.ModelResourceBindingIsCurrent != true || publication.SourceDefinitionFlagsAreCurrent != true)
            throw Error("fresh single-node prefix requires matching current identity, current source flags/binding and explicit unparented publication");
        var textures = instance.Asset.TextureResources;
        LoadedObjects source = textures.Catalog ?? throw Error("the selected original WorldPQ definition source is absent");
        DokObject declared = textures.CatalogDefinition ?? throw Error("the selected original WorldPQ definition identity is absent");
        DokObject canonical = ReadObject(source, declared.ObjectId, OriginalScenePlacements.ModelWorldPqClass);
        DokProperty[] targets = canonical.Properties.Where(p => p.NameHash == 0x0B6E1578).ToArray();
        if (targets.Length != 1 || targets[0].TypeCode != 5 || targets[0].ElementCount != 1 || targets[0].RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(targets[0].RawValue) != instance.Asset.Reference.ModelObject.ObjectId)
            throw Error("WorldPQ definition does not select the current typed Model asset");
        DokObject model = ReadObject(instance.Asset.Reference.Dictionary, instance.Asset.Reference.ModelObject.ObjectId, 0xD40B3C8F);
        DokProperty[] files = model.Properties.Where(p => p.NameHash == 0x8AB68B3F).ToArray();
        if (files.Length != 1 || files[0].TypeCode != 5 || files[0].ElementCount != 1 || files[0].RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(files[0].RawValue) != instance.Asset.Reference.ModelFileId ||
            instance.Asset.RawTransforms.Source.FileId != instance.Asset.Reference.ModelFileId ||
            textures.Model.Source.ResourceUri != instance.Asset.RawTransforms.Source.ResourceUri ||
            textures.Model.Source.DecodedSha256 != instance.Asset.RawTransforms.Source.DecodedSha256)
            throw Error("typed Model dictionary does not select the current canonical source asset");
        if (instance.SourcePlacement is { } placement)
        {
            LoadedObjects placementSource = placement.Source.InstanceDocument;
            if (placementSource.Source.DecodedSha256 != Convert.ToHexString(SHA256.HashData(placementSource.Source.Bytes)))
                throw Error("scene instance override bytes differ from their source identity");
            DokObject[] rows = DokReader.Read(placementSource.Source.Bytes).Objects.Where(o => o.ObjectId == placement.Source.Object.ObjectId).ToArray();
            if (rows.Length != 1 || rows[0].Kind != DokObjectKind.Reference || rows[0].Reference is not { } reference ||
                reference.ResourceId != source.Source.FileId || reference.ObjectId != canonical.ObjectId)
                throw Error("scene instance does not refer to the selected WorldPQ definition");
            if (rows[0].Properties.Any(p => p.NameHash is 0x05D92ACC or 0xBAE0F275 or FrustumViewCullingTypeHash))
                throw Error("scene instance overrides require their explicit current flag/culling publication");
        }
        var flags = ReadWorldPqSourceFlags(source, canonical.ObjectId);
        var type = ReadWorldPqDeclaration(source, canonical.ObjectId);
        if (flags.Flag != 1 || flags.DynamicFlag != 0 || type.Value != uint.MaxValue)
            throw Error("source definition requires a separate current flag/culling profile");
        return new(flags, type, new(publication.CurrentSourceIdentity, false, true, flags.MergeChildren44Bit4),
            NativeContractId + "; fresh source Flag01/Dynamic00; D9F540 flags44 and D9F8CD type33C; model-bound bit2 clear; child-union bit4 clear; per-view eligibility remains explicit");
    }

    private static OriginalMapModelNativeFrustumCullingSource ReadDeclaration(LoadedObjects source,
        uint objectId, uint classId, string basis)
    {
        DokObject item = ReadObject(source, objectId, classId);
        LoadedRdbAsset bytes = source.Source;
        DokProperty[] values = item.Properties.Where(p => p.NameHash == FrustumViewCullingTypeHash).ToArray();
        if (values.Length != 1 || values[0].TypeCode != 5 || values[0].ElementCount != 1 || values[0].RawValue.Length != 4)
            throw Error("FrustumViewCullingType requires one original type5 uint32");
        return new(BinaryPrimitives.ReadUInt32LittleEndian(values[0].RawValue),
            bytes.ResourceUri + $"#object/{objectId:X8}/property/{FrustumViewCullingTypeHash:X8}", bytes.DecodedSha256, basis);
    }

    private static DokObject ReadObject(LoadedObjects source, uint objectId, uint classId)
    {
        ArgumentNullException.ThrowIfNull(source);
        LoadedRdbAsset bytes = source.Source;
        if (string.IsNullOrWhiteSpace(bytes.ResourceUri) || bytes.DecodedSha256 != Convert.ToHexString(SHA256.HashData(bytes.Bytes)))
            throw Error("declaration bytes differ from their source identity");
        DokObject[] objects = DokReader.Read(bytes.Bytes).Objects.Where(o => o.ObjectId == objectId).ToArray();
        if (objects.Length != 1 || objects[0].Kind != DokObjectKind.Inline || objects[0].ClassId != classId)
            throw Error("declaration requires the exact original typed object");
        return objects[0];
    }

    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool Finite(Vector4 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
    private static InvalidDataException Error(string message) => new("Native map Model visibility: " + message + ".");
}
