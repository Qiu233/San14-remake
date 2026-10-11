using System.Runtime.CompilerServices;
using San14.Data;

/// <summary>Concrete current owned View/season/CPU source join for the loaded
/// SceneView. Bootstrap has no target claim. Actual attempts attach their
/// accepted terrain inventory and node51 execution before stencil submission.</summary>
public sealed class SceneViewNativeMapF103SourceOwner
{
    private readonly SceneViewNativeMapLoadInputs loaded;
    private readonly SceneViewNativeMapCpuSourceOwner cpu;
    private readonly MapSourceCreatedLightingDeviceOwner lightingOwner;
    private readonly MapSourceCreatedF103CurrentFrameFactory f103;
    private readonly SceneViewNativeMapSourceFields scalars;
    private readonly MainMapContextCurrentSource contextOwner;
    private readonly CommonRenderSettings commonSource;
    private readonly MainMapCameraModeOwner mainMode;
    private readonly NativeTerrainCurrentViewOwner terrainOwner;
    private readonly TerrainViewPublicationSource terrainSource;
    private readonly SceneViewNativeMapNode51ExecutionOwner node51,node56;
    private readonly SceneViewNativeMapNode51ExecutionOwner bootstrapNode51Owner;
    private readonly MainSceneNode51Execution bootstrapNode51;
    private readonly MapSeasonMainCameraPhaseSource seasonPhase;
    private readonly SkyWeatherNativePowerSource weatherPower;
    private readonly string generation;
    private readonly ConditionalWeakTable<NativeMapFrameContext, CurrentFrame> current = new();
    private AcceptedSkyLightingState? previousLighting;
    public MapLightingBinding Binding { get; }
    public MapSourceCreatedF103CurrentFrame Bootstrap { get; }
    public PreparedViewLocalLightQueries BootstrapEarly { get; }
    public MapStaticModelCurrentOwnedFrame BootstrapPopulation => Bootstrap.CpuPopulation!;
    private MapSourceCreatedSeasonLightingFrame bootstrapPresent = null!;
    private SkySeasonMainCameraRgbFrame bootstrapRgb = null!;
    public MapSourceCreatedSeasonLightingFrame RequireBootstrapPresentSeason() => bootstrapPresent;
    public SkySeasonMainCameraRgbFrame RequireBootstrapSeasonRgb() => bootstrapRgb;

    private sealed record CurrentFrame(MapSourceCreatedF103CurrentFrame F103,
        MapSourceCreatedF103PopulationPublicationInputs Publication,
        SkySeasonMainCameraRgbFrame Rgb, MapSourceCreatedSeasonLightingFrame Present,
        IReadOnlyList<TerrainCurrentKind1WorldPQPublication> Queue,
        NativeTerrainCurrentViewFrame? Terrain, MainSceneNode51Execution? Node51,
        MapSourceCreatedStaticPrimitiveAdmissionPlan Admission,
        PreparedViewLocalLightQueries Queries);

