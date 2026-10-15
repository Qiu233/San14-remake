using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>CPU sources for the actual controlled branch. This owner
/// deliberately creates fresh all-ten-factory scalar storage; it does not
/// import an /Functor0 frame or prove controller texture readiness.</summary>
public sealed class MapPostEffect3OwnedControlledCameraSources
{
    private MapPostEffect3OwnedControlledCameraSources(
        LoadedMapPostEffect3ScalarZeroAllocationSource allocator,SkyWeatherNativePowerSource power,
        LoadedMapPostEffect3ControllerExecuteSource execute,MapPostEffect3ControlledToneSource tone,
        LoadedMapPostEffect3ControlledCameraSource camera,LoadedMapPostEffect3ControlledOpticsSource optics,
        LoadedMapPostEffect3RetainedResizeSource resize)
        =>(Allocator,Power,Execute,Tone,Camera,Optics,Resize)=(allocator,power,execute,tone,camera,optics,resize);
    public LoadedMapPostEffect3ScalarZeroAllocationSource Allocator{get;}
    public SkyWeatherNativePowerSource Power{get;}
    public LoadedMapPostEffect3ControllerExecuteSource Execute{get;}
    public MapPostEffect3ControlledToneSource Tone{get;}
    public LoadedMapPostEffect3ControlledCameraSource Camera{get;}
    public LoadedMapPostEffect3ControlledOpticsSource Optics{get;}
    public LoadedMapPostEffect3RetainedResizeSource Resize{get;}
    public static MapPostEffect3OwnedControlledCameraSources Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(MapPostEffect3ScalarZeroAllocationSource.Load(repository,locale),
            SkyWeatherNativePowerSource.Load(repository,locale),MapPostEffect3ControllerExecuteSource.Load(repository,locale),
            MapPostEffect3ControlledToneSource.Load(repository,locale),
            RenderSamplerSourceAdapters.ReadControlledCameraSource(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale)),
            MapPostEffect3ControlledOpticsSource.Load(repository,locale),
            MapPostEffect3RetainedResizeSource.Load(repository,locale));
    }
}

/// <summary>Actual EB caller inputs whose producers are outside the Camera
/// fields recovered here. GammaDivisor is the real xmm13, LdrIntensity is
/// the selected mix in xmm9, and GatherSizeType is the real localC0. The two
/// viewports and adaptation lock predicate are actual caller values. The
/// Viewport scalar consumes only selected Camera1548/154C and source-zero XY;
/// CompositeLastViewport consumes the full selected/fallback rectangle.</summary>
public sealed record MapPostEffect3ControlledCameraCaller(long FrameNumber,
    int ActualWorkingSceneWidth,int ActualWorkingSceneHeight,int ActualFxaaMode,
    float ActualGammaDivisorXmm13,float ActualDeltaTime,float ActualLdrLutIntensityXmm9,
    int ActualGatherSizeTypeC0,bool ActualAdaptationLockPredicate,
    Vector4 ActualViewport1540,Vector4 ActualCompositeLastViewport,int? ActualProjectionClipMode,
    float ActualContextHeatScale254,string SourceEvidence);

