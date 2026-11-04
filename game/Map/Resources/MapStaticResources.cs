using San14.Data;

/// <summary>One projected scene and locale from the application's loaded
/// static generation. Opening workers borrow its source content through an
/// exclusive repository scope; no camera, World or renderer enters this owner.</summary>
internal sealed record MapStaticSceneResources(GameResourceRepository Root, long Revision,
    MapTerrainSourcePreparation Terrain, D3D11SceneMediaSources Media,
    D3D11TerrainPreparedPrograms TerrainPrograms, D3D11CityGateStaticResources PrimaryModels,
    MapEnvironmentStaticResources EnvironmentStatic, D3D11PrimaryGpuMediaSource PrimaryGpuMedia,
    MapRouteStaticResources RouteMedia, MapDomesticEffectStaticResources DomesticEffects,
    HdrShaderResources Hdr)
{
    internal void RequireCurrentRevision()
    {
        if (Root.ResourceRevision != Revision)
            throw new ResourceReadGenerationChangedException(GameResourceId.Parse(Terrain.Scene.Scene.Source.ResourceUri));
    }

    internal GameResourceRepository CreateReadScope()
    {
        RequireCurrentRevision();
        return Root.CreateReadScope();
    }
}

/// <summary>The declared product-map static closure. Main retains this CPU
/// generation for the process; views only borrow it. No scene exit disposes
/// source content, and a replacement leaves old borrowers' snapshots intact.</summary>
internal sealed class MapStaticResources
{
    private readonly GameResourceRepository root;
    private readonly long revision;
    private readonly Dictionary<(string Archive, uint Scene, string Locale), MapStaticSceneResources> scenes;
    private readonly ResourceReadGeneration[] generation;
    private readonly ResourceOverrides overrides;

    private MapStaticResources(GameResourceRepository root, long revision, MapSceneConfiguration configuration,
        uint mainStageFileId,
        Dictionary<(string Archive, uint Scene, string Locale), MapStaticSceneResources> scenes,
        ResourceReadGeneration[] generation, D3D11TerrainPreparedPrograms[] terrainPrograms, D3D11PrimaryGpuMediaSource[] primaryGpuMedia,
        MapRoutePreparedMedia[] routeGpuMedia)
    {
        this.root = root; this.revision = revision; Configuration = configuration; MainStageFileId = mainStageFileId;
        this.scenes = scenes; this.generation = generation; overrides = new(root.OverrideDirectory);
        TerrainPrograms = Array.AsReadOnly(terrainPrograms);
        PrimaryGpuMedia = Array.AsReadOnly(primaryGpuMedia);
        RouteGpuMedia = Array.AsReadOnly(routeGpuMedia);
    }

    internal MapSceneConfiguration Configuration { get; }
    internal uint MainStageFileId { get; }
    internal IReadOnlyList<D3D11TerrainPreparedPrograms> TerrainPrograms { get; }
    internal IReadOnlyList<D3D11PrimaryGpuMediaSource> PrimaryGpuMedia { get; }
    internal IReadOnlyList<MapRoutePreparedMedia> RouteGpuMedia { get; }

