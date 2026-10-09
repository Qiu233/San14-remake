namespace San14.Data;

/// <summary>The original extent comparison occurs before the descriptor resize.
/// This immutable plan keeps that comparison even after a successful resize.</summary>
public sealed class OriginalMapPostEffect3ApplyExtentPlan
{
    internal OriginalMapPostEffect3ApplyExtentPlan(OriginalMapPostEffect3DescriptorState owner,
        OriginalRenderTextureRuntimeDescriptor working,int width,int height,long frame,string caller,
        OriginalMapFinalOutputHdrRoute route,bool resize)
    {Owner=owner;Working=working;Width=width;Height=height;FrameNumber=frame;CallerIdentity=caller;HdrRoute=route;RequiresResize=resize;}
    internal OriginalMapPostEffect3DescriptorState Owner { get; }
    internal OriginalRenderTextureRuntimeDescriptor Working { get; }
    internal string CallerIdentity { get; }
    public int Width { get; }
    public int Height { get; }
    public long FrameNumber { get; }
    public OriginalMapFinalOutputHdrRoute HdrRoute { get; }
    public bool RequiresResize { get; }
}

/// <summary>Owned equivalent of the mutable 3SPK descriptor+40/+44 extent.
/// Fresh creation uses context dimensions; compares the existing
/// dimensions and resizes before execution. The GPU owner must
/// serialize calls and acknowledge its actual resize. This class allocates no
/// resources, executes no native functions and never manufactures pixels.</summary>
public sealed class OriginalMapPostEffect3DescriptorState
{
    private OriginalMapFinalOutputPostEffect3Owner descriptor;
    private OriginalMapPostEffect3ApplyExtentPlan? pending;
    private bool resizeAcknowledged;
    public OriginalMapFinalOutputPostEffect3Source Source { get; }
    public string OwnerIdentity { get; }
    public int Width => descriptor.Width;
    public int Height => descriptor.Height;
    public long LastAttemptNumber { get; private set; }
    public long ResizeGeneration { get; private set; }

    public OriginalMapPostEffect3DescriptorState(OriginalMapFinalOutputPostEffect3Source source,
        int actualContextWidth,int actualContextHeight,string ownedContextIdentity,string contextEvidence)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedContextIdentity);
        Source=source;OwnerIdentity=ownedContextIdentity+"/owned-PostEffect3";
        descriptor=source.CreateFreshForOwnedContext(actualContextWidth,actualContextHeight,
            OwnerIdentity,contextEvidence);
    }

    public OriginalMapPostEffect3ApplyExtentPlan PrepareApply(
        OriginalRenderTextureRuntimeDescriptor actualWorkingTarget,int width,int height,
        int currentCommonMode,long actualAttemptNumber,string actualCallerIdentity)
    {
        if(pending is not null||actualAttemptNumber<=LastAttemptNumber)
            throw new InvalidDataException("PostEffect3 requires one increasing serialized Apply attempt.");
        if(currentCommonMode!=1)
            throw new NotSupportedException("This descriptor owner executes the source PostEffectModule1 branch.");
        var route=OriginalMapFinalOutputHdrRoute.Capture(actualWorkingTarget,width,height,currentCommonMode,
            descriptor,actualAttemptNumber,actualCallerIdentity);
        var plan=new OriginalMapPostEffect3ApplyExtentPlan(this,actualWorkingTarget,width,height,
            actualAttemptNumber,actualCallerIdentity,route,width!=Width||height!=Height);
        pending=plan;resizeAcknowledged=false;LastAttemptNumber=actualAttemptNumber;
        return plan;
    }

    /// <summary>Call after the backend actually resized this module. A later
    /// effect failure does not undo an already completed native resize.</summary>
    public void AcknowledgeResize(OriginalMapPostEffect3ApplyExtentPlan plan,int actualWidth,int actualHeight,
        string actualBackendWriterEvidence)
    {
        RequirePending(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualBackendWriterEvidence);
        if(!plan.RequiresResize||resizeAcknowledged||actualWidth!=plan.Width||actualHeight!=plan.Height)
            throw new InvalidDataException("Resize acknowledgement differs from the exact pending module extent.");
        descriptor=OriginalMapFinalOutputPostEffect3Owner.CreatePresent(descriptor.OwnerIdentity,actualWidth,actualHeight,
            Source.Source.Source.ResourceUri+";actual-owned-resize"+actualBackendWriterEvidence);
        ResizeGeneration++;resizeAcknowledged=true;
    }

    public void CompleteApply(OriginalMapPostEffect3ApplyExtentPlan plan)
    {
        RequirePending(plan);
        if(plan.RequiresResize&&!resizeAcknowledged)
            throw new InvalidDataException("Source Apply cannot execute with its required descriptor resize unacknowledged.");
        pending=null;resizeAcknowledged=false;
    }

    /// <summary>Release a failed attempt. Preserve any resize that its backend
    /// already acknowledged; an unexecuted resize leaves the old extent.</summary>
    public void AbortApply(OriginalMapPostEffect3ApplyExtentPlan plan)
    {RequirePending(plan);pending=null;resizeAcknowledged=false;}

    private void RequirePending(OriginalMapPostEffect3ApplyExtentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if(!ReferenceEquals(pending,plan)||!ReferenceEquals(plan.Owner,this))
            throw new InvalidDataException("PostEffect3 acknowledgement must retain its exact pending owner/attempt.");
        plan.HdrRoute.ValidateSameWorkingTarget(plan.Working,plan.Width,plan.Height,plan.FrameNumber,plan.CallerIdentity);
    }
}
