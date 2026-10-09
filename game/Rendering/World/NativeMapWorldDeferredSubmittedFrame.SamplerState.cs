public sealed partial class NativeMapWorldDeferredSubmittedFrame
{
    /// <summary>The actual final late raster, joined to the exact submitted
    /// roster. Pixel samplers may be retained from earlier source draws and
    /// must be resolved by the complete owning frontend prefix instead.</summary>
    internal DeferredRasterState RequireLastSubmittedRaster(
        MapSourceCreatedWorldNativeSession session)
    {
        session.RequireSubmittedWorld(this);RequireCurrent(Context);
        return passes[^1].ActualRaster;
    }
    internal sealed partial class SubmittedPass
    {
        internal DeferredRasterState ActualRaster=>raster;
    }
}
