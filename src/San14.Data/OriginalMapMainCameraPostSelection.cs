using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>The complete Main17BD0 direct-store family between V468 and V418.
/// Helpers and later weather writers have independent ownership.</summary>
public sealed class LoadedOriginalMapMainCameraPostSelectionSource
{
    internal LoadedOriginalMapMainCameraPostSelectionSource(string locale) => Locale=OriginalMainMapCameraRules.Language(locale);
    public string Locale {get;}
    public string RuleVersion => OriginalMainMapCameraRules.Version;
    public string SourceEvidence => $"{RuleVersion};{Locale};post-selection-direct-fields;fresh-app";
    private const uint ScalarOne=0x3F800000;
    private const uint ScalarMinusOne=0xBF800000;
    private const uint ScalarPointOne=0x3DCCCCCD;
    private const uint Distance200000=0x48435000;
    private const uint Distance5000=0x459C4000;
    private const uint ScalarFourThirdsApproximation=0x3FAAA993;
    private const uint Distance25000=0x46C35000;
    private const uint Distance2000=0x44FA0000;
    private const uint Distance32000=0x46FA0000;
    private const uint Distance50000=0x47435000;
    private const uint Distance29000=0x46E29000;
    private const uint Distance40000=0x471C4000;
    private const uint ScalarPoint001=0x3A83126F;
    private const uint ScalarPoint0001=0x38D1B717;
    private const uint ScalarTwo=0x40000000;
    private const uint Distance300=0x43960000;
    /// <summary>Pure source direct stores for explicit App/config inputs.
    /// This projection does not execute helpers, geometry or native process state.</summary>
    public IReadOnlyDictionary<int,uint> ProjectDirectWrites(int currentQuality,int currentMode,
        uint appFlags,uint priorCloudyBits,uint selectedCloudyBits)
    {
        int m=currentMode,g=currentQuality;
        uint flags0=appFlags & 0xFFFF,flags2=appFlags>>16;
        uint cloudy=selectedCloudyBits;
        float previousCloudy430=BitConverter.UInt32BitsToSingle(priorCloudyBits);
        bool low=g<1;
        Dictionary<int,uint> words=[];
        void W(int prepared,uint bits)=>words.Add(prepared,bits);
        W(0x1C4,0); //CPU74 LightAdaptedScale, not Controller.AutoDateTimeStepType.
        if(m>=3){W(0x1C8,ScalarPointOne);cloudy=0;}
        if(!low)cloudy=0;
        W(0x1D0,cloudy);
        W(0xAC0,4);
        W(0xB3C,low?Distance200000:Distance5000);
        W(0xB08,low?ScalarFourThirdsApproximation:ScalarOne);
        W(0x414,low?0u:1u);W(0x418,low?0u:2u);
        W(0x840,low?1u:0u);W(0x84C,low?3u:0u);
        W(0x8E0,low?1u:0u);W(0x868,low?3u:0u);
        W(0x180,low?ScalarPointOne:0u);W(0x188,low?ScalarOne:0u);
        W(0x1320,low?1u:0u);W(0x1324,low?ScalarPointOne:ScalarMinusOne);
        W(0x1344,low?16u:0u);W(0x674,low?2u:1u);W(0x7A0,low?1u:2u);
        W(0x101C,low||(flags2&1)!=0?ScalarOne:0u);
        float currentCloudy=BitConverter.UInt32BitsToSingle(cloudy);
        W(0x210,previousCloudy430==currentCloudy?0u:ScalarPoint001);
        if((flags0&0x200)!=0)W(0x151C,0x8BAC7B6D);
        if((flags0&0x20)!=0&&g<2)
        {
            bool distant=m>0;
            W(0xA00,8);W(0xA3C,distant?9u:11u);
            W(0x9F4,(distant?Distance25000:Distance2000));
            W(0x9EC,(distant?Distance32000:Distance50000));
            W(0x9F0,(distant?Distance29000:Distance40000));
            W(0xA08,(distant?ScalarPoint0001:ScalarPoint001));
            W(0xA9C,(distant?ScalarPoint0001:ScalarPoint001));W(0xA88,distant?7u:0u);
        }
        else W(0xA00,0);
        W(0xF74,m<3?ScalarTwo:0u);W(0xF60,m<3?Distance300:0u);W(0xF80,m<3?1u:0u);
        return new ReadOnlyDictionary<int,uint>(words);
    }

}

/// <summary>One owned App's retained inputs to Main17BD0. The fresh factory
/// admits only constructor/reset state; subsequent receipts are remake state.</summary>
public sealed class OriginalMapMainCameraPostSelectionOwner
{
    private readonly LoadedOriginalMapMainCameraPostSelectionSource source;
    private readonly OriginalMainMapCameraModeOwner mainOwner;
    private readonly uint flags0,flags2;
    private float previousCloudy430;
    private long revision;
    private OriginalMapMainCameraPostSelectionOwner(LoadedOriginalMapMainCameraPostSelectionSource source,
        OriginalMainMapCameraModeOwner mainOwner,uint flags0,uint flags2,float previousCloudy)
    {this.source=source;this.mainOwner=mainOwner;this.flags0=flags0;this.flags2=flags2;previousCloudy430=previousCloudy;}

