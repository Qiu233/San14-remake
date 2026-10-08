using System.Runtime.CompilerServices;
using System.Numerics;
using San14.Data;

/// <summary>Actual SceneView population, Environment five-root publication,
/// Main seasonal Camera, selected shadow record and ordered World F103 join.
/// Resource creation and provider selection belong to the supplied light owners.</summary>
public sealed class OriginalSceneViewNativeMapWorldF103SourceOwner
{
    private readonly OriginalSceneViewNativeMapLoadInputs loaded;
    private readonly OriginalSceneViewNativeMapCpuSourceOwner cpu;
    private readonly Func<OriginalMapSeasonDirectionalLightSourceSet,IReadOnlyList<OriginalMapSourceCreatedDirectionalLightingSource>> lights;
    private readonly Func<OriginalMapSeasonDirectionalLightSourceSet,OriginalMapDirectionalLightProviderOwner>? providers;
    private readonly OriginalMapSourceCreatedF103CurrentFrameFactory factory;
    private readonly OriginalDeferredF103WorldPassSource associationSource;
    private readonly OriginalMainMapContextCurrentSource contextOwner;
    private readonly LoadedOriginalMapFreshCommonSource commonSource;
    private readonly LoadedOriginalDeferredEarlyShadowDirectionAuxSource shadowDirectionAux;
    private readonly OriginalDeferredEarlyShadowContextConstructorFields shadowContextFields;
    private readonly LoadedOriginalDeferredEarlyShadowForceFitSource shadowForceFitSource;
    private readonly LoadedOriginalDeferredEarlyShadowBiasSource shadowBiasSource;
    private OriginalDeferredEarlyShadowCurrentBiasCache? compactShadowBias;
    private readonly OriginalDeferredEarlyShadowOwnedViewMaskSource shadowViewMasks;
    private readonly OriginalSceneViewNativeMapShadowPopulationSourceOwner shadowPopulation;
    private readonly uint shadowNativeClipProfile;
    private readonly OriginalMapModelNativeFrustumCullingSource armyCommonView;
    private readonly OriginalNativeArmyWorldCpuPublicationOwner armyCpu = new();
    private readonly OriginalMainMapCameraModeOwner mainMode;
    private readonly OriginalMapMainCameraPostSelectionOwner mainPostSelection;
    private readonly OriginalMapGameLightQualityOwner graphicsQuality;
    private readonly OriginalSceneViewNativeMapSourceFields scalars;
    private readonly OriginalMapSeasonMainCameraPhaseSource phase;
    private readonly OriginalSkyWeatherNativePowerSource weather;
    private readonly OriginalNativeTerrainCurrentViewOwner terrainOwner;
    private readonly OriginalTerrainViewPublicationSource terrainSource;
    private readonly OriginalSceneViewNativeMapNode51ExecutionOwner node51,node56,bootstrapNode51;
    private readonly OriginalMainCameraView19B8Owner header;
    private readonly OriginalOwnedWorldLists worldLists;
    private readonly LoadedOriginalEnvironmentWorldLightPublicationSource environment;
    private OriginalSceneViewNativeMapSeasonResourceBundle seasonResources;
    private readonly Dictionary<uint,OriginalOwnedWorldData> data = new();
    private readonly ConditionalWeakTable<OriginalNativeMapFrameContext,CurrentFrame> current = new();
    private readonly OriginalDeferredEarlySourceViewQueriesOwner early;
    private readonly string generation;
    private long listFrame;
    private OriginalSkySeasonMainCameraRgbFrame? previousRgb;
    public OriginalMapLightingBinding Binding { get; }
    public CurrentFrame Bootstrap { get; }
    public OriginalDeferredEarlySourceViewQueriesPacket BootstrapEarly { get; }
    public OriginalMainSceneNode51Execution BootstrapNode51 { get; }
    public OriginalSceneViewNativeMapSeasonResourceBundle SeasonResources => seasonResources;
    public IReadOnlyList<OriginalNativeMapStageTiming> LastCurrentPreparationTimings { get; private set; } = [];

