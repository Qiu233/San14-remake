using San14.Core;
using San14.Data;

/// <summary>One opening's CPU resource owners. The caller runs preparation on
/// an exclusive worker, then transfers these same owners to its map thread.
/// No session, live World, Godot object or renderer is borrowed here.</summary>
internal sealed class D3D11MapCpuResources : IDisposable
{
    private bool ownsResourceContext = true;
    private bool disposed;

    private D3D11MapCpuResources() { }

    internal GameResourceRepository Repository { get; private init; } = null!;
    internal LoadedSceneResources Scene { get; private init; } = null!;
    internal string Source { get; private init; } = "";
    internal string Inputs { get; private init; } = "";
    internal string Locale { get; private init; } = "";
    internal int Width { get; private init; }
    internal int Height { get; private init; }
    internal string SeasonalInputsDirectory => Path.Combine(Inputs, "current-season-resource-input");
    internal string ResourceContextDirectory => Path.Combine(Inputs, "resource-context-input");
    internal string ArmyMaterialDirectory => Path.Combine(Inputs, "army-visual-input", "material-source");
    internal string FlagMaterialDirectory => Path.Combine(Inputs, "city-accessory-input", "material-source");
    internal D3D11SceneMediaPreparation SceneMedia { get; private init; } = null!;
    internal SkyCloudProgramResources CloudPrograms { get; private init; } = null!;
    internal GroundDetailResources GroundDetails { get; private init; } = null!;
    internal HeightFogProgramResources FogPrograms { get; private init; } = null!;
    internal FogAccelerationResources FogAcceleration { get; private init; } = null!;
    internal SkyMainProgramResources SkyPrograms { get; private init; } = null!;
    internal D3D11CurrentSeasonResources SeasonResources { get; private init; } = null!;
    internal D3D11CurrentSeasonLibrary SeasonLibrary { get; private init; } = null!;
    internal RenderResourceContext ResourceContext { get; private init; } = null!;
    internal RenderResourceContext.Batch ResourceSeasonBatch { get; private init; } = null!;
    internal RainDropProviderSource RainProvider { get; private init; } = null!;
    internal D3D11SpecialEffectEncoding.Source SpecialEffectInputs { get; private init; } = null!;
    internal D3D11CurrentHdrCatalog Hdr { get; private init; } = null!;
    internal MapCommonRenderState InitialModelCommon { get; private init; } = null!;
    internal MainMapContextOwnerSource InitialContext { get; private init; } = null!;
    internal D3D11CityGateTemplateBank PrimaryTemplates { get; private init; } = null!;
    internal CityGateAccessoryResources CityAccessories { get; private init; } = null!;
    internal CityGateAccessoryGeometryResources AccessoryGeometry { get; private init; } = null!;
    internal ArmyGeometryResources ArmyResources { get; private init; } = null!;
    internal ArmyMaterialResources ArmyMaterials { get; private init; } = null!;
    internal CityGateFlagMaterialResources FlagMaterials { get; private init; } = null!;
    internal LoadedScenario Scenario { get; private init; } = null!;
    internal LoadedMap Map { get; private init; } = null!;
    internal uint CommonFrustumMode { get; private init; }

    internal static D3D11MapCpuResources Prepare(string sourceDirectory, string? overrideDirectory,
        LoadedTerrainSceneResources terrainSources, MainMapCameraFrame frame,
        NativeMapSeasonState nativeSeason, string locale, string source,
        int width, int height, string scenarioKey, CancellationToken token)
        => PrepareCore(sourceDirectory, overrideDirectory, terrainSources, frame,
            repository => D3D11CurrentSeasonResources.LoadSourceReadyCpu(repository, nativeSeason, frame),
            locale, source, width, height, scenarioKey, token);

    internal static D3D11MapCpuResources PrepareScenarioPreview(string sourceDirectory, string? overrideDirectory,
        LoadedTerrainSceneResources terrainSources, MainMapCameraFrame frame,
        MapSeasonScenarioPreviewFrame previewSeason, string locale, string source,
        int width, int height, string scenarioKey, CancellationToken token)
        => PrepareCore(sourceDirectory, overrideDirectory, terrainSources, frame,
            repository => D3D11CurrentSeasonResources.LoadScenarioPreviewSourceReadyCpu(repository, previewSeason, frame),
            locale, source, width, height, scenarioKey, token);

