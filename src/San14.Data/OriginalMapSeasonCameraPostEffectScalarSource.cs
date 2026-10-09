namespace San14.Data;

/// <summary>Paired native direct-copy route, resolved once with the selected
/// season's other native kernels. This is not a GPU or active-game receipt.</summary>
public sealed class LoadedOriginalMapSeasonCameraPostEffectScalarSource
{
    internal LoadedOriginalMapSeasonCameraPostEffectScalarSource(string locale,string evidence)
        =>(Locale,SourceEvidence)=(locale,evidence);
    public string Locale{get;}
    public string RuleVersion=>RenderRuleIdentity.Version;
    public string SourceEvidence{get;}
    public int VerifiedCodeWindowCount=>0;
    public int VerifiedMainPreparationWindowCount=>8;
    public OriginalMapSeasonCameraPostEffectScalarWriteIdentity CaptureDirectSelection(
        OriginalMapSeasonCameraSelection selected,OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(camera);
        if(camera.ExecutableLocale!=Locale)
            throw new InvalidDataException("Season optics native producer and accepted Camera locales differ.");
        var scalars=OriginalMapSeasonCameraPostEffectScalars.CaptureDirectSelection(selected,camera);
        return new(this,selected,camera,scalars);
    }

    /// <summary>Main17BD0 passes flags8 to V468. After the selected source
    /// copy, CB379C sets CPU1240.bit0; the subsequent V418 publishes1510.
    /// A plain direct-copy receipt does not include this Main caller action.</summary>
    public OriginalMapSeasonCameraPostEffectScalarWriteIdentity CaptureActualMainPreparation(
        OriginalSkySeasonMainCameraInputs inputs,OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(inputs);ArgumentNullException.ThrowIfNull(camera);
        var prior=inputs.MainFrame.Camera;
        if(!ReferenceEquals(inputs.Source.PostEffectScalarSource,this)||
            inputs.FrameNumber!=camera.FrameNumber||prior.FrameNumber!=camera.FrameNumber||
            prior.ExecutableLocale!=Locale||camera.ExecutableLocale!=Locale||
            prior.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId||
            camera.Source.Camera.ObjectId!=prior.Source.Camera.ObjectId||
            camera.Source.Resource.Source.ResourceUri!=prior.Source.Resource.Source.ResourceUri||
            camera.Source.Resource.Source.DecodedSha256!=prior.Source.Resource.Source.DecodedSha256)
            throw new InvalidDataException("Main flag publication requires its actual same-frame seasonal Main17BD0 inputs.");
        var selected=inputs.Source.Selection;
        var direct=CaptureDirectSelection(selected,camera);
        // The stock system Camera reader does not admit seasonal resources.
        // For this canonical fresh seasonal package, CPU1240 has no property
        // writer; the pinned C3E012 constructor's qword123C high word is zero.
        // Later runtime edits need their own selected-word receipt.
        if(selected.FirstPackage.Camera.ClassId!=OriginalMapCameraConstructionSource.PerspectiveCameraClassId||
            selected.FirstPackage.Camera.Properties.Count!=1119)
            throw new NotSupportedException("Main flags require the canonical fresh seasonal Perspective constructor scope.");
        uint selectedConstructorWord1240=0;
        uint prepared1510=selectedConstructorWord1240|1u;
        var finalScalars=direct.Scalars.WithSelectedCurrentReaders(selected,camera).WithActualMainPhase(inputs,camera);
        if(inputs.MainPostSelection is{}post)finalScalars=finalScalars.WithMainPostSelection(post,camera);
        return new(this,selected,camera,finalScalars,prepared1510,inputs);
    }
}

