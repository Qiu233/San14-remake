namespace San14.Data;

/// <summary>Paired native direct-copy route, resolved once with the selected
/// season's other native kernels. This is not a GPU or active-game receipt.</summary>
public sealed class LoadedOriginalMapSeasonCameraPostEffectScalarSource
{
    internal LoadedOriginalMapSeasonCameraPostEffectScalarSource(string locale,string exe,string text,string evidence)
        =>(Locale,ExecutableSha256,DecodedTextSha256,SourceEvidence)=(locale,exe,text,evidence);
    public string Locale{get;}
    public string ExecutableSha256{get;}
    public string DecodedTextSha256{get;}
    public string SourceEvidence{get;}
    public int VerifiedCodeWindowCount=>13;
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
            camera.ImageBase,camera.ExecutableLocale,camera.FrameNumber,camera.FrameProvenance+";"+SourceEvidence,Scalars,this);
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
            Native.Locale!=actual.Native.Locale||Native.ExecutableSha256!=actual.Native.ExecutableSha256||
            Native.DecodedTextSha256!=actual.Native.DecodedTextSha256||
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
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Season optics executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Season optics decoded text");
        int delta=native.Locale=="TC"?0:0x16B0;
        void Check(int tc,int size,string hash)=>VerifyHash(text.AsSpan(tc+delta-start,size),hash,"Season optics direct writer window");
        Check(0xC358A0,42,"4C2F347DDB4DC110C3958375FA8BEEEBBEDDBE740A62C3A575A58D4DEB252923");
        Check(0xC3890B,60,"D3C22C5F0FD48A5BA25DBA3C5477E7234B2C05877DC70F54EA4ADFF254987C7B");
        Check(0xC38977,48,"38D94FCD3B1E40A0A14AA9DDC271D95673AA9B33D7A82038960AF7D2103D8409");
        Check(0xC3845E,24,"2B62325368FBDC33C7C526095BA1B3241A7316B31C390DA9B96F7F35074A6C2C");
        Check(0xC367F5,36,"21D25DCC384A2416262E18525E1E5E0F8A7668BAAF94749B5DCCAF327CD698D2");
        Check(0xC4191B,36,"DCE9876FC9A23BA994E19999C814DA634F6E10133E19BD783E4B3ECED8C4A65F");
        Check(0xC38A4F,24,"4B5CA62699F10E7AD7AD7FAA8D3C536342D420D0EDA4AD758B672D345BC1342D");
        Check(0xC43B4A,24,"297DFE48288D904B9D3A9D4354868054BA145EA2809BECB8287135AFA917A947");
        Check(0xC43A06,48,"1D2F0C01714F1EE1EB2DC1F8E029B8072A5425487776847810C8670B7811D738");
        Check(0xC43A66,48,"E2BDFEE44A43690EF1B3C0ABB43495EB52042043F9035D1C8E3258FC6F23E68F");
        Check(0xC44350,39,"B47EF778A032D7738406D1D86C6E7C1B4EE0ED34CE8E75357D70AC35E6A7D2D0");
        Check(0xC358A0,14320,native.Locale=="TC"?
            "D6EF330729BDC2CB0D194146C41008DD436E3E2DFE771E2F36100560C27126A1":
            "36BE0173188C94C33552BE5E9661BA49C190B6759DD6780026F6B0EE07581AE6");
        int caller=native.Locale=="TC"?0xCB360C:0xCB4CFC;
        VerifyHash(text.AsSpan(caller-start,140),native.Locale=="TC"?
            "AC86EBDF03FFE8D66A980D1AE1ACCC551AF02FAD579BE3E40469A1AC279839E2":
            "6A1E4EE82DE7010941EC77C953C2A2CCB7C64D2C07EA0E27F9C7B0D57A7DF00C","Season optics actual V468 caller");
        void MainCheck(int tc,int sc,int size,string hash)=>
            VerifyHash(text.AsSpan((native.Locale=="TC"?tc:sc)-start,size),hash,"Main17BD0 late flag writer window");
        MainCheck(0xC3E012,0xC3F6C2,11,"EE0F40CCD9D2E7762BC5CAF213C056966700285032C7D494E4A6DA0B0BDFAA70");
        MainCheck(0xC38EFC,0xC3A5AC,12,"416B55878D0F97599B52BCA0E9193A74CAE37D78DC44CBFB8CA1D4E3BF7A3B0D");
        MainCheck(0x17D6A,0x17D6A,42,"6874FDC9D42AB566EBBFFD489DCA78AAC8E39BA3ABBDD31C465C6B4D8E1AF83A");
        MainCheck(0xCB325D,0xCB494D,38,native.Locale=="TC"?
            "7BCE783EE315ECD38CA7C9232EA4EE33D6E2140AAFF83907414A26C898AA10E9":
            "D726187EA99E98DA5E021AC3A9C779B26F07344F2C2348F47B24417BA3969AC6");
        MainCheck(0xCB373F,0xCB4E2F,101,"E6DA81DCED83EF2AF2F1A57EE75E9D6CFE932F34B0A2CBEA56FF9873B74B8E8F");
        MainCheck(0x18106,0x18106,15,"DDCFD933902218A03DB61353DCEB7340398227F4AD2BBA55D30DDC9C38AE28DF");
        MainCheck(0xC40870,0xC41F20,46,native.Locale=="TC"?
            "42F47983EDBC3077A181116F41CDA05981E139B0CE586096F19E08CD6B82FD3E":
            "1662AA518E85726370BF67828ACA8DFA374AD252D3486C2E6FE416ED07C13EFD");
        MainCheck(0xC44006,0xC456B6,12,"63869C3148072F353A26A8EB61ABA65D58128220844B985EC5AB82FAF1301ED3");
        int mainVtable=native.Locale=="TC"?0x16B42E0:0x16B4F90;
        ulong expected468=native.Locale=="TC"?0x140CB3210UL:0x140CB4900UL;
        ulong expected418=native.Locale=="TC"?0x140C40870UL:0x140C41F20UL;
        if(U64(image.Raw(mainVtable+0x468,8),0)!=expected468||
            U64(image.Raw(mainVtable+0x418,8),0)!=expected418)
            throw new InvalidDataException("Main17BD0 flag preparation differs from its exact native V468/V418 entries.");
        return new(native.Locale,native.ExecutableSha256,native.DecodedTextSha256,native.ResourceUri+
            ";paired-thirteen-source-code-windows;V468-actual-direct-selected-copy;selected-R8/target-RDX;"+
            "five-optics-and-three-flags-and-two-LUT-raw-keys;C3845E..C38476/SC-C39B0E..C39B26;"+
            "three-limb-and-two-brightness-current-words;C367F5/C4191B/C38A4F/C43B4A;"+
            "full-copy-body-pins-same-first-RDI-source-and-skipped1078/near-far/FOV/aspect;"+
            "eight-paired-Main17BD0-flag-windows-and-exact-V468/V418-table;no-resource-lookup-or-GPU-current-frame-claim");
    }
}
