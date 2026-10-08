using San14.Data;

/// <summary>The source-selected storage sides of this actual Middle frame.
/// Retained module selections remain tied to their invocation, not relabelled
/// as fresh constructor selections or used as proof of GPU initialization.</summary>
public sealed class OriginalMapPostEffect3MiddleAllocationSides
{
    private readonly OriginalMapPostEffect3AllocationPlan allocation;
    private readonly OriginalMapPostEffect3OwnedFreshCameraFrame frame;
    private readonly OriginalMapPostEffect3FreshOwnedAllocationSides? fresh;
    private readonly OriginalMapPostEffect3RetainedModuleInvocation? invocation;
    public IReadOnlyDictionary<int,int> TextureInstances{get;}
    public IReadOnlyDictionary<int,int> BufferInstances{get;}
    public IReadOnlyDictionary<int,int> ConstantBufferInstances{get;}
    public long FrameNumber=>frame.FrameNumber;
    public bool ProvesGpuContentsInitialized=>false;

    private OriginalMapPostEffect3MiddleAllocationSides(OriginalMapPostEffect3OwnedFreshCameraFrame frame,
        OriginalMapPostEffect3FreshOwnedAllocationSides source)
    {
        this.frame=frame;allocation=frame.Allocation;fresh=source;
        TextureInstances=source.TextureInstances;BufferInstances=source.BufferInstances;
        ConstantBufferInstances=source.ConstantBufferInstances;
    }
    private OriginalMapPostEffect3MiddleAllocationSides(OriginalMapPostEffect3OwnedFreshCameraFrame frame,
        OriginalMapPostEffect3RetainedModuleInvocation source)
    {
        this.frame=frame;allocation=frame.Allocation;invocation=source;
        TextureInstances=source.TextureInstances;BufferInstances=source.BufferInstances;
        ConstantBufferInstances=source.ConstantBufferInstances;
    }
    internal static OriginalMapPostEffect3MiddleAllocationSides FromFresh(
        OriginalMapPostEffect3OwnedFreshCameraFrame frame,OriginalMapPostEffect3FreshOwnedAllocationSides source)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(source);
        var result=new OriginalMapPostEffect3MiddleAllocationSides(frame,source);result.ValidateFor(frame.Allocation);return result;
    }
    internal static OriginalMapPostEffect3MiddleAllocationSides FromInvocation(
        OriginalMapPostEffect3OwnedFreshCameraFrame frame,OriginalMapPostEffect3RetainedModuleInvocation source)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(source);
        var result=new OriginalMapPostEffect3MiddleAllocationSides(frame,source);result.ValidateFor(frame.Allocation);return result;
    }
    public void ValidateFor(OriginalMapPostEffect3AllocationPlan actualAllocation)
    {
        ArgumentNullException.ThrowIfNull(actualAllocation);frame.RequireCurrent();
        if(!ReferenceEquals(allocation,actualAllocation)||actualAllocation.ResizeOnly)
            throw new InvalidDataException("Middle sides require this exact original module allocation.");
        if(invocation is not null)
        {
            invocation.RequireModule(actualAllocation);invocation.RequireCurrent();
            invocation.RequireInitializedFrame(frame);
            if(invocation.FrameNumber!=frame.FrameNumber)
                throw new InvalidDataException("Middle sides belong to a different retained module frame.");
        }
        else fresh!.ValidateFor(actualAllocation);
    }
}
