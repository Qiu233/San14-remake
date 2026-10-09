using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Named runtime view supplied by the owned renderer after the actual
/// source resource lookup. This is an identity, not a native pointer or an
/// assertion that a particular original-game frame used this view.</summary>
public sealed record MapPostEffect3ApplyView(long FrameNumber,string OwnerIdentity);

public enum MapPostEffect3ApplyBindingKind { Texture, NonOwningRuntimeView }

public sealed record MapPostEffect3ApplyBinding(string Name,
    MapPostEffect3ApplyBindingKind Kind,MapPostEffect3ApplyView? View,
    string SourceEvidence);

/// <summary>Already resolved current resource owners. LightSceneEnabled is the
/// actual predicate. Null optional owners mean explicit lookup absence;
/// unknown lookups must not use this API.</summary>
public sealed record MapPostEffect3ApplyExternalInputs(long FrameNumber,
    MapPostEffect3ApplyView WorkingScene,MapPostEffect3ApplyView? Depth,
    bool LightSceneEnabled,MapPostEffect3ApplyView? LightScene,
    MapPostEffect3ApplyView? Velocity,MapPostEffect3ApplyView Noise,
    MapPostEffect3ApplyView? PreviousDepth,MapPostEffect3ApplyView? PreviousVelocity,
    string LookupEvidence);

/// <summary>Preserves the native binding and cleanup order. InputPrevVelocity
/// intentionally has no cleanup in ..; the native owner retains it.</summary>
public sealed record MapPostEffect3ApplyBindingPlan(long FrameNumber,
    IReadOnlyList<MapPostEffect3ApplyBinding> BeforeExecution,
    IReadOnlyList<MapPostEffect3ApplyBinding> AfterExecution,
    string LookupEvidence);

public sealed class MapPostEffect3ApplyValueWrite
{
    private readonly byte[] bytes;
    internal MapPostEffect3ApplyValueWrite(string name,MapPostEffect3WriteStage stage,byte[] value,string evidence)
    {Name=name;Stage=stage;bytes=value.ToArray();SourceEvidence=evidence;}
    public string Name{get;}
    public MapPostEffect3WriteStage Stage{get;}
    public MapPostEffect3WritePhase Phase=>MapPostEffect3WriteStages.Phase(Stage);
    public ReadOnlyMemory<byte> Bytes=>bytes;
    public string SourceEvidence{get;}
    public MapPostEffect3ApplyValueWrite CopyAcceptedSnapshot()=>
        new(Name,Stage,bytes,SourceEvidence);
}

/// <summary>Only writers whose actual inputs are available from the complete
/// prepared Camera prefix, its independently audited ExtraScalars and explicit
/// Aux are admitted. This partial recipe never authorizes a complete 3SPK draw.</summary>
public sealed record MapPostEffect3ApplyKnownWrites(long FrameNumber,
    IReadOnlyList<MapPostEffect3ApplyValueWrite> Writes,string CameraEvidence)
{
    public bool IsCompleteForExecution=>false;
    public string UnsupportedInputs=>
        "remaining controller-derived values; Camera1798..1850 resource owners; "+
        "current Apply1A4/1A8 functor indices; controller initialization/derived values; "+
        "native parameter types/aliases and pass execution state";
    public void RequireCompleteForExecution()=>throw new NotSupportedException(
        "PostEffect3 known writers are partial: "+UnsupportedInputs);
}

/// <summary>TC Common3C0==1/PostEffect3-present source path. The module
/// is 6CA7130C/3SPK1000; none of these keys are the A1C builtin80 buffer.</summary>
public static class MapPostEffect3ApplyRecipe
{
    public const string SourceScope="TC;PostEffect3=6CA7130C;3SPK1000;Common3C0=1";
    private static string Scope(string locale)=>locale=="TC"?SourceScope:
        $"{locale}:paired-controlled-Apply;PostEffect3=6CA7130C;3SPK1000;Common3C0=1";

    private static MapPostEffect3ApplyValueWrite Value(string locale,string name,MapPostEffect3WriteStage stage,
        byte[] bytes,string evidence)=>new(name,stage,bytes,evidence);

