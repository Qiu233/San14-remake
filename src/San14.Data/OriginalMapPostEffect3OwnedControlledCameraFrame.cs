using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>CPU sources for the actual EB746A controlled branch. This owner
/// deliberately creates fresh all-ten-factory scalar storage; it does not
/// import an E71690/Functor0 frame or prove controller texture readiness.</summary>
public sealed class OriginalMapPostEffect3OwnedControlledCameraSources
{
    private OriginalMapPostEffect3OwnedControlledCameraSources(
        LoadedOriginalMapPostEffect3ScalarZeroAllocationSource allocator,OriginalSkyWeatherNativePowerSource power,
        LoadedOriginalMapPostEffect3ControllerExecuteSource execute,OriginalMapPostEffect3ControlledToneSource tone,
        LoadedOriginalMapPostEffect3ControlledCameraSource camera,LoadedOriginalMapPostEffect3ControlledOpticsSource optics,
        LoadedOriginalMapPostEffect3RetainedResizeSource resize)
        =>(Allocator,Power,Execute,Tone,Camera,Optics,Resize)=(allocator,power,execute,tone,camera,optics,resize);
    public LoadedOriginalMapPostEffect3ScalarZeroAllocationSource Allocator{get;}
    public OriginalSkyWeatherNativePowerSource Power{get;}
    public LoadedOriginalMapPostEffect3ControllerExecuteSource Execute{get;}
    public OriginalMapPostEffect3ControlledToneSource Tone{get;}
    public LoadedOriginalMapPostEffect3ControlledCameraSource Camera{get;}
    public LoadedOriginalMapPostEffect3ControlledOpticsSource Optics{get;}
    public LoadedOriginalMapPostEffect3RetainedResizeSource Resize{get;}
    public static OriginalMapPostEffect3OwnedControlledCameraSources Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(OriginalMapPostEffect3ScalarZeroAllocationSource.Load(repository,locale),
            OriginalSkyWeatherNativePowerSource.Load(repository,locale),OriginalMapPostEffect3ControllerExecuteSource.Load(repository,locale),
            OriginalMapPostEffect3ControlledToneSource.Load(repository,locale),
            OriginalDeferredSamplerSources.ReadControlledCameraSource(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale)),
            OriginalMapPostEffect3ControlledOpticsSource.Load(repository,locale),
            OriginalMapPostEffect3RetainedResizeSource.Load(repository,locale));
    }
}

/// <summary>Actual EB caller inputs whose producers are outside the Camera
/// fields recovered here. GammaDivisor is the real xmm13, LdrIntensity is
/// the selected mix in xmm9, and GatherSizeType is the real localC0. The two
/// viewports and adaptation lock predicate are actual caller values. The
/// Viewport scalar consumes only selected Camera1548/154C and source-zero XY;
/// CompositeLastViewport consumes the full selected/fallback rectangle.</summary>
public sealed record OriginalMapPostEffect3ControlledCameraCaller(long FrameNumber,
    int ActualWorkingSceneWidth,int ActualWorkingSceneHeight,int ActualFxaaMode,
    float ActualGammaDivisorXmm13,float ActualDeltaTime,float ActualLdrLutIntensityXmm9,
    int ActualGatherSizeTypeC0,bool ActualAdaptationLockPredicate,
    Vector4 ActualViewport1540,Vector4 ActualCompositeLastViewport,int? ActualAb5D10Result,
    float ActualContextHeatScale254,string SourceEvidence);

/// <summary>The actual selected HDR wrapper and its native metadata snapshot.
/// A caller supplies the wrapper identity after its real selection producer.
/// Null metadata records an actual null +28 pointer; it is not inferred from
/// G1T or from the successful593 handle. This receipt makes no GPU claim.</summary>
public sealed class OriginalMapPostEffect3ControlledSelectedHdrTexture
{
    private readonly byte[]? metadata;
    private OriginalMapPostEffect3ControlledSelectedHdrTexture(OriginalMapPostEffect3CurrentLutLookup lookup,
        OriginalMapPostEffect3ApplyView view,ReadOnlyMemory<byte>? actualMetadata,string evidence)
    {Lookup=lookup;View=view;metadata=actualMetadata?.ToArray();SourceEvidence=evidence;}
    public OriginalMapPostEffect3CurrentLutLookup Lookup{get;}
    public OriginalMapPostEffect3ApplyView View{get;}
    public ReadOnlyMemory<byte>? NativeMetadata28=>metadata is null?(ReadOnlyMemory<byte>?)null:new ReadOnlyMemory<byte>(metadata.ToArray());
    public string SourceEvidence{get;}
    public bool ProvesGpuTextureReady=>false;
    public string TextureSourceIdentity
    {
        get
        {
            var binding=Lookup.SelectedStaticTexture!;
            return binding.Definitions.Source.ResourceUri+"@"+binding.Definitions.Source.DecodedSha256+
                $"#Static/{binding.Definition.ObjectId:X8};"+binding.Resource.Source.ResourceUri+"@"+
                binding.Resource.Source.DecodedSha256+$"#TID/{binding.ImageIndex}";
        }
    }
    public static OriginalMapPostEffect3ControlledSelectedHdrTexture CaptureActualStaticSelection(
        OriginalMapPostEffect3CurrentLutLookup actualPreparedHdrLookup,OriginalMapPostEffect3ApplyView actualSelectedWrapper,
        ReadOnlyMemory<byte>? actualNativeWrapper28MetadataChain,string actualSelectionAndMetadataProducerEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualPreparedHdrLookup);ArgumentNullException.ThrowIfNull(actualSelectedWrapper);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualSelectedWrapper.OwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualSelectionAndMetadataProducerEvidence);
        if(!actualPreparedHdrLookup.IsHdrLookup||!actualPreparedHdrLookup.LookupSucceeded||
            !actualPreparedHdrLookup.ProvesPreparedCameraHandleTransfer||actualPreparedHdrLookup.SelectedStaticTexture is null||
            actualSelectedWrapper.FrameNumber!=actualPreparedHdrLookup.FrameNumber)
            throw new InvalidDataException("Controlled Tone needs the same actual prepared HDR lookup and selected Static wrapper.");
        return new(actualPreparedHdrLookup,actualSelectedWrapper,actualNativeWrapper28MetadataChain,
            actualSelectionAndMetadataProducerEvidence+";explicit-selected-Static-wrapper/native28-snapshot;not-a-GPU-readiness-proof");
    }
}

