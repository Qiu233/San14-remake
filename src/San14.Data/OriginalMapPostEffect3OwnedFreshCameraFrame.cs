namespace San14.Data;

/// <summary>Source kernels resolved once for an owned original Camera. This
/// current producer supports the recovered TC no-controller ABI.</summary>
public sealed class OriginalMapPostEffect3OwnedFreshCameraSources
{
    private OriginalMapPostEffect3OwnedFreshCameraSources(LoadedOriginalMapPostEffect3LensPresetSource lens,
        LoadedOriginalMapPostEffect3ScalarZeroAllocationSource allocator,
        OriginalMapPostEffect3LensPresetIntermediateMathSources lensMath,
        OriginalMapPostEffect3LightShaftExponential shaftMath,
        OriginalMapPostEffect3ParameterCompositeMathSources compositeMath,
        OriginalMapPostEffect3PresentLightMathSource sunMath)
        =>(Lens,Allocator,LensMath,ShaftMath,CompositeMath,SunMath)=(lens,allocator,lensMath,shaftMath,compositeMath,sunMath);
    public LoadedOriginalMapPostEffect3LensPresetSource Lens{get;}
    public LoadedOriginalMapPostEffect3ScalarZeroAllocationSource Allocator{get;}
    public OriginalMapPostEffect3LensPresetIntermediateMathSources LensMath{get;}
    public OriginalMapPostEffect3LightShaftExponential ShaftMath{get;}
    public OriginalMapPostEffect3ParameterCompositeMathSources CompositeMath{get;}
    public OriginalMapPostEffect3PresentLightMathSource SunMath{get;}
    public static OriginalMapPostEffect3OwnedFreshCameraSources Load(OriginalGameRepository repository,
        OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(camera);
        if(camera.ExecutableLocale!="TC")throw new NotSupportedException("Owned complete  Apply currently admits its recovered TC; caller ABI");
        return new(OriginalMapPostEffect3LensPresetSource.LoadForCamera(repository,camera.Source,"TC"),
            OriginalMapPostEffect3ScalarZeroAllocationSource.Load(repository,"TC"),
            OriginalMapPostEffect3LensPresetIntermediateMathSources.Load(repository,"TC"),
            OriginalMapPostEffect3LightShaftExponential.LoadSseSource(repository,"TC"),
            OriginalMapPostEffect3ParameterCompositeMathSources.Load(repository,"TC"),
            OriginalMapPostEffect3PresentLightMathSource.Load(repository,"TC"));
    }
}

public sealed record OriginalMapPostEffect3OwnedFreshCameraInputs(
    OriginalMapPostEffect3ApplyUncontrolledSimpleInputs Caller,
    OriginalMapPostEffect3ApplyUncontrolledPresentLightInputs? SelectedLight,
    OriginalMapPostEffect3ApplyUncontrolledAbsentLightInputs? AbsentLight,
    OriginalNativeCameraTangentDispatch NativeDispatch);

public sealed record OriginalMapPostEffect3OwnedFactoryInitialization(
    IReadOnlyList<OriginalMapPostEffect3LensPresetPrepareBaseResult> PrepareHooks,
    OriginalMapPostEffect3ParameterLightShaftInitialization Shaft,
    OriginalMapPostEffect3ParameterDepthOfFieldUpdate DepthOfField,
    OriginalMapPostEffect3ParameterMotionBlurUpdate MotionBlur,
    OriginalMapPostEffect3ParameterLensFlareInitialization Lens,
    OriginalMapPostEffect3ParameterCompositeUpdate Composite,
    IReadOnlyList<OriginalMapPostEffect3ParameterConstantBuffer> ConstantBuffers)
{
    public bool CommitsGpuFrameOrHistory=>false;
}

/// <summary>One private scalar bank, actual Lens pins and source writes.
/// Keeps fresh source tail scope explicit; no imported/current tail is guessed.
/// Factory preparation follows source prepare/init order. TemporalAA Before is
/// deliberately later, when its pass is selected. This class does not run GPU
/// passes or commit texture contents, matrices or runtime pair history.</summary>
public sealed class OriginalMapPostEffect3OwnedFreshCameraFrame:IDisposable
{
    private readonly OriginalMapPostEffect3OwnedFreshCameraSources sources;
    private readonly OriginalMapCameraConstructionPacket camera;
    private readonly OriginalNativeCameraTangentDispatch dispatch;
    private bool disposed;
    private bool failed;
    private OriginalMapPostEffect3OwnedFactoryInitialization? initialized;
    private OriginalMapPostEffect3TemporalAAFirstPassPreparation? temporalAA;
    private OriginalMapPostEffect3OwnedFreshCameraFrame(OriginalMapPostEffect3OwnedFreshCameraSources native,
        OriginalMapCameraConstructionPacket snapshot,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3CompleteFreshScalarInitialization? defaults,OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3ApplyMode1Route route,OriginalMapPostEffect3LensPresetParameterOwner lens,
        IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> writes,OriginalNativeCameraTangentDispatch nativeDispatch)
    {sources=native;camera=snapshot;Allocation=allocation;Defaults=defaults;State=state;Route=route;Lens=lens;ApplyWrites=writes;dispatch=nativeDispatch;}
    public OriginalMapPostEffect3AllocationPlan Allocation{get;}
    /// <summary>Present only when this invocation actually initialized fresh storage.</summary>
    public OriginalMapPostEffect3CompleteFreshScalarInitialization? Defaults{get;}
    public OriginalMapPostEffect3ParameterState State{get;}
    public OriginalMapPostEffect3ApplyMode1Route Route{get;}
    public OriginalMapPostEffect3LensPresetParameterOwner Lens{get;}
    public IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> ApplyWrites{get;}
    public long FrameNumber=>State.FrameNumber;
    public string OwnerIdentity=>State.OwnerIdentity;

