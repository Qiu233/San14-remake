using San14.Data;

/// <summary>Common254/258 produced by this live World preparation's retained
/// clock before geometry. This is CPU time publication, not draw completion.</summary>
public sealed class MapPostEffect3WorldCommonClockPublication
{
    internal MapPostEffect3WorldCommonClockPublication(MapPostEffect3WorldControlledPreparation owner,
        NativeMapFrameContext context,MapCommonClockPublication clock)
        =>(Owner,Context,SourceClock)=(owner,context,clock);
    internal MapPostEffect3WorldControlledPreparation Owner{get;}
    public NativeMapFrameContext Context{get;}
    public MapCommonClockPublication SourceClock{get;}
    public long FrameNumber=>SourceClock.FrameNumber;
    public float CommonTime254=>SourceClock.CommonTime254;
    public float CommonEffectiveDelta258=>SourceClock.CommonEffectiveDelta258;
    public string SourceEvidence=>SourceClock.SourceEvidence+";same-actual-World-context/Common-owner;not-native-upstream-timer-equivalence";
    public void RequireCurrent()=>Owner.RequireCurrentCommonClock(this);
}

public sealed partial class MapPostEffect3WorldControlledPreparation
{
    private MapPostEffect3WorldCommonClockPublication? currentCommonClock;

    /// <summary>Call after the same request's current World CPU preparation,
    /// before57/58 consume Common254. Pass the actual App/renderer clock input,
    /// not a presentation duration substituted for that source input.</summary>
    public MapPostEffect3WorldCommonClockPublication PrepareCommonClockBeforeGeometry(
        NativeMapFrameContext actualContext,MapCommonRenderState actualCommon,
        MapPostEffect3FirstWorldFrontendPreparationInputs actualClock)
    {
        ArgumentNullException.ThrowIfNull(actualContext);ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(actualClock);
        lock(clockGate)
        {
            RequireCurrentClockWorld(actualContext,actualCommon);
            var publication=commonClock.PrepareCurrent(actualContext.FrameNumber,actualCommon,
                actualClock.ActualRendererElapsedDelta,actualClock.ActualClockOwnerIdentity);
            if(currentCommonClock is not null&&ReferenceEquals(currentCommonClock.SourceClock,publication))
            {
                if(!ReferenceEquals(currentCommonClock.Context,actualContext))
                    throw new InvalidDataException("The current Common time belongs to another actual World context.");
                return currentCommonClock;
            }
            return currentCommonClock=new(this,actualContext,publication);
        }
    }
    internal void RequireCurrentCommonClock(MapPostEffect3WorldCommonClockPublication actual)
    {
        lock(clockGate)
        {
            if(!ReferenceEquals(actual,currentCommonClock)||!ReferenceEquals(actual.Owner,this))
                throw new InvalidDataException("World Common clock is foreign or no longer current.");
            actual.SourceClock.RequireCurrent();
            var common=host.Session.CurrentFrame?.PreparedSourceFrame?.Common??
                throw new InvalidDataException("World has no accepted current Common.");
            actual.SourceClock.RequireCurrentForCommon(common);
            RequireCurrentClockWorld(actual.Context,common);
        }
    }
    private void RequireCurrentClockWorld(NativeMapFrameContext context,MapCommonRenderState common)
    {
        var prepared=host.Session.CurrentFrame??throw new InvalidDataException("Common clock requires current World CPU preparation.");
        var world=host.Session.CurrentWorld??throw new InvalidDataException("Common clock requires the same actual World source owner.");
        if(!ReferenceEquals(prepared.Context,context)||!ReferenceEquals(dependencies.WorldSource.RequireCurrent(context),world)||
            !ReferenceEquals(prepared.PreparedSourceFrame,world.First)||!ReferenceEquals(world.First.Common,common)||
            common.FrameNumber!=context.FrameNumber||common.SelectedViewOwnerIdentity!=world.First.Lighting.Binding.ViewIdentity||
            world.First.Camera.ExecutableLocale!=ExecutableLocale)
            throw new InvalidDataException("Common clock input is foreign to the current World/context/View/source generation.");
    }
}
