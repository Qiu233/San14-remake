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
            Native.ExecutableSha256!=inputs.Source.PostEffectScalarSource.ExecutableSha256||
            Native.DecodedTextSha256!=inputs.Source.PostEffectScalarSource.DecodedTextSha256||
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
        const string sourceScope="OriginalMapSeasonPreparedLutSource.cs";
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Prepared LUT executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Prepared LUT decoded text");
        void Check(int rva,int size,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,sourceScope,rva,size,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,size),actual.Sha,"Prepared LUT native window");
        }
        Check(0x17BD0,0x1D,"044AB8A7238C0F4A868EBD7792885DFF9770FBB404FF75518618505A1DADE464");
        Check(0x17CFC,0x48,"7D948E1340E0FC07949FB854CAC6F7C592DCB60626CB946BFEC775B9B759B2EF");
        Check(0x17E50,0x33,"DA6174BE849AC7E3B83FDB554F11AFE5297AABF217A19CB6CF9FA85313655DC5");
        Check(0x17D6A,0x2A,"6874FDC9D42AB566EBBFFD489DCA78AAC8E39BA3ABBDD31C465C6B4D8E1AF83A");
        Check(0xCB3430,0x33,"6F7247EEA983239FC9966A17A60329223FAC2DE78CD93B17D9E9E8BCE4DADB22");
        Check(0xCB3698,0x68,"22B3A8A1E6E3FB913C5466A2B4750B253B5CBEBA2EA1214F6C7AE87D059A2550");
        Check(0xCAB260,0x2C,"CE8795DF7AD4539F96FA8B46407DEDA0CF29472A5102B955576D208A0CF79F97");
        Check(0xC57BF0,0x91,"C0C2014B6E0B83887E38B23272F9A278C2B1AD58A09522FA7056F775C0D2E629");
        int vtable=OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,sourceScope,0x16B42E0);
        int caller=OriginalMapPostEffect3NativeSourceProfile.CodeRva(native.Locale,sourceScope,0xCB3210);
        if(U64(image.Raw(vtable+0x468,8),0)!=0x140000000UL+(uint)caller)
            throw new InvalidDataException("Main Camera.v468 is not the exact prepared LUT caller.");
        return new(native,native.ResourceUri+"@"+native.ExecutableSha256+
            $";{native.Locale}:Main17BD0 RBP=RSP+100/control54=RBP-3C;owner28C>=0-or-auto0..4;actualVT={vtable:X8}/v468={caller:X8};TC-reference-role:"+
            "17D3A/17E62-control54-nonnegative;17D8D-cameraV468/VT16B42E0=CB3210;"+
            "CB3430-control54/CB3457-explicit-sixth-arg=-1=>ESI-nonnegative;"+
            "CB369F-RDX-selectedCamera/CB36C4-CAAEE0;CB36CB-jns-no-old-handle-restore;"+
            "selected-source-C57BF0-932/C57C30-cache2EC8,C57C3A-933/C57C7A-cache2ED0;"+
            "CAB260/267-source2EC8=>Main2B58/prepared1828;CAB26E/275-source2ED0=>Main2B60/prepared1830;"+
            "successful-owned-lookup-still-required;not-original-GPU-selection-observation");
    }
}
