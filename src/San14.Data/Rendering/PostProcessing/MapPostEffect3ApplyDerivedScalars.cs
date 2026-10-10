using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Actual dimensions provided by the owned device. They are
/// deliberately separate from the working Scene wrapper's dimensions. Field98
/// is initialized from this pair; later camera preparation leaves it unchanged.</summary>
public sealed record MapPostEffect3ApplyControllerDimensions(long FrameNumber,
    string OwnerIdentity, int DeviceWidth, int DeviceHeight, float Diagonal98,
    float DeviceAspect94, string SourceEvidence)
{
    public string ExecutableLocale{get;init;}="TC";
}

public sealed record MapPostEffect3ApplyDerivedScalarPlan(long FrameNumber,
    IReadOnlyList<MapPostEffect3ApplyValueWrite> InitializationWrites,
    IReadOnlyList<MapPostEffect3ApplyValueWrite> ApplyWrites,
    MapPostEffect3ApplyControllerDimensions Dimensions,
    float EffectiveWidth80, float EffectiveHeight84,
    float AspectRatioF4, float SensorHeightRatioF0,
    float BloomAntiFlickerDc, bool BloomAntiFlickerAa, bool SpriteDofA9, string SourceEvidence)
{
    public string ExecutableLocale=>Dimensions.ExecutableLocale;
    public bool IsCompleteForExecution=>false;
    public string UnsupportedInputs=>"resource-dependent controller optics and LUT/tonemap lookup; " +
        "complete controller initialization parameter table; module execution/resource bindings; " +
        "current per-view Camera writers; non-SSE pow dispatch";
    public void RequireCompleteForExecution()=>throw new NotSupportedException(UnsupportedInputs);
}

/// <summary>Computed controller scalar writers. Uses the ported
/// positive finite SSE pow kernel. No framework pow,
/// substitute Camera tail, original process access or GPU dispatch occurs.</summary>
public static class MapPostEffect3ApplyDerivedScalars
{
    public const string SourceScope="TC;fresh-owned-controller;partial-derived-writers";
    private static string Scope(string locale)=>locale=="TC"?SourceScope:
        $"{locale}:paired-fresh-owned-controller;typed-controller;partial-derived-writers";

