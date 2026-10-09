namespace San14.Data;

/// <summary>One fresh owned Main Host Q, constructed by the exact source
/// factory. Its final v170 is a return-only leaf and its sixteen output-overlay
/// pointers are source-cleared. Importing/changing any Q callback or overlay
/// invalidates this owner rather than supplying an absence Boolean.</summary>
public sealed class MapPostEffect3ApplyMode1WriterScope
{
    private readonly MapPostEffect3OwnedMainHostCallbackScope callbacks;
    private MapPostEffect3ApplyMode1WriterScope(MainMapContextCurrentSource source)
    {callbacks=new(source);}
    public MainMapContextCurrentSource Source => callbacks.Source;
    public static MapPostEffect3ApplyMode1WriterScope ConstructFresh(
        MainMapContextCurrentSource actualOwnedContextSource)
    {
        ArgumentNullException.ThrowIfNull(actualOwnedContextSource);
        return new(actualOwnedContextSource);
    }
    public MapPostEffect3ApplyMode1WriterScopeCurrent PrepareCurrent(
        MainMapContextCurrentPacket actualContext,MapPostEffect3ApplyMode1Route actualRoute,
        MapPostEffect3ApplyFallbackCameraSelection actualCameraSelection)
    {
        ArgumentNullException.ThrowIfNull(actualContext);ArgumentNullException.ThrowIfNull(actualRoute);
        ArgumentNullException.ThrowIfNull(actualCameraSelection);RequireCurrentContext(actualContext);
        if(!actualRoute.ExecutesToneFallback||actualRoute.ExecutesModuleFunctor||
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
    internal void RequireCurrentContext(MainMapContextCurrentPacket current)
    {
        callbacks.RequireCurrentSource(current);
        if(current.ContextFlags3F4!=0||
            current.Phase is not(MainMapContextCurrentPhase.DeferredPreparationReset or MainMapContextCurrentPhase.NativeThinSurfaceFlagWriter)||
            (current.ContextFlags3F0&~0x400u)!=0)
            throw new NotSupportedException("Mode1 writer scope requires the owned fresh Q and only the source-closed reset/thin-surface writers; imported or unknown Q state is unsupported.");
    }
}

/// <summary>The shared owned Host constructor/vtable/callback/overlay source.
/// It grants no Mode1 or Functor1 current-phase/route/output authority. Each
/// consumer applies its own actual writer and route contract separately.</summary>
internal sealed class MapPostEffect3OwnedMainHostCallbackScope
{
    private bool valid=true;
    internal MapPostEffect3OwnedMainHostCallbackScope(MainMapContextCurrentSource actualSource)
    {ArgumentNullException.ThrowIfNull(actualSource);RequireSource(actualSource);Source=actualSource;}
    internal MainMapContextCurrentSource Source{get;}
    internal void MarkImportedOrUnknownCallbackOrOverlayWriter(string actualWriter)
    {ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);valid=false;}
    internal void RequireCurrentSource(MainMapContextCurrentPacket current)
    {
        RequireSource(Source);Source.Validate(current);
        if(!valid||current.FrameNumber<=0)
            throw new NotSupportedException("Owned Host callback/overlay scope has no current unmodified source owner.");
    }
    private static void RequireSource(MainMapContextCurrentSource source)
    {
        var host=source.Host;
        if(host.Locale is not("SC" or "TC") || host.RuleVersion!=RenderRuleIdentity.Version ||
            host.ContextFlags3E4!=MainMapContextOwnerSource.FreshHostFlags3E4)
            throw new InvalidDataException("Owned Host callback scope requires the current context rules.");
    }
}

/// <summary>Same-frame source writer family, not GPU submission acknowledgement.
/// CommonAux300 need not be guessed absent: native first tests the
/// selected Q overlay, which this owned fresh constructor clears.</summary>
public sealed class MapPostEffect3ApplyMode1WriterScopeCurrent
{
    internal MapPostEffect3ApplyMode1WriterScopeCurrent(MapPostEffect3ApplyMode1WriterScope owner,
        MainMapContextCurrentPacket context,MapPostEffect3ApplyMode1Route route,
        MapPostEffect3ApplyFallbackCameraSelection camera)
    {Owner=owner;Context=context;Route=route;CameraSelection=camera;}
    public MapPostEffect3ApplyMode1WriterScope Owner { get; }
    public MainMapContextCurrentPacket Context { get; }
    public MapPostEffect3ApplyMode1Route Route { get; }
    public MapPostEffect3ApplyFallbackCameraSelection CameraSelection { get; }
    public long FrameNumber=>Context.FrameNumber;

    public string SourceEvidence=>Owner.Source.SourceProvenance+
        "final-Host-Q-v170-ret0;-sixteen-Q2F8-overlay-slots-null;Main-slot0"+
        "-r13-current-CommonAux;-null-Q-overlay=>-independent-of-Aux300-pointer"+
        "-return-scope-restores-viewport-only;not-GPU-receipt";
    public void Validate(MapCameraConstructionPacket camera,MapPostEffect3ApplyMode1Route route)
    {
        Owner.RequireCurrentContext(Context);
        if(!ReferenceEquals(Route,route)||FrameNumber!=camera.FrameNumber||CameraSelection.Camera is not { } selected||
            selected.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            !selected.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Mode1 writer family is foreign to the completed actual Camera/route.");
        Route.ValidateSameCamera(camera);
    }
    public void RequireSameContext(MainMapContextCurrentPacket actualContext)
    {
        ArgumentNullException.ThrowIfNull(actualContext);Owner.RequireCurrentContext(actualContext);
        if(!ReferenceEquals(actualContext.Source,Context.Source)||actualContext.FrameNumber!=FrameNumber)
            throw new InvalidDataException("Mode1 writer family belongs to another actual current Q owner/frame.");
    }
}
