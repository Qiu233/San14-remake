namespace San14.Data;

/// <summary>Source kernels resolved once for an owned original Camera. This
/// current producer supports the recovered TC no-controller ABI.</summary>
public sealed class MapPostEffect3OwnedFreshCameraSources
{
    private MapPostEffect3OwnedFreshCameraSources(LoadedMapPostEffect3LensPresetSource lens,
        LoadedMapPostEffect3ScalarZeroAllocationSource allocator,
        MapPostEffect3LensPresetIntermediateMathSources lensMath,
        MapPostEffect3LightShaftExponential shaftMath,
        MapPostEffect3ParameterCompositeMathSources compositeMath,
        MapPostEffect3PresentLightMathSource sunMath)
        =>(Lens,Allocator,LensMath,ShaftMath,CompositeMath,SunMath)=(lens,allocator,lensMath,shaftMath,compositeMath,sunMath);
    public LoadedMapPostEffect3LensPresetSource Lens{get;}
    public LoadedMapPostEffect3ScalarZeroAllocationSource Allocator{get;}
    public MapPostEffect3LensPresetIntermediateMathSources LensMath{get;}
    public MapPostEffect3LightShaftExponential ShaftMath{get;}
    public MapPostEffect3ParameterCompositeMathSources CompositeMath{get;}
    public MapPostEffect3PresentLightMathSource SunMath{get;}
    public static MapPostEffect3OwnedFreshCameraSources Load(GameResourceRepository repository,
        MapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(camera);
        if(camera.ExecutableLocale!="TC")throw new NotSupportedException("Owned complete  Apply currently admits its recovered TC; caller ABI");
        return new(MapPostEffect3LensPresetSource.LoadForCamera(repository,camera.Source,"TC"),
            MapPostEffect3ScalarZeroAllocationSource.Load(repository,"TC"),
            MapPostEffect3LensPresetIntermediateMathSources.Load(repository,"TC"),
            MapPostEffect3LightShaftExponential.LoadSseSource(repository,"TC"),
            MapPostEffect3ParameterCompositeMathSources.Load(repository,"TC"),
            MapPostEffect3PresentLightMathSource.Load(repository,"TC"));
    }
}

public sealed record MapPostEffect3OwnedFreshCameraInputs(
    MapPostEffect3ApplyUncontrolledSimpleInputs Caller,
    MapPostEffect3ApplyUncontrolledPresentLightInputs? SelectedLight,
    MapPostEffect3ApplyUncontrolledAbsentLightInputs? AbsentLight,
    NativeCameraTangentDispatch NativeDispatch);

public sealed record MapPostEffect3OwnedFactoryInitialization(
    IReadOnlyList<MapPostEffect3LensPresetPrepareBaseResult> PrepareHooks,
    MapPostEffect3ParameterLightShaftInitialization Shaft,
    MapPostEffect3ParameterDepthOfFieldUpdate DepthOfField,
    MapPostEffect3ParameterMotionBlurUpdate MotionBlur,
    MapPostEffect3ParameterLensFlareInitialization Lens,
    MapPostEffect3ParameterCompositeUpdate Composite,
    IReadOnlyList<MapPostEffect3ParameterConstantBuffer> ConstantBuffers)
{
    public bool CommitsGpuFrameOrHistory=>false;
}

/// <summary>One private scalar bank, actual Lens pins and source writes.
/// Keeps fresh source tail scope explicit; no imported/current tail is guessed.
/// Factory preparation follows source prepare/init order. TemporalAA Before is
/// deliberately later, when its pass is selected. This class does not run GPU
/// passes or commit texture contents, matrices or runtime pair history.</summary>
public sealed class MapPostEffect3OwnedFreshCameraFrame:IDisposable
{
    private readonly MapPostEffect3OwnedFreshCameraSources sources;
    private readonly MapCameraConstructionPacket camera;
    private readonly NativeCameraTangentDispatch dispatch;
    private bool disposed;
    private bool failed;
    private MapPostEffect3OwnedFactoryInitialization? initialized;
    private MapPostEffect3TemporalAAFirstPassPreparation? temporalAA;
    private MapPostEffect3OwnedFreshCameraFrame(MapPostEffect3OwnedFreshCameraSources native,
        MapCameraConstructionPacket snapshot,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3CompleteFreshScalarInitialization? defaults,MapPostEffect3ParameterState state,
        MapPostEffect3ApplyMode1Route route,MapPostEffect3LensPresetParameterOwner lens,
        IReadOnlyList<MapPostEffect3ApplyValueWrite> writes,NativeCameraTangentDispatch nativeDispatch)
    {sources=native;camera=snapshot;Allocation=allocation;Defaults=defaults;State=state;Route=route;Lens=lens;ApplyWrites=writes;dispatch=nativeDispatch;}
    public MapPostEffect3AllocationPlan Allocation{get;}
    /// <summary>Present only when this invocation actually initialized fresh storage.</summary>
    public MapPostEffect3CompleteFreshScalarInitialization? Defaults{get;}
    public MapPostEffect3ParameterState State{get;}
    public MapPostEffect3ApplyMode1Route Route{get;}
    public MapPostEffect3LensPresetParameterOwner Lens{get;}
    public IReadOnlyList<MapPostEffect3ApplyValueWrite> ApplyWrites{get;}
    public long FrameNumber=>State.FrameNumber;
    public string OwnerIdentity=>State.OwnerIdentity;

