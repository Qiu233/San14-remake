using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Explicit caller ABI values. OptionalAuxFlags8 null means
/// the context+240 Aux lookup is known absent. Gamma enable is the actual
/// current native word; no initial/current global is inferred here.</summary>
public sealed record MapPostEffect3ApplyUncontrolledSimpleInputs(long FrameNumber,
    int ActualFxaaModeArgument,float ActualGammaValueArgument,float ActualDeltaTimeArgument,
    uint ActualGammaEnabledWord,uint? OptionalAuxFlags8,string CallerEvidence);

/// <summary>.. source identities. A present Camera1810 supplies
/// Lens CPU+8 and Camera15CC address owners. These are pointer-valued scalar
/// parameters, not texture bindings or fake integer pointers.</summary>
public sealed record MapPostEffect3ApplyUncontrolledLensPresetOwners(long FrameNumber,
    bool ActualCamera1810Present,MapPostEffect3ApplyView? SplitOwner,
    MapPostEffect3ApplyView? IntermediateOwner,string LookupEvidence);

public sealed record MapPostEffect3ApplyUncontrolledPointerWrite(string Name,
    MapPostEffect3ApplyView? OwnedPointerTarget,string SourceEvidence)
{
    public int NativeByteCount=>8;
    public bool IsNativeNullPointer=>OwnedPointerTarget is null;
}

public sealed record MapPostEffect3ApplyUncontrolledSimplePlan(long FrameNumber,
    MapPostEffect3ApplyUncontrolledVariant Variant,
    IReadOnlyList<MapPostEffect3ApplyValueWrite> ScalarWrites,
    IReadOnlyList<MapPostEffect3ApplyUncontrolledPointerWrite> PointerWrites,
    bool NativeReturnPredicateFromLensPresetPresence,string SourceEvidence)
{
    public bool IsCompleteForExecution=>false;
    public string UnsupportedInputs=>"remaining raw Camera/sun/white-point writes; actual owned pointer representation; " +
        "selected descriptor58 branch; full native parameter/pass execution";
}

/// <summary>Simple conditional writers from recovered only. This class
/// preserves its LensPreset-presence AL return separately from writer success.
/// It does not decide whether descriptor58 should select or .</summary>
public static class MapPostEffect3ApplyUncontrolledSimpleWriters
{
    public static MapPostEffect3ApplyUncontrolledSimplePlan PrepareFreshSource(
        MapCameraConstructionPacket camera,MapPostEffect3ApplyUncontrolledCameraInputs cameraInputs,
        MapPostEffect3ApplyUncontrolledVariant explicitVariant,string actualVariantEvidence,
        MapPostEffect3ApplyUncontrolledSimpleInputs caller,
        MapPostEffect3ApplyUncontrolledLensPresetOwners lensPreset)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(cameraInputs);
        ArgumentNullException.ThrowIfNull(caller);ArgumentNullException.ThrowIfNull(lensPreset);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualVariantEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(caller.CallerEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(lensPreset.LookupEvidence);
        if(explicitVariant!=MapPostEffect3ApplyUncontrolledVariant.LensPresetCameraFields)
            throw new NotSupportedException("Simple writer recipe admits explicitly selected recovered  only");
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
        string evidence="TC;explicit-owned-caller"+actualVariantEvidence+";"+
            caller.CallerEvidence+";"+lensPreset.LookupEvidence+";"+cameraInputs.SourceEvidence;
        var writes=new List<MapPostEffect3ApplyValueWrite>();
        void Word(string name,MapPostEffect3WriteStage stage,uint word)
        {byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32LittleEndian(bytes,word);writes.Add(new(name,stage,bytes,evidence));}
        void F(string name,MapPostEffect3WriteStage stage,float value)=>Word(name,stage,BitConverter.SingleToUInt32Bits(value));
        uint CameraWord(int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(cameraInputs.ReadCameraBytes(camera,offset,4).Span);
        Word("FXAAEnable",MapPostEffect3WriteStage.NoControllerFXAAEnable,caller.ActualFxaaModeArgument==1?1u:0u);
        F("DeltaTime",MapPostEffect3WriteStage.NoControllerDeltaTime,caller.ActualDeltaTimeArgument);
        Word("GammaEnable",MapPostEffect3WriteStage.NoControllerGammaEnable,caller.ActualGammaEnabledWord);
        F("GammaValue",MapPostEffect3WriteStage.NoControllerGammaValue,caller.ActualGammaValueArgument);
        bool auto=((caller.OptionalAuxFlags8??0)&(1u<<25))!=0||(camera.SourceSpecialRectangleFlag1510&1)!=0;
        // Unlike controller 's +1/-1 sentinel, this native writer is0/1.
        Word("AutoExposureEnable",MapPostEffect3WriteStage.NoControllerAutoExposureEnable,auto?1u:0u);
        if(!auto)Word("ExposureValue",MapPostEffect3WriteStage.NoControllerExposureValue,CameraWord(0x170));
        Word("ReverseZ",MapPostEffect3WriteStage.NoControllerReverseZ,(CameraWord(0x158)>>1)&1);
        Word("LowLuminanceBloomingFix",MapPostEffect3WriteStage.NoControllerLowLuminanceBloomingFix,CameraWord(0x1368));
        Word("DofEnable",MapPostEffect3WriteStage.NoControllerDofEnable,CameraWord(0x1320)&1);
        Word("MotionBlurEnable",MapPostEffect3WriteStage.NoControllerMotionBlurEnable,cameraInputs.Float(0x130C)>0?1u:0u);
        Word("LensFlareEnable",MapPostEffect3WriteStage.NoControllerLensFlareEnable,1);
        var pointers=new MapPostEffect3ApplyUncontrolledPointerWrite[]{
            new("LensPresetSplit",lensPreset.SplitOwner,evidence+";Camera1810.Data20.Cpu08"),
            new("LensPresetIntermediate",lensPreset.IntermediateOwner,evidence+";Camera15CC-address")};
        return new(camera.FrameNumber,explicitVariant,Array.AsReadOnly(writes.ToArray()),Array.AsReadOnly(pointers),
            lensPreset.ActualCamera1810Present,evidence+"-return-r15b=LensPreset-presence");
    }
}
