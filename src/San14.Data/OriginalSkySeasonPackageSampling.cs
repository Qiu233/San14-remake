using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact declared sampling references from a Camera in one of the four
/// original seasonal databases. Package selection/application and persistent
/// shader descriptor lifetime are separate caller contracts.</summary>
public sealed record OriginalSkySeasonPackageSampling(OriginalMapSeasonCameraPackage Package,
    OriginalSkySamplingReference Earth, OriginalSkySamplingReference Moon,
    OriginalSkySamplingReference MilkyWay, OriginalSkySamplingReference Star)
{
    public string SourceEvidence => Package.SourceEvidence + ";" + OriginalSkySeasonPackageTextures.NativeEvidence;
    public string NullAssignmentEvidence => OriginalSkySeasonPackageTextures.ExplainNullAssignment(this);
}

/// <summary>Typed, on-demand original seasonal Earth declaration -> common
/// Static -> original G1T. Does not apply the native package or claim that a
/// source zero cleared a previously retained GPU descriptor.</summary>
public static class OriginalSkySeasonPackageTextures
{
    // A loaded seasonal Camera owns fixed sampling declarations. The current
    // selection and shader descriptor lifetime are checked by their callers.
    private static readonly ConditionalWeakTable<OriginalMapSeasonCameraPackage,
        OriginalSkySeasonPackageSampling> LoadedSampling = new();
    public const string DefinitionArchive = "KIDSSystemResource";
    public const uint DefinitionFileId = 0xD2ECCCDF;
    public const string DefinitionSha256 = "BEC57B5340D4FE8B4762DDA0B857BFC7717F971391E7B271EFA0728386EF99EE";
    public const uint EarthObjectId = 0x622DC967;
    public const string TextureArchive = "RRPreview";
    public const uint TextureFileId = 0x8338DB8A;
    public const string TextureSha256 = "8230C6E601CC472A751B93734BA0EDEAF09533FDDF7D648BB7824AF00B981405";
    public const uint OrdinaryNativeViewFormat = 87;
    public const string SourceSamplerKeyHex = "1014300000000000";
    public const string NativeEvidence = "docs/.local/ce-sky-applied-sampling-20261002/contract.json";

    private static readonly IReadOnlyDictionary<uint, string> PackageSources = new Dictionary<uint, string>
    {
        [0x402B9DB3] = "8BFDAB817AF4ECFC0A48E4AF494E894E4F3A8573FD730180B265ED39578878F0",
        [0xCE74F252] = "9007D8C8B96B00253F1653A19A682FDBF0890D6B0E442127F608F77ACCBA055B",
        [0x938A57CC] = "DEEFDAD3E40097584160D6DD20B1CF5B4930F318241E349B53C15FEBB7B9CE46",
        [0x30B94EBE] = "BCA9194FACE7BDF88EEFD4CAE4A3DBE6044ED0E8F4B185FC9B4E99CB42F06A5D"
    };
    private static readonly uint[] CameraIds = [0xE17F5C21, 0x28356E95, 0x2D8F4132, 0x7F535F74, 0xD6715053];

    public static OriginalSkySeasonPackageSampling Read(OriginalMapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var sampling = LoadedSampling.GetValue(package, ReadAtImport);
        // Null proofs retain the canonical package returned by the importer.
        // Register that same loaded value, so validation does not import it again.
        if (!ReferenceEquals(package, sampling.Package))
            LoadedSampling.GetValue(sampling.Package, _ => sampling);
        return sampling;
    }

    private static OriginalSkySeasonPackageSampling ReadAtImport(OriginalMapSeasonCameraPackage package)
    {
        uint fileId = package.Objects.Source.FileId;
        if (!PackageSources.TryGetValue(fileId, out string? sha) || !CameraIds.Contains(package.Camera.ObjectId))
            throw new InvalidDataException("Season sampling requires a declared Camera from one of the four exact original seasonal sources.");
        ValidateSource(package.Objects.Source, DefinitionArchive, fileId, sha);
        var database = OriginalMapSeasonResourceConsumer.ReadActionDatabase(package.Action, package.Objects);
        var canonical = OriginalMapSeasonResourceConsumer.ReadCameraPackage(database, package.Camera.ObjectId);
        if (canonical.Camera.Properties.Count != 1119 || canonical.Objects.Document.Objects.Count != 141)
            throw new InvalidDataException("Season sampling requires the complete original Camera/package source profile.");
        var refs = Enum.GetValues<OriginalSkySamplingRole>().Select(role =>
            OriginalSkySamplingResources.ReadCameraReference(canonical.Objects, canonical.Camera.ObjectId, role)).ToArray();
        if (refs[0].SourceObjectId != EarthObjectId || refs.Skip(1).Any(r => !r.IsSourceReferenceZero))
            throw new InvalidDataException("Season source sampling differs from Earth622DC967 and its three declared zero references.");
        return new(canonical, refs[0], refs[1], refs[2], refs[3]);
    }

