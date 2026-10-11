using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Concrete actual selected L -> package -> named six-face constants
/// -> source GPU WORK assembler. Pending only the named work/producer owner;
/// it cannot replace missing source activation or optional stages with null.</summary>
public sealed class SceneViewNativeMapSkySourceOwner : IAsyncDisposable
{
    private readonly SkyAmbientSourceFrameAssembler assembler;
    private readonly SkySelectedLightAtmosphereOwner atmosphere;
    private readonly SceneViewNativeMapSkyWorkSourceOwner work;
    private readonly string recordGeneration;
    private readonly Sky2TextureBindings textureBindings;
    private readonly Sky2TextureBindingSnapshot textureSnapshot;
    private SkySeasonPackageSampling sampling;
    private NativeMapFrameContext? completedWorkContext;
    public void BindActualGraphicsColorMaskOwner(MapSourceCreatedGraphicsColorMaskOwner actualOwner)
        => work.BindActualGraphicsColorMaskOwner(actualOwner);
    public MapOwnedGraphicsMaskPrefix RequireCompletedSkyGraphicsPrefix(NativeMapFrameContext context)
    {
        ValidateCompleteFrameCandidate(context);
        return work.RequireCompletedSkyGraphicsPrefix(context);
    }
    private SceneViewNativeMapSkySourceOwner(SkyAmbientSourceFrameAssembler assembler,
        SkySelectedLightAtmosphereOwner atmosphere, SceneViewNativeMapSkyWorkSourceOwner work,
        string generation,Sky2TextureBindings textureBindings,Sky2TextureBindingSnapshot textureSnapshot,
        SkySeasonPackageSampling sampling)
    { this.textureBindings=textureBindings;this.textureSnapshot=textureSnapshot;this.sampling=sampling;this.assembler = assembler; this.atmosphere = atmosphere; this.work = work; recordGeneration = generation; }

    public static async Task<SceneViewNativeMapSkySourceOwner> BuildAsync(
        SceneViewNativeMapLoadInputs loaded, Vkd3dShaderCompiler compiler,
        MapSourceCreatedLightingDeviceOwner lighting, MapSourceCreatedF103CurrentFrame bootstrap,
        MapSourceCreatedSeasonLightingFrame bootstrapPresent,
        SkySeasonMainCameraRgbFrame bootstrapRgb, MainSceneNode51Execution bootstrapExecution)
    {
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        var repo = loaded.Repository;
        var sampling=SkySeasonPackageTextures.Read(bootstrapPresent.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage);
        var textureBindings=new Sky2TextureBindings();
        textureBindings.ApplyPackage(sampling,reference=>SkySeasonPackageTextures.LoadSampling(repo,reference));
        var textureSnapshot=textureBindings.Capture();
        var atmosphere = SkySelectedLightAtmosphereOwner.CreateFresh(repo, locale,
            bootstrap.Lighting.Binding.ViewIdentity + "/owned-Generic-package", NativeLogDispatch.Sse);
        // Actual activation/typed optional-stage gates are distinct from the
        // backend's complete-generation batch budget, which is not Clock254.
        var work = SceneViewNativeMapSkyWorkSourceOwner.ReadSource(loaded, compiler, bootstrap, bootstrapPresent, bootstrapRgb,
            bootstrapExecution);
        try
        {
        var core = PrepareCore(repo, atmosphere, bootstrap.Feature, bootstrap.Lighting,
            work.BootstrapSelectedSkyRecord, bootstrapPresent,textureSnapshot);
        var ambient = SkyAmbientResources.Load(repo, repo.LoadObjects("system", 0xDAB638B2), 0x910603AC,
            repo.LoadObjects("system", 0x718CCB8B), repo.LoadObjects("system", 0x93FB3280));
        var applied = core.AppliedSeason;
        var controller = SkySceneSourceDefaults.PrepareController(applied.CurrentCamera.Controller,
            applied.CurrentCamera.Parameters, applied.CurrentCameraLights.SunPresent, applied.CurrentCameraLights.MoonPresent);
        var frame = SkySceneSourceFrameProvider.PrepareAppliedSeason(applied.Selection,
            applied.CurrentCamera, bootstrap.Lighting, ambient.SpecularTargets[0], applied.CurrentCameraLights,
            core.SharedAtmosphere, controller, 2,textureSnapshot);
        work.PrepareBootstrapProducers(frame);
        var first = core.CreateInputs(work.BootstrapProducers, work.BootstrapStages);
        var complete = SceneViewNativeMapSkyNamedWriteCoverage.PrepareCompleteCurrent(
            frame.SourceFrame, repo.LoadShaderContainer("system", 0x340AC335), checked((int)work.BootstrapStages.Sky2PixelProgram));
        // These two fields are metadata of the non-applied route. The actual
        // applied-source assembler returns before it reads either; the cache
        // object still comes from this exact current source, never smoke data.
        var construction = new SkyAmbientSourceConstruction(frame.SourceProvenance +
            ";all-named-current-writes;new-owned-padding-not-native-retained-cache",
            complete.Frame.Faces, applied.CurrentCameraLights,
            new("applied-route-does-not-read-ordinary-row-associations;package10-actual-row0", null, null, 0, false, false),
            complete.OwnedVertexPaddingTemplate, complete.OwnedPixelPaddingTemplate, 2,
            work.RequireSourceProgramSelection(ambient) with{TextureBindings=textureSnapshot});
        var assembler = await SkyAmbientSourceFrameAssembler.CreateAppliedSeasonAsync(repo, compiler,
            lighting.Registration, lighting.Source, construction, first,textureBindings, work.BuildOptionalProducersAsync,
            allowReadback: false);
        return new(assembler, atmosphere, work, bootstrap.Lighting.Binding.ViewIdentity,textureBindings,textureSnapshot,sampling);
        }
        catch (Exception buildFailure)
        {
            textureBindings.Dispose();
            var partial = new NativeMapBorrowerRetirementPackage(
                bootstrap.Lighting.Binding.ViewIdentity + "/source-Sky-partial/" + Guid.NewGuid().ToString("N"),
                buildFailure is NativeMapBorrowerConstructionException nested ? nested.RetainedPackage : null,
                work);
            try { await partial.DisposeAsync(); }
            catch (Exception retirementFailure)
            {
                partial.RetainFailure();
                throw new NativeMapBorrowerConstructionException(buildFailure, retirementFailure, partial);
            }
            throw;
        }
    }

