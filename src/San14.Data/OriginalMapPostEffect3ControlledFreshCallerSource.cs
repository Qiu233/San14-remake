using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual consumed fresh Main caller fields. Resources are
/// admitted only through this frame's typed seasonal transfer and source-null
/// external971/972 branches. Unknown nonnull resources remain unsupported.</summary>
public sealed class OriginalMapPostEffect3ControlledFreshCaller
{
    private readonly OriginalMapCameraConstructionPacket camera;
    internal OriginalMapPostEffect3ControlledFreshCaller(OriginalMapCameraConstructionPacket camera,int gather,
        bool locked,int blades,int diaphragm,string evidence)
    {this.camera=camera.CopyAcceptedSnapshot();GatherSizeTypeC0=gather;AdaptationLock=locked;StopBladeCount6C=blades;
        Diaphragm9C=diaphragm;SourceEvidence=evidence;}
    public long FrameNumber=>camera.FrameNumber;
    public int GatherSizeTypeC0{get;}
    public bool AdaptationLock{get;}
    public float LdrIntensityXmm9=>0;
    public int StopBladeCount6C{get;}
    public int Diaphragm9C{get;}
    public byte ExternalStarA0=>0;
    public byte ChangeFStopA1=>1;
    public byte FreshControllerArgument3=>1;
    public byte FreshApplyArgument5=>0;
    public string SourceEvidence{get;}
    public void ValidateSameCamera(OriginalMapCameraConstructionPacket actual)
    {
        camera.RequireEquivalentCurrentPostEffectScalars(actual);
        if(actual.FrameNumber!=FrameNumber||!camera.Prefix.Span.SequenceEqual(actual.Prefix.Span))
            throw new InvalidDataException("Fresh controlled caller belongs to another actual Camera.");
    }
    public OriginalMapPostEffect3ControllerScalarSnapshot CapturePostApplyController(
        OriginalMapPostEffect3ApplyDerivedScalarPlan actualDerived,
        OriginalMapPostEffect3ControlledOpticsPreparation actualOptics)
    {
        ArgumentNullException.ThrowIfNull(actualDerived);ArgumentNullException.ThrowIfNull(actualOptics);
        string owner=actualDerived.Dimensions.OwnerIdentity;
        if(actualDerived.FrameNumber!=FrameNumber||actualOptics.FrameNumber!=FrameNumber||actualOptics.OwnerIdentity!=owner||
            actualDerived.ExecutableLocale!=camera.ExecutableLocale||actualOptics.Locale!=camera.ExecutableLocale||!actualOptics.IsCompleteCpuOpticsPreparation||
            actualOptics.NativePreparationReturnAl!=1||actualOptics.DirtyAfAfterPreparation!=0)
            throw new InvalidDataException("Current controller snapshot needs this actual fresh Camera/derived/complete E1F990 preparation.");
        return new(FrameNumber,owner,StopBladeCount6C,ChangeFStopA1,ExternalStarA0,Diaphragm9C,GatherSizeTypeC0,
            actualDerived.SpriteDofA9?(byte)1:(byte)0,BitConverter.SingleToUInt32Bits(actualDerived.BloomAntiFlickerDc),
            actualDerived.BloomAntiFlickerAa?(byte)1:(byte)0,SourceEvidence+";"+actualOptics.SourceEvidence+
            ";E243F0-A1=1;EB7559-E252F0-Camera1348;EB756A-E25300-Camera1320.bit3;"+
            "EB75A5-E25CF0-externalStar-null;E1F990-does-not-clear-A1;actual-A4/A9/DC/AA-current-writers");
    }
}

/// <summary>A new owned native configuration explicitly initialized from
/// source7/2. It retains its own current revision; it never represents the
/// original process's mutable globals or an immutable user preference.</summary>
public sealed class OriginalMapPostEffect3ControlledOwnedGraphicsConfiguration
{
    internal OriginalMapPostEffect3ControlledOwnedGraphicsConfiguration(string owner,int quality,int convention,string evidence)
        =>(OwnerIdentity,QualityLevel,ProjectionConvention,SourceEvidence)=(owner,quality,convention,evidence);
    public string OwnerIdentity{get;}
    public int QualityLevel{get;private set;}
    public int ProjectionConvention{get;private set;}
    public long Revision{get;private set;}
    /// <summary>This CPU port explicitly dispatches its owned math through
    /// the admitted original SSE kernels; it performs no native CPU dispatch observation.</summary>
    public OriginalNativeLogDispatch LogDispatch=>OriginalNativeLogDispatch.Sse;
    public string SourceEvidence{get;private set;}
    public void RecordActualOwnedConfigurationWriter(int quality,int projectionConvention,string actualWriterEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWriterEvidence);
        QualityLevel=quality;ProjectionConvention=projectionConvention;Revision=checked(Revision+1);
        SourceEvidence=actualWriterEvidence+";actual-owned-native-configuration-revision="+Revision;
    }
}

