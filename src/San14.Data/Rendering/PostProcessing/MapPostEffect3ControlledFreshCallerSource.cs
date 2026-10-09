using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual consumed fresh Main caller fields. Resources are
/// admitted only through this frame's typed seasonal transfer and source-null
/// external971/972 branches. Unknown nonnull resources remain unsupported.</summary>
public sealed class MapPostEffect3ControlledFreshCaller
{
    private readonly MapCameraConstructionPacket camera;
    internal MapPostEffect3ControlledFreshCaller(MapCameraConstructionPacket camera,int gather,
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
    public void ValidateSameCamera(MapCameraConstructionPacket actual)
    {
        camera.RequireEquivalentCurrentPostEffectScalars(actual);
        if(actual.FrameNumber!=FrameNumber||!camera.Prefix.Span.SequenceEqual(actual.Prefix.Span))
            throw new InvalidDataException("Fresh controlled caller belongs to another actual Camera.");
    }
    public MapPostEffect3ControllerScalarSnapshot CapturePostApplyController(
        MapPostEffect3ApplyDerivedScalarPlan actualDerived,
        MapPostEffect3ControlledOpticsPreparation actualOptics)
    {
        ArgumentNullException.ThrowIfNull(actualDerived);ArgumentNullException.ThrowIfNull(actualOptics);
        string owner=actualDerived.Dimensions.OwnerIdentity;
        if(actualDerived.FrameNumber!=FrameNumber||actualOptics.FrameNumber!=FrameNumber||actualOptics.OwnerIdentity!=owner||
            actualDerived.ExecutableLocale!=camera.ExecutableLocale||actualOptics.Locale!=camera.ExecutableLocale||!actualOptics.IsCompleteCpuOpticsPreparation||
            actualOptics.NativePreparationReturnAl!=1||actualOptics.DirtyAfAfterPreparation!=0)
            throw new InvalidDataException("Current controller snapshot needs this actual fresh Camera/derived/complete  preparation");
        return new(FrameNumber,owner,StopBladeCount6C,ChangeFStopA1,ExternalStarA0,Diaphragm9C,GatherSizeTypeC0,
            actualDerived.SpriteDofA9?(byte)1:(byte)0,BitConverter.SingleToUInt32Bits(actualDerived.BloomAntiFlickerDc),
            actualDerived.BloomAntiFlickerAa?(byte)1:(byte)0,SourceEvidence+";"+actualOptics.SourceEvidence+
            "-A1=1;-Camera1348;-Camera1320.bit3"+
            "-externalStar-null;-does-not-clear-A1;actual-A4/A9/DC/AA-current-writers");
    }
}

/// <summary>A new owned native configuration explicitly initialized from
/// source7/2. It retains its own current revision; it never represents the
/// original process's mutable globals or an immutable user preference.</summary>
public sealed class MapPostEffect3ControlledOwnedGraphicsConfiguration
{
    internal MapPostEffect3ControlledOwnedGraphicsConfiguration(string owner,int quality,int convention,string evidence)
        =>(OwnerIdentity,QualityLevel,ProjectionConvention,SourceEvidence)=(owner,quality,convention,evidence);
    public string OwnerIdentity{get;}
    public int QualityLevel{get;private set;}
    public int ProjectionConvention{get;private set;}
    public long Revision{get;private set;}
    /// <summary>This CPU port explicitly dispatches its owned math through
    /// the admitted original SSE kernels; it performs no native CPU dispatch observation.</summary>
    public NativeLogDispatch LogDispatch=>NativeLogDispatch.Sse;
    public string SourceEvidence{get;private set;}
    public void RecordActualOwnedConfigurationWriter(int quality,int projectionConvention,string actualWriterEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWriterEvidence);
        QualityLevel=quality;ProjectionConvention=projectionConvention;Revision=checked(Revision+1);
        SourceEvidence=actualWriterEvidence+";actual-owned-native-configuration-revision="+Revision;
    }
}

