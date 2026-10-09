using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Source-loaded successful StaticSky2AtmoTable dependencies plus the
/// exact scalar math revisions. This is an owned source producer, independent
/// of an original process's current asynchronous wrapper state.</summary>
public sealed class OriginalSkySeasonTransmittanceSource
{
    private OriginalSkySeasonTransmittanceSource(LoadedOriginalSkyAtmoTableSources tables,
        LoadedOriginalNativeSkyAtanSource atan,LoadedOriginalNativeCameraTangentSource tangent,
        OriginalSkyWeatherNativePowerSource power)
    {Tables=tables;Atan=atan;Tangent=tangent;Power=power;}
    public LoadedOriginalSkyAtmoTableSources Tables{get;}
    public LoadedOriginalNativeSkyAtanSource Atan{get;}
    public LoadedOriginalNativeCameraTangentSource Tangent{get;}
    public OriginalSkyWeatherNativePowerSource Power{get;}
    public string SourceProvenance=>Tables.SourceEvidence+";"+Atan.SourceProvenance+
        ";"+Tangent.ResourceUri+"@"+Tangent.RuleVersion+";"+Power.SourceProvenance+
        ";owned-source-successful-raw-dependency-publication";
    public static OriginalSkySeasonTransmittanceSource Load(OriginalGameRepository repository,
        OriginalMapSeasonCameraPackage package,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(OriginalSkyAtmoTableResources.LoadForSeason(repository,package),
            OriginalNativeSkyAtanSource.Load(repository.SourceDirectory,locale),
            OriginalNativeCameraTangentSource.Load(repository,locale),
            OriginalSkyWeatherNativePowerSource.Load(repository,locale));
    }
}

public sealed record OriginalSkySeasonTransmittanceSample(Vector4 Value,float? HeightCoordinate,
    float? AngleCoordinate,bool AboveAtmosphereWhite,string SourceProvenance);
public sealed record OriginalSkySeasonTransmittedColors(OriginalSkylightBaseColors OriginalBaseColors,
    OriginalSkylightBaseColors TransmittedColors,OriginalSkySeasonTransmittanceSample Sun,
    OriginalSkySeasonTransmittanceSample Moon,string SourceProvenance);

/// <summary>Exact C5C1A0 coordinate generation, C99C00 original half4 sampling,
/// and C458DF..C45983 RGB products. Later Moon temperature, weather scattering,
/// ambient and transition composition are separate Camera producer stages.</summary>
public static class OriginalSkySeasonTransmittance
{
    public static OriginalSkySeasonTransmittanceSample Sample(OriginalSkySeasonTransmittanceSource source,
        Vector4 sourceDirection,float sourceHeightMetres,OriginalNativeCameraTangentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.Atan.Locale!=source.Power.Locale||source.Tangent.Language!=source.Power.Locale)
            throw new InvalidDataException("Transmittance native trigonometry and power belong to different source executables.");
        foreach(float value in new[]{sourceDirection.X,sourceDirection.Y,sourceDirection.Z,sourceHeightMetres,source.Tables.AtmoTopHeight})
            if(!float.IsFinite(value))throw new InvalidDataException("Original Sky transmittance requires finite source geometry.");
        if(dispatch is not (OriginalNativeCameraTangentDispatch.Sse or OriginalNativeCameraTangentDispatch.AvxFma)||
            source.Tables.AtmoTopHeight<=0)
            throw new InvalidDataException("Original Sky transmittance requires an explicit source math dispatch and positive source atmosphere height.");
        string evidence=source.SourceProvenance+";C5C1A0-height-X/angle-Y;dispatch="+dispatch;
        if(source.Tables.AtmoTopHeight<sourceHeightMetres)
            return new(Vector4.One,null,null,true,evidence+";source-height-above-top-white-branch");
        float baseHeight=Clamp(D(sourceHeightMetres,source.Tables.AtmoTopHeight));
        // C5C204 loads binary32 .5 and C5C20C calls float pow101EC10.
        // Its SSE entry converts the two inputs to binary64 and rounds the
        // return back to binary32. Reuse that exact positive finite port.
        // Original SSE/AVX both return exact endpoints; the general AVX/FMA
        // power branch remains separate even though its tangent is admitted.
        float height=baseHeight switch
        {
            0=>0,1=>1,
            _ when dispatch==OriginalNativeCameraTangentDispatch.Sse=>
                OriginalSkyWeatherNativePower.PowSse(baseHeight,.5f,source.Power),
            _=>throw new NotSupportedException("Original Sky transmittance general AVX/FMA power is not recovered; select an explicitly owned SSE dispatch.")
        };
        float tangent=OriginalNativeCameraTangent.Tangent1020A90(F(0x3FB16873),source.Tangent,dispatch);
        float mu=Max(S(F(0x3B23D70A),sourceDirection.Y),F(0xBE4A3D71));
        float angle=OriginalNativeSkyAtan.Atan1032034(M(tangent,mu),source.Atan);
        angle=M(A(D(angle,F(0x3F8CCCCD)),F(0x3F3D70A4)),.5f);
        Vector4 sample=source.Tables.SampleGrid(Clamp(height),Clamp(angle));
        Vector4 result=new(Max(0,sample.X),Max(0,sample.Y),Max(0,sample.Z),Max(0,sample.W));
        return new(result,Clamp(height),Clamp(angle),false,evidence+";native-half4-lower-clamp");
    }

    public static OriginalSkySeasonTransmittedColors PrepareBaseColors(
        OriginalCameraLightingController selectedController,OriginalSkylightParameters parameters,
        OriginalSkylightDirections directions,OriginalSkySeasonTransmittanceSource source,
        OriginalNativeCameraTangentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(selectedController);ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(directions);ArgumentNullException.ThrowIfNull(source);
        if(selectedController.DeclaredTransmittanceTable!=OriginalSkyAtmoTableResources.ObjectId)
            throw new InvalidDataException("Applied seasonal Camera and its actual source transmittance table differ.");
        var colors=OriginalSkylightColorMath.Prepare(selectedController.SunHueSaturation,selectedController.MoonHueSaturation);
        float height=M(parameters.Altitude,1000f);
        var sun=Sample(source,directions.Sun,height,dispatch);
        var moon=Sample(source,directions.Moon,height,dispatch);
        Vector4 Product(Vector4 color,Vector4 table)=>new(M(color.X,table.X),M(color.Y,table.Y),M(color.Z,table.Z),color.W);
        return new(colors,new(Product(colors.Sun,sun.Value),Product(colors.Moon,moon.Value)),sun,moon,
            source.SourceProvenance+";C458DF-C45983-two-native-source-transmittance-RGB-products;later-V418-composition-required");
    }
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static float Clamp(float value)=>value>=1?1:Max(0,value);
    private static float Max(float a,float b)=>a>b?a:b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
