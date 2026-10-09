namespace San14.Data;

/// <summary>Source Main17BD0 prepares the selected external Camera's LUT
/// cache without restoring the preceding prepared handles. This source route
/// does not manufacture a successful lookup or a GPU texture capability.</summary>
public sealed class LoadedOriginalMapSeasonPreparedLutSource
{
    internal LoadedOriginalMapSeasonPreparedLutSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public OriginalMapSeasonPreparedLutTransfer CaptureActualMainPreparation(
        OriginalSkySeasonMainCameraInputs actualMainInputs,OriginalMapCameraConstructionPacket actualPostSeasonCamera)
    {
        Validate(actualMainInputs,actualPostSeasonCamera);
        return new(this,actualMainInputs,actualPostSeasonCamera);
    }
    internal void Validate(OriginalSkySeasonMainCameraInputs inputs,OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(inputs);ArgumentNullException.ThrowIfNull(camera);
        var prior=inputs.MainFrame.Camera;
        var write=camera.CurrentSeasonPostEffectWriteIdentity??
            throw new InvalidDataException("Prepared LUT transfer requires the same actual season write identity.");
        if(inputs.FrameNumber!=camera.FrameNumber||inputs.FrameNumber!=prior.FrameNumber||
            Native.Locale!=camera.ExecutableLocale||Native.Locale!=inputs.Source.PostEffectScalarSource.Locale||
            prior.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            prior.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            prior.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256)
            throw new InvalidDataException("Main selected Camera, native prepared route and accepted Camera belong to different frames or sources.");
        // The actual writer already ran the current scalar calculation. Join
        // its same Main input boundary instead of replaying that producer and
        // reimporting the complete static provider for each lookup validation.
        write.RequireActualMainPreparation(inputs,camera);
    }
}

public sealed class OriginalMapSeasonPreparedLutTransfer
{
    private readonly OriginalMapCameraConstructionPacket camera;
    internal OriginalMapSeasonPreparedLutTransfer(LoadedOriginalMapSeasonPreparedLutSource native,
        OriginalSkySeasonMainCameraInputs inputs,OriginalMapCameraConstructionPacket camera)
    {Native=native;MainInputs=inputs;this.camera=camera.CopyAcceptedSnapshot();}
    public LoadedOriginalMapSeasonPreparedLutSource Native{get;}
    public OriginalSkySeasonMainCameraInputs MainInputs{get;}
    public long FrameNumber=>camera.FrameNumber;
    public uint HdrLutKey932=>camera.HdrLutKey932;
    public uint LdrLutKey933=>camera.LdrLutKey933;
    public uint SelectedCameraObjectId=>MainInputs.Source.Selection.FirstPackage.Camera.ObjectId;
    public string SelectedCameraResourceUri=>MainInputs.Source.Selection.FirstPackage.Objects.Source.ResourceUri;
    public string SelectedCameraResourceSha256=>MainInputs.Source.Selection.FirstPackage.Objects.Source.DecodedSha256;
    public bool SourceRestoresPreviousPreparedPrimaryHandles=>false;
    public bool RequiresSuccessfulLookupForNonNullPresence=>true;
    public bool ProvesOriginalActiveGpuFrame=>false;
    public bool ProvesGpuTextureReady=>false;
    public string SourceEvidence=>Native.SourceEvidence+";"+camera.CurrentSeasonPostEffectWriteIdentity!.SourceEvidence;
    public void ValidateSameCamera(OriginalMapCameraConstructionPacket actualPostSeasonCamera)
    {
        Native.Validate(MainInputs,actualPostSeasonCamera);
        camera.RequireEquivalentCurrentPostEffectScalars(actualPostSeasonCamera);
        if(!camera.Prefix.Span.SequenceEqual(actualPostSeasonCamera.Prefix.Span))
            throw new InvalidDataException("Prepared LUT transfer belongs to a different current Camera prefix.");
    }
}

public static class OriginalMapSeasonPreparedLutSource
{
    public static LoadedOriginalMapSeasonPreparedLutSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadSeasonPreparedLutSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapSeasonPreparedLutSource ReadSeasonPreparedLutSource(LoadedOriginalDeferredSamplerNativeSource native)
    {
        const string implementationProfile="ce-main-season-prepared-lut-v1";
        return new(native,implementationProfile+
            ";selected-current-Camera;control-index-nonnegative;no-previous-handle-restore;"+
            "HDR-cache2EC8=>Main2B58/prepared1828;LDR-cache2ED0=>Main2B60/prepared1830;"+
            "successful-owned-lookup-still-required");
    }
}
