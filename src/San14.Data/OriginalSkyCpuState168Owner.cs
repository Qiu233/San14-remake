using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Actual successful source-call returns, supplied by the work owner.
/// These counts confer no GPU readiness or resource lifetime authority.</summary>
public sealed record OriginalSkyCpuWorkAcknowledgement(uint BaseFacesReturned,
    bool BasePreparationCalled, uint FilterFacesReturned);

public sealed class OriginalSkyCpuState168Call
{
    internal OriginalSkyCpuState168Call(OriginalSkyCpuState168Owner owner, long frame, uint state,
        uint budget, bool enabled, float elapsed, float span, OriginalSkyAmbientCurrentClock254 clock, string provenance)
    { Owner=owner;FrameNumber=frame;EntryState168=state;RequestedWorkUnits=budget;Enabled=enabled;
      Elapsed20AtEntry=elapsed;InterpolationSpan=span;Clock=clock;SourceProvenance=provenance; }
    internal OriginalSkyCpuState168Owner Owner { get; }
    internal bool InitializationRecorded { get; set; }
    internal bool WorkReady { get; set; }
    public long FrameNumber { get; }
    public uint EntryState168 { get; }
    public uint GenerationIndex => EntryState168&3;
    public uint FirstFlat => (EntryState168>>2)&63;
    public uint PreparationSubphase => (EntryState168>>13)&3;
    public uint RequestedWorkUnits { get; }
    public bool Enabled { get; }
    public float Elapsed20AtEntry { get; }
    public float InterpolationSpan { get; }
    public OriginalSkyAmbientCurrentClock254 Clock { get; }
    public string SourceProvenance { get; }
}

public sealed class OriginalSkyCpuState168CompletionPlan
{
    internal OriginalSkyCpuState168CompletionPlan(OriginalSkyCpuState168Call call,
        uint state, float elapsed, uint units, bool completed, uint? completedIndex, bool blend, float fraction)
    { Call=call;State168=state;ElapsedAfterWork=elapsed;WorkUnitsReturned=units;
      GenerationCompleted=completed;CompletedGenerationIndex=completedIndex;RequiresBlend=blend;BlendFraction=fraction; }
    public OriginalSkyCpuState168Call Call { get; }
    public uint State168 { get; }
    public float ElapsedAfterWork { get; }
    public uint WorkUnitsReturned { get; }
    public bool GenerationCompleted { get; }
    public uint? CompletedGenerationIndex { get; }
    public bool RequiresBlend { get; }
    public float BlendFraction { get; }
    public uint NextGenerationIndex => State168&3;
    public uint SelectedSpecularIndex => (State168>>8)&3;
    public (uint InputA,uint InputB,uint Destination) BlendIndices => OriginalSkySceneSchedule.BlendIndices(NextGenerationIndex);
}

/// <summary>Private current writer receipt for the Q80/F103 consumer.
/// The selected original wrapper can be ungenerated: this is CPU selection,
/// not permission to borrow a completed GPU generation under another index.</summary>
public sealed class OriginalSkyCpuState168Snapshot
{
    private readonly OriginalSkyCpuState168Owner owner;
    private readonly long revision;
    internal OriginalSkyCpuState168Snapshot(OriginalSkyCpuState168Owner owner,long revision,long frame,uint state,string provenance)
    {this.owner=owner;this.revision=revision;FrameNumber=frame;State168=state;SourceProvenance=provenance;}
    public long FrameNumber { get; }
    public uint State168 { get; }
    public uint GenerationIndex => State168&3;
    public uint SelectedSpecularIndex => (State168>>8)&3;
    public bool RetainsEarlierEnvmap => (State168&0x40000000)!=0;
    public string OwnedSkyIdentity => owner.OwnedSkyIdentity;
    public string SourceProvenance { get; }
    public void ValidateCurrent(string actualSkyIdentity,long consumerFrame)
        =>owner.ValidateSnapshot(this,revision,actualSkyIdentity,consumerFrame);
}