    public static MapPostEffect3ApplyControllerDimensions ConstructFreshDimensions(
        long frameNumber,string ownerIdentity,int actualDeviceWidth,int actualDeviceHeight,
        SkyWeatherNativePowerSource actualSsePower)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerIdentity);ValidatePower(actualSsePower);
        if(frameNumber<=0)throw new InvalidDataException("Controller dimensions require an owned frame.");
        ValidateExtent(actualDeviceWidth,actualDeviceHeight);
        // Native cvtdq2ps/powf/addss/sqrtss, in its actual height-then-width order.
        float w=actualDeviceWidth,h=actualDeviceHeight;
        float diagonal=MathF.Sqrt(A(Pow(h,2,actualSsePower),Pow(w,2,actualSsePower)));
        return new(frameNumber,ownerIdentity,actualDeviceWidth,actualDeviceHeight,diagonal,D(w,h),
            Scope(actualSsePower.Locale)+";device-descriptor50/54;"+actualSsePower.SourceProvenance)
            {ExecutableLocale=actualSsePower.Locale};
    }

    /// <summary>Fresh controller's supported named writes only. The supplied
    /// Scene dimensions are the actual working wrapper40/42, not viewport size.
    /// Later initialized/resized controller instances require a separate writer.
    /// This API never treats a source archive Camera as a sampled game frame.</summary>
    public static MapPostEffect3ApplyDerivedScalarPlan PrepareFreshSource(
        MapCameraConstructionPacket camera, MapPostEffect3ApplyFreshCameraScalars tail,
        MapPostEffect3ApplyControllerDimensions dimensions,
        int actualWorkingSceneWidth,int actualWorkingSceneHeight,
        SkyWeatherNativePowerSource actualSsePower)
        =>PrepareSourceCore(camera,tail,dimensions,actualWorkingSceneWidth,actualWorkingSceneHeight,actualSsePower,null);

    internal static MapPostEffect3ApplyDerivedScalarPlan PrepareRetainedExtentDisabledSource(
        MapCameraConstructionPacket camera,MapPostEffect3ApplyFreshCameraScalars tail,
        MapPostEffect3ApplyDerivedScalarPlan previous,int actualWorkingSceneWidth,int actualWorkingSceneHeight,
        SkyWeatherNativePowerSource actualSsePower)
        =>PrepareSourceCore(camera,tail,previous.Dimensions with {FrameNumber=camera.FrameNumber},
            actualWorkingSceneWidth,actualWorkingSceneHeight,actualSsePower,previous);

    private static MapPostEffect3ApplyDerivedScalarPlan PrepareSourceCore(
        MapCameraConstructionPacket camera,MapPostEffect3ApplyFreshCameraScalars tail,
        MapPostEffect3ApplyControllerDimensions dimensions,int actualWorkingSceneWidth,int actualWorkingSceneHeight,
        SkyWeatherNativePowerSource actualSsePower,MapPostEffect3ApplyDerivedScalarPlan? retained)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(tail);
        ArgumentNullException.ThrowIfNull(dimensions);ValidatePower(actualSsePower);
        tail.ValidateSameCamera(camera);ValidateExtent(actualWorkingSceneWidth,actualWorkingSceneHeight);
        if(camera.ExecutableLocale!=actualSsePower.Locale||camera.FrameNumber!=dimensions.FrameNumber||
            string.IsNullOrWhiteSpace(dimensions.OwnerIdentity))
            throw new InvalidDataException("Derived Apply writers require the same-locale Camera/controller owned frame.");
        string evidence=Scope(camera.ExecutableLocale)+";"+camera.FrameProvenance+";"+tail.SourceEvidence+";"+dimensions.SourceEvidence;
        var init=new List<MapPostEffect3ApplyValueWrite>();
        var apply=new List<MapPostEffect3ApplyValueWrite>();

        void F(List<MapPostEffect3ApplyValueWrite> output,string name,MapPostEffect3WriteStage stage,params float[] values)
        {
            if(values.Any(v=>!float.IsFinite(v)))throw new NotSupportedException("Native derived scalar reaches a nonfinite branch.");
            byte[] bytes=new byte[checked(values.Length*4)];
            for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4),values[i]);
            output.Add(new(name,stage,bytes,evidence));
        }
        void I(List<MapPostEffect3ApplyValueWrite> output,string name,MapPostEffect3WriteStage stage,int value)
        {byte[] b=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(b,value);output.Add(new(name,stage,b,evidence));}
        if(retained is null)
        {
            F(init,"ScreenWidth",MapPostEffect3WriteStage.InitialScreenWidth,dimensions.DeviceWidth);
            F(init,"ScreenHeight",MapPostEffect3WriteStage.InitialScreenHeight,dimensions.DeviceHeight);
            I(init,"EnableDebugBloomSoftKneeCurve",MapPostEffect3WriteStage.InitialDebugBloom,0);
            I(init,"EnableDebugHistgram",MapPostEffect3WriteStage.InitialDebugHistogram,0);
        }
        uint flags=tail.Settings.DepthOfField.Flags;
        bool useWorkingExtent=(flags&(1u<<4))!=0;
        bool multiResolutionFix=(flags&(1u<<5))!=0;
        if(retained is not null&&(useWorkingExtent||multiResolutionFix))
            throw new NotSupportedException("Retained FC/FD changes need their actual  dirty optics writer");
        float width=useWorkingExtent?actualWorkingSceneWidth:dimensions.DeviceWidth;
        float height=useWorkingExtent?actualWorkingSceneHeight:dimensions.DeviceHeight;
        if(retained is not null){width=retained.EffectiveWidth80;height=retained.EffectiveHeight84;}
        // flagsFC/FD are fresh zero. writes ScreenWidth/Height
        // only when either flag changes, or enabled override changes its extent.
        if(useWorkingExtent||multiResolutionFix)
        {
            F(apply,"ScreenWidth",MapPostEffect3WriteStage.ControlledResize,width);
            F(apply,"ScreenHeight",MapPostEffect3WriteStage.ControlledResize,height);
            I(apply,"Enable4KResFix",MapPostEffect3WriteStage.ControlledResize,useWorkingExtent?1:0);
            I(apply,"EnableMultiResFix",MapPostEffect3WriteStage.ControlledResize,multiResolutionFix?1:0);
        }
        // writes the internal A9 byte, not a named shader parameter.
        bool spriteDof=tail.Settings.DepthOfField.SpriteThreshold>=0;
        F(apply,"LuminanceRangeMinMax",MapPostEffect3WriteStage.ControlledLuminanceRangeMinMax,
            Pow(2,tail.Settings.Exposure.MinBrightness,actualSsePower),Pow(2,tail.Settings.Exposure.MaxBrightness,actualSsePower));
        F(apply,"AdjustMiddleGray",MapPostEffect3WriteStage.ControlledAdjustMiddleGray,
            M(Pow(2,BitConverter.UInt32BitsToSingle(tail.Settings.DepthOfField.CompareBrightnessBits),actualSsePower),B(0x3E5EDC7F)));
        F(apply,"BloomThresholdCurveParams",MapPostEffect3WriteStage.ControlledBloomThresholdCurveParams,
            Pow(tail.Settings.LensEffects.BloomThreshold,B(0x400CCCCD),actualSsePower),tail.Settings.LensEffects.BloomThresholdSoftKnee);
        F(apply,"TinyChromaAberraAmount",MapPostEffect3WriteStage.ControlledTinyChromaAberraAmount,M(D(64,dimensions.Diagonal98),tail.Settings.LensEffects.AberraAmount));
        F(apply,"LensFlareThreshold",MapPostEffect3WriteStage.ControlledLensFlareThreshold,
            Pow(tail.Settings.LensEffects.LensFlareThreshold,B(0x400CCCCD),actualSsePower));
        float limb=Pow(camera.Optics.Limb.Weight,B(0x3EE8BA2E),actualSsePower);
        // first publishes prior controller70 if enabled, otherwise -.5.
        // Fresh memset leaves70 zero, and then replaces it.
        F(apply,"LimbDarkenningLinFade",MapPostEffect3WriteStage.ControlledResetLimbDarkening,-.5f);
        F(apply,"LimbDarkenningLinFade",MapPostEffect3WriteStage.ControlledSetLimbDarkening,limb);
        float aspectScale=B(0x40166666);
        float f4,f0;
        if(retained is not null){f4=retained.AspectRatioF4;f0=retained.SensorHeightRatioF0;}
        else
        {
            float diagonal=MathF.Sqrt(A(Pow(M(aspectScale,width),2,actualSsePower),Pow(height,2,actualSsePower)));
            f4=D(dimensions.DeviceAspect94,aspectScale);f0=D(height,diagonal);
        }
        float dc=tail.Settings.LensEffects.BloomRadius;
        float intensity=BinaryPrimitives.ReadSingleLittleEndian(camera.Prefix.Span.Slice(0x180,4));
        // writes byteAA only; writes floatDC only. They are not
        // shader-name writes despite adjacent wrapper code.
        if(!float.IsFinite(dc)||!float.IsFinite(f4)||!float.IsFinite(f0))
            throw new NotSupportedException("Fresh controller scalar reaches a nonfinite native branch.");
        return new(camera.FrameNumber,Array.AsReadOnly(init.ToArray()),Array.AsReadOnly(apply.ToArray()),
            dimensions,width,height,f4,f0,dc,intensity>0&&dc>=1,spriteDof,evidence);
    }
    private static void ValidatePower(SkyWeatherNativePowerSource power)
    {ArgumentNullException.ThrowIfNull(power);if(power.Locale is not ("TC" or "SC"))throw new InvalidDataException("Derived Apply recipe requires a pinned TC/SC SSE power source.");}
    private static void ValidateExtent(int width,int height)
    {if(width<=0||height<=0||width>ushort.MaxValue||height>ushort.MaxValue)throw new InvalidDataException("Apply extent is outside the positive native working-wrapper16-bit range.");}
    private static float Pow(float value,float exponent,SkyWeatherNativePowerSource power)=>SkyWeatherNativePower.PowSse(value,exponent,power);
    private static float B(uint word)=>BitConverter.UInt32BitsToSingle(word);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
