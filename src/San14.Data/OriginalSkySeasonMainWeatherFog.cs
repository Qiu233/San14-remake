using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>An accepted Main/season source weather/Fog writer. Publishes the
/// computed Main22C saturation and semantic72-byte Fog coverage, retaining
/// the original allocation's unknown24-byte tail.</summary>
public sealed class OriginalSkySeasonMainWeatherFogFrame
{
    internal OriginalSkySeasonMainWeatherFogFrame(OriginalSkySeasonMainCameraRgbFrame camera,
        OriginalSkyWeatherKnownFogRows fog,OriginalSkyWeatherAmbientSample sun,
        OriginalSkyWeatherAmbientSample moon,float sourceWeather640,float sourceWeatherSaturation22C,string provenance)
    {AcceptedSeasonCamera=camera;Fog=fog;SunAmbient=sun;MoonAmbient=moon;
        SourceWeather640=sourceWeather640;SourceWeatherSaturation22C=sourceWeatherSaturation22C;SourceProvenance=provenance;}
    public OriginalSkySeasonMainCameraRgbFrame AcceptedSeasonCamera{get;}
    public long FrameNumber=>AcceptedSeasonCamera.Inputs.FrameNumber;
    public OriginalSkyWeatherKnownFogRows Fog{get;}
    public OriginalSkyWeatherAmbientSample SunAmbient{get;}
    public OriginalSkyWeatherAmbientSample MoonAmbient{get;}
    public float SourceWeather640{get;}
    public float SourceWeatherSaturation22C { get; }
    public string SourceProvenance{get;}
    public bool NativeWholeFog96IdentityPreserved=>false;
    public void RequireCurrent(OriginalSkySeasonMainCameraRgbFrame actual)
    {
        if(!ReferenceEquals(actual,AcceptedSeasonCamera))
            throw new InvalidDataException("Main weather/Fog publication belongs to another accepted source season frame.");
    }
    public byte[] ApplyKnownWritesToCurrentPreparedPrefix(OriginalSkySeasonMainCameraRgbFrame actual)
    {
        RequireCurrent(actual);
        byte[] prefix=actual.SunMoonRgb.Prefix.ToArray();
        // Original V418 C45C9C (paired SC writer) unconditionally publishes
        // the same XMM6 used by the Fog saturation adjustment. The later
        // embedded Main copy and Cloud inputD8 read this exact field.
        BinaryPrimitives.WriteSingleLittleEndian(prefix.AsSpan(0x22C,4),SourceWeatherSaturation22C);
        Fog.Known72.Span.CopyTo(prefix.AsSpan(0x340,72));return prefix;
    }
}

