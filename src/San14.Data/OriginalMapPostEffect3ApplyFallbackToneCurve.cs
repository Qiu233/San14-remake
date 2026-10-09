using System.Buffers.Binary;

namespace San14.Data;

public sealed record OriginalMapPostEffect3ApplyFallbackToneCurvePlan(float ActualFifthFloatArgument,
    int ActualAuxToneType3A4,uint ActualAuxFlags8,string SourceEvidence);

/// <summary> initializes XMM14 to0, then the selected current Aux's
/// flags8/toneType3A4 select exact .1/.2/.3 source literals. supplies
/// this float as 's fifth argument; it is not the integer tone type.</summary>
public static class OriginalMapPostEffect3ApplyFallbackToneCurve
{
    public static OriginalMapPostEffect3ApplyFallbackToneCurvePlan ResolveCurrentAux(OriginalMapExposureViewAux actualAux)
    {
        ArgumentNullException.ThrowIfNull(actualAux);ArgumentException.ThrowIfNullOrWhiteSpace(actualAux.SourceProvenance);
        int mode=actualAux.ToneType3A4;float value=0;
        if((actualAux.Flags8&(1u<<23))!=0&&mode>=3)value=BitConverter.UInt32BitsToSingle(0x3E99999A);
        else value=mode switch{1=>BitConverter.UInt32BitsToSingle(0x3DCCCCCD),
            2=>BitConverter.UInt32BitsToSingle(0x3E4CCCCD),3=>BitConverter.UInt32BitsToSingle(0x3E99999A),_=>0};
        return new(value,mode,actualAux.Flags8,";XMM14-zero;-current-Aux3A4;-flags8.bit23"+
            "-literal3E99999A;-literal3DCCCCCD;-literal3E4CCCCD;-arg5"+actualAux.SourceProvenance);
    }
    /// <summary>Read the actual prepared prefix, including later writers.
    /// Fresh source preparation writes FFFFFFFF at embedded14FC,
    /// whole Camera1CC. Native default constructor absence is a separate case.</summary>
    public static uint ReadSelectedCamera1CC(OriginalMapCameraConstructionPacket selectedCamera)
    {
        ArgumentNullException.ThrowIfNull(selectedCamera);
        if(selectedCamera.FrameNumber<=0)throw new InvalidDataException("Tone override requires an owned selected Camera.");
        return BinaryPrimitives.ReadUInt32LittleEndian(selectedCamera.Prefix.Span.Slice(0x1CC,4));
    }
}
