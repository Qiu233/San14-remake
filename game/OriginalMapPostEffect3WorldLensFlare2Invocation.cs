using San14.Data;

/// <summary>Borrowed actual first World frontend for factory6. Original
/// procedural draws, compute and After's stock mip draws keep distinct
/// native Bind/Reset lifecycles. No fixture raster or sampler is installed.</summary>
public sealed class OriginalMapPostEffect3WorldLensFlare2Invocation:IOriginalMapPostEffect3LensFlare2InvocationLease
{
    private readonly OriginalMapPostEffect3WorldControlledFactoryFrame frame;
    private readonly OriginalMapPostEffect3LensFlare2ExecutionOwner owner;
    public OriginalMapPostEffect3WorldLensFlare2Invocation(
        OriginalMapPostEffect3WorldControlledFactoryFrame actualFrame,
        OriginalMapPostEffect3LensFlare2ExecutionOwner actualOwner)
    {
        ArgumentNullException.ThrowIfNull(actualFrame);ArgumentNullException.ThrowIfNull(actualOwner);
        frame=actualFrame;owner=actualOwner;RequireCurrent();
    }
    public void RequireCurrent()
    {
        frame.RequireCurrent();
        if(!ReferenceEquals(owner.Bank,frame.World.Bank))
            throw new InvalidDataException("LensFlare2 World invocation belongs to another actual module bank.");
    }
    public void AcceptNativeBefore(OriginalMapPostEffect3LensFlare2GpuPassSource source,
        IOriginalMapPostEffect3ComputeSamplerLease linear,IOriginalMapPostEffect3ComputeSamplerLease point)
    {
        RequireCurrent();frame.Frontend.AcceptLensFlare2NativeBefore(source,linear,point);
        if(source.SourcePassIndex!=45)frame.Frontend.BindLensFlare2Compute(source);
    }
    public Task<IOriginalMapPostEffect3FullscreenDrawContextLease> BindDiaphragmAsync(
        OriginalMapPostEffect3LensFlare2GpuPassSource source)
    {RequireCurrent();return frame.Frontend.BindLensFlare2DiaphragmAsync(source);}
    public void AcceptCompletedPass(OriginalMapPostEffect3LensFlare2GpuPassSource source,
        OriginalMapPostEffect3FullscreenCompletedPass? graphics,OriginalMapPostEffect3ComputeCompletedPass? compute)
    {RequireCurrent();frame.Frontend.AcceptLensFlare2Pass(owner,source,graphics,compute);}
    public Task<IOriginalMapPostEffect3LensFlare2AfterStockLease> BindAfterMipAsync(
        OriginalMapPostEffect3LensFlare2MipPlan plan,OriginalMapPostEffect3LensFlare2MipStep step)
    {RequireCurrent();return frame.Frontend.BindLensFlare2AfterMipAsync(plan,step);}
    public void AcceptCompletedAfterMip(OriginalMapPostEffect3LensFlare2MipCompletedDraw completed)
    {RequireCurrent();frame.Frontend.AcceptLensFlare2AfterMip(owner,completed);}
}