    /// <summary>Install already loaded CPU declarations after the caller has
    /// drained the existing session gate. This changes no Main/App, previous
    /// RGB, population, Context, clock or GPU owner and grants no ready result.
    /// Actual light providers must supply this bundle's constructors.</summary>
    public void InstallSeasonResources(OriginalSceneViewNativeMapSeasonResourceBundle resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.Locale != loaded.InitialFrame.MainCamera.Camera.ExecutableLocale)
            throw new InvalidDataException("Loaded season declarations must use this Main session's executable locale.");
        seasonResources = resources;
    }

    public sealed record CurrentFrame(OriginalMapSourceCreatedWorldF103Frames World,
        OriginalMapSourceCreatedF103PopulationPublicationInputs Publication,
        OriginalSkySeasonMainCameraRgbFrame Rgb,OriginalMapSourceCreatedSeasonLightingFrame Present,
        IReadOnlyList<OriginalTerrainCurrentKind1WorldPQPublication> Queue,
        OriginalMapWorldKind1CurrentQueue CountedQueue,
        OriginalNativeTerrainCurrentViewFrame? Terrain,OriginalMainSceneNode51Execution? Node51,
        OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan Admission,
        OriginalMainCameraView19B8Projection Camera19B8,
        OriginalArmyWorldPrimitivePublication ArmyPublication,
        OriginalArmyCurrentWorldVisibilityView ArmyVisibilityView)
    {
        public OriginalMapSourceCreatedF103CurrentFrame First => World.Passes[0].Frame;
        public OriginalMapSourceCreatedWorldF103Frames GeometryWorld { get; init; } = World;
        public OriginalMapSourceCreatedF103CurrentFrame GeometryFirst => GeometryWorld.Passes[0].Frame;
        public OriginalNativeMapArmyPreparedFrame? ActualArmyPreparedFrame { get; init; }
        private OriginalMapCameraConstructionPacket? preparedMainCamera;
        /// <summary>The actual same-request season writer's Camera. Geometry
        /// visibility keeps its preceding snapshot; later effects borrow this
        /// prepared prefix and its typed Main scalar publication.</summary>
        public OriginalMapCameraConstructionPacket PreparedMainCamera => preparedMainCamera ??=
            GeometryFirst.Lighting.CapturePreparedCameraFromSameAcceptedMain(GeometryFirst.Camera);
        private OriginalDeferredEarlyIndirectPatternCameraTail? earlyPatternCameraTail;
        public OriginalDeferredEarlyIndirectPatternCameraTail EarlyPatternCameraTail => earlyPatternCameraTail ??=
            OriginalDeferredEarlyIndirectPatternCameraSource.CaptureCurrent(GeometryFirst,Rgb.Inputs,PreparedMainCamera);
        private OriginalDeferredEarlyShadowCurrentMainFields? preparedShadowMain;
        public OriginalDeferredEarlyShadowCurrentMainFields PreparedShadowMain => preparedShadowMain ??=
            OriginalDeferredEarlyShadowCurrentMainSource.CaptureActualMainPreparation(Rgb.Inputs,PreparedMainCamera);
        public OriginalDeferredEarlyShadowPreparationArguments? ShadowPreparationArguments { get; init; }
        public OriginalDeferredEarlyShadowCurrentDirectionFields? PreparedShadowDirection { get; init; }
        public OriginalDeferredEarlyShadowCurrentOwnCameraFields? PreparedShadowOwnCamera { get; init; }
        public OriginalDeferredEarlyShadowCurrentCompactFields? PreparedCompactShadow { get; init; }
        public OriginalDeferredEarlyShadowCurrentWorkVolumeFields? PreparedShadowWorkVolume { get; init; }
        public OriginalDeferredEarlyShadowRecordNumericFields? ShadowRecordNumericFields { get; init; }
        public OriginalDeferredEarlyShadowCurrentCompactResetFields? PreparedCompactReset { get; init; }
        public OriginalDeferredEarlyShadowCurrentForceFitFields? PreparedShadowForceFit { get; init; }
        public OriginalDeferredEarlyShadowCurrentBiasCache? PreparedShadowBiasCache { get; init; }
        public OriginalDeferredEarlyShadowCurrentWorkPublication? ShadowWorkPublication { get; init; }
        public OriginalDeferredEarlyShadowCurrentCompactVolumeFields? PreparedCompactVolume { get; init; }
        public OriginalDeferredEarlyShadowCurrentCasterFields? PreparedShadowCaster { get; init; }
        public OriginalDeferredEarlyShadowCurrentViewMaskFields? PreparedShadowViewMasks { get; init; }
        public OriginalDeferredEarlyShadowCurrentCasterLightRows? PreparedShadowLightRows { get; init; }
        public OriginalDeferredEarlyShadowCurrentCounterCameraFields? PreparedShadowCounterCamera { get; init; }
        public OriginalDeferredEarlyShadowCurrentViewCounterCapacities? PreparedShadowCounterCapacities { get; init; }
        public OriginalDeferredEarlyShadowCompleteModelCandidates? PreparedShadowStaticModels { get; init; }
        public OriginalDeferredEarlyShadowCompleteModelCandidates? PreparedShadowArmyModels { get; init; }
        public OriginalSceneViewNativeMapShadowModelAdmissions? PreparedShadowStaticAdmissions { get; init; }
        public OriginalSceneViewNativeMapShadowModelAdmissions? PreparedShadowArmyAdmissions { get; init; }
    }

    public OriginalSceneViewNativeMapWorldF103SourceOwner(OriginalSceneViewNativeMapLoadInputs loaded,
        OriginalSceneViewNativeMapCpuSourceOwner cpu,
        Func<OriginalMapSeasonDirectionalLightSourceSet,IReadOnlyList<OriginalMapSourceCreatedDirectionalLightingSource>> actualLightSources,
        LoadedOriginalDeferredTexturePlan plan,OriginalMapLightingBinding binding,string generation,
        OriginalMainMapCameraModeOwner mainMode,
        Func<OriginalMapSeasonDirectionalLightSourceSet,OriginalMapDirectionalLightProviderOwner>? actualProviders=null,
        IReadOnlyList<OriginalNativeArmyWorldLogicalActor>? actualBootstrapArmyInventory=null)
    {
        ArgumentNullException.ThrowIfNull(actualLightSources);
        this.loaded=loaded;this.cpu=cpu;lights=actualLightSources;Binding=binding;this.generation=generation;this.mainMode=mainMode;
        providers=actualProviders;
        var repo=loaded.Repository;string locale=loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        graphicsQuality=OriginalMapGameLightQualityOwner.CreateFreshPrimary(repo,locale,generation+"/Configs");
        mainPostSelection=OriginalMapMainCameraPostSelectionOwner.CreateFresh(repo,mainMode);
        factory=new(plan,OriginalMapSourceCreatedF103ColorScope.FreshSourceNamedModulesWithoutHeaderEdits);
        associationSource=OriginalDeferredF103WorldPassAssociations.ReadNode166(plan.Graph);
        contextOwner=OriginalMainMapContextCurrentSource.CreateFresh(repo,locale,generation+"/Host");
        var commonObjects=repo.LoadObjects("KIDSSystemResource",0x6D011726);
        commonSource=OriginalMapFreshCommonResources.ReadSourceTemplate(commonObjects);
        shadowDirectionAux=OriginalDeferredEarlyShadowDirectionSource.ReadSourceCreatedTemplate(commonObjects);
        armyCommonView=OriginalMapModelNativeVisibilitySources.ReadCommonViewTemplate(commonObjects,0x899F101B);
        scalars=OriginalSceneViewNativeMapSourceFields.CreateFreshRetainedMain(loaded.InitialFrame.MainCamera.Camera);
        phase=OriginalMapSeasonMainCameraPhaseSource.LoadSourceDefaults(repo,locale);
        weather=OriginalSkyWeatherNativePowerSource.Load(repo,locale);
        terrainOwner=OriginalNativeTerrainCurrentViewOwner.CreateFreshFieldSceneNoExternalAssignments(loaded.TerrainMaterial);
        terrainSource=OriginalTerrainViewPublicationSource.ReadFieldScene(loaded.TerrainMaterial.Parameters);
        node51=OriginalSceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/node51");
        node56=OriginalSceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/node56",56);
        bootstrapNode51=OriginalSceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/bootstrap-node51");
        var defaultView=OriginalDefaultView0Source.LoadDeclarationDefaults(repo);
        header=OriginalMainCameraView19B8Owner.ConstructFresh(defaultView,binding.CameraOwnerIdentity);
        shadowContextFields=OriginalDeferredEarlyShadowPreparationArgumentsSource.Read(defaultView).ConstructorFields;
        shadowForceFitSource=LoadedOriginalDeferredEarlyShadowForceFitSource.Load(repo,locale);
        shadowBiasSource=LoadedOriginalDeferredEarlyShadowBiasSource.Load(repo,locale);
        shadowViewMasks=new(defaultView);
        shadowPopulation=new(cpu);
        // Same source-created renderer profile used by the normal Main Camera.
        // Import once; ordinary requests never reread the source image.
        shadowNativeClipProfile=OriginalMainMapCameraStartupSource.Load(repo,locale).StaticProjectionProfile;
        worldLists=new(OriginalOwnedWorldListSource.Load(repo),1);listFrame=1;
        environment=OriginalEnvironmentWorldLightPublicationSource.Load(repo);
        var season=loaded.InitialFrame.World.NativeMapSeason??throw new InvalidDataException("World source season is absent.");
        seasonResources=OriginalSceneViewNativeMapSeasonResourceBundle.Load(repo,season,locale,1);
        var boot=loaded.InitialFrame with {MainCamera=loaded.InitialFrame.MainCamera with {
            Camera=loaded.InitialFrame.MainCamera.Camera.CopyAcceptedSnapshotForOwnedFrame(1,generation+"/bootstrap-optics")}};
        if (actualBootstrapArmyInventory is null)
        {
            if (boot.World.Units.Count != 0)
                throw new InvalidDataException("Nonempty World bootstrap needs its actual source-owned ordinary/linked-flag logical inventory.");
            actualBootstrapArmyInventory=Array.Empty<OriginalNativeArmyWorldLogicalActor>();
        }
        if ((boot.World.Units.Count == 0) != (actualBootstrapArmyInventory.Count == 0))
            throw new InvalidDataException("World bootstrap Army inventory disagrees with its actual captured unit population.");
        Bootstrap=Prepare(boot,null,0,null,actualBootstrapArmyInventory);
        BootstrapNode51=bootstrapNode51.PublishAndPrepareCurrent(1,Binding);
        early=OriginalDeferredEarlySourceViewQueriesOwner.CreateFromFreshSourceGroupPools(Bootstrap.World.View,generation,0,
            generation+"/fresh-group-pools",generation+"/actual-owned-world-construction");
        BootstrapEarly=early.PrepareCurrent(Bootstrap.World.View,generation+"/bootstrap-query");
    }

    public CurrentFrame PrepareCurrent(OriginalNativeMapFrameContext context,OriginalSceneViewNativeMapFrameCapture capture,
        OriginalNativeMapArmyPreparedFrame army,float delta)
    {
        if(!ReferenceEquals(army.Context,context)||army.CpuPrimitivePopulation is not { } actual ||
            !ReferenceEquals(actual.Prepared,army)||army.WorldPeriodNumber!=capture.World.PeriodNumber||army.Unresolved.Count!=0)
            throw new InvalidDataException("World source assembly needs its actual complete same-frame Army BeforeFrame inventory.");
        var frame=Prepare(capture,context,delta,army,null);current.Add(context,frame);
        early.ApplySourceBankReset(frame.World.View,generation+"/actual-view-reset/"+context.FrameNumber);
        return frame;
    }

    private CurrentFrame Prepare(OriginalSceneViewNativeMapFrameCapture capture,OriginalNativeMapFrameContext? context,float delta,
        OriginalNativeMapArmyPreparedFrame? actualArmy, IReadOnlyList<OriginalNativeArmyWorldLogicalActor>? bootstrapArmyInventory)
    {
        if(!float.IsFinite(delta)||delta<0||capture.TerrainBatches.Count==0||capture.TerrainBatches.Any(b=>b.Patches.Count==0)||
            capture.World.NativeMapSeason is not { } season)
            throw new InvalidDataException("World source assembly needs its nonempty accepted terrain and owned season.");
        // Capture one loaded declaration bundle for all consumers of this
        // attempt. Installation belongs between drained session requests.
        var resources=seasonResources;
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var timings=new List<OriginalNativeMapStageTiming>();double previous=0;
        void Mark(string stage)
        {
            double elapsed=timer.Elapsed.TotalMilliseconds;
            timings.Add(new(stage,elapsed-previous,elapsed));previous=elapsed;
        }
        var population=cpu.PrepareCurrent(capture);long number=population.FrameNumber;
        Mark("current-static-population");
        var mode=mainMode.PrepareCurrent(capture.MainCamera);mode.ValidateCurrent(mainMode,capture.MainCamera);
        var selectedSeason=resources.CameraSource.WithCalendar(OriginalMapSeasonRuntime.PrepareFrame(season,number));
        var post=mainPostSelection.PrepareCurrent(selectedSeason,mode,graphicsQuality);
        var before=OriginalSkySeasonMainCameraProducer.PrepareBeforeFinalPreparation(selectedSeason,capture.MainCamera,mode.Mode288,phase,
            previousAcceptedRgbFrame:previousRgb,mainPostSelection:post);
        var rgb=OriginalSkySeasonMainCameraProducer.PrepareSunMoonAndSky(before,delta,OriginalNativeCameraTangentDispatch.Sse,
            OriginalSkySceneSourceDefaults.CreateControllerConstructor(),OriginalSkySceneSourceDefaults.CreateSky2Parameters(107));
        var fog=OriginalSkySeasonMainWeatherFog.PrepareSourceDatabaseResetFrame(rgb,weather,OriginalNativeCameraTangentDispatch.Sse);
        var present=OriginalMapSourceCreatedSeasonLightingFrame.CapturePreparedSeason(rgb,fog);
        Mark("current-main-mode-season-rgb-fog");
        var declarations=resources.DirectionalDeclarations;
        var metadata=resources.LightMetadata;
        var sources=lights(declarations).ToArray();
        resources.RequireCurrentLightSources(sources);
        string Record(int index)=>generation+"/World-record/"+index;
        string Light(int index)=>index==2?Binding.LightOwnerIdentity:generation+"/Directional-data/"+index;
        var common=OriginalMapFreshCommonResources.PrepareSourceCreatedBeforeAmbientPublication(commonSource,number,
            generation+"/source-Common-settings",generation+"/Common",Binding.ViewIdentity,generation+"/properties-before-ambient");
        var provider=providers?.Invoke(declarations);
        var caller=provider is null?null:OriginalDirectionalLightProviderCaller.CaptureFreshOwnedCaller(loaded.Repository,
            before.MainFrame,common,OriginalDirectionalProviderCallerScope.FreshOwnedCameraClearedLateControlsNoLaterSelectorWriter);
        var packets=sources.Select((s,i)=>
        {
            var input=new OriginalMapSourceCreatedLightingFrameInputs(number,Binding with {LightOwnerIdentity=Light(i)},present.Parameters,
            present.PriorPreparedCameraPrefix,contextOwner.Host.ContextFlags3E4,delta,OriginalMapLightingRuntimeTable.Present,
            OriginalMapLightingCameraScope.LightingPrefix,scalars.PrepareBuiltinAmbientCameraRate(capture.MainCamera.Camera),true,new(),
            PresentSeasonCamera:present);
            return provider is null?s.PrepareFrame(input):s.PrepareFrame(input,
                provider.PrepareFreshCallerFrame(s.Construction,caller!,before.MainFrame,present.PreparedCameraPrefix.Span));
        }).ToArray();
        var primary=packets[2];
        Mark("current-common-five-light-packets");
        if(primary.SourceIdentity.DeclaredGroupMask!=0x80)throw new InvalidDataException("Actual primary World root is not group80.");
        if(number<listFrame)throw new InvalidDataException("World source list cannot move backward.");
        if(number>listFrame){worldLists.FrameSync(listFrame,number);listFrame=number;}
        else worldLists.Clear(number,OriginalEnvironmentWorldLightPublicationSource.WorldLightOutputObjectId);
        foreach(var source in sources)if(!data.ContainsKey(source.Construction.SourceObjectId))
            data.Add(source.Construction.SourceObjectId,worldLists.CreateData(OriginalOwnedWorldCpuState.Present));
        if(!OriginalMapSeasonWorldLightRegistration.Append(environment,declarations,worldLists,number,
            sources.Select(s=>new OriginalMapSeasonWorldLightDataBinding(s.Construction.SourceObjectId,data[s.Construction.SourceObjectId])).ToArray()))
            throw new InvalidDataException("Actual Environment world-light append failed.");
        var registered=worldLists.Snapshot(number,OriginalEnvironmentWorldLightPublicationSource.WorldLightOutputObjectId);
        if(!registered.Entries.Select(e=>e?.DataIdentity).SequenceEqual(sources.Select(s=>(long?)data[s.Construction.SourceObjectId].Identity)))
            throw new InvalidDataException("Actual Environment registered a different ordered root list.");
        var counted=OriginalMapSourceCreatedWorldKind1Publication.PrepareSourceRetainedQueue(number,Binding,generation+"/World-queue/"+number,
            declarations,metadata.Select((m,i)=>new OriginalMapWorldKind1CurrentRecordInput(number,Binding.ViewIdentity,m,i,Record(i),
                m.DeclaredGroupMask,m.MaxPrimitiveNum,packets[i].LocalLightData.Span[0xF20],true,true,true,
                environment.SourceProvenance+";actual-constructed-world-record="+Record(i))).ToArray());
        var queue=counted.Records.Select((r,i)=>new OriginalTerrainCurrentKind1WorldPQPublication(number,Binding.ViewIdentity,
            counted.QueueOwnerIdentity,i,0x10000000,generation+"/World-pair/"+number+"/"+i,Record(i),r.Mask78,true,r.ProducerProvenance)).ToArray();
        Mark("current-world-list-and-record-counts");
        var armyView=OriginalArmyCurrentWorldVisibilityView.PrepareActualNullCustomSelection(number,Binding.ViewIdentity,
            armyCommonView,false,null,null,generation+"/source-owned-retained-null-View1C08/"+number);
        var actors=actualArmy is null ? bootstrapArmyInventory ??
            throw new InvalidDataException("Bootstrap Army logical source inventory is absent.") :
            armyCpu.CaptureActualLogicalInventory(capture.World,actualArmy.Ordinary,actualArmy.Models,actualArmy.Flags);
        var armyCamera=actualArmy is null ? new OriginalDeferredCameraInputs(capture.MainCamera.Camera.SourceWorldToView,
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
        var tail=OriginalMapSourceCreatedViewFields.ReadFreshRetainedCameraFields(capture.MainCamera.Camera);
        if(tail.ViewSignedA00 is <1 or >32||common.CurrentViewAux3F2 is not byte sky||sky is <1 or >32)
            throw new NotSupportedException("Current World needs its actual shadow and Sky selectors.");
        uint shadowMask=1u<<(tail.ViewSignedA00-1),skyMask=1u<<(sky-1);
        var shadow=queue.FirstOrDefault(r=>(r.CurrentWorldPQWord78&shadowMask)!=0)??throw new InvalidDataException("Actual shadow root is absent.");
        var skyRecord=queue.FirstOrDefault(r=>(r.CurrentWorldPQWord78&~skyMask)==0)??throw new InvalidDataException("Actual Sky root is absent.");
        if(shadow.CurrentItemIndex!=2||skyRecord.CurrentItemIndex!=2)
            throw new NotSupportedException("This initial World branch requires independently selected root80 shadow and Sky payloads.");
        uint mask1BF8=uint.MaxValue;
        for(int i=0;i<queue.Length;i++)if((packets[i].LocalLightData.Span[0xF1C]&15)!=0)mask1BF8&=queue[i].CurrentWorldPQWord78;
        var payload=OriginalSkySceneSelectedViewLightSource.Capture(primary.LocalLightData.Span,
            new(number,Binding.ViewIdentity,shadow.CurrentWorldPQOwnerIdentity,Binding.LightOwnerIdentity));
        var selected=OriginalTerrainSelectedKind1Record.FromOwnedCurrentPublication(primary,shadow,payload,mask1BF8,
            generation+"/actual-A00-shadow/independent-Common-Sky-selection");
        var publication=new OriginalMapSourceCreatedViewPublicationState(number,Binding.ViewIdentity,Binding.CameraOwnerIdentity,
            generation+"/View/"+number,false,0,[0,0,0,0],true,null){OtherAdditionalOwners1B80_1BB0_1B98Absent=true,SelectedShadowRecord1BD0=selected};
        OriginalNativeTerrainCurrentViewFrame? terrain=null;
        if(context is not null){terrain=terrainOwner.PrepareWithSelectedKind1Record(context,primary,publication,selected,common);publication=terrain.CurrentViewPublication;}
        else publication=publication with {Terrain1BB8=OriginalTerrainCurrentViewPublication.PrepareWithSelectedKind1Record(terrainSource,primary,
            capture.TerrainBatches.SelectMany(b=>b.Patches).ToArray(),selected,common,generation+"/bootstrap-terrain-CPU")};
        Mark("current-view-and-terrain-publication");
        var inputs=new OriginalMapSourceCreatedF103PopulationPublicationInputs(true,true,publication,
            new Dictionary<string,OriginalMapSourceCreatedModelExclusionOverride>(),false,null,generation+"/actual-static-and-complete-Army/"+number);
        var q=contextOwner.BeginDeferredPreparation(number,generation+"/execution/"+number);
        var world=OriginalMapSourceCreatedWorldF103Frames.Prepare(factory,associationSource,population,counted,primary,
            packets.Select((p,i)=>(p,i)).ToDictionary(x=>Record(x.i),x=>x.p),common,q,inputs,
            armyPublication.CompleteCountsByRecordOwner,null,true,generation+"/World-F103/"+number);
        if(world.Passes.Count==0)throw new NotSupportedException("Actual current World has no admitted source F103 pass.");
        Mark("current-world-f103-dispatch");
        var admission=OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan.PrepareFreshWorld(world.Passes[0].Frame,inputs,counted,
            armyPublication.CompleteCountsByRecordOwner);
        var execution=context is null?null:node51.PublishAndPrepareCurrent(number,Binding);
        var result=new CurrentFrame(world,inputs,rgb,present,queue,counted,terrain,execution,admission,
            header.ProjectCurrentMain0(capture.MainCamera.Camera,generation+"/actual-View19B8/"+number),armyPublication,armyView)
            {ActualArmyPreparedFrame=actualArmy};
        var shadowMain=result.PreparedShadowMain;
        var shadowArguments=shadowMain.SelectCurrentPreparationArguments(shadowContextFields);
        result=result with {ShadowPreparationArguments=shadowArguments,
            PreparedShadowDirection=OriginalDeferredEarlyShadowDirectionSource.PrepareCurrent(
                shadowMain,selected,result.GeometryFirst.Lighting,common,shadowDirectionAux,shadowArguments)};
        var ownCamera=OriginalDeferredEarlyShadowOwnCameraSource.PrepareCurrent(
            shadowMain,result.PreparedShadowDirection!,shadowNativeClipProfile,OriginalNativeCameraTangentDispatch.Sse);
        result=result with {PreparedShadowOwnCamera=ownCamera,
            PreparedShadowWorkVolume=OriginalDeferredEarlyShadowWorkVolumeSource.PrepareCurrent(
                ownCamera,OriginalNativeCameraTangentDispatch.Sse),
            PreparedCompactShadow=shadowMain.Values.ModeA3C is 0 or 9 or 10 or 11
                ? OriginalDeferredEarlyShadowCompactSource.PrepareCurrent(ownCamera) : null};
        if(result.PreparedCompactShadow is { } compact)
        {
            // This generation retains the source-created pool6C8 controller.
            // Per-request compact numeric packets do not reconstruct its
            // CPU138 parameter cache or overwrite a later sbias setter.
            compactShadowBias??=shadowBiasSource.ConstructFreshOwnedCache(compact);
            var forceFit=shadowForceFitSource.PrepareFreshCurrent(compact);
            var reset=OriginalDeferredEarlyShadowCompactResetSource.PrepareCurrent(compact);
            var sourceRecord=counted.Records.Single(record=>record.RecordOwnerIdentity==selected.RecordOwnerIdentity);
            var recordFields=world.BorrowAcceptedRecordLighting(sourceRecord)
                .CaptureRetainedDirectionalRecordNumericFields(sourceRecord);
            var work=OriginalDeferredEarlyShadowWorkPublicationSource.PrepareCurrent(result.PreparedShadowWorkVolume!,recordFields,reset);
            var volume=work is null?null:OriginalDeferredEarlyShadowCompactVolumeSource.PrepareCurrent(
                compact,work,OriginalNativeCameraTangentDispatch.Sse);
            result=result with {ShadowRecordNumericFields=recordFields,PreparedCompactReset=reset,PreparedShadowForceFit=forceFit,
                PreparedShadowBiasCache=compactShadowBias,
                ShadowWorkPublication=work,PreparedCompactVolume=volume,
                PreparedShadowCaster=volume is null?null:OriginalDeferredEarlyShadowCasterSource.PrepareCurrent(volume)};
            shadowViewMasks.BeginCurrent(number,Binding.ViewIdentity);
            shadowViewMasks.PublishCurrentDefaultView0(shadowMain);
            var lightRows=OriginalDeferredEarlyShadowCasterAdmissionSource.CaptureFreshSourceRetainedKind1Rows(
                counted,world.BorrowAcceptedRecordLighting);
            // The imported counter fields currently cover direct V468. A real
            // interpolated camera keeps only those capacities with closed inputs.
            var counterCamera=shadowMain.ActualMainInputs.Source.Selection.SecondPackage is null
                ? OriginalDeferredEarlyShadowViewCounterCameraSource.CaptureCurrentMain(shadowMain) : null;
            result=result with {PreparedShadowViewMasks=shadowViewMasks.CaptureCurrentWorldCollectionCaller(),
                PreparedShadowLightRows=lightRows,PreparedShadowCounterCamera=counterCamera,
                PreparedShadowCounterCapacities=OriginalDeferredEarlyShadowViewCounterSource.PrepareCurrent(lightRows,
                    result.GeometryFirst.ViewFields.CurrentPublication.SelectedShadowRecord1BD0 is not null,
                    counterCamera?.PreparedSignedA98,counterCamera?.PreparedSigned106C),
                PreparedShadowStaticModels=shadowPopulation.PrepareStatic(population,Binding.ViewIdentity),
                PreparedShadowArmyModels=shadowPopulation.PrepareArmy(armyPublication)};
            bool hasSelectedShadow=result.GeometryFirst.ViewFields.CurrentPublication.SelectedShadowRecord1BD0 is not null;
            result=result with {
                PreparedShadowStaticAdmissions=OriginalSceneViewNativeMapShadowPopulationSourceOwner.PrepareAdmissions(
                    result.PreparedShadowStaticModels!,result.PreparedShadowViewMasks!,lightRows,result.GeometryFirst.Common,hasSelectedShadow),
                PreparedShadowArmyAdmissions=OriginalSceneViewNativeMapShadowPopulationSourceOwner.PrepareAdmissions(
                    result.PreparedShadowArmyModels!,result.PreparedShadowViewMasks!,lightRows,result.GeometryFirst.Common,hasSelectedShadow)};
        }
        Mark("current-primitive-admission-general-scene-camera-header");
        LastCurrentPreparationTimings=timings.AsReadOnly();
        return result;
    }

    public CurrentFrame RequireCurrent(OriginalNativeMapFrameContext context)=>current.TryGetValue(context,out var frame)?frame:
        throw new InvalidDataException("Actual current World preparation is absent.");
    public OriginalMapStaticModelCurrentOwnedFrame RequirePopulation(OriginalNativeMapFrameContext context)=>RequireCurrent(context).First.CpuPopulation!;
    public OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan RequireAdmission(OriginalNativeMapFrameContext context)=>RequireCurrent(context).Admission;
    /// <summary>Called by the real57 parameter adapter at its OR40 writer.
    /// This updates current shading selections, not geometry draw completion.</summary>
    public CurrentFrame ApplyExecutedSpecializedGeometryFlagWriter(OriginalNativeMapFrameContext context,
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
    public OriginalDeferredEarlySourceViewQueriesPacket PrepareEarly(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted)=>
        early.PrepareCurrent(RequireCurrent(accepted.Context).World.View,generation+"/current-early-query");
    public OriginalMainMapContextCurrentPacket PrepareAuxiliaryContext(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted)
    {var f=RequireCurrent(accepted.Context).First;return contextOwner.ApplyExecutedThinSurfaceFlagWriter(f.Context,
        checked((int)f.ViewFields.RainDropFlags19B4),generation+"/thin-surface",f.ViewFields.SourceProvenance);}
    public OriginalNativeTerrainStencilPreparedFrame PrepareTerrainStencil(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalNativeTerrainStencilCurrentOwner stencil,TerrainGbufferRasterState raster)
    {
        var f=RequireCurrent(accepted.Context);node51.ValidateCurrent(f.Node51!);
        var terrainExecution=node56.PublishAndPrepareCurrent(accepted.Context.FrameNumber,f.First.Lighting.Binding);
        var eligible=f.Queue.Where(r=>(terrainSource.Exclusion2B8&r.CurrentWorldPQWord78)==0).ToArray();
        if(eligible.Length!=1)throw new NotSupportedException("Actual World cannot provide one ordinary Field record.");
        return stencil.PrepareFromGeneralScene(accepted.Context,f.First,f.Terrain!,eligible[0],terrainExecution,raster);
    }
    public void CommitCompleteFrame(OriginalNativeMapFrameContext context)=>previousRgb=RequireCurrent(context).Rgb;
}