/// <summary>The actual selected HDR wrapper and its native metadata snapshot.
/// A caller supplies the wrapper identity after its real selection producer.
/// Null metadata records an actual null +28 pointer; it is not inferred from
/// G1T or from the successful593 handle. This receipt makes no GPU claim.</summary>
public sealed class MapPostEffect3ControlledSelectedHdrTexture
{
    private readonly byte[]? metadata;
    private MapPostEffect3ControlledSelectedHdrTexture(MapPostEffect3CurrentLutLookup lookup,
        MapPostEffect3ApplyView view,ReadOnlyMemory<byte>? actualMetadata,string evidence)
    {Lookup=lookup;View=view;metadata=actualMetadata?.ToArray();SourceEvidence=evidence;}
    public MapPostEffect3CurrentLutLookup Lookup{get;}
    public MapPostEffect3ApplyView View{get;}
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
    public static MapPostEffect3ControlledSelectedHdrTexture CaptureActualStaticSelection(
        MapPostEffect3CurrentLutLookup actualPreparedHdrLookup,MapPostEffect3ApplyView actualSelectedWrapper,
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

/// <summary>Same-frame successful or explicitly absent light lookup.
/// The positive branch consumes this actual light's F80 vector. Absence has
/// its own frozen native fallback; no F0 projection is used.</summary>
public sealed class MapPostEffect3ControlledLightLookup
{
    private readonly MapCameraConstructionPacket camera;
    private readonly Vector3? projectionVector;
    private MapPostEffect3ControlledLightLookup(MapCameraConstructionPacket camera,
        Vector3? light,string owner,string evidence)
    {this.camera=camera.CopyAcceptedSnapshot();projectionVector=light;OwnerIdentity=owner;SourceEvidence=evidence;}
    public long FrameNumber=>camera.FrameNumber;
    public string OwnerIdentity{get;}
    public string SourceEvidence{get;}
    public bool FoundLight=>projectionVector is not null;
    public static MapPostEffect3ControlledLightLookup CaptureActualLookup(MapCameraConstructionPacket camera,
        MapSourceCreatedLightingPacket? actualSelectedLight,string actualLookupOwner,string actualLookupEvidence)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentException.ThrowIfNullOrWhiteSpace(actualLookupOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualLookupEvidence);
        if(camera.FrameNumber<=0)throw new InvalidDataException("Controlled light lookup needs its actual owned frame.");
        if(actualSelectedLight is{}light&&(light.FrameNumber!=camera.FrameNumber||
            light.SourceIdentity.CameraObjectId!=camera.Source.Camera.ObjectId||
            !light.PreparedCameraPrefix.Span.SequenceEqual(camera.Prefix.Span)||light.LocalLightData.Length!=0x10C0))
            throw new InvalidDataException("Controlled light lookup does not belong to this exact prepared Camera.");
        return new(camera,actualSelectedLight is {} selected
            ? DecodeProjectionVector(selected.LocalLightData.Span) : null,actualLookupOwner,actualLookupEvidence);
    }
    /// <summary>The current native numeric route already owns the provider's
    /// complete LightData. Capture the actual lookup result without recreating
    /// an unrelated class66 lighting frame. Null is an actual absent lookup.</summary>
    public static MapPostEffect3ControlledLightLookup CaptureActualNumericLookup(
        MapCameraConstructionPacket camera,ReadOnlyMemory<byte>? actualSelectedLightData,
        long? actualSelectedLightFrameNumber,string actualLookupOwner,string actualLookupEvidence)
    {
        if(actualSelectedLightData is{}data &&
            (data.Length!=0x10C0||actualSelectedLightFrameNumber!=camera.FrameNumber))
            throw new InvalidDataException("Controlled numeric light lookup needs this frame's complete LightData.");
        return new(camera,actualSelectedLightData is {} selected
            ? DecodeProjectionVector(selected.Span) : null,actualLookupOwner,actualLookupEvidence);
    }

    /// <summary>Capture the selected light's actual projection vector directly.
    /// Null is an absent selection; a zero vector still represents a present light.</summary>
    public static MapPostEffect3ControlledLightLookup CaptureProjectionLookup(
        MapCameraConstructionPacket camera,Vector3? actualSelectedProjectionVector,
        long? actualSelectedLightFrameNumber,string actualLookupOwner,string actualLookupEvidence)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if(actualSelectedProjectionVector is not null && actualSelectedLightFrameNumber!=camera.FrameNumber)
            throw new InvalidDataException("Controlled projection lookup needs the selected vector from this Camera frame.");
        return new(camera,actualSelectedProjectionVector,actualLookupOwner,actualLookupEvidence);
    }

    internal Vector3? ReadProjectionVector(MapCameraConstructionPacket currentCamera)
    {
        camera.RequireEquivalentCurrentPostEffectScalars(currentCamera);
        if(currentCamera.FrameNumber!=FrameNumber||currentCamera.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            currentCamera.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            currentCamera.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256||
            !camera.Prefix.Span.SequenceEqual(currentCamera.Prefix.Span))
            throw new InvalidDataException("Controlled light selection belongs to another actual Camera frame.");
        return projectionVector;
    }