/// <summary>Same-frame successful or explicitly absent D59120 light lookup.
/// The positive branch consumes this actual light's F80 vector. Absence has
/// its own frozen native fallback; no F0 projection is used.</summary>
public sealed class OriginalMapPostEffect3ControlledLightLookup
{
    private readonly OriginalMapCameraConstructionPacket camera;
    private readonly OriginalMapSourceCreatedLightingPacket? light;
    private OriginalMapPostEffect3ControlledLightLookup(OriginalMapCameraConstructionPacket camera,
        OriginalMapSourceCreatedLightingPacket? light,string owner,string evidence)
    {this.camera=camera.CopyAcceptedSnapshot();this.light=light?.CopyAcceptedSnapshot();OwnerIdentity=owner;SourceEvidence=evidence;}
    public long FrameNumber=>camera.FrameNumber;
    public string OwnerIdentity{get;}
    public string SourceEvidence{get;}
    public bool FoundLight=>light is not null;
    public static OriginalMapPostEffect3ControlledLightLookup CaptureActualLookup(OriginalMapCameraConstructionPacket camera,
        OriginalMapSourceCreatedLightingPacket? actualSelectedLight,string actualLookupOwner,string actualLookupEvidence)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentException.ThrowIfNullOrWhiteSpace(actualLookupOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualLookupEvidence);
        if(camera.FrameNumber<=0)throw new InvalidDataException("Controlled light lookup needs its actual owned frame.");
        actualSelectedLight?.ValidateCurrentSourceFrame();
        if(actualSelectedLight is{}light&&(light.FrameNumber!=camera.FrameNumber||
            light.SourceIdentity.CameraObjectId!=camera.Source.Camera.ObjectId||
            !light.PreparedCameraPrefix.Span.SequenceEqual(camera.Prefix.Span)||light.LocalLightData.Length!=0x10C0))
            throw new InvalidDataException("Controlled light lookup does not belong to this exact prepared Camera.");
        return new(camera,actualSelectedLight,actualLookupOwner,actualLookupEvidence);
    }
    internal ReadOnlyMemory<byte>? ReadSelectedLight(OriginalMapCameraConstructionPacket currentCamera)
    {
        camera.RequireEquivalentCurrentPostEffectScalars(currentCamera);
        if(currentCamera.FrameNumber!=FrameNumber||currentCamera.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            currentCamera.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            currentCamera.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256||
            !camera.Prefix.Span.SequenceEqual(currentCamera.Prefix.Span))
            throw new InvalidDataException("Controlled light selection belongs to another actual Camera frame.");
        return light?.LocalLightData;
    }
}

public sealed record OriginalMapPostEffect3OwnedControlledCameraInputs(
    OriginalMapSeasonPreparedLutTransfer PreparedTransfer,OriginalMapPostEffect3OwnedMainLutSelector MainSelector,
    OriginalMapPostEffect3ApplyFreshLutLookups Lookups,OriginalMapPostEffect3ControlledSelectedHdrTexture SelectedHdr,
    OriginalMapPostEffect3ControlledLightLookup Light,OriginalMapPostEffect3ControlledCameraCaller Caller,
    ReadOnlyMemory<byte> ActualPreparedAux460,string ActualAuxEvidence,
    OriginalMapPostEffect3ControllerScalarSnapshot? ActualPostApplyControllerScalars,
    OriginalMapPostEffect3ControllerExecuteCaller ActualExecuteCaller,OriginalNativeLogDispatch ActualOwnedLogDispatch)
{
    /// <summary>Actual consumed field receipt. When present the historical
    /// complete460 input is not read; unused Aux bytes are not required.</summary>
    public OriginalMapPostEffect3ControlledAuxFields? ActualAuxFields{get;init;}
    public OriginalMapPostEffect3ControlledFreshCaller? ActualFreshCaller{get;init;}
}

