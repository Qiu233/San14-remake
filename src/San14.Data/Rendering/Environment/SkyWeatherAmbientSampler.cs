using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record SkyWeatherAmbientSample(Vector4 Value,float? HeightCoordinate,
    float? AngleCoordinate,bool AboveAtmosphereWhite,string SourceProvenance);

/// <summary>Actual C999C0 Ambient2D producer. Its coordinates share the native
/// transmittance formula but read the second original raw table and multiply
/// all four lanes by the native binary32 1e-5 literal after sampling.</summary>
public static class SkyWeatherAmbientSampler
{
    public static SkyWeatherAmbientSample Sample(SkySeasonTransmittanceSource tableSource,
        SkyWeatherNativePowerSource powerSource,Vector4 currentDirection,
        float currentEffectiveHeight,NativeCameraTangentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(tableSource);ArgumentNullException.ThrowIfNull(powerSource);
        if(dispatch!=NativeCameraTangentDispatch.Sse)
            throw new NotSupportedException("Weather Ambient2D currently ports actual SSE power only; AVX/FMA is a separate source branch.");
        if(tableSource.Atan.Locale!=powerSource.Locale||
            tableSource.Tangent.Language!=powerSource.Locale)
            throw new InvalidDataException("Ambient2D table, native trigonometry and power belong to different source executables.");
        foreach(float value in new[]{currentDirection.X,currentDirection.Y,currentDirection.Z,currentEffectiveHeight})
            if(!float.IsFinite(value))throw new InvalidDataException("Ambient2D requires finite current source geometry.");
        float top=tableSource.Tables.AtmoTopHeight;
        if(top<=0)throw new InvalidDataException("Ambient2D requires positive source atmosphere top.");
        string provenance=tableSource.SourceProvenance+";"+powerSource.SourceProvenance+
            ";C999C0/second-raw-Ambient2D;currentHeight="+BitConverter.SingleToUInt32Bits(currentEffectiveHeight).ToString("X8");
        if(top<currentEffectiveHeight)return new(Vector4.One,null,null,true,provenance+";above-atmosphere-white/no-table-multiplier");
        float height=SkyWeatherNativePower.PowSse(Clamp(D(currentEffectiveHeight,top)),.5f,powerSource);
        float angle=AngleCoordinate(tableSource,currentDirection,dispatch);
        height=Clamp(height);
        Vector4 source=tableSource.Tables.SampleGrid(height,angle,SkyAtmoRawTable.Ambient2D);
        float scale=F(0x3727C5AC);
        Vector4 output=new(Max(0,M(source.X,scale)),Max(0,M(source.Y,scale)),
            Max(0,M(source.Z,scale)),Max(0,M(source.W,scale)));
        return new(output,height,angle,false,provenance+";original-four-lane-1e-5-and-lower-clamp");
    }
    public static float EffectiveHeight(float selectedAltitudeKilometres,float currentMainEyeY)=>
        A(M(selectedAltitudeKilometres,1000),M(currentMainEyeY,F(0x3C23D70A)));
    /// <summary>The direction coordinate is independent of current height.
    /// Native initialization also needs it when the first camera is above the atmosphere.</summary>
    public static float AngleCoordinate(SkySeasonTransmittanceSource tableSource,
        Vector4 currentDirection,NativeCameraTangentDispatch dispatch)
    {
        float tangent=NativeCameraTangent.Tangent1020A90(F(0x3FB16873),tableSource.Tangent,dispatch);
        float mu=Max(S(F(0x3B23D70A),currentDirection.Y),F(0xBE4A3D71));
        float angle=NativeSkyAtan.Atan1032034(M(tangent,mu),tableSource.Atan);
        return Clamp(M(A(D(angle,F(0x3F8CCCCD)),F(0x3F3D70A4)),.5f));
    }
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static float Clamp(float value)=>value>=1?1:Max(0,value);
    private static float Max(float a,float b)=>a>b?a:b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
