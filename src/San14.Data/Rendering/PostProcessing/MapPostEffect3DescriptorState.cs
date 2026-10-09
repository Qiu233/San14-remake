namespace San14.Data;

/// <summary>The original extent comparison occurs before the descriptor resize.
/// This immutable plan keeps that comparison even after a successful resize.</summary>
public sealed class MapPostEffect3ApplyExtentPlan
{
    internal MapPostEffect3ApplyExtentPlan(MapPostEffect3DescriptorState owner,
        RenderTextureRuntimeDescriptor working,int width,int height,long frame,string caller,
        MapFinalOutputHdrRoute route,bool resize)
    {Owner=owner;Working=working;Width=width;Height=height;FrameNumber=frame;CallerIdentity=caller;HdrRoute=route;RequiresResize=resize;}
    internal MapPostEffect3DescriptorState Owner { get; }
    internal RenderTextureRuntimeDescriptor Working { get; }
    internal string CallerIdentity { get; }
    public int Width { get; }
    public int Height { get; }
    public long FrameNumber { get; }
    public MapFinalOutputHdrRoute HdrRoute { get; }
    public bool RequiresResize { get; }
}

/// <summary>Owned equivalent of the mutable 3SPK descriptor+40/+44 extent.
/// Fresh creation uses context dimensions; compares the existing
/// dimensions and resizes before execution. The GPU owner must
/// serialize calls and acknowledge its actual resize. This class allocates no
/// resources, executes no native functions and never manufactures pixels.</summary>
public sealed class MapPostEffect3DescriptorState
{
    private MapFinalOutputPostEffect3Owner descriptor;
    private MapPostEffect3ApplyExtentPlan? pending;
    private bool resizeAcknowledged;
    public MapFinalOutputPostEffect3Source Source { get; }
    public string OwnerIdentity { get; }
    public int Width => descriptor.Width;
    public int Height => descriptor.Height;
    public long LastAttemptNumber { get; private set; }
    public long ResizeGeneration { get; private set; }

    public MapPostEffect3DescriptorState(MapFinalOutputPostEffect3Source source,
        int actualContextWidth,int actualContextHeight,string ownedContextIdentity,string contextEvidence)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedContextIdentity);
        Source=source;OwnerIdentity=ownedContextIdentity+"/owned-PostEffect3";
        descriptor=source.CreateFreshForOwnedContext(actualContextWidth,actualContextHeight,
            OwnerIdentity,contextEvidence);
    }

    public MapPostEffect3ApplyExtentPlan PrepareApply(
        RenderTextureRuntimeDescriptor actualWorkingTarget,int width,int height,
        int currentCommonMode,long actualAttemptNumber,string actualCallerIdentity)
    {
        if(pending is not null||actualAttemptNumber<=LastAttemptNumber)
            throw new InvalidDataException("PostEffect3 requires one increasing serialized Apply attempt.");
        if(currentCommonMode!=1)
            throw new NotSupportedException("This descriptor owner executes the source PostEffectModule1 branch.");
        var route=MapFinalOutputHdrRoute.Capture(actualWorkingTarget,width,height,currentCommonMode,
            descriptor,actualAttemptNumber,actualCallerIdentity);
        var plan=new MapPostEffect3ApplyExtentPlan(this,actualWorkingTarget,width,height,
            actualAttemptNumber,actualCallerIdentity,route,width!=Width||height!=Height);
        pending=plan;resizeAcknowledged=false;LastAttemptNumber=actualAttemptNumber;
        return plan;
    }

    /// <summary>Call after the backend actually resized this module. A later
    /// effect failure does not undo an already completed native resize.</summary>
    public void AcknowledgeResize(MapPostEffect3ApplyExtentPlan plan,int actualWidth,int actualHeight,
        string actualBackendWriterEvidence)
    {
        RequirePending(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualBackendWriterEvidence);
        if(!plan.RequiresResize||resizeAcknowledged||actualWidth!=plan.Width||actualHeight!=plan.Height)
            throw new InvalidDataException("Resize acknowledgement differs from the exact pending module extent.");
        descriptor=MapFinalOutputPostEffect3Owner.CreatePresent(descriptor.OwnerIdentity,actualWidth,actualHeight,
            Source.Source.Source.ResourceUri+";actual-owned-resize"+actualBackendWriterEvidence);
        ResizeGeneration++;resizeAcknowledged=true;
    }

    public void CompleteApply(MapPostEffect3ApplyExtentPlan plan)
    {
        RequirePending(plan);
        if(plan.RequiresResize&&!resizeAcknowledged)
            throw new InvalidDataException("Source Apply cannot execute with its required descriptor resize unacknowledged.");
        pending=null;resizeAcknowledged=false;
    }

    /// <summary>Release a failed attempt. Preserve any resize that its backend
    /// already acknowledged; an unexecuted resize leaves the old extent.</summary>
    public void AbortApply(MapPostEffect3ApplyExtentPlan plan)
    {RequirePending(plan);pending=null;resizeAcknowledged=false;}

    private void RequirePending(MapPostEffect3ApplyExtentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if(!ReferenceEquals(pending,plan)||!ReferenceEquals(plan.Owner,this))
            throw new InvalidDataException("PostEffect3 acknowledgement must retain its exact pending owner/attempt.");
        plan.HdrRoute.ValidateSameWorkingTarget(plan.Working,plan.Width,plan.Height,plan.FrameNumber,plan.CallerIdentity);
    }
}