/// <summary>Independent fresh owned Functor1 scalar preparation. Factory
/// defaults, recovered controlled Camera/light writes, metadata4 and actual
/// E22860 writes10 are source-ordered. Controller optics/ghost preparation,
/// factory Before hooks, resources and GPU execution remain separate gates.</summary>
public sealed class OriginalMapPostEffect3OwnedControlledCameraFrame
{
    private readonly OriginalMapCameraConstructionPacket camera;
    private readonly OriginalMapPostEffect3ParameterState? retainedState;
    private OriginalMapPostEffect3OwnedControlledCameraFrame(OriginalMapCameraConstructionPacket camera,
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3CompleteFreshScalarInitialization defaults,
        OriginalMapPostEffect3ApplyMode1Route route,OriginalMapPostEffect3ControllerScalarOwner controller,
        OriginalMapPostEffect3ControlledToneMetadata tone,OriginalMapPostEffect3ApplyDerivedScalarPlan derived,
        IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> writes,OriginalMapPostEffect3ControlledOpticsPreparation optics,string evidence,
        OriginalMapPostEffect3ParameterState? retainedState=null)
    {this.camera=camera.CopyAcceptedSnapshot();Allocation=allocation;Defaults=defaults;Route=route;Controller=controller;
        Tone=tone;Derived=derived;ApplyWrites=writes;Optics=optics;SourceEvidence=evidence;this.retainedState=retainedState;}
    public OriginalMapPostEffect3AllocationPlan Allocation{get;}
    public OriginalMapPostEffect3CompleteFreshScalarInitialization Defaults{get;}
    public OriginalMapPostEffect3ParameterState State=>retainedState??Defaults.State;
    public OriginalMapPostEffect3ApplyMode1Route Route{get;}
    public OriginalMapPostEffect3ControllerScalarOwner Controller{get;}
    public OriginalMapPostEffect3ControllerScalarInvocation Invocation=>Route.ControllerInvocation!;
    public OriginalMapPostEffect3ControlledToneMetadata Tone{get;}
    public OriginalMapPostEffect3ApplyDerivedScalarPlan Derived{get;}
    public OriginalMapPostEffect3ControlledOpticsPreparation Optics{get;}
    public IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> ApplyWrites{get;}
    public long FrameNumber=>State.FrameNumber;
    public string SourceEvidence{get;}
    public bool IsCompleteForExecution=>false;
    public bool ProvesGpuExecution=>false;
    public string RequiredRemainingInputs=>"controller texture and ghost-buffer device owners; "+
        "selected factory Before hooks/constant buffers; actual resource uploads and pass execution";
    public void RequireCompleteForExecution()=>throw new NotSupportedException(RequiredRemainingInputs);
    public void ValidateSameCamera(OriginalMapCameraConstructionPacket actualCamera)=>Route.ValidateSameCamera(actualCamera);
    /// <summary>Immutable accepted source snapshot. This accessor does not
    /// acknowledge an execution, a Host return or any device contents.</summary>
    public OriginalMapCameraConstructionPacket CaptureAcceptedCamera()=>camera.CopyAcceptedSnapshot();

    public static OriginalMapPostEffect3OwnedControlledCameraFrame Prepare(
        OriginalMapFinalOutputPostEffect3Source source,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3OwnedControlledCameraSources native,
        OriginalMapPostEffect3OwnedControlledCameraInputs inputs)
        =>PrepareCore(source,allocation,camera,native,inputs,null,null);

    public static OriginalMapPostEffect3OwnedControlledCameraFrame PrepareRetained(
        OriginalMapFinalOutputPostEffect3Source source,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3OwnedControlledCameraSources native,
        OriginalMapPostEffect3OwnedControlledCameraInputs inputs,OriginalMapPostEffect3OwnedControlledCameraFrame previous,
        OriginalMapPostEffect3Functor1ModuleState module)
        =>PrepareCore(source,allocation,camera,native,inputs,previous,module);

