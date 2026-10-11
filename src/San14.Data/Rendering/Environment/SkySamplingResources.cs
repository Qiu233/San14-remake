using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public enum SkySamplingRole { Earth, Moon, MilkyWay, Star }

/// <summary>An imported Camera reference. Its object key is independent of an
/// archive file ID and does not describe a current shader binding.</summary>
public sealed class SkySamplingReference
{
    public SkySamplingRole Role { get; }
    public uint SourceObjectId { get; }
    public uint CameraObjectId { get; }
    public uint PropertyNameHash { get; }
    public string SourceResourceUri { get; }
    public string SourceSha256 { get; }
    public bool IsSourceReferenceZero => SourceObjectId == 0;
    public string SourceEvidence => $"{SourceResourceUri}@{SourceSha256}" +
        $"#object={CameraObjectId:X8}/property={PropertyNameHash:X8}";

    internal SkySamplingReference(SkySamplingRole role, uint objectId,
        uint cameraId, uint propertyHash, string resourceUri, string sourceSha256)
        => (Role, SourceObjectId, CameraObjectId, PropertyNameHash, SourceResourceUri, SourceSha256) =
            (role, objectId, cameraId, propertyHash, resourceUri, sourceSha256);
}

/// <summary>A resolved sampling reference and its independently owned texture.</summary>
public sealed class LoadedSkySamplingResource
{
    public SkySamplingRole Role => CameraReference.Role;
    public SkySamplingReference CameraReference { get; }
    public SkyTextureSource Texture { get; }
    public string SourceName => SkySamplingResources.SourceName(Role);
    public uint PixelRegister => SkySamplingResources.PixelRegister(Role);
    public byte[] GetSamplerDescriptor() => Texture.GetSamplerDescriptor();

    internal LoadedSkySamplingResource(SkySamplingReference reference, SkyTextureSource texture)
        => (CameraReference, Texture) = (reference, texture);
}

public static class SkySamplingResources
{
    public const uint CameraClassId = 0x23E7C64E;
    public static SkySeasonPackageSampling ReadCameraSampling(LoadedObjects definitions, uint cameraObjectId)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var cameras = definitions.Document.Objects.Where(item => item.ObjectId == cameraObjectId).ToArray();
        if (cameras.Length != 1)
            throw new InvalidDataException("Sky sampling requires one selected Camera declaration.");
        return new(definitions, cameras[0], definitions.Source.ResourceUri + $"#object={cameraObjectId:X8}");
    }
    public static string SourceName(SkySamplingRole role) => role switch
    {
        SkySamplingRole.Earth => "Sky2EarthSurfaceMap",
        SkySamplingRole.Moon => "Sky2MoonSurfaceMap",
        SkySamplingRole.MilkyWay => "Sky2MilkyWayMap",
        SkySamplingRole.Star => "Sky2StarMap",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static uint PixelRegister(SkySamplingRole role) => role switch
    {
        SkySamplingRole.Earth => 3,
        SkySamplingRole.Moon => 5,
        SkySamplingRole.MilkyWay => 4,
        SkySamplingRole.Star => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static uint NameHash(SkySamplingRole role) => role switch
    {
        SkySamplingRole.Earth => 0xB0EE87B8,
        SkySamplingRole.Moon => 0xF83B0CC1,
        SkySamplingRole.MilkyWay => 0x66BCE906,
        SkySamplingRole.Star => 0x2E30D2C7,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static SkySamplingReference ReadCameraReference(LoadedObjects definitions,
        uint explicitCameraObjectId, SkySamplingRole role)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var matches = definitions.Document.Objects.Where(o => o.ObjectId == explicitCameraObjectId).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException("Sky sampling requires one explicit Camera object.");
        return ReadCameraReference(definitions, matches[0], role);
    }

    internal static SkySamplingReference ReadCameraReference(LoadedObjects definitions,
        DokObject camera, SkySamplingRole role)
    {
        if (camera.ObjectId == 0 || camera.Kind != DokObjectKind.Inline || camera.ClassId != CameraClassId)
            throw new InvalidDataException("Sky sampling requires an inline GameCamera declaration.");
        uint hash = NameHash(role);
        var fields = camera.Properties.Where(p => p.NameHash == hash).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 ||
            fields[0].ElementSize != 4 || fields[0].RawValue.Length != 4)
            throw new InvalidDataException($"{SourceName(role)} requires one type5 scalar object key.");
        return new(role, BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue),
            camera.ObjectId, hash, definitions.Source.ResourceUri, definitions.Source.DecodedSha256);
    }

    /// <summary>Resolve the declared Static object through its context file and
    /// image index. Import texture data once; subsequent users borrow the owner.</summary>
    public static LoadedSkySamplingResource LoadStatic(GameResourceRepository repository,
        SkySamplingReference reference, LoadedObjects staticDefinitions,
        string explicitArchive, uint explicitNativeViewFormat)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(staticDefinitions);
        if (reference.IsSourceReferenceZero)
            throw new InvalidDataException($"{SourceName(reference.Role)} has no declared Static texture.");
        var binding = StaticTextureResources.Load(repository, staticDefinitions, explicitArchive,
            [reference.SourceObjectId]).Single();
        var texture = SkyTextureSource.Import(binding.Resource, binding.ImageIndex, explicitNativeViewFormat);
        if (texture.SourceDimension != 4)
            throw new InvalidDataException("Sky sampling requires a two-dimensional texture.");
        return new(reference, texture);
    }
}