/// <summary>E38440 constructor, E46810 work/selection and EA3 caller elapsed20
/// lifecycle. Low work k and output bits8..9 are separate; source bit30 is an
/// actual enable branch, never a guessed backend generation modulo.</summary>
public sealed class OriginalSkyCpuState168Owner
{
    private uint state;
    private float elapsed,priorCallerClock;
    private bool workPresent;
    private long revision,lastFrame;
    private OriginalSkyCpuState168Call? pending;
    private OriginalSkyCpuState168CompletionPlan? plan;
    private readonly string clockOwner;
    private OriginalSkyCpuState168Owner(string sky,string clock,string provenance)
    {OwnedSkyIdentity=sky;clockOwner=clock;SourceProvenance=provenance;}
    public string OwnedSkyIdentity { get; }
    public string SourceProvenance { get; }
    public uint CurrentState168 => state;
    public float NodeElapsed20 => elapsed;
    public float NodePriorClock18 => priorCallerClock;
    public static OriginalSkyCpuState168Owner CreateFresh(OriginalGameRepository repository,string locale,
        string ownedSkyIdentity,string actualClockOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedSkyIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualClockOwnerIdentity);
        return new(ownedSkyIdentity,actualClockOwnerIdentity,
            OriginalDeferredSamplerSources.ReadSkyCpuState168NativeSource(repository.SourceDirectory,locale)+
            $";owned-Sky={ownedSkyIdentity};owned-node-clock254={actualClockOwnerIdentity};fresh-constructor168/16C/elapsed20=0");
    }
    public OriginalSkyCpuState168Call BeginCurrentSourceCall(OriginalSkyAmbientSourceBudgetFrame budget,
        OriginalSkyAmbientSourceEnableInputs enable)
    {
        ArgumentNullException.ThrowIfNull(budget);
        return Begin(budget.Camera,enable,budget.Clock,budget.WorkUnits,budget.SourceProvenance+";actual-mode0-source-budget");
    }
    /// <summary>An explicit backend batch may use the same original state
    /// writers, but its budget is not claimed to be the original clock budget.
    /// Clock254, current Camera, enable and returned work remain explicit.</summary>
    public OriginalSkyCpuState168Call BeginCurrentBackendBatch(OriginalMapSourceCreatedSeasonLightingFrame present,
        OriginalSkyAmbientSourceEnableInputs enable,OriginalSkyAmbientCurrentClock254 clock,
        uint explicitBackendWorkUnits,string backendBudgetProvenance)
    {
        ArgumentNullException.ThrowIfNull(present);ArgumentException.ThrowIfNullOrWhiteSpace(backendBudgetProvenance);
        return Begin(present.AcceptedSeasonCamera,enable,clock,explicitBackendWorkUnits,
            present.SourceProvenance+";explicit-backend-budget-not-native-budget="+backendBudgetProvenance);
    }
    private OriginalSkyCpuState168Call Begin(OriginalSkySeasonMainCameraRgbFrame camera,
        OriginalSkyAmbientSourceEnableInputs enable,OriginalSkyAmbientCurrentClock254 clock,uint budget,string provenance)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(enable);ArgumentNullException.ThrowIfNull(clock);
        if(pending is not null)throw new InvalidOperationException("Complete the current source Sky call before beginning another.");
        OriginalSkyAmbientSourceBudgetOwner.RequireAcceptedRgb(camera);
        ReadOnlySpan<byte> b=camera.CurrentSkyCamera.PreparedCameraPrefix.Span;
        if(clock.FrameNumber!=camera.Inputs.FrameNumber||enable.FrameNumber!=clock.FrameNumber||clock.FrameNumber<=lastFrame||
            clock.ClockOwnerIdentity!=clockOwner||string.IsNullOrWhiteSpace(clock.CurrentWriterProvenance)||
            !float.IsFinite(clock.Value)||budget>43||
            camera.Inputs.MainFrame.Camera.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId||
            Word(b,0x280)!=0x3F800000||Word(b,0x284)!=0x3F800000||Word(b,0x3A8)!=0)
            throw new InvalidDataException("Sky state lifecycle requires one current original source Main/enable/clock owner and finite source mode0 span1.");
        pending=new(this,clock.FrameNumber,state,budget,enable.AllowsWorkInitialization,elapsed,1,clock,
            SourceProvenance+";"+enable.SourceProvenance+";"+provenance);
        if(!pending.Enabled){state|=0x40000000;revision++;}
        return pending;
    }
    /// <summary>The real initializer result precedes the draws. Only success
    /// stores bit31 and the local bit30 clear. Already-ready WORK skips it.</summary>
    public void RecordWorkInitialization(OriginalSkyCpuState168Call call,bool actualAllocationPresent,bool initializerSucceeded)
    {
        Require(call);
        if(!call.Enabled||call.InitializationRecorded||(!actualAllocationPresent&&initializerSucceeded)||
            (workPresent&&!actualAllocationPresent)||
            ((state&0x80000000)!=0&&(!actualAllocationPresent||!initializerSucceeded)))
            throw new InvalidDataException("Sky WORK initialization result is inconsistent with the actual source branch.");
        workPresent=actualAllocationPresent;call.InitializationRecorded=true;
        call.WorkReady=actualAllocationPresent&&(initializerSucceeded||(state&0x80000000)!=0);
        if(call.WorkReady&&(state&0x80000000)==0){state=(state&~0x40000000u)|0x80000000;revision++;}
    }
    public OriginalSkyCpuState168CompletionPlan PrepareCompletion(OriginalSkyCpuState168Call call,
        OriginalSkyCpuWorkAcknowledgement actualReturns)
    {
        Require(call);ArgumentNullException.ThrowIfNull(actualReturns);
        if(plan is not null)throw new InvalidOperationException("The current Sky call already has a completion plan.");
        bool ready=call.Enabled&&call.InitializationRecorded&&call.WorkReady;
        if(call.Enabled&&!call.InitializationRecorded)throw new InvalidDataException("The enabled source call lacks its actual WORK initialization result.");
        uint nextState=state,units=0;float nextElapsed=elapsed;bool complete=false,blend=false;uint? completed=null;float fraction=0;
        if(!ready)
        {
            if(actualReturns!=new OriginalSkyCpuWorkAcknowledgement(0,false,0))
                throw new InvalidDataException("Disabled or not-ready native WORK cannot return draws/copy/filter work.");
        }
        else
        {
            uint k=call.GenerationIndex,flat=call.FirstFlat,phase=call.PreparationSubphase,remaining=call.RequestedWorkUnits;
            if(k>=3||flat>41||phase>1)throw new NotSupportedException("Sky lifecycle closes the actual fresh three-slot work state.");
            uint baseMax=flat<6?Math.Min(remaining,6-flat):0;
            if(actualReturns.BaseFacesReturned>baseMax||
                (actualReturns.BaseFacesReturned!=0&&actualReturns.BaseFacesReturned!=baseMax))
                throw new InvalidDataException("Actual successful base return must respect the original face/budget boundary.");
            flat+=actualReturns.BaseFacesReturned;remaining-=actualReturns.BaseFacesReturned;units+=actualReturns.BaseFacesReturned;
            bool baseStopped=baseMax>0&&actualReturns.BaseFacesReturned==0;
            bool shouldCopy=!baseStopped&&flat==6&&phase==0&&remaining>0;
            if(actualReturns.BasePreparationCalled!=shouldCopy)
                throw new InvalidDataException("Actual base-copy call does not match the source progress/subphase/budget branch.");
            if(shouldCopy){remaining--;units++;phase++;}
            uint filterMax=!baseStopped&&phase==1?Math.Min(remaining,42-flat):0;
            if(actualReturns.FilterFacesReturned>filterMax)
                throw new InvalidDataException("Actual filter return exceeds the source phase/budget range.");
            flat+=actualReturns.FilterFacesReturned;units+=actualReturns.FilterFacesReturned;
            if(flat==42){completed=k;k=(k+1)%3;flat=phase=0;nextElapsed=0;complete=true;}
            uint local=(state&~0x40000000u)|0x80000000;
            if(call.RequestedWorkUnits!=0)local=(local&0xFFFF9F00)|k|(flat<<2)|(phase<<13);
            (uint output,bool doBlend,float t)=SelectOutput(k,nextElapsed,call.InterpolationSpan);
            nextState=(local&0xFFFFFCFF)|(output<<8);blend=doBlend;fraction=t;
        }
        return plan=new(call,nextState,nextElapsed,units,complete,completed,blend,fraction);
    }
    /// <summary>Commit only after the actual source work call returns. The
    /// optional blend return is separate from the caller's work-unit pointer.
    /// EA3 then advances node elapsed20 and writes its prior clock18/1C.</summary>
    public OriginalSkyCpuState168Snapshot CompleteCurrentCall(OriginalSkyCpuState168CompletionPlan completed,
        uint actualBlendReturnValue)
    {
        ArgumentNullException.ThrowIfNull(completed);Require(completed.Call);
        if(!ReferenceEquals(plan,completed)||(!completed.RequiresBlend&&actualBlendReturnValue!=0)||
            (completed.RequiresBlend&&actualBlendReturnValue!=6))
            throw new InvalidDataException("Sky state completion requires the exact current plan and actual complete six-face blend branch.");
        float nextElapsed=Add(completed.ElapsedAfterWork,MathF.Max(Subtract(completed.Call.Clock.Value,priorCallerClock),0));
        if(!float.IsFinite(nextElapsed))throw new InvalidDataException("Sky node elapsed20 overflowed its actual finite source domain.");
        state=completed.State168;elapsed=nextElapsed;
        priorCallerClock=completed.Call.Clock.Value;lastFrame=completed.Call.FrameNumber;revision++;pending=null;plan=null;
        return CaptureForConsumer(lastFrame);
    }
    public OriginalSkyCpuState168Snapshot CaptureForConsumer(long actualConsumerFrame)
    {
        if(pending is not null||actualConsumerFrame<1||actualConsumerFrame<lastFrame)
            throw new InvalidDataException("Sky current state must be consumed after its call returns, in a current owned frame.");
        return new(this,revision,actualConsumerFrame,state,SourceProvenance+
            $";source-call-frame={lastFrame};consumer-frame={actualConsumerFrame};state168={state:X8};work-present={workPresent};not-a-GPU-readiness-receipt");
    }
    public void ResetWork()
    {
        if(pending is not null)throw new InvalidOperationException("Cannot reset WORK during a source call.");
        workPresent=false;state&=0x7FFFFFFF;revision++;
        // E45966 clears only bit31. It preserves output/progress/bit30.
    }
    internal void ValidateSnapshot(OriginalSkyCpuState168Snapshot snapshot,long capturedRevision,string sky,long frame)
    {
        if(pending is not null||sky!=OwnedSkyIdentity||snapshot.FrameNumber!=frame||frame<lastFrame||
            capturedRevision!=revision||snapshot.State168!=state)
            throw new InvalidDataException("Sky current state receipt is stale, foreign or from another consumer frame.");
    }
    private void Require(OriginalSkyCpuState168Call call)
    {if(!ReferenceEquals(pending,call)||!ReferenceEquals(call.Owner,this))throw new InvalidDataException("Sky call is stale or belongs to another owner.");}
    private static (uint Output,bool Blend,float Fraction) SelectOutput(uint k,float elapsed,float span)
    {
        if(span<=0)return((k+2)%3,false,0);
        float t=Divide(elapsed,span);
        if(t<=0)return((k+1)%3,false,t);
        if(t>=1)return((k+2)%3,false,t);
        return(3,true,t);
    }
    private static uint Word(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(at,4));
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Subtract(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
}

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadSkyCpuState168NativeSource(string sourceDirectory,string locale)
    {
        var profile=Profile(locale);byte[] exe=File.ReadAllBytes(Path.Combine(sourceDirectory,$"SAN14CE_{locale}.exe"));
        VerifyHash(exe,profile.ExeHash,"SkyCPU168 executable");var image=new NativeImage(exe);
        byte[] code=image.DecodeText(out int textRva);VerifyHash(code,profile.TextHash,"SkyCPU168 decoded text");
        (int Rva,int Count,string Hash)[] windows=locale switch{
            "TC"=>[(0xE38440,359,"0C3A7CF2C98C6FAFBDC83D0B1F7D5FF5CDB7508199B04F941FCB23BA56AD3DBD"),
                (0xE46810,1707,"0D1166B27B95DFFC08490F75479EC849202483D251457B8FAD9A26BEF2BC0BDA"),
                (0xEA3CF7,383,"6DDF24EB68A4C39D23E8D679751D82256603E46024FE2F15427A8A7A76A8EA64"),
                (0xE6C040,36,"924AC6C9DF003FD9573F5C0E204BDB57F9F6D43265D2B981A784289547137E56"),
                (0xE45940,64,"CD67C4B37284D6834FB0F9176A2693E663FFBE6D5D11330A3A6B1AB8ADB0BF57")],
            "SC"=>[(0xE392C0,359,"2244594010ADF27505592B624081F0DEEF1C53F59BC5ECCB6F4A2FA4867615C4"),
                (0xE47810,1707,"03709654303625CE0F5DC1A350E0749B1524C85EDE8558174C10642FAE9F23E5"),
                (0xEA4CE7,383,"880E575E61B16910402CD0BB6C1B50AA39275FE17F0730690BEB4A5338E64053"),
                (0xE6D0F0,36,"1D807BBB6ED3E15F123D8FE64051F698883FE84E57A855E6D1916C0451AE9372"),
                (0xE46940,64,"CD67C4B37284D6834FB0F9176A2693E663FFBE6D5D11330A3A6B1AB8ADB0BF57")],
            _=>throw new InvalidDataException("SkyCPU168 requires pinned SC or TC source.")};
        foreach(var w in windows)VerifyHash(code.AsSpan(w.Rva-textRva,w.Count),w.Hash,"SkyCPU168 lifecycle source");
        if(BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(locale=="TC"?0x1343B98:0x1344B98,4))!=0x3F800000)
            throw new InvalidDataException("SkyCPU168 interpolation1 threshold changed.");
        return $"SAN14CE_{locale}.exe@{profile.ExeHash};decodedText={profile.TextHash};"+
            "source-constructor-state0;E46810-lowK/progress/phase-independent-output8..9;enable-bit30;ready-bit31;EA3-elapsed20-after-return;reset-only-ready";
    }
}
