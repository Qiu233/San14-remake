using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>F40628..AA CPU writes. Effective delta3350/258 is separate from
/// the difference between accumulated/wrapped timestamps.</summary>
public readonly record struct OriginalMapCommonFrameSyncTime(float PreviousTime334C,
    float CurrentTime3348, float EffectiveDelta3350)
{
    public float Scalar3354=>1;
}

public static class OriginalMapCommonFrameClockSource
{
    /// <summary>Pure original scalar operations from actual settings+10 and
    /// the admitted App/renderer delta. This does not publish a current table.</summary>
    public static OriginalMapCommonFrameSyncTime PrepareFrameSyncTime(float actualPrior3348,
        float actualRendererDelta,uint actualSelectedSettingsFlags10)
    {
        if(!float.IsFinite(actualPrior3348)||!float.IsFinite(actualRendererDelta))
            throw new InvalidDataException("Source FrameSync scalar input is not finite.");
        bool paused=(actualSelectedSettingsFlags10&8)!=0;
        float effective=paused?0:actualRendererDelta;
        float value=paused?actualPrior3348:Add(actualPrior3348,actualRendererDelta);
        int steps=0;
        while(value>3600)
        {
            float next=Add(value,-3600);
            if(++steps>100000||next==value)throw new NotSupportedException("Source FrameSync time left its finite progress domain.");
            value=next;
        }
        return new(actualPrior3348,value,effective);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]internal static float Subtract(float a,float b)=>a-b;
}

/// <summary>Current D12ACB/D61BB4/BBC publication from this retained owned
/// clock. No geometry, post-effect, native timer or GPU completion is implied.</summary>
public sealed class OriginalMapCommonClockPublication
{
    internal OriginalMapCommonClockPublication(OriginalMapOwnedCommonFrameClock owner,long frame,
        OriginalMapFreshCommonResourceSnapshot common,string clockOwner,float delta,OriginalMapCommonFrameSyncTime time)
        =>(Owner,FrameNumber,Common,ActualClockOwnerIdentity,ActualInputDelta,Time)=(owner,frame,common,clockOwner,delta,time);
    internal OriginalMapOwnedCommonFrameClock Owner{get;}
    internal OriginalMapFreshCommonResourceSnapshot Common{get;}
    internal float ActualInputDelta{get;}
    public long FrameNumber{get;}
    public string ActualClockOwnerIdentity{get;}
    public OriginalMapCommonFrameSyncTime Time{get;}
    public float CommonTime254=>Time.CurrentTime3348;
    public float CommonEffectiveDelta258=>Time.EffectiveDelta3350;
    public string SourceEvidence=>Owner.SourceEvidence+$";frame={FrameNumber};actual-clock={ActualClockOwnerIdentity};"+
        "F40628-owned3348-once/D12ACB-D61BB4-Common254/D12AB6-D61BBC-Common258;CPU-publication-only";
    public void RequireCurrent()=>Owner.RequireCurrent(this);
    public void RequireCurrentForCommon(OriginalMapFreshCommonResourceSnapshot actualCommon)
    {
        RequireCurrent();
        if(!ReferenceEquals(actualCommon,Common))throw new InvalidDataException("Published Common time belongs to another current table packet.");
    }
}

public sealed class OriginalMapApplyClockConsumption
{
    internal OriginalMapApplyClockConsumption(OriginalMapCommonClockPublication common,float previous,float delta)
        =>(Common,PreviousApplyTime1E4,ApplyDelta)=(common,previous,delta);
    public OriginalMapCommonClockPublication Common{get;}
    public float PreviousApplyTime1E4{get;}
    public float CurrentApplyTime1E4=>Common.CommonTime254;
    public float ApplyDelta{get;}
    public string SourceEvidence=>Common.SourceEvidence+";EB5E7A-read-same254/EB5E8D-subtract1E4/EB5E96-write1E4-once";
}

