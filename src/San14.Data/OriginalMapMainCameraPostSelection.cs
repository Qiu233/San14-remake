using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Reflection.PortableExecutable;

namespace San14.Data;

/// <summary>The complete Main17BD0 direct-store family between V468 and V418.
/// Helpers and later weather writers have independent ownership.</summary>
public sealed class LoadedOriginalMapMainCameraPostSelectionSource
{
    private readonly IReadOnlyDictionary<int,uint> constants;
    internal LoadedOriginalMapMainCameraPostSelectionSource(string locale,string sha,
        Dictionary<int,uint> constants,string evidence)
    {Locale=locale;ExecutableSha256=sha;this.constants=new ReadOnlyDictionary<int,uint>(constants);SourceEvidence=evidence;}
    public string Locale{get;}
    public string ExecutableSha256{get;}
    public string SourceEvidence{get;}
    internal uint Constant(int tcRva)=>constants[tcRva];
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
        uint C(int tcRva)=>Constant(tcRva);
        W(0x1C4,0); //CPU74 LightAdaptedScale, not Controller.AutoDateTimeStepType.
        if(m>=3){W(0x1C8,C(0x1343B70));cloudy=0;}
        if(!low)cloudy=0;
        W(0x1D0,cloudy);
        W(0xAC0,4);
        W(0xB3C,low?C(0x1343CC4):C(0x1343C88));
        W(0xB08,low?C(0x1343BA8):C(0x1343B98));
        W(0x414,low?0u:1u);W(0x418,low?0u:2u);
        W(0x840,low?1u:0u);W(0x84C,low?3u:0u);
        W(0x8E0,low?1u:0u);W(0x868,low?3u:0u);
        W(0x180,low?C(0x1343B70):0u);W(0x188,low?C(0x1343B98):0u);
        W(0x1320,low?1u:0u);W(0x1324,low?C(0x1343B70):C(0x1343CDC));
        W(0x1344,low?16u:0u);W(0x674,low?2u:1u);W(0x7A0,low?1u:2u);
        W(0x101C,low||(flags2&1)!=0?C(0x1343B98):0u);
        float currentCloudy=BitConverter.UInt32BitsToSingle(cloudy);
        W(0x210,previousCloudy430==currentCloudy?0u:C(0x1343B5C));
        if((flags0&0x200)!=0)W(0x151C,0x8BAC7B6D);
        if((flags0&0x20)!=0&&g<2)
        {
            bool distant=m>0;
            W(0xA00,8);W(0xA3C,distant?9u:11u);
            W(0x9F4,C(distant?0x1343C94:0x1343C78));
            W(0x9EC,C(distant?0x1343C9C:0x1343CAC));
            W(0x9F0,C(distant?0x1343C98:0x1343CA4));
            W(0xA08,C(distant?0x1343B58:0x1343B5C));
            W(0xA9C,C(distant?0x1343B58:0x1343B5C));W(0xA88,distant?7u:0u);
        }
        else W(0xA00,0);
        W(0xF74,m<3?C(0x1343BE0):0u);W(0xF60,m<3?C(0x1343C58):0u);W(0xF80,m<3?1u:0u);
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
            source.ExecutableSha256!=quality.ExecutableSha256||selected.Selection.SecondPackage is not null)
            throw new InvalidDataException("Main post-selection requires the same owned Main, setting and direct seasonal frame.");
        selected.Selection.Validate();
        uint cloudy=ReadCloudy(selected);
        int m=mode.Mode288,g=quality.CurrentQualityOption18;
        var words=new Dictionary<int,uint>(source.ProjectDirectWrites(g,m,flags0|(flags2<<16),
            BitConverter.SingleToUInt32Bits(previousCloudy430),cloudy));
        cloudy=words[0x1D0];
        float currentCloudy=BitConverter.UInt32BitsToSingle(cloudy);
        string evidence=source.SourceEvidence+";"+mode.SourceProvenance+";"+quality.SourceProvenance+
            $";owned-App-revision={++revision};flags0={flags0:X};flags2={flags2:X};prior430={BitConverter.SingleToUInt32Bits(previousCloudy430):X8};new430={cloudy:X8};App290-reset-minus1/no-positive-FOV-override";
        var result=new OriginalMapMainCameraPostSelectionPacket(main,selected.Selection,words,g,m,evidence);
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
        OriginalMapSeasonCameraSelection selected,Dictionary<int,uint> words,int quality,int mode,string evidence)
    {FrameNumber=main.FrameNumber;locale=main.ExecutableLocale;uri=main.Source.Resource.Source.ResourceUri;
        sha=main.Source.Resource.Source.DecodedSha256;PreparedWords=new ReadOnlyDictionary<int,uint>(words);
        selectedUri=selected.FirstPackage.Objects.Source.ResourceUri;selectedSha=selected.FirstPackage.Objects.Source.DecodedSha256;
        selectedCamera=selected.FirstPackage.Camera.ObjectId;selectedIndex=selected.FirstIndex;
        QualityOption18=quality;MainMode288=mode;SourceEvidence=evidence;}
    public long FrameNumber{get;}
    public int QualityOption18{get;}
    public int MainMode288{get;}
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
    public static LoadedOriginalMapMainCameraPostSelectionSource Load(OriginalGameRepository repository,string locale)
        =>OriginalDeferredSamplerSources.ReadMainPostSelectionSource(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapMainCameraPostSelectionSource ReadMainPostSelectionSource(LoadedOriginalDeferredSamplerNativeSource native)
    {
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Main post-selection executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);VerifyHash(text,native.DecodedTextSha256,"Main post-selection decoded text");
        string mainHash=native.Locale switch{"TC"=>"1FB3CB3A5C58501FE8DE508F8BD554143942F6BC9D09ADB4A2793F4EA1EBCFEE",
            "SC"=>"F4532EF6C80FC9B8E5224487AC41A25B05DEECB8613384570E25607FB81823B3",_=>throw new NotSupportedException("Main post-selection requires SC/TC.")};
        VerifyHash(text.AsSpan(0x17BD0-start,2194),mainHash,"complete Main17BD0 direct writer owner");
        int delta=native.Locale=="SC"?0x1000:0;
        (int Rva,uint Bits)[] constants=[(0x1343B98,0x3F800000),(0x1343CDC,0xBF800000),(0x1343B70,0x3DCCCCCD),
            (0x1343CC4,0x48435000),(0x1343C88,0x459C4000),(0x1343BA8,0x3FAAA993),
            (0x1343C94,0x46C35000),(0x1343C78,0x44FA0000),(0x1343C9C,0x46FA0000),(0x1343CAC,0x47435000),
            (0x1343C98,0x46E29000),(0x1343CA4,0x471C4000),(0x1343B5C,0x3A83126F),(0x1343B58,0x38D1B717),
            (0x1343BE0,0x40000000),(0x1343C58,0x43960000)];
        Dictionary<int,uint> values=[];
        foreach(var c in constants)
        {
            uint actual=BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(c.Rva+delta,4));
            if(actual!=c.Bits)throw new InvalidDataException("Main source constant changed.");values.Add(c.Rva,actual);
        }
        bool tc=native.Locale=="TC";
        VerifyHash(text.AsSpan((tc?0x9A00:0x99F0)-start,490),tc?
            "E6C7CB19A7816B07A99934F7230CD3ED6EB7554555CA4FFD36DB870E7B9801F2":
            "7C8DC11B6E44E051AF9196C1D84BA8BF167123290EE3A14CCD7884B4DDFE9D8F","complete fresh App constructor");
        VerifyHash(text.AsSpan(0x10CF0-start,265),tc?
            "894A90660C581C01D85F3D8A71E639E18B662B87C6804AF0D15B4059B96C6952":
            "D94A8CBBBCE0725CB8DD4C5095C1D7B43221C1CBF0773D7B391B3C4D1BF56A0D","complete fresh App reset");
        VerifyHash(text.AsSpan(0x11040-start,129),tc?
            "2437AEE130B52D6CFF6C219D87B72D6FBE3F13E1934754480D306521F0930944":
            "9BFDC3C3D8F3BFF4916D1A5A8258E63F192B7392620EBD28B1371918418EA94B","App singleton static owner");
        using(var pe=new PEReader(new MemoryStream(exe,false)))
        {
            var data=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".data");
            int app=tc?0x1B63610:0x1B64670;
            if(data.VirtualAddress!=(tc?0x1A21000:0x1A22000)||data.SizeOfRawData!=0x139600||
                data.VirtualSize!=(tc?0xE03F6C:0xE0412C)||app<data.VirtualAddress+data.SizeOfRawData||
                app+0x434>data.VirtualAddress+data.VirtualSize)
                throw new InvalidDataException("Fresh static App is outside the original image zero-fill tail.");
        }
        return new(native.Locale,native.ExecutableSha256,values,
            $"{native.Locale}@{native.ExecutableSha256};complete-Main17BD0[{mainHash}];V468-return-to-V418/direct37-store-family;fresh-App009A00/10CF0");
    }
}
