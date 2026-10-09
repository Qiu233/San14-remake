using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Actual dimensions provided by the owned device at E24A80. They are
/// deliberately separate from the working Scene wrapper's dimensions. Field98
/// is initialized from this pair and E22590 does not update it.</summary>
public sealed record OriginalMapPostEffect3ApplyControllerDimensions(long FrameNumber,
    string OwnerIdentity, int DeviceWidth, int DeviceHeight, float Diagonal98,
    float DeviceAspect94, string SourceEvidence)
{
    public string ExecutableLocale{get;init;}="TC";
}

public sealed record OriginalMapPostEffect3ApplyDerivedScalarPlan(long FrameNumber,
    IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> InitializationWrites,
    IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> ApplyWrites,
    OriginalMapPostEffect3ApplyControllerDimensions Dimensions,
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

/// <summary>TC EB74A3..EB7928 computed scalar writers. Uses the already ported,
/// hash-pinned original positive finite SSE pow kernel. No framework pow,
/// substitute Camera tail, original process access or GPU dispatch occurs.</summary>
public static class OriginalMapPostEffect3ApplyDerivedScalars
{
    public const string SourceScope="TC:E24A80;E22590;EB74A3..EB7928;fresh-owned-controller;partial-derived-writers";
    private static string Scope(string locale)=>locale=="TC"?SourceScope:
        $"{locale}:paired-fresh-owned-controller;constructor={OriginalMapPostEffect3NativeSourceProfile.CodeRva(locale,"OriginalMapPostEffect3ApplyDerivedScalars.cs",0xE24A80):X8};partial-derived-writers";

    public static OriginalMapPostEffect3ApplyControllerDimensions ConstructFreshDimensions(
        long frameNumber,string ownerIdentity,int actualDeviceWidth,int actualDeviceHeight,
        OriginalSkyWeatherNativePowerSource actualSsePower)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerIdentity);ValidatePower(actualSsePower);
        if(frameNumber<=0)throw new InvalidDataException("Controller dimensions require an owned frame.");
        ValidateExtent(actualDeviceWidth,actualDeviceHeight);
        // Native cvtdq2ps/powf/addss/sqrtss, in its actual height-then-width order.
        float w=actualDeviceWidth,h=actualDeviceHeight;
        float diagonal=MathF.Sqrt(A(Pow(h,2,actualSsePower),Pow(w,2,actualSsePower)));
        return new(frameNumber,ownerIdentity,actualDeviceWidth,actualDeviceHeight,diagonal,D(w,h),
            Scope(actualSsePower.Locale)+";TC-reference-role:E24B6A..E24C06;device-descriptor50/54;"+actualSsePower.SourceProvenance)
            {ExecutableLocale=actualSsePower.Locale};
    }

    /// <summary>Fresh controller's supported named writes only. The supplied
    /// Scene dimensions are the actual working wrapper40/42, not viewport size.
    /// Later initialized/resized controller instances require a separate writer.
    /// This API never treats a source archive Camera as a sampled game frame.</summary>
    public static OriginalMapPostEffect3ApplyDerivedScalarPlan PrepareFreshSource(
        OriginalMapCameraConstructionPacket camera, OriginalMapPostEffect3ApplyFreshCameraScalars tail,
        OriginalMapPostEffect3ApplyControllerDimensions dimensions,
        int actualWorkingSceneWidth,int actualWorkingSceneHeight,
        OriginalSkyWeatherNativePowerSource actualSsePower)
        =>PrepareSourceCore(camera,tail,dimensions,actualWorkingSceneWidth,actualWorkingSceneHeight,actualSsePower,null);

    internal static OriginalMapPostEffect3ApplyDerivedScalarPlan PrepareRetainedExtentDisabledSource(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyFreshCameraScalars tail,
        OriginalMapPostEffect3ApplyDerivedScalarPlan previous,int actualWorkingSceneWidth,int actualWorkingSceneHeight,
        OriginalSkyWeatherNativePowerSource actualSsePower)
        =>PrepareSourceCore(camera,tail,previous.Dimensions with {FrameNumber=camera.FrameNumber},
            actualWorkingSceneWidth,actualWorkingSceneHeight,actualSsePower,previous);

    private static OriginalMapPostEffect3ApplyDerivedScalarPlan PrepareSourceCore(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyFreshCameraScalars tail,
        OriginalMapPostEffect3ApplyControllerDimensions dimensions,int actualWorkingSceneWidth,int actualWorkingSceneHeight,
        OriginalSkyWeatherNativePowerSource actualSsePower,OriginalMapPostEffect3ApplyDerivedScalarPlan? retained)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(tail);
        ArgumentNullException.ThrowIfNull(dimensions);ValidatePower(actualSsePower);
        tail.ValidateSameCamera(camera);ValidateExtent(actualWorkingSceneWidth,actualWorkingSceneHeight);
        if(camera.ExecutableLocale!=actualSsePower.Locale||camera.FrameNumber!=dimensions.FrameNumber||
            string.IsNullOrWhiteSpace(dimensions.OwnerIdentity))
            throw new InvalidDataException("Derived Apply writers require the same-locale Camera/controller owned frame.");
        string evidence=Scope(camera.ExecutableLocale)+";"+camera.FrameProvenance+";"+tail.SourceEvidence+";"+dimensions.SourceEvidence;
        var init=new List<OriginalMapPostEffect3ApplyValueWrite>();
        var apply=new List<OriginalMapPostEffect3ApplyValueWrite>();
        uint Code(uint rva)=>(uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(camera.ExecutableLocale,"OriginalMapPostEffect3ApplyDerivedScalars.cs",(int)rva);
        void F(List<OriginalMapPostEffect3ApplyValueWrite> output,string name,uint at,uint helper,params float[] values)
        {
            if(values.Any(v=>!float.IsFinite(v)))throw new NotSupportedException("Native derived scalar reaches a nonfinite branch.");
            byte[] bytes=new byte[checked(values.Length*4)];
            for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4),values[i]);
            output.Add(new(name,Code(at),Code(helper),bytes,evidence));
        }
        void I(List<OriginalMapPostEffect3ApplyValueWrite> output,string name,uint at,uint helper,int value)
        {byte[] b=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(b,value);output.Add(new(name,Code(at),Code(helper),b,evidence));}
        if(retained is null)
        {
            F(init,"ScreenWidth",0xE24C0F,0xE24A80,dimensions.DeviceWidth);
            F(init,"ScreenHeight",0xE24C35,0xE24A80,dimensions.DeviceHeight);
            I(init,"EnableDebugBloomSoftKneeCurve",0xE24C60,0xE24A80,0);
            I(init,"EnableDebugHistgram",0xE24C81,0xE24A80,0);
        }
        uint flags=tail.Word(0x1320);
        bool useWorkingExtent=(flags&(1u<<4))!=0;
        bool multiResolutionFix=(flags&(1u<<5))!=0;
        if(retained is not null&&(useWorkingExtent||multiResolutionFix))
            throw new NotSupportedException("Retained FC/FD changes need their actual E22590 dirty optics writer.");
        float width=useWorkingExtent?actualWorkingSceneWidth:dimensions.DeviceWidth;
        float height=useWorkingExtent?actualWorkingSceneHeight:dimensions.DeviceHeight;
        if(retained is not null){width=retained.EffectiveWidth80;height=retained.EffectiveHeight84;}
        // E1CD10 flagsFC/FD are fresh zero. E22590 writes ScreenWidth/Height
        // only when either flag changes, or enabled override changes its extent.
        if(useWorkingExtent||multiResolutionFix)
        {
            F(apply,"ScreenWidth",0xEB74A3,0xE22590,width);
            F(apply,"ScreenHeight",0xEB74A3,0xE22590,height);
            I(apply,"Enable4KResFix",0xEB74A3,0xE22590,useWorkingExtent?1:0);
            I(apply,"EnableMultiResFix",0xEB74A3,0xE22590,multiResolutionFix?1:0);
        }
        // E227E0 writes the internal A9 byte, not a named shader parameter.
        bool spriteDof=tail.Float(0x1324)>=0;
        F(apply,"LuminanceRangeMinMax",0xEB76B6,0xE259C0,
            Pow(2,tail.Float(0x136C),actualSsePower),Pow(2,tail.Float(0x1370),actualSsePower));
        F(apply,"AdjustMiddleGray",0xEB76F2,0xE25000,
            M(Pow(2,tail.Float(0x1328),actualSsePower),B(0x3E5EDC7F)));
        F(apply,"BloomThresholdCurveParams",0xEB7790,0xE25100,
            Pow(tail.Float(0x1354),B(0x400CCCCD),actualSsePower),tail.Float(0x1358));
        F(apply,"TinyChromaAberraAmount",0xEB77E8,0xE25180,M(D(64,dimensions.Diagonal98),tail.Float(0x1380)));
        F(apply,"LensFlareThreshold",0xEB7814,0xE25910,
            Pow(tail.Float(0x1360),B(0x400CCCCD),actualSsePower));
        float limb=Pow(camera.ExtraScalars.LimbDarkeningWeight7B0,B(0x3EE8BA2E),actualSsePower);
        // E22530 first publishes prior controller70 if enabled, otherwise -.5.
        // Fresh E1CD10 memset leaves70 zero, and E25980 then replaces it.
        F(apply,"LimbDarkenningLinFade",0xEB7834,0xE22530,-.5f);
        F(apply,"LimbDarkenningLinFade",0xEB786C,0xE25980,limb);
        float aspectScale=B(0x40166666);
        float f4,f0;
        if(retained is not null){f4=retained.AspectRatioF4;f0=retained.SensorHeightRatioF0;}
        else
        {
            float diagonal=MathF.Sqrt(A(Pow(M(aspectScale,width),2,actualSsePower),Pow(height,2,actualSsePower)));
            f4=D(dimensions.DeviceAspect94,aspectScale);f0=D(height,diagonal);
        }
        float dc=tail.Float(0x135C);
        float intensity=BinaryPrimitives.ReadSingleLittleEndian(camera.Prefix.Span.Slice(0x180,4));
        // E22480 writes byteAA only; E250F0 writes floatDC only. They are not
        // shader-name writes despite adjacent E22490/E25100 wrapper code.
        if(!float.IsFinite(dc)||!float.IsFinite(f4)||!float.IsFinite(f0))
            throw new NotSupportedException("Fresh controller scalar reaches a nonfinite native branch.");
        return new(camera.FrameNumber,Array.AsReadOnly(init.ToArray()),Array.AsReadOnly(apply.ToArray()),
            dimensions,width,height,f4,f0,dc,intensity>0&&dc>=1,spriteDof,evidence);
    }
    private static void ValidatePower(OriginalSkyWeatherNativePowerSource power)
    {ArgumentNullException.ThrowIfNull(power);if(power.Locale is not ("TC" or "SC"))throw new InvalidDataException("Derived Apply recipe requires a pinned TC/SC SSE power source.");}
    private static void ValidateExtent(int width,int height)
    {if(width<=0||height<=0||width>ushort.MaxValue||height>ushort.MaxValue)throw new InvalidDataException("Apply extent is outside the positive native working-wrapper16-bit range.");}
    private static float Pow(float value,float exponent,OriginalSkyWeatherNativePowerSource power)=>OriginalSkyWeatherNativePower.PowSse(value,exponent,power);
    private static float B(uint word)=>BitConverter.UInt32BitsToSingle(word);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