    /// <summary>.. temporarily replaces the external exposure scale
    /// only for the actual rbp30 predicate. restores a saved nonnull
    /// view; a null previous view has no restore write in the original path.</summary>
    public static MapPostEffect3ApplyBindingPlan PrepareExposureScaleSwap(long frameNumber,
        bool actualOutputExposureScalePredicate,MapPostEffect3ApplyView? outputExposureScale,
        MapPostEffect3ApplyView? previousScale,string sourceEvidence,string locale="TC")
    {
        if(frameNumber<=0)throw new InvalidDataException("Exposure swap requires an owned frame.");
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        foreach(var view in new[]{outputExposureScale,previousScale})
            if(view is not null&&(view.FrameNumber!=frameNumber||string.IsNullOrWhiteSpace(view.OwnerIdentity)))
                throw new InvalidDataException("Exposure scale owner is missing or belongs to another frame.");
        var before=new List<MapPostEffect3ApplyBinding>();var after=new List<MapPostEffect3ApplyBinding>();
        if(actualOutputExposureScalePredicate)
        {
            ArgumentNullException.ThrowIfNull(outputExposureScale);
            before.Add(new("EA2OutputExposureScale",MapPostEffect3ApplyBindingKind.Texture,
                outputExposureScale,Scope(locale)+";actual-global-rbp1A78+50;"+sourceEvidence));
            if(previousScale is not null)after.Add(new("EA2OutputExposureScale",MapPostEffect3ApplyBindingKind.Texture,
                previousScale,Scope(locale)+";saved-nonnull-view-restored;"+sourceEvidence));
        }
        return new(frameNumber,Array.AsReadOnly(before.ToArray()),Array.AsReadOnly(after.ToArray()),sourceEvidence);
    }

    /// <summary>Add independent direct setters from the separately recovered
    /// fresh 44-word Camera tail. Derived controller expressions and resource
    /// lookups still require their actual source implementations.</summary>
    public static MapPostEffect3ApplyKnownWrites PrepareFreshSourceKnownCameraWrites(
        MapCameraConstructionPacket camera,MapPostEffect3ApplyFreshCameraScalars tail,
        ReadOnlySpan<byte> preparedAux460,string auxEvidence)
    {
        return PrepareFreshSourceKnownCameraWrites(camera,tail,
            MapPostEffect3ControlledAuxFields.CaptureActualPrepared(preparedAux460,camera.FrameNumber,"explicit-selected-View",auxEvidence));
    }
    public static MapPostEffect3ApplyKnownWrites PrepareFreshSourceKnownCameraWrites(
        MapCameraConstructionPacket camera,MapPostEffect3ApplyFreshCameraScalars tail,
        MapPostEffect3ControlledAuxFields actualAuxFields)
    {
        ArgumentNullException.ThrowIfNull(tail);tail.ValidateSameCamera(camera);
        var prefix=PrepareKnownCameraWrites(camera,actualAuxFields);
        var writes=prefix.Writes.ToList();
        string evidence=prefix.CameraEvidence+";"+tail.SourceEvidence;
        void F(string name,MapPostEffect3WriteStage stage,int offset)
        {
            byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(bytes,tail.Word(offset));
            writes.Add(Value(camera.ExecutableLocale,name,stage,bytes,evidence+$";Camera{offset:X}"));
        }
        F("SpriteDofThreshold",MapPostEffect3WriteStage.ControlledSpriteDofThreshold,0x1324);
        // is an exact 8-byte pair from the existing prepared prefix.
        writes.Add(Value(camera.ExecutableLocale,"EvRangeMinMax",MapPostEffect3WriteStage.ControlledEvRangeMinMax,camera.Prefix.Span.Slice(0x1B8,8).ToArray(),evidence+";Camera1B8/1BC"));
        byte[] omitted=new byte[8];BinaryPrimitives.WriteUInt32LittleEndian(omitted,tail.Word(0x1374));
        BinaryPrimitives.WriteUInt32LittleEndian(omitted.AsSpan(4),tail.Word(0x1378));
        writes.Add(Value(camera.ExecutableLocale,"OmittedLuminancePercentsLowHigh",MapPostEffect3WriteStage.ControlledOmittedLuminancePercentsLowHigh,omitted,evidence+";Camera1374/1378"));
        byte[] chromaType=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(chromaType,Math.Clamp(tail.Int(0x137C),0,3));
        writes.Add(Value(camera.ExecutableLocale,"TinyChromaAberraType",MapPostEffect3WriteStage.ControlledTinyChromaAberraType,chromaType,evidence+";Camera137C;native-clamp0..3"));
        F("CrossFilterWeight",MapPostEffect3WriteStage.ControlledCrossFilter,0x139C);
        F("CrossFilterLength",MapPostEffect3WriteStage.ControlledCrossFilter,0x13A0);
        F("CrossFilterAttenuation",MapPostEffect3WriteStage.ControlledCrossFilter,0x13A4);
        F("CrossFilterInclination",MapPostEffect3WriteStage.ControlledCrossFilter,0x13A8);
        F("CrossFilterAberration",MapPostEffect3WriteStage.ControlledCrossFilterAberration,0x13AC);
        return new(camera.FrameNumber,Array.AsReadOnly(writes.OrderBy(w=>w.Stage).ToArray()),evidence);
    }