public sealed class LoadedMapPostEffect3ControlledFreshCallerSource
{
    internal LoadedMapPostEffect3ControlledFreshCallerSource(LoadedDeferredSamplerNativeSource native,int quality,int convention,string evidence)
        =>(Native,sourceQuality,sourceConvention,SourceEvidence)=(native,quality,convention,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    private readonly int sourceQuality,sourceConvention;
    public string SourceEvidence{get;}
    public MapPostEffect3ControlledOwnedGraphicsConfiguration CreateFreshOwnedConfiguration(string actualOwner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualOwner);
        return new(actualOwner,sourceQuality,sourceConvention,SourceEvidence+
            ";actual-new-owned-configuration-Quality7/Convention2-written-from-source;owned-port-SSE-math;not-original-current-state");
    }
    public MapPostEffect3ControlledFreshCaller CaptureFreshCurrent(
        MapCameraConstructionPacket actualCamera,MapSeasonPreparedLutTransfer actualTransfer,
        MapPostEffect3ApplyFreshLutLookups actualLookups)
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
        // /CCE key0 writes literal-null caches2ED8/2EE0; /A1
        // transports them into prepared1848/1850. Both current source cameras
        // are checked, so no unknown resource selection becomes absence.
        if(Main(971,0xE0C70CE1,5)!=0||Main(972,0xFD3DD206,5)!=0||
            Season(971,0xE0C70CE1,5)!=0||Season(972,0xFD3DD206,5)!=0)
            throw new NotSupportedException("Current external diaphragm/star source keys need their actual owned resource lookups.");
        uint mask=Main(1021,0x740E879D,5),fade=Main(1020,0xDB86C1F7,8),lumi=Main(1022,0x14515444,8);
        uint selectedMask=Season(1021,0x740E879D,5);
        // is unsigned>0. A selected mask0 retains all three
        // Main values; a nonzero mask copies this exact selected package.
        if(selectedMask!=0){mask=selectedMask;fade=Season(1020,0xDB86C1F7,8);lumi=Season(1022,0x14515444,8);}
        bool locked=(BitConverter.UInt32BitsToSingle(fade)>0||BitConverter.UInt32BitsToSingle(lumi)!=1)&&(mask&0x40000000)!=0;
        var tail=MapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(camera,"actual-controlled-caller-fields");
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
        LoadedObjects actualSelectedTemplate,MapFreshCommonResourceSnapshot actualCurrentCommon)
        => AdvanceOwnedFrameSyncTime(actualPrior3348,actualRendererDelta,
            MapFreshCommonResources.ReadSourceTemplate(actualSelectedTemplate),actualCurrentCommon);

    /// <summary>Normal requests borrow the already imported selected template.
    /// Current Common ownership/fields remain current inputs; no DOK is read.</summary>
    public float AdvanceOwnedFrameSyncTime(float actualPrior3348,float actualRendererDelta,
        LoadedMapFreshCommonSource actualSelectedTemplate,MapFreshCommonResourceSnapshot actualCurrentCommon)
    {
        ArgumentNullException.ThrowIfNull(actualSelectedTemplate);ArgumentNullException.ThrowIfNull(actualCurrentCommon);
        var source=actualSelectedTemplate;
        if(actualCurrentCommon.Scope!=MapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication||
            actualCurrentCommon.CommonViewFlags8!=source.Flags8||actualCurrentCommon.Resource240Flags0C!=source.Flags0C||
            !float.IsFinite(actualPrior3348)||!float.IsFinite(actualRendererDelta))
            throw new InvalidDataException("Current FrameSync time requires this retained source Common and finite actual clock.");
        return MapCommonFrameClockSource.PrepareFrameSyncTime(actualPrior3348,actualRendererDelta,source.Flags10).CurrentTime3348;
    }
}

public static class MapPostEffect3ControlledFreshCallerSource
{
    public static LoadedMapPostEffect3ControlledFreshCallerSource Load(GameResourceRepository repository,string locale="TC")
        =>RenderSamplerSourceAdapters.ReadControlledFreshCallerSource(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3ControlledFreshCallerSource ReadControlledFreshCallerSource(LoadedDeferredSamplerNativeSource native)
    {
        const int defaultQuality=7,defaultProjectionConvention=2;
        return new(native,defaultQuality,defaultProjectionConvention,
            RenderRuleIdentity.For("controlled-fresh-configuration",native.Locale)+
            ";fresh-owned-configuration;quality7;projection-convention2;current-writers-retain-normal-revisions");
    }
}
