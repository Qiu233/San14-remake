using System.Runtime.CompilerServices;
using System.Numerics;
using San14.Data;

/// <summary>Actual SceneView population, Environment five-root publication,
/// Main seasonal Camera, selected shadow record and ordered World F103 join.
/// Resource creation and provider selection belong to the supplied light owners.</summary>
public sealed class SceneViewNativeMapWorldF103SourceOwner
{
    private readonly SceneViewNativeMapLoadInputs loaded;
    private readonly SceneViewNativeMapCpuSourceOwner cpu;
    private readonly Func<MapSeasonDirectionalLightSourceSet,IReadOnlyList<MapSourceCreatedDirectionalLightingSource>> lights;
    private readonly Func<MapSeasonDirectionalLightSourceSet,MapDirectionalLightProviderOwner>? providers;
    private readonly MapSourceCreatedF103CurrentFrameFactory factory;
    private readonly DeferredF103WorldPassSource associationSource;
    private readonly MainMapContextCurrentSource contextOwner;
    private readonly LoadedMapFreshCommonSource commonSource;
    private readonly LoadedDeferredEarlyShadowDirectionAuxSource shadowDirectionAux;
    private readonly DeferredEarlyShadowContextConstructorFields shadowContextFields;
    private readonly LoadedDeferredEarlyShadowForceFitSource shadowForceFitSource;
    private readonly LoadedDeferredEarlyShadowBiasSource shadowBiasSource;
    private DeferredEarlyShadowCurrentBiasCache? compactShadowBias;
    private readonly DeferredEarlyShadowOwnedViewMaskSource shadowViewMasks;
    private readonly SceneViewNativeMapShadowPopulationSourceOwner shadowPopulation;
    private readonly uint shadowNativeClipProfile;
    private readonly MapModelNativeFrustumCullingSource armyCommonView;
    private readonly NativeArmyWorldCpuPublicationOwner armyCpu = new();
    private readonly MainMapCameraModeOwner mainMode;
    private readonly MapMainCameraPostSelectionOwner mainPostSelection;
    private readonly MapGameLightQualityOwner graphicsQuality;
    private readonly SceneViewNativeMapSourceFields scalars;
    private readonly MapSeasonMainCameraPhaseSource phase;
    private readonly SkyWeatherNativePowerSource weather;
    private readonly NativeTerrainCurrentViewOwner terrainOwner;
    private readonly TerrainViewPublicationSource terrainSource;
    private readonly SceneViewNativeMapNode51ExecutionOwner node51,node56,bootstrapNode51;
    private readonly MainCameraView19B8Owner header;
    private readonly OwnedWorldLists worldLists;
    private readonly LoadedEnvironmentWorldLightPublicationSource environment;
    private SceneViewNativeMapSeasonResourceBundle seasonResources;
    private readonly Dictionary<uint,OwnedWorldData> data = new();
    private readonly ConditionalWeakTable<NativeMapFrameContext,CurrentFrame> current = new();
    private readonly DeferredEarlySourceViewQueriesOwner early;
    private readonly string generation;
    private long listFrame;
    private SkySeasonMainCameraRgbFrame? previousRgb;
    public MapLightingBinding Binding { get; }
    public CurrentFrame Bootstrap { get; }
    public DeferredEarlySourceViewQueriesPacket BootstrapEarly { get; }
    public MainSceneNode51Execution BootstrapNode51 { get; }
    public SceneViewNativeMapSeasonResourceBundle SeasonResources => seasonResources;
    public IReadOnlyList<NativeMapStageTiming> LastCurrentPreparationTimings { get; private set; } = [];