public sealed class LoadedOriginalMapPostEffect3ControlledFreshCallerSource
{
    internal LoadedOriginalMapPostEffect3ControlledFreshCallerSource(LoadedOriginalDeferredSamplerNativeSource native,int quality,int convention,string evidence)
        =>(Native,sourceQuality,sourceConvention,SourceEvidence)=(native,quality,convention,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    private readonly int sourceQuality,sourceConvention;
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3ControlledOwnedGraphicsConfiguration CreateFreshOwnedConfiguration(string actualOwner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwner);
        return new(actualOwner,sourceQuality,sourceConvention,SourceEvidence+
            ";actual-new-owned-configuration-Quality7/Convention2-written-from-source;owned-port-SSE-math;not-original-current-state");
    }
    public OriginalMapPostEffect3ControlledFreshCaller CaptureFreshCurrent(
        OriginalMapCameraConstructionPacket actualCamera,OriginalMapSeasonPreparedLutTransfer actualTransfer,
        OriginalMapPostEffect3ApplyFreshLutLookups actualLookups)
    {
        ArgumentNullException.ThrowIfNull(actualCamera);ArgumentNullException.ThrowIfNull(actualTransfer);
        ArgumentNullException.ThrowIfNull(actualLookups);actualTransfer.ValidateSameCamera(actualCamera);
        actualLookups.ValidateSameCamera(actualCamera);
        if(actualCamera.ExecutableLocale!=Native.Locale||!actualLookups.HdrPrimary1828Present||actualLookups.LdrPrimary1830Present||actualLookups.LdrSecondary1840Present||
            actualCamera.LdrLutKey933!=0)
            throw new NotSupportedException("Fresh controlled caller source currently admits actual HDR selected and both actual LDR slots absent.");
        var camera=actualCamera.CopyAcceptedSnapshot();
        var selected=actualTransfer.MainInputs.Source.Selection;selected.Validate();
        var package=selected.FirstPackage;
        if(selected.SecondPackage is not null)throw new NotSupportedException("Controlled caller fields require the actual direct seasonal package branch.");
        var canonical=package.Objects.Document.Objects.Single(o=>o.ObjectId==package.Camera.ObjectId);
        uint Main(int ordinal,uint hash,uint type)=>Word(camera.Source.Camera,ordinal,hash,type);
        uint Season(int ordinal,uint hash,uint type)=>Word(canonical,ordinal,hash,type);
        // C57C84/CCE key0 writes literal-null caches2ED8/2EE0; CAB28C/A1
        // transports them into prepared1848/1850. Both current source cameras
        // are checked, so no unknown resource selection becomes absence.
        if(Main(971,0xE0C70CE1,5)!=0||Main(972,0xFD3DD206,5)!=0||
            Season(971,0xE0C70CE1,5)!=0||Season(972,0xFD3DD206,5)!=0)
            throw new NotSupportedException("Current external diaphragm/star source keys need their actual owned resource lookups.");
        uint mask=Main(1021,0x740E879D,5),fade=Main(1020,0xDB86C1F7,8),lumi=Main(1022,0x14515444,8);
        uint selectedMask=Season(1021,0x740E879D,5);
        // C38BAB/C38BB2 is unsigned>0. A selected mask0 retains all three
        // Main values; a nonzero mask copies this exact selected package.
        if(selectedMask!=0){mask=selectedMask;fade=Season(1020,0xDB86C1F7,8);lumi=Season(1022,0x14515444,8);}
        bool locked=(BitConverter.UInt32BitsToSingle(fade)>0||BitConverter.UInt32BitsToSingle(lumi)!=1)&&(mask&0x40000000)!=0;
        var tail=OriginalMapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(camera,"actual-controlled-caller-fields");
        uint dof=tail.Word(0x1320);
        return new(camera,(int)(dof&7),locked,tail.Int(0x1348),(int)((dof>>3)&1),SourceEvidence+";"+
            actualTransfer.SourceEvidence+";"+actualLookups.SourceEvidence+
            ";actual-external971/972-key0-cache-transfer;LDR1830/1840-null->xmm9zero;"+
            "Camera1320&7-localC0;source-current-LightGroup13F0/13F4/13F8-adaptation-predicate");
    }
    private static uint Word(DokObject camera,int ordinal,uint hash,uint type)
    {
        var p=camera.Properties[ordinal];
        if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=type||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4)
            throw new InvalidDataException("Controlled caller property differs from its exact source binding.");
        return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
    }

    /// <summary>Apply the actual source FrameSync time writer to this owned
    /// current clock, then publish block254. The scalar input is the renderer's
    /// actual current delta. Pause is the actual selected template flags10.bit3.</summary>
    public float AdvanceOwnedFrameSyncTime(float actualPrior3348,float actualRendererDelta,
        LoadedObjects actualSelectedTemplate,OriginalMapFreshCommonResourceSnapshot actualCurrentCommon)
        => AdvanceOwnedFrameSyncTime(actualPrior3348,actualRendererDelta,
            OriginalMapFreshCommonResources.ReadSourceTemplate(actualSelectedTemplate),actualCurrentCommon);

    /// <summary>Normal requests borrow the already imported selected template.
    /// Current Common ownership/fields remain current inputs; no DOK is read.</summary>
    public float AdvanceOwnedFrameSyncTime(float actualPrior3348,float actualRendererDelta,
        LoadedOriginalMapFreshCommonSource actualSelectedTemplate,OriginalMapFreshCommonResourceSnapshot actualCurrentCommon)
    {
        ArgumentNullException.ThrowIfNull(actualSelectedTemplate);ArgumentNullException.ThrowIfNull(actualCurrentCommon);
        var source=actualSelectedTemplate;
        if(actualCurrentCommon.Scope!=OriginalMapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication||
            actualCurrentCommon.CommonViewFlags8!=source.Flags8||actualCurrentCommon.Resource240Flags0C!=source.Flags0C||
            !float.IsFinite(actualPrior3348)||!float.IsFinite(actualRendererDelta))
            throw new InvalidDataException("Current FrameSync time requires this retained source Common and finite actual clock.");
        return OriginalMapCommonFrameClockSource.PrepareFrameSyncTime(actualPrior3348,actualRendererDelta,source.Flags10).CurrentTime3348;
    }
}

