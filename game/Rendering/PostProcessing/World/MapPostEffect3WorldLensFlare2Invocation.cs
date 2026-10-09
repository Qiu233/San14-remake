using San14.Data;

/// <summary>Borrowed actual first World frontend for factory6. Original
/// procedural draws, compute and After's stock mip draws keep distinct
/// native Bind/Reset lifecycles. No fixture raster or sampler is installed.</summary>
public sealed class MapPostEffect3WorldLensFlare2Invocation:IMapPostEffect3LensFlare2InvocationLease
{
    private readonly MapPostEffect3WorldControlledFactoryFrame frame;
    private readonly MapPostEffect3LensFlare2ExecutionOwner owner;
    public MapPostEffect3WorldLensFlare2Invocation(
        MapPostEffect3WorldControlledFactoryFrame actualFrame,
        MapPostEffect3LensFlare2ExecutionOwner actualOwner)
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
    public void AcceptNativeBefore(MapPostEffect3LensFlare2GpuPassSource source,
        IMapPostEffect3ComputeSamplerLease linear,IMapPostEffect3ComputeSamplerLease point)
    {
        RequireCurrent();frame.Frontend.AcceptLensFlare2NativeBefore(source,linear,point);
        if(source.SourcePassIndex!=45)frame.Frontend.BindLensFlare2Compute(source);
    }
    public Task<IMapPostEffect3FullscreenDrawContextLease> BindDiaphragmAsync(
        MapPostEffect3LensFlare2GpuPassSource source)
    {RequireCurrent();return frame.Frontend.BindLensFlare2DiaphragmAsync(source);}
    public void AcceptCompletedPass(MapPostEffect3LensFlare2GpuPassSource source,
        MapPostEffect3FullscreenCompletedPass? graphics,MapPostEffect3ComputeCompletedPass? compute)
    {RequireCurrent();frame.Frontend.AcceptLensFlare2Pass(owner,source,graphics,compute);}
    public Task<IMapPostEffect3LensFlare2AfterStockLease> BindAfterMipAsync(
        MapPostEffect3LensFlare2MipPlan plan,MapPostEffect3LensFlare2MipStep step)
    {RequireCurrent();return frame.Frontend.BindLensFlare2AfterMipAsync(plan,step);}
    public void AcceptCompletedAfterMip(MapPostEffect3LensFlare2MipCompletedDraw completed)
    {RequireCurrent();frame.Frontend.AcceptLensFlare2AfterMip(owner,completed);}
}