    private static Vector3 DecodeProjectionVector(ReadOnlySpan<byte> light) => new(
        BinaryPrimitives.ReadSingleLittleEndian(light[0xF80..]),
        BinaryPrimitives.ReadSingleLittleEndian(light[0xF84..]),
        BinaryPrimitives.ReadSingleLittleEndian(light[0xF88..]));
}

public sealed record MapPostEffect3OwnedControlledCameraInputs(
    MapSeasonPreparedLutTransfer PreparedTransfer,MapPostEffect3OwnedMainLutSelector MainSelector,
    MapPostEffect3ApplyFreshLutLookups Lookups,MapPostEffect3ControlledSelectedHdrTexture SelectedHdr,
    MapPostEffect3ControlledLightLookup Light,MapPostEffect3ControlledCameraCaller Caller,
    ReadOnlyMemory<byte> ActualPreparedAux460,string ActualAuxEvidence,
    MapPostEffect3ControllerScalarSnapshot? ActualPostApplyControllerScalars,
    MapPostEffect3ControllerExecuteCaller ActualExecuteCaller,NativeLogDispatch ActualOwnedLogDispatch)
{
    /// <summary>Actual consumed field receipt. When present the historical
    /// complete460 input is not read; unused Aux bytes are not required.</summary>
    public MapPostEffect3ControlledAuxFields? ActualAuxFields{get;init;}
    public MapPostEffect3ControlledFreshCaller? ActualFreshCaller{get;init;}
}

/// <summary>Independent fresh owned Functor1 scalar preparation. Factory
/// defaults, recovered controlled Camera/light writes, metadata4 and actual
/// writes10 are source-ordered. Controller optics/ghost preparation,
/// factory Before hooks, resources and GPU execution remain separate gates.</summary>
public sealed class MapPostEffect3OwnedControlledCameraFrame
{
    private readonly MapCameraConstructionPacket camera;
    private readonly MapPostEffect3ParameterState? retainedState;
    private MapPostEffect3OwnedControlledCameraFrame(MapCameraConstructionPacket camera,
        MapPostEffect3AllocationPlan allocation,MapPostEffect3CompleteFreshScalarInitialization defaults,
        MapPostEffect3ApplyMode1Route route,MapPostEffect3ControllerScalarOwner controller,
        MapPostEffect3ControlledToneMetadata tone,MapPostEffect3ApplyDerivedScalarPlan derived,
        IReadOnlyList<MapPostEffect3ApplyValueWrite> writes,MapPostEffect3ControlledOpticsPreparation optics,string evidence,
        MapPostEffect3ParameterState? retainedState=null)
    {this.camera=camera.CopyAcceptedSnapshot();Allocation=allocation;Defaults=defaults;Route=route;Controller=controller;
        Tone=tone;Derived=derived;ApplyWrites=writes;Optics=optics;SourceEvidence=evidence;this.retainedState=retainedState;}
    public MapPostEffect3AllocationPlan Allocation{get;}
    public MapPostEffect3CompleteFreshScalarInitialization Defaults{get;}
    public MapPostEffect3ParameterState State=>retainedState??Defaults.State;
    public MapPostEffect3ApplyMode1Route Route{get;}
    public MapPostEffect3ControllerScalarOwner Controller{get;}
    public MapPostEffect3ControllerScalarInvocation Invocation=>Route.ControllerInvocation!;
    public MapPostEffect3ControlledToneMetadata Tone{get;}
    public MapPostEffect3ApplyDerivedScalarPlan Derived{get;}
    public MapPostEffect3ControlledOpticsPreparation Optics{get;}
    public IReadOnlyList<MapPostEffect3ApplyValueWrite> ApplyWrites{get;}
    public long FrameNumber=>State.FrameNumber;
    public string SourceEvidence{get;}
    public bool IsCompleteForExecution=>false;
    public bool ProvesGpuExecution=>false;
    public string RequiredRemainingInputs=>"controller texture and ghost-buffer device owners; "+
        "selected factory Before hooks/constant buffers; actual resource uploads and pass execution";
    public void RequireCompleteForExecution()=>throw new NotSupportedException(RequiredRemainingInputs);
    public void ValidateSameCamera(MapCameraConstructionPacket actualCamera)=>Route.ValidateSameCamera(actualCamera);
    /// <summary>Immutable accepted source snapshot. This accessor does not
    /// acknowledge an execution, a Host return or any device contents.</summary>
    public MapCameraConstructionPacket CaptureAcceptedCamera()=>camera.CopyAcceptedSnapshot();

