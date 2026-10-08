using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalSkySamplingRole { Earth, Moon, MilkyWay, Star }

/// <summary>A source Camera key, distinct from the applied provider package and
/// from an archive file ID. Zero does not establish current draw absence.</summary>
public sealed record OriginalSkySamplingReference(LoadedObjects Definitions,
    DokObject Camera, DokProperty Property, OriginalSkySamplingRole Role, uint SourceObjectId)
{
    public bool IsSourceReferenceZero => SourceObjectId == 0;
    public string SourceEvidence => $"{Definitions.Source.ResourceUri}@{Definitions.Source.DecodedSha256}" +
        $"#object={Camera.ObjectId:X8}/property={Property.NameHash:X8}";
}

public sealed record LoadedOriginalSkySamplingResource(OriginalSkySamplingRole Role,
    LoadedTextureContainer Resource, int ImageIndex, uint NativeViewFormat,
    string SelectionEvidence, OriginalSkySamplingReference? CameraReference,
    OriginalStaticTextureBinding? StaticBinding)
{
    public string SourceName => OriginalSkySamplingResources.SourceName(Role);
    public uint PixelRegister => OriginalSkySamplingResources.PixelRegister(Role);
    public byte[] GetSamplerDescriptor()
    {
        OriginalSkySamplingResources.ValidateSelection(this);
        return OriginalSkySamplingResources.ReadSamplerDescriptor(Resource, ImageIndex, NativeViewFormat);
    }
}

/// <summary>Resolves only an explicit original Camera reference or the separately
/// proved registered Earth source. No texture name, size, controller geometry,
/// zero reference or constructor default selects a sampling resource.</summary>
public static class OriginalSkySamplingResources
{
    public const uint CameraClassId = 0x23E7C64E;
    public const string RegisteredEarthSha256 = "8230C6E601CC472A751B93734BA0EDEAF09533FDDF7D648BB7824AF00B981405";
    public const string FreshNullConstructionContractSha256 = "6ED3125C3E8786A13DCC33AAE1657C352051F4FF84C2B975941B52403079525D";
    public const string FreshNullConstructionEvidence =
        "C4F6F0->C3BF60:null-source2D40/2D48/2D50/2D58;" +
        "C3EFD9:self->CAAEE0:View1690/1698/16A0/16A8->packet90/98/A0/A8;" +
        "CPU.V418=C40870:own-view-preparation;" +
        "exclude-CPU.V468=CB3210:external-E17F5C21-package-application";

    /// <summary>Describes the audited native construction path for a canonical
    /// zero reference. The caller must separately enforce fresh construction
    /// with no external package application; this method establishes no current
    /// Camera lifetime/state from a DOK value. It allocates no texture or sampler.</summary>
    public static string ExplainFreshNull(OriginalSkySamplingReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var canonical = ReadCameraReference(reference.Definitions, reference.Camera.ObjectId, reference.Role);
        if (!canonical.IsSourceReferenceZero || canonical.SourceObjectId != reference.SourceObjectId ||
            canonical.SourceEvidence != reference.SourceEvidence)
            throw new InvalidDataException("Fresh null construction evidence requires the complete canonical zero Camera reference.");
        return canonical.SourceEvidence + ";scope=FreshCameraCreatedNoExternalPackage;" +
            FreshNullConstructionEvidence + ";native-construction-contract-sha256=" + FreshNullConstructionContractSha256;
    }

    public static string SourceName(OriginalSkySamplingRole role) => role switch
    {
        OriginalSkySamplingRole.Earth => "Sky2EarthSurfaceMap",
        OriginalSkySamplingRole.Moon => "Sky2MoonSurfaceMap",
        OriginalSkySamplingRole.MilkyWay => "Sky2MilkyWayMap",
        OriginalSkySamplingRole.Star => "Sky2StarMap",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static uint PixelRegister(OriginalSkySamplingRole role) => role switch
    {
        OriginalSkySamplingRole.Earth => 3,
        OriginalSkySamplingRole.Moon => 5,
        OriginalSkySamplingRole.MilkyWay => 4,
        OriginalSkySamplingRole.Star => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static uint NameHash(OriginalSkySamplingRole role) => role switch
    {
        OriginalSkySamplingRole.Earth => 0xB0EE87B8,
        OriginalSkySamplingRole.Moon => 0xF83B0CC1,
        OriginalSkySamplingRole.MilkyWay => 0x66BCE906,
        OriginalSkySamplingRole.Star => 0x2E30D2C7,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static OriginalSkySamplingReference ReadCameraReference(LoadedObjects definitions,
        uint explicitCameraObjectId, OriginalSkySamplingRole role)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ValidateSource(definitions.Source);
        // Reparse full bytes so stale/mutable caller metadata cannot choose an object.
        var canonical = DokReader.Read(definitions.Source.Bytes);
        var matches = canonical.Objects.Where(o => o.ObjectId == explicitCameraObjectId).ToArray();
        if (explicitCameraObjectId == 0 || matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline ||
            matches[0].ClassId != CameraClassId)
            throw new InvalidDataException("Sky sampling needs one explicit original GameCamera source object.");
        var camera = matches[0];
        var fields = camera.Properties.Where(p => p.NameHash == NameHash(role)).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 ||
            fields[0].ElementSize != 4 || fields[0].RawValue.Length != 4)
            throw new InvalidDataException($"{SourceName(role)} requires one complete type5/count1 source key.");
        return new(new(canonical, definitions.Source), camera, fields[0], role,
            BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue));
    }

