using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Explicit E71690 caller ABI values. OptionalAuxFlags8 null means
/// the context+240 Aux lookup is known absent. Gamma enable is the actual
/// current native1A8C934 word; no initial/current global is inferred here.</summary>
public sealed record OriginalMapPostEffect3ApplyUncontrolledSimpleInputs(long FrameNumber,
    int ActualFxaaModeArgument,float ActualGammaValueArgument,float ActualDeltaTimeArgument,
    uint ActualGammaEnableWord1A8C934,uint? OptionalAuxFlags8,string CallerEvidence);

/// <summary>E7242C..E72459 source identities. A present Camera1810 supplies
/// Lens CPU+8 and Camera15CC address owners. These are pointer-valued scalar
/// parameters, not texture bindings or fake integer pointers.</summary>
public sealed record OriginalMapPostEffect3ApplyUncontrolledLensPresetOwners(long FrameNumber,
    bool ActualCamera1810Present,OriginalMapPostEffect3ApplyView? SplitOwner,
    OriginalMapPostEffect3ApplyView? IntermediateOwner,string LookupEvidence);

public sealed record OriginalMapPostEffect3ApplyUncontrolledPointerWrite(string Name,
    OriginalMapPostEffect3ApplyView? OwnedPointerTarget,uint CallSiteRva,string SourceEvidence)
{
    public int NativeByteCount=>8;
    public bool IsNativeNullPointer=>OwnedPointerTarget is null;
}

public sealed record OriginalMapPostEffect3ApplyUncontrolledSimplePlan(long FrameNumber,
    OriginalMapPostEffect3ApplyUncontrolledVariant Variant,
    IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> ScalarWrites,
    IReadOnlyList<OriginalMapPostEffect3ApplyUncontrolledPointerWrite> PointerWrites,
    bool NativeReturnPredicateFromLensPresetPresence,string SourceEvidence)
{
    public bool IsCompleteForExecution=>false;
    public string UnsupportedInputs=>"remaining raw Camera/sun/white-point writes; actual owned pointer representation; " +
        "selected descriptor58 branch; full native parameter/pass execution";
}

/// <summary>Simple conditional writers from recovered E71690 only. This class
/// preserves its LensPreset-presence AL return separately from writer success.
/// It does not decide whether descriptor58 should select E71690 or E70780.</summary>
public static class OriginalMapPostEffect3ApplyUncontrolledSimpleWriters
{
    public static OriginalMapPostEffect3ApplyUncontrolledSimplePlan PrepareFreshSource(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyUncontrolledCameraInputs cameraInputs,
        OriginalMapPostEffect3ApplyUncontrolledVariant explicitVariant,string actualVariantEvidence,
        OriginalMapPostEffect3ApplyUncontrolledSimpleInputs caller,
        OriginalMapPostEffect3ApplyUncontrolledLensPresetOwners lensPreset)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(cameraInputs);
        ArgumentNullException.ThrowIfNull(caller);ArgumentNullException.ThrowIfNull(lensPreset);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualVariantEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.CallerEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(lensPreset.LookupEvidence);
        if(explicitVariant!=OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE71690)
            throw new NotSupportedException("Simple writer recipe admits explicitly selected recovered E71690 only.");
        cameraInputs.ValidateSameCamera(camera);
        if(camera.ExecutableLocale!="TC"||caller.FrameNumber!=camera.FrameNumber||lensPreset.FrameNumber!=camera.FrameNumber||
            !float.IsFinite(caller.ActualGammaValueArgument)||!float.IsFinite(caller.ActualDeltaTimeArgument))
            throw new InvalidDataException("Simple uncontrolled writers require the same TC owned frame and finite actual caller arguments.");
        foreach(var target in new[]{lensPreset.SplitOwner,lensPreset.IntermediateOwner})
            if(target is not null&&(target.FrameNumber!=camera.FrameNumber||string.IsNullOrWhiteSpace(target.OwnerIdentity)))
                throw new InvalidDataException("LensPreset pointer target has no owned identity or belongs to another frame.");
        if(lensPreset.ActualCamera1810Present)
        {
            if(lensPreset.SplitOwner is null||lensPreset.IntermediateOwner is null)
                throw new InvalidDataException("Present LensPreset requires both actual source pointer targets.");
        }
        else if(lensPreset.SplitOwner is not null||lensPreset.IntermediateOwner is not null)
            throw new InvalidDataException("Absent source LensPreset cannot retain nonnull pointer targets.");
        string evidence="TC:E71690;E71836..E725D7;explicit-owned-caller;"+actualVariantEvidence+";"+
            caller.CallerEvidence+";"+lensPreset.LookupEvidence+";"+cameraInputs.SourceEvidence;
        var writes=new List<OriginalMapPostEffect3ApplyValueWrite>();
        void Word(string name,uint at,uint word)
        {byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(bytes,word);writes.Add(new(name,at,0xE71690,bytes,evidence));}
        void F(string name,uint at,float value)=>Word(name,at,BitConverter.SingleToUInt32Bits(value));
        uint CameraWord(int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(cameraInputs.ReadCameraBytes(camera,offset,4).Span);
        Word("FXAAEnable",0xE7186B,caller.ActualFxaaModeArgument==1?1u:0u);
        F("DeltaTime",0xE7210B,caller.ActualDeltaTimeArgument);
        Word("GammaEnable",0xE72137,caller.ActualGammaEnableWord1A8C934);
        F("GammaValue",0xE72163,caller.ActualGammaValueArgument);
        bool auto=((caller.OptionalAuxFlags8??0)&(1u<<25))!=0||(camera.SourceSpecialRectangleFlag1510&1)!=0;
        // Unlike controller E22440's +1/-1 sentinel, this native writer is0/1.
        Word("AutoExposureEnable",0xE723F5,auto?1u:0u);
        if(!auto)Word("ExposureValue",0xE72427,CameraWord(0x170));
        Word("ReverseZ",0xE724E6,(CameraWord(0x158)>>1)&1);
        Word("LowLuminanceBloomingFix",0xE7251A,CameraWord(0x1368));
        Word("DofEnable",0xE72551,CameraWord(0x1320)&1);
        Word("MotionBlurEnable",0xE7258F,cameraInputs.Float(0x130C)>0?1u:0u);
        Word("LensFlareEnable",0xE725D2,1);
        var pointers=new OriginalMapPostEffect3ApplyUncontrolledPointerWrite[]{
            new("LensPresetSplit",lensPreset.SplitOwner,0xE72483,evidence+";Camera1810.Data20.Cpu08"),
            new("LensPresetIntermediate",lensPreset.IntermediateOwner,0xE724AD,evidence+";Camera15CC-address")};
        return new(camera.FrameNumber,explicitVariant,Array.AsReadOnly(writes.ToArray()),Array.AsReadOnly(pointers),
            lensPreset.ActualCamera1810Present,evidence+";E725D7-return-r15b=LensPreset-presence");
    }
}