    /// <summary>Install already loaded CPU declarations after the caller has
    /// drained the existing session gate. This changes no Main/App, previous
    /// RGB, population, Context, clock or GPU owner and grants no ready result.
    /// Actual light providers must supply this bundle's constructors.</summary>
    public void InstallSeasonResources(SceneViewNativeMapSeasonResourceBundle resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.Locale != loaded.InitialFrame.MainCamera.Camera.ExecutableLocale)
            throw new InvalidDataException("Loaded season declarations must use this Main session's executable locale.");
        seasonResources = resources;
    }

    public sealed record CurrentFrame(MapSourceCreatedWorldF103Frames World,
        MapSourceCreatedF103PopulationPublicationInputs Publication,
        SkySeasonMainCameraRgbFrame Rgb,MapSourceCreatedSeasonLightingFrame Present,
        IReadOnlyList<TerrainCurrentKind1WorldPQPublication> Queue,
        MapWorldKind1CurrentQueue CountedQueue,
        NativeTerrainCurrentViewFrame? Terrain,MainSceneNode51Execution? Node51,
        MapSourceCreatedStaticPrimitiveAdmissionPlan Admission,
        MainCameraView19B8Projection Camera19B8,
        ArmyWorldPrimitivePublication ArmyPublication,
        ArmyCurrentWorldVisibilityView ArmyVisibilityView)
    {
        public MapSourceCreatedF103CurrentFrame First => World.Passes[0].Frame;
        public MapSourceCreatedWorldF103Frames GeometryWorld { get; init; } = World;
        public MapSourceCreatedF103CurrentFrame GeometryFirst => GeometryWorld.Passes[0].Frame;
        public NativeMapArmyPreparedFrame? ActualArmyPreparedFrame { get; init; }
        private MapCameraConstructionPacket? preparedMainCamera;
        /// <summary>The actual same-request season writer's Camera. Geometry
        /// visibility keeps its preceding snapshot; later effects borrow this
        /// prepared prefix and its typed Main scalar publication.</summary>
        public MapCameraConstructionPacket PreparedMainCamera => preparedMainCamera ??=
            GeometryFirst.Lighting.CapturePreparedCameraFromSameAcceptedMain(GeometryFirst.Camera);
        private DeferredEarlyIndirectPatternCameraTail? earlyPatternCameraTail;
        public DeferredEarlyIndirectPatternCameraTail EarlyPatternCameraTail => earlyPatternCameraTail ??=
            DeferredEarlyIndirectPatternCameraSource.CaptureCurrent(GeometryFirst,Rgb.Inputs,PreparedMainCamera);
        private DeferredEarlyShadowCurrentMainFields? preparedShadowMain;
        public DeferredEarlyShadowCurrentMainFields PreparedShadowMain => preparedShadowMain ??=
            DeferredEarlyShadowCurrentMainSource.CaptureActualMainPreparation(Rgb.Inputs,PreparedMainCamera);
        public DeferredEarlyShadowPreparationArguments? ShadowPreparationArguments { get; init; }
        public DeferredEarlyShadowCurrentDirectionFields? PreparedShadowDirection { get; init; }
        public DeferredEarlyShadowCurrentOwnCameraFields? PreparedShadowOwnCamera { get; init; }
        public DeferredEarlyShadowCurrentCompactFields? PreparedCompactShadow { get; init; }
        public DeferredEarlyShadowCurrentWorkVolumeFields? PreparedShadowWorkVolume { get; init; }
        public DeferredEarlyShadowRecordNumericFields? ShadowRecordNumericFields { get; init; }
        public DeferredEarlyShadowCurrentCompactResetFields? PreparedCompactReset { get; init; }
        public DeferredEarlyShadowCurrentForceFitFields? PreparedShadowForceFit { get; init; }
        public DeferredEarlyShadowCurrentBiasCache? PreparedShadowBiasCache { get; init; }
        public DeferredEarlyShadowCurrentWorkPublication? ShadowWorkPublication { get; init; }
        public DeferredEarlyShadowCurrentCompactVolumeFields? PreparedCompactVolume { get; init; }
        public DeferredEarlyShadowCurrentCasterFields? PreparedShadowCaster { get; init; }
        public DeferredEarlyShadowCurrentViewMaskFields? PreparedShadowViewMasks { get; init; }
        public DeferredEarlyShadowCurrentCasterLightRows? PreparedShadowLightRows { get; init; }
        public DeferredEarlyShadowCurrentCounterCameraFields? PreparedShadowCounterCamera { get; init; }
        public DeferredEarlyShadowCurrentViewCounterCapacities? PreparedShadowCounterCapacities { get; init; }
        public DeferredEarlyShadowCompleteModelCandidates? PreparedShadowStaticModels { get; init; }
        public DeferredEarlyShadowCompleteModelCandidates? PreparedShadowArmyModels { get; init; }
        public SceneViewNativeMapShadowModelAdmissions? PreparedShadowStaticAdmissions { get; init; }
        public SceneViewNativeMapShadowModelAdmissions? PreparedShadowArmyAdmissions { get; init; }
    }

    public SceneViewNativeMapWorldF103SourceOwner(SceneViewNativeMapLoadInputs loaded,
        SceneViewNativeMapCpuSourceOwner cpu,
        Func<MapSeasonDirectionalLightSourceSet,IReadOnlyList<MapSourceCreatedDirectionalLightingSource>> actualLightSources,
        LoadedDeferredTexturePlan plan,MapLightingBinding binding,string generation,
        MainMapCameraModeOwner mainMode,
        Func<MapSeasonDirectionalLightSourceSet,MapDirectionalLightProviderOwner>? actualProviders=null,
        IReadOnlyList<NativeArmyWorldLogicalActor>? actualBootstrapArmyInventory=null)
    {
        ArgumentNullException.ThrowIfNull(actualLightSources);
        this.loaded=loaded;this.cpu=cpu;lights=actualLightSources;Binding=binding;this.generation=generation;this.mainMode=mainMode;
        providers=actualProviders;
        var repo=loaded.Repository;string locale=loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        graphicsQuality=MapGameLightQualityOwner.CreateFreshPrimary(repo,locale,generation+"/Configs");
        mainPostSelection=MapMainCameraPostSelectionOwner.CreateFresh(repo,mainMode);
        factory=new(plan,MapSourceCreatedF103ColorScope.FreshSourceNamedModulesWithoutHeaderEdits);
        associationSource=DeferredF103WorldPassAssociations.ReadNode166(plan.Graph);
        contextOwner=MainMapContextCurrentSource.CreateFresh(repo,locale,generation+"/Host");
        var commonObjects=repo.LoadObjects("KIDSSystemResource",0x6D011726);
        commonSource=MapFreshCommonResources.ReadSourceTemplate(commonObjects);
        shadowDirectionAux=DeferredEarlyShadowDirectionSource.ReadSourceCreatedTemplate(commonObjects);
        armyCommonView=MapModelNativeVisibilitySources.ReadCommonViewTemplate(commonObjects,0x899F101B);
        scalars=SceneViewNativeMapSourceFields.CreateFreshRetainedMain(loaded.InitialFrame.MainCamera.Camera);
        phase=MapSeasonMainCameraPhaseSource.LoadSourceDefaults(repo,locale);
        weather=SkyWeatherNativePowerSource.Load(repo,locale);
        terrainOwner=NativeTerrainCurrentViewOwner.CreateFreshFieldSceneNoExternalAssignments(loaded.TerrainMaterial);
        terrainSource=TerrainViewPublicationSource.ReadFieldScene(loaded.TerrainMaterial.Parameters);
        node51=SceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/node51");
        node56=SceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/node56",56);
        bootstrapNode51=SceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/bootstrap-node51");
        var defaultView=DefaultView0Source.LoadDeclarationDefaults(repo);
        header=MainCameraView19B8Owner.ConstructFresh(defaultView,binding.CameraOwnerIdentity);
        shadowContextFields=DeferredEarlyShadowPreparationArgumentsSource.Read(defaultView).ConstructorFields;
        shadowForceFitSource=LoadedDeferredEarlyShadowForceFitSource.Load(repo,locale);
        shadowBiasSource=LoadedDeferredEarlyShadowBiasSource.Load(repo,locale);
        shadowViewMasks=new(defaultView);
        shadowPopulation=new(cpu);
        // Same source-created renderer profile used by the normal Main Camera.
        // Import once; ordinary requests never reread the source image.
        shadowNativeClipProfile=MainMapCameraStartupSource.Load(repo,locale).StaticProjectionProfile;
        worldLists=new(OwnedWorldListSource.Load(repo),1);listFrame=1;
        environment=EnvironmentWorldLightPublicationSource.Load(repo);
        var season=loaded.InitialFrame.World.NativeMapSeason??throw new InvalidDataException("World source season is absent.");
        seasonResources=SceneViewNativeMapSeasonResourceBundle.Load(repo,season,locale,1);
        var boot=loaded.InitialFrame with {MainCamera=loaded.InitialFrame.MainCamera with {
            Camera=loaded.InitialFrame.MainCamera.Camera.CopyAcceptedSnapshotForOwnedFrame(1,generation+"/bootstrap-optics")}};
        if (actualBootstrapArmyInventory is null)
        {
            if (boot.World.Units.Count != 0)
                throw new InvalidDataException("Nonempty World bootstrap needs its actual source-owned ordinary/linked-flag logical inventory.");
            actualBootstrapArmyInventory=Array.Empty<NativeArmyWorldLogicalActor>();
        }
        if ((boot.World.Units.Count == 0) != (actualBootstrapArmyInventory.Count == 0))
            throw new InvalidDataException("World bootstrap Army inventory disagrees with its actual captured unit population.");
        Bootstrap=Prepare(boot,null,0,null,actualBootstrapArmyInventory);
        BootstrapNode51=bootstrapNode51.PublishAndPrepareCurrent(1,Binding);
        early=DeferredEarlySourceViewQueriesOwner.CreateFromFreshSourceGroupPools(Bootstrap.World.View,generation,0,
            generation+"/fresh-group-pools",generation+"/actual-owned-world-construction");
        BootstrapEarly=early.PrepareCurrent(Bootstrap.World.View,generation+"/bootstrap-query");
    }

    public CurrentFrame PrepareCurrent(NativeMapFrameContext context,SceneViewNativeMapFrameCapture capture,
        NativeMapArmyPreparedFrame army,float delta)
    {
        if(!ReferenceEquals(army.Context,context)||army.CpuPrimitivePopulation is not { } actual ||
            !ReferenceEquals(actual.Prepared,army)||army.WorldPeriodNumber!=capture.World.PeriodNumber||army.Unresolved.Count!=0)
            throw new InvalidDataException("World source assembly needs its actual complete same-frame Army BeforeFrame inventory.");
        var frame=Prepare(capture,context,delta,army,null);current.Add(context,frame);
        early.ApplySourceBankReset(frame.World.View,generation+"/actual-view-reset/"+context.FrameNumber);
        return frame;
    }

    private CurrentFrame Prepare(SceneViewNativeMapFrameCapture capture,NativeMapFrameContext? context,float delta,
        NativeMapArmyPreparedFrame? actualArmy, IReadOnlyList<NativeArmyWorldLogicalActor>? bootstrapArmyInventory)
    {
        if(!float.IsFinite(delta)||delta<0||capture.TerrainBatches.Count==0||capture.TerrainBatches.Any(b=>b.Patches.Count==0)||
            capture.World.NativeMapSeason is not { } season)
            throw new InvalidDataException("World source assembly needs its nonempty accepted terrain and owned season.");
        // Capture one loaded declaration bundle for all consumers of this
        // attempt. Installation belongs between drained session requests.
        var resources=seasonResources;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var timings=new List<NativeMapStageTiming>();double previous=0;
        void Mark(string stage)
        {
            double elapsed=timer.Elapsed.TotalMilliseconds;
            timings.Add(new(stage,elapsed-previous,elapsed));previous=elapsed;
        }
        var population=cpu.PrepareCurrent(capture);long number=population.FrameNumber;
        Mark("current-static-population");
        var mode=mainMode.PrepareCurrent(capture.MainCamera);mode.ValidateCurrent(mainMode,capture.MainCamera);
        var selectedSeason=resources.CameraSource.WithCalendar(MapSeasonRuntime.PrepareFrame(season,number));
        var post=mainPostSelection.PrepareCurrent(selectedSeason,mode,graphicsQuality);
        var before=SkySeasonMainCameraProducer.PrepareBeforeFinalPreparation(selectedSeason,capture.MainCamera,mode.Mode288,phase,
            previousAcceptedRgbFrame:previousRgb,mainPostSelection:post);
        var rgb=SkySeasonMainCameraProducer.PrepareSunMoonAndSky(before,delta,NativeCameraTangentDispatch.Sse,
            SkySceneSourceDefaults.CreateControllerConstructor(),SkySceneSourceDefaults.CreateSky2Parameters(107));
        var fog=SkySeasonMainWeatherFog.PrepareSourceDatabaseResetFrame(rgb,weather,NativeCameraTangentDispatch.Sse);
        var present=MapSourceCreatedSeasonLightingFrame.CapturePreparedSeason(rgb,fog);
        Mark("current-main-mode-season-rgb-fog");
        var declarations=resources.DirectionalDeclarations;
        var metadata=resources.LightMetadata;
        var sources=lights(declarations).ToArray();
        resources.RequireCurrentLightSources(sources);
        string Record(int index)=>generation+"/World-record/"+index;
        string Light(int index)=>index==2?Binding.LightOwnerIdentity:generation+"/Directional-data/"+index;
        var common=MapFreshCommonResources.PrepareSourceCreatedBeforeAmbientPublication(commonSource,number,
            generation+"/source-Common-settings",generation+"/Common",Binding.ViewIdentity,generation+"/properties-before-ambient");
        var provider=providers?.Invoke(declarations);
        var caller=provider is null?null:DirectionalLightProviderCaller.CaptureFreshOwnedCaller(loaded.Repository,
            before.MainFrame,common,DirectionalProviderCallerScope.FreshOwnedCameraClearedLateControlsNoLaterSelectorWriter);
        var packets=sources.Select((s,i)=>
        {
            var input=new MapSourceCreatedLightingFrameInputs(number,Binding with {LightOwnerIdentity=Light(i)},present.Parameters,
            present.PriorPreparedCameraPrefix,contextOwner.Host.ContextFlags3E4,delta,MapLightingRuntimeTable.Present,
            MapLightingCameraScope.LightingPrefix,scalars.PrepareBuiltinAmbientCameraRate(capture.MainCamera.Camera),true,new(),
            PresentSeasonCamera:present);
            return provider is null?s.PrepareFrame(input):s.PrepareFrame(input,
                provider.PrepareFreshCallerFrame(s.Construction,caller!,before.MainFrame,present.PreparedCameraPrefix.Span));
        }).ToArray();
        var primary=packets[2];
        Mark("current-common-five-light-packets");
        if(primary.SourceIdentity.DeclaredGroupMask!=0x80)throw new InvalidDataException("Actual primary World root is not group80.");
        if(number<listFrame)throw new InvalidDataException("World source list cannot move backward.");
        if(number>listFrame){worldLists.FrameSync(listFrame,number);listFrame=number;}
        else worldLists.Clear(number,EnvironmentWorldLightPublicationSource.WorldLightOutputObjectId);
        foreach(var source in sources)if(!data.ContainsKey(source.Construction.SourceObjectId))
            data.Add(source.Construction.SourceObjectId,worldLists.CreateData(OwnedWorldCpuState.Present));
        if(!MapSeasonWorldLightRegistration.Append(environment,declarations,worldLists,number,
            sources.Select(s=>new MapSeasonWorldLightDataBinding(s.Construction.SourceObjectId,data[s.Construction.SourceObjectId])).ToArray()))
            throw new InvalidDataException("Actual Environment world-light append failed.");
        var registered=worldLists.Snapshot(number,EnvironmentWorldLightPublicationSource.WorldLightOutputObjectId);
        if(!registered.Entries.Select(e=>e?.DataIdentity).SequenceEqual(sources.Select(s=>(long?)data[s.Construction.SourceObjectId].Identity)))
            throw new InvalidDataException("Actual Environment registered a different ordered root list.");
        var counted=MapSourceCreatedWorldKind1Publication.PrepareSourceRetainedQueue(number,Binding,generation+"/World-queue/"+number,
            declarations,metadata.Select((m,i)=>new MapWorldKind1CurrentRecordInput(number,Binding.ViewIdentity,m,i,Record(i),
                m.DeclaredGroupMask,m.MaxPrimitiveNum,packets[i].LocalLightData.Span[0xF20],true,true,true,
                environment.SourceProvenance+";actual-constructed-world-record="+Record(i))).ToArray());
        var queue=counted.Records.Select((r,i)=>new TerrainCurrentKind1WorldPQPublication(number,Binding.ViewIdentity,
            counted.QueueOwnerIdentity,i,0x10000000,generation+"/World-pair/"+number+"/"+i,Record(i),r.Mask78,true,r.ProducerProvenance)).ToArray();
        Mark("current-world-list-and-record-counts");
        var armyView=ArmyCurrentWorldVisibilityView.PrepareActualNullCustomSelection(number,Binding.ViewIdentity,
            armyCommonView,false,null,null,generation+"/source-owned-retained-null-View1C08/"+number);
        var actors=actualArmy is null ? bootstrapArmyInventory ??
            throw new InvalidDataException("Bootstrap Army logical source inventory is absent.") :
            armyCpu.CaptureActualLogicalInventory(capture.World,actualArmy.Ordinary,actualArmy.Models,actualArmy.Flags);
        var armyCamera=actualArmy is null ? new DeferredCameraInputs(capture.MainCamera.Camera.SourceWorldToView,
            capture.MainCamera.Camera.SourceWorldToProjection,new Vector4(capture.MainCamera.Camera.CurrentPose.Position80,1)) :
            actualArmy.Context.PreparedCamera ?? throw new InvalidDataException("Army visibility needs its actual current geometry Camera.");
        if (armyCamera.SourceWorldToView!=capture.MainCamera.Camera.SourceWorldToView ||
            armyCamera.SourceWorldToProjection!=capture.MainCamera.Camera.SourceWorldToProjection)
            throw new InvalidDataException("World static and Army visibility use different current geometry Cameras.");
        var armyPublication=armyCpu.PrepareLogicalCurrent(actors,armyCamera,counted,common,armyView,
            generation+"/actual-complete-Army-current-World/"+number);
        if (actualArmy is not null && (armyPublication.Primitives.Count!=actualArmy.CpuPrimitivePopulation!.CompleteSourcePrimitiveCount ||
            !armyPublication.Actors.Select(a=>(a.Family,a.ActorIdentity,a.Source.Identity)).ToHashSet().SetEquals(
                actualArmy.CpuPrimitivePopulation.Actors.Select(a=>(a.Family,a.Actor,a.Source.Identity)))))
            throw new InvalidDataException("World Army publication differs from its actual BeforeFrame canonical source inventory.");
        Mark("current-army-visibility-and-record-counts");
        var tail=MapSourceCreatedViewFields.ReadFreshRetainedCameraFields(capture.MainCamera.Camera);
        if(tail.ViewSignedA00 is <1 or >32||common.CurrentViewAux3F2 is not byte sky||sky is <1 or >32)
            throw new NotSupportedException("Current World needs its actual shadow and Sky selectors.");
        uint shadowMask=1u<<(tail.ViewSignedA00-1),skyMask=1u<<(sky-1);
        var shadow=queue.FirstOrDefault(r=>(r.CurrentWorldPQWord78&shadowMask)!=0)??throw new InvalidDataException("Actual shadow root is absent.");
        var skyRecord=queue.FirstOrDefault(r=>(r.CurrentWorldPQWord78&~skyMask)==0)??throw new InvalidDataException("Actual Sky root is absent.");
        if(shadow.CurrentItemIndex!=2||skyRecord.CurrentItemIndex!=2)
            throw new NotSupportedException("This initial World branch requires independently selected root80 shadow and Sky payloads.");
        uint mask1BF8=uint.MaxValue;
        for(int i=0;i<queue.Length;i++)if((packets[i].LocalLightData.Span[0xF1C]&15)!=0)mask1BF8&=queue[i].CurrentWorldPQWord78;
        var payload=SkySceneSelectedViewLightSource.Capture(primary.LocalLightData.Span,
            new(number,Binding.ViewIdentity,shadow.CurrentWorldPQOwnerIdentity,Binding.LightOwnerIdentity));
        var selected=TerrainSelectedKind1Record.FromOwnedCurrentPublication(primary,shadow,payload,mask1BF8,
            generation+"/actual-A00-shadow/independent-Common-Sky-selection");
        var publication=new MapSourceCreatedViewPublicationState(number,Binding.ViewIdentity,Binding.CameraOwnerIdentity,
            generation+"/View/"+number,false,0,[0,0,0,0],true,null){OtherAdditionalOwners1B80_1BB0_1B98Absent=true,SelectedShadowRecord1BD0=selected};
        NativeTerrainCurrentViewFrame? terrain=null;
        if(context is not null){terrain=terrainOwner.PrepareWithSelectedKind1Record(context,primary,publication,selected,common);publication=terrain.CurrentViewPublication;}
        else publication=publication with {Terrain1BB8=TerrainCurrentViewPublication.PrepareWithSelectedKind1Record(terrainSource,primary,
            capture.TerrainBatches.SelectMany(b=>b.Patches).ToArray(),selected,common,generation+"/bootstrap-terrain-CPU")};
        Mark("current-view-and-terrain-publication");
        var inputs=new MapSourceCreatedF103PopulationPublicationInputs(true,true,publication,
            new Dictionary<string,MapSourceCreatedModelExclusionOverride>(),false,null,generation+"/actual-static-and-complete-Army/"+number);
        var q=contextOwner.BeginDeferredPreparation(number,generation+"/execution/"+number);
        var world=MapSourceCreatedWorldF103Frames.Prepare(factory,associationSource,population,counted,primary,
            packets.Select((p,i)=>(p,i)).ToDictionary(x=>Record(x.i),x=>x.p),common,q,inputs,
            armyPublication.CompleteCountsByRecordOwner,null,true,generation+"/World-F103/"+number);
        if(world.Passes.Count==0)throw new NotSupportedException("Actual current World has no admitted source F103 pass.");
        Mark("current-world-f103-dispatch");
        var admission=MapSourceCreatedStaticPrimitiveAdmissionPlan.PrepareFreshWorld(world.Passes[0].Frame,inputs,counted,
            armyPublication.CompleteCountsByRecordOwner);
        var execution=context is null?null:node51.PublishAndPrepareCurrent(number,Binding);
        var result=new CurrentFrame(world,inputs,rgb,present,queue,counted,terrain,execution,admission,
            header.ProjectCurrentMain0(capture.MainCamera.Camera,generation+"/actual-View19B8/"+number),armyPublication,armyView)
            {ActualArmyPreparedFrame=actualArmy};
        var shadowMain=result.PreparedShadowMain;
        var shadowArguments=shadowMain.SelectCurrentPreparationArguments(shadowContextFields);
        result=result with {ShadowPreparationArguments=shadowArguments,
            PreparedShadowDirection=DeferredEarlyShadowDirectionSource.PrepareCurrent(
                shadowMain,selected,result.GeometryFirst.Lighting,common,shadowDirectionAux,shadowArguments)};
        var ownCamera=DeferredEarlyShadowOwnCameraSource.PrepareCurrent(
            shadowMain,result.PreparedShadowDirection!,shadowNativeClipProfile,NativeCameraTangentDispatch.Sse);
        result=result with {PreparedShadowOwnCamera=ownCamera,
            PreparedShadowWorkVolume=DeferredEarlyShadowWorkVolumeSource.PrepareCurrent(
                ownCamera,NativeCameraTangentDispatch.Sse),
            PreparedCompactShadow=shadowMain.Values.ModeA3C is 0 or 9 or 10 or 11
                ? DeferredEarlyShadowCompactSource.PrepareCurrent(ownCamera) : null};
        if(result.PreparedCompactShadow is { } compact)
        {
            // This generation retains the source-created pool6C8 controller.
            // Per-request compact numeric packets do not reconstruct its
            // CPU138 parameter cache or overwrite a later sbias setter.
            compactShadowBias??=shadowBiasSource.ConstructFreshOwnedCache(compact);
            var forceFit=shadowForceFitSource.PrepareFreshCurrent(compact);
            var reset=DeferredEarlyShadowCompactResetSource.PrepareCurrent(compact);
            var sourceRecord=counted.Records.Single(record=>record.RecordOwnerIdentity==selected.RecordOwnerIdentity);
            var recordFields=world.BorrowAcceptedRecordLighting(sourceRecord)
                .CaptureRetainedDirectionalRecordNumericFields(sourceRecord);
            var work=DeferredEarlyShadowWorkPublicationSource.PrepareCurrent(result.PreparedShadowWorkVolume!,recordFields,reset);
            var volume=work is null?null:DeferredEarlyShadowCompactVolumeSource.PrepareCurrent(
                compact,work,NativeCameraTangentDispatch.Sse);
            result=result with {ShadowRecordNumericFields=recordFields,PreparedCompactReset=reset,PreparedShadowForceFit=forceFit,
                PreparedShadowBiasCache=compactShadowBias,
                ShadowWorkPublication=work,PreparedCompactVolume=volume,
                PreparedShadowCaster=volume is null?null:DeferredEarlyShadowCasterSource.PrepareCurrent(volume)};
            shadowViewMasks.BeginCurrent(number,Binding.ViewIdentity);
            shadowViewMasks.PublishCurrentDefaultView0(shadowMain);
            var lightRows=DeferredEarlyShadowCasterAdmissionSource.CaptureFreshSourceRetainedKind1Rows(
                counted,world.BorrowAcceptedRecordLighting);
            // The imported counter fields currently cover direct V468. A real
            // interpolated camera keeps only those capacities with closed inputs.
            var counterCamera=shadowMain.ActualMainInputs.Source.Selection.SecondPackage is null
                ? DeferredEarlyShadowViewCounterCameraSource.CaptureCurrentMain(shadowMain) : null;
            result=result with {PreparedShadowViewMasks=shadowViewMasks.CaptureCurrentWorldCollectionCaller(),
                PreparedShadowLightRows=lightRows,PreparedShadowCounterCamera=counterCamera,
                PreparedShadowCounterCapacities=DeferredEarlyShadowViewCounterSource.PrepareCurrent(lightRows,
                    result.GeometryFirst.ViewFields.CurrentPublication.SelectedShadowRecord1BD0 is not null,
                    counterCamera?.PreparedSignedA98,counterCamera?.PreparedSigned106C),
                PreparedShadowStaticModels=shadowPopulation.PrepareStatic(population,Binding.ViewIdentity),
                PreparedShadowArmyModels=shadowPopulation.PrepareArmy(armyPublication)};
            bool hasSelectedShadow=result.GeometryFirst.ViewFields.CurrentPublication.SelectedShadowRecord1BD0 is not null;
            result=result with {
                PreparedShadowStaticAdmissions=SceneViewNativeMapShadowPopulationSourceOwner.PrepareAdmissions(
                    result.PreparedShadowStaticModels!,result.PreparedShadowViewMasks!,lightRows,result.GeometryFirst.Common,hasSelectedShadow),
                PreparedShadowArmyAdmissions=SceneViewNativeMapShadowPopulationSourceOwner.PrepareAdmissions(
                    result.PreparedShadowArmyModels!,result.PreparedShadowViewMasks!,lightRows,result.GeometryFirst.Common,hasSelectedShadow)};
        }
        Mark("current-primitive-admission-general-scene-camera-header");
        LastCurrentPreparationTimings=timings.AsReadOnly();
        return result;
    }

    public CurrentFrame RequireCurrent(NativeMapFrameContext context)=>current.TryGetValue(context,out var frame)?frame:
        throw new InvalidDataException("Actual current World preparation is absent.");
    public MapStaticModelCurrentOwnedFrame RequirePopulation(NativeMapFrameContext context)=>RequireCurrent(context).First.CpuPopulation!;
    public MapSourceCreatedStaticPrimitiveAdmissionPlan RequireAdmission(NativeMapFrameContext context)=>RequireCurrent(context).Admission;
    /// <summary>Called by the real57 parameter adapter at its OR40 writer.
    /// This updates current shading selections, not geometry draw completion.</summary>
    public CurrentFrame ApplyExecutedSpecializedGeometryFlagWriter(NativeMapFrameContext context,
        uint actualContextFlags3F0, string actualWriterProvenance)
    {
        var before = RequireCurrent(context);
        var q = contextOwner.ApplyExecutedSpecializedGeometryFlagWriter(before.First.Context, actualWriterProvenance);
        if (actualContextFlags3F0 != q.ContextFlags3F0)
            throw new InvalidDataException("Actual specialized geometry writer differs from its source OR40 result.");
        var world = before.World.PrepareForCurrentContext(factory, q, generation + "/post-geometry-F103/" + context.FrameNumber);
        var after = before with { World = world };
        current.Remove(context); current.Add(context, after);
        return after;
    }
    public DeferredEarlySourceViewQueriesPacket PrepareEarly(MapSourceCreatedNativeSessionPreparedFrame accepted)=>
        early.PrepareCurrent(RequireCurrent(accepted.Context).World.View,generation+"/current-early-query");
    public MainMapContextCurrentPacket PrepareAuxiliaryContext(MapSourceCreatedNativeSessionPreparedFrame accepted)
    {var f=RequireCurrent(accepted.Context).First;return contextOwner.ApplyExecutedThinSurfaceFlagWriter(f.Context,
        checked((int)f.ViewFields.RainDropFlags19B4),generation+"/thin-surface",f.ViewFields.SourceProvenance);}
    public NativeTerrainStencilPreparedFrame PrepareTerrainStencil(MapSourceCreatedNativeSessionPreparedFrame accepted,
        NativeTerrainStencilCurrentOwner stencil,TerrainGbufferRasterState raster)
    {
        var f=RequireCurrent(accepted.Context);node51.ValidateCurrent(f.Node51!);
        var terrainExecution=node56.PublishAndPrepareCurrent(accepted.Context.FrameNumber,f.First.Lighting.Binding);
        var eligible=f.Queue.Where(r=>(terrainSource.Exclusion2B8&r.CurrentWorldPQWord78)==0).ToArray();
        if(eligible.Length!=1)throw new NotSupportedException("Actual World cannot provide one ordinary Field record.");
        return stencil.PrepareFromGeneralScene(accepted.Context,f.First,f.Terrain!,eligible[0],terrainExecution,raster);
    }
    public void CommitCompleteFrame(NativeMapFrameContext context)=>previousRgb=RequireCurrent(context).Rgb;
}
