using System.Runtime.CompilerServices;
using San14.Data;

/// <summary>Concrete current owned View/season/CPU source join for the loaded
/// SceneView. Bootstrap has no target claim. Actual attempts attach their
/// accepted terrain inventory and node51 execution before stencil submission.</summary>
public sealed class OriginalSceneViewNativeMapF103SourceOwner
{
    private readonly OriginalSceneViewNativeMapLoadInputs loaded;
    private readonly OriginalSceneViewNativeMapCpuSourceOwner cpu;
    private readonly OriginalMapSourceCreatedLightingDeviceOwner lightingOwner;
    private readonly OriginalMapSourceCreatedF103CurrentFrameFactory f103;
    private readonly OriginalSceneViewNativeMapSourceFields scalars;
    private readonly OriginalMainMapContextCurrentSource contextOwner;
    private readonly LoadedOriginalMapFreshCommonSource commonSource;
    private readonly OriginalMainMapCameraModeOwner mainMode;
    private readonly OriginalNativeTerrainCurrentViewOwner terrainOwner;
    private readonly OriginalTerrainViewPublicationSource terrainSource;
    private readonly OriginalSceneViewNativeMapNode51ExecutionOwner node51,node56;
    private readonly OriginalSceneViewNativeMapNode51ExecutionOwner bootstrapNode51Owner;
    private readonly OriginalMainSceneNode51Execution bootstrapNode51;
    private readonly OriginalMapSeasonMainCameraPhaseSource seasonPhase;
    private readonly OriginalSkyWeatherNativePowerSource weatherPower;
    private readonly OriginalDeferredEarlySourceViewQueriesOwner early;
    private readonly string generation;
    private readonly ConditionalWeakTable<OriginalNativeMapFrameContext, CurrentFrame> current = new();
    private OriginalSkySeasonMainCameraRgbFrame? previousRgb;
    public OriginalMapLightingBinding Binding { get; }
    public OriginalMapSourceCreatedF103CurrentFrame Bootstrap { get; }
    public OriginalDeferredEarlySourceViewQueriesPacket BootstrapEarly { get; }
    public OriginalMapStaticModelCurrentOwnedFrame BootstrapPopulation => Bootstrap.CpuPopulation!;
    private OriginalMapSourceCreatedSeasonLightingFrame bootstrapPresent = null!;
    private OriginalSkySeasonMainCameraRgbFrame bootstrapRgb = null!;
    public OriginalMapSourceCreatedSeasonLightingFrame RequireBootstrapPresentSeason() => bootstrapPresent;
    public OriginalSkySeasonMainCameraRgbFrame RequireBootstrapSeasonRgb() => bootstrapRgb;

    private sealed record CurrentFrame(OriginalMapSourceCreatedF103CurrentFrame F103,
        OriginalMapSourceCreatedF103PopulationPublicationInputs Publication,
        OriginalSkySeasonMainCameraRgbFrame Rgb, OriginalMapSourceCreatedSeasonLightingFrame Present,
        IReadOnlyList<OriginalTerrainCurrentKind1WorldPQPublication> Queue,
        OriginalNativeTerrainCurrentViewFrame? Terrain, OriginalMainSceneNode51Execution? Node51,
        OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan Admission);

