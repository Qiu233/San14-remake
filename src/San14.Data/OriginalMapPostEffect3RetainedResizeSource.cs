
namespace San14.Data;

/// <summary>The original working-wrapper40/42 versus module40/44 comparison.
/// This result determines Apply local5 / controller argument4. It records a CPU
/// request; it does not acknowledge allocation or device completion.</summary>
public sealed record OriginalMapPostEffect3ActualExtentTransition(long FrameNumber,
    string OwnerIdentity,uint PriorModuleWidth40,uint PriorModuleHeight44,
    ushort WorkingWrapperWidth40,ushort WorkingWrapperHeight42,byte ControllerArgument4R9B,
    string SourceEvidence)
{
    public bool RequiresModuleResize=>ControllerArgument4R9B!=0;
}

/// <summary>Loaded complete paired native retained resize and optics boundaries.
/// FC/FD0 preserves controller construction dimensions and does not dirty AF.
/// Actual module resizing separately drives IsFirst and DOF's Count reset.</summary>
public sealed class LoadedOriginalMapPostEffect3RetainedResizeSource
{
    internal LoadedOriginalMapPostEffect3RetainedResizeSource(string locale,string ruleVersion)
    {Locale=locale;RuleVersion=ruleVersion;}
    public string Locale{get;}
    public string RuleVersion{get;}
    public string SourceEvidence=>$"{RenderRuleIdentity.For("MapPostEffect3RetainedResize",Locale)};retained-E22590/EB5D50/1145D00/1147830/11468D0;"+
        "controller-E22860-IsFirst->factory7-1155140/1157230;project-rules";

    public OriginalMapPostEffect3ActualExtentTransition ReadActualExtentTransition(long frameNumber,string owner,
        uint actualPriorModuleWidth40,uint actualPriorModuleHeight44,int actualWorkingWrapperWidth40,int actualWorkingWrapperHeight42)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if(frameNumber<=0||actualPriorModuleWidth40==0||actualPriorModuleHeight44==0||
            actualWorkingWrapperWidth40<=0||actualWorkingWrapperHeight42<=0||
            actualWorkingWrapperWidth40>ushort.MaxValue||actualWorkingWrapperHeight42>ushort.MaxValue)
            throw new InvalidDataException("Retained resize needs the actual module and positive wrapper extents.");
        ushort w=(ushort)actualWorkingWrapperWidth40,h=(ushort)actualWorkingWrapperHeight42;
        byte argument4=(byte)(actualPriorModuleWidth40!=w||actualPriorModuleHeight44!=h?1:0);
        return new(frameNumber,owner,actualPriorModuleWidth40,actualPriorModuleHeight44,w,h,argument4,
            SourceEvidence+";EB68F3..EB6982-actual-before-resize-extent-comparison;EB7F5E-r9b");
    }

    /// <summary>Normal retained Apply for the closed FC=FD=0 profile. The
    /// controller's88/8C/94/98 and effective80/84/F0/F4 survive actual resize.
    /// Other changed optics still require their actual dirty setter.</summary>
    public OriginalMapPostEffect3ApplyDerivedScalarPlan PrepareRetainedCurrentDerived(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyFreshCameraScalars currentTail,
        OriginalMapPostEffect3ApplyFreshCameraScalars previousTail,OriginalMapPostEffect3ApplyDerivedScalarPlan previous,
        int actualWorkingSceneWidth,int actualWorkingSceneHeight,OriginalSkyWeatherNativePowerSource power)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(currentTail);
        ArgumentNullException.ThrowIfNull(previousTail);ArgumentNullException.ThrowIfNull(previous);ArgumentNullException.ThrowIfNull(power);
        if(camera.ExecutableLocale!=Locale||power.Locale!=Locale||previous.ExecutableLocale!=Locale||
            camera.FrameNumber!=previous.FrameNumber+1)
            throw new InvalidDataException("Retained derived Apply needs the same controller's next current Camera.");
        if((currentTail.Word(0x1320)&0x30)!=0||(previousTail.Word(0x1320)&0x30)!=0)
            throw new NotSupportedException("Retained FC/FD changes or enabled extent overrides require their actual E22590 AF writer.");
        var plan=OriginalMapPostEffect3ApplyDerivedScalars.PrepareRetainedExtentDisabledSource(
            camera,currentTail,previous,actualWorkingSceneWidth,actualWorkingSceneHeight,power);
        return plan with {SourceEvidence=plan.SourceEvidence+";"+SourceEvidence+
            ";E225F2-FC0-returns-without80/84/F0/F4/AF-writes;retain-original88/8C/94/98"};
    }
}

public static class OriginalMapPostEffect3RetainedResizeSource
{
    public static LoadedOriginalMapPostEffect3RetainedResizeSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadPostEffect3RetainedResizeSource(repository.SourceDirectory,locale);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3RetainedResizeSource LoadPostEffect3RetainedResizeSource(string directory,string locale)
    {
        _ = RenderRuleIdentity.For("MapPostEffect3RetainedResize",locale);

        return new(locale,RenderRuleIdentity.Version);
    }
}