    public async Task SubmitCurrentAsync(MapSourceCreatedNativeSessionPreparedFrame actual,
        MapSourceCreatedSeasonLightingFrame present, SkySeasonMainCameraRgbFrame rgb,
        MainSceneNode51Execution actualExecution, float explicitOwnedGeometryDeltaSeconds)
    {
        var source = actual.PreparedSourceFrame ?? throw new InvalidDataException("Sky requires actual current owned View publication.");
         actual.Feature.ValidateFrame(actual.MainCamera, actual.Lighting);
        if (actual.Context.FrameNumber != present.FrameNumber || actual.Feature.CurrentView.ViewIdentity != recordGeneration)
            throw new InvalidDataException("Sky current View and actual final Present-season prefix belong to different attempts.");
        var selectedRecord = source.ViewFields.CurrentPublication.SelectedShadowRecord1BD0 ??
            throw new InvalidDataException("Current source Sky1B20 needs its actual independent selected kind1 record owner.");
        // Work scope proves 1B20==this record; 1BD0 alone does not identify
        // the Sky selection. Its source predicates are validated independently.
        var currentSelection = work.PrepareCurrentSelection(actual, present, rgb, selectedRecord, actualExecution);
        var selectedSampling=SkySeasonPackageTextures.Read(present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage);
        if(!ReferenceEquals(selectedSampling,sampling))
        {
            textureBindings.RequireCompatiblePackage(selectedSampling,
                reference=>SkySeasonPackageTextures.LoadSampling(work.Repository,reference));
            sampling=selectedSampling;
        }
        textureSnapshot.RequireCurrent();
        var core = PrepareCore(work.Repository, atmosphere, actual.Feature, actual.Lighting,
            currentSelection.SelectedSkyRecord, present,textureSnapshot);
        var applied = core.AppliedSeason;
        var controller = SkySceneSourceDefaults.PrepareController(applied.CurrentCamera.Controller,
            applied.CurrentCamera.Parameters, applied.CurrentCameraLights.SunPresent, applied.CurrentCameraLights.MoonPresent);
        var sourceFrame = SkySceneSourceFrameProvider.PrepareAppliedSeason(applied.Selection,
            applied.CurrentCamera, actual.Lighting, assembler.Scene.SpecularTargets[0].Source,
            applied.CurrentCameraLights, core.SharedAtmosphere, controller, 2,textureSnapshot);
        var workFrame = work.PrepareCurrentWork(actual.Context, currentSelection, sourceFrame, explicitOwnedGeometryDeltaSeconds);
        var input = core.CreateInputs(workFrame.Producers, workFrame.Stages);
        if (!workFrame.Enable.AllowsWorkInitialization)
        { work.CompleteDisabledSourceCall(workFrame); completedWorkContext = actual.Context; return; }
        work.BeginActualWorkCall(workFrame);
        var submitted = await assembler.SubmitFrameAsync(workFrame.GenerationIndex, input,
            workFrame.MaximumWorkUnits, workFrame.OptionalLight);
        work.CompleteActualWorkCall(workFrame, submitted);
        completedWorkContext = actual.Context;
    }