    public static OriginalMapMainCameraPostSelectionOwner CreateFresh(OriginalGameRepository repository,
        OriginalMainMapCameraModeOwner mainOwner)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(mainOwner);
        var source=OriginalMapMainCameraPostSelectionSource.Load(repository,mainOwner.Locale);
        return new(source,mainOwner,0xE2,0,0);
    }
    public OriginalMapMainCameraPostSelectionPacket PrepareCurrent(OriginalSkySeasonMainCameraSource selected,
        OriginalMainMapCameraModePacket mode,OriginalMapGameLightQualityOwner quality)
    {
        ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(mode);ArgumentNullException.ThrowIfNull(quality);
        mode.ValidateCurrent(mainOwner,mode.AcceptedMainFrame);
        var main=mode.AcceptedMainFrame.Camera;
        if(selected.FrameNumber!=main.FrameNumber||source.Locale!=main.ExecutableLocale||
            source.Locale!=quality.Locale||selected.Selection.SecondPackage is not null)
            throw new InvalidDataException("Main post-selection requires the same owned Main, setting and direct seasonal frame.");
        selected.Selection.Validate();
        uint cloudy=ReadCloudy(selected);
        int m=mode.Mode288,g=quality.CurrentQualityOption18;
        uint appFlags=flags0|(flags2<<16),priorCloudy=BitConverter.SingleToUInt32Bits(previousCloudy430),selectedCloudy=cloudy;
        var words=new Dictionary<int,uint>(source.ProjectDirectWrites(g,m,appFlags,priorCloudy,selectedCloudy));
        cloudy=words[0x1D0];
        float currentCloudy=BitConverter.UInt32BitsToSingle(cloudy);
        string evidence=source.SourceEvidence+";"+mode.SourceProvenance+";"+quality.SourceProvenance+
            $";owned-App-revision={++revision};flags0={flags0:X};flags2={flags2:X};prior430={BitConverter.SingleToUInt32Bits(previousCloudy430):X8};new430={cloudy:X8};App290-reset-minus1/no-positive-FOV-override";
        var result=new OriginalMapMainCameraPostSelectionPacket(main,selected.Selection,words,g,m,
            appFlags,priorCloudy,selectedCloudy,evidence);
        previousCloudy430=currentCloudy;
        return result;
    }
    private static uint ReadCloudy(OriginalSkySeasonMainCameraSource selected)
    {
        var field=selected.Selection.FirstPackage.Camera.Properties[850];
        if(field.Index!=850||field.NameHash!=0x2F6A6AF9||field.TypeCode!=8||field.ElementCount!=1||field.RawValue.Length!=4)
            throw new InvalidDataException("Main cloudy lacks its exact selected CPU7C source property.");
        uint bits=BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        if(!float.IsFinite(BitConverter.UInt32BitsToSingle(bits)))throw new InvalidDataException("Main cloudy is nonfinite.");
        return bits;
    }
}

public sealed class OriginalMapMainCameraPostSelectionPacket
{
    private readonly string locale,uri,sha;
    private readonly string selectedUri,selectedSha;
    private readonly uint selectedCamera;
    private readonly int selectedIndex;
    internal OriginalMapMainCameraPostSelectionPacket(OriginalMapCameraConstructionPacket main,
        OriginalMapSeasonCameraSelection selected,Dictionary<int,uint> words,int quality,int mode,
        uint appFlags,uint priorCloudy,uint selectedCloudy,string evidence)
    {FrameNumber=main.FrameNumber;locale=main.ExecutableLocale;uri=main.Source.Resource.Source.ResourceUri;
        sha=main.Source.Resource.Source.DecodedSha256;PreparedWords=new ReadOnlyDictionary<int,uint>(words);
        selectedUri=selected.FirstPackage.Objects.Source.ResourceUri;selectedSha=selected.FirstPackage.Objects.Source.DecodedSha256;
        selectedCamera=selected.FirstPackage.Camera.ObjectId;selectedIndex=selected.FirstIndex;
        QualityOption18=quality;MainMode288=mode;AppFlags=appFlags;PriorCloudy430Bits=priorCloudy;
        SelectedCloudy430Bits=selectedCloudy;SourceEvidence=evidence;}
    public long FrameNumber{get;}
    public int QualityOption18{get;}
    public int MainMode288{get;}
    public uint AppFlags{get;}
    public uint PriorCloudy430Bits{get;}
    public uint SelectedCloudy430Bits{get;}
    public IReadOnlyDictionary<int,uint> PreparedWords{get;}
    public string SourceEvidence{get;}
    public bool TryReadPreparedWord(int offset,out uint word)=>PreparedWords.TryGetValue(offset,out word);
    public void ValidateSameSelection(OriginalMapSeasonCameraSelection selected)
    {
        selected.Validate();
        if(selected.SecondPackage is not null||selected.FirstIndex!=selectedIndex||
            selected.FirstPackage.Camera.ObjectId!=selectedCamera||selected.FirstPackage.Objects.Source.ResourceUri!=selectedUri||
            selected.FirstPackage.Objects.Source.DecodedSha256!=selectedSha)
            throw new InvalidDataException("Main post-selection writer belongs to another seasonal package.");
    }
    public void ValidateSameMain(OriginalMapCameraConstructionPacket camera)
    {
        if(camera.FrameNumber!=FrameNumber||camera.ExecutableLocale!=locale||camera.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId||
            camera.Source.Resource.Source.ResourceUri!=uri||camera.Source.Resource.Source.DecodedSha256!=sha)
            throw new InvalidDataException("Main post-selection writer belongs to another frame/source.");
    }
    public byte[] ApplyToPrefix(ReadOnlySpan<byte> prefix)
    {
        if(prefix.Length!=OriginalMapCameraConstruction.PrefixByteLength)throw new InvalidDataException("Main post-selection requires a complete prepared Camera prefix.");
        byte[] result=prefix.ToArray();
        foreach(var pair in PreparedWords)if(pair.Key<result.Length)
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(pair.Key,4),pair.Value);
        return result;
    }
}

public static class OriginalMapMainCameraPostSelectionSource
{
    public static LoadedOriginalMapMainCameraPostSelectionSource CreateDefaults(string locale) => new(locale);
    public static LoadedOriginalMapMainCameraPostSelectionSource Load(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}