    internal static MapStaticResources Load(GameResourceRepository root,
        MapSceneConfiguration configuration, string nativeProgramRoot, string staticMediaRoot, CancellationToken token,
        MapStaticResources? previous = null)
    {
        ArgumentNullException.ThrowIfNull(root); ArgumentNullException.ThrowIfNull(configuration);
        token.ThrowIfCancellationRequested();
        long revision = root.ResourceRevision;
        var reads = new Dictionary<GameResourceId, ResourceReadGeneration>();
        var repository = root.CreateReadScope(observed =>
        {
            if (reads.TryGetValue(observed.Id, out var precedingRead) && precedingRead != observed)
                throw new ResourceReadGenerationChangedException(observed.Id);
            reads[observed.Id] = observed;
        });
        //The actual tactics/gems consumer borrows this initialized fixed-file
        //generation later. Complete both declared locales before Title admission.
        foreach (string locale in new[] { "TC", "SC" })
        {
            token.ThrowIfCancellationRequested();
            _ = repository.LoadSkillDefinitions($"0010_{locale}/fixdataexce.s14");
            _ = repository.LoadTerrainTypes($"0010_{locale}/fixdataexce.s14");
            _ = repository.LoadObjectDefinitions($"0010_{locale}/fixdataexce.s14");
        }
        // Resolve the original normal Stage once in this read generation.
        // Openings borrow this identity; a fitted projection never chooses it.
        var hdr = HdrShaderResources.Load(repository);
        uint mainStageFileId = RenderResourceContext.LoadMainStageDatabaseId(repository);
        _ = configuration.InitialSceneIndex(playable: true, mainStageFileId);
        var projected = configuration.Scenes.Where(scene => configuration.TryGetProjection(scene.FileId, out _)).ToArray();
        if (projected.Length == 0)
            throw new InvalidDataException("The product map has no declared projected scene.");
        var scenes = new Dictionary<(string Archive, uint Scene, string Locale), MapStaticSceneResources>();
        // Only this load's declared selections enter the new owner. Unchanged
        // shader sources reuse their preceding publication without disk work;
        // removed or changed selections are not retained as a history cache.
        var reusable = previous?.TerrainPrograms.ToDictionary(program => (program.LogicalKey, program.Identity));
        var programs = new Dictionary<(string LogicalKey, string Identity), D3D11TerrainPreparedPrograms>();
        var primaryGpuMedia = new List<D3D11PrimaryGpuMediaSource>();
        var routeGpuMedia = new List<MapRoutePreparedMedia>();
        string generationDirectory = Path.Combine(staticMediaRoot, Guid.NewGuid().ToString("N"));
        foreach (var scene in projected)
        {
            token.ThrowIfCancellationRequested();
            var terrain = MapTerrainSourcePreparation.Prepare(repository, configuration.Archive, scene.FileId, token);
            terrain = terrain with
            {
                CpuGeometry = PreparedTerrainGeometryCatalogue.Prepare(terrain.Terrain.Terrain, terrain.Terrain.Height, token)
            };
            string sceneDirectory = Path.Combine(generationDirectory, configuration.Archive, scene.FileId.ToString("X8"));
            var primaryModels = D3D11CityGateStaticResources.Prepare(repository, terrain.Scene,
                Path.Combine(sceneDirectory, "primary-model-media"), token);
            var gpuMedia = D3D11PrimaryGpuMediaSource.Prepare(repository, primaryModels,
                terrain.Scene.Scene.Source.ResourceUri + "/primary", token);
            primaryGpuMedia.Add(gpuMedia);
            // Route declarations depend on Scene and seasonal registration,
            // not locale, camera, a draft or live World. Load all four normal
            // namespaces before Title and share identical media between them.
            var routeMedia = MapRouteStaticResources.Load(repository, terrain.Scene,
                Path.Combine(sceneDirectory, "route-media"), token);
            routeGpuMedia.AddRange(routeMedia.Media);
            var domesticEffects = MapDomesticEffectStaticResources.Load(repository, terrain.Scene, token);
            foreach (string locale in new[] { "TC", "SC" })
            {
                token.ThrowIfCancellationRequested();
                var media = D3D11SceneMediaPreparation.LoadSources(repository, terrain.Terrain, locale);
                var environmentStatic = MapEnvironmentStaticResources.Load(repository, media.Renderer, locale,
                    Path.Combine(sceneDirectory, "environment", locale), token);
                var sourcePrograms = D3D11TerrainProgramSources.From(media.Terrain.Source.Material);
                var programKey = (sourcePrograms.LogicalKey, sourcePrograms.Identity);
                if (!programs.TryGetValue(programKey, out var preparedPrograms))
                {
                    D3D11TerrainPreparedPrograms? preceding = null;
                    reusable?.TryGetValue(programKey, out preceding);
                    preparedPrograms = sourcePrograms.PrepareNativeInputs(nativeProgramRoot, token, preceding);
                    programs.Add(programKey, preparedPrograms);
                }
                scenes.Add((configuration.Archive, scene.FileId, locale),
                    new(root, revision, terrain, media, preparedPrograms, primaryModels, environmentStatic, gpuMedia, routeMedia, domesticEffects, hdr));
            }
        }
        var loaded = new MapStaticResources(root, revision, configuration, mainStageFileId, scenes, reads.Values.ToArray(),
            programs.Values.ToArray(), primaryGpuMedia.ToArray(), routeGpuMedia.ToArray());
        if (!loaded.IsCurrentGeneration(verifySources: true)) throw new ResourceReadGenerationChangedException(reads.Keys.First());
        token.ThrowIfCancellationRequested();
        return loaded;
    }

    // Physical dependency checks belong to actual preparation publication.
    // Ordinary application admission uses the notification owner's epoch;
    // standalone readers retain their existing source-stamp behavior.
    internal bool IsCurrentGeneration(bool verifySources = false)
    {
        if (root.ResourceRevision != revision) return false;
        if (!verifySources && root.HasResourceChangeNotifications) return true;
        var stamps = new Dictionary<string, (long Length, long Ticks)>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in generation)
        {
            string selected = Path.GetFullPath(overrides.Find(source.Id) ?? source.FallbackPath);
            if (!StringComparer.OrdinalIgnoreCase.Equals(selected, source.SelectedPath)) return false;
            if (!stamps.TryGetValue(selected, out var stamp))
            {
                try
                {
                    var file = new FileInfo(selected);
                    if (!file.Exists) return false;
                    stamp = (file.Length, file.LastWriteTimeUtc.Ticks);
                }
                catch (FileNotFoundException) { return false; }
                catch (DirectoryNotFoundException) { return false; }
                stamps.Add(selected, stamp);
            }
            if (stamp.Length != source.Length || stamp.Ticks != source.LastWriteUtcTicks) return false;
        }
        return root.ResourceRevision == revision;
    }

    internal MapStaticSceneResources RequireScene(string archive, uint scene, string locale)
    {
        if (!IsCurrentGeneration())
            throw new ResourceReadGenerationChangedException(generation[0].Id);
        if (!scenes.TryGetValue((archive, scene, locale), out var source))
            throw new InvalidDataException($"The projected map static source was not loaded: san14://rdb/{archive}/{scene:X8}, {locale}.");
        return source;
    }
}
