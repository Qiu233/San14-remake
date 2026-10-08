namespace San14.Data;

/// <summary>Detached accepted Camera values for queued work. The source arrays
/// are verified and copied when captured; current checks compare only the
/// accepted frame, source identity, prefix, pose, lens, matrices and immutable
/// seasonal writer values. They never read or parse the source resource again.</summary>
public sealed class OriginalMapPostEffect3AcceptedCameraState
{
    private readonly OriginalMapCameraConstructionPacket camera;
    private OriginalMapPostEffect3AcceptedCameraState(OriginalMapCameraConstructionPacket camera)
        =>this.camera=camera.CopyAcceptedSnapshot();
    public long FrameNumber=>camera.FrameNumber;
    public static OriginalMapPostEffect3AcceptedCameraState Capture(OriginalMapCameraConstructionPacket actualAcceptedCamera)
    {
        ArgumentNullException.ThrowIfNull(actualAcceptedCamera);
        return new(actualAcceptedCamera);
    }
    public void ValidateCurrent(OriginalMapCameraConstructionPacket actualCamera)
    {
        ArgumentNullException.ThrowIfNull(actualCamera);
        if(actualCamera.FrameNumber!=camera.FrameNumber||actualCamera.ImageBase!=camera.ImageBase||
            actualCamera.ExecutableLocale!=camera.ExecutableLocale||
            actualCamera.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            actualCamera.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            actualCamera.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256||
            actualCamera.CurrentPose!=camera.CurrentPose||actualCamera.Lens!=camera.Lens||actualCamera.Matrices!=camera.Matrices||
            !actualCamera.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Current Camera differs from the accepted frame/source/prefix/pose/lens/matrices.");
        camera.RequireEquivalentCurrentPostEffectScalars(actualCamera);
    }
    public void ValidateCurrentPreparedPrefix(ReadOnlySpan<byte> actualPrefix)
    {
        if(!actualPrefix.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Current lighting prepared Camera prefix differs from its accepted Camera.");
    }
}

/// <summary>Capability for one fully accepted seasonal Main LUT join. The
/// complete source validators run at Capture. Its immutable receipt references
/// and detached Camera values remain the proof for later current checks;
/// mutable source documents and resource paths are not cached or reread.</summary>
public sealed class OriginalMapPostEffect3AcceptedCurrentLutFrame
{
    private readonly OriginalMapPostEffect3AcceptedCameraState camera;
    private readonly OriginalMapSeasonPreparedLutTransfer transfer;
    private readonly OriginalMapPostEffect3OwnedMainLutSelector selector;
    private readonly OriginalMapPostEffect3ApplyFreshLutLookups lookups;
    private readonly OriginalMapPostEffect3CurrentLutLookup hdr;
    private readonly OriginalMapPostEffect3CurrentLutLookup? ldr;
    private readonly OriginalStaticTextureBinding? hdrStatic,ldrStatic;
    private readonly uint hdrKey,ldrKey;
    private OriginalMapPostEffect3AcceptedCurrentLutFrame(OriginalMapCameraConstructionPacket actualCamera,
        OriginalMapSeasonPreparedLutTransfer actualTransfer,OriginalMapPostEffect3OwnedMainLutSelector actualSelector,
        OriginalMapPostEffect3ApplyFreshLutLookups actualLookups)
    {
        camera=OriginalMapPostEffect3AcceptedCameraState.Capture(actualCamera);
        transfer=actualTransfer;selector=actualSelector;lookups=actualLookups;
        hdr=actualLookups.ResolvedHdrLookup!;ldr=actualLookups.ResolvedLdrLookup;
        hdrStatic=hdr.SelectedStaticTexture;ldrStatic=ldr?.SelectedStaticTexture;
        hdrKey=actualCamera.HdrLutKey932;ldrKey=actualCamera.LdrLutKey933;
        SourceEvidence=actualTransfer.SourceEvidence+";"+actualLookups.SourceEvidence+
            ";accepted-immutable-current-Camera-and-exact-Main-transfer/selector/lookup-receipts";
    }
    public long FrameNumber=>camera.FrameNumber;
    public string SourceEvidence{get;}
    public static OriginalMapPostEffect3AcceptedCurrentLutFrame Capture(OriginalMapCameraConstructionPacket actualCamera,
        OriginalMapSeasonPreparedLutTransfer actualTransfer,OriginalMapPostEffect3OwnedMainLutSelector actualSelector,
        OriginalMapPostEffect3ApplyFreshLutLookups actualLookups)
    {
        ArgumentNullException.ThrowIfNull(actualCamera);ArgumentNullException.ThrowIfNull(actualTransfer);
        ArgumentNullException.ThrowIfNull(actualSelector);ArgumentNullException.ThrowIfNull(actualLookups);
        // This full lookup validation includes the seasonal transfer validation.
        // Current checks below deliberately do not call it again.
        actualLookups.ValidateSameCamera(actualCamera);
        var hdr=actualLookups.ResolvedHdrLookup??throw new InvalidDataException("Accepted controlled LUT frame needs its actual HDR lookup.");
        hdr.ValidateOwnedMainSelector(actualSelector);actualLookups.ResolvedLdrLookup?.ValidateOwnedMainSelector(actualSelector);
        if(!actualLookups.HdrPrimary1828Present||!ReferenceEquals(hdr.PreparedTransfer,actualTransfer)||
            actualLookups.ResolvedLdrLookup is{}ldr&&!ReferenceEquals(ldr.PreparedTransfer,actualTransfer)||
            actualSelector.FrameNumber!=actualCamera.FrameNumber)
            throw new InvalidDataException("Accepted controlled LUT capability requires the exact current seasonal Main transfer and selector.");
        return new(actualCamera,actualTransfer,actualSelector,actualLookups);
    }
    public void ValidateCurrent(OriginalMapCameraConstructionPacket actualCamera,
        OriginalMapSeasonPreparedLutTransfer actualTransfer,OriginalMapPostEffect3OwnedMainLutSelector actualSelector,
        OriginalMapPostEffect3ApplyFreshLutLookups actualLookups)
    {
        camera.ValidateCurrent(actualCamera);
        if(!ReferenceEquals(transfer,actualTransfer)||!ReferenceEquals(selector,actualSelector)||!ReferenceEquals(lookups,actualLookups)||
            !ReferenceEquals(actualLookups.ResolvedHdrLookup,hdr)||!ReferenceEquals(actualLookups.ResolvedLdrLookup,ldr)||
            !ReferenceEquals(hdr.PreparedTransfer,transfer)||ldr is{}currentLdr&&!ReferenceEquals(currentLdr.PreparedTransfer,transfer)||
            !ReferenceEquals(hdr.SelectedStaticTexture,hdrStatic)||!ReferenceEquals(ldr?.SelectedStaticTexture,ldrStatic)||
            hdr.SourceObjectKey!=hdrKey||actualCamera.HdrLutKey932!=hdrKey||actualCamera.LdrLutKey933!=ldrKey||
            hdr.FrameNumber!=FrameNumber||actualLookups.FrameNumber!=FrameNumber||actualSelector.FrameNumber!=FrameNumber||
            actualTransfer.FrameNumber!=FrameNumber||actualLookups.SourceHdrLutKey932!=hdrKey||actualLookups.SourceLdrLutKey933!=ldrKey||
            (ldr is null?ldrKey!=0:ldr.SourceObjectKey!=ldrKey||ldr.FrameNumber!=FrameNumber))
            throw new InvalidDataException("Current LUT handles, raw keys, Main transfer or selector differ from the accepted receipts.");
    }
    public void ValidateCurrentPreparedPrefix(ReadOnlySpan<byte> actualPrefix)=>camera.ValidateCurrentPreparedPrefix(actualPrefix);

    /// <summary>Accept after actual CPU preparation. This initial full route
    /// validation freezes the exact controlled frame for later short checks.</summary>
    public ControlledCameraReceipt AcceptControlledCamera(OriginalMapPostEffect3OwnedControlledCameraFrame actualFrame,
        OriginalMapCameraConstructionPacket actualCamera)
    {
        ArgumentNullException.ThrowIfNull(actualFrame);camera.ValidateCurrent(actualCamera);
        actualFrame.ValidateSameCamera(actualCamera);
        if(actualFrame.FrameNumber!=FrameNumber||!actualFrame.Route.UsesOwnedController||
            actualFrame.Route.SelectedModuleFunctorIndex!=1||!ReferenceEquals(actualFrame.Invocation.State,actualFrame.State))
            throw new InvalidDataException("Controlled frame differs from the accepted actual Camera/Functor1 scalar bank.");
        return new(this,actualFrame);
    }
    public sealed class ControlledCameraReceipt
    {
        private readonly OriginalMapPostEffect3AcceptedCurrentLutFrame owner;
        private readonly OriginalMapPostEffect3OwnedControlledCameraFrame frame;
        private readonly OriginalMapPostEffect3ApplyMode1Route route;
        private readonly OriginalMapPostEffect3ParameterState state;
        private readonly OriginalMapPostEffect3AllocationPlan allocation;
        private readonly string sourceSha256,contextOwner;
        internal ControlledCameraReceipt(OriginalMapPostEffect3AcceptedCurrentLutFrame owner,
            OriginalMapPostEffect3OwnedControlledCameraFrame frame)
        {
            this.owner=owner;this.frame=frame;route=frame.Route;state=frame.State;allocation=frame.Allocation;
            sourceSha256=state.SourceSha256;contextOwner=allocation.ContextOwnerIdentity;
        }
        public void ValidateCurrent(OriginalMapPostEffect3OwnedControlledCameraFrame actualFrame,
            OriginalMapCameraConstructionPacket actualCamera)
        {
            owner.camera.ValidateCurrent(actualCamera);
            if(!ReferenceEquals(actualFrame,frame)||!ReferenceEquals(actualFrame.Route,route)||
                !ReferenceEquals(actualFrame.State,state)||!ReferenceEquals(actualFrame.Allocation,allocation)||
                !ReferenceEquals(actualFrame.Invocation.State,state)||actualFrame.FrameNumber!=owner.FrameNumber||
                state.SourceSha256!=sourceSha256||allocation.ContextOwnerIdentity!=contextOwner||
                !route.UsesOwnedController||route.SelectedModuleFunctorIndex!=1)
                throw new InvalidDataException("Current controlled Camera frame is not the exact accepted route/state/allocation receipt.");
        }
    }
}
