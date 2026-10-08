using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Named runtime view supplied by the owned renderer after the actual
/// source resource lookup. This is an identity, not a native pointer or an
/// assertion that a particular original-game frame used this view.</summary>
public sealed record OriginalMapPostEffect3ApplyView(long FrameNumber,string OwnerIdentity);

public enum OriginalMapPostEffect3ApplyBindingKind { Texture, NonOwningRuntimeView }

public sealed record OriginalMapPostEffect3ApplyBinding(string Name,
    OriginalMapPostEffect3ApplyBindingKind Kind,OriginalMapPostEffect3ApplyView? View,
    uint CallSiteRva,string SourceEvidence);

/// <summary>Already resolved current resource owners. LightSceneEnabled is the
/// actual EB6A4A predicate. Null optional owners mean explicit lookup absence;
/// unknown lookups must not use this API.</summary>
public sealed record OriginalMapPostEffect3ApplyExternalInputs(long FrameNumber,
    OriginalMapPostEffect3ApplyView WorkingScene,OriginalMapPostEffect3ApplyView? Depth,
    bool LightSceneEnabled,OriginalMapPostEffect3ApplyView? LightScene,
    OriginalMapPostEffect3ApplyView? Velocity,OriginalMapPostEffect3ApplyView Noise,
    OriginalMapPostEffect3ApplyView? PreviousDepth,OriginalMapPostEffect3ApplyView? PreviousVelocity,
    string LookupEvidence);

/// <summary>Preserves the native binding and cleanup order. InputPrevVelocity
/// intentionally has no cleanup in EB80DD..EB8211; the native owner retains it.</summary>
public sealed record OriginalMapPostEffect3ApplyBindingPlan(long FrameNumber,
    IReadOnlyList<OriginalMapPostEffect3ApplyBinding> BeforeExecution,
    IReadOnlyList<OriginalMapPostEffect3ApplyBinding> AfterExecution,
    string LookupEvidence);

public sealed class OriginalMapPostEffect3ApplyValueWrite
{
    private readonly byte[] bytes;
    internal OriginalMapPostEffect3ApplyValueWrite(string name,uint callSite,uint helper,byte[] value,string evidence)
    {Name=name;CallSiteRva=callSite;HelperRva=helper;bytes=value.ToArray();SourceEvidence=evidence;}
    public string Name{get;}
    public uint CallSiteRva{get;}
    public uint HelperRva{get;}
    public ReadOnlyMemory<byte> Bytes=>bytes;
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3ApplyValueWrite CopyAcceptedSnapshot()=>
        new(Name,CallSiteRva,HelperRva,bytes,SourceEvidence);
}

/// <summary>Only writers whose actual inputs are available from the complete
/// prepared Camera prefix, its independently audited ExtraScalars and explicit
/// Aux are admitted. This partial recipe never authorizes a complete 3SPK draw.</summary>
public sealed record OriginalMapPostEffect3ApplyKnownWrites(long FrameNumber,
    IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> Writes,string CameraEvidence)
{
    public bool IsCompleteForExecution=>false;
    public string UnsupportedInputs=>
        "remaining controller-derived values; Camera1798..1850 resource owners; "+
        "current Apply1A4/1A8 functor indices; controller initialization/derived values; "+
        "native parameter types/aliases and pass execution state";
    public void RequireCompleteForExecution()=>throw new NotSupportedException(
        "PostEffect3 known writers are partial: "+UnsupportedInputs);
}