    public static MapPostEffect3ApplyBindingPlan PrepareExternalBindings(
        MapPostEffect3ApplyExternalInputs input,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(input);
        if(input.FrameNumber<=0)throw new InvalidDataException("Apply bindings require an owned frame.");
        ArgumentException.ThrowIfNullOrWhiteSpace(input.LookupEvidence);
        void Validate(MapPostEffect3ApplyView? v)
        {
            if(v is not null&&(v.FrameNumber!=input.FrameNumber||string.IsNullOrWhiteSpace(v.OwnerIdentity)))
                throw new InvalidDataException("PostEffect3 binding belongs to another frame or has no resolved owner.");
        }
        ArgumentNullException.ThrowIfNull(input.WorkingScene);ArgumentNullException.ThrowIfNull(input.Noise);
        foreach(var v in new[]{input.WorkingScene,input.Depth,input.LightScene,input.Velocity,input.Noise,input.PreviousDepth,input.PreviousVelocity})Validate(v);
        var before=new List<MapPostEffect3ApplyBinding>();
        var after=new List<MapPostEffect3ApplyBinding>();
        MapPostEffect3ApplyBinding B(string name,MapPostEffect3ApplyView? view,
            string evidence,MapPostEffect3ApplyBindingKind kind=MapPostEffect3ApplyBindingKind.Texture)
            =>new(name,kind,view,Scope(locale)+";"+evidence+";"+input.LookupEvidence);
        before.Add(B("InputColor",input.WorkingScene,"actual-working-wrapper"));
        if(input.Depth is not null)before.Add(B("InputDepth",input.Depth,"resolved-rbp50-depth"));
        before.Add(B("InputLightScene",input.LightSceneEnabled&&input.LightScene is not null?input.LightScene:input.WorkingScene,
            "-when-enabled-and-present;otherwise-working-wrapper"));
        if(input.Velocity is not null)before.Add(B("InputVelocity",input.Velocity,
            "Apply60-current-list+2C-or-Apply58"));
        before.Add(B("InputNoise",input.Noise,"resolved-A099841E-v28-or-Aux310+10;non-owning-r9=0",
            MapPostEffect3ApplyBindingKind.NonOwningRuntimeView));
        if(input.PreviousDepth is not null)before.Add(B("InputPrevDepth",input.PreviousDepth,"Apply170"));
        if(input.PreviousVelocity is not null)before.Add(B("InputPrevVelocity",input.PreviousVelocity,"Apply178"));
        // Source guards each operation with the resolved parameter slot<count.
        // The executor must preserve that guard; a missing key is a skipped write.
        after.Add(B("InputColor",null,"clear both runtime views/refcounts"));
        if(input.Depth is not null)after.Add(B("InputDepth",null,"depth-present guard"));
        after.Add(B("InputLightScene",null,"D72210"));
        after.Add(B("InputNoise",null,"r8=r9=0",MapPostEffect3ApplyBindingKind.NonOwningRuntimeView));
        if(input.PreviousDepth is not null)after.Add(B("InputPrevDepth",null,"previous-depth-present guard"));
        if(input.Velocity is not null)after.Add(B("InputVelocity",null,"velocity-present guard"));
        return new(input.FrameNumber,Array.AsReadOnly(before.ToArray()),Array.AsReadOnly(after.ToArray()),input.LookupEvidence);
    }