    public SceneViewNativeMapF103SourceOwner(SceneViewNativeMapLoadInputs loaded,
        SceneViewNativeMapCpuSourceOwner cpu, MapSourceCreatedLightingDeviceOwner lighting,
        LoadedDeferredTexturePlan plan, MapLightingBinding binding, string viewGeneration,
        MainMapCameraModeOwner actualMainMode)
    {
        this.loaded=loaded; this.cpu=cpu; lightingOwner=lighting; Binding=binding; generation=viewGeneration;
        mainMode=actualMainMode;
        var repo=loaded.Repository; string locale=loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        scalars=SceneViewNativeMapSourceFields.CreateFreshRetainedMain(loaded.InitialFrame.MainCamera.Camera);
        contextOwner=MainMapContextCurrentSource.CreateFresh(repo,locale,generation+"/Host");
        commonSource=CommonRenderResources.ImportSettings(repo.LoadObjects("KIDSSystemResource",0x6D011726));
        f103=new(plan,MapSourceCreatedF103ColorScope.FreshSourceNamedModulesWithoutHeaderEdits);
        terrainSource=TerrainViewPublicationSource.ReadFieldScene(loaded.TerrainMaterial.Parameters);
        terrainOwner=NativeTerrainCurrentViewOwner.CreateFreshFieldSceneNoExternalAssignments(loaded.TerrainMaterial);
        node51=SceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/owned-View-collection");
        node56=SceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/node56-owned-View-collection",56);
        bootstrapNode51Owner=SceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,
            generation+"/bootstrap-owned-View-collection");
        seasonPhase=MapSeasonMainCameraPhaseSource.LoadSourceDefaults(repo,locale);
        weatherPower=SkyWeatherNativePowerSource.Load(repo,locale);
        var boot=loaded.InitialFrame with {MainCamera=loaded.InitialFrame.MainCamera with {
            Camera=loaded.InitialFrame.MainCamera.Camera.CopyAcceptedSnapshotForOwnedFrame(1,generation+"/bootstrap-optics")}};
        var bootstrap=Prepare(boot,null,0);
        Bootstrap=bootstrap.F103;
        bootstrapPresent=bootstrap.Present;
        bootstrapRgb=bootstrap.Rgb;
        bootstrapNode51=bootstrapNode51Owner.PublishAndPrepareCurrent(Bootstrap.FrameNumber,Binding);
        BootstrapEarly=bootstrap.Queries;
    }

    public MapSourceCreatedF103CurrentFrame PrepareCurrent(NativeMapFrameContext context,
        SceneViewNativeMapFrameCapture capture, NativeMapArmyPreparedFrame army,
        MapLightingBinding actualSessionBinding, float explicitPresentationDelta)
    {
        if(actualSessionBinding!=Binding || !ReferenceEquals(army.Context,context) ||
            army.CpuPrimitivePopulation is not { } actualArmy || actualArmy.Actors.Count!=0 ||
            army.OrdinaryActors!=0 || army.FlagActors!=0 || army.Unresolved.Count!=0)
            throw new InvalidDataException("Initial source factory requires its actual same-context empty Army publication; nonempty/unknown visibility cannot become empty.");
        var frame=Prepare(capture,context,explicitPresentationDelta);
        current.Add(context,frame);
        return frame.F103;
    }

    private CurrentFrame Prepare(SceneViewNativeMapFrameCapture capture,
        NativeMapFrameContext? accepted,float delta)
    {
        if(!float.IsFinite(delta)||delta<0||capture.TerrainBatches.Count==0||
            capture.TerrainBatches.Any(b=>b.Patches.Count==0)||capture.World.NativeMapSeason is not { } season)
            throw new InvalidDataException("Current source View needs its real nonempty terrain, owned third-date season and explicit presentation delta.");
        var population=cpu.PrepareCurrent(capture);
        long number=population.FrameNumber;
        var mode=mainMode.PrepareCurrent(capture.MainCamera); mode.ValidateCurrent(mainMode,capture.MainCamera);
        var selectedSeason=SkySeasonMainCameraProducer.ResolveOwnedSource(loaded.Repository,
            MapSeasonRuntime.ResolveSourceFrame(loaded.Repository,season,number),capture.MainCamera.Camera.ExecutableLocale);
        var before=SkySeasonMainCameraProducer.PrepareBeforeFinalPreparation(selectedSeason,capture.MainCamera,
            mode.Mode288,seasonPhase,previousAcceptedLightingState:previousLighting);
        var rgb=SkySeasonMainCameraProducer.PrepareSunMoonAndSky(before,delta,NativeCameraTangentDispatch.Sse,
            SkySceneSourceDefaults.CreateControllerConstructor(),SkySceneSourceDefaults.CreateSky2Parameters(107));
        var weather=SkySeasonMainWeatherFog.PrepareSourceDatabaseResetFrame(rgb,weatherPower,NativeCameraTangentDispatch.Sse);
        var present=MapSourceCreatedSeasonLightingFrame.CapturePreparedSeason(rgb,weather);
        var lighting=MapSourceCreatedLighting.Prepare(lightingOwner.Source,
            new(number,Binding,present.Parameters,present.PriorPreparedCameraPrefix,
                contextOwner.Host.ContextFlags3E4,delta,MapLightingRuntimeTable.Present,
                MapLightingCameraScope.LightingPrefix,scalars.PrepareBuiltinAmbientCameraRate(capture.MainCamera.Camera),
                null,new(),PresentSeasonCamera:present));
        var common=MapCommonRenderState.FromSettings(commonSource,
            number,generation+"/source-Common-settings",generation+"/Common",Binding.ViewIdentity,
            generation+"/properties-applied-before-ambient");
        // These are newly created managed source kind1 records. The queue
        // really owns every pair/record identity and complete shared payload;
        // it is not a process address or an inference from a counter row.
        var masks=MapSourceCreatedViewFields.SourceKind1RecordMasks(lighting,common.ViewGroupSelector!.Value);
        var queue=masks.Select((mask,index)=>new TerrainCurrentKind1WorldPQPublication(number,Binding.ViewIdentity,
            generation+"/queue/"+number,index,0x10000000,generation+"/pair/"+number+"/"+index,
            generation+"/record/"+number+"/"+index,mask,true,generation+"/owned-CE31D0-kind1-construction")).ToArray();
        var sourceCameraTail=MapSourceCreatedViewFields.ReadFreshRetainedCameraFields(capture.MainCamera.Camera);
        int shadowGroup=sourceCameraTail.ViewSignedA00;
        if(shadowGroup is <1 or >32 || common.ViewGroupSelector is not byte skySelector || skySelector is <1 or >32)
            throw new NotSupportedException("This nonnull owned selection needs actual MainA00 and Common3F2 group fields.");
        uint shadowSelectionMask=1u<<(shadowGroup-1);
        uint skySelectionMask=1u<<(skySelector-1);
        // D789B2..D789CB clones Main+A00 and produces the independent
        // shadow selection mask. D78BC4/D78BD2 selects its first matching
        // kind1 record. This is not inferred from Common3F2 or queue order0.
        var selected=queue.FirstOrDefault(item=>(item.CurrentWorldPQWord78&shadowSelectionMask)!=0)??
            throw new InvalidDataException("Current queue has no actual MainA00 selected shadow record.");
        var skySelected=queue.FirstOrDefault(item=>(item.CurrentWorldPQWord78&~skySelectionMask)==0)??
            throw new InvalidDataException("Current queue has no actual Common3F2 selected Sky record.");
        if(!ReferenceEquals(selected,skySelected))
            throw new NotSupportedException("This initial source branch closes only the independently selected same-record 1B20/1BD0 case.");
        uint mask1BF8=uint.MaxValue;
        // D78B8C..D78BAF/D78C7B: only active lower F1C nibble
        // intersects the accumulated record mask. Zero nibble preserves it.
        foreach(var item in queue)
            if((lighting.LocalLightData.Span[0xF1C]&0xF)!=0)mask1BF8&=item.CurrentWorldPQWord78;
        var payload=SkySceneSelectedViewLightSource.Capture(lighting.LocalLightData.Span,
            new(number,Binding.ViewIdentity,selected.CurrentWorldPQOwnerIdentity,Binding.LightOwnerIdentity));
        var record=TerrainSelectedKind1Record.FromOwnedCurrentPublication(lighting,selected,payload,mask1BF8,
            generation+"/owned-current-D789B2-A00/D78BC4-firstmatching-1BD0-and-independent-Common3F2-1B20");
        var publication=new MapSourceCreatedViewPublicationState(number,Binding.ViewIdentity,
            Binding.CameraOwnerIdentity,generation+"/current-managed-View/"+number,false,0,[0,0,0,0],true,null)
            {OtherAdditionalOwners1B80_1BB0_1B98Absent=true,SelectedShadowRecord1BD0=record};
        NativeTerrainCurrentViewFrame? terrain=null;
        if(accepted is not null)
        {
            terrain=terrainOwner.PrepareWithSelectedKind1Record(accepted,lighting,publication,record,common);
            publication=terrain.CurrentViewPublication;
        }
        else publication=publication with {Terrain1BB8=TerrainCurrentViewPublication.PrepareWithSelectedKind1Record(
            terrainSource,lighting,capture.TerrainBatches.SelectMany(b=>b.Patches).ToArray(),record,common,
            generation+"/bootstrap-CPU-terrain-not-GPU")};
        var admissionInput=new MapSourceCreatedF103PopulationPublicationInputs(true,true,publication,
            new Dictionary<string,MapSourceCreatedModelExclusionOverride>(),true,null,
            generation+"/complete-static-and-actual-zero-Army/"+number);
        var q=contextOwner.BeginDeferredPreparation(number,generation+"/current-execution/"+number);
        var frame=f103.PrepareFromPopulation(population,lightingOwner.Source,lighting,common,q,admissionInput,null,
            generation+"/actual-current-source-F103/"+number);
        var admission=MapSourceCreatedStaticPrimitiveAdmissionPlan.PrepareFresh(frame,admissionInput);
        MainSceneNode51Execution? execution=accepted is null?null:node51.PublishAndPrepareCurrent(number,Binding);
        var queries=PreparedViewLocalLightQueries.CreateFresh(frame.ViewFields,0);
        return new(frame,admissionInput,rgb,present,queue,terrain,execution,admission,queries);
    }

    public MapStaticModelCurrentOwnedFrame RequirePopulation(NativeMapFrameContext context)=>Require(context).F103.CpuPopulation!;
    public MapSourceCreatedStaticPrimitiveAdmissionPlan RequireAdmission(NativeMapFrameContext context)=>Require(context).Admission;
    public PreparedViewLocalLightQueries PrepareEarly(MapSourceCreatedNativeSessionPreparedFrame accepted) =>
        Require(accepted.Context).Queries;
    public SkySeasonMainCameraRgbFrame RequireSeasonRgb(NativeMapFrameContext context)=>Require(context).Rgb;
    public MainSceneNode51Execution RequireBootstrapNode51Execution()
    { bootstrapNode51Owner.ValidateCurrent(bootstrapNode51); return bootstrapNode51; }
    public MainSceneNode51Execution RequireNode51Execution(NativeMapFrameContext context)
    {
        var actual=Require(context).Node51 ?? throw new InvalidDataException("Current View execution is unavailable.");
        node51.ValidateCurrent(actual); return actual;
    }
    public MapSourceCreatedSeasonLightingFrame RequirePresentSeason(NativeMapFrameContext context)=>Require(context).Present;
    public MainMapContextCurrentPacket PrepareAuxiliaryContext(MapSourceCreatedNativeSessionPreparedFrame accepted)
    {
        var frame=Require(accepted.Context).F103;
        return contextOwner.ApplyExecutedThinSurfaceFlagWriter(frame.Context,checked((int)frame.ViewFields.RainDropFlags19B4),
            generation+"/owned-thin-surface-writer",frame.ViewFields.SourceProvenance);
    }
    public NativeTerrainStencilPreparedFrame PrepareTerrainStencil(MapSourceCreatedNativeSessionPreparedFrame accepted,
        NativeTerrainStencilCurrentOwner stencil,TerrainGbufferRasterState raster)
    {
        var frame=Require(accepted.Context);
        node51.ValidateCurrent(frame.Node51!);
        var terrainExecution=node56.PublishAndPrepareCurrent(accepted.Context.FrameNumber,frame.F103.Lighting.Binding);
        var eligibleTerrainRecords=frame.Queue.Where(r=>(terrainSource.Exclusion2B8&r.CurrentWorldPQWord78)==0).ToArray();
        if(eligibleTerrainRecords.Length!=1)
            throw new NotSupportedException("Current owned kind1 queue cannot supply this finite single ordinary Terrain submission: " +
                $"Field exclusion={terrainSource.Exclusion2B8:X8}; HDRI declaration group={frame.F103.Lighting.SourceIdentity.DeclaredGroupMask:X8}; " +
                $"current masks=[{string.Join(",",frame.Queue.Select(r=>r.CurrentWorldPQWord78.ToString("X8")))}]; eligible={eligibleTerrainRecords.Length}. " +
                "The additional current kind1 record/payload creator must be supplied; missing records cannot be remapped to an existing light.");
        var actualTerrainRecord=eligibleTerrainRecords[0];
        return stencil.PrepareFromGeneralScene(accepted.Context,frame.F103,frame.Terrain!,actualTerrainRecord,terrainExecution,raster);
    }
    public void CommitCompleteFrame(NativeMapFrameContext context)
    { previousLighting=AcceptedSkyLightingState.Capture(Require(context).Rgb); }
    private CurrentFrame Require(NativeMapFrameContext context)
        =>current.TryGetValue(context,out var value)?value:throw new InvalidDataException("This accepted context has no actual current owned source View.");
}
