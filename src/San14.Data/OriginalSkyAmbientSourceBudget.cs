using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>The actual current owner clock at table+254. This clock is distinct
/// from World third-date, Skylight dateTime and wall-clock time; its writer must
/// supply the same render-frame value explicitly.</summary>
public sealed record OriginalSkyAmbientCurrentClock254(long FrameNumber,
    string ClockOwnerIdentity, float Value, string CurrentWriterProvenance);

public sealed class OriginalSkyAmbientSourceBudgetFrame
{
    internal OriginalSkyAmbientSourceBudgetFrame(OriginalSkyAmbientSourceBudgetOwner owner,
        OriginalSkySeasonMainCameraRgbFrame camera, OriginalSkyAmbientCurrentClock254 clock,
        uint units, float fraction, float interval)
    { Owner=owner; Camera=camera; Clock=clock; WorkUnits=units; Fraction14=fraction; Interval=interval; }
    internal OriginalSkyAmbientSourceBudgetOwner Owner { get; }
    internal OriginalSkySeasonMainCameraRgbFrame Camera { get; }
    public OriginalSkyAmbientCurrentClock254 Clock { get; }
    public long FrameNumber=>Clock.FrameNumber;
    public uint WorkUnits { get; }
    public float Fraction14 { get; }
    public float Interval { get; }
    public string SourceProvenance=>Owner.SourceProvenance+";"+Clock.CurrentWriterProvenance+
        $";frame={FrameNumber};EA3-source-trigger0-budget={WorkUnits}";
}

/// <summary>Exact finite EA3 source mode0 budget owner. E6C040 initializes
/// fractional work14 and prior clock1C to zero. Work resource readiness,
/// completed work count, generated cube selection and Fog76 remain separate.
/// This owner never turns missing inputs into a disabled Sky consumer.</summary>
public sealed class OriginalSkyAmbientSourceBudgetOwner
{
    private readonly string clockOwner;
    private float fraction, priorClock;
    private long lastFrame;
    private OriginalSkyAmbientSourceBudgetFrame? pending;
    private OriginalSkyAmbientSourceBudgetOwner(string nodeOwner, string clockOwner,
        LoadedOriginalMapCameraConstructionSource main)
    {
        this.clockOwner=clockOwner;
        SourceProvenance=$"owned-node5={nodeOwner};clock254={clockOwner};"+
            $"source={main.Resource.Source.ResourceUri}@{main.Resource.Source.DecodedSha256}:38D04576;"+
            "TC:E6C05C/E6C060:fraction14=0/priorClock1C=0;"+
            "CPU128->Camera280:AA84CF1C=1;CPU12C->Camera284:71003548=1;"+
            "CPU15C->Camera3A8:2F288D0C=0;EA3CD7/EA3D01-EA3D91/EA3E6C";
    }
    public string SourceProvenance { get; }
    public float CurrentFraction14 => fraction;
    public float PriorClock1C => priorClock;

    public static OriginalSkyAmbientSourceBudgetOwner CreateFreshSourceMain(
        LoadedOriginalMapCameraConstructionSource main, string nodeOwnerIdentity,
        string actualClockOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(main);
        if(string.IsNullOrWhiteSpace(nodeOwnerIdentity)||string.IsNullOrWhiteSpace(actualClockOwnerIdentity)||
            main.Camera.ObjectId!=0x38D04576)
            throw new InvalidDataException("Sky budget requires the actual source Main Camera and explicit fresh node/clock owners.");
        var canonical=OriginalMapCameraConstructionSource.ReadPerspective(main.Resource,main.Camera.ObjectId);
        if(canonical.CpuWord(0x128)!=0x3F800000||canonical.CpuWord(0x12C)!=0x3F800000||canonical.CpuWord(0x15C)!=0)
            throw new InvalidDataException("Sky source budget requires the exact mode0/frequency1/interpolation1 Main declaration.");
        return new(nodeOwnerIdentity,actualClockOwnerIdentity,canonical);
    }

    /// <summary>EA3 changes fraction14 before E46810. The caller must complete
    /// this exact frame after E46810 returns, including its false-return case;
    /// only then does EA3E6C write priorClock1C. Budget is an input work request,
    /// not evidence that WORK initialized or that any face was drawn.</summary>
    public OriginalSkyAmbientSourceBudgetFrame PrepareCurrentSourceMode0(
        OriginalSkySeasonMainCameraRgbFrame camera, OriginalSkyAmbientCurrentClock254 clock)
    {
        ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(clock);
        if(pending is not null)throw new InvalidOperationException("Complete the previous native Sky work call before preparing another clock frame.");
        RequireAcceptedRgb(camera);
        if(camera.Inputs.MainFrame.Camera.Source.Camera.ObjectId!=0x38D04576||
            camera.Inputs.FrameNumber!=clock.FrameNumber||clock.FrameNumber<=lastFrame||
            clock.ClockOwnerIdentity!=clockOwner||string.IsNullOrWhiteSpace(clock.CurrentWriterProvenance)||
            !float.IsFinite(clock.Value))
            throw new InvalidDataException("Sky budget requires the same current Main frame and finite actual owner254 clock.");
        ReadOnlySpan<byte> prefix=camera.CurrentSkyCamera.PreparedCameraPrefix.Span;
        if(prefix.Length!=OriginalPreparedCameraLighting.PrefixByteLength||
            Word(prefix,0x280)!=0x3F800000||Word(prefix,0x284)!=0x3F800000||Word(prefix,0x3A8)!=0)
            throw new NotSupportedException("Edited/future Sky frequency, interpolation or trigger requires its actual source budget branch.");
        var result=CalculateSourceMode0(clock.Value,priorClock,fraction);
        fraction=result.Fraction;
        return pending=new(this,camera,clock,result.WorkUnits,fraction,result.Interval);
    }
    internal static void RequireAcceptedRgb(OriginalSkySeasonMainCameraRgbFrame camera)
    {
        if(camera.CurrentSkyCamera.FrameNumber!=camera.Inputs.FrameNumber||
            !camera.CurrentSkyCamera.PreparedCameraPrefix.Span.SequenceEqual(camera.SunMoonRgb.Prefix.Span))
            throw new InvalidDataException("Sky activation requires the same accepted present-table Main RGB/current-Camera generation.");
    }

