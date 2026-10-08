namespace San14.Data;

/// <summary>One fresh owned Main Host Q, constructed by the exact source
/// factory. Its final v170 is a return-only leaf and its sixteen output-overlay
/// pointers are source-cleared. Importing/changing any Q callback or overlay
/// invalidates this owner rather than supplying an absence Boolean.</summary>
public sealed class OriginalMapPostEffect3ApplyMode1WriterScope
{
    private readonly OriginalMapPostEffect3OwnedMainHostCallbackScope callbacks;
    private OriginalMapPostEffect3ApplyMode1WriterScope(OriginalMainMapContextCurrentSource source)
    {callbacks=new(source);}
    public OriginalMainMapContextCurrentSource Source => callbacks.Source;
    public static OriginalMapPostEffect3ApplyMode1WriterScope ConstructFresh(
        OriginalMainMapContextCurrentSource actualOwnedContextSource)
    {
        ArgumentNullException.ThrowIfNull(actualOwnedContextSource);
        return new(actualOwnedContextSource);
    }
    public OriginalMapPostEffect3ApplyMode1WriterScopeCurrent PrepareCurrent(
        OriginalMainMapContextCurrentPacket actualContext,OriginalMapPostEffect3ApplyMode1Route actualRoute,
        OriginalMapPostEffect3ApplyFallbackCameraSelection actualCameraSelection)
    {
        ArgumentNullException.ThrowIfNull(actualContext);ArgumentNullException.ThrowIfNull(actualRoute);
        ArgumentNullException.ThrowIfNull(actualCameraSelection);RequireCurrentContext(actualContext);
        if(!actualRoute.ExecutesEc3360Fallback||actualRoute.ExecutesModuleFunctor||
            actualCameraSelection.FrameNumber!=actualContext.FrameNumber||actualRoute.FrameNumber!=actualContext.FrameNumber||
            actualCameraSelection.OwnerIdentity!=Source.OwnedHostIdentity||actualCameraSelection.Slot!=0||
            actualCameraSelection.Camera is not { } camera)
            throw new NotSupportedException("Mode1 writer scope covers only the actual owned Main slot0, not absent/foreign Cameras or another output slot.");
        actualRoute.ValidateSameCamera(camera);
        if(camera.ExecutableLocale!=Source.Host.Locale)
            throw new InvalidDataException("Mode1 writer scope Camera and Q native profiles differ.");
        return new(this,actualContext,actualRoute,actualCameraSelection);
    }
    public void MarkImportedOrUnknownCallbackOrOverlayWriter(string actualWriter)
        =>callbacks.MarkImportedOrUnknownCallbackOrOverlayWriter(actualWriter);
    internal void RequireCurrentContext(OriginalMainMapContextCurrentPacket current)
    {
        callbacks.RequireCurrentSource(current);
        if(current.ContextFlags3F4!=0||
            current.Phase is not(OriginalMainMapContextCurrentPhase.DeferredPreparationReset or OriginalMainMapContextCurrentPhase.NativeThinSurfaceFlagWriter)||
            (current.ContextFlags3F0&~0x400u)!=0)
            throw new NotSupportedException("Mode1 writer scope requires the owned fresh Q and only the source-closed reset/thin-surface writers; imported or unknown Q state is unsupported.");
    }
}

/// <summary>The shared owned Host constructor/vtable/callback/overlay source.
/// It grants no Mode1 or Functor1 current-phase/route/output authority. Each
/// consumer applies its own actual writer and route contract separately.</summary>
internal sealed class OriginalMapPostEffect3OwnedMainHostCallbackScope
{
    private bool valid=true;
    internal OriginalMapPostEffect3OwnedMainHostCallbackScope(OriginalMainMapContextCurrentSource actualSource)
    {ArgumentNullException.ThrowIfNull(actualSource);RequireSource(actualSource);Source=actualSource;}
    internal OriginalMainMapContextCurrentSource Source{get;}
    internal void MarkImportedOrUnknownCallbackOrOverlayWriter(string actualWriter)
    {ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);valid=false;}
    internal void RequireCurrentSource(OriginalMainMapContextCurrentPacket current)
    {
        RequireSource(Source);Source.Validate(current);
        if(!valid||current.FrameNumber<=0)
            throw new NotSupportedException("Owned Host callback/overlay scope has no current unmodified source owner.");
    }
    private static void RequireSource(OriginalMainMapContextCurrentSource source)
    {
        var host=source.Host;
        var expected=host.Locale switch
        {
            "TC"=>(0x13C4ED0u,"0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464"),
            "SC"=>(0x13C5CF0u,"CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88"),
            _=>throw new NotSupportedException("Owned Host callback scope has no exact native profile.")
        };
        if(host.ContextOwnerVtableRva!=expected.Item1||host.ExecutableSha256!=expected.Item2||
            host.ContextFlags3E4!=OriginalMainMapContextOwnerSource.FreshHostFlags3E4)
            throw new InvalidDataException("Owned Host callback scope requires the actual final constructor/vtable source.");
    }
}

/// <summary>Same-frame source writer family, not GPU submission acknowledgement.
/// CommonAux300 need not be guessed absent: native EB9A75 first tests the
/// selected Q overlay, which this owned fresh constructor clears.</summary>
public sealed class OriginalMapPostEffect3ApplyMode1WriterScopeCurrent
{
    internal OriginalMapPostEffect3ApplyMode1WriterScopeCurrent(OriginalMapPostEffect3ApplyMode1WriterScope owner,
        OriginalMainMapContextCurrentPacket context,OriginalMapPostEffect3ApplyMode1Route route,
        OriginalMapPostEffect3ApplyFallbackCameraSelection camera)
    {Owner=owner;Context=context;Route=route;CameraSelection=camera;}
    public OriginalMapPostEffect3ApplyMode1WriterScope Owner { get; }
    public OriginalMainMapContextCurrentPacket Context { get; }
    public OriginalMapPostEffect3ApplyMode1Route Route { get; }
    public OriginalMapPostEffect3ApplyFallbackCameraSelection CameraSelection { get; }
    public long FrameNumber=>Context.FrameNumber;
    public uint Virtual170TargetRva=>Owner.Source.Host.Locale=="TC"?0xA624F0u:0xA90880u;
    public string SourceEvidence=>Owner.Source.SourceProvenance+
        ";final-Host-Q-v170-ret0;BF58A0-sixteen-Q2F8-overlay-slots-null;Main-slot0;"+
        "EB84C7-r13-current-CommonAux;EB9A75-null-Q-overlay=>EB9B36-independent-of-Aux300-pointer;"+
        "E6D4B0/95C070-return-scope-restores-viewport-only;not-GPU-receipt";
    public void Validate(OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyMode1Route route)
    {
        Owner.RequireCurrentContext(Context);
        if(!ReferenceEquals(Route,route)||FrameNumber!=camera.FrameNumber||CameraSelection.Camera is not { } selected||
            selected.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            !selected.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Mode1 writer family is foreign to the completed actual Camera/route.");
        Route.ValidateSameCamera(camera);
    }
    public void RequireSameContext(OriginalMainMapContextCurrentPacket actualContext)
    {
        ArgumentNullException.ThrowIfNull(actualContext);Owner.RequireCurrentContext(actualContext);
        if(!ReferenceEquals(actualContext.Source,Context.Source)||actualContext.FrameNumber!=FrameNumber)
            throw new InvalidDataException("Mode1 writer family belongs to another actual current Q owner/frame.");
    }
}