    public static MapPostEffect3OwnedControlledCameraFrame Prepare(
        MapFinalOutputPostEffect3Source source,MapPostEffect3AllocationPlan allocation,
        MapCameraConstructionPacket camera,MapPostEffect3OwnedControlledCameraSources native,
        MapPostEffect3OwnedControlledCameraInputs inputs)
        =>PrepareCore(source,allocation,camera,native,inputs,null,null);

    public static MapPostEffect3OwnedControlledCameraFrame PrepareRetained(
        MapFinalOutputPostEffect3Source source,MapPostEffect3AllocationPlan allocation,
        MapCameraConstructionPacket camera,MapPostEffect3OwnedControlledCameraSources native,
        MapPostEffect3OwnedControlledCameraInputs inputs,MapPostEffect3OwnedControlledCameraFrame previous,
        MapPostEffect3Functor1ModuleState module)
        =>PrepareCore(source,allocation,camera,native,inputs,previous,module);

    private static MapPostEffect3OwnedControlledCameraFrame PrepareCore(
        MapFinalOutputPostEffect3Source source,MapPostEffect3AllocationPlan allocation,
        MapCameraConstructionPacket camera,MapPostEffect3OwnedControlledCameraSources native,
        MapPostEffect3OwnedControlledCameraInputs inputs,MapPostEffect3OwnedControlledCameraFrame? previous,
        MapPostEffect3Functor1ModuleState? module)
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
        var aux=inputs.ActualAuxFields??MapPostEffect3ControlledAuxFields.CaptureActualPrepared(
            inputs.ActualPreparedAux460.Span,snapshot.FrameNumber,"explicit-selected-View",inputs.ActualAuxEvidence);
        aux.ValidateFrame(snapshot.FrameNumber);
        var tail=MapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(snapshot,allocation.ContextOwnerIdentity+"/controlled-camera-tail");
        var known=MapPostEffect3ApplyRecipe.PrepareFreshSourceKnownCameraWrites(snapshot,tail,aux);
        var derived=previous is null?MapPostEffect3ApplyDerivedScalars.PrepareFreshSource(snapshot,tail,
            MapPostEffect3ApplyDerivedScalars.ConstructFreshDimensions(snapshot.FrameNumber,allocation.ContextOwnerIdentity,
                checked((int)allocation.ContextWidth),checked((int)allocation.ContextHeight),native.Power),
            inputs.Caller.ActualWorkingSceneWidth,inputs.Caller.ActualWorkingSceneHeight,native.Power):
            native.Resize.PrepareRetainedCurrentDerived(snapshot,tail,
                MapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(previous.camera,previous.State.OwnerIdentity),
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
        var defaults=previous?.Defaults??MapPostEffect3ApplyCompleteInitialScalars.CreateFresh(allocation.Source,snapshot.FrameNumber,
            allocation.ContextOwnerIdentity,native.Allocator);
        var state=previous is null?defaults.State:module!.CloneRetainedScalarState(previous.State,snapshot.FrameNumber);
        state.BindOwnedAllocation(allocation);
        var optics=previous is null?native.Optics.PrepareFreshCurrentCamera(snapshot,derived,allocation.ContextOwnerIdentity,inputs.ActualOwnedLogDispatch):
            MapPostEffect3ControlledOpticsPreparation.RetainUnchanged(previous.Optics,state,allocation);
        if(previous is null)
        {
            state.ApplyKnownCameraWrites(new(snapshot.FrameNumber,derived.InitializationWrites,native.Camera.SourceEvidence));
            optics.ApplyInitializationToCurrentState(state,allocation);
        }
        var currentWrites=known.Writes.Concat(derived.ApplyWrites).Concat(extra).OrderBy(w=>w.Stage).ToArray();
        var writes=(previous is null?derived.InitializationWrites.Concat(optics.InitializationWrites):
            Enumerable.Empty<MapPostEffect3ApplyValueWrite>()).Concat(currentWrites)
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
        // Ten fields are from the actual post-Apply controller snapshot.
        // Fresh constructor flags must not overwrite those current caller facts.
        var controller=previous?.Controller??native.Execute.ConstructFreshScalarOwner(allocation.Source,allocation.ContextOwnerIdentity);
        controller.AcceptCurrentSnapshot(actualController);
        var invocation=controller.BeginExecute(state,inputs.ActualExecuteCaller);
        var route=MapPostEffect3ApplyMode1Route.ResolveOwnedControlled(source,allocation.Source,snapshot,inputs.Lookups,
            optics,invocation,allocation.ContextOwnerIdentity+"/controlled-mode1");
        return new(snapshot,allocation,defaults,route,controller,tone,derived,Array.AsReadOnly(writes.Concat(optics.Writes).ToArray()),optics,
            defaults.SourceEvidence+";"+native.Camera.SourceEvidence+";"+inputs.PreparedTransfer.SourceEvidence+";"+tone.SourceEvidence+
            ";"+optics.SourceEvidence+"CPU-Functor1-bank;no-writes;factory-and-device-execution-remain-separate",
            previous is null?null:state);
    }

