
namespace San14.Data;

/// <summary>The original working-wrapper40/42 versus module40/44 comparison.
/// This result determines Apply local5 controller argument4. It records a CPU
/// request; it does not acknowledge allocation or device completion.</summary>
public sealed record MapPostEffect3ActualExtentTransition(long FrameNumber,
    string OwnerIdentity,uint PriorModuleWidth40,uint PriorModuleHeight44,
    ushort WorkingWrapperWidth40,ushort WorkingWrapperHeight42,byte ControllerArgument4R9B,
    string SourceEvidence)
{
    public bool RequiresModuleResize=>ControllerArgument4R9B!=0;
}

/// <summary>Loaded complete paired native retained resize and optics boundaries.
/// FC/FD0 preserves controller construction dimensions and does not dirty AF.
/// Actual module resizing separately drives IsFirst and DOF's Count reset.</summary>
public sealed class LoadedMapPostEffect3RetainedResizeSource
{
    internal LoadedMapPostEffect3RetainedResizeSource(string locale,string ruleVersion)
    {Locale=locale;RuleVersion=ruleVersion;}
    public string Locale{get;}
    public string RuleVersion{get;}
    public string SourceEvidence=>$"{RenderRuleIdentity.For("MapPostEffect3RetainedResize",Locale)};retained-"+
        "controller-IsFirst->factory7-;project-rules";

    public MapPostEffect3ActualExtentTransition ReadActualExtentTransition(long frameNumber,string owner,
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
            SourceEvidence+"-actual-before-resize-extent-comparison;-r9b");
    }

    /// <summary>Normal retained Apply for the closed FC=FD=0 profile. The
    /// controller's88/8C/94/98 and effective80/84/F0/F4 survive actual resize.
    /// Other changed optics still require their actual dirty setter.</summary>
    public MapPostEffect3ApplyDerivedScalarPlan PrepareRetainedCurrentDerived(
        MapCameraConstructionPacket camera,MapPostEffect3ApplyFreshCameraScalars currentTail,
        MapPostEffect3ApplyFreshCameraScalars previousTail,MapPostEffect3ApplyDerivedScalarPlan previous,
        int actualWorkingSceneWidth,int actualWorkingSceneHeight,SkyWeatherNativePowerSource power)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(currentTail);
        ArgumentNullException.ThrowIfNull(previousTail);ArgumentNullException.ThrowIfNull(previous);ArgumentNullException.ThrowIfNull(power);
        if(camera.ExecutableLocale!=Locale||power.Locale!=Locale||previous.ExecutableLocale!=Locale||
            camera.FrameNumber!=previous.FrameNumber+1)
            throw new InvalidDataException("Retained derived Apply needs the same controller's next current Camera.");
        if((currentTail.Settings.DepthOfField.Flags&0x30)!=0||(previousTail.Settings.DepthOfField.Flags&0x30)!=0)
            throw new NotSupportedException("Retained FC/FD changes or enabled extent overrides require their actual  AF writer");
        var plan=MapPostEffect3ApplyDerivedScalars.PrepareRetainedExtentDisabledSource(
            camera,currentTail,previous,actualWorkingSceneWidth,actualWorkingSceneHeight,power);
        return plan with {SourceEvidence=plan.SourceEvidence+";"+SourceEvidence+
            "-FC0-returns-without80/84/F0/F4/AF-writes;retain-original88/8C/94/98"};
    }
}

public static class MapPostEffect3RetainedResizeSource
{
    public static LoadedMapPostEffect3RetainedResizeSource Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.LoadPostEffect3RetainedResizeSource(repository.SourceDirectory,locale);
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3RetainedResizeSource LoadPostEffect3RetainedResizeSource(string directory,string locale)
    {
        _ = RenderRuleIdentity.For("MapPostEffect3RetainedResize",locale);

        return new(locale,RenderRuleIdentity.Version);
    }
}