/// <summary>Immutable identity for the thirteen actual scalar writes and two
/// current raw LUT keys. Prefix
/// equality alone cannot compare a Camera's separately prepared optical tail.</summary>
public sealed class OriginalMapSeasonCameraPostEffectScalarWriteIdentity
{
    private readonly uint[] words;
    private readonly OriginalSkySeasonMainCameraInputs? actualMainPreparation;
    internal OriginalMapSeasonCameraPostEffectScalarWriteIdentity(
        LoadedOriginalMapSeasonCameraPostEffectScalarSource native,OriginalMapSeasonCameraSelection selected,
        OriginalMapCameraConstructionPacket camera,OriginalMapSeasonCameraPostEffectScalars scalars,
        uint? preparedSpecialRectangleFlag1510=null,OriginalSkySeasonMainCameraInputs? actualMainPreparation=null)
    {
        Native=native;Scalars=scalars;FrameNumber=camera.FrameNumber;
        MainResourceUri=camera.Source.Resource.Source.ResourceUri;
        MainResourceSha256=camera.Source.Resource.Source.DecodedSha256;MainCameraObjectId=camera.Source.Camera.ObjectId;
        SelectedResourceUri=selected.FirstPackage.Objects.Source.ResourceUri;
        SelectedResourceSha256=selected.FirstPackage.Objects.Source.DecodedSha256;
        SelectedCameraObjectId=selected.FirstPackage.Camera.ObjectId;SelectionPolicy=selected.SelectionPolicy;
        FirstIndex=selected.FirstIndex;WeightBits=BitConverter.SingleToUInt32Bits(selected.Weight);
        PreparedSpecialRectangleFlag1510=preparedSpecialRectangleFlag1510;
        this.actualMainPreparation=actualMainPreparation;
        words=scalars.PreparedCameraOffsets.Select(at=>scalars.TryReadWord(at,out uint value)?value:
            throw new InvalidDataException("Missing actual season optics word in native writer receipt.")).ToArray();
    }
    private OriginalMapSeasonCameraPostEffectScalarWriteIdentity(
        OriginalMapSeasonCameraPostEffectScalarWriteIdentity prior,long frame,string evidence)
    {
        Native=prior.Native;Scalars=prior.Scalars.ForOwnedFrame(frame,evidence);FrameNumber=frame;
        MainResourceUri=prior.MainResourceUri;MainResourceSha256=prior.MainResourceSha256;MainCameraObjectId=prior.MainCameraObjectId;
        SelectedResourceUri=prior.SelectedResourceUri;SelectedResourceSha256=prior.SelectedResourceSha256;
        SelectedCameraObjectId=prior.SelectedCameraObjectId;SelectionPolicy=prior.SelectionPolicy;
        FirstIndex=prior.FirstIndex;WeightBits=prior.WeightBits;words=prior.words.ToArray();
        PreparedSpecialRectangleFlag1510=prior.PreparedSpecialRectangleFlag1510;
        actualMainPreparation=prior.actualMainPreparation;
    }
    public LoadedOriginalMapSeasonCameraPostEffectScalarSource Native{get;}
    public OriginalMapSeasonCameraPostEffectScalars Scalars{get;}
    public long FrameNumber{get;}
    public string MainResourceUri{get;}
    public string MainResourceSha256{get;}
    public uint MainCameraObjectId{get;}
    public string SelectedResourceUri{get;}
    public string SelectedResourceSha256{get;}
    public uint SelectedCameraObjectId{get;}
    public OriginalMapSeasonCameraSelectionPolicy SelectionPolicy{get;}
    public int FirstIndex{get;}
    public uint WeightBits{get;}
    public uint? PreparedSpecialRectangleFlag1510{get;}
    public string SourceEvidence=>Native.SourceEvidence+";"+Scalars.SourceEvidence+
        (PreparedSpecialRectangleFlag1510 is null?";direct-copy-only/no-Main-late-flags":
            ";Main17BD0-flags8/CB379C-post-copy-CPU1240-OR1/V418-prepared1510");
    internal OriginalMapSeasonCameraPostEffectScalarWriteIdentity ForOwnedFrame(long frame,string evidence)
        =>new(this,frame,evidence);
    public OriginalMapCameraConstructionPacket ApplyToSameAcceptedCamera(OriginalMapCameraConstructionPacket camera)
    {
        Scalars.ValidateForCamera(camera);
        return new(camera.Prefix.ToArray(),camera.Source,camera.CurrentPose,camera.Lens,camera.Matrices,
            camera.ExecutableLocale,camera.FrameNumber,camera.FrameProvenance+";"+SourceEvidence,Scalars,this);
    }
    internal void RequireActualMainPreparation(OriginalSkySeasonMainCameraInputs inputs,
        OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(inputs);ArgumentNullException.ThrowIfNull(camera);
        Scalars.ValidateForCamera(camera);
        if(!ReferenceEquals(actualMainPreparation,inputs)||
            !ReferenceEquals(Native,inputs.Source.PostEffectScalarSource)||
            FrameNumber!=inputs.FrameNumber||FrameNumber!=camera.FrameNumber||
            PreparedSpecialRectangleFlag1510 is null)
            throw new InvalidDataException("Prepared LUT transfer requires this current actual Main writer and input boundary.");
        var selected=inputs.Source.Selection;
        if(SelectedResourceUri!=selected.FirstPackage.Objects.Source.ResourceUri||
            SelectedResourceSha256!=selected.FirstPackage.Objects.Source.DecodedSha256||
            SelectedCameraObjectId!=selected.FirstPackage.Camera.ObjectId||
            SelectionPolicy!=selected.SelectionPolicy||FirstIndex!=selected.FirstIndex||
            WeightBits!=BitConverter.SingleToUInt32Bits(selected.Weight))
            throw new InvalidDataException("Prepared LUT transfer has another current selected package or interpolation argument.");
    }
    public void RequireEquivalentWrite(OriginalMapSeasonCameraPostEffectScalarWriteIdentity actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if(FrameNumber!=actual.FrameNumber||MainResourceUri!=actual.MainResourceUri||
            MainResourceSha256!=actual.MainResourceSha256||MainCameraObjectId!=actual.MainCameraObjectId||
            Native.Locale!=actual.Native.Locale||
            SelectedResourceUri!=actual.SelectedResourceUri||SelectedResourceSha256!=actual.SelectedResourceSha256||
            SelectedCameraObjectId!=actual.SelectedCameraObjectId||SelectionPolicy!=actual.SelectionPolicy||
            FirstIndex!=actual.FirstIndex||WeightBits!=actual.WeightBits||
            !ReferenceEquals(actualMainPreparation,actual.actualMainPreparation)||
            PreparedSpecialRectangleFlag1510!=actual.PreparedSpecialRectangleFlag1510||!words.SequenceEqual(actual.words))
            throw new InvalidDataException("Camera prefix matches but its current season optics writer differs.");
    }
}

public static class OriginalMapSeasonCameraPostEffectScalarSource
{
    public static LoadedOriginalMapSeasonCameraPostEffectScalarSource Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadSeasonPostEffectScalarSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapSeasonCameraPostEffectScalarSource ReadSeasonPostEffectScalarSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Season optics requires its paired SC/TC native source.");

        return new(native.Locale,native.RenderRuleIdentity+
            ";paired-thirteen-source-code-windows;V468-actual-direct-selected-copy;selected-R8/target-RDX;"+
            "five-optics-and-three-flags-and-two-LUT-raw-keys;C3845E..C38476/SC-C39B0E..C39B26;"+
            "three-limb-and-two-brightness-current-words;C367F5/C4191B/C38A4F/C43B4A;"+
            "full-copy-body-pins-same-first-RDI-source-and-skipped1078/near-far/FOV/aspect;"+
            "eight-paired-Main17BD0-flag-windows-and-exact-V468/V418-table;no-resource-lookup-or-GPU-current-frame-claim");
    }
}