    public static OriginalMapPostEffect3OwnedFreshCameraFrame Prepare(
        OriginalMapFinalOutputPostEffect3Source source,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3OwnedFreshCameraSources native,
        OriginalMapPostEffect3OwnedFreshCameraInputs inputs,
        OriginalMapPostEffect3ParameterState? retainedScalarState=null)
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
        var lookups=OriginalMapPostEffect3ApplyFreshLutLookups.ConstructFreshSource(snapshot,
            allocation.ContextOwnerIdentity+"/fresh-lookups",native.Lens);
        var route=OriginalMapPostEffect3ApplyMode1Route.ResolveFreshWithoutController(source,allocation.Source,snapshot,lookups,
            allocation.ContextOwnerIdentity+"/mode1");
        if(!route.ExecutesModuleFunctor||route.SelectedModuleFunctorIndex!=0)
            throw new NotSupportedException("Owned full parameters require the actual successful Functor0 route.");
        var raw=OriginalMapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(snapshot,
            allocation.ContextOwnerIdentity+"/fresh-camera-tail");
        var direct=OriginalMapPostEffect3ApplyUncontrolledDirectWriters.PrepareFreshSource(snapshot,raw,route.Preparation.Variant,route.SourceEvidence);
        var light=inputs.SelectedLight is{}selected?
            OriginalMapPostEffect3ApplyUncontrolledPresentLightWriters.PrepareFreshSource(route.Preparation,snapshot,selected,
                native.SunMath,native.LensMath.Tangent,inputs.NativeDispatch):
            OriginalMapPostEffect3ApplyUncontrolledAbsentLightWriters.Prepare(route.Preparation,inputs.AbsentLight!);
        if(inputs.AbsentLight is{}absent&&absent.FrameNumber!=snapshot.FrameNumber)
            throw new InvalidDataException("Absent light lookup belongs to another Camera frame.");
        var math=OriginalMapPostEffect3LensPresetIntermediateProducer.PrepareFreshSource(snapshot,native.Lens,native.LensMath,inputs.NativeDispatch);
        var packet=OriginalMapPostEffect3LensPresetPreparedIntermediatePacket.ConstructFreshSource(math);
        var defaults=retainedScalarState is null?
            OriginalMapPostEffect3ApplyCompleteInitialScalars.CreateFresh(allocation.Source,snapshot.FrameNumber,
                allocation.ContextOwnerIdentity,native.Allocator):null;
        var state=retainedScalarState?.CloneForNewFrame(snapshot.FrameNumber)??defaults!.State;
        state.BindOwnedAllocation(allocation);
        var lens=native.Lens.PinCurrentParameters(packet,allocation.ContextOwnerIdentity+"/lens-pins");
        try
        {
            var pointerOwners=new OriginalMapPostEffect3ApplyUncontrolledLensPresetOwners(snapshot.FrameNumber,true,
                new(snapshot.FrameNumber,lens.OwnerIdentity+"/split"),new(snapshot.FrameNumber,lens.OwnerIdentity+"/intermediate"),lens.SourceEvidence);
            var simple=OriginalMapPostEffect3ApplyUncontrolledSimpleWriters.PrepareFreshSource(snapshot,raw,route.Preparation.Variant,
                route.SourceEvidence,inputs.Caller,pointerOwners);
            // Both light branches run before LSLarge. The phase boundaries
            // preserve their execution order independently of scalar sorting.
            var cameraWrites=direct.Writes.Concat(simple.ScalarWrites).OrderBy(w=>w.Stage).ToArray();
            var writes=cameraWrites.Where(w=>w.Phase==OriginalMapPostEffect3WritePhase.BeforeLight).Concat(light)
                .Concat(cameraWrites.Where(w=>w.Phase==OriginalMapPostEffect3WritePhase.AfterLight)).Select(w=>w.CopyAcceptedSnapshot()).ToArray();
            if(!simple.NativeReturnPredicateFromLensPresetPresence||simple.PointerWrites.Count!=2||
                simple.PointerWrites.Any(w=>w.IsNativeNullPointer)||writes.Select(w=>w.Name).Distinct().Count()!=writes.Length)
                throw new InvalidDataException("Owned  writer/pointer population differs from the recovered successful source branch");
            state.ApplyKnownCameraWrites(new(snapshot.FrameNumber,Array.AsReadOnly(writes),route.SourceEvidence));
            lens.ApplyPointerWrites(state);lens.ValidatePointerWrites(state);
            return new(native,snapshot,allocation,defaults,state,route,lens,Array.AsReadOnly(writes),inputs.NativeDispatch);
        }
        catch{lens.Dispose();throw;}
    }

    public OriginalMapPostEffect3OwnedFactoryInitialization PrepareFactories(uint actualContextCounter1BF0,
        bool actualMbInputVelocityWrapperPresent,string actualContextEvidence,
        OriginalMapPostEffect3ParameterMotionBlurHistory motionHistory,
        OriginalMapPostEffect3LensFlareHistory lensHistory,OriginalMapPostEffect3ParameterAliasMode actualAliasMode)
    {
        RequireCurrent();ArgumentException.ThrowIfNullOrWhiteSpace(actualContextEvidence);
        if(initialized is not null)throw new InvalidOperationException("This owned fresh Apply has already initialized its selected factories.");
        try
        {
        var prepares=new List<OriginalMapPostEffect3LensPresetPrepareBaseResult>();
        void PrepareBase()=>prepares.Add(OriginalMapPostEffect3LensPresetPrepareBase.Apply(Lens,State,
            new(FrameNumber,actualContextCounter1BF0,(int)Allocation.ContextWidth,(int)Allocation.ContextHeight,actualContextEvidence),
            sources.LensMath,dispatch));
        // Factory0 prepare/init are . The first actual shared prepare
        // is factory1, followed by four marker-equal prepares in source order.
        PrepareBase();var shaft=OriginalMapPostEffect3ParameterLightShaft.PrepareInit(State,Allocation,sources.ShaftMath,actualAliasMode);
        PrepareBase();var dof=OriginalMapPostEffect3ParameterDepthOfField.Prepare(State,Allocation,Lens,
            (int)Allocation.ContextWidth,(int)Allocation.ContextHeight,actualAliasMode);
        PrepareBase();var motion=OriginalMapPostEffect3ParameterMotionBlur.Prepare(State,Allocation,motionHistory,
            new(actualContextCounter1BF0,actualMbInputVelocityWrapperPresent,actualContextEvidence));
        PrepareBase();var flare=OriginalMapPostEffect3ParameterLensFlare.PrepareInit(State,Allocation,Lens,lensHistory,sources.LensMath.Power,actualAliasMode);
        PrepareBase();var composite=OriginalMapPostEffect3ParameterComposite.Prepare(State,Allocation,Lens,sources.CompositeMath,
            (int)Allocation.ContextWidth,(int)Allocation.ContextHeight,dispatch);
        var cbs=new List<OriginalMapPostEffect3ParameterConstantBuffer>();
        if(shaft.ConstantBuffer is{}s)cbs.Add(s);if(dof.ConstantBuffer is{}d)cbs.Add(d);
        cbs.AddRange(motion.ConstantBuffers);cbs.AddRange(flare.ConstantBuffers);cbs.Add(composite.ConstantBuffer);
        return initialized=new(prepares.AsReadOnly(),shaft,dof,motion,flare,composite,cbs.AsReadOnly());
        }
        catch{failed=true;throw;}
    }

    public OriginalMapPostEffect3TemporalAAFirstPassPreparation PrepareTemporalAABefore(
        OriginalMapPostEffect3ParameterTemporalAAHistory actualHistory)
    {
        RequireCurrent();
        if(initialized is null)throw new InvalidOperationException("Original pass0 Before follows all factory prepare/init hooks.");
        if(temporalAA is not null)throw new InvalidOperationException("Original pass0 Before already ran in this owned Apply.");
        try{return temporalAA=OriginalMapPostEffect3Functor0Preparation.PrepareFirstPass(State,Allocation,actualHistory,(int)Allocation.ContextWidth);}
        catch{failed=true;throw;}
    }
    public OriginalMapPostEffect3OwnedFactoryInitialization RequirePreparedFactories()
    {
        RequireCurrent();
        return initialized??throw new InvalidOperationException("The owned frame has not initialized its factories.");
    }

    public void RequireCurrent()
    {ObjectDisposedException.ThrowIf(disposed,this);if(failed)throw new InvalidOperationException("Owned Apply preparation failed; discard this attempt before another frame.");Route.ValidateSameCamera(camera);Lens.ValidatePointerWrites(State);}
    public void Dispose(){if(disposed)return;disposed=true;Lens.Dispose();}
}
