using System.Numerics;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Independent weather coverage and optional table-dependent Fog writes.
/// Without a table the RGB prefix retains saturation and Fog bytes other than
/// the cleared mode; no sampled ambient or Fog result is manufactured.</summary>
public sealed class SkySeasonMainWeatherFogFrame
{
    private readonly SkyWeatherAmbientSample? sunAmbient,moonAmbient;
    internal SkySeasonMainWeatherFogFrame(SkySeasonMainCameraRgbFrame camera,
        SkyWeatherKnownFogRows fog,SkyWeatherAmbientSample sun,
        SkyWeatherAmbientSample moon,float coverage,float saturation,string provenance)
    {AcceptedSeasonCamera=camera;FogWrites=new(fog,coverage,saturation);sunAmbient=sun;moonAmbient=moon;
        SourceWeather640=coverage;SourceProvenance=provenance;}
    internal SkySeasonMainWeatherFogFrame(SkySeasonMainCameraRgbFrame camera,float coverage,string provenance)
    {AcceptedSeasonCamera=camera;SourceWeather640=coverage;SourceProvenance=provenance;}
    public SkySeasonMainCameraRgbFrame AcceptedSeasonCamera{get;}
    public long FrameNumber=>AcceptedSeasonCamera.Inputs.FrameNumber;
    public SkyWeatherResult? FogWrites { get; }
    public SkyWeatherResult Weather => FogWrites ??
        throw new NotSupportedException("Table-dependent weather has no output for an absent atmosphere.");
    public SkyWeatherKnownFogRows Fog=>Weather.Fog;
    public SkyWeatherAmbientSample SunAmbient=>sunAmbient ??
        throw new NotSupportedException("Absent atmosphere has no sampled Sun ambient output.");
    public SkyWeatherAmbientSample MoonAmbient=>moonAmbient ??
        throw new NotSupportedException("Absent atmosphere has no sampled Moon ambient output.");
    public float SourceWeather640{get;}
    public float SourceWeatherSaturation22C=>FogWrites?.Saturation ??
        BinaryPrimitives.ReadSingleLittleEndian(AcceptedSeasonCamera.SunMoonRgb.Prefix.Span.Slice(0x22C,4));
    public string SourceProvenance{get;}
    public bool NativeWholeFog96IdentityPreserved=>false;
    public void RequireCurrent(SkySeasonMainCameraRgbFrame actual)
    {
        if(!ReferenceEquals(actual,AcceptedSeasonCamera))
            throw new InvalidDataException("Main weather/Fog publication belongs to another accepted source season frame.");
    }
    public byte[] ApplyKnownWritesToCurrentPreparedPrefix(SkySeasonMainCameraRgbFrame actual)
    {
        RequireCurrent(actual);
        return FogWrites is { } writes ? SkyWeatherFogEncoding.ApplyToCamera(actual.SunMoonRgb.Prefix.Span,writes)
            : actual.SunMoonRgb.Prefix.ToArray();
    }
}