    public static LoadedOriginalSkySamplingResource LoadEarth(OriginalGameRepository repository,
        OriginalMapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var sampling = Read(package);
        var definitions = repository.LoadObjects(DefinitionArchive, DefinitionFileId);
        ValidateSource(definitions.Source, DefinitionArchive, DefinitionFileId, DefinitionSha256);
        // LoadStatic reparses the complete Camera/Static bytes and preserves the
        // object key, ContextHash, TID and both original provenance layers.
        var earth = OriginalSkySamplingResources.LoadStatic(repository, sampling.Earth,
            definitions, TextureArchive, OrdinaryNativeViewFormat);
        ValidateEarth(package, earth);
        return earth;
    }

    public static void ValidateEarth(OriginalMapSeasonCameraPackage package,
        LoadedOriginalSkySamplingResource earth)
    {
        ArgumentNullException.ThrowIfNull(earth);
        var sampling = Read(package);
        if (earth.Role != OriginalSkySamplingRole.Earth || earth.NativeViewFormat != OrdinaryNativeViewFormat ||
            earth.ImageIndex != 0 || earth.CameraReference is not { } camera ||
            camera.SourceEvidence != sampling.Earth.SourceEvidence || camera.SourceObjectId != EarthObjectId ||
            earth.StaticBinding is not { } binding || binding.Definition.ObjectId != EarthObjectId ||
            !ReferenceEquals(binding.Resource, earth.Resource) || binding.ImageIndex != 0)
            throw new InvalidDataException("Season Earth lost its exact selected package/Static/TID binding.");
        ValidateSource(binding.Definitions.Source, DefinitionArchive, DefinitionFileId, DefinitionSha256);
        var common = DokReader.Read(binding.Definitions.Source.Bytes);
        var declarations = common.Objects.Where(o => o.ObjectId == EarthObjectId).ToArray();
        if (declarations.Length != 1 || declarations[0].Kind != DokObjectKind.Inline ||
            declarations[0].ClassId != 0xFF7DBFD4 || declarations[0].Properties.Count != 2 ||
            Scalar(declarations[0], 0x6C7321D2, 5) != TextureFileId ||
            Scalar(declarations[0], 0x00200571, 4) != 0)
            throw new InvalidDataException("Season Earth requires the canonical common Static ContextHash/TID source.");
        var declaration = declarations[0];
        if (binding.Definition.Kind != declaration.Kind || binding.Definition.ClassId != declaration.ClassId ||
            binding.Definition.Offset != declaration.Offset || binding.Definition.Length != declaration.Length ||
            !binding.Definition.RawHeader.AsSpan().SequenceEqual(declaration.RawHeader) ||
            binding.ContextReference.NameHash != 0x6C7321D2 || binding.ContextReference.TypeCode != 5 ||
            !binding.ContextReference.RawValue.AsSpan().SequenceEqual(declaration.Properties[0].RawValue) ||
            binding.ImageReference.NameHash != 0x00200571 || binding.ImageReference.TypeCode != 4 ||
            !binding.ImageReference.RawValue.AsSpan().SequenceEqual(declaration.Properties[1].RawValue))
            throw new InvalidDataException("Season Earth retained Static metadata differs from its complete original source.");
        ValidateSource(earth.Resource.Source, TextureArchive, TextureFileId, TextureSha256);
        var container = G1TextureContainerReader.Read(earth.Resource.Source.Bytes);
        var image = container.Textures.Single();
        if (container.RawVersion != "0500" || container.Platform != 0 || image.Index != 0 ||
            image.PixelFormat != 1 || image.TextureSystem != 0 || image.LayerCount != 1 || image.Depth != 1 ||
            image.Width != 128 || image.Height != 64 || image.MipCount != 1 ||
            image.EncodedDataRange is not { Offset: 44, Length: 32768 } ||
            Convert.ToHexString(image.RawHeader) != "1001670000102100")
            throw new InvalidDataException("Season Earth differs from the exact original planar0500 image profile.");
        var retained = earth.Resource.Container.Textures[earth.ImageIndex];
        if (earth.Resource.Container.RawVersion != container.RawVersion || earth.Resource.Container.Platform != container.Platform ||
            earth.Resource.Container.Textures.Count != 1 || retained.Index != image.Index ||
            retained.PixelFormat != image.PixelFormat || retained.Width != image.Width || retained.Height != image.Height ||
            retained.Depth != image.Depth || retained.TextureSystem != image.TextureSystem || retained.LayerCount != image.LayerCount ||
            retained.MipCount != image.MipCount || retained.RawMipCount != image.RawMipCount ||
            retained.RawContainerVersion != image.RawContainerVersion || retained.SubresourceLayout != image.SubresourceLayout ||
            retained.ImageRange != image.ImageRange || retained.EncodedDataRange != image.EncodedDataRange ||
            !retained.RawHeader.AsSpan().SequenceEqual(image.RawHeader) || !retained.Mips.SequenceEqual(image.Mips))
            throw new InvalidDataException("Season Earth retained image metadata differs from the exact original file.");
        var recipe = G1TextureSamplerReader.ReadRetainedSource(image);
        if (Convert.ToHexString(recipe.RawKey) != SourceSamplerKeyHex ||
            !earth.GetSamplerDescriptor().AsSpan().SequenceEqual(recipe.EncodeD3D11Descriptor()))
            throw new InvalidDataException("Season Earth must retain the complete original source sampler descriptor.");
    }

