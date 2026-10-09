namespace San14.Data;

/// <summary>Source mode1 branch selection. A successfully resolved LensPreset
/// makes return true and selects the without-controller functor0.
/// Explicitly failed resolution uses . The controlled route instead
/// carries the actual scalar invocation and source Apply1A8. Both
/// bypass legacy/A1; neither route proves a GPU result.</summary>
public sealed class MapPostEffect3ApplyMode1Route
{
    private readonly MapCameraConstructionPacket camera;
    private readonly MapFinalOutputPostEffect3Source source;
    private readonly MapPostEffect3Container container;
    private readonly MapPostEffect3ApplyFreshLutLookups lookups;
    private MapPostEffect3ApplyMode1Route(long frame,string owner,
        MapPostEffect3ApplyPreparationSelection selection,string evidence,MapCameraConstructionPacket currentCamera,
        MapFinalOutputPostEffect3Source actualSource,MapPostEffect3Container actualContainer,
        bool preparationReturnedTrue,MapPostEffect3ApplyFunctorSelection? selectedFunctor,
        MapPostEffect3ApplyFreshLutLookups actualLookups,
        MapPostEffect3ControllerScalarInvocation? controllerInvocation=null,
        MapPostEffect3ControlledOpticsPreparation? controlledPreparation=null)
    {FrameNumber=frame;OwnerIdentity=owner;Preparation=selection;SourceEvidence=evidence;camera=currentCamera.CopyAcceptedSnapshot();source=actualSource;container=actualContainer;
        NativePreparationReturnedTrue=preparationReturnedTrue;FunctorSelection=selectedFunctor;lookups=actualLookups;
        ControllerInvocation=controllerInvocation;ControlledPreparation=controlledPreparation;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public MapPostEffect3ApplyPreparationSelection Preparation{get;}
    public bool NativePreparationReturnedTrue{get;}
    public bool ExecutesModuleFunctor=>NativePreparationReturnedTrue;
    public bool ExecutesToneFallback=>!NativePreparationReturnedTrue;
    public MapPostEffect3ApplyFunctorSelection? FunctorSelection{get;}
    public MapPostEffect3ControllerScalarInvocation? ControllerInvocation{get;}
    public MapPostEffect3ControlledOpticsPreparation? ControlledPreparation{get;}
    public bool UsesOwnedController=>ControllerInvocation is not null;
    public int? SelectedModuleFunctorIndex=>UsesOwnedController?ControllerInvocation!.Caller.ActualSelectedFunctorIndex:
        FunctorSelection?.WithoutControllerIndex1A4;
    public bool ProvesGpuExecution=>false;
    public bool BypassesLegacyA1Branch=>true;


    public string SourceEvidence{get;}
    public void ValidateSameCamera(MapCameraConstructionPacket currentCamera)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);
        lookups.ValidateSameCamera(currentCamera);
        var selected=MapPostEffect3ApplyPreparationSelection.ReadActualSource(source,container,Preparation.OwnerIdentity);
        if(selected.Descriptor58!=Preparation.Descriptor58)
            throw new InvalidDataException("Mode1 preparation source changed after selection.");
        if(currentCamera.FrameNumber!=FrameNumber||currentCamera.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            currentCamera.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            currentCamera.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256||
            currentCamera.CurrentPose!=camera.CurrentPose||!currentCamera.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Mode1 route belongs to another Camera/source/prefix/frame.");
    }
    public static MapPostEffect3ApplyMode1Route ResolveFreshWithoutController(
        MapFinalOutputPostEffect3Source source,MapPostEffect3Container container,
        MapCameraConstructionPacket camera,MapPostEffect3ApplyFreshLutLookups lookups,
        string actualMode1Owner)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(lookups);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualMode1Owner);lookups.ValidateSameCamera(camera);
        var selection=MapPostEffect3ApplyPreparationSelection.ReadActualSource(source,container,actualMode1Owner+"/descriptor");
        if(selection.Variant!=MapPostEffect3ApplyUncontrolledVariant.LensPresetCameraFields||
            lookups.FreshControllerPresent190)
            throw new NotSupportedException("Fresh no-controller route requires actual source  and cleared controller lookups");
        bool present=lookups.FreshLensPreset1810Present;
        var functor=present?MapPostEffect3ApplyFunctorSelection.ConstructFreshDefault(source,container,
            camera.FrameNumber,actualMode1Owner+"/default-factory",lookups.SourceEvidence):null;
        return new(camera.FrameNumber,actualMode1Owner,selection,selection.SourceEvidence+";"+lookups.SourceEvidence+
            (present?";present1810=>R15B1;=>AL1;-Apply1A4-functor0"+functor!.SourceEvidence:
                ";failed-lookup-null1810=>AL0;=>/-")+
            "-mode1-cleanup;-jump99F0-bypasses8609-legacyA1;=>-outer-loop",camera,source,container,present,functor,lookups);
    }

    /// <summary>The caller has prepared its owned controller and reached the
    /// actual invocation. A successful prepared593 handle by itself is
    /// insufficient; the typed invocation carries this bank and actual1A8.</summary>
    public static MapPostEffect3ApplyMode1Route ResolveOwnedControlled(
        MapFinalOutputPostEffect3Source source,MapPostEffect3Container container,
        MapCameraConstructionPacket camera,MapPostEffect3ApplyFreshLutLookups lookups,
        MapPostEffect3ControlledOpticsPreparation actualControlledPreparation,
        MapPostEffect3ControllerScalarInvocation actualControllerInvocation,string actualMode1Owner)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(lookups);
        ArgumentNullException.ThrowIfNull(actualControllerInvocation);ArgumentException.ThrowIfNullOrWhiteSpace(actualMode1Owner);
        ArgumentNullException.ThrowIfNull(actualControlledPreparation);
        lookups.ValidateSameCamera(camera);actualControllerInvocation.RequirePending();
        actualControlledPreparation.ValidateAppliedState(actualControllerInvocation.State);
        if(!lookups.HdrPrimary1828Present||actualControllerInvocation.State.FrameNumber!=camera.FrameNumber||
            actualControllerInvocation.State.SourceSha256!=container.Sha256)
            throw new InvalidDataException("Fresh controlled route requires the actual prepared HDR handle and same-frame controller invocation.");
        var selection=MapPostEffect3ApplyPreparationSelection.ReadActualSource(source,container,actualMode1Owner+"/descriptor");
        var functor=MapPostEffect3ApplyFunctorSelection.ConstructFreshDefault(source,container,
            camera.FrameNumber,actualMode1Owner+"/default-factory",lookups.SourceEvidence);
        if(actualControllerInvocation.Caller.ActualSelectedFunctorIndex!=functor.WithControllerIndex1A8)
            throw new InvalidDataException("Actual controller argument6 does not match the owned default Apply1A8.");
        return new(camera.FrameNumber,actualMode1Owner,selection,selection.SourceEvidence+";"+lookups.SourceEvidence+";"+
            actualControllerInvocation.SourceEvidence+";"+actualControlledPreparation.SourceEvidence+";owned-controller;-controlled-preparation"+
            "-successful-preparation;-global-byte-literalAL1;-actual-Apply1A8;-"+
            "-shared99F0-bypasses-legacyA1;owned-CPU-controller-scalar-route-only",camera,source,container,true,functor,lookups,
            actualControllerInvocation,actualControlledPreparation);
    }
}