/// <summary>The source-created empty-axis Main package's actual V418 Fog
/// branch. Current weather comes from the explicitly selected database reset;
/// later dynamic weather/controller edits require their own actual writers.
/// It does not pretend to implement every unrelated V418 weather output.</summary>
public static class SkySeasonMainWeatherFog
{
    public static SkySeasonMainWeatherFogFrame PrepareSourceDatabaseResetFrame(
        SkySeasonMainCameraRgbFrame camera,SkyWeatherNativePowerSource power,
        NativeCameraTangentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(power);
        var source=camera.Inputs.Source;source.Selection.Validate();
        if(!source.UsesSourceCreatedEmptyAxes||source.Selection.SecondPackage is not null||
            source.Selection.FirstPackage.Camera.ObjectId!=MapSeasonCameraApplication.RootCameraObjectId||

            power.Locale!=camera.Inputs.MainFrame.Camera.ExecutableLocale)
            throw new NotSupportedException("Source-created Main weather/Fog requires the actual fresh database reset, Root package, native6500 and matched executable; later edited/interpolated weather is explicit.");
        if(dispatch!=NativeCameraTangentDispatch.Sse)
            throw new NotSupportedException("Current Main weather/Fog implements the actual SSE power dispatch only.");
        var profile=source.WeatherProfile;
        var inputs=camera.Inputs.Weather;
        // C452A8/C45382 -> C45668: only Cloudy/Humidity enter current640.
        float cloud=Clamp(M(inputs.Climate.Cloudiness,profile.CloudinessScale));
        float humidity=inputs.Climate.Humidity,low=profile.CloudHumidityLow,high=profile.CloudHumidityHigh;
        float humidityWeight=high>low?Clamp(D(S(humidity,low),S(high,low))):humidity>=low?1:0;
        float current640=M(cloud,humidityWeight);
        if(source.Transmittance.Tables is null)
            return new(camera,current640,camera.SourceProvenance+";independent-weather-coverage;absent-atmosphere-retains-saturation-and-Fog-except-mode");
        if(camera.Inputs.CurrentColorTemperature21C!=6500)
            throw new NotSupportedException("Present-table weather requires the supported 6500K ambient branch.");
        float strength=profile.FogStrength,heightBlend=M(strength,profile.FogHeightBlend);heightBlend=A(heightBlend,S(1,strength));
        float opacity=SkyWeatherNativePower.PowSse(profile.FogOpacity,strength,power);
        float hStart=profile.AtmosphereHumidityLow,hEnd=profile.AtmosphereHumidityHigh,weather=humidity;
        float weatherWeight=hEnd==hStart?(weather>hStart?1:0):Clamp(D(S(weather,hStart),S(hEnd,hStart)));
        float complement=S(1,weatherWeight);
        float rayleighScale=A(M(weatherWeight,profile.WetRayleighScale),M(complement,profile.DryRayleighScale));
        float heightDenominator=M(M(heightBlend,profile.FogDepthScale),100);
        float eyeY=inputs.EyeHeight;
        float raised=SkyWeatherNativePower.PowSse(D(eyeY,heightDenominator),4,power);
        float density=A(M(Max(raised,1),strength),S(1,strength));density=M(density,opacity);
        float mieScale=A(M(weatherWeight,profile.WetMieScale),M(complement,profile.DryMieScale));density=M(density,mieScale);
        var table=source.Transmittance.RequireTables();
        float maximum=Max(table.MieExtinction.X,Max(table.MieExtinction.Z,table.MieExtinction.Y));
        Vector4 mie=new(M(M(density,profile.MieTint.X),D(table.MieExtinction.X,maximum)),
            M(M(density,profile.MieTint.Y),D(table.MieExtinction.Y,maximum)),
            M(M(density,profile.MieTint.Z),D(table.MieExtinction.Z,maximum)),0);
        Vector4 rayleigh=new(M(M(profile.RayleighTint.X,table.RayleighExtinction.X),rayleighScale),
            M(M(profile.RayleighTint.Y,table.RayleighExtinction.Y),rayleighScale),
            M(M(profile.RayleighTint.Z,table.RayleighExtinction.Z),rayleighScale),0);
        float saturationStart=profile.SaturationLow,saturationEnd=profile.SaturationHigh;
        float saturation=saturationStart==saturationEnd?(current640>saturationEnd?0:1):
            S(1,Clamp(D(S(current640,saturationStart),S(saturationEnd,saturationStart))));
        Vector4 Adjust(Vector4 color)
        {
            if(saturation>=1)return color;
            var hsv=SkylightColorMath.RgbToHsv(color);hsv.Y=M(hsv.Y,saturation);
            return SkylightColorMath.HsvToRgb(hsv);
        }
        rayleigh=Adjust(rayleigh);
        float height=SkyWeatherAmbientSampler.EffectiveHeight(inputs.Altitude,eyeY);
        var sun=SkyWeatherAmbientSampler.Sample(source.Transmittance,power,camera.SunMoonRgb.Lighting.Sun.Direction,height,dispatch);
        var moon=SkyWeatherAmbientSampler.Sample(source.Transmittance,power,camera.SunMoonRgb.Lighting.Moon.Direction,height,dispatch);
        Vector4 sunAmbient=Adjust(sun.Value),moonAmbient=Adjust(moon.Value);
        var lighting=camera.SunMoonRgb.Lighting;
        var sunBase=lighting.Sun.BaseColor; var moonBase=lighting.Moon.BaseColor;
        Vector4 background=new(M(A(M(moonAmbient.X,moonBase.X),M(sunAmbient.X,sunBase.X)),F(0x3E99999A)),
            M(A(M(moonAmbient.Y,moonBase.Y),M(sunAmbient.Y,sunBase.Y)),F(0x3E99999A)),
            M(A(M(moonAmbient.Z,moonBase.Z),M(sunAmbient.Z,sunBase.Z)),F(0x3E99999A)),0);
        // Original source CPU DB4 -> prepared1054 is the loaded anisotropy.
        var fog=SkyWeatherFogAssembler.Generate(new(background,rayleigh,mie,
            M(profile.FogStart,100),M(M(heightBlend,profile.FogDepthScale),100),table.PlanetRadius,1,
            M(S(1,M(strength,F(0x3E99999A))),profile.Anisotropy),M(profile.FogPivot,100),
            A(M(profile.FogBlend,profile.FogBlendScale),S(1,profile.FogBlend))));
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