    private static void RequireUnchangedRetainedOptics(MapPostEffect3OwnedControlledCameraFrame previous,
        MapCameraConstructionPacket camera,MapPostEffect3ApplyDerivedScalarPlan derived,
        MapPostEffect3OwnedControlledCameraInputs inputs)
    {
        var before=MapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(previous.camera,previous.State.OwnerIdentity);
        var now=MapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(camera,previous.State.OwnerIdentity);
        int[] fields=[0x12FC,0x1300,0x1304,0x131C,0x7A8,0x7AC];
        var oldTail=MapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(previous.camera,previous.State.OwnerIdentity);
        var newTail=MapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(camera,previous.State.OwnerIdentity);
        if(fields.Any(o=>before.Word(o)!=now.Word(o))||oldTail.Int(0x1344)!=newTail.Int(0x1344)||
            oldTail.Int(0x1348)!=newTail.Int(0x1348)||oldTail.Word(0x1320)!=newTail.Word(0x1320)||
            previous.camera.Optics.Limb.Weight!=camera.Optics.Limb.Weight||
            previous.Derived.EffectiveWidth80!=derived.EffectiveWidth80||previous.Derived.EffectiveHeight84!=derived.EffectiveHeight84||
            previous.Derived.AspectRatioF4!=derived.AspectRatioF4||previous.Derived.SensorHeightRatioF0!=derived.SensorHeightRatioF0||
            inputs.ActualOwnedLogDispatch!=NativeLogDispatch.Sse)
            throw new NotSupportedException("Changed retained optics needs its actual AF/A1/setter source producer; this owner admits the unchanged current profile.");
    }
}