    private static OriginalMapPostEffect3OwnedControlledCameraFrame PrepareCore(
        OriginalMapFinalOutputPostEffect3Source source,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3OwnedControlledCameraSources native,
        OriginalMapPostEffect3OwnedControlledCameraInputs inputs,OriginalMapPostEffect3OwnedControlledCameraFrame? previous,
        OriginalMapPostEffect3Functor1ModuleState? module)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(native);ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.PreparedTransfer);ArgumentNullException.ThrowIfNull(inputs.MainSelector);
        ArgumentNullException.ThrowIfNull(inputs.Lookups);ArgumentNullException.ThrowIfNull(inputs.SelectedHdr);
        ArgumentNullException.ThrowIfNull(inputs.Light);ArgumentNullException.ThrowIfNull(inputs.Caller);
        ArgumentNullException.ThrowIfNull(inputs.ActualExecuteCaller);
        if(inputs.ActualPostApplyControllerScalars is null&&inputs.ActualFreshCaller is null)
            throw new InvalidDataException("Controlled preparation needs its actual controller snapshot or typed fresh current writers.");
        ArgumentException.ThrowIfNullOrWhiteSpace(inputs.ActualAuxEvidence);
        var snapshot=camera.CopyAcceptedSnapshot();inputs.PreparedTransfer.ValidateSameCamera(snapshot);inputs.Lookups.ValidateSameCamera(snapshot);
        var hdr=inputs.Lookups.ResolvedHdrLookup??throw new InvalidDataException("Controlled frame requires its actual prepared HDR lookup.");
        hdr.ValidateOwnedMainSelector(inputs.MainSelector);inputs.Lookups.ResolvedLdrLookup?.ValidateOwnedMainSelector(inputs.MainSelector);
        if(camera.ExecutableLocale!=native.Camera.Native.Locale||camera.ExecutableLocale!=native.Execute.Native.Locale||
            camera.ExecutableLocale!=native.Optics.Native.Locale||camera.ExecutableLocale!=native.Allocator.Locale||
            camera.ExecutableLocale!=native.Power.Locale||camera.ExecutableLocale!=native.Tone.Locale||!inputs.Lookups.HdrPrimary1828Present||
            !ReferenceEquals(hdr.PreparedTransfer,inputs.PreparedTransfer)||!ReferenceEquals(hdr,inputs.SelectedHdr.Lookup)||
            inputs.Lookups.ResolvedLdrLookup is{}ldr&&!ReferenceEquals(ldr.PreparedTransfer,inputs.PreparedTransfer)||
            inputs.Caller.FrameNumber!=snapshot.FrameNumber||inputs.ActualExecuteCaller.FrameNumber!=snapshot.FrameNumber||
            inputs.ActualPostApplyControllerScalars is{} suppliedController&&
                (suppliedController.FrameNumber!=snapshot.FrameNumber||suppliedController.ControllerOwnerIdentity!=allocation.ContextOwnerIdentity)||
            inputs.ActualExecuteCaller.ActualSelectedFunctorIndex!=1)
            throw new InvalidDataException("Controlled preparation requires this exact current Camera/Main transfer/selected HDR/controller frame and source default1A8.");
        var aux=inputs.ActualAuxFields??OriginalMapPostEffect3ControlledAuxFields.CaptureActualPrepared(
            inputs.ActualPreparedAux460.Span,snapshot.FrameNumber,"explicit-selected-View",inputs.ActualAuxEvidence);
        aux.ValidateFrame(snapshot.FrameNumber);
        var tail=OriginalMapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(snapshot,allocation.ContextOwnerIdentity+"/controlled-camera-tail");
        var known=OriginalMapPostEffect3ApplyRecipe.PrepareFreshSourceKnownCameraWrites(snapshot,tail,aux);
        var derived=previous is null?OriginalMapPostEffect3ApplyDerivedScalars.PrepareFreshSource(snapshot,tail,
            OriginalMapPostEffect3ApplyDerivedScalars.ConstructFreshDimensions(snapshot.FrameNumber,allocation.ContextOwnerIdentity,
                checked((int)allocation.ContextWidth),checked((int)allocation.ContextHeight),native.Power),
            inputs.Caller.ActualWorkingSceneWidth,inputs.Caller.ActualWorkingSceneHeight,native.Power):
            native.Resize.PrepareRetainedCurrentDerived(snapshot,tail,
                OriginalMapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(previous.camera,previous.State.OwnerIdentity),
                previous.Derived,inputs.Caller.ActualWorkingSceneWidth,inputs.Caller.ActualWorkingSceneHeight,native.Power);
        if(inputs.ActualFreshCaller is{} fresh)
        {
            fresh.ValidateSameCamera(snapshot);
            if(fresh.GatherSizeTypeC0!=inputs.Caller.ActualGatherSizeTypeC0||fresh.AdaptationLock!=inputs.Caller.ActualAdaptationLockPredicate||
                fresh.LdrIntensityXmm9!=inputs.Caller.ActualLdrLutIntensityXmm9||
                inputs.ActualExecuteCaller.Argument3R8B!=fresh.FreshControllerArgument3||
                inputs.ActualExecuteCaller.Argument5EntryRsp28!=fresh.FreshApplyArgument5)
                throw new InvalidDataException("Controlled fresh caller differs from its actual source fields/creation/reset branch.");
        }
        var extra=native.Camera.PrepareCameraAndLightWrites(snapshot,inputs.MainSelector,inputs.Caller,inputs.Light,derived,
            aux);
        if(previous is not null)
        {
            if(module is null||!ReferenceEquals(module.Allocation,allocation)||!ReferenceEquals(previous.Allocation,allocation)||
                previous.Invocation.NativeControllerReturn!=true||snapshot.FrameNumber!=previous.FrameNumber+1)
                throw new InvalidDataException("Retained Camera requires its exact preceding returned controlled module.");
            RequireUnchangedRetainedOptics(previous,snapshot,derived,inputs);
        }
        var defaults=previous?.Defaults??OriginalMapPostEffect3ApplyCompleteInitialScalars.CreateFresh(allocation.Source,snapshot.FrameNumber,
            allocation.ContextOwnerIdentity,native.Allocator);
        var state=previous is null?defaults.State:module!.CloneRetainedScalarState(previous.State,snapshot.FrameNumber);
        state.BindOwnedAllocation(allocation);
        var optics=previous is null?native.Optics.PrepareFreshCurrentCamera(snapshot,derived,allocation.ContextOwnerIdentity,inputs.ActualOwnedLogDispatch):
            OriginalMapPostEffect3ControlledOpticsPreparation.RetainUnchanged(previous.Optics,state,allocation);
        if(previous is null)
        {
            state.ApplyKnownCameraWrites(new(snapshot.FrameNumber,derived.InitializationWrites,native.Camera.SourceEvidence));
            optics.ApplyInitializationToCurrentState(state,allocation);
        }
        var currentWrites=known.Writes.Concat(derived.ApplyWrites).Concat(extra).OrderBy(w=>w.CallSiteRva).ToArray();
        var writes=(previous is null?derived.InitializationWrites.Concat(optics.InitializationWrites):
            Enumerable.Empty<OriginalMapPostEffect3ApplyValueWrite>()).Concat(currentWrites)
            .Select(w=>w.CopyAcceptedSnapshot()).ToArray();
        state.ApplyKnownCameraWrites(new(snapshot.FrameNumber,Array.AsReadOnly(currentWrites),native.Camera.SourceEvidence));
        var tone=native.Tone.CaptureOwnedTextureMetadata(state,allocation,new(snapshot.FrameNumber,allocation.ContextOwnerIdentity),
            inputs.SelectedHdr.View.OwnerIdentity,inputs.SelectedHdr.TextureSourceIdentity,inputs.SelectedHdr.NativeMetadata28,
            inputs.SelectedHdr.SourceEvidence);
        native.Tone.Apply(state,allocation,tone);
        if(previous is null)optics.ApplyToCurrentState(state,allocation);
        optics.ValidateCurrent(state,allocation);
        var actualController=inputs.ActualPostApplyControllerScalars??inputs.ActualFreshCaller!.CapturePostApplyController(derived,optics);
        if(actualController.EnableDofA4!=inputs.Caller.ActualGatherSizeTypeC0||
            (actualController.SpriteA9!=0)!=derived.SpriteDofA9||
            actualController.BloomRadiusBitsDC!=BitConverter.SingleToUInt32Bits(derived.BloomAntiFlickerDc)||
            (actualController.BloomEnableAA!=0)!=derived.BloomAntiFlickerAa)
            throw new InvalidDataException("Actual post-Apply controller fields differ from the recovered same-Camera A4/A9/DC/AA writers.");
        // Ten E22860 fields are from the actual post-Apply controller snapshot.
        // Fresh constructor flags must not overwrite those current caller facts.
        var controller=previous?.Controller??native.Execute.ConstructFreshScalarOwner(allocation.Source,allocation.ContextOwnerIdentity);
        controller.AcceptCurrentSnapshot(actualController);
        var invocation=controller.BeginExecute(state,inputs.ActualExecuteCaller);
        var route=OriginalMapPostEffect3ApplyMode1Route.ResolveOwnedControlled(source,allocation.Source,snapshot,inputs.Lookups,
            optics,invocation,allocation.ContextOwnerIdentity+"/controlled-mode1");
        return new(snapshot,allocation,defaults,route,controller,tone,derived,Array.AsReadOnly(writes.Concat(optics.Writes).ToArray()),optics,
            defaults.SourceEvidence+";"+native.Camera.SourceEvidence+";"+inputs.PreparedTransfer.SourceEvidence+";"+tone.SourceEvidence+
            ";"+optics.SourceEvidence+";CPU-Functor1-bank;no-E71690-writes;factory-and-device-execution-remain-separate",
            previous is null?null:state);
    }

    private static void RequireUnchangedRetainedOptics(OriginalMapPostEffect3OwnedControlledCameraFrame previous,
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyDerivedScalarPlan derived,
        OriginalMapPostEffect3OwnedControlledCameraInputs inputs)
    {
        var before=OriginalMapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(previous.camera,previous.State.OwnerIdentity);
        var now=OriginalMapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(camera,previous.State.OwnerIdentity);
        int[] fields=[0x12FC,0x1300,0x1304,0x131C,0x7A8,0x7AC];
        var oldTail=OriginalMapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(previous.camera,previous.State.OwnerIdentity);
        var newTail=OriginalMapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(camera,previous.State.OwnerIdentity);
        if(fields.Any(o=>before.Word(o)!=now.Word(o))||oldTail.Int(0x1344)!=newTail.Int(0x1344)||
            oldTail.Int(0x1348)!=newTail.Int(0x1348)||oldTail.Word(0x1320)!=newTail.Word(0x1320)||
            previous.camera.ExtraScalars.LimbDarkeningWeight7B0!=camera.ExtraScalars.LimbDarkeningWeight7B0||
            previous.Derived.EffectiveWidth80!=derived.EffectiveWidth80||previous.Derived.EffectiveHeight84!=derived.EffectiveHeight84||
            previous.Derived.AspectRatioF4!=derived.AspectRatioF4||previous.Derived.SensorHeightRatioF0!=derived.SensorHeightRatioF0||
            inputs.ActualOwnedLogDispatch!=OriginalNativeLogDispatch.Sse)
            throw new NotSupportedException("Changed retained optics needs its actual AF/A1/setter source producer; this owner admits the unchanged current profile.");
    }
}