    /// <summary>Exact independent setters, kept in their Apply call order.
    /// Aux carries only fields consumed here from this owned frame. Other
    /// controller writers remain explicitly unsupported, rather than zeroed.</summary>
    public static MapPostEffect3ApplyKnownWrites PrepareKnownCameraWrites(
        MapCameraConstructionPacket camera,ReadOnlySpan<byte> preparedAux460,string auxEvidence)
    {
        return PrepareKnownCameraWrites(camera,
            MapPostEffect3ControlledAuxFields.CaptureActualPrepared(preparedAux460,camera.FrameNumber,"explicit-selected-View",auxEvidence));
    }
    public static MapPostEffect3ApplyKnownWrites PrepareKnownCameraWrites(
        MapCameraConstructionPacket camera,MapPostEffect3ControlledAuxFields aux)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(aux);aux.ValidateFrame(camera.FrameNumber);
        if(camera.FrameNumber<=0||camera.Prefix.Length!=0x3C0||aux.Mode3C0!=1)
            throw new InvalidDataException("Known PostEffect3 writes require this owned Camera and explicit prepared mode1 Aux.");
        byte[] prefix=camera.Prefix.ToArray();var extra=camera.ExtraScalars;
        var result=new List<MapPostEffect3ApplyValueWrite>();
        string evidence=Scope(camera.ExecutableLocale)+";whole-Camera-copy-base-rbp140;"+camera.FrameProvenance+";"+aux.SourceEvidence;
        void Raw(string name,MapPostEffect3WriteStage stage,int offset,int size)
            =>result.Add(Value(camera.ExecutableLocale,name,stage,prefix.AsSpan(offset,size).ToArray(),evidence+$";Camera{offset:X}"));
        void Int(string name,MapPostEffect3WriteStage stage,int value)
        {byte[] b=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(b,value);result.Add(Value(camera.ExecutableLocale,name,stage,b,evidence));}
        void Float(string name,MapPostEffect3WriteStage stage,float value)
        {byte[] b=new byte[4];BinaryPrimitives.WriteSingleLittleEndian(b,value);result.Add(Value(camera.ExecutableLocale,name,stage,b,evidence));}
        // tests Camera158 bit1. The swapped source lanes are intentional.
        bool swap=(BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(0x158,4))&2)!=0;
        byte[] nearFar=new byte[8];prefix.AsSpan(swap?0x144:0x140,4).CopyTo(nearFar);
        prefix.AsSpan(swap?0x140:0x144,4).CopyTo(nearFar.AsSpan(4));
        result.Add(Value(camera.ExecutableLocale,"CameraNearFarCm",MapPostEffect3WriteStage.ControlledCameraNearFarCm,nearFar,evidence+";Camera140/144;Camera158.bit1-swap"));
        bool autoExposure=(aux.Flags8&(1u<<25))!=0||(camera.SourceSpecialRectangleFlag1510&1)!=0;
        // encodes enabled=+1 and disabled=-1, unlike the other bool setters.
        Int("EnableAutoExposure",MapPostEffect3WriteStage.ControlledEnableAutoExposure,autoExposure?1:-1);
        if(!autoExposure)Raw("UserExposureScale",MapPostEffect3WriteStage.ControlledUserExposureScale,0x170,4);
        Raw("BloomIntensity",MapPostEffect3WriteStage.ControlledBloomIntensity,0x180,4);
        Int("UseBloomAntiFlicker",MapPostEffect3WriteStage.ControlledUseBloomAntiFlicker,1);
        Int("Enable4KCBFix",MapPostEffect3WriteStage.ControlledEnable4KCBFix,0);
        Int("EnableBloomColorFix",MapPostEffect3WriteStage.ControlledEnableBloomColorFix,
            aux.BloomColorFix448==1?1:0);
        Raw("LensFlareIntensity",MapPostEffect3WriteStage.ControlledLensFlareIntensity,0x188,4);
        Float("FXAAEdgeThreshold",MapPostEffect3WriteStage.ControlledFXAAEdgeThreshold,extra.FxaaEdgeSensitivity404);
        Float("FXAAEdgeThresholdMin",MapPostEffect3WriteStage.ControlledFXAAEdgeThreshold,extra.FxaaEdgeSensitivityMin408);
        Float("FXAAEdgeSharpness",MapPostEffect3WriteStage.ControlledFXAAEdgeSharpness,extra.FxaaSharpness40C);
        Float("FXAAPixelRange",MapPostEffect3WriteStage.ControlledFXAAPixelRange,extra.FxaaPixelRange410);
        return new(camera.FrameNumber,Array.AsReadOnly(result.ToArray()),evidence);
    }
}
