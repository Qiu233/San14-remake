using System.Numerics;
using RenderingDevice = Godot.RenderingDevice;
using San14.Data;

/// <summary>Normal source-owned World geometry order. Geometry admissions stay
/// immutable; actual57 publishes later Q, and59 supplies the depth sample.
/// The explicit backend selects its real current display surface as parent.</summary>
public sealed class SceneViewNativeMapWorldGeometrySourceOwner : IAsyncDisposable
{
    private readonly GameResourceRepository repository;
    private readonly string locale;
    private readonly SceneViewNativeMapWorldF103SourceOwner world;
    private readonly NativeMapArmyPopulationRenderer army;
    private readonly SceneViewNativeMapWorldSkySourceOwner sky;
    private readonly NativeMapStaticWorldOrderOwner staticOrder;
    private readonly NativeWorldShaderTableRegistry tables;
    private readonly WorldPrimitiveOrderSource orderSource;
    private readonly LoadedModelFrontendColorSource colors;
    private readonly ModelQueueStateSource queue49,queue50,queue54,queue55;
    private readonly NativeStaticStencilCurrentOwner staticStencil;
    private readonly LoadedWorldGeometryLateCpuSource lateSource;
    private readonly OrdinaryRs4CpuSource rs4CpuSource;
    private readonly OrdinaryRs4CurrentPopulationOwner rs4Population;
    private readonly OrdinaryRs4CpuFrontendState rs4Frontend =
        OrdinaryRs4CpuFrontendState.UnobservedCallerProjection();
    private readonly LoadedWorldGeometryLateEffectSource effectSource;
    private readonly WorldGeometryLateEffectParameterState effects;
    private readonly WorldGeometryLateGenericScopeState generic;
    private readonly MapPostEffect3FallbackCameraConstructorWords rectangles;
    private readonly WorldGeometryNode58PackedParameters packed58;
    private readonly WorldGeometryHostFields targetHost;
    private NativeMapArmyPreparedFrame? preparedArmy;
    private ArmyWorldPrimitivePublication? armyPublication;
    private NativeArmyCurrentAdmission? armyAdmission;
    private NativeMapArmyWorldQueuePreparation? armyQueues;
    private NativeArmyWorldFlagMaterialPublication? flagMaterials;
    private NativeArmyMaterialRenderResourceContext? materialResources;
    private bool retired;
    private WorldGeometryLateFogPublication? hostQ88;
    private bool invocationOpen;
    private WorldGeometryTerrain56StageSourceOwner? terrainStages;
    public WorldGeometryStageLifetimeOwner Targets { get; }
    public WorldGeometryLateCpuUpdates? Last57 { get; private set; }
    public WorldGeometryLateCpuUpdates? Last58 { get; private set; }
    public WorldGeometryLateCpuState? LastLateState { get; private set; }
    public IReadOnlyList<OrdinaryRs4CpuDispatchCompleted>? LastRs4CpuDispatches { get; private set; }
    public OrdinaryRs4CpuFrontendState Rs4Frontend => rs4Frontend;
    public NativeMapArmyWorldQueuePreparation? CurrentArmyQueues => armyQueues;
    public int CurrentRetainedArmyMaterialReferenceCount => materialResources?.RetainedMaterialReferenceCount ?? 0;
    public int LastCompletedArmyMaterialReferenceCount { get; private set; }

    /// <summary>The caller has accepted this same Host's complete final output.
    /// A geometry queue receipt alone does not end the MaterialData lifetime.</summary>
    public void CompleteActualFrameResourceLifetime(NativeMapFrameContext accepted)
    {
        if(materialResources is not {} context)return;
        if(!ReferenceEquals(context.ActualArmy.Context,accepted))
            throw new InvalidDataException("Completed Host output belongs to another retained Army material context.");
        LastCompletedArmyMaterialReferenceCount=context.RetainedMaterialReferenceCount;
        ReleaseMaterialResources();
    }

    /// <summary>Failure has returned from the Host request. Retire and drain
    /// the partial geometry borrowers before releasing their retained Data.
    /// No accepted-frame or source-time commit is produced.</summary>
    public async Task AbortFailedFrameAsync(MapSourceCreatedWorldPlayableHost host)
    {
        await RetireTargetGenerationAsync(new(host.Scene.TerrainFrame,host.Scene.OutputFrame));
    }

    /// <summary>Only call after the actual Host retirement has drained its
    /// borrowers, including an unsuccessful construction attempt.</summary>
    internal void ReleaseAfterHostRetirement() => ReleaseMaterialResources();