    public OriginalSceneViewNativeMapF103SourceOwner(OriginalSceneViewNativeMapLoadInputs loaded,
        OriginalSceneViewNativeMapCpuSourceOwner cpu, OriginalMapSourceCreatedLightingDeviceOwner lighting,
        LoadedOriginalDeferredTexturePlan plan, OriginalMapLightingBinding binding, string viewGeneration,
        OriginalMainMapCameraModeOwner actualMainMode)
    {
        this.loaded=loaded; this.cpu=cpu; lightingOwner=lighting; Binding=binding; generation=viewGeneration;
        mainMode=actualMainMode;
        var repo=loaded.Repository; string locale=loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        scalars=OriginalSceneViewNativeMapSourceFields.CreateFreshRetainedMain(loaded.InitialFrame.MainCamera.Camera);
        contextOwner=OriginalMainMapContextCurrentSource.CreateFresh(repo,locale,generation+"/Host");
        commonSource=OriginalMapFreshCommonResources.ReadSourceTemplate(repo.LoadObjects("KIDSSystemResource",0x6D011726));
        f103=new(plan,OriginalMapSourceCreatedF103ColorScope.FreshSourceNamedModulesWithoutHeaderEdits);
        terrainSource=OriginalTerrainViewPublicationSource.ReadFieldScene(loaded.TerrainMaterial.Parameters);
        terrainOwner=OriginalNativeTerrainCurrentViewOwner.CreateFreshFieldSceneNoExternalAssignments(loaded.TerrainMaterial);
        node51=OriginalSceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/owned-View-collection");
        node56=OriginalSceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,generation+"/node56-owned-View-collection",56);
        bootstrapNode51Owner=OriginalSceneViewNativeMapNode51ExecutionOwner.CreateFreshSingleView(repo,locale,contextOwner,
            generation+"/bootstrap-owned-View-collection");
        seasonPhase=OriginalMapSeasonMainCameraPhaseSource.LoadSourceDefaults(repo,locale);
        weatherPower=OriginalSkyWeatherNativePowerSource.Load(repo,locale);
        var boot=loaded.InitialFrame with {MainCamera=loaded.InitialFrame.MainCamera with {
            Camera=loaded.InitialFrame.MainCamera.Camera.CopyAcceptedSnapshotForOwnedFrame(1,generation+"/bootstrap-optics")}};
        var prepared=Prepare(boot,null,0);
        Bootstrap=prepared.F103;
        bootstrapPresent=prepared.Present;
        bootstrapRgb=prepared.Rgb;
        bootstrapNode51=bootstrapNode51Owner.PublishAndPrepareCurrent(Bootstrap.FrameNumber,Binding);
        early=OriginalDeferredEarlySourceViewQueriesOwner.CreateFromFreshSourceGroupPools(Bootstrap.ViewFields,
            generation,0,generation+"/fresh-group-pools",generation+"/actual-owned-group-construction");
        BootstrapEarly=early.PrepareCurrent(Bootstrap.ViewFields,generation+"/bootstrap-query-publication");
    }

    public OriginalMapSourceCreatedF103CurrentFrame PrepareCurrent(OriginalNativeMapFrameContext context,
        OriginalSceneViewNativeMapFrameCapture capture, OriginalNativeMapArmyPreparedFrame army,
        OriginalMapLightingBinding actualSessionBinding, float explicitPresentationDelta)
    {
        if(actualSessionBinding!=Binding || !ReferenceEquals(army.Context,context) ||
            army.CpuPrimitivePopulation is not { } actualArmy || actualArmy.Actors.Count!=0 ||
            army.OrdinaryActors!=0 || army.FlagActors!=0 || army.Unresolved.Count!=0)
            throw new InvalidDataException("Initial source factory requires its actual same-context empty Army publication; nonempty/unknown visibility cannot become empty.");
        var frame=Prepare(capture,context,explicitPresentationDelta);
        current.Add(context,frame);
        early.ApplySourceBankReset(frame.F103.ViewFields,generation+"/actual-owned-view-reset/"+context.FrameNumber);
        return frame.F103;
    }

    private CurrentFrame Prepare(OriginalSceneViewNativeMapFrameCapture capture,
        OriginalNativeMapFrameContext? accepted,float delta)
    {
        if(!float.IsFinite(delta)||delta<0||capture.TerrainBatches.Count==0||
            capture.TerrainBatches.Any(b=>b.Patches.Count==0)||capture.World.NativeMapSeason is not { } season)
            throw new InvalidDataException("Current source View needs its real nonempty terrain, owned third-date season and explicit presentation delta.");
        var population=cpu.PrepareCurrent(capture);
        long number=population.FrameNumber;
        var mode=mainMode.PrepareCurrent(capture.MainCamera); mode.ValidateCurrent(mainMode,capture.MainCamera);
        var selectedSeason=OriginalSkySeasonMainCameraProducer.ResolveOwnedSource(loaded.Repository,
            OriginalMapSeasonRuntime.ResolveSourceFrame(loaded.Repository,season,number),capture.MainCamera.Camera.ExecutableLocale);
        var before=OriginalSkySeasonMainCameraProducer.PrepareBeforeFinalPreparation(selectedSeason,capture.MainCamera,
            mode.Mode288,seasonPhase,previousAcceptedRgbFrame:previousRgb);
        var rgb=OriginalSkySeasonMainCameraProducer.PrepareSunMoonAndSky(before,delta,OriginalNativeCameraTangentDispatch.Sse,
            OriginalSkySceneSourceDefaults.CreateControllerConstructor(),OriginalSkySceneSourceDefaults.CreateSky2Parameters(107));
        var weather=OriginalSkySeasonMainWeatherFog.PrepareSourceDatabaseResetFrame(rgb,weatherPower,OriginalNativeCameraTangentDispatch.Sse);
        var present=OriginalMapSourceCreatedSeasonLightingFrame.CapturePreparedSeason(rgb,weather);
        var lighting=OriginalMapSourceCreatedLighting.Prepare(lightingOwner.Source,
            new(number,Binding,present.Parameters,present.PriorPreparedCameraPrefix,
                contextOwner.Host.ContextFlags3E4,delta,OriginalMapLightingRuntimeTable.Present,
                OriginalMapLightingCameraScope.LightingPrefix,scalars.PrepareBuiltinAmbientCameraRate(capture.MainCamera.Camera),
                null,new(),PresentSeasonCamera:present));
        var common=OriginalMapFreshCommonResources.PrepareSourceCreatedBeforeAmbientPublication(commonSource,
            number,generation+"/source-Common-settings",generation+"/Common",Binding.ViewIdentity,
            generation+"/properties-applied-before-ambient");
        // These are newly created managed source kind1 records. The queue
        // really owns every pair/record identity and complete shared payload;
        // it is not a process address or an inference from a counter row.
        var masks=OriginalMapSourceCreatedViewFields.SourceKind1RecordMasks(lighting,common.CurrentViewAux3F2!.Value);
        var queue=masks.Select((mask,index)=>new OriginalTerrainCurrentKind1WorldPQPublication(number,Binding.ViewIdentity,
            generation+"/queue/"+number,index,0x10000000,generation+"/pair/"+number+"/"+index,
            generation+"/record/"+number+"/"+index,mask,true,generation+"/owned-CE31D0-kind1-construction")).ToArray();
        var sourceCameraTail=OriginalMapSourceCreatedViewFields.ReadFreshRetainedCameraFields(capture.MainCamera.Camera);
        int shadowGroup=sourceCameraTail.ViewSignedA00;
        if(shadowGroup is <1 or >32 || common.CurrentViewAux3F2 is not byte skySelector || skySelector is <1 or >32)
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
        var payload=OriginalSkySceneSelectedViewLightSource.Capture(lighting.LocalLightData.Span,
            new(number,Binding.ViewIdentity,selected.CurrentWorldPQOwnerIdentity,Binding.LightOwnerIdentity));
        var record=OriginalTerrainSelectedKind1Record.FromOwnedCurrentPublication(lighting,selected,payload,mask1BF8,
            generation+"/owned-current-D789B2-A00/D78BC4-firstmatching-1BD0-and-independent-Common3F2-1B20");
        var publication=new OriginalMapSourceCreatedViewPublicationState(number,Binding.ViewIdentity,
            Binding.CameraOwnerIdentity,generation+"/current-managed-View/"+number,false,0,[0,0,0,0],true,null)
            {OtherAdditionalOwners1B80_1BB0_1B98Absent=true,SelectedShadowRecord1BD0=record};
        OriginalNativeTerrainCurrentViewFrame? terrain=null;
        if(accepted is not null)
        {
            terrain=terrainOwner.PrepareWithSelectedKind1Record(accepted,lighting,publication,record,common);
            publication=terrain.CurrentViewPublication;
        }
        else publication=publication with {Terrain1BB8=OriginalTerrainCurrentViewPublication.PrepareWithSelectedKind1Record(
            terrainSource,lighting,capture.TerrainBatches.SelectMany(b=>b.Patches).ToArray(),record,common,
            generation+"/bootstrap-CPU-terrain-not-GPU")};
        var admissionInput=new OriginalMapSourceCreatedF103PopulationPublicationInputs(true,true,publication,
            new Dictionary<string,OriginalMapSourceCreatedModelExclusionOverride>(),true,null,
            generation+"/complete-static-and-actual-zero-Army/"+number);
        var q=contextOwner.BeginDeferredPreparation(number,generation+"/current-execution/"+number);
        var frame=f103.PrepareFromPopulation(population,lightingOwner.Source,lighting,common,q,admissionInput,null,
            generation+"/actual-current-source-F103/"+number);
        var admission=OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan.PrepareFresh(frame,admissionInput);
        OriginalMainSceneNode51Execution? execution=accepted is null?null:node51.PublishAndPrepareCurrent(number,Binding);
        return new(frame,admissionInput,rgb,present,queue,terrain,execution,admission);
    }

    public OriginalMapStaticModelCurrentOwnedFrame RequirePopulation(OriginalNativeMapFrameContext context)=>Require(context).F103.CpuPopulation!;
    public OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan RequireAdmission(OriginalNativeMapFrameContext context)=>Require(context).Admission;
    public OriginalDeferredEarlySourceViewQueriesPacket PrepareEarly(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted)
        =>early.PrepareCurrent(Require(accepted.Context).F103.ViewFields,generation+"/actual-current-early-query");
    public OriginalSkySeasonMainCameraRgbFrame RequireSeasonRgb(OriginalNativeMapFrameContext context)=>Require(context).Rgb;
    public OriginalMainSceneNode51Execution RequireBootstrapNode51Execution()
    { bootstrapNode51Owner.ValidateCurrent(bootstrapNode51); return bootstrapNode51; }
    public OriginalMainSceneNode51Execution RequireNode51Execution(OriginalNativeMapFrameContext context)
    {
        var actual=Require(context).Node51 ?? throw new InvalidDataException("Current View execution is unavailable.");
        node51.ValidateCurrent(actual); return actual;
    }
    public OriginalMapSourceCreatedSeasonLightingFrame RequirePresentSeason(OriginalNativeMapFrameContext context)=>Require(context).Present;
    public OriginalMainMapContextCurrentPacket PrepareAuxiliaryContext(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted)
    {
        var frame=Require(accepted.Context).F103;
        return contextOwner.ApplyExecutedThinSurfaceFlagWriter(frame.Context,checked((int)frame.ViewFields.RainDropFlags19B4),
            generation+"/owned-thin-surface-writer",frame.ViewFields.SourceProvenance);
    }
    public OriginalNativeTerrainStencilPreparedFrame PrepareTerrainStencil(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalNativeTerrainStencilCurrentOwner stencil,TerrainGbufferRasterState raster)
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
    public void CommitCompleteFrame(OriginalNativeMapFrameContext context)
    { previousRgb=Require(context).Rgb; }
    private CurrentFrame Require(OriginalNativeMapFrameContext context)
        =>current.TryGetValue(context,out var value)?value:throw new InvalidDataException("This accepted context has no actual current owned source View.");
}