    private static D3D11MapCpuResources PrepareCore(string sourceDirectory, string? overrideDirectory,
        LoadedTerrainSceneResources terrainSources, MainMapCameraFrame frame,
        Func<GameResourceRepository, D3D11CurrentSeasonResources> loadSeason, string locale, string source,
        int width, int height, string scenarioKey, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var repository = new GameResourceRepository(sourceDirectory, overrideDirectory);
        var scene = terrainSources.Scene;
        string inputs = Path.GetDirectoryName(source)
            ?? throw new InvalidDataException("Native map preparation has no output parent.");
        RenderResourceContext? resourceContext = null;
        RenderResourceContext.Batch? resourceSeasonBatch = null;
        try
        {
            // These banks are produced from this scene and accepted camera.
            // They are outputs of this opening, never an offline-package input.
            var sceneMedia = D3D11SceneMediaPreparation.Prepare(repository, terrainSources,
                frame.Camera, source, width, height);
            token.ThrowIfCancellationRequested();
            var cloudPrograms = SkyCloudProgramResources.Load(repository, sceneMedia.Renderer);
            var groundDetails = GroundDetailResources.Prepare(repository, scene,
                frame.Camera.Lens.ReverseDepth, locale);
            var fogPrograms = HeightFogProgramResources.Load(repository);
            var fogAcceleration = FogAccelerationResources.Load(repository);
            var skyPrograms = SkyMainProgramResources.Load(repository, sceneMedia.Renderer);
            token.ThrowIfCancellationRequested();

            D3D11ProjectShaderInputPreparation.WriteNativeInputs(source, locale);
            D3D11TimingInputPreparation.Load(repository, locale)
                .WriteAnimationCadenceInputs(Path.Combine(inputs, "animation-cadence-input"));
            var seasonResources = loadSeason(repository);
            string seasonalInputs = Path.Combine(inputs, "current-season-resource-input");
            var seasonLibrary = new D3D11CurrentSeasonLibrary(repository, seasonResources, frame);
            seasonLibrary.WriteNativeLutInputs(seasonalInputs);
            new D3D11TerrainSeasonPalette(sceneMedia.Terrain.Parameters).WriteNativeInputs(seasonalInputs);
            token.ThrowIfCancellationRequested();

            resourceContext = RenderResourceContext.LoadCurrentMap(repository, scene,
                seasonResources.Source.CameraSource.Season.Resources.Action, out resourceSeasonBatch);
            var rainProvider = RainDropProviderSource.Load(repository, resourceContext);
            var specialEffectInputs = D3D11SpecialEffectEncoding.Create(repository, seasonResources.InitialCommonSettings);
            resourceContext.WriteNativeInputs(Path.Combine(inputs, "resource-context-input"));
            var hdrResources = HdrShaderResources.Load(repository);
            var hdr = D3D11CurrentHdrCatalog.Create(repository, frame, hdrResources);
            token.ThrowIfCancellationRequested();

            var initialCommonObjects = repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId);
            var initialModelCommon = MapCommonRenderState.FromSettings(
                CommonRenderResources.ImportSettings(initialCommonObjects),
                frame.Camera.FrameNumber, "map-model/settings", "map-model/common", "map-model/view", "initial-resource-preparation");
            var initialContext = MainMapContextOwnerSource.LoadFreshHostDefaults(locale);
            D3D11SdfInputPreparation.PrepareFreshNormalMain(repository, initialContext,
                DefaultView0Source.LoadDeclarationDefaults(repository), initialCommonObjects)
                .WriteNativeInputs(Path.Combine(inputs, "sdf-source-input"));
            var records = MapSourceCreatedWorldKind1Publication.ReadSeasonSourceRecords(seasonResources.Source.DirectionalDeclarations);
            var primaryTemplates = D3D11CityGateTemplateBank.Prepare(repository, scene, frame.Camera,
                initialModelCommon, initialContext.ContextFlags3E4, records,
                sceneMedia.FieldModels.ShaderTableOwners, Path.Combine(inputs, "base-visual-world-input", "primary-templates"));
            primaryTemplates.WriteNativeInputs();
            token.ThrowIfCancellationRequested();

            var cityAccessories = CityGateAccessoryResources.Load(repository, scene);
            cityAccessories.WriteNativeInputs(Path.Combine(inputs, "city-accessory-input"));
            var accessoryGeometry = CityGateAccessoryGeometryResources.Prepare(repository, cityAccessories,
                frame.Camera, initialModelCommon, initialContext.ContextFlags3E4, records,
                locale, Path.Combine(inputs, "city-accessory-input"));
            accessoryGeometry.WriteNativeInputs();
            token.ThrowIfCancellationRequested();
            var armyResources = ArmyGeometryResources.Prepare(repository, frame.Camera, initialModelCommon,
                initialContext.ContextFlags3E4, records, accessoryGeometry, locale, Path.Combine(inputs, "army-visual-input"));
            armyResources.WriteNativeInputs();
            armyResources.WriteActionInputs(Path.Combine(inputs, "army-graphics-action-input"));
            token.ThrowIfCancellationRequested();

            var armyMaterials = ArmyMaterialResources.Load(repository, locale, armyResources);
            //Default pole materials and their media belong to loaded resources,
            //before any live Army publication requests its dynamic flag keys.
            armyMaterials.WriteNativeInputs(Path.Combine(inputs, "army-visual-input", "material-source"));
            ArmyMaterialManagerResources.Load(repository, armyResources.Catalog,
                resourceContext.MaterialInitialization!).WriteNativeInputs(Path.Combine(inputs, "army-visual-input", "material-source"));
            var flagMaterials = CityGateFlagMaterialResources.Load(repository, locale, accessoryGeometry);
            flagMaterials.WriteNativeInputs(Path.Combine(inputs, "city-accessory-input", "material-source"));
            uint commonFrustumMode = MapModelNativeVisibilitySources.ReadCommonViewTemplate(
                repository.LoadObjects("KIDSSystemResource", 0x6D011726), 0x899F101B).Value;
            var scenarioMetadata = repository.EffectiveScenarios.Single(s =>
                GameResourceId.File(s.SourcePath).ToString() == scenarioKey && s.Locale == locale);
            var scenario = repository.LoadScenario(scenarioMetadata);
            var map = repository.LoadCeMap();
            token.ThrowIfCancellationRequested();

            return new()
            {
                Repository = repository, Scene = scene, Source = source, Inputs = inputs,
                Locale = locale, Width = width, Height = height,
                SceneMedia = sceneMedia, CloudPrograms = cloudPrograms, GroundDetails = groundDetails,
                FogPrograms = fogPrograms, FogAcceleration = fogAcceleration, SkyPrograms = skyPrograms,
                SeasonResources = seasonResources, SeasonLibrary = seasonLibrary,
                ResourceContext = resourceContext, ResourceSeasonBatch = resourceSeasonBatch,
                RainProvider = rainProvider, SpecialEffectInputs = specialEffectInputs, Hdr = hdr,
                InitialModelCommon = initialModelCommon, InitialContext = initialContext,
                PrimaryTemplates = primaryTemplates, CityAccessories = cityAccessories,
                AccessoryGeometry = accessoryGeometry, ArmyResources = armyResources,
                ArmyMaterials = armyMaterials, FlagMaterials = flagMaterials,
                Scenario = scenario, Map = map, CommonFrustumMode = commonFrustumMode
            };
        }
        catch
        {
            resourceSeasonBatch?.Dispose();
            resourceContext?.Dispose();
            throw;
        }
    }

    /// <summary>The adopted map now retires this context and its season batch.
    /// Other retained CPU owners continue to borrow this repository.</summary>
    internal void DetachResourceContext()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!ownsResourceContext) throw new InvalidOperationException("Native map resource context was already transferred.");
        ownsResourceContext = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (ownsResourceContext)
        {
            ResourceSeasonBatch.Dispose();
            ResourceContext.Dispose();
        }
    }
}