    private void ReleaseMaterialResources()
    {materialResources?.Dispose();materialResources=null;}

    public async Task RetireTargetGenerationAsync(NativeMapTargetContext targets)
    {
        await Targets.RetireTargetGenerationAsync(targets);invocationOpen=false;
        ReleaseMaterialResources();armyQueues=null;
    }

    public async ValueTask DisposeAsync()
    {
        if(retired)return;
        await Targets.DisposeAsync();invocationOpen=false;
        ReleaseMaterialResources();flagMaterials?.Retire();flagMaterials=null;armyQueues=null;retired=true;
    }

    public SceneViewNativeMapWorldGeometrySourceOwner(GameResourceRepository repository,string locale,
        SceneViewNativeMapWorldF103SourceOwner actualWorld,NativeMapArmyPopulationRenderer actualArmy,
        SceneViewNativeMapWorldSkySourceOwner actualSky,NativeMapStaticWorldOrderOwner actualStaticOrder,
        NativeWorldShaderTableRegistry actualSharedTables,WorldPrimitiveOrderSource actualOrderSource,
        LoadedModelFrontendColorSource actualColors,NativeStaticStencilCurrentOwner actualStaticStencil)
    {
        this.repository=repository;this.locale=locale;
        world=actualWorld;army=actualArmy;sky=actualSky;staticOrder=actualStaticOrder;tables=actualSharedTables;
        orderSource=actualOrderSource;colors=actualColors;staticStencil=actualStaticStencil;
        var stage=WorldGeometryStageSource.Load(repository,locale);
        targetHost=stage.ConstructFreshHostFields();
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        queue49=ModelRendererStateSourceReader.ReadQueue(graph,49);
        queue50=ModelRendererStateSourceReader.ReadQueue(graph,50);
        queue54=ModelRendererStateSourceReader.ReadQueue(graph,54);
        queue55=ModelRendererStateSourceReader.ReadQueue(graph,55);
        lateSource=WorldGeometryLateCpuSource.Load(repository,locale);
        var rs4=OrdinaryRs4Source.Read(graph,repository.LoadObjects("system",0x93FB3280),lateSource.Native);
        rs4CpuSource=new(rs4,repository.LoadObjects("KIDSSystemResource",MapFreshCommonResources.SourceFileId));
        rs4Population=new(rs4);Targets=new(stage,rs4);
        packed58=WorldGeometryNode58PackedParameterSource.Read(lateSource.Queue58);
        effectSource=LoadedWorldGeometryLateEffectSource.Load(repository,locale);
        effects=effectSource.ConstructRegisteredSourceModules(LoadedWorldGeometryLateHostSource.Load(repository,locale));
        // Actual managed allocation starts these retained cache holes at zero.
        // They are not original constructor defaults. The real scope writers
        // overwrite all effective values and retain subsequent cache/dirty state.
        generic=WorldGeometryLateGenericScopeState.CreateFresh(new(Vector4.Zero,Vector4.Zero,
            Vector4.Zero,Vector4.Zero,Vector4.Zero,0,0,0,0));
        generic.ImportCurrentDescriptor(null); // Actual managed allocation input; native constructor preserves C8.
        rectangles=RenderSamplerSourceAdapters.ReadFallbackCameraRectangleNativeSource(
            RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }

    public MapSourceCreatedWorldPlayableCurrentSources CreateCurrentSources(
        MapSourceCreatedWorldPlayableCurrentSources existing)
        =>existing with
        {
            AfterCurrentPreparedWithLighting=PrepareArmyCurrentAsync,
            SubmitWorldGeometryBeforeTerrain=SubmitBeforeTerrainAsync,
            CompleteGeometryAndPrepareShading=CompleteAfterTerrainAsync,
            RequireGeometryDepthSampling=frame=>
            {
                var sampling=Targets.RequireFinalSampling();
                if(!ReferenceEquals(sampling.Accepted.Context,frame.Context))throw new InvalidDataException("Geometry depth belongs to another World invocation.");
                return sampling;
            }
        };

    private async Task PrepareArmyCurrentAsync(MapSourceCreatedNativeSessionPreparedFrame frame,
        DirectionalLightRetainedConsumerReceipt? consumer)
    {
        var current=world.RequireCurrent(frame.Context);
        preparedArmy=await army.CaptureBeforeFrameAsync(frame.Context);
        if(!ReferenceEquals(preparedArmy,current.ActualArmyPreparedFrame))
            throw new InvalidDataException("Geometry Army snapshot differs from its actual World visibility publication.");
        armyPublication=current.ArmyPublication;
        armyAdmission=await army.BindCurrentWorldPrimitiveAdmissionAsync(preparedArmy,armyPublication,current.Admission);
    }

    private async Task SubmitBeforeTerrainAsync(MapSourceCreatedNativeSessionPreparedFrame frame,
        NativeMapArmyPreparedFrame armyFrame,MapSourceCreatedWorldPlayableHost host)
    {
        ObjectDisposedException.ThrowIf(retired,this);
        if(invocationOpen){await Targets.EndCurrentAsync();invocationOpen=false;}
        if(materialResources is not null)
            throw new InvalidOperationException("The preceding Host frame still owns its Army material resource context.");
        var current=world.RequireCurrent(frame.Context);
        if(!ReferenceEquals(armyFrame,preparedArmy)||armyPublication is null||armyAdmission is null)
            throw new InvalidDataException("World geometry requires its exact prepared Army publication/admission.");
        // Original Main begin clears Q88; node137 publishes it after geometry.
        hostQ88=null;Last57=null;Last58=null;LastLateState=null;LastRs4CpuDispatches=null;
        var clock=host.PrepareGeometryCommonClock(frame.Context);clock.RequireCurrent();
        var displayEntry=await host.PrepareMainDisplayEntryAsync(frame.Context);
        void RequireCurrent()=>_=world.RequireCurrent(frame.Context);
        var depth=host.Viewport.PrepareSourceMainFrame(frame.Selection,frame.Feature.Binding.ViewIdentity);
        WorldGeometryDrawDomain Domain(RenderTargetFrame target)
        {
            var extent=target.Textures.Values.First().ExplicitExtent;
            return new(0,0,extent.X,extent.Y,depth.ViewportMinimumDepth,depth.ViewportMaximumDepth,
                false,new(0,0,extent.X,extent.Y),"actual owned full-target wrapper selection;"+depth.SourceProvenance);
        }
        await Targets.BeginActualAsync(frame,()=>new(unchecked((int)world.RequireCurrent(frame.Context).First.Context.ContextFlags3F0),
            true,targetHost),host,RequireCurrent,Domain(frame.Context.OutputFrame),_=>Domain(frame.Context.TerrainFrame),
            ()=>host.CompleteGeometryConsumerReadsAsync(frame.Context),async targets=>
            {await army.BeforeTargetRetirementAsync(targets);await host.Session.Population.BeforeTargetRetirementAsync(targets);},
            new WorldGeometryTargetRow(displayEntry,displayEntry.TargetStackIndex,displayEntry.Domain));
        invocationOpen=true;
        // One CPU/material preparation is shared by earlier RS4 and later
        // static54/55. The lease serializes retirement until actual submission
        // or failed-attempt disposal; preparing it does not draw geometry.
        await using var staticQueues=await host.Session.Population.PrepareQueuesAsync(frame.Context,
            current.Admission,staticStencil);
        var staticQueueOrder=staticOrder.PrepareCurrentOrder(staticQueues.Plan);
        if(armyFrame.FlagActors!=0 || armyFrame.Flags.Count!=0)
        {
            flagMaterials??=NativeArmyWorldFlagMaterialPublication.CreateLoadedOwned(repository,tables,locale);
            materialResources=flagMaterials.PrepareActualFrameWithOwnedResourceContext(armyFrame,RequireCurrent);
        }
        // These are initialized device PSO fields. Source ordinary entry
        // disables blending, so factor/op fields are inactive and do not
        // represent an invented native eight-slot frontend history.
        var inactiveDeviceFields=Enumerable.Range(0,6).Select(_=>new ModelBlendAttachment(false,
            RenderingDevice.BlendFactor.One,RenderingDevice.BlendFactor.Zero,RenderingDevice.BlendOperation.Add,
            RenderingDevice.BlendFactor.One,RenderingDevice.BlendFactor.Zero,RenderingDevice.BlendOperation.Add,
            true,true,true,true)).ToArray();
        var actualColorEntry=ArmyWorldQueueActiveColorSourceOwner.ProjectActiveAttachments(inactiveDeviceFields,
            colors.PrepareOrdinaryQueueActiveColorEntry(queue49,6));
        var queues=await army.PrepareWorldQueuesAsync(armyFrame,armyPublication,armyAdmission,
            frame.CpuPopulation!.MainCamera,current.GeometryFirst.Common,frame.Lighting.ContextFlags3E4,
            tables,orderSource,actualColorEntry,materialResources is null?null:flagMaterials,RequireCurrent);
        armyQueues=queues;
        // Publish the complete CPU primitive bank before the earlier RS4
        // queues. This preparation does not submit Army49/50 or select48.
        var rs4Current=rs4Population.CaptureCurrent(staticOrder,current.Admission,queues.Tuples);
        _=await Targets.Bind43Async();
        await Targets.Pop47AfterCallerAsync(binding=>
        {
            var actual43=binding??throw new InvalidDataException("Normal World requires its actual43 selection.");
            var viewport=WorldGeometryLateViewportOwner.FromLoadedSourceCamera(actual43,host,rectangles);
            uint Flags()=>world.RequireCurrent(frame.Context).GeometryFirst.Context.ContextFlags3F0;
            var parameters=new OrdinaryRs4CpuParameterSourceOwner(actual43,current.GeometryWorld,rs4CpuSource,
                effectSource,effects,generic,viewport.Scope,()=>viewport.ReadActualCurrentWrapper(),
                ()=>viewport.ReadActualSelectedView1540(),Flags,()=>hostQ88);
            var selection=current.Node51!.Invocation;
            var dispatcher=new OrdinaryRs4CpuDispatcherOwner(rs4CpuSource,colors,actual43,rs4Current,
                current.GeometryWorld,orderSource,rs4Frontend,parameters,
                ()=>new(true,frame.Lighting.ContextFlags3E4,Flags(),selection.CurrentContextWord3F8!.Value,true));
            foreach(int node in new[]{44,45,46})
                dispatcher.ExecuteQueue(node,clock.CommonTime254,current.CountedQueue.Records.Count,null,null);
            dispatcher.RequireCompletedBeforePop47();LastRs4CpuDispatches=dispatcher.CompletedQueues;
            return Task.CompletedTask;
        });
        _=await Targets.Bind48Async();var bound=Targets.GetArmyTarget();
        var color49=ArmyWorldQueueNativeColorSourceOwner.CaptureActualActiveEntry(colors,queues.SourceQueues.Plan,
            queue49,current.GeometryFirst.Common,6,RequireCurrent);
        await queues.SubmitQueueAsync(49,bound,color49);
        var color50=ArmyWorldQueueNativeColorSourceOwner.CaptureActualActiveEntry(colors,queues.SourceQueues.Plan,
            queue50,current.GeometryFirst.Common,6,RequireCurrent);
        await queues.SubmitQueueAsync(50,bound,color50);_=await queues.CompleteAcceptedAsync();
        await sky.SubmitCurrentAsync(frame,current,current.Node51??throw new InvalidDataException("World51 is unavailable."),
            current.First.Lighting.AcceptedInputs.FrameDelta,host.CurrentConsumerReceipt);
        _=await Targets.Pop52AndBind53Async();
        var color54=NativeMapStaticActiveColorSourceOwner.CaptureActualEntry(colors,staticQueues.Plan,
            queue54,current.GeometryFirst.Common,RequireCurrent);
        await staticQueues.DrawQueueAsync(54,staticQueueOrder,color54);
        var color55=NativeMapStaticActiveColorSourceOwner.CaptureActualEntry(colors,staticQueues.Plan,
            queue55,current.GeometryFirst.Common,RequireCurrent);
        await staticQueues.DrawQueueAsync(55,staticQueueOrder,color55);
        _=staticQueues.CompleteAccepted();
    }

    private async Task<SceneViewNativeMapWorldF103SourceOwner.CurrentFrame> CompleteAfterTerrainAsync(
        MapSourceCreatedNativeSessionPreparedFrame geometry,MapSourceCreatedWorldPlayableHost host)
    {
        var current=world.RequireCurrent(geometry.Context);
        var terrain=host.RequirePreparedGeometryTerrain(geometry.Context);
        var submitted=host.Scene.LastTerrainSubmittedFrame??throw new InvalidDataException("World56 has not submitted terrain.");
        if(terrainStages is null || !ReferenceEquals(terrainStages.ActualScene,host.Scene))
            terrainStages=new(host.Scene,effectSource);
        _=terrainStages.AcceptCompletedTerrain56(submitted,geometry.Context,generic);
        var clock=host.PrepareGeometryCommonClock(geometry.Context);clock.RequireCurrent();
        await Targets.Pop59AfterCallerAsync(binding=>
        {
            var actual53=binding??throw new InvalidDataException("Normal World requires its actual53 selection.");
            var state=WorldGeometryLateStateFromTerrain56.CreateAfterActualSubmission(actual53,terrain,submitted);
            var viewport=WorldGeometryLateViewportOwner.FromLoadedSourceCamera(actual53,host,rectangles);
            uint Flags()=>world.RequireCurrent(geometry.Context).First.Context.ContextFlags3F0;
            var parameters=new WorldGeometryLateParameterSourceOwner(actual53,current.GeometryWorld,effectSource,effects,generic,
                viewport.Scope,()=>viewport.ReadActualCurrentWrapper(),()=>viewport.ReadActualSelectedView1540(),Flags,
                flags=>world.ApplyExecutedSpecializedGeometryFlagWriter(geometry.Context,flags,lateSource.SourceEvidence),
                ()=>hostQ88,()=>packed58.Packed728);
            var selection=current.Node51!.Invocation;
            var owner=new WorldGeometryLateCpuStateOwner(lateSource,colors,actual53,state,
                ()=>new(true,geometry.Lighting.ContextFlags3E4,Flags(),selection.CurrentContextWord3F8!.Value,true),parameters);
            var common=current.GeometryFirst.Common;
            Last57=owner.Apply57(common.CommonViewResource240Present,(common.CommonViewFlags8&4)!=0,null);
            var publication=staticOrder.RequireCurrentTuples();
            var actualArmy=armyQueues??throw new InvalidDataException("World58 has no current Army first-bank tuple preparation.");
            actualArmy.RequireCurrent();actualArmy.Tuples.Tables.RequireCurrent();
            var rows=current.CountedQueue.Records.Select(record=>
            {
                var statics=publication.Tuples.Where(tuple=>current.Admission.Require(tuple.InstanceKey,tuple.SourceDrawIndex)
                    .IsAdmittedToWorldRecord(record.RecordOwnerIdentity)).ToArray();
                var armies=actualArmy.Tuples.Tuples.Where(tuple=>tuple.Primitive.AdmittedRecordOwners
                    .Contains(record.RecordOwnerIdentity,StringComparer.Ordinal)).ToArray();
                // Both current publishers retain their real insertion order.
                // The supported current World places them in distinct record
                // banks. An overlap requires a real shared insertion producer.
                if(statics.Length!=0 && armies.Length!=0)
                    throw new NotSupportedException("World58 overlapping static/Army bank requires its actual shared publisher insertion order.");
                var tuples=statics.Select(t=>new CurrentLateTuple(t.InstanceKey+"/draw/"+t.SourceDrawIndex,
                    t.ShaderState,t.FirstCounterKey)).Concat(armies.Select(t=>new CurrentLateTuple(t.Primitive.PacketKey,
                    t.ShaderPublication.FinalTable,t.FirstCounterKey))).ToArray();
                var sorted=orderSource.Sort(WorldPrimitiveSortFamily.Opaque,tuples,(tuple,field)=>field switch
                {3=>tuple.Table.OrderedAllocationIdentity,14=>tuple.FirstCounterKey,
                    _=>throw new NotSupportedException("World58 current tuple has no source for selected sort field "+field)});
                var count=current.GeometryFirst.ViewFields.Kind1Records.Single(r=>r.LightOwnerIdentity==record.RecordOwnerIdentity)
                    .CurrentCpuPrimitiveCount;
                if(armies.Length!=actualArmy.CpuPublication.CompleteCountsByRecordOwner[record.RecordOwnerIdentity] || count!=sorted.Length)
                    throw new InvalidDataException("World58 complete static/Army first bank differs from its actual record counter.");
                var nativeRecord=new ModelOrdinaryDispatcherRecord(geometry.Context.FrameNumber,geometry.Feature.Binding.ViewIdentity,
                    record.Mask78,true,count,true,record.ProducerProvenance+";actual complete sorted first bank;state2/bank0");
                return new WorldGeometryLateRecord(nativeRecord,count,record.FirstCounterCapacity,
                    sorted.Select(t=>new WorldGeometryLatePrimitive(t.Identity,t.Table.TableOwner)).ToArray());
            }).ToArray();
            Last58=owner.Apply58(rows.Length,rows.Length,null,common,clock.CommonTime254,actual53.Frame.ColorViews.Count,rows);
            LastLateState=state;return Task.CompletedTask;
        });
        return world.RequireCurrent(geometry.Context);
    }
    private sealed record CurrentLateTuple(string Identity,NativeWorldShaderTableState Table,ulong FirstCounterKey);
}
