using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Actual absence of the context/ViewMask-filtered light lookup.
/// This is supplied by the source lookup owner, not inferred from a missing
/// archive key, empty scalar bank or disabled LightShaft option.</summary>
public sealed record MapPostEffect3ApplyUncontrolledAbsentLightInputs(
    long FrameNumber,string ActualLookupOwnerIdentity,bool ActualLightShaftArgument,string LookupEvidence);

/// <summary>The absent-light branch writes seven keys, leaves SunProjPos
/// untouched and uses the original LSGaussBlur constant7. It does not synthesize
/// the present-light projection/trigonometry path.</summary>
public static class MapPostEffect3ApplyUncontrolledAbsentLightWriters
{
    public static IReadOnlyList<MapPostEffect3ApplyValueWrite> Prepare(
        MapPostEffect3ApplyPreparationSelection selection,
        MapPostEffect3ApplyUncontrolledAbsentLightInputs input)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ActualLookupOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.LookupEvidence);
        if(input.FrameNumber<=0)throw new InvalidDataException("Absent-light preparation requires an owned frame.");
        if(selection.Variant!=MapPostEffect3ApplyUncontrolledVariant.LensPresetCameraFields)
            throw new NotSupportedException("Absent-light writer recipe requires the LensPreset camera field route.");
        string evidence=selection.SourceEvidence+";absent-current-light;"+
            "stack38/SunScale=0;stack90/SunColor=0;IntoSunLight=0;stack44=7;stack34=0;"+
            input.ActualLookupOwnerIdentity+";"+input.LookupEvidence;
        MapPostEffect3ApplyValueWrite W(string name,MapPostEffect3WriteStage stage,int length,uint value=0)
        {byte[] bytes=new byte[length];BinaryPrimitives.WriteUInt32LittleEndian(bytes,value);return new(name,stage,bytes,evidence);}
        // These fallback writes run in the Light phase before LSLarge.
        // Preserve their branch order when combining camera scalar writes.
        return Array.AsReadOnly(new[]{W("WhitePoint",MapPostEffect3WriteStage.LightWhitePoint,4),W("IntoSunLight",MapPostEffect3WriteStage.LightIntoSunLight,4),
            W("SunScale",MapPostEffect3WriteStage.LightSunScale,8),W("SunColor",MapPostEffect3WriteStage.LightSunColor,12),
            W("LightShaftEnable",MapPostEffect3WriteStage.LightLightShaftEnable,4,input.ActualLightShaftArgument?1u:0u),
            W("LSGaussBlur",MapPostEffect3WriteStage.LightLSGaussBlur,4,7),W("LSStepLength",MapPostEffect3WriteStage.LightLSStepLength,4)});
    }
}
