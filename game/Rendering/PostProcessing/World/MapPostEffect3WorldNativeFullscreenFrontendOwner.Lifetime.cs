using San14.Data;

public sealed partial class MapPostEffect3WorldNativeFullscreenFrontendOwner
{
    private MapPostEffect3ManagedModuleScope? moduleScope;
    internal MapPostEffect3ManagedModuleScope BeginManagedModule(
        MapPostEffect3Functor1ModuleInvocation invocation,MapPostEffect3Functor1FrontendCacheOwner cache)
    {
        RequireCurrent();RequireReleasedBeforeNextBind();
        if(moduleScope is not null||invocation.FrameNumber!=World.Context.FrameNumber)
            throw new InvalidDataException("Actual managed Begin needs this current caller before factory callbacks.");
        WorldPreparation.ActualFrontendParent.RequireCurrent();
        cache.BeginModule();
        return moduleScope=new(this,invocation,WorldPreparation.ActualFrontendParent,World.CurrentStock,
            ordinaryRaster,checked(++revision));
    }
    internal MapPostEffect3ManagedModuleScope EndManagedModule(MapPostEffect3Functor1FrontendCacheOwner cache)
    {
        RequireCurrent();RequireReleasedBeforeNextBind();
        var scope=moduleScope??throw new InvalidDataException("Actual managed End has no entrance capsule.");
        scope.RequirePending(this);WorldPreparation.ActualFrontendParent.RequireCurrent();
        if(!ReferenceEquals(scope.Parent,WorldPreparation.ActualFrontendParent)||!ReferenceEquals(scope.Stock,World.CurrentStock))
            throw new InvalidDataException("Module End cannot restore a substituted caller/parent.");
        scope.DrawContinuation=CaptureReleasedDrawContinuation();cache.EndModule();scope.Ended=true;revision++;return scope;
    }
    internal void RestoreManagedOuterScope(MapPostEffect3ManagedModuleScope scope)
    {
        if(!ReferenceEquals(scope,moduleScope)||!scope.Ended||scope.OuterRestored)
            throw new InvalidDataException("Managed outer restore needs its exact once-only completed End.");
        //Explicit device lists ended before accepted directory return; no
        //global Vulkan pipeline is inherited between lists. The real parent
        //stack remains pinned, and its original scope reference is restored.
        scope.Parent.RequireCurrent();RequireReleasedBeforeNextBind();
        LastRestoredNativeState=null;revision++;scope.OuterRestored=true;
    }
}

/// <summary>Actual managed caller/parent/raster capsule captured before
/// Prepare/Init. The port's lists own explicit full state. Unknown original
/// raw bytes are neither filled nor advertised as equal to this snapshot.</summary>
public sealed class MapPostEffect3ManagedModuleScope
{
    private readonly MapPostEffect3WorldNativeFullscreenFrontendOwner owner;
    internal readonly MapPostEffect3Functor1ModuleInvocation Invocation;
    internal readonly MapPostEffect3WorldCompositeSourceOwner.ParentBinding Parent;
    internal readonly MapCopy97CurrentStockPacket Stock;
    private readonly ModelRasterInputs raster;
    private readonly long entranceRevision;
    internal bool Ended,OuterRestored;
    internal MapPostEffect3ManagedDrawContinuation? DrawContinuation;
    internal MapPostEffect3ManagedModuleScope(MapPostEffect3WorldNativeFullscreenFrontendOwner owner,
        MapPostEffect3Functor1ModuleInvocation invocation,MapPostEffect3WorldCompositeSourceOwner.ParentBinding parent,
        MapCopy97CurrentStockPacket stock,ModelRasterInputs raster,long revision)
        =>(this.owner,Invocation,Parent,Stock,this.raster,entranceRevision)=(owner,invocation,parent,stock,raster,revision);
    public long FrameNumber=>Invocation.FrameNumber;
    public bool ManagedBeginExecuted=>true;
    public bool ManagedEndExecuted=>Ended;
    public bool ManagedOuterRestored=>OuterRestored;
    public bool BackendByteEquivalenceVerified=>false;
    public string SourceEvidence=>owner.SourceEvidence+";actual-managed-parent/explicit-list-scope-Begin/End;native-raw-byte-equivalence-unproven";
    internal void RequirePending(MapPostEffect3WorldNativeFullscreenFrontendOwner actual)
    {if(!ReferenceEquals(actual,owner)||Ended||OuterRestored||entranceRevision<=0||raster.Attachments.Count!=1)
        throw new InvalidDataException("Managed capsule is foreign, ended or incomplete.");}
}