    /// <summary>The supplied nonzero Camera key names a Static object. Its
    /// ContextHash names the file in the explicitly supplied archive; TID names
    /// its image. The requested native view stays explicit.</summary>
    public static LoadedOriginalSkySamplingResource LoadStatic(OriginalGameRepository repository,
        OriginalSkySamplingReference reference, LoadedObjects staticDefinitions,
        string explicitArchive, uint explicitNativeViewFormat)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(staticDefinitions);
        var canonical = ReadCameraReference(reference.Definitions, reference.Camera.ObjectId, reference.Role);
        if (canonical.SourceObjectId != reference.SourceObjectId)
            throw new InvalidDataException("Sky sampling key differs from the complete Camera source bytes.");
        if (canonical.SourceObjectId == 0)
            throw new InvalidDataException($"{SourceName(reference.Role)} source reference is zero. Resolve the exact applied " +
                "E17F5C21 provider root/child/interpolated package (C3FEA0 -> CAAEE0) before loading its sampling resource.");
        ValidateSource(staticDefinitions.Source);
        var canonicalDefinitions = new LoadedObjects(DokReader.Read(staticDefinitions.Source.Bytes), staticDefinitions.Source);
        var binding = OriginalStaticTextureResources.Load(repository, canonicalDefinitions, explicitArchive,
            [canonical.SourceObjectId]).Single();
        ValidateImage(binding.Resource, binding.ImageIndex, explicitNativeViewFormat);
        return new(reference.Role, binding.Resource, binding.ImageIndex, explicitNativeViewFormat,
            $"{canonical.Definitions.Source.ResourceUri}#object={canonical.Camera.ObjectId:X8}/property={NameHash(reference.Role):X8}" +
            $" -> {canonicalDefinitions.Source.ResourceUri}#object={binding.Definition.ObjectId:X8}", canonical, binding);
    }

    /// <summary>Exact previously established Static own dependency rank/key
    /// proof. This does not populate or rewrite the zero Camera DOK reference.</summary>
    public static LoadedOriginalSkySamplingResource LoadRegisteredEarth(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var resource = repository.LoadTextureContainer("RRPreview", 0x8338DB8A);
        ValidateImage(resource, 0, 87);
        if (resource.Source.DecodedSha256 != RegisteredEarthSha256 || resource.Container.Textures.Count != 1)
            throw new InvalidDataException("Registered Earth differs from the complete proved RRPreview/8338DB8A source.");
        var recipe = G1TextureSamplerReader.ReadRetainedSource(resource.Container.Textures[0]);
        if (!recipe.RawKey.AsSpan().SequenceEqual(Convert.FromHexString("1014300000000000")))
            throw new InvalidDataException("Registered Earth sampler differs from the proved Static descriptor.");
        return new(OriginalSkySamplingRole.Earth, resource, 0, 87,
            "docs/.local/ce-sky2-earth-context-origin-20261002/old-g1t-contract.json: own Static dependency rank29597 -> registered key8338DB8A",
            null, null);
    }

    private static void ValidateImage(LoadedTextureContainer resource, int imageIndex, uint nativeView)
    {
        ValidateSource(resource.Source);
        var canonical = G1TextureContainerReader.Read(resource.Source.Bytes);
        if ((uint)imageIndex >= canonical.Textures.Count)
            throw new InvalidDataException("Sky sampling TID is outside the complete source container.");
        var image = canonical.Textures[imageIndex];
        if (image.TextureSystem != 0 || image.LayerCount != 1 ||
            image.SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain ||
            (image.PixelFormat, nativeView) is not ((0x01, 87) or (0x01, 91) or (0x03, 10) or
                (0x59, 71) or (0x59, 72) or (0x5F, 98) or (0x5F, 99)))
            throw new InvalidDataException("Sky sampling requires a verified Texture2D image and exact matching native view.");
        _ = G1TextureSamplerReader.ReadRetainedSource(image);
    }

    internal static byte[] ReadSamplerDescriptor(LoadedTextureContainer resource, int imageIndex, uint nativeView)
    {
        ValidateImage(resource, imageIndex, nativeView);
        return G1TextureSamplerReader.ReadRetainedSource(G1TextureContainerReader.Read(resource.Source.Bytes)
            .Textures[imageIndex]).EncodeD3D11Descriptor();
    }

    internal static void ValidateSelection(LoadedOriginalSkySamplingResource selection)
    {
        if (selection.CameraReference is { } reference)
        {
            var canonical = ReadCameraReference(reference.Definitions, reference.Camera.ObjectId, selection.Role);
            if (reference.Role != selection.Role || canonical.SourceObjectId == 0 ||
                canonical.SourceObjectId != reference.SourceObjectId || selection.StaticBinding is not { } binding ||
                binding.Definition.ObjectId != canonical.SourceObjectId ||
                !ReferenceEquals(binding.Resource, selection.Resource) || binding.ImageIndex != selection.ImageIndex)
                throw new InvalidDataException("Sky sampling selection differs from its explicit Camera/Static binding.");
            ValidateSource(binding.Definitions.Source);
        }
        else if (selection.Role != OriginalSkySamplingRole.Earth || selection.StaticBinding is not null ||
            selection.ImageIndex != 0 || selection.NativeViewFormat != 87 ||
            selection.Resource.Source.ResourceUri != "san14://rdb/RRPreview/8338DB8A" ||
            selection.Resource.Source.DecodedSha256 != RegisteredEarthSha256)
            throw new InvalidDataException("Only the exact registered Earth proof can supply a selection without a Camera source key.");
    }

    private static void ValidateSource(LoadedRdbAsset source)
    {
        if (string.IsNullOrWhiteSpace(source.ResourceUri) ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != source.DecodedSha256)
            throw new InvalidDataException("Sky sampling source differs from its retained complete-file hash.");
    }
}