public sealed class LoadedOriginalMapPostEffect3ControlledCameraSource
{
    internal LoadedOriginalMapPostEffect3ControlledCameraSource(LoadedOriginalDeferredSamplerNativeSource native,
        uint lockedAdaptation,uint sourceHdrTvScale,byte[] defaultLightProjection,uint gatherMaximum,uint gatherFactor,
        uint lightThreshold,uint lightLimit,uint lightEpsilon,uint lightScale,string evidence)
    {Native=native;lockedAdaptationBits=lockedAdaptation;hdrTvScaleBits=sourceHdrTvScale;defaultLightBytes=defaultLightProjection;
        gatherMaximumBits=gatherMaximum;gatherFactorBits=gatherFactor;lightThresholdBits=lightThreshold;lightLimitBits=lightLimit;
        lightEpsilonBits=lightEpsilon;lightScaleBits=lightScale;SourceEvidence=evidence;}
    private readonly uint lockedAdaptationBits,hdrTvScaleBits,gatherMaximumBits,gatherFactorBits,
        lightThresholdBits,lightLimitBits,lightEpsilonBits,lightScaleBits;
    private readonly byte[] defaultLightBytes;
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public uint LockedAdaptationBits => lockedAdaptationBits;
    internal IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> PrepareCameraAndLightWrites(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3OwnedMainLutSelector selector,
        OriginalMapPostEffect3ControlledCameraCaller caller,OriginalMapPostEffect3ControlledLightLookup light,
        OriginalMapPostEffect3ApplyDerivedScalarPlan derived,OriginalMapPostEffect3ControlledAuxFields aux)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.SourceEvidence);
        if(caller.FrameNumber!=camera.FrameNumber||selector.FrameNumber!=camera.FrameNumber||Native.Locale!=camera.ExecutableLocale||
            !float.IsFinite(caller.ActualGammaDivisorXmm13)||caller.ActualGammaDivisorXmm13==0||
            !float.IsFinite(caller.ActualDeltaTime)||!float.IsFinite(caller.ActualLdrLutIntensityXmm9)||!float.IsFinite(caller.ActualContextHeatScale254)||
            new[]{caller.ActualViewport1540.X,caller.ActualViewport1540.Y,caller.ActualViewport1540.Z,caller.ActualViewport1540.W,
                caller.ActualCompositeLastViewport.X,caller.ActualCompositeLastViewport.Y,caller.ActualCompositeLastViewport.Z,
                caller.ActualCompositeLastViewport.W}.Any(v=>!float.IsFinite(v)))
            throw new InvalidDataException("Controlled Camera writers require the actual finite same-frame caller arguments.");
        var raw=OriginalMapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(camera,"controlled-field-source-only");
        var tail=OriginalMapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(camera,"controlled-field-source-only/tail");
        var writes=new List<OriginalMapPostEffect3ApplyValueWrite>();
        string evidence=SourceEvidence+";"+camera.FrameProvenance+";"+caller.SourceEvidence+";"+light.SourceEvidence;
        uint Code(uint rva)=>(uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Native.Locale,"OriginalMapPostEffect3OwnedControlledCameraFrame.cs",(int)rva);
        void W(string name,uint at,uint helper,params uint[] words)
        {byte[] bytes=new byte[checked(words.Length*4)];for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),words[i]);writes.Add(new(name,Code(at),Code(helper),bytes,evidence));}
        void F(string name,uint at,uint helper,params float[] values)=>W(name,at,helper,values.Select(BitConverter.SingleToUInt32Bits).ToArray());
        W("AdaptedScaleLight",0xEB7683,0xE24FA0,caller.ActualAdaptationLockPredicate?lockedAdaptationBits:
            BinaryPrimitives.ReadUInt32LittleEndian(camera.Prefix.Span[0x1C4..]));
        W("AdaptedScaleDark",0xEB7683,0xE24FA0,caller.ActualAdaptationLockPredicate?lockedAdaptationBits:
            BinaryPrimitives.ReadUInt32LittleEndian(camera.Prefix.Span[0x1C8..]));
        float height=derived.EffectiveHeight84;
        float gather=MathF.Min(BitConverter.UInt32BitsToSingle(gatherMaximumBits),
            D(M(A(A(caller.ActualGatherSizeTypeC0,caller.ActualGatherSizeTypeC0),1),BitConverter.UInt32BitsToSingle(gatherFactorBits)),height));
        W("GatherSizeType",0xEB75B3,0xE253F0,unchecked((uint)caller.ActualGatherSizeTypeC0));
        F("GatherMaxCoC",0xEB75B3,0xE253F0,gather);
        F("DOF2HeatSimmerParams",0xEB760F,0xE25390,tail.Float(0x13C0),tail.Float(0x13C4),
            M(tail.Float(0x13C8),caller.ActualContextHeatScale254));
        W("EnableSeparateHdrLut",0xEB78E5,0xE25C40,
            aux.SeparateHdrMode440==2&&selector.Selector==2?1u:0u);
        W("HdrTvNit",0xEB78E5,0xE25C40,tail.Word(0x13CC));W("BaseTvNit",0xEB78E5,0xE25C40,tail.Word(0x13D0));
        F("LdrLutIntensity",0xEB794F,0xE25860,caller.ActualLdrLutIntensityXmm9);
        W("GammaEnable",0xEB7962,0xE25320,1);F("GammaValue",0xEB7962,0xE25320,D(1,caller.ActualGammaDivisorXmm13));
        uint CameraSourceWord(int ordinal,uint hash,uint type)
        {
            var p=camera.Source.Camera.Properties[ordinal];
            if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=type||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4)
                throw new InvalidDataException("Controlled radial Camera source field differs from its exact loader declaration.");
            return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
        }
        uint radialAmount=CameraSourceWord(989,0x40EF01BE,8),radialSamples=CameraSourceWord(994,0x1E65C92A,4);
        bool radialEnabled=BitConverter.UInt32BitsToSingle(radialAmount)!=0&&unchecked((int)radialSamples)>0;
        if(radialEnabled)throw new NotSupportedException("Enabled controlled radial blur requires the actual prepared12E4/12E8 center producer.");
        W("EnableRadialBlur",0xEB798C,0xE25B50,0);
        W("HdrTvScale",0xEB7A06,0x114B0F0,selector.Selector==1?hdrTvScaleBits:0x3F800000);
        Vector4 projection=CalculateLensProjection(camera.Prefix,light.ReadSelectedLight(camera),caller.ActualAb5D10Result);
        F("LensFlareProjPosFovScale",0xEB7BF8,0xE258F0,projection.X,projection.Y,projection.Z,projection.W);
        W("EnableFXAA",0xEB7C15,0xE224E0,caller.ActualFxaaMode==1?1u:0u);
        W("FXAAType",0xEB7C23,0xE25760,raw.Word(0x418));
        W("FXAAQualityEdgeThreshold",0xEB7C73,0xE256A0,raw.Word(0x41C));
        W("FXAAQualityEdgeThresholdMin",0xEB7C73,0xE256A0,raw.Word(0x420));
        W("FXAAQualitySubpixSharpness",0xEB7C83,0xE25670,raw.Word(0x424));
        W("FXAAQualityLevel",0xEB7C98,0xE25610,raw.Word(0x428));W("FXAAQualityPerformance",0xEB7C98,0xE25610,raw.Word(0x42C));
        F("Viewport",0xEB7D58,0x114B0F0,0,0,caller.ActualViewport1540.Z,caller.ActualViewport1540.W);
        F("CompositeLastViewport",0xEB7DEB,0x114B0F0,caller.ActualCompositeLastViewport.X,caller.ActualCompositeLastViewport.Y,
            caller.ActualCompositeLastViewport.Z,caller.ActualCompositeLastViewport.W);
        F("DeltaTime",0xEB7DFB,0xD7B4D0,MathF.Max(0,caller.ActualDeltaTime));
        return writes.AsReadOnly();
    }
    /// <summary>Exact finite D5BF60 normalization, negative-far placement,
    /// 16900 source-ordered projection and EB7B24 validation/FOV scale branch.
    /// Actual AB5D10 output remains explicit for the positive-light branch.</summary>
    public Vector4 CalculateLensProjection(ReadOnlyMemory<byte> currentCameraPrefix,ReadOnlyMemory<byte>? actualSelectedLight,
        int? actualAb5D10Result=null)
    {
        if(currentCameraPrefix.Length!=0x3C0)throw new InvalidDataException("Controlled lens projection needs the actual complete Camera prefix.");
        if(actualSelectedLight is null)return new(BinaryPrimitives.ReadSingleLittleEndian(defaultLightBytes),
            BinaryPrimitives.ReadSingleLittleEndian(defaultLightBytes.AsSpan(4)),BinaryPrimitives.ReadSingleLittleEndian(defaultLightBytes.AsSpan(8)),
            BinaryPrimitives.ReadSingleLittleEndian(defaultLightBytes.AsSpan(12)));
        if(actualSelectedLight.Value.Length!=0x10C0||actualAb5D10Result is null)
            throw new NotSupportedException("Positive controlled light projection needs actual AB5D10 output and complete LightData.");
        float C(int at)=>BinaryPrimitives.ReadSingleLittleEndian(currentCameraPrefix.Span[at..]);
        float L(int at)=>BinaryPrimitives.ReadSingleLittleEndian(actualSelectedLight.Value.Span[at..]);
        float x=L(0xF80),y=L(0xF84),z=L(0xF88);
        float inverse=D(1,MathF.Sqrt(A(A(M(x,x),M(y,y)),M(z,z))));
        x=A(M(-C(0x144),M(x,inverse)),C(0x10));y=A(M(-C(0x144),M(y,inverse)),C(0x14));z=A(M(-C(0x144),M(z,inverse)),C(0x18));
        float clipX=A(A(A(M(y,C(0x110)),M(x,C(0x100))),M(z,C(0x120))),C(0x130));
        float clipY=A(A(A(M(x,C(0x104)),M(y,C(0x114))),M(z,C(0x124))),C(0x134));
        float clipZ=A(A(A(M(x,C(0x108)),M(y,C(0x118))),M(z,C(0x128))),C(0x138));
        float clipW=A(A(A(M(x,C(0x10C)),M(y,C(0x11C))),M(z,C(0x12C))),C(0x13C));
        inverse=D(1,clipW);float px=M(clipX,inverse),py=M(clipY,inverse),pz=M(clipZ,inverse);
        if(new[]{px,py,pz,clipW}.Any(v=>!float.IsFinite(v)))throw new NotSupportedException("Controlled lens projection reached an unsupported nonfinite branch.");
        float threshold=actualAb5D10Result==2?0:BitConverter.UInt32BitsToSingle(lightThresholdBits);
        float limit=BitConverter.UInt32BitsToSingle(lightLimitBits),epsilon=BitConverter.UInt32BitsToSingle(lightEpsilonBits);
        bool valid=!(threshold>pz)&&!(0>clipW)&&MathF.Abs(px)<=limit&&MathF.Abs(py)<=limit;
        float vertical=D(BitConverter.UInt32BitsToSingle(lightScaleBits),MathF.Max(C(0x148),epsilon));
        return valid?new(px,py,D(vertical,MathF.Max(C(0x14C),epsilon)),vertical):new(px,py,0,0);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3ControlledCameraSource ReadControlledCameraSource(LoadedOriginalDeferredSamplerNativeSource native)
    {
        const string sourceScope="OriginalMapPostEffect3OwnedControlledCameraFrame.cs";
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Controlled Camera executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);VerifyHash(text,native.DecodedTextSha256,"Controlled Camera decoded text");
        void Check(int rva,int count,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,sourceScope,rva,count,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,count),actual.Sha,"Controlled Camera source window");
        }
        Check(0xEB746A,0x9A0,"2F220D327DDFB56B14F370B20A1772455A1FAAF5E1779AF13FE71A0D2E130A79");
        Check(0xE25320,0x62,"1651A6D0361B42DB571F565C9E420A446F78F7A0D3FFA1881E614F2A1C5CD264");
        Check(0xE253F0,0x97,"CCBE06F6EC4B5B1077EF79C66AEA6ED30B6C7C5832CBF7AF4F74CB97F4C7DF9D");
        Check(0xC439A6,0x60,"F510BB44FC3617321A356AD5419620DD380D0C0E13D478DBD65B3657288AB89F");
        Check(0xE25C40,0x7D,"68658BF5D3837653838FF77E66640F24A3ECD379C7D2CF7ECA371CB067C41ED2");
        Check(0xD7B4D0,0x45,"7D9D9828C35B068E26EAFEC15FBB28B91198CB992AEEE26EBDAD7B1416680E0C");
        Check(0xD5BF60,0x11B,"26A5B0FF93F774B88ACD543A3E89C39C39857FE069E1C46287E9A2101FAB81E8");
        Check(0x16900,0xFF,"8F313817CA8738B55EB1A30C009192FBFD12D50424E0A75829CB8B633C3D72E5");
        int Data(int rva)=>OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,sourceScope,rva);
        uint Literal(int rva)=>BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(Data(rva),4));
        return new(native,Literal(0x1343CD0),Literal(0x16EAFA4),image.Raw(Data(0x14BB8A0),16).ToArray(),Literal(0x139698C),
            Literal(0x13A1928),Literal(0x1343CDC),Literal(0x14BBC90),Literal(0x1343B54),Literal(0x16C9134),
            native.ResourceUri+$";{native.Locale}:controlled-Camera-native-profile;TC-reference-role:EB746A;D7B4D0-DeltaTime+conditional-E1F990/fixedAL1;"+
            "D5BF60/16900-actual-selected-light-projection;direct-source-fields;no-E71690/Functor0-bank");
    }
}
