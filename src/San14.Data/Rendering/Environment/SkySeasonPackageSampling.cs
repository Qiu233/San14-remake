using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Immutable sampling declarations imported for one selected Camera.
/// Shader descriptor state and package application have separate owners.</summary>
public sealed class SkySeasonPackageSampling
{
    public SkySamplingReference Earth { get; }
    public SkySamplingReference Moon { get; }
    public SkySamplingReference MilkyWay { get; }
    public SkySamplingReference Star { get; }
    public string SourceEvidence { get; }

    internal SkySeasonPackageSampling(MapSeasonCameraPackage package)
        : this(package.Objects, package.Camera, package.SourceEvidence) { }

    internal SkySeasonPackageSampling(LoadedObjects definitions, DokObject camera, string sourceEvidence)
    {
        Earth = Read(SkySamplingRole.Earth);
        Moon = Read(SkySamplingRole.Moon);
        MilkyWay = Read(SkySamplingRole.MilkyWay);
        Star = Read(SkySamplingRole.Star);
        SourceEvidence = sourceEvidence;

        SkySamplingReference Read(SkySamplingRole role) =>
            SkySamplingResources.ReadCameraReference(definitions, camera, role);
    }
}

public static class SkySeasonPackageTextures
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, SkySeasonPackageSampling> LoadedSampling = new();
    private static readonly ConditionalWeakTable<GameResourceRepository,
        ConditionalWeakTable<SkySamplingReference, LoadedSkySamplingResource>> SamplingTextures = new();
    public const string DefinitionArchive = "KIDSSystemResource";
    public const uint DefinitionFileId = 0xD2ECCCDF;
    public const string TextureArchive = "RRPreview";
    public const uint OrdinaryNativeViewFormat = 87;
    public const string NativeEvidence = "docs/.local/ce-sky-applied-sampling-20261002/contract.json";

    public static SkySeasonPackageSampling Read(MapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return LoadedSampling.GetValue(package, value => new(value));
    }

    public static LoadedSkySamplingResource LoadEarth(GameResourceRepository repository,
        MapSeasonCameraPackage package) => LoadSampling(repository, Read(package).Earth);

    public static LoadedSkySamplingResource LoadSampling(GameResourceRepository repository,
        SkySamplingReference reference)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(reference);
        var textures = SamplingTextures.GetValue(repository, _ => new());
        return textures.GetValue(reference, value =>
            SkySamplingResources.LoadStatic(repository, value,
                repository.LoadObjects(DefinitionArchive, DefinitionFileId),
                TextureArchive, OrdinaryNativeViewFormat));
    }
}