/// <summary>One retained Common/Apply clock generation. The source constructor
/// writes both starting values0. Loaded settings/caller are imported once;
/// each current request publishes3348/254/258 and later consumes Apply1E4.
/// Only one current publication is retained. Frame numbers are identities.
/// The native upstream fixed/external/counter timer branch remains an input.</summary>
public sealed class OriginalMapOwnedCommonFrameClock
{
    private readonly object gate=new();
    private readonly LoadedOriginalMapPostEffect3ControlledFreshCallerSource caller;
    private readonly LoadedOriginalMapFreshCommonSource template;
    private float currentTime3348,previousApplyTime1E4;
    private OriginalMapCommonClockPublication? current;
    private OriginalMapApplyClockConsumption? apply;
    public OriginalMapOwnedCommonFrameClock(LoadedOriginalMapPostEffect3ControlledFreshCallerSource loadedCaller,
        LoadedOriginalMapFreshCommonSource loadedSelectedTemplate)
    {
        caller=loadedCaller??throw new ArgumentNullException(nameof(loadedCaller));
        template=loadedSelectedTemplate??throw new ArgumentNullException(nameof(loadedSelectedTemplate));
    }
    public string SourceEvidence=>caller.SourceEvidence+";"+template.SourceIdentity+
        ";actual-owned-Common-D0176F3348zero/Apply-E6BB1C1E4zero;retained-source-clock";

    public OriginalMapCommonClockPublication PrepareCurrent(long frame,OriginalMapFreshCommonResourceSnapshot actualCommon,
        float actualRendererDelta,string actualClockOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualCommon);ArgumentException.ThrowIfNullOrWhiteSpace(actualClockOwnerIdentity);
        lock(gate)
        {
            if(frame<=0||frame!=actualCommon.FrameNumber||!float.IsFinite(actualRendererDelta))
                throw new InvalidDataException("Common clock publication needs the actual same accepted frame and finite App delta.");
            if(current is not null)
            {
                if(frame<current.FrameNumber||current.ActualClockOwnerIdentity!=actualClockOwnerIdentity)
                    throw new InvalidDataException("Retained Common clock belongs to a newer request or another actual clock owner.");
                if(frame==current.FrameNumber)
                {
                    if(!ReferenceEquals(actualCommon,current.Common)||
                        BitConverter.SingleToUInt32Bits(actualRendererDelta)!=BitConverter.SingleToUInt32Bits(current.ActualInputDelta))
                        throw new InvalidDataException("This request already published another Common or App delta.");
                    return current;
                }
            }
            // The loaded overload retains the exact existing selected Common
            // scope validation without rereading/hashing the template.
            float value=caller.AdvanceOwnedFrameSyncTime(currentTime3348,actualRendererDelta,template,actualCommon);
            float effective=(template.Flags10&8)!=0?0:actualRendererDelta;
            var publication=new OriginalMapCommonClockPublication(this,frame,actualCommon,actualClockOwnerIdentity,
                actualRendererDelta,new(currentTime3348,value,effective));
            currentTime3348=value;current=publication;apply=null;
            return publication;
        }
    }
    public OriginalMapApplyClockConsumption ConsumeApplyCurrent(OriginalMapCommonClockPublication actualPublication)
    {
        lock(gate)
        {
            RequireCurrent(actualPublication);
            if(apply is not null)return apply;
            var consumption=new OriginalMapApplyClockConsumption(actualPublication,previousApplyTime1E4,
                OriginalMapCommonFrameClockSource.Subtract(actualPublication.CommonTime254,previousApplyTime1E4));
            previousApplyTime1E4=actualPublication.CommonTime254;apply=consumption;
            return consumption;
        }
    }
    internal void RequireCurrent(OriginalMapCommonClockPublication actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        lock(gate)
        {
            if(!ReferenceEquals(actual,current)||!ReferenceEquals(actual.Owner,this))
                throw new InvalidDataException("Common clock publication is foreign or no longer current.");
        }
    }
}
