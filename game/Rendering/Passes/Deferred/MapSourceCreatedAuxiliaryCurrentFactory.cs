using System.Buffers.Binary;
using San14.Data;

/// <summary>Same accepted Main/View, selected Common settings and actual Q
/// writers for node97/137. Every value is supplied by its typed current owner;
/// source filter1/N2/scalar25 and depth1 are loaded automatically.</summary>
public sealed record MapSourceCreatedAuxiliaryCurrentFrame(long FrameNumber,
    MapCommonViewFilterCurrent Common, MainMapContextCurrentPacket Context,
    float CameraFov148,float CameraAspect14C,DeferredAuxiliaryCameraFilterSource CameraFilters,
    string CameraSourceProvenance)
{
    public bool ThinSurfaceSourceGate=>Context.ThinSurfaceSourceGate;
    public uint ContextFlags3F0=>Context.ContextFlags3F0;
    public uint FilterType90=>Common.Mode90;
    public int SampleCount94=>Common.SampleCount94;
    public System.Numerics.Vector4 GaussianDispersion=>Common.PreparedGaussianDispersion;
    public int DepthFilterType98=>Common.DepthFilterType98;
    public int TransparencyResolution8C=>Common.TransparencyResolution8C;
    public string CommonSettingsOwnerIdentity=>Common.CommonSettingsOwnerIdentity;
    public string SourceProvenance=>Common.SourceProvenance+";"+Context.SourceProvenance+";"+CameraSourceProvenance;
}

public static class MapSourceCreatedAuxiliaryCurrentFactory
{
    public static MapSourceCreatedAuxiliaryCurrentFrame Prepare(
        MapSourceCreatedNativeSessionPreparedFrame acceptedView,
        MapCommonViewFilterCurrent currentCommon, MainMapContextCurrentPacket currentContext,
        string actualCommonSettingsOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(acceptedView);ArgumentNullException.ThrowIfNull(currentCommon);
        ArgumentNullException.ThrowIfNull(currentContext);
        acceptedView.Selection.Validate();acceptedView.Feature.ValidateFrame(acceptedView.MainCamera,acceptedView.Lighting);
        long frame=acceptedView.Context.FrameNumber;
        currentCommon.ValidateFrame(frame,actualCommonSettingsOwnerIdentity,acceptedView.Feature.CurrentView.ViewIdentity);
        currentContext.Source.Validate(currentContext);
        if(currentContext.FrameNumber!=frame||acceptedView.Lighting.FrameNumber!=frame||
            currentContext.Source.Host.Locale!=acceptedView.MainCamera.ExecutableLocale||
            (currentContext.ContextFlags3F0&~0x400u)!=(acceptedView.Selection.Current.ContextFlags3F0&~0x400u)||
            acceptedView.Context.PreparedCamera is null||
            acceptedView.Context.PreparedCamera!=PreparedCameraSceneSource.ReadCompletePrefix(acceptedView.MainCamera.Prefix.Span))
            throw new InvalidDataException("Auxiliary current sources differ from the accepted Main/View/context frame or current original selection.");
        // Only the closed subsequent OR400 may differ from the earlier shader
        // selection snapshot. Its audited F103 requested-key writers do not
        // consume bit10; every other current context flag stays exact.
        var camera=acceptedView.Lighting.PreparedCameraPrefix.Span;
        float fov=BinaryPrimitives.ReadSingleLittleEndian(camera.Slice(0x148,4));
        float aspect=BinaryPrimitives.ReadSingleLittleEndian(camera.Slice(0x14C,4));
        if(!float.IsFinite(fov)||!float.IsFinite(aspect)||fov<=0||aspect<=0)
            throw new InvalidDataException("Auxiliary current projection requires actual finite accepted Main FOV/aspect.");
        var main=acceptedView.CpuPopulation?.MainCamera??
            throw new InvalidDataException("Auxiliary filters require the actual accepted source Main Camera owner.");
        if(!main.Camera.Prefix.Span.SequenceEqual(acceptedView.MainCamera.Prefix.Span))
            throw new InvalidDataException("Auxiliary late Camera words belong to another accepted Main Camera.");
        var filters=DeferredAuxiliaryCameraFilterSource.CaptureSourceLoadedMain(main,
            AuxiliaryCameraFilterScope.SourceLoadedCameraNoLaterFilterWriter);
        filters.RequireSameMain(main);
        return new(frame,currentCommon,currentContext,fov,aspect,filters,filters.SourceProvenance);
    }
}
