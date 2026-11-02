using System.Text;
using San14.Data;

/// <summary>Fixed environment program banks and declarations owned by the
/// application's static resource generation. No accepted Camera, season or
/// World is captured here; opening scenes borrow this published directory.</summary>
internal sealed class MapEnvironmentStaticResources
{
    internal const string OpeningReferenceFile = "environment-static-root.txt";
    private static readonly UTF8Encoding ManifestEncoding = new(false);

    private MapEnvironmentStaticResources(string directory,
        ShadowReceiverResources receivers, ContactOcclusionResources occlusion,
        D3D11CloudPatternProgramMedia cloudPatterns, D3D11PatternRenderResources patternTargets,
        D3D11SdfInputPreparation sdfConstruction)
    {
        DirectoryPath = directory;
        Receivers = receivers;
        Occlusion = occlusion;
        CloudPatterns = cloudPatterns;
        PatternTargets = patternTargets;
        SdfConstruction = sdfConstruction;
    }

    internal string DirectoryPath { get; }
    internal ShadowReceiverResources Receivers { get; }
    internal ContactOcclusionResources Occlusion { get; }
    internal D3D11CloudPatternProgramMedia CloudPatterns { get; }
    internal D3D11PatternRenderResources PatternTargets { get; }
    internal D3D11SdfInputPreparation SdfConstruction { get; }

    // The caller supplies a distinct directory for the resource generation and
    // publishes this owner only after all writes and generation checks succeed.
    internal static MapEnvironmentStaticResources Load(GameResourceRepository repository,
        NamedViewRendererResources renderer, string locale, string generationDirectory,
        CancellationToken token)
    {
        string directory = Path.GetFullPath(generationDirectory);
        token.ThrowIfCancellationRequested();
        var receivers = ShadowReceiverResources.Load(repository);
        receivers.WriteNativeInputs(directory);
        ShadowReceiverSamplerEncoding.WriteNativeInputs(repository, locale, directory);
        token.ThrowIfCancellationRequested();
        var occlusion = ContactOcclusionResources.Load(repository, locale);
        occlusion.WriteNativeInputs(directory);
        token.ThrowIfCancellationRequested();
        var cloudPatterns = D3D11CloudPatternProgramMedia.Prepare(
            SkyCloudProgramResources.Load(repository, renderer));
        cloudPatterns.WriteNativeInputs(directory);
        token.ThrowIfCancellationRequested();
        var ambient = SkyAmbientResources.Load(repository,
            repository.LoadObjects("system", 0xDAB638B2), 0x910603AC,
            repository.LoadObjects("system", 0x718CCB8B),
            repository.LoadObjects("system", 0x93FB3280));
        var patternTargets = D3D11PatternRenderResources.Load(repository, ambient, locale);
        patternTargets.WriteNativeInputs(directory);
        token.ThrowIfCancellationRequested();
        // The normal Main construction recipe and its vertex program depend
        // only on resource declarations and the locale's fresh Host defaults.
        // Actual SDF population, counts, targets and GPU lifetime stay per scene.
        var sdfConstruction = D3D11SdfInputPreparation.PrepareFreshNormalMain(repository,
            MainMapContextOwnerSource.LoadFreshHostDefaults(locale),
            DefaultView0Source.LoadDeclarationDefaults(repository),
            repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId));
        sdfConstruction.WriteNativeInputs(Path.Combine(directory, "sdf-source-input"));
        token.ThrowIfCancellationRequested();
        return new(directory, receivers, occlusion, cloudPatterns, patternTargets, sdfConstruction);
    }

    internal void WriteOpeningReference(string inputRoot)
    {
        Directory.CreateDirectory(inputRoot);
        File.WriteAllText(Path.Combine(inputRoot, OpeningReferenceFile),
            DirectoryPath + "\n", ManifestEncoding);
    }
}