    /// <summary>GPU substage completion advances only the actual work slot.
    /// Star/Cloud source time commits at the complete Host boundary; a later
    /// Host failure must not double-advance the next full presentation delta.</summary>
    public void ValidateCompleteFrameCandidate(NativeMapFrameContext actualContext)
    {
        if (!ReferenceEquals(actualContext, completedWorkContext))
            throw new InvalidDataException("Complete Host frame has no same-attempt completed Sky work candidate.");
        work.ValidateCompleteFrameCandidate(actualContext);
    }

    public void CommitCompleteFrame(NativeMapFrameContext actualContext)
        => work.CommitCompleteFrame(actualContext);

    private sealed record PreparedCore(MapSourceCreatedLightingPacket Lighting, int SourceSeasonMonth,
        string SeasonSourceProvenance, SkySceneSharedAtmosphere SharedAtmosphere,
        SkySeasonAppliedFrameInputs AppliedSeason)
    {
        public SkyAmbientSourceFrameInputs CreateInputs(SkySceneProducerFrame actualProducers,
            SkyAppliedSourceStageFrame actualStages)
            => new(Lighting, SourceSeasonMonth, SeasonSourceProvenance, SharedAtmosphere,
                SkyAmbientSourceState.ResolvedCurrent, actualProducers, AppliedSeason: AppliedSeason)
                { SourceStages = actualStages };
    }

    private static PreparedCore PrepareCore(GameResourceRepository repo,
        SkySelectedLightAtmosphereOwner owner, MapSourceCreatedF103FeaturePacket feature,
        MapSourceCreatedLightingPacket lighting, TerrainSelectedKind1Record actualSkyRecord,
        MapSourceCreatedSeasonLightingFrame present,Sky2TextureBindingSnapshot textureSnapshot)
    {
        if (present.FrameNumber != lighting.FrameNumber ||
            !present.PreparedCameraPrefix.Span.SequenceEqual(lighting.PreparedCameraPrefix.Span))
            throw new InvalidDataException("Sky must consume the final same-frame Present weather/clock prepared prefix.");
        if (actualSkyRecord.FrameNumber != lighting.FrameNumber || actualSkyRecord.ViewIdentity != lighting.Binding.ViewIdentity ||
            !actualSkyRecord.CompleteLightData.Span.SequenceEqual(lighting.LocalLightData.Span))
            throw new InvalidDataException("Actual selected Sky record does not own this same complete current LightData.");
        var selected = SkySceneSelectedViewLightSource.CaptureOwnedKind1(feature, lighting,
            actualSkyRecord.RecordOwnerIdentity);
        var currentAtmosphere = owner.PrepareCurrent(selected, lighting);
        currentAtmosphere.ValidateCurrent(owner, selected, lighting);
        var season = present.AcceptedSeasonCamera.Inputs.Source;
        var sourceMoon = textureSnapshot.RequireNull(SkySamplingRole.Moon);
        var moon = Sky2SourceZeroMoon.Create(repo.LoadShaderContainer("system", 0x340AC335),
            sourceMoon, lighting, selected);
        var raw = selected.CompleteLightData.Span;
        Vector4 packagePosition = new(BinaryPrimitives.ReadSingleLittleEndian(raw[..4]),
            BinaryPrimitives.ReadSingleLittleEndian(raw.Slice(4,4)), BinaryPrimitives.ReadSingleLittleEndian(raw.Slice(8,4)),
            BinaryPrimitives.ReadSingleLittleEndian(raw.Slice(12,4)));
        var lights = SkySceneSelectedViewLightSource.PrepareSourceZeroMoonLights(selected,
            packagePosition, actualSkyRecord.RecordOwnerIdentity + "/D3A850-package10-row0", moon);
        return new(lighting, season.Season.Calendar.State.ThirdDate.Month, season.SourceProvenance,
            currentAtmosphere.Atmosphere, new(season.Selection, present.CurrentSkyCamera, lights));
    }

    public async ValueTask DisposeAsync()
    {
        textureBindings.Dispose();
        await assembler.DisposeAsync();
        await work.DisposeAsync();
    }
}