    /// <summary>Source pointer assignment clears the embedded Camera fields.
    /// Null wrapper branches skip the persistent Sky2/Star setters; a prior
    /// shader descriptor therefore requires its own proven lifetime.</summary>
    public static string ExplainNullAssignment(OriginalSkySeasonPackageSampling sampling)
    {
        ArgumentNullException.ThrowIfNull(sampling);
        var canonical = Read(sampling.Package);
        if (!SameReference(sampling.Earth, canonical.Earth) || !SameReference(sampling.Moon, canonical.Moon) ||
            !SameReference(sampling.MilkyWay, canonical.MilkyWay) || !SameReference(sampling.Star, canonical.Star))
            throw new InvalidDataException("Season null assignment requires its canonical package source references.");
        return canonical.SourceEvidence + ";scope=SelectedSourcePackageCameraPointerAssignment;" +
            "CAAFA4..CAAFCE:source2D48/2D50/2D58-unconditionally-overwrites-embedded29C8/29D0/29D8;" +
            "D3AD88..D3ADD0:null-Moon/Milky-wrappers-skip-persistent-Sky2-setters;" +
            "D39091..D390B5:null-Star-wrapper-skips-persistent-Star-setter;" +
            "GPU-null-requires-fresh-or-separately-proved-null-descriptor-lifetime";
    }

    private static bool SameReference(OriginalSkySamplingReference left, OriginalSkySamplingReference right) =>
        left.Role == right.Role && left.SourceObjectId == right.SourceObjectId &&
        left.Property.NameHash == right.Property.NameHash && left.SourceEvidence == right.SourceEvidence;

    private static void ValidateSource(LoadedRdbAsset source, string archive, uint fileId, string sha)
    {
        if (source.FileId != fileId || source.ResourceUri != GameResourceId.Rdb(archive, fileId).ToString() ||
            source.OverrideSource is not null || source.DecodedSha256 != sha ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != sha)
            throw new InvalidDataException("Season sampling requires its exact complete original source URI/hash and no override.");
    }
    private static uint Scalar(DokObject owner, uint hash, ushort type)
    {
        var fields = owner.Properties.Where(p => p.NameHash == hash).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != 1 ||
            fields[0].ElementSize != 4 || fields[0].RawValue is not { Length: 4 })
            throw new InvalidDataException("Season Earth Static property differs from its typed scalar source.");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }
}
