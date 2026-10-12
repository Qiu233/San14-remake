using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Common Cloud material after current weather preparation. Cameras,
/// lighting, atmosphere resources and retained animation belong to consumers.</summary>
public sealed class SkyCloudMaterial
{
    public const float AtmosphereHeight = 8000000;
    public const float DensityDistance = 1000;
    public const float SunHorizonOffset = -.06f;
    public const float ReservedPeriod = 2000000;

    private SkyCloudMaterial(SkyCloudWeatherState weather)
    {
        Weather = weather;
        Base = Layer(weather.Base); Detail = Layer(weather.Detail);
        Distort = Layer(weather.Distort); Cirrus = Layer(weather.Cirrus);
        CirrusDistort = Layer(weather.CirrusDistort);
        var settings = weather.Settings;
        SampleStepParameters = new(D(settings.SampleStep, 4), M(settings.SampleStep, 2.5f),
            D(1, DensityDistance), 1);
        float asymmetry0 = ClampSigned(settings.Hg.Asymmetry0);
        float asymmetry1 = ClampSigned(settings.Hg.Asymmetry1);
        float h0 = M(asymmetry0, asymmetry0), h1 = M(asymmetry1, asymmetry1);
        HgParameters0 = new(M(D(S(1, h0), 12.566371f), settings.Hg.Intensity),
            M(asymmetry0, -2), A(h0, 1), settings.Hg.Weight);
        HgParameters1 = new(M(D(S(1, h1), 12.566371f), settings.Hg.Intensity),
            M(asymmetry1, -2), A(h1, 1), 0);
    }
    public SkyCloudWeatherState Weather { get; }
    public SkyCloudNoiseLayer3 Base { get; }
    public SkyCloudNoiseLayer3 Detail { get; }
    public SkyCloudNoiseLayer2 Distort { get; }
    public SkyCloudNoiseLayer2 Cirrus { get; }
    public SkyCloudNoiseLayer2 CirrusDistort { get; }
    public Vector4 SampleStepParameters { get; }
    public Vector4 HgParameters0 { get; }
    public Vector4 HgParameters1 { get; }
    public Vector4 StableDimensions => new(32, 128, 32, 8);

    public static SkyCloudMaterial Prepare(SkyCloudWeatherState weather)
    {
        ArgumentNullException.ThrowIfNull(weather);
        return new(weather);
    }

    public SkyCloudVolumeMaterial PrepareVolume(SkyCloudAnimationState animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        var baseLayer = Texture(Base, animation.BasePhase);
        var detailLayer = Texture(Detail, animation.DetailPhase);
        var distort = Texture(Distort, animation.DistortPhase);
        var cirrus = Texture(Cirrus, animation.CirrusPhase);
        var cirrusDistort = Texture(CirrusDistort, animation.CirrusDistortPhase);
        var settings = Weather.Settings;
        return new(baseLayer.Offset, baseLayer.Scale, detailLayer.Offset, detailLayer.Scale,
            distort.Offset, distort.Scale,
            new(S(settings.RaySamples, settings.MinimumRaySamples), settings.MinimumRaySamples),
            new(settings.BottomAltitude, settings.Thickness, D(1, settings.Thickness), AtmosphereHeight),
            new(S(1, Weather.Coverage), Weather.Density, Weather.DetailAmount, D(Weather.Erosion, DensityDistance)),
            new(M(D(1, Detail.Period.X), settings.DistortAmplitude.X),
                M(D(1, Detail.Period.Y), settings.DistortAmplitude.Y),
                M(D(1, Detail.Period.Z), settings.DistortAmplitude.Z), Weather.Saturation),
            new(settings.BaseErosion, settings.DetailErosion),
            cirrus.Offset, cirrus.Scale, cirrusDistort.Offset, cirrusDistort.Scale,
            new(settings.CirrusAltitude, S(1, Weather.CirrusCoverage),
                settings.CirrusCoverage.Clear, settings.CirrusCoverage.Cloudy),
            new(M(settings.CirrusDistortAmplitude.X, D(1, Cirrus.Period.X)),
                M(settings.CirrusDistortAmplitude.Y, D(1, Cirrus.Period.Y))));
    }
    private static (Vector3 Offset, Vector3 Scale) Texture(SkyCloudNoiseLayer3 layer, Vector3 phase)
    {
        var scale = new Vector3(D(1, layer.Period.X), D(1, layer.Period.Y), D(1, layer.Period.Z));
        return (new(M(A(phase.X, layer.Position.X), scale.X),
            M(A(phase.Y, layer.Position.Y), scale.Y), M(A(phase.Z, layer.Position.Z), scale.Z)), scale);
    }
    private static (Vector2 Offset, Vector2 Scale) Texture(SkyCloudNoiseLayer2 layer, Vector2 phase)
    {
        var scale = new Vector2(D(1, layer.Period.X), D(1, layer.Period.Y));
        return (new(M(A(phase.X, layer.Position.X), scale.X), M(A(phase.Y, layer.Position.Y), scale.Y)), scale);
    }
    private static SkyCloudNoiseLayer3 Layer(SkyCloudNoiseLayer3 layer) => new(
        new(Size(layer.Period.X), Size(layer.Period.Y), Size(layer.Period.Z)), layer.Position, layer.Velocity);
    private static SkyCloudNoiseLayer2 Layer(SkyCloudNoiseLayer2 layer) => new(
        new(Size(layer.Period.X), Size(layer.Period.Y)), layer.Position, layer.Velocity);
    private static float Size(float value) => value >= float.MaxValue ? float.MaxValue :
        value > .0011920929f ? value : .0011920929f;
    private static float ClampSigned(float value) => value >= .9999881f ? .9999881f :
        value > -.9999881f ? value : -.9999881f;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
