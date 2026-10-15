using Godot;
using San14.Data;

/// <summary>The actual caller producer creates these from the current World
/// Camera/selected light and native caller tuple. Unknown caller fields and
/// unknown allocation sides must not be supplied as fixture defaults.</summary>
public sealed record MapPostEffect3FirstWorldSourceFrame(
    MapPostEffect3OwnedFreshCameraFrame Parameters,
    MapPostEffect3TextureParameterState Textures,
    int ActualShaftConstantBufferInstance);

/// <summary>Native facts not inferred from the working Scene's format or
/// frame number. The inherited parent target comes from its actual ordered
/// producer; allocation sides describe this owned bank.</summary>
public sealed record MapPostEffect3FirstWorldPreparationInputs(
    RenderTextureRuntimeDescriptor ActualInheritedParentTarget,
    string ActualInheritedTargetBindingEvidence,float ActualRendererElapsedDelta,
    string ActualClockOwnerIdentity,
    IReadOnlyDictionary<int,int> ActualAllocationSides,int ActualShaftConstantBufferInstance);

public sealed record MapPostEffect3FirstWorldFrontendPreparationInputs(
    float ActualRendererElapsedDelta,string ActualClockOwnerIdentity);

public sealed record MapPostEffect3FirstWorldShaftResult(long FrameNumber,
    MapPostEffect3ParameterLightShaftInitialization Shaft,
    MapPostEffect3TemporalAAFirstPassPreparation TemporalAA,
    MapPostEffect3ComputeCompletedPass? ActualDispatch)
{
    public bool CompletesFunctor0=>false;
    public bool CommitsHostHistory=>false;
}

/// <summary>Bounded real World→3SPK execution entry. Owns the real module bank,
/// lazy source program library and first native CS state. The source caller
/// producer remains mandatory; this owner never substitutes the old A1 chain,
/// a fixture Camera/light or an inferred enabled/disabled effect. At present
/// the executed prefix admits source-disabled TemporalAA followed by Shaft1.
/// Other passes and completion/history remain outside this owner.</summary>
public sealed class MapPostEffect3FirstWorldExecutionOwner:IAsyncDisposable
{
    public MapFinalOutputPostEffect3Source Source{get;}
    public string ExecutableLocale=>depthSource.Native.Locale;
    public PostProcessResourceBank Bank{get;}
    public MapPostEffect3ProgramLibrary Programs{get;}
    public MapPostEffect3ComputeExecutor Compute{get;}
    public MapFirstWorldNativeSamplerState NativeSamplers{get;}
    public MapPostEffect3WorldCompositeSourceOwner CompositeParent{get;}
    public MapPostEffect3WorldInputLightSceneSourceOwner InputLightSceneSource{get;}
    public MapPostEffect3FreshOwnedAllocationSides FreshAllocationSides{get;}
    public MapPostEffect3RetainedModuleState RetainedModule{get;}
    public MapPostEffect3RetainedModuleInvocation? CurrentRetainedInvocation{get;private set;}
    public MapPostEffect3RetainedCpuExit? LastRetainedCpuExit{get;private set;}
    public MapPostEffect3BeginFunctorCounterReceipt? LastBegin{get;private set;}
    public MapPostEffect3FirstWorldShaftResult? LastSubmitted{get;private set;}
    public MapPostEffect3FirstWorldPreparedPrefix? CurrentPrefix{get;private set;}
    public object? LastPreparationFacts{get;private set;}
    private readonly MapSourceCreatedWorldPlayableHost host;
    private readonly SceneViewNativeMapWorldDependencies dependencies;
    private readonly LoadedMapPostEffect3InputDepthSource depthSource;
    private readonly LoadedMapPostEffect3BeginFunctorReturnSource beginReturnSource;
    private readonly object lifetime=new();
    private readonly SemaphoreSlim serial=new(1,1);
    private MapPostEffect3OwnedFreshCameraFrame? acceptedParameters;
    private MapPostEffect3InputDepthSamplerOwner? depthSampler;
    private MapPostEffect3WorldInputNoiseSourceOwner? inputNoiseSource;
    private MapPostEffect3OwnedDirectCaller? acceptedCaller;
    private bool usesDedicatedLightScene;
    private bool accepted,retiring;
    private MapPostEffect3FirstWorldPreparedPrefix? previousCompletedPrefix;
    private MapPostEffect3RetainedCpuExit? previousCompletedExit;
    private MapPostEffect3FirstWorldPreparedPrefix? completedFirstGpuPrefix;
    private MapPostEffect3RetainedCpuExit? completedFirstCpuExit;
    private MapPostEffect3LensExecutionOwner.LensRetainedSamplerCapability? retainedNativeSamplers;
    private readonly List<MapPostEffect3OwnedFreshCameraFrame> retiredParameterFrames=new();
    private Task? disposal;
    private int retirementStage,retiredParameterCount;

