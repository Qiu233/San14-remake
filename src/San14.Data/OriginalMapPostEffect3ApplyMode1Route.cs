namespace San14.Data;

/// <summary>Source mode1 branch selection. A successfully resolved LensPreset
/// makes E71690 return true and selects the without-controller functor0.
/// Explicitly failed resolution uses EC3360. The controlled route instead
/// carries the actual E22860 scalar invocation and source Apply1A8. Both
/// bypass legacy/A1; neither route proves a GPU result.</summary>
public sealed class OriginalMapPostEffect3ApplyMode1Route
{
    private readonly OriginalMapCameraConstructionPacket camera;
    private readonly OriginalMapFinalOutputPostEffect3Source source;
    private readonly OriginalMapPostEffect3Container container;
    private readonly OriginalMapPostEffect3ApplyFreshLutLookups lookups;
    private OriginalMapPostEffect3ApplyMode1Route(long frame,string owner,
        OriginalMapPostEffect3ApplyPreparationSelection selection,string evidence,OriginalMapCameraConstructionPacket currentCamera,
        OriginalMapFinalOutputPostEffect3Source actualSource,OriginalMapPostEffect3Container actualContainer,
        bool preparationReturnedTrue,OriginalMapPostEffect3ApplyFunctorSelection? selectedFunctor,
        OriginalMapPostEffect3ApplyFreshLutLookups actualLookups,
        OriginalMapPostEffect3ControllerScalarInvocation? controllerInvocation=null,
        OriginalMapPostEffect3ControlledOpticsPreparation? controlledPreparation=null)
    {FrameNumber=frame;OwnerIdentity=owner;Preparation=selection;SourceEvidence=evidence;camera=currentCamera.CopyAcceptedSnapshot();source=actualSource;container=actualContainer;
        NativePreparationReturnedTrue=preparationReturnedTrue;FunctorSelection=selectedFunctor;lookups=actualLookups;
        ControllerInvocation=controllerInvocation;ControlledPreparation=controlledPreparation;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public OriginalMapPostEffect3ApplyPreparationSelection Preparation{get;}
    public bool NativePreparationReturnedTrue{get;}
    public bool ExecutesModuleFunctor=>NativePreparationReturnedTrue;
    public bool ExecutesEc3360Fallback=>!NativePreparationReturnedTrue;
    public OriginalMapPostEffect3ApplyFunctorSelection? FunctorSelection{get;}
    public OriginalMapPostEffect3ControllerScalarInvocation? ControllerInvocation{get;}
    public OriginalMapPostEffect3ControlledOpticsPreparation? ControlledPreparation{get;}
    public bool UsesOwnedController=>ControllerInvocation is not null;
    public int? SelectedModuleFunctorIndex=>UsesOwnedController?ControllerInvocation!.Caller.ActualSelectedFunctorIndex:
        FunctorSelection?.WithoutControllerIndex1A4;
    public bool ProvesGpuExecution=>false;
    public bool BypassesLegacyA1Branch=>true;
    public uint? FallbackCallSiteRva=>ExecutesEc3360Fallback?0xEB80D4u:null;
    public uint SharedTailEntryRva=>0xEB99F0;
    public string SourceEvidence{get;}
    public void ValidateSameCamera(OriginalMapCameraConstructionPacket currentCamera)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);
        lookups.ValidateSameCamera(currentCamera);
        var selected=OriginalMapPostEffect3ApplyPreparationSelection.ReadActualSource(source,container,Preparation.OwnerIdentity);
        if(selected.Descriptor58!=Preparation.Descriptor58)
            throw new InvalidDataException("Mode1 preparation source changed after selection.");
        if(currentCamera.FrameNumber!=FrameNumber||currentCamera.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            currentCamera.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            currentCamera.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256||
            currentCamera.CurrentPose!=camera.CurrentPose||!currentCamera.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Mode1 route belongs to another Camera/source/prefix/frame.");
    }
    public static OriginalMapPostEffect3ApplyMode1Route ResolveFreshWithoutController(
        OriginalMapFinalOutputPostEffect3Source source,OriginalMapPostEffect3Container container,
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyFreshLutLookups lookups,
        string actualMode1Owner)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(lookups);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualMode1Owner);lookups.ValidateSameCamera(camera);
        var selection=OriginalMapPostEffect3ApplyPreparationSelection.ReadActualSource(source,container,actualMode1Owner+"/descriptor");
        if(selection.Variant!=OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE71690||
            lookups.FreshControllerPresent190)
            throw new NotSupportedException("Fresh no-controller route requires actual source E71690 and cleared controller lookups.");
        bool present=lookups.FreshLensPreset1810Present;
        var functor=present?OriginalMapPostEffect3ApplyFunctorSelection.ConstructFreshDefault(source,container,
            camera.FrameNumber,actualMode1Owner+"/default-factory",lookups.SourceEvidence):null;
        return new(camera.FrameNumber,actualMode1Owner,selection,selection.SourceEvidence+";"+lookups.SourceEvidence+
            (present?";TC:E7242C/E72446-present1810=>R15B1;E725D7=>AL1;EB7EC6/EB7ECE;EB804E/EB805C-Apply1A4-functor0;"+functor!.SourceEvidence:
                ";TC:E7242C/E725D7-failed-lookup-null1810=>AL0;EB7EC6/EB7EC8=>EB80BF/EB80D4-EC3360;")+
            "EB80DD-mode1-cleanup;EB8604-jump99F0-bypasses8609-legacyA1;EB9CE7/EB9CEE=>EB619D-outer-loop",camera,source,container,present,functor,lookups);
    }

    /// <summary>The caller has prepared its owned controller and reached the
    /// actual E22860 invocation. A successful prepared593 handle by itself is
    /// insufficient; the typed invocation carries this bank and actual1A8.</summary>
    public static OriginalMapPostEffect3ApplyMode1Route ResolveOwnedControlled(
        OriginalMapFinalOutputPostEffect3Source source,OriginalMapPostEffect3Container container,
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyFreshLutLookups lookups,
        OriginalMapPostEffect3ControlledOpticsPreparation actualControlledPreparation,
        OriginalMapPostEffect3ControllerScalarInvocation actualControllerInvocation,string actualMode1Owner)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(lookups);
        ArgumentNullException.ThrowIfNull(actualControllerInvocation);ArgumentException.ThrowIfNullOrWhiteSpace(actualMode1Owner);
        ArgumentNullException.ThrowIfNull(actualControlledPreparation);
        lookups.ValidateSameCamera(camera);actualControllerInvocation.RequirePending();
        actualControlledPreparation.ValidateAppliedState(actualControllerInvocation.State);
        if(!lookups.HdrPrimary1828Present||actualControllerInvocation.State.FrameNumber!=camera.FrameNumber||
            actualControllerInvocation.State.SourceSha256!=container.Sha256)
            throw new InvalidDataException("Fresh controlled route requires the actual prepared HDR handle and same-frame controller invocation.");
        var selection=OriginalMapPostEffect3ApplyPreparationSelection.ReadActualSource(source,container,actualMode1Owner+"/descriptor");
        var functor=OriginalMapPostEffect3ApplyFunctorSelection.ConstructFreshDefault(source,container,
            camera.FrameNumber,actualMode1Owner+"/default-factory",lookups.SourceEvidence);
        if(actualControllerInvocation.Caller.ActualSelectedFunctorIndex!=functor.WithControllerIndex1A8)
            throw new InvalidDataException("Actual controller argument6 does not match the owned default Apply1A8.");
        return new(camera.FrameNumber,actualMode1Owner,selection,selection.SourceEvidence+";"+lookups.SourceEvidence+";"+
            actualControllerInvocation.SourceEvidence+";"+actualControlledPreparation.SourceEvidence+";TC:EB7461-owned-controller;EB7DFB-D7B4D0-controlled-preparation;"+
            "EB7EC6-successful-preparation;EB7ED7/E1F970-global-byte-literalAL1;EB7F6B-actual-Apply1A8;EB7F79-E22860;"+
            "EB8604-shared99F0-bypasses-legacyA1;owned-CPU-controller-scalar-route-only",camera,source,container,true,functor,lookups,
            actualControllerInvocation,actualControlledPreparation);
    }
}