public static class OriginalMapPostEffect3ControlledFreshCallerSource
{
    public static LoadedOriginalMapPostEffect3ControlledFreshCallerSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadControlledFreshCallerSource(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3ControlledFreshCallerSource ReadControlledFreshCallerSource(LoadedOriginalDeferredSamplerNativeSource native)
    {
        const string sourceScope="OriginalMapPostEffect3ControlledFreshCallerSource.cs";
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"controlled caller executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"controlled caller decoded text");
        // The complete baseline hash also pins all pointer/property bindings.
        foreach(var (at,length,hash) in new (int,int,string)[]{
            (0xEB5F0A,31,"4B1CBE2E54AAF26449D9A805A6A67A7493CE74E1A920B48938EABAC6EBDA7FC9"),
            (0xEB6CDF,33,"2C6CBAB847E841437A0A38C7AE2966CCB10C0EABEBE29E3D4878735871FD2100"),
            (0xEB7110,35,"7E0DC36637F14A9D1EAD526A72F29EC798DD886212B3CF1B26B5D3402F5D8A3A"),
            (0xEB73D3,116,"2CAAAD8E88A11E54FF0D1A6FFA17FB5E4F9EA3F13717FEBEF3EC4ADC7E2BF0B0"),
            (0xC57C81,148,"2DEFDE7F3403E423A65B73215D9CCA98AD6AE4B6FC7348EE9CF79FF303CEB9F7"),
            (0xCAB28C,28,"B3E3554C4F618118206AFA6CD4112119EC897E326725A8C563E0F8FEE67D8525"),
            (0xEB7516,164,"0A91B963EC223F406B8E50A6C5585011E35F47DBA0D66586F06DE6A13F11B5D4"),
            (0xEB7614,116,"D9957F3F2899BF250F64C0F02C545CF1DC085758EF25B19949F99B9F0E8A3D33"),
            (0xC38BAB,45,"56E397F309882473E97DE15FA2E501218928EEAC0E43061B144DE2845AE75239"),
            (0xC43CA6,48,"922D9964E85FC2B4B7D1016884543E5E0CD587107D589BC84C92D8EB1EBF9B2B"),
            (0xE6BA99,138,"A1EB2264F865ED971E8490A2CC0408AE18EC40B1E440341B3C178964ECA99E74"),
            (0xD0176F,32,"6A1178DF08AEF9B686F5D1DD8FA2D448BEBACE70FECA0A660E65FE26998B1A1F"),
            (0xD01AE1,21,"516E936B719503A55CB74A7697777CE3DFF8A8C035DBB5E92569418C91408167"),
            (0xF40628,140,"BA6390EF11DABDA0C9EEB82ECA4E001A368FA0EF20759FF6E4D0FA179528BF6C"),
            (0xD12AB6,65,"1228E6E26E3694D02705736F5003D2C80F17DA385831141E108CB1335CF47502"),
            (0xD61BA6,89,"41AB48E41AD9C3041E478EB920A23DCD5881346196DC4156F36B9A05191D7765")})
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,sourceScope,at,length,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,length),actual.Sha,"controlled fresh caller window");
        }
        int qualityRva=OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,sourceScope,0x1A8C788);
        int conventionRva=OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,sourceScope,0x1A8BE70);
        int quality=BinaryPrimitives.ReadInt32LittleEndian(image.Raw(qualityRva,4));
        int convention=BinaryPrimitives.ReadInt32LittleEndian(image.Raw(conventionRva,4));
        if(quality!=7||convention!=2)throw new InvalidDataException("Original native graphics configuration initializers changed.");
        return new(native,quality,convention,native.ResourceUri+"@"+native.ExecutableSha256+
            ";pinned-actual-gather/LDR/external-resource/controller/adaptation/clock-writers;"+
            $"Quality{qualityRva:X8}-source7;ProjectionConvention{conventionRva:X8}-source2;new-owned-configuration-only");
    }
}