/// <summary>TC EB5D50 Common3C0==1/PostEffect3-present source path. The module
/// is 6CA7130C/3SPK1000; none of these keys are the A1C builtin80 buffer.</summary>
public static class OriginalMapPostEffect3ApplyRecipe
{
    public const string SourceScope="TC:EB6899..EB8609;PostEffect3=6CA7130C;3SPK1000;Common3C0=1";
    private static string Scope(string locale)=>locale=="TC"?SourceScope:
        $"{locale}:paired-controlled-Apply;PostEffect3=6CA7130C;3SPK1000;Common3C0=1";
    private static uint Code(string locale,uint rva)=>(uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(locale,
        "OriginalMapPostEffect3ApplyRecipe.cs",(int)rva);
    private static OriginalMapPostEffect3ApplyValueWrite Value(string locale,string name,uint callSite,uint helper,
        byte[] bytes,string evidence)=>new(name,Code(locale,callSite),Code(locale,helper),bytes,evidence);

    /// <summary>EB7EEF..EB7F55 temporarily replaces the external exposure scale
    /// only for the actual rbp30 predicate. EB7FCE restores a saved nonnull
    /// view; a null previous view has no restore write in the original path.</summary>
    public static OriginalMapPostEffect3ApplyBindingPlan PrepareExposureScaleSwap(long frameNumber,
        bool actualOutputExposureScalePredicate,OriginalMapPostEffect3ApplyView? outputExposureScale,
        OriginalMapPostEffect3ApplyView? previousScale,string sourceEvidence,string locale="TC")
    {
        if(frameNumber<=0)throw new InvalidDataException("Exposure swap requires an owned frame.");
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        foreach(var view in new[]{outputExposureScale,previousScale})
            if(view is not null&&(view.FrameNumber!=frameNumber||string.IsNullOrWhiteSpace(view.OwnerIdentity)))
                throw new InvalidDataException("Exposure scale owner is missing or belongs to another frame.");
        var before=new List<OriginalMapPostEffect3ApplyBinding>();var after=new List<OriginalMapPostEffect3ApplyBinding>();
        if(actualOutputExposureScalePredicate)
        {
            ArgumentNullException.ThrowIfNull(outputExposureScale);
            before.Add(new("EA2OutputExposureScale",OriginalMapPostEffect3ApplyBindingKind.Texture,
                outputExposureScale,Code(locale,0xEB7F49),Scope(locale)+";actual-global-rbp1A78+50;"+sourceEvidence));
            if(previousScale is not null)after.Add(new("EA2OutputExposureScale",OriginalMapPostEffect3ApplyBindingKind.Texture,
                previousScale,Code(locale,0xEB7FEA),Scope(locale)+";saved-nonnull-view-restored;"+sourceEvidence));
        }
        return new(frameNumber,Array.AsReadOnly(before.ToArray()),Array.AsReadOnly(after.ToArray()),sourceEvidence);
    }

    /// <summary>Add independent direct setters from the separately recovered
    /// fresh 44-word Camera tail. Derived controller expressions and resource
    /// lookups still require their actual source implementations.</summary>
    public static OriginalMapPostEffect3ApplyKnownWrites PrepareFreshSourceKnownCameraWrites(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyFreshCameraScalars tail,
        ReadOnlySpan<byte> preparedAux460,string auxEvidence)
    {
        return PrepareFreshSourceKnownCameraWrites(camera,tail,
            OriginalMapPostEffect3ControlledAuxFields.CaptureActualPrepared(preparedAux460,camera.FrameNumber,"explicit-selected-View",auxEvidence));
    }
    public static OriginalMapPostEffect3ApplyKnownWrites PrepareFreshSourceKnownCameraWrites(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyFreshCameraScalars tail,
        OriginalMapPostEffect3ControlledAuxFields actualAuxFields)
    {
        ArgumentNullException.ThrowIfNull(tail);tail.ValidateSameCamera(camera);
        var prefix=PrepareKnownCameraWrites(camera,actualAuxFields);
        var writes=prefix.Writes.ToList();
        string evidence=prefix.CameraEvidence+";"+tail.SourceEvidence;
        void F(string name,uint at,uint helper,int offset)
        {
            byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(bytes,tail.Word(offset));
            writes.Add(Value(camera.ExecutableLocale,name,at,helper,bytes,evidence+$";Camera{offset:X}"));
        }
        F("SpriteDofThreshold",0xEB75E3,0xE25CC0,0x1324);
        // E25510 is an exact 8-byte pair from the existing prepared prefix.
        writes.Add(Value(camera.ExecutableLocale,"EvRangeMinMax",0xEB7658,0xE25510,camera.Prefix.Span.Slice(0x1B8,8).ToArray(),evidence+";Camera1B8/1BC"));
        byte[] omitted=new byte[8];BinaryPrimitives.WriteUInt32LittleEndian(omitted,tail.Word(0x1374));
        BinaryPrimitives.WriteUInt32LittleEndian(omitted.AsSpan(4),tail.Word(0x1378));
        writes.Add(Value(camera.ExecutableLocale,"OmittedLuminancePercentsLowHigh",0xEB76CE,0xE25AD0,omitted,evidence+";Camera1374/1378"));
        byte[] chromaType=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(chromaType,Math.Clamp(tail.Int(0x137C),0,3));
        writes.Add(Value(camera.ExecutableLocale,"TinyChromaAberraType",0xEB77D8,0xE251D0,chromaType,evidence+";Camera137C;native-clamp0..3"));
        F("CrossFilterWeight",0xEB789A,0xE25220,0x139C);
        F("CrossFilterLength",0xEB789A,0xE25220,0x13A0);
        F("CrossFilterAttenuation",0xEB789A,0xE25220,0x13A4);
        F("CrossFilterInclination",0xEB789A,0xE25220,0x13A8);
        F("CrossFilterAberration",0xEB78AA,0xE252C0,0x13AC);
        return new(camera.FrameNumber,Array.AsReadOnly(writes.OrderBy(w=>w.CallSiteRva).ToArray()),evidence);
    }

    public static OriginalMapPostEffect3ApplyBindingPlan PrepareExternalBindings(
        OriginalMapPostEffect3ApplyExternalInputs input,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(input);
        if(input.FrameNumber<=0)throw new InvalidDataException("Apply bindings require an owned frame.");
        ArgumentException.ThrowIfNullOrWhiteSpace(input.LookupEvidence);
        void Validate(OriginalMapPostEffect3ApplyView? v)
        {
            if(v is not null&&(v.FrameNumber!=input.FrameNumber||string.IsNullOrWhiteSpace(v.OwnerIdentity)))
                throw new InvalidDataException("PostEffect3 binding belongs to another frame or has no resolved owner.");
        }
        ArgumentNullException.ThrowIfNull(input.WorkingScene);ArgumentNullException.ThrowIfNull(input.Noise);
        foreach(var v in new[]{input.WorkingScene,input.Depth,input.LightScene,input.Velocity,input.Noise,input.PreviousDepth,input.PreviousVelocity})Validate(v);
        var before=new List<OriginalMapPostEffect3ApplyBinding>();
        var after=new List<OriginalMapPostEffect3ApplyBinding>();
        OriginalMapPostEffect3ApplyBinding B(string name,OriginalMapPostEffect3ApplyView? view,uint at,
            string evidence,OriginalMapPostEffect3ApplyBindingKind kind=OriginalMapPostEffect3ApplyBindingKind.Texture)
            =>new(name,kind,view,Code(locale,at),Scope(locale)+";"+evidence+";"+input.LookupEvidence);
        before.Add(B("InputColor",input.WorkingScene,0xEB69F0,"D72210;actual-working-wrapper"));
        if(input.Depth is not null)before.Add(B("InputDepth",input.Depth,0xEB6A23,"D72210;resolved-rbp50-depth"));
        before.Add(B("InputLightScene",input.LightSceneEnabled&&input.LightScene is not null?input.LightScene:input.WorkingScene,
            input.LightSceneEnabled&&input.LightScene is not null?0xEB6A88u:0xEB6A97u,
            "EB6A4A/EB6A50;C0BB30-when-enabled-and-present;otherwise-working-wrapper"));
        if(input.Velocity is not null)before.Add(B("InputVelocity",input.Velocity,0xEB6B0C,
            "Apply60-current-list+2C-or-Apply58;C091E0/C0BB30;D72210"));
        before.Add(B("InputNoise",input.Noise,0xEB6BDD,"E25D00;resolved-A099841E-v28-or-Aux310+10;non-owning-r9=0",
            OriginalMapPostEffect3ApplyBindingKind.NonOwningRuntimeView));
        if(input.PreviousDepth is not null)before.Add(B("InputPrevDepth",input.PreviousDepth,0xEB6C98,"Apply170;D72210"));
        if(input.PreviousVelocity is not null)before.Add(B("InputPrevVelocity",input.PreviousVelocity,0xEB6CD3,"Apply178;D72210"));
        // Source guards each operation with the resolved parameter slot<count.
        // The executor must preserve that guard; a missing key is a skipped write.
        after.Add(B("InputColor",null,0xEB817E,"EB80F1..EB817E;clear both runtime views/refcounts"));
        if(input.Depth is not null)after.Add(B("InputDepth",null,0xEB81A1,"D72210;depth-present guard"));
        after.Add(B("InputLightScene",null,0xEB81BE,"D72210"));
        after.Add(B("InputNoise",null,0xEB81E1,"E25D00;r8=r9=0",OriginalMapPostEffect3ApplyBindingKind.NonOwningRuntimeView));
        if(input.PreviousDepth is not null)after.Add(B("InputPrevDepth",null,0xEB81F8,"D72210;previous-depth-present guard"));
        if(input.Velocity is not null)after.Add(B("InputVelocity",null,0xEB820C,"D72210;velocity-present guard"));
        return new(input.FrameNumber,Array.AsReadOnly(before.ToArray()),Array.AsReadOnly(after.ToArray()),input.LookupEvidence);
    }

    /// <summary>Exact independent setters, kept in their Apply call order.
    /// Aux carries only fields consumed here from this owned frame. Other
    /// controller writers remain explicitly unsupported, rather than zeroed.</summary>
    public static OriginalMapPostEffect3ApplyKnownWrites PrepareKnownCameraWrites(
        OriginalMapCameraConstructionPacket camera,ReadOnlySpan<byte> preparedAux460,string auxEvidence)
    {
        return PrepareKnownCameraWrites(camera,
            OriginalMapPostEffect3ControlledAuxFields.CaptureActualPrepared(preparedAux460,camera.FrameNumber,"explicit-selected-View",auxEvidence));
    }
    public static OriginalMapPostEffect3ApplyKnownWrites PrepareKnownCameraWrites(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ControlledAuxFields aux)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(aux);aux.ValidateFrame(camera.FrameNumber);
        if(camera.FrameNumber<=0||camera.Prefix.Length!=0x3C0||aux.Mode3C0!=1)
            throw new InvalidDataException("Known PostEffect3 writes require this owned Camera and explicit prepared mode1 Aux.");
        byte[] prefix=camera.Prefix.ToArray();var extra=camera.ExtraScalars;
        var result=new List<OriginalMapPostEffect3ApplyValueWrite>();
        string evidence=Scope(camera.ExecutableLocale)+";whole-Camera-copy-base-rbp140;"+camera.FrameProvenance+";"+aux.SourceEvidence;
        void Raw(string name,uint at,uint helper,int offset,int size)
            =>result.Add(Value(camera.ExecutableLocale,name,at,helper,prefix.AsSpan(offset,size).ToArray(),evidence+$";Camera{offset:X}"));
        void Int(string name,uint at,uint helper,int value)
        {byte[] b=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(b,value);result.Add(Value(camera.ExecutableLocale,name,at,helper,b,evidence));}
        void Float(string name,uint at,uint helper,float value)
        {byte[] b=new byte[4];BinaryPrimitives.WriteSingleLittleEndian(b,value);result.Add(Value(camera.ExecutableLocale,name,at,helper,b,evidence));}
        // EB74B1 tests Camera158 bit1. The swapped source lanes are intentional.
        bool swap=(BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(0x158,4))&2)!=0;
        byte[] nearFar=new byte[8];prefix.AsSpan(swap?0x144:0x140,4).CopyTo(nearFar);
        prefix.AsSpan(swap?0x140:0x144,4).CopyTo(nearFar.AsSpan(4));
        result.Add(Value(camera.ExecutableLocale,"CameraNearFarCm",0xEB74D9,0xE25140,nearFar,evidence+";Camera140/144;Camera158.bit1-swap"));
        bool autoExposure=(aux.Flags8&(1u<<25))!=0||(camera.SourceSpecialRectangleFlag1510&1)!=0;
        // E22440 encodes enabled=+1 and disabled=-1, unlike the other bool setters.
        Int("EnableAutoExposure",0xEB771D,0xE22440,autoExposure?1:-1);
        if(!autoExposure)Raw("UserExposureScale",0xEB7733,0xE25E80,0x170,4);
        Raw("BloomIntensity",0xEB776C,0xE250C0,0x180,4);
        Int("UseBloomAntiFlicker",0xEB77AA,0xE22490,1);
        Int("Enable4KCBFix",0xEB77B4,0xE25490,0);
        Int("EnableBloomColorFix",0xEB77CA,0xE254D0,
            aux.BloomColorFix448==1?1:0);
        Raw("LensFlareIntensity",0xEB77F8,0xE258C0,0x188,4);
        Float("FXAAEdgeThreshold",0xEB7C3B,0xE25700,extra.FxaaEdgeSensitivity404);
        Float("FXAAEdgeThresholdMin",0xEB7C3B,0xE25700,extra.FxaaEdgeSensitivityMin408);
        Float("FXAAEdgeSharpness",0xEB7C4B,0xE255B0,extra.FxaaSharpness40C);
        Float("FXAAPixelRange",0xEB7C5B,0xE255E0,extra.FxaaPixelRange410);
        return new(camera.FrameNumber,Array.AsReadOnly(result.ToArray()),evidence);
    }
}
