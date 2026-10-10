namespace San14.Data;

public sealed partial class MapSourceCreatedLightingPacket
{
    /// <summary>Capture the actual current Main Camera after this lighting
    /// owner's successful season RGB/weather/clock writes. Geometry's accepted
    /// Main snapshot is the exact prior stage; it is not substituted for the
    /// later3C0 Camera consumed by post output. No guard or source byte changes.</summary>
    public MapCameraConstructionPacket CapturePreparedCameraFromSameAcceptedMain(
        MapCameraConstructionPacket acceptedMain)
    {
        ArgumentNullException.ThrowIfNull(acceptedMain);
        var present=acceptedInputs.PresentSeasonCamera??
            throw new NotSupportedException("Prepared source Camera capture requires the actual owned successful season writer.");
        present.ValidatePreparedLighting(this);
        var prior=present.AcceptedSeasonCamera.Inputs.MainFrame.Camera;
        var source=present.AcceptedSeasonCamera.Inputs.Source;
        var expectedAfterSelectedScalarCopies=present.AcceptedSeasonCamera.Inputs.BeforeFinalPreparationPrefix;
        if(acceptedMain.FrameNumber!=FrameNumber||prior.FrameNumber!=FrameNumber||
            acceptedMain.ExecutableLocale!=prior.ExecutableLocale||
            acceptedMain.Source.Camera.ObjectId!=SourceIdentity.CameraObjectId||
            acceptedMain.Source.Resource.Source.ResourceUri!=SourceIdentity.CameraResourceUri||
            acceptedMain.Source.Resource.Source.DecodedSha256!=SourceIdentity.CameraSha256||
            !acceptedMain.Prefix.Span.SequenceEqual(prior.Prefix.Span)||
            !expectedAfterSelectedScalarCopies.Span[..0x1D0].SequenceEqual(camera.AsSpan(0,0x1D0))||
            SceneInputs.Camera!=PreparedCameraSceneSource.ReadCompletePrefix(camera))
            throw new InvalidDataException("Current prepared season Camera does not belong to this exact accepted Main/lighting source generation.");
        var main=acceptedMain.CopyAcceptedSnapshot();
        var optics=source.PostEffectScalarSource.CaptureActualMainPreparation(present.AcceptedSeasonCamera.Inputs,main);
        return new(camera.ToArray(),main.Source,main.Geometry,
            main.ExecutableLocale,FrameNumber,
            main.FrameProvenance+";"+present.SourceProvenance+";actual-post-season-Camera3C0-from-owned-lighting-writer",optics.Scalars,optics,
            optics.Scalars.ApplyOpticalTailTo(CameraOpticsEncoding.DecodePrefix(main.Source,camera)));
    }
}