public sealed class LoadedMapPostEffect3ControlledCameraSource
{
    internal LoadedMapPostEffect3ControlledCameraSource(LoadedDeferredSamplerNativeSource native,
        uint lockedAdaptation,uint sourceHdrTvScale,Vector4 defaultLightProjection,uint gatherMaximum,uint gatherFactor,
        uint lightThreshold,uint lightLimit,uint lightEpsilon,uint lightScale,string evidence)
    {Native=native;lockedAdaptationBits=lockedAdaptation;hdrTvScaleBits=sourceHdrTvScale;this.defaultLightProjection=defaultLightProjection;
        gatherMaximumBits=gatherMaximum;gatherFactorBits=gatherFactor;lightThresholdBits=lightThreshold;lightLimitBits=lightLimit;
        lightEpsilonBits=lightEpsilon;lightScaleBits=lightScale;SourceEvidence=evidence;}
    private readonly uint lockedAdaptationBits,hdrTvScaleBits,gatherMaximumBits,gatherFactorBits,
        lightThresholdBits,lightLimitBits,lightEpsilonBits,lightScaleBits;
    private readonly Vector4 defaultLightProjection;
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public uint LockedAdaptationBits => lockedAdaptationBits;
    internal float ProjectionThreshold => BitConverter.UInt32BitsToSingle(lightThresholdBits);
    internal float ProjectionLimit => BitConverter.UInt32BitsToSingle(lightLimitBits);
    internal float ProjectionEpsilon => BitConverter.UInt32BitsToSingle(lightEpsilonBits);
    internal float ProjectionScale => BitConverter.UInt32BitsToSingle(lightScaleBits);
    internal Vector4 AbsentLightProjection => defaultLightProjection;
    internal IReadOnlyList<MapPostEffect3ApplyValueWrite> PrepareCameraAndLightWrites(
        MapCameraConstructionPacket camera,MapPostEffect3OwnedMainLutSelector selector,
        MapPostEffect3ControlledCameraCaller caller,MapPostEffect3ControlledLightLookup light,
        MapPostEffect3ApplyDerivedScalarPlan derived,MapPostEffect3ControlledAuxFields aux)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.SourceEvidence);
        if(caller.FrameNumber!=camera.FrameNumber||selector.FrameNumber!=camera.FrameNumber||Native.Locale!=camera.ExecutableLocale||
            !float.IsFinite(caller.ActualGammaDivisorXmm13)||caller.ActualGammaDivisorXmm13==0||
            !float.IsFinite(caller.ActualDeltaTime)||!float.IsFinite(caller.ActualLdrLutIntensityXmm9)||!float.IsFinite(caller.ActualContextHeatScale254)||
            new[]{caller.ActualViewport1540.X,caller.ActualViewport1540.Y,caller.ActualViewport1540.Z,caller.ActualViewport1540.W,
                caller.ActualCompositeLastViewport.X,caller.ActualCompositeLastViewport.Y,caller.ActualCompositeLastViewport.Z,
                caller.ActualCompositeLastViewport.W}.Any(v=>!float.IsFinite(v)))
            throw new InvalidDataException("Controlled Camera writers require the actual finite same-frame caller arguments.");
        var raw=MapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(camera,"controlled-field-source-only");
        var tail=MapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(camera,"controlled-field-source-only/tail");
        var writes=new List<MapPostEffect3ApplyValueWrite>();
        string evidence=SourceEvidence+";"+camera.FrameProvenance+";"+caller.SourceEvidence+";"+light.SourceEvidence;

        void W(string name,MapPostEffect3WriteStage stage,params uint[] words)
        {byte[] bytes=new byte[checked(words.Length*4)];for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),words[i]);writes.Add(new(name,stage,bytes,evidence));}
        void F(string name,MapPostEffect3WriteStage stage,params float[] values)=>W(name,stage,values.Select(BitConverter.SingleToUInt32Bits).ToArray());
        W("AdaptedScaleLight",MapPostEffect3WriteStage.ControlledAdaptedScaleLight,caller.ActualAdaptationLockPredicate?lockedAdaptationBits:
            BitConverter.SingleToUInt32Bits(camera.Optics.Exposure.LightAdaptationScale));
        W("AdaptedScaleDark",MapPostEffect3WriteStage.ControlledAdaptedScaleLight,caller.ActualAdaptationLockPredicate?lockedAdaptationBits:
            BitConverter.SingleToUInt32Bits(camera.Optics.Exposure.DarkAdaptationScale));
        float height=derived.EffectiveHeight84;
        float gather=MathF.Min(BitConverter.UInt32BitsToSingle(gatherMaximumBits),
            D(M(A(A(caller.ActualGatherSizeTypeC0,caller.ActualGatherSizeTypeC0),1),BitConverter.UInt32BitsToSingle(gatherFactorBits)),height));
        W("GatherSizeType",MapPostEffect3WriteStage.ControlledGather,unchecked((uint)caller.ActualGatherSizeTypeC0));
        F("GatherMaxCoC",MapPostEffect3WriteStage.ControlledGather,gather);
        F("DOF2HeatSimmerParams",MapPostEffect3WriteStage.ControlledDOF2HeatSimmerParams,tail.Settings.HeatShimmer.Vibra,tail.Settings.HeatShimmer.Cycle,
            M(tail.Settings.HeatShimmer.Speed,caller.ActualContextHeatScale254));
        W("EnableSeparateHdrLut",MapPostEffect3WriteStage.ControlledHdrDisplay,
            aux.SeparateHdrMode440==2&&selector.Selector==2?1u:0u);
        W("HdrTvNit",MapPostEffect3WriteStage.ControlledHdrDisplay,tail.Settings.ColorGrading.HdrTvNit);W("BaseTvNit",MapPostEffect3WriteStage.ControlledHdrDisplay,tail.Settings.ColorGrading.BaseTvNit);
        F("LdrLutIntensity",MapPostEffect3WriteStage.ControlledLdrLutIntensity,caller.ActualLdrLutIntensityXmm9);
        W("GammaEnable",MapPostEffect3WriteStage.ControlledGamma,1);F("GammaValue",MapPostEffect3WriteStage.ControlledGamma,D(1,caller.ActualGammaDivisorXmm13));
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
        W("EnableRadialBlur",MapPostEffect3WriteStage.ControlledEnableRadialBlur,0);
        W("HdrTvScale",MapPostEffect3WriteStage.ControlledHdrTvScale,selector.Selector==1?hdrTvScaleBits:0x3F800000);
        Vector4 projection=CalculateLensProjectionFromVector(camera.Prefix,light.ReadProjectionVector(camera),caller.ActualProjectionClipMode);
        F("LensFlareProjPosFovScale",MapPostEffect3WriteStage.ControlledLensFlareProjPosFovScale,projection.X,projection.Y,projection.Z,projection.W);
        W("EnableFXAA",MapPostEffect3WriteStage.ControlledEnableFXAA,caller.ActualFxaaMode==1?1u:0u);
        W("FXAAType",MapPostEffect3WriteStage.ControlledFXAAType,raw.Settings.Fxaa.Type);
        W("FXAAQualityEdgeThreshold",MapPostEffect3WriteStage.ControlledFxaaEdges,BitConverter.SingleToUInt32Bits(raw.Settings.Fxaa.QualityEdgeThreshold));
        W("FXAAQualityEdgeThresholdMin",MapPostEffect3WriteStage.ControlledFxaaEdges,BitConverter.SingleToUInt32Bits(raw.Settings.Fxaa.QualityEdgeThresholdMin));
        W("FXAAQualitySubpixSharpness",MapPostEffect3WriteStage.ControlledFXAAQualitySubpixSharpness,BitConverter.SingleToUInt32Bits(raw.Settings.Fxaa.QualitySubpixSharpness));
        W("FXAAQualityLevel",MapPostEffect3WriteStage.ControlledFxaaQuality,raw.Settings.Fxaa.QualityLevel);W("FXAAQualityPerformance",MapPostEffect3WriteStage.ControlledFxaaQuality,raw.Settings.Fxaa.QualityPerformance);
        F("Viewport",MapPostEffect3WriteStage.ControlledViewport,0,0,caller.ActualViewport1540.Z,caller.ActualViewport1540.W);
        F("CompositeLastViewport",MapPostEffect3WriteStage.ControlledCompositeLastViewport,caller.ActualCompositeLastViewport.X,caller.ActualCompositeLastViewport.Y,
            caller.ActualCompositeLastViewport.Z,caller.ActualCompositeLastViewport.W);
        F("DeltaTime",MapPostEffect3WriteStage.ControlledDeltaTime,MathF.Max(0,caller.ActualDeltaTime));
        return writes.AsReadOnly();
    }
    /// <summary>Exact finite normalization, negative-far placement,
    /// 16900 source-ordered projection and validation/FOV scale branch.
    /// Actual output remains explicit for the positive-light branch.</summary>
    public Vector4 CalculateLensProjection(ReadOnlyMemory<byte> currentCameraPrefix,ReadOnlyMemory<byte>? actualSelectedLight,
        int? actualProjectionClipMode=null)
    {
        if(currentCameraPrefix.Length!=0x3C0)throw new InvalidDataException("Controlled lens projection needs the actual complete Camera prefix.");
        if(actualSelectedLight is null)return defaultLightProjection;
        if(actualSelectedLight.Value.Length!=0x10C0||actualProjectionClipMode is null)
            throw new NotSupportedException("Positive controlled light projection needs actual  output and complete LightData");
        float L(int at)=>BinaryPrimitives.ReadSingleLittleEndian(actualSelectedLight.Value.Span[at..]);
        return CalculateLensProjectionCore(currentCameraPrefix,new(L(0xF80),L(0xF84),L(0xF88)),actualProjectionClipMode.Value);
    }

    public Vector4 CalculateLensProjectionFromVector(ReadOnlyMemory<byte> currentCameraPrefix,
        Vector3? actualSelectedProjectionVector,int? actualProjectionClipMode=null)
    {
        if(currentCameraPrefix.Length!=0x3C0)throw new InvalidDataException("Controlled lens projection needs the actual complete Camera prefix.");
        if(actualSelectedProjectionVector is not {} vector)return defaultLightProjection;
        if(actualProjectionClipMode is null)
            throw new NotSupportedException("Positive controlled light projection needs the actual projection convention.");
        return CalculateLensProjectionCore(currentCameraPrefix,vector,actualProjectionClipMode.Value);
    }

    private Vector4 CalculateLensProjectionCore(ReadOnlyMemory<byte> currentCameraPrefix,
        Vector3 selectedVector,int actualProjectionClipMode)
    {
        float C(int at)=>BinaryPrimitives.ReadSingleLittleEndian(currentCameraPrefix.Span[at..]);
        float x=selectedVector.X,y=selectedVector.Y,z=selectedVector.Z;
        float inverse=D(1,MathF.Sqrt(A(A(M(x,x),M(y,y)),M(z,z))));
        x=A(M(-C(0x144),M(x,inverse)),C(0x10));y=A(M(-C(0x144),M(y,inverse)),C(0x14));z=A(M(-C(0x144),M(z,inverse)),C(0x18));
        float clipX=A(A(A(M(y,C(0x110)),M(x,C(0x100))),M(z,C(0x120))),C(0x130));
        float clipY=A(A(A(M(x,C(0x104)),M(y,C(0x114))),M(z,C(0x124))),C(0x134));
        float clipZ=A(A(A(M(x,C(0x108)),M(y,C(0x118))),M(z,C(0x128))),C(0x138));
        float clipW=A(A(A(M(x,C(0x10C)),M(y,C(0x11C))),M(z,C(0x12C))),C(0x13C));
        inverse=D(1,clipW);float px=M(clipX,inverse),py=M(clipY,inverse),pz=M(clipZ,inverse);
        if(new[]{px,py,pz,clipW}.Any(v=>!float.IsFinite(v)))throw new NotSupportedException("Controlled lens projection reached an unsupported nonfinite branch.");
        float threshold=actualProjectionClipMode==2?0:BitConverter.UInt32BitsToSingle(lightThresholdBits);
        float limit=BitConverter.UInt32BitsToSingle(lightLimitBits),epsilon=BitConverter.UInt32BitsToSingle(lightEpsilonBits);
        bool valid=!(threshold>pz)&&!(0>clipW)&&MathF.Abs(px)<=limit&&MathF.Abs(py)<=limit;
        float vertical=D(BitConverter.UInt32BitsToSingle(lightScaleBits),MathF.Max(C(0x148),epsilon));
        return valid?new(px,py,D(vertical,MathF.Max(C(0x14C),epsilon)),vertical):new(px,py,0,0);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3ControlledCameraSource ReadControlledCameraSource(LoadedDeferredSamplerNativeSource native)
    {
        const float lockedAdaptation=float.MaxValue,hdrTvScale=3.3333333f;
        const float gatherMaximum=.05f,gatherFactor=1.5f;
        const float lightThreshold=-1f,lightLimit=1.2f,lightEpsilon=.000011920929f,lightScale=.009305f;
        static uint Word(float value)=>BitConverter.SingleToUInt32Bits(value);
        return new(native,Word(lockedAdaptation),Word(hdrTvScale),Vector4.Zero,Word(gatherMaximum),
            Word(gatherFactor),Word(lightThreshold),Word(lightLimit),Word(lightEpsilon),Word(lightScale),
            RenderRuleIdentity.For("controlled-camera",native.Locale)+
            ";current-camera-and-selected-light-projection;finite-scalar-rules;default-null-light-vector-zero");
    }
}