/// <summary>The source-created empty-axis Main package's actual V418 Fog
/// branch. Current weather comes from the explicitly selected database reset;
/// later dynamic weather/controller edits require their own actual writers.
/// It does not pretend to implement every unrelated V418 weather output.</summary>
public static class OriginalSkySeasonMainWeatherFog
{
    public static OriginalSkySeasonMainWeatherFogFrame PrepareSourceDatabaseResetFrame(
        OriginalSkySeasonMainCameraRgbFrame camera,OriginalSkyWeatherNativePowerSource power,
        OriginalNativeCameraTangentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(power);
        var source=camera.Inputs.Source;source.Selection.Validate();source.SceneClockSeed.Validate();
        if(!source.UsesSourceCreatedEmptyAxes||source.Selection.SecondPackage is not null||
            source.Selection.FirstPackage.Camera.ObjectId!=OriginalMapSeasonCameraApplication.RootCameraObjectId||
            camera.Inputs.CurrentColorTemperature21C!=6500||
            power.Locale!=camera.Inputs.MainFrame.Camera.ExecutableLocale)
            throw new NotSupportedException("Source-created Main weather/Fog requires the actual fresh database reset, Root package, native6500 and matched executable; later edited/interpolated weather is explicit.");
        if(dispatch!=OriginalNativeCameraTangentDispatch.Sse)
            throw new NotSupportedException("Current Main weather/Fog implements the actual SSE power dispatch only.");
        var package=OriginalSkySeasonPackageTextures.Read(source.Selection.FirstPackage).Package.Camera;
        float Field(int ordinal,int lane=0)
        {
            var prop=package.Properties.Single(x=>x.Index==ordinal);
            if(prop.TypeCode!=8||prop.ElementSize!=4||prop.RawValue.Length!=prop.ElementCount*4||
                lane>=prop.ElementCount)throw new InvalidDataException("Source Main weather property layout differs from its actual loader copy.");
            float value=BinaryPrimitives.ReadSingleLittleEndian(prop.RawValue.AsSpan(lane*4,4));
            if(!float.IsFinite(value))throw new InvalidDataException("Source Main weather property is nonfinite.");
            return value;
        }
        // C452A8/C45382 -> C45668: only Cloudy/Humidity enter current640.
        float cloud=Clamp(M(BinaryPrimitives.ReadSingleLittleEndian(camera.SunMoonRgb.Prefix.Span[0x1D0..]),Field(770)));
        float humidity=BinaryPrimitives.ReadSingleLittleEndian(camera.SunMoonRgb.Prefix.Span[0x1D8..]),low=Field(879),high=Field(880);
        float humidityWeight=high>low?Clamp(D(S(humidity,low),S(high,low))):humidity>=low?1:0;
        float current640=M(cloud,humidityWeight);
        float strength=Field(923),heightBlend=M(strength,Field(925));heightBlend=A(heightBlend,S(1,strength));
        float opacity=OriginalSkyWeatherNativePower.PowSse(Field(924),strength,power);
        float hStart=Field(896),hEnd=Field(897),weather=humidity;
        float weatherWeight=hEnd==hStart?(weather>hStart?1:0):Clamp(D(S(weather,hStart),S(hEnd,hStart)));
        float complement=S(1,weatherWeight);
        float rayleighScale=A(M(weatherWeight,Field(899)),M(complement,Field(898)));
        float heightDenominator=M(M(heightBlend,Field(920)),100);
        float eyeY=BinaryPrimitives.ReadSingleLittleEndian(camera.Inputs.MainFrame.Camera.Prefix.Span.Slice(0x14,4));
        float raised=OriginalSkyWeatherNativePower.PowSse(D(eyeY,heightDenominator),4,power);
        float density=A(M(Max(raised,1),strength),S(1,strength));density=M(density,opacity);
        float mieScale=A(M(weatherWeight,Field(901)),M(complement,Field(900)));density=M(density,mieScale);
        var table=source.Transmittance.Tables;
        float maximum=Max(table.MieExtinction.X,Max(table.MieExtinction.Z,table.MieExtinction.Y));
        Vector4 mie=new(M(M(density,Field(926,0)),D(table.MieExtinction.X,maximum)),
            M(M(density,Field(926,1)),D(table.MieExtinction.Y,maximum)),
            M(M(density,Field(926,2)),D(table.MieExtinction.Z,maximum)),0);
        Vector4 rayleigh=new(M(M(Field(927,0),table.RayleighExtinction.X),rayleighScale),
            M(M(Field(927,1),table.RayleighExtinction.Y),rayleighScale),
            M(M(Field(927,2),table.RayleighExtinction.Z),rayleighScale),0);
        float saturationStart=Field(973),saturationEnd=Field(974);
        float saturation=saturationStart==saturationEnd?(current640>saturationEnd?0:1):
            S(1,Clamp(D(S(current640,saturationStart),S(saturationEnd,saturationStart))));
        Vector4 Adjust(Vector4 color)
        {
            if(saturation>=1)return color;
            var hsv=OriginalSkylightColorMath.RgbToHsv(color);hsv.Y=M(hsv.Y,saturation);
            return OriginalSkylightColorMath.HsvToRgb(hsv);
        }
        rayleigh=Adjust(rayleigh);
        float height=OriginalSkyWeatherAmbientSampler.EffectiveHeight(camera.Inputs.Phase.Parameters.Altitude,eyeY);
        var sun=OriginalSkyWeatherAmbientSampler.Sample(source.Transmittance,power,camera.SunMoonRgb.Directions.Sun,height,dispatch);
        var moon=OriginalSkyWeatherAmbientSampler.Sample(source.Transmittance,power,camera.SunMoonRgb.Directions.Moon,height,dispatch);
        Vector4 sunAmbient=Adjust(sun.Value),moonAmbient=Adjust(moon.Value);
        float ReadRgb(int at)=>BinaryPrimitives.ReadSingleLittleEndian(camera.SunMoonRgb.Prefix.Span.Slice(at,4));
        Vector4 background=new(M(A(M(moonAmbient.X,ReadRgb(0x308)),M(sunAmbient.X,ReadRgb(0x2C4))),F(0x3E99999A)),
            M(A(M(moonAmbient.Y,ReadRgb(0x30C)),M(sunAmbient.Y,ReadRgb(0x2C8))),F(0x3E99999A)),
            M(A(M(moonAmbient.Z,ReadRgb(0x310)),M(sunAmbient.Z,ReadRgb(0x2CC))),F(0x3E99999A)),0);
        // Original source CPU DB4 -> prepared1054 is the loaded anisotropy.
        var fog=OriginalSkyWeatherFogAssembler.Generate(new(background,rayleigh,mie,
            M(Field(921),100),M(M(heightBlend,Field(920)),100),table.PlanetRadius,1,
            M(S(1,M(strength,F(0x3E99999A))),Field(595)),M(Field(919),100),
            A(M(Field(914),Field(915)),S(1,Field(914)))));
        return new(camera,fog,sun,moon,current640,saturation,camera.SourceProvenance+";"+power.SourceProvenance+
            ";same-Main-V418-Root-source-database-reset-weather;computed-saturation-Main22C-write;actual72-semantic-writes;"+
            "Fog48..5F-retained-unknown;unrelated-V418-weather-outputs-not-published");
    }
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static float Clamp(float x)=>x>=1?1:Max(0,x);
    private static float Max(float a,float b)=>a>b?a:b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
