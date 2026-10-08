using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Actual absence of D586B0's context/ViewMask-filtered light lookup.
/// This is supplied by the source lookup owner, not inferred from a missing
/// archive key, empty scalar bank or disabled LightShaft option.</summary>
public sealed record OriginalMapPostEffect3ApplyUncontrolledAbsentLightInputs(
    long FrameNumber,string ActualLookupOwnerIdentity,bool ActualLightShaftArgument,string LookupEvidence);

/// <summary>The E71690 absent-light branch writes seven keys, leaves SunProjPos
/// untouched and uses the original LSGaussBlur constant7. It does not synthesize
/// the present-light projection/trigonometry path.</summary>
public static class OriginalMapPostEffect3ApplyUncontrolledAbsentLightWriters
{
    public static IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> Prepare(
        OriginalMapPostEffect3ApplyPreparationSelection selection,
        OriginalMapPostEffect3ApplyUncontrolledAbsentLightInputs input)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ActualLookupOwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.LookupEvidence);
        if(input.FrameNumber<=0)throw new InvalidDataException("Absent-light preparation requires an owned frame.");
        if(selection.Variant!=OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE71690)
            throw new NotSupportedException("Absent-light writer recipe requires the actual E71690 descriptor route.");
        string evidence=selection.SourceEvidence+";TC:E71A28..E71A85-D586B0-null;E72604..E7262C;"+
            "stack38/SunScale=0;stack90/SunColor=0;IntoSunLight=0;stack44=7;stack34=0;"+
            input.ActualLookupOwnerIdentity+";"+input.LookupEvidence;
        OriginalMapPostEffect3ApplyValueWrite W(string name,uint at,int length,uint value=0)
        {byte[] bytes=new byte[length];BinaryPrimitives.WriteUInt32LittleEndian(bytes,value);return new(name,at,0xE71690,bytes,evidence);}
        // Source execution order crosses a late-address fallback block before
        // rejoining E71E3F. Sorting this collection by native RVA would be wrong.
        return Array.AsReadOnly(new[]{W("WhitePoint",0xE72627,4),W("IntoSunLight",0xE71E95,4),
            W("SunScale",0xE71EC8,8),W("SunColor",0xE71EF1,12),
            W("LightShaftEnable",0xE71F2B,4,input.ActualLightShaftArgument?1u:0u),
            W("LSGaussBlur",0xE71F55,4,7),W("LSStepLength",0xE71F7F,4)});
    }
}