    public static MapPostEffect3OwnedFreshCameraFrame Prepare(
        MapFinalOutputPostEffect3Source source,MapPostEffect3AllocationPlan allocation,
        MapCameraConstructionPacket camera,MapPostEffect3OwnedFreshCameraSources native,
        MapPostEffect3OwnedFreshCameraInputs inputs,
        MapPostEffect3ParameterState? retainedScalarState=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(native);ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Caller);
        if((inputs.SelectedLight is null)==(inputs.AbsentLight is null))
            throw new InvalidDataException("Owned Apply needs exactly one actual successful or absent light lookup.");
        if(camera.ExecutableLocale!="TC"||inputs.Caller.FrameNumber!=camera.FrameNumber||
            allocation.ContextWidth==0||allocation.ContextHeight==0||
            allocation.ContextWidth>int.MaxValue||allocation.ContextHeight>int.MaxValue)
            throw new InvalidDataException("Owned Apply requires the same recovered TC Camera frame and native descriptor extent.");
        var snapshot=camera.CopyAcceptedSnapshot();
        retainedScalarState?.RequireRetainedOwnedAllocation(allocation,snapshot.FrameNumber);
        var lookups=MapPostEffect3ApplyFreshLutLookups.ConstructFreshSource(snapshot,
            allocation.ContextOwnerIdentity+"/fresh-lookups",native.Lens);
        var route=MapPostEffect3ApplyMode1Route.ResolveFreshWithoutController(source,allocation.Source,snapshot,lookups,
            allocation.ContextOwnerIdentity+"/mode1");
        if(!route.ExecutesModuleFunctor||route.SelectedModuleFunctorIndex!=0)
            throw new NotSupportedException("Owned full parameters require the actual successful Functor0 route.");
        var raw=MapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(snapshot,
            allocation.ContextOwnerIdentity+"/fresh-camera-tail");
        var direct=MapPostEffect3ApplyUncontrolledDirectWriters.PrepareFreshSource(snapshot,raw,route.Preparation.Variant,route.SourceEvidence);
        var light=inputs.SelectedLight is{}selected?
            MapPostEffect3ApplyUncontrolledPresentLightWriters.PrepareFreshSource(route.Preparation,snapshot,selected,
                native.SunMath,native.LensMath.Tangent,inputs.NativeDispatch):
            MapPostEffect3ApplyUncontrolledAbsentLightWriters.Prepare(route.Preparation,inputs.AbsentLight!);
        if(inputs.AbsentLight is{}absent&&absent.FrameNumber!=snapshot.FrameNumber)
            throw new InvalidDataException("Absent light lookup belongs to another Camera frame.");
        var math=MapPostEffect3LensPresetIntermediateProducer.PrepareFreshSource(snapshot,native.Lens,native.LensMath,inputs.NativeDispatch);
        var packet=MapPostEffect3LensPresetPreparedIntermediatePacket.ConstructFreshSource(math);
        var defaults=retainedScalarState is null?
            MapPostEffect3ApplyCompleteInitialScalars.CreateFresh(allocation.Source,snapshot.FrameNumber,
                allocation.ContextOwnerIdentity,native.Allocator):null;
        var state=retainedScalarState?.CloneForNewFrame(snapshot.FrameNumber)??defaults!.State;
        state.BindOwnedAllocation(allocation);
        var lens=native.Lens.PinCurrentParameters(packet,allocation.ContextOwnerIdentity+"/lens-pins");
        try
        {
            var pointerOwners=new MapPostEffect3ApplyUncontrolledLensPresetOwners(snapshot.FrameNumber,true,
                new(snapshot.FrameNumber,lens.OwnerIdentity+"/split"),new(snapshot.FrameNumber,lens.OwnerIdentity+"/intermediate"),lens.SourceEvidence);
            var simple=MapPostEffect3ApplyUncontrolledSimpleWriters.PrepareFreshSource(snapshot,raw,route.Preparation.Variant,
                route.SourceEvidence,inputs.Caller,pointerOwners);
            // Both light branches run before LSLarge. The phase boundaries
            // preserve their execution order independently of scalar sorting.
            var cameraWrites=direct.Writes.Concat(simple.ScalarWrites).OrderBy(w=>w.Stage).ToArray();
            var writes=cameraWrites.Where(w=>w.Phase==MapPostEffect3WritePhase.BeforeLight).Concat(light)
                .Concat(cameraWrites.Where(w=>w.Phase==MapPostEffect3WritePhase.AfterLight)).Select(w=>w.CopyAcceptedSnapshot()).ToArray();
            if(!simple.NativeReturnPredicateFromLensPresetPresence||simple.PointerWrites.Count!=2||
                simple.PointerWrites.Any(w=>w.IsNativeNullPointer)||writes.Select(w=>w.Name).Distinct().Count()!=writes.Length)
                throw new InvalidDataException("Owned  writer/pointer population differs from the recovered successful source branch");
            state.ApplyKnownCameraWrites(new(snapshot.FrameNumber,Array.AsReadOnly(writes),route.SourceEvidence));
            lens.ApplyPointerWrites(state);lens.ValidatePointerWrites(state);
            return new(native,snapshot,allocation,defaults,state,route,lens,Array.AsReadOnly(writes),inputs.NativeDispatch);
        }
        catch{lens.Dispose();throw;}
    }

    public MapPostEffect3OwnedFactoryInitialization PrepareFactories(uint actualContextCounter1BF0,
        bool actualMbInputVelocityWrapperPresent,string actualContextEvidence,
        MapPostEffect3ParameterMotionBlurHistory motionHistory,
        MapPostEffect3LensFlareHistory lensHistory,MapPostEffect3ParameterAliasMode actualAliasMode)
    {
        RequireCurrent();ArgumentException.ThrowIfNullOrWhiteSpace(actualContextEvidence);
        if(initialized is not null)throw new InvalidOperationException("This owned fresh Apply has already initialized its selected factories.");
        try
        {
        var prepares=new List<MapPostEffect3LensPresetPrepareBaseResult>();
        void PrepareBase()=>prepares.Add(MapPostEffect3LensPresetPrepareBase.Apply(Lens,State,
            new(FrameNumber,actualContextCounter1BF0,(int)Allocation.ContextWidth,(int)Allocation.ContextHeight,actualContextEvidence),
            sources.LensMath,dispatch));
        // Factory0 prepare/init are . The first actual shared prepare
        // is factory1, followed by four marker-equal prepares in source order.
        PrepareBase();var shaft=MapPostEffect3ParameterLightShaft.PrepareInit(State,Allocation,sources.ShaftMath,actualAliasMode);
        PrepareBase();var dof=MapPostEffect3ParameterDepthOfField.Prepare(State,Allocation,Lens,
            (int)Allocation.ContextWidth,(int)Allocation.ContextHeight,actualAliasMode);
        PrepareBase();var motion=MapPostEffect3ParameterMotionBlur.Prepare(State,Allocation,motionHistory,
            new(actualContextCounter1BF0,actualMbInputVelocityWrapperPresent,actualContextEvidence));
        PrepareBase();var flare=MapPostEffect3ParameterLensFlare.PrepareInit(State,Allocation,Lens,lensHistory,sources.LensMath.Power,actualAliasMode);
        PrepareBase();var composite=MapPostEffect3ParameterComposite.Prepare(State,Allocation,Lens,sources.CompositeMath,
            (int)Allocation.ContextWidth,(int)Allocation.ContextHeight,dispatch);
        var cbs=new List<MapPostEffect3ParameterConstantBuffer>();
        if(shaft.ConstantBuffer is{}s)cbs.Add(s);if(dof.ConstantBuffer is{}d)cbs.Add(d);
        cbs.AddRange(motion.ConstantBuffers);cbs.AddRange(flare.ConstantBuffers);cbs.Add(composite.ConstantBuffer);
        return initialized=new(prepares.AsReadOnly(),shaft,dof,motion,flare,composite,cbs.AsReadOnly());
        }
        catch{failed=true;throw;}
    }

    public MapPostEffect3TemporalAAFirstPassPreparation PrepareTemporalAABefore(
        MapPostEffect3ParameterTemporalAAHistory actualHistory)
    {
        RequireCurrent();
        if(initialized is null)throw new InvalidOperationException("Original pass0 Before follows all factory prepare/init hooks.");
        if(temporalAA is not null)throw new InvalidOperationException("Original pass0 Before already ran in this owned Apply.");
        try{return temporalAA=MapPostEffect3Functor0Preparation.PrepareFirstPass(State,Allocation,actualHistory,(int)Allocation.ContextWidth);}
        catch{failed=true;throw;}
    }
    public MapPostEffect3OwnedFactoryInitialization RequirePreparedFactories()
    {
        RequireCurrent();
        return initialized??throw new InvalidOperationException("The owned frame has not initialized its factories.");
    }

    public void RequireCurrent()
    {ObjectDisposedException.ThrowIf(disposed,this);if(failed)throw new InvalidOperationException("Owned Apply preparation failed; discard this attempt before another frame.");Route.ValidateSameCamera(camera);Lens.ValidatePointerWrites(State);}
    public void Dispose(){if(disposed)return;disposed=true;Lens.Dispose();}
}