    public void CompleteCurrentFrameAfterWorkCall(OriginalSkyAmbientSourceBudgetFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if(!ReferenceEquals(frame,pending)||!ReferenceEquals(frame.Owner,this))
            throw new InvalidDataException("Sky budget completion requires the exact pending frame from this node owner.");
        priorClock=frame.Clock.Value;lastFrame=frame.FrameNumber;pending=null;
    }

    /// <summary>Pure original finite mode0 branch, including the actual
    /// Common254 wrap to a value below this node's prior1C. The signed64
    /// CVTTSS2SI result supplies its low32 ESI to the unsigned cap43.
    /// Exposed for instruction fixtures; it confers no owner authority.</summary>
    public static (uint WorkUnits,float Fraction,float Interval) CalculateSourceMode0(
        float actualClock254,float nodePriorClock1C,float nodeFraction14)
    {
        if(!float.IsFinite(actualClock254)||!float.IsFinite(nodePriorClock1C)||
            !float.IsFinite(nodeFraction14)||nodeFraction14<0||nodeFraction14>=1)
            throw new InvalidDataException("Sky mode0 budget is outside its finite source clock/fraction domain.");
        float interval=D(1f,43f), units=D(S(actualClock254,nodePriorClock1C),interval);
        if(!float.IsFinite(units))
            throw new InvalidDataException("Sky budget units overflowed the current finite source domain.");
        float whole=MathF.Floor(units),nextFraction=A(S(units,whole),nodeFraction14);
        uint wholeUnits=unchecked((uint)Sse.X64.ConvertToInt64WithTruncation(Vector128.CreateScalar(whole)));
        if(nextFraction>=1f){wholeUnits=unchecked(wholeUnits+1);nextFraction=S(nextFraction,MathF.Floor(nextFraction));}
        return (Math.Min(wholeUnits,43u),nextFraction,interval);
    }
    private static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(at,4));
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

/// <summary>EA3's actual enable argument, independent of WORK readiness/budget.
/// A null Aux is known absence only when CommonResource240Present is false;
/// a present partial source snapshot cannot invent byte12.</summary>
public sealed class OriginalSkyAmbientSourceEnableInputs
{
    private OriginalSkyAmbientSourceEnableInputs(long frame,string view,bool commonPresent,byte? byte12,
        bool enable,string provenance)
    {FrameNumber=frame;ViewOwnerIdentity=view;CommonResource240Present=commonPresent;
        ActualAuxiliaryByte12=byte12;AllowsWorkInitialization=enable;SourceProvenance=provenance;}
    public long FrameNumber { get; }
    public string ViewOwnerIdentity { get; }
    public bool CommonResource240Present { get; }
    public byte? ActualAuxiliaryByte12 { get; }
    public bool MainSourcePresentTable1680=>true;
    public bool AllowsWorkInitialization { get; }
    public string SourceProvenance { get; }
    public static OriginalSkyAmbientSourceEnableInputs PrepareCurrentSourcePresentTable(
        OriginalSkySeasonMainCameraRgbFrame camera,OriginalMapFreshCommonResourceSnapshot common,
        string currentSelectedViewIdentity)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(common);
        OriginalSkyAmbientSourceBudgetOwner.RequireAcceptedRgb(camera);
        common.ValidateFrame(camera.Inputs.FrameNumber,common.CommonTableOwnerIdentity,currentSelectedViewIdentity);
        if(camera.Inputs.MainFrame.Camera.Source.Camera.ObjectId!=0x38D04576||
            !camera.Inputs.Source.UsesSourceCreatedEmptyAxes)
            throw new NotSupportedException("Sky source enable requires the owned direct Main source present-table producer.");
        byte? value=common.CurrentAuxiliaryByte12;
        if(common.CommonViewResource240Present&&value is null)
            throw new InvalidDataException("Present current Common Aux requires its actual byte12; partial fresh fields cannot disable Sky.");
        if(!common.CommonViewResource240Present&&value is not null)
            throw new InvalidDataException("Sky Common absence and current Aux publication disagree.");
        bool enable=value is null||(value.Value&1)==0;
        return new(camera.Inputs.FrameNumber,currentSelectedViewIdentity,common.CommonViewResource240Present,
            value,enable,common.ProducerProvenance+";"+camera.SourceProvenance+
            ";TC:EA3DAB-EA3DFD;actual-Common-byte12-and-owned-source-Main1680");
    }
}