    private MapPostEffect3FirstWorldExecutionOwner(MapFinalOutputPostEffect3Source source,
        PostProcessResourceBank bank,MapPostEffect3ProgramLibrary programs,
        MapPostEffect3ComputeExecutor compute,MapFirstWorldNativeSamplerState samplers,
        MapSourceCreatedWorldPlayableHost host,LoadedMapPostEffect3InputDepthSource depth,
        SceneViewNativeMapWorldDependencies dependencies,
        MapPostEffect3RetainedModuleState module,
        LoadedMapPostEffect3BeginFunctorReturnSource beginReturnSource,
        MapPostEffect3FreshOwnedAllocationSides freshSides,
        MapPostEffect3WorldCompositeSourceOwner compositeParent,
        MapPostEffect3WorldInputLightSceneSourceOwner inputLightScene)
        =>(Source,Bank,Programs,Compute,NativeSamplers,this.host,depthSource,this.dependencies,RetainedModule,this.beginReturnSource,FreshAllocationSides,CompositeParent,InputLightSceneSource)=
            (source,bank,programs,compute,samplers,host,depth,dependencies,module,beginReturnSource,freshSides,compositeParent,inputLightScene);

    /// <summary>Use the Host's async before-first-frame seam. Resource creation
    /// establishes neither readable module images nor scalar writer contents.</summary>
    public static async Task<MapPostEffect3FirstWorldExecutionOwner> BuildAsync(
        SceneViewNativeMapWorldDependencies dependencies,
        MapSourceCreatedWorldPlayableHost freshHost,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(dependencies);ArgumentNullException.ThrowIfNull(freshHost);
        if(freshHost.Scene.RenderAttemptNumber!=0 || freshHost.Session.CurrentFrame is not null ||
            !ReferenceEquals(dependencies.Sky.Device,freshHost.BorrowedSky))
            throw new InvalidDataException("First World PostEffect3 owner requires its actual fresh dependency/Host generation.");
        var source=MapFinalOutputPostEffect3Source.Load(dependencies.Repository);
        var container=MapPostEffect3ContainerReader.Read(source.Payload.Span);
        var extent=freshHost.Scene.OutputFrame.Textures[0x064B746B].ExplicitExtent;
        var allocation=MapPostEffect3AllocationReader.Read(container,
            checked((uint)extent.X),checked((uint)extent.Y),"first-World-3SPK:"+Guid.NewGuid().ToString("N"));
        var native=RenderSamplerSourceAdapters.Load(dependencies.Repository.SourceDirectory,
            dependencies.WorldSource.Bootstrap.First.Camera.ExecutableLocale);
        var depth=MapPostEffect3InputDepthSource.Load(dependencies.Repository,native.Locale);
        var stock=MapFinalOutputToneShaderSource.ReadCurrent(native.Locale);
        PostProcessResourceBank? bank=null;
        MapPostEffect3ProgramLibrary? programs=null;
        MapPostEffect3ComputeExecutor? compute=null;
        MapFirstWorldNativeSamplerState? samplers=null;
        MapPostEffect3WorldCompositeSourceOwner? compositeParent=null;
        MapPostEffect3WorldInputLightSceneSourceOwner? lightScene=null;
        var nativeBegin=MapPostEffect3BeginFunctorCounterSource.Load(dependencies.Repository,native.Locale);
        var nativeBeginReturn=MapPostEffect3BeginFunctorReturnSource.Load(dependencies.Repository,native.Locale);
        MapPostEffect3RetainedModuleState? module=null;
        try
        {
            bank=await PostProcessResourceBank.BuildAsync(allocation,allowReadback);
            // ResourceBank owns its accepted immutable plan copy. Source side
            // receipts must bind that exact owner, not the caller's plan.
            var freshSides=MapPostEffect3TextureZeroAllocationSource.Load(dependencies.Repository,native.Locale)
                .SelectFreshOwnedSides(bank.Source);
            module=MapPostEffect3RetainedModuleState.ConstructFreshOwned(bank.Source,freshSides,nativeBegin);
            programs=MapPostEffect3ProgramLibrary.CreateWithStockScreenVertex(container,dependencies.Compiler,stock);
            compute=MapPostEffect3ComputeExecutor.Create(programs,bank);
            samplers=await MapFirstWorldNativeSamplerState.BuildAsync(native,freshHost,dependencies.Sky,bank);
            compositeParent=await MapPostEffect3WorldCompositeSourceOwner.BuildAsync(freshHost,allowReadback);
            lightScene=await MapPostEffect3WorldInputLightSceneSourceOwner.BuildAsync(freshHost,dependencies.Compiler,allowReadback);
            var owner=new MapPostEffect3FirstWorldExecutionOwner(source,bank,programs,compute,samplers,freshHost,depth,dependencies,module,nativeBeginReturn,freshSides,compositeParent,lightScene);
            samplers.BindModuleOwner(owner);return owner;
        }
        catch(Exception buildFailure)
        {
            // Retire borrowers before every source owner; no GPU work occurs
            // here beyond actual resource construction.
            var partial=NativeMapBorrowerRetirementPackage.CreatePending("World-PostEffect3-build",
                new IAsyncDisposable?[]{compute,lightScene,samplers,programs,bank,compositeParent,
                    module is null?null:new RetainedModuleRetirement(module)}.OfType<IAsyncDisposable>(),
                (buildFailure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(partial);}
            catch(Exception retirementFailure)
            {
                throw new NativeMapBorrowerConstructionException(buildFailure,retirementFailure,partial!);
            }
            throw;
        }
    }

    private sealed class RetainedModuleRetirement(MapPostEffect3RetainedModuleState module):IAsyncDisposable
    {
        public ValueTask DisposeAsync(){module.Dispose();return ValueTask.CompletedTask;}
    }

    /// <summary>Same module and program library, with the native resize-only
    /// physical allocation. The caller has returned the previous directory
    /// and released its borrowed frame before installing a new target size.</summary>
    public async Task RefreshOwnedExtentAsync(NativeMapFrameContext context)
    {
        await Compute.DrainPendingAsync();
        var extent=context.OutputFrame.Textures[0x064B746B].ExplicitExtent;
        if(Bank.Source.ContextWidth!=(uint)extent.X||Bank.Source.ContextHeight!=(uint)extent.Y)
            await Bank.ResizeOwnedResourcesAsync(extent);
        await CompositeParent.RefreshOwnedExtentAsync(context);
    }

    /// <summary>Real source preparation without a fixture callback: actual
    /// Node51/Apply slot, resolved inherited target, fresh graphics initializer,
    /// complete current World light lookup, native Lens preset and factory
    /// kernels. The module's own native Begin publishes the counter before
    /// factory preparation; it is never taken from Q or the World frame clock.
    /// Parent target and allocation sides remain explicit required inputs.
    /// Cold unbound texture slots come from the verified original zero allocator.
    /// This is an additional first-prefix execution; it commits no final output.</summary>
    public Task<MapPostEffect3FirstWorldShaftResult> PrepareAndDispatchFromCurrentWorldAsync(
        NativeMapFrameContext context,MapPostEffect3FirstWorldPreparationInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ActualInheritedTargetBindingEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ActualClockOwnerIdentity);
        ArgumentNullException.ThrowIfNull(input.ActualAllocationSides);
        var parent=CompositeParent.CurrentBinding??throw new InvalidDataException("Actual frontend parent has not been bound for node193.");
        parent.RequireCurrent();
        if(!ReferenceEquals(parent.Context,context)||!ReferenceEquals(parent.EffectiveSource,input.ActualInheritedParentTarget))
            throw new InvalidDataException("First source preparation does not inherit this frontend's actual typed parent binding.");
        if(!float.IsFinite(input.ActualRendererElapsedDelta))
            throw new InvalidDataException("Actual owned renderer elapsed delta is not finite.");
        var frozen=input with{ActualAllocationSides=new Dictionary<int,int>(input.ActualAllocationSides)};
        FreshAllocationSides.ValidateFor(Bank.Source);
        if(!frozen.ActualAllocationSides.OrderBy(p=>p.Key).SequenceEqual(NextActualTextureSides(context).OrderBy(p=>p.Key)))
            throw new InvalidDataException("Fresh first World prefix cannot replace the source constructor's actual allocation sides.");
        return PrepareAndDispatchFirstShaftAsync(context,(allocation,prepared,begin)=>
        {
            var world=dependencies.WorldSource.RequireCurrent(context);
            if(!ReferenceEquals(world,host.Session.CurrentWorld))
                throw new InvalidDataException("Source World preparation changed before the actual Direct caller.");
            var graph=prepared.Selection.SourcePlan.Graph;
            var graphics=MapPostEffect3FreshGraphicsConfiguration.LoadFreshTc(dependencies.Repository,
                allocation.ContextOwnerIdentity+"/fresh-graphics-configuration");
            var caller=MapPostEffect3ApplyViewSlotSource.PrepareFreshWorldCaller(graph,
                world.Node51 ??throw new InvalidDataException("Actual same-frame node51 invocation is unavailable."),
                prepared.PreparedSourceFrame!,dependencies.Repository.LoadObjects("KIDSSystemResource",0x6D011726),
                graphics,frozen.ActualInheritedParentTarget,frozen.ActualInheritedTargetBindingEvidence,
                frozen.ActualRendererElapsedDelta,frozen.ActualClockOwnerIdentity);
            var light=MapPostEffect3WorldLightLookupOwner.CaptureCurrent(host,dependencies.WorldSource,context,caller);
            var currentCamera=prepared.Lighting.CapturePreparedCameraFromSameAcceptedMain(prepared.MainCamera);
            caller.ValidateSameCamera(currentCamera);
            var native=MapPostEffect3OwnedFreshCameraSources.Load(dependencies.Repository,currentCamera);
            var textures=previousCompletedPrefix is { } prior ?
                MapPostEffect3TextureParameterState.CreateRetainedForInvocation(allocation,CurrentRetainedInvocation!,prior.Textures) :
                MapPostEffect3TextureParameterState.CreateFreshWithSourceZeroAllocator(allocation,
                context.FrameNumber,frozen.ActualAllocationSides,
                MapPostEffect3TextureZeroAllocationSource.Load(dependencies.Repository,ExecutableLocale),
                "actual-owned-first-World-3SPK-allocation-selection;"+caller.SourceEvidence);
            var frame=MapPostEffect3OwnedFreshCameraFrame.Prepare(Source,allocation,currentCamera,native,
                new(caller.SimpleInputs,light.CreatePresentLightInputs(),light.CreateAbsentLightInputs(),
                    NativeCameraTangentDispatch.Sse),retainedScalarState:previousCompletedPrefix?.Parameters.State);
            try
            {
                // MBInputVelocity is still the actual cold pointer here; this
                // finite prefix writes only working Scene and selected E13.
                // Do not turn a native parameter declaration into a present
                // velocity image or bind an arbitrary noise texture.
                int mb=frame.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"MBInputVelocity",
                    MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale) ??
                    throw new InvalidDataException("Actual MBInputVelocity source lookup is absent.");
                bool velocity=textures.ReadKnownNullablePointer(mb) is not null;
                begin.RequireCurrent();
                if(begin.SelectedFunctorIndex!=frame.Route.SelectedModuleFunctorIndex ||
                    begin.ModuleOwnerIdentity!=allocation.ContextOwnerIdentity ||begin.ModuleSha256!=allocation.Source.Sha256)
                    throw new InvalidDataException("Factory preparation has another module BeginFunctor publication.");
                var invocation=CurrentRetainedInvocation??throw new InvalidDataException("Actual module invocation is absent before factory Init.");
                if(!ReferenceEquals(invocation.Begin,begin))throw new InvalidDataException("Factory Init belongs to another retained module Begin.");
                invocation.PrepareFactories(frame,velocity,MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale);
                light.RequireCurrent();
                acceptedCaller=caller;
                return Task.FromResult(new MapPostEffect3FirstWorldSourceFrame(frame,textures,
                    frozen.ActualShaftConstantBufferInstance));
            }
            catch{frame.Dispose();throw;}
        });
    }

    /// <summary>The independent frontend actually owns the original Output
    /// format parent. Node192's verified matched mode1 branch preserves its
    /// typed stack, and node193 inherits that actual binding. No future output
    /// declaration or previously bound late Scene is substituted for it.</summary>
    public Task<MapPostEffect3FirstWorldShaftResult> PrepareAndDispatchFromOwnedFrontendAsync(
        NativeMapFrameContext context,MapPostEffect3FirstWorldFrontendPreparationInputs input)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(input);
        var prepared=host.Session.CurrentFrame??throw new InvalidDataException("Owned frontend requires the actual World source frame.");
        if(!ReferenceEquals(prepared.Context,context))throw new InvalidDataException("Owned frontend received another World attempt.");
        var camera=prepared.Lighting.CapturePreparedCameraFromSameAcceptedMain(prepared.MainCamera);
        var parent=CompositeParent.BindNode192And193Current(context,camera);parent.RequireCurrent();
        FreshAllocationSides.ValidateFor(Bank.Source);
        var names=MapPostEffect3ParameterState.CreateUninitialized(Bank.Source.Source,context.FrameNumber,Bank.Source.ContextOwnerIdentity);
        int shaftCb=names.FindResourceIndex(MapPostEffect3ParameterResourceTable.ConstantBuffer,"plLightShaftConstants")??
            throw new InvalidDataException("Actual first source Shaft constant-buffer declaration is absent.");
        return PrepareAndDispatchFromCurrentWorldAsync(context,new(parent.EffectiveSource,
            parent.SourceEvidence,input.ActualRendererElapsedDelta,
            input.ActualClockOwnerIdentity,NextActualTextureSides(context),
            (previousCompletedExit?.ConstantBufferInstances??FreshAllocationSides.ConstantBufferInstances)[shaftCb]));
    }

    /// <summary>Invoke in the actual AfterDeferred prefix, before old Final.
    /// The producer receives the exact accepted Camera/World packet and owned
    /// allocation plan. It must recover the real lens preset tuple/light lookup and
    /// initialize its factories and actual allocation-side table using the
    /// receipt from this module's real Begin. The default current-World method
    /// supplies that producer; no Main mount or Host-history commit is implied.</summary>
    public async Task<MapPostEffect3FirstWorldShaftResult> PrepareAndDispatchFirstShaftAsync(
        NativeMapFrameContext context,
        Func<MapPostEffect3AllocationPlan,MapSourceCreatedNativeSessionPreparedFrame,
            MapPostEffect3BeginFunctorCounterReceipt,
            Task<MapPostEffect3FirstWorldSourceFrame>> actualFrameProducer)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(actualFrameProducer);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(accepted)throw new InvalidOperationException("World source invocation is already running.");
            if(context.FrameNumber is not (1 or 2)||(context.FrameNumber==2&&(previousCompletedPrefix?.Context.FrameNumber!=1||previousCompletedExit?.FrameNumber!=1)))
                throw new InvalidDataException("Real second World frame requires this module's private completed frame1 GPU roster.");
            accepted=true;
        }
        await serial.WaitAsync();
        try
        {
            var late=host.Scene.LastWorldDeferredSubmittedFrame ??
                throw new InvalidDataException("Real World late submissions have not completed.");
            if(!ReferenceEquals(late.Context,context) || context.FrameNumber is not (1 or 2))
                throw new InvalidDataException("3SPK entry belongs to another actual World attempt.");
            host.Session.RequireSubmittedWorld(late);
            var prepared=host.Session.CurrentFrame ??throw new InvalidDataException("Actual World preparation is unavailable.");
            if(context.FrameNumber==2)
            {
                if(depthSampler is not null){await depthSampler.DisposeAsync();depthSampler=null;}
                NativeSamplers.PrepareRetainedOwnedFrame(this,context,retainedNativeSamplers??throw new InvalidDataException("No actual completed Lens sampler writer."));
            }
            var invocation=CurrentRetainedInvocation=RetainedModule.BeginFrame(context.FrameNumber);
            LastRetainedCpuExit=null;
            var begin=LastBegin=invocation.Begin;
            // Native Begin increments CPU54 before factory execution. A later
            // preparation/draw failure consumes this invocation, without reset.
            begin.RequireCurrent();
            invocation.ReportSourceFrontendBeginResult(beginReturnSource.CaptureCurrentFrontendBegin(invocation));
            var input=await actualFrameProducer(Bank.Source,prepared,begin) ??
                throw new InvalidDataException("The actual lens preset source producer returned no current frame.");
            var frame=input.Parameters;
            frame.RequireCurrent();frame.Route.ValidateSameCamera(
                prepared.Lighting.CapturePreparedCameraFromSameAcceptedMain(prepared.MainCamera));
            if(!ReferenceEquals(frame.Allocation,Bank.Source) || frame.FrameNumber!=context.FrameNumber ||
                input.Textures.FrameNumber!=context.FrameNumber || input.Textures.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity ||
                input.Textures.SourceSha256!=Bank.Source.Source.Sha256 || !frame.Route.ExecutesModuleFunctor ||
                frame.Route.SelectedModuleFunctorIndex!=0 || !ReferenceEquals(host.Session.CurrentFrame,prepared))
                throw new InvalidDataException("Source producer returned a foreign module/Camera/World/texture-table frame.");
            if(acceptedParameters is not null)retiredParameterFrames.Add(acceptedParameters);
            acceptedParameters=frame;
            begin.RequireCurrent();
            var init=frame.RequirePreparedFactories();
            invocation.RequireInitializedFrame(frame);
            if(!invocation.TextureInstances.OrderBy(p=>p.Key).SequenceEqual((previousCompletedExit?.TextureInstances??FreshAllocationSides.TextureInstances).OrderBy(p=>p.Key))||
                !invocation.ConstantBufferInstances.OrderBy(p=>p.Key).SequenceEqual((previousCompletedExit?.ConstantBufferInstances??FreshAllocationSides.ConstantBufferInstances).OrderBy(p=>p.Key)))
                throw new InvalidDataException("First World invocation differs from the module's genuine constructor sides.");
            // Real external scene/depth bindings. The current Scene is proved
            // by late submission; E13 selection is independently source-pinned.
            var scene=context.OutputFrame.Textures[0x064B746B];
            var depth=context.TerrainFrame.Textures[0xE13DDB4F];
            depthSource.RequireSelectedOwnedDepthDescriptor(depth.EffectiveSource);
            int color=frame.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"InputColor",
                MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale) ??
                throw new InvalidDataException("Actual InputColor declaration is absent.");
            int depthIndex=frame.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"InputDepth",
                MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale) ??
                throw new InvalidDataException("Actual InputDepth declaration is absent.");
            input.Textures.BindExternal(color,new(context.FrameNumber,scene.EffectiveSource.OwnerIdentity),
                late.SourceEvidence+";actual-working-scene");
            input.Textures.BindExternal(depthIndex,new(context.FrameNumber,depth.EffectiveSource.OwnerIdentity),
                depthSource.SourceEvidence+";actual-input-depth");
            var world=MapPostEffect3WorldStockGraphicsSourceOwner.CaptureCurrent(host,context,Bank,input.Textures);
            LastPreparationFacts=CapturePreparationFacts(frame,input.Textures,"before-mandatory-external-input-writes");
            var postSeasonCamera=prepared.Lighting.CapturePreparedCameraFromSameAcceptedMain(prepared.MainCamera);
            // The dedicated light scene gate uses BOTH the nonzero copied SceneType
            // and the actual positive source length/RGB caller predicate.
            var sceneType=postSeasonCamera.Source.Camera.Properties[142];
            if(sceneType.Index!=142||sceneType.NameHash!=0x4168C674||sceneType.TypeCode!=5||sceneType.ElementCount!=1||
                sceneType.RawValue.Length!=4)
                throw new InvalidDataException("Current source LightShaftSceneType differs from its native loader field.");
            var caller=acceptedCaller??throw new InvalidDataException("Actual InputLightScene selection requires the typed same-frame source caller.");
            caller.ValidateSameCamera(postSeasonCamera);
            int lightScene=frame.State.FindResourceIndex(MapPostEffect3ParameterResourceTable.Texture,"InputLightScene")??
                throw new InvalidDataException("Original InputLightScene parameter is absent.");
            usesDedicatedLightScene=System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(sceneType.RawValue)!=0&&caller.ActualLightShaftArgument;
            if(usesDedicatedLightScene)
                await InputLightSceneSource.BindAndProduceCurrentAsync(world,frame,postSeasonCamera,caller);
            else
                input.Textures.BindExternal(lightScene,new(context.FrameNumber,scene.EffectiveSource.OwnerIdentity),
                    late.SourceEvidence+";"+caller.SourceEvidence+";dedicated-light-scene-gate-false;working-Scene-fallback");
            inputNoiseSource=MapPostEffect3WorldInputNoiseSourceOwner.BindCurrent(host,world,frame,postSeasonCamera);
            LastPreparationFacts=CapturePreparationFacts(frame,input.Textures,"after-actual-LightScene-branch-and-initialized-Noise-input-writes");
            // Original Apply installs every external input before entering
            // Lens preset factory aliases; all Init aliases then precede TAA Before.
            input.Textures.ApplyPreparedFactoryAliases(frame,MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale);
            var temporal=invocation.PrepareTemporalAABefore();
            if(temporal.Disposition!=MapPostEffect3TemporalAAFirstPassDisposition.DisabledNativeTextureAliasRequiresProducer)
                throw new NotSupportedException("Enabled actual TemporalAA requires its real pass0/history inputs and sampler mutations before Shaft; this first-prefix owner does not skip it.");
            input.Textures.ApplyDisabledTemporalAAAlias(temporal,MapPostEffect3ParameterAliasMode.SourceAsciiZeroLocale);
            if(!init.Shaft.BeforeHookAllowsPasses)
                return PublishPrefix(context,input,world,begin,new(context.FrameNumber,init.Shaft,temporal,null));
            var shaft=init.Shaft.ConstantBuffer ??throw new InvalidDataException("Enabled source Shaft has no actual CB writer.");
            _=MapPostEffect3ParameterLightShaft.PreparePassBindings(Bank.Source,1,init.Shaft);
            await Bank.UploadCompleteConstantBufferAsync(shaft,input.ActualShaftConstantBufferInstance);
            var exposure=host.Final.Chain.Exposure.LastPacket ??
                throw new InvalidDataException("Source node1 Exposure has no actual completed packet.");
            NativeSamplers.CaptureFirstWorldPrefix(late,exposure);
            var textures=new List<IMapPostEffect3ComputeTextureLease>();
            foreach(int index in Bank.Source.Passes[1].ActiveBindings.Where(b=>
                b.Table==MapPostEffect3BindingTable.TextureParameters &&
                b.Role==MapPostEffect3BindingRole.ShaderResourceRead).Select(b=>b.ParameterIndex).Distinct())
            {
                var current=input.Textures.Require(index);
                if(current.Reference.PhysicalAllocationParameterIndex is not null)
                    throw new NotSupportedException("First Shaft reads an owned module texture without an actual completed input producer: "+index);
                if(current.Reference.ProducerOwnerIdentity==scene.EffectiveSource.OwnerIdentity)
                    textures.Add(world.LeaseWorkingScene(index));
                else if(current.Reference.ProducerOwnerIdentity==depth.EffectiveSource.OwnerIdentity)
                    textures.Add(world.LeaseInputDepth(depthSource,index));
                else if(current.Reference.ProducerOwnerIdentity==inputNoiseSource.OwnerIdentity)
                    textures.Add(inputNoiseSource.Lease());
                else if(usesDedicatedLightScene&&current.Reference.ProducerOwnerIdentity==InputLightSceneSource.OwnerIdentity)
                    textures.Add(InputLightSceneSource.Lease());
                else throw new NotSupportedException("First Shaft has another external current resource producer: "+index);
            }
            depthSampler=await world.BuildInputDepthSamplerAsync(depthSource,depthIndex,0,2,frame,this);
            var samplers=NativeSamplers.LeaseFirstShaftDefaults().Append(depthSampler).ToArray();
            var outputs=Bank.Source.Passes[1].ActiveBindings.Where(b=>
                b.Table==MapPostEffect3BindingTable.TextureParameters &&
                b.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite).Select(b=>b.ParameterIndex).Distinct()
                .ToDictionary(index=>index,index=>input.Textures.Require(index).Reference.InstanceIndex);
            var dispatch=await NativeSamplers.DispatchFirstShaftAsync(Compute,new(context.FrameNumber,textures,samplers,
                new Dictionary<int,int>{{shaft.Declaration.Index,input.ActualShaftConstantBufferInstance}},outputs)
                {TextureParameters=input.Textures});
            begin.RequireCurrent();frame.RequireCurrent();host.Session.RequireSubmittedWorld(late);
            return PublishPrefix(context,input,world,begin,new(context.FrameNumber,init.Shaft,temporal,dispatch));
        }
        finally{lock(lifetime)accepted=false;serial.Release();}
    }

    private object CapturePreparationFacts(MapPostEffect3OwnedFreshCameraFrame frame,
        MapPostEffect3TextureParameterState textures,string stage)
    {
        var flags=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(string name in new[]{"TemporalAAEnable","DofEnable","MotionBlurEnable","LensFlareEnable","LightShaftEnable","IntoSunLight"})
            flags.Add(name,System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(frame.State.RequireValue(name).Span));
        var reads=Bank.Source.Passes[1].ActiveBindings.Where(b=>b.Table==MapPostEffect3BindingTable.TextureParameters&&
            b.Role==MapPostEffect3BindingRole.ShaderResourceRead).Select(b=>
        {
            var declaration=Bank.Source.TextureParameters[b.ParameterIndex];
            var pointer=textures.ReadKnownNullablePointer(b.ParameterIndex);
            return new{b.SourceRowIndex,b.ParameterIndex,b.RegisterIndex,b.NativeViewKind,b.StageMask,
                nativeNameHash=declaration.NameHash,nativeFlags=declaration.NativeFlags,
                knownNull=pointer is null,owner=pointer?.ProducerOwnerIdentity,
                physicalAllocation=pointer?.PhysicalAllocationParameterIndex,side=pointer?.InstanceIndex};
        }).ToArray();
        return new{stage,frame.FrameNumber,sourceEffectFlags=flags,actualPass1Reads=reads};
    }

    private MapPostEffect3FirstWorldShaftResult PublishPrefix(NativeMapFrameContext context,
        MapPostEffect3FirstWorldSourceFrame frame,MapPostEffect3WorldStockGraphicsSourceOwner world,
        MapPostEffect3BeginFunctorCounterReceipt begin,MapPostEffect3FirstWorldShaftResult result)
    {
        frame.Parameters.RequireCurrent();world.RequireCurrent();begin.RequireCurrent();
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            LastSubmitted=result;
            CurrentPrefix=new(this,context,frame.Parameters,frame.Textures,world,result,
                frame.ActualShaftConstantBufferInstance,begin,depthSource,depthSampler,
                CompositeParent.CurrentBinding??throw new InvalidDataException("Prepared prefix lacks its actual frontend parent binding."),
                inputNoiseSource??throw new InvalidDataException("Prepared prefix lacks its actual initialized noise source."),
                usesDedicatedLightScene?InputLightSceneSource:null);
        }
        return result;
    }

    internal void RequireCurrentPrefix(MapPostEffect3FirstWorldPreparedPrefix prefix)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(CurrentPrefix,prefix)||!ReferenceEquals(prefix.Parameters,acceptedParameters)||
                !ReferenceEquals(prefix.Begin,LastBegin)||!ReferenceEquals(prefix.Result,LastSubmitted)||
                !ReferenceEquals(prefix.WorldStockScope.Bank,Bank)||prefix.Context.FrameNumber!=prefix.Parameters.FrameNumber)
                throw new InvalidDataException("Prepared World prefix belongs to another module invocation or source publication.");
        }
        prefix.Parameters.RequireCurrent();prefix.Begin.RequireCurrent();prefix.WorldStockScope.RequireCurrent();
        prefix.FrontendParentBinding.RequireCurrent();
        if(usesDedicatedLightScene)InputLightSceneSource.RequireCurrent();
        host.Session.RequireSubmittedWorld(host.Scene.LastWorldDeferredSubmittedFrame??
            throw new InvalidDataException("Prepared prefix's actual late World publication is absent."));
        var prepared=host.Session.CurrentFrame??throw new InvalidDataException("Prepared prefix has no accepted World frame.");
        if(!ReferenceEquals(prepared.Context,prefix.Context))
            throw new InvalidDataException("Prepared prefix's World render attempt was replaced.");
        prefix.Parameters.Route.ValidateSameCamera(prepared.Lighting.CapturePreparedCameraFromSameAcceptedMain(prepared.MainCamera));
    }

    /// <summary>Source CPU exit is published only after the actual complete
    /// World GPU roster. This does not accept Host presentation. The private
    /// completed roster retains the actual native sampler writer for bounded
    /// continuation; existing per-frame snapshots retain
    /// the sides that were used; the module's next selectors are flipped once.</summary>
    internal MapPostEffect3RetainedCpuExit CompleteRetainedAfterActualFunctor0(
        MapPostEffect3FirstWorldPreparedPrefix prefix,MapPostEffect3ShaftExecutionOwner actualShaft,
        MapPostEffect3MiddleExecutor actualMiddle,MapPostEffect3LensExecutionOwner actualLens,
        MapPostEffect3LensCompletedExecution completedLens,
        MapPostEffect3StockGraphicsExecutor actualGraphics,MapPostEffect3StockGraphicsCompletedPass completedComposite)
    {
        RequireCurrentPrefix(prefix);
        var invocation=CurrentRetainedInvocation??throw new InvalidDataException("Actual retained module invocation is unavailable.");
        invocation.RequireInitializedFrame(prefix.Parameters);
        if(LastRetainedCpuExit is not null||!ReferenceEquals(invocation.Begin,prefix.Begin)||
            !ReferenceEquals(actualShaft.Bank,Bank)||actualShaft.LastCompletedFamily is not { } shaft||
            !ReferenceEquals(shaft.ComputeChain.Prefix,prefix)||
            !ReferenceEquals(actualMiddle.Parameters,prefix.Parameters)||!ReferenceEquals(actualMiddle.Textures,prefix.Textures)||
            !ReferenceEquals(actualLens.Bank,Bank)||!ReferenceEquals(actualLens.LastCompleted,completedLens)||
            !ReferenceEquals(actualGraphics.Bank,Bank)||!ReferenceEquals(actualGraphics.LastCompletedPass,completedComposite)||
            completedComposite.SourcePassIndex!=44||completedComposite.FrameNumber!=prefix.Context.FrameNumber||
            completedComposite.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
            completedLens.FrameNumber!=prefix.Context.FrameNumber||completedLens.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity)
            throw new InvalidDataException("Retained source exit lacks this exact actual World Functor0 owner roster.");
        if(prefix.Result.TemporalAA.Disposition!=MapPostEffect3TemporalAAFirstPassDisposition.DisabledNativeTextureAliasRequiresProducer)
            throw new NotSupportedException("Actual retained first-World completion needs the produced source-disabled TemporalAA branch.");
        if(shaft.ComputeChain.SourceSkippedAllShaftPasses)
        {
            if(prefix.Result.Shaft.BeforeHookAllowsPasses||shaft.ActualStockPass7 is not null||shaft.ComputeChain.OrderedComputePasses.Count!=0)
                throw new InvalidDataException("Retained exit's skipped Shaft roster differs from its actual Before predicate.");
        }
        else if(!prefix.Result.Shaft.BeforeHookAllowsPasses||
            !shaft.ComputeChain.OrderedComputePasses.Select(p=>p.SourcePassIndex).SequenceEqual(Enumerable.Range(1,6))||
            shaft.ComputeChain.OrderedComputePasses.Any(p=>p.FrameNumber!=prefix.Context.FrameNumber||p.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity)||
            shaft.ActualStockPass7 is not {SourcePassIndex:7} pass7||pass7.FrameNumber!=prefix.Context.FrameNumber||
            pass7.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity)
            throw new InvalidDataException("Retained exit lacks all actual Shaft1..7 source passes.");
        shaft.OutputColor?.RequireCurrent();
        actualMiddle.RequireFinishedMiddlePasses();
        if(!completedLens.OrderedPasses.Select(p=>p.SourcePassIndex).SequenceEqual(Enumerable.Range(20,24))||
            completedLens.OrderedPasses.Any(p=>p.SourceBeforeAllowsExecution&&p.Compute is null&&p.Ghost is null))
            throw new InvalidDataException("Retained exit lacks the actual Lens20..43 source execution/skip roster.");
        foreach(int pass in Enumerable.Range(0,45))invocation.RecordProcessedSourcePass(pass);
        var exit=invocation.ApplySourceExitAfterFrontendEnd();
        beginReturnSource.ValidateCompletedFunctor0(exit);
        retainedNativeSamplers=actualLens.CaptureRetainedNativeComputeSamplers();
        if(prefix.Context.FrameNumber==1){completedFirstGpuPrefix=prefix;completedFirstCpuExit=exit;}
        previousCompletedPrefix=prefix;previousCompletedExit=exit;
        return LastRetainedCpuExit=exit;
    }

    private IReadOnlyDictionary<int,int> NextActualTextureSides(NativeMapFrameContext context)
    {
        if(context.FrameNumber==1&&previousCompletedExit is null)return FreshAllocationSides.TextureInstances;
        if(context.FrameNumber==2&&previousCompletedExit is {FrameNumber:1} previous&&previousCompletedPrefix is not null)return previous.TextureInstances;
        throw new InvalidDataException("No genuine source selectors precede this bounded World frame.");
    }
    internal void RequireRetainedWorldAttempt(NativeMapFrameContext context)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(context.FrameNumber!=2||completedFirstGpuPrefix?.Context.FrameNumber!=1||completedFirstCpuExit?.FrameNumber!=1||
                !ReferenceEquals(host.Session.CurrentFrame?.Context,context)||
                host.Scene.RenderAttemptNumber!=2||host.Scene.FrameNumber!=1||CurrentRetainedInvocation is not {FrameNumber:2} invocation)
                throw new InvalidDataException("Native prefix is not this module's real frame2 after completed frame1.");
            invocation.RequireModule(Bank.Source);invocation.RequireCurrent();
        }
    }

    /// <summary>The caller retires this borrower before its World Host and
    /// dependencies. A partially accepted source frame is retired as well.</summary>
    public ValueTask DisposeAsync()
    {
        lock(lifetime)
        {
            retiring=true;
            if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)disposal=RetireAsync();
            return new(disposal);
        }
    }
    private async Task RetireAsync()
    {
        await serial.WaitAsync();
        try
        {
            if(retirementStage==0){await Compute.DisposeAsync();retirementStage=1;}
            if(retirementStage==1){await InputLightSceneSource.DisposeAsync();retirementStage=2;}
            if(retirementStage==2){if(depthSampler is not null)await depthSampler.DisposeAsync();retirementStage=3;}
            if(retirementStage==3){await NativeSamplers.DisposeAsync();retirementStage=4;}
            if(retirementStage==4){await Programs.DisposeAsync();retirementStage=5;}
            if(retirementStage==5){await Bank.DisposeAsync();retirementStage=6;}
            if(retirementStage==6){await CompositeParent.DisposeAsync();retirementStage=7;}
            if(retirementStage==7){acceptedParameters?.Dispose();retirementStage=8;}
            while(retiredParameterCount<retiredParameterFrames.Count)
            {retiredParameterFrames[retiredParameterCount].Dispose();retiredParameterCount++;}
            if(retirementStage==8){RetainedModule.Dispose();retirementStage=9;}
        }
        finally{serial.Release();}
    }
}
