using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record CameraExposureSettings
{
    public float LightAdaptationScale { get; init; }
    public float DarkAdaptationScale { get; init; }
    public float UserScale { get; init; }
    public float Gamma { get; init; }
    public float Luminance { get; init; }
    public float RangeMinimum { get; init; }
    public float RangeMaximum { get; init; }
    public float MinBrightness { get; init; }
    public float MaxBrightness { get; init; }
    public float LowBrightnessPercent { get; init; }
    public float HighBrightnessPercent { get; init; }
}

public sealed record CameraOutputSettings
{
    public float BloomIntensity { get; init; }
    public float StarIntensity { get; init; }
    public float LensFlareIntensity { get; init; }
    public float ColorScaleX { get; init; }
    public float ColorScaleY { get; init; }
    public float ColorScaleZ { get; init; }
    public float SaturationScaleX { get; init; }
    public float SaturationScaleY { get; init; }
    public float SaturationScaleZ { get; init; }
}

public sealed record CameraSurfaceSettings
{
    public uint SpecularOcclusionType { get; init; }
}

public sealed record CameraFxaaSettings
{
    public float EdgeThreshold { get; init; }
    public float EdgeThresholdMin { get; init; }
    public float EdgeSharpness { get; init; }
    public float PixelRange { get; init; }
    public uint Enable { get; init; }
    public uint Type { get; init; }
    public float QualityEdgeThreshold { get; init; }
    public float QualityEdgeThresholdMin { get; init; }
    public float QualitySubpixSharpness { get; init; }
    public uint QualityLevel { get; init; }
    public uint QualityPerformance { get; init; }
}

public sealed record CameraLimbSettings
{
    public float Start { get; init; }
    public float End { get; init; }
    public float Weight { get; init; }
}

public sealed record CameraLightShaftSettings
{
    public float NoiseScale { get; init; }
    public float NoiseFrequency { get; init; }
    public float NoiseWeight { get; init; }
}

public sealed record CameraDepthOfFieldSettings
{
    public float DistantBlurZThreshold { get; init; }
    public float DistantBlurIntensity { get; init; }
    public uint Flags { get; init; }
    public float SpriteThreshold { get; init; }
    public uint CompareBrightnessBits { get; init; }
}

public sealed record CameraColorGradingSettings
{
    public uint HdrLutKey { get; init; }
    public uint LdrLutKey { get; init; }
    public float LdrGradingIntensity { get; init; }
    public float LutTransitionSpan { get; init; }
    public uint Lut1 { get; init; }
    public uint Lut2 { get; init; }
    public float InterpolationRate { get; init; }
    public float Intensity { get; init; }
    public uint HdrTvNit { get; init; }
    public uint BaseTvNit { get; init; }
    public uint TonemapType { get; init; }
    public float NeutralBlackIn { get; init; }
    public float NeutralBlackOut { get; init; }
}

public sealed record CameraLensSimulationSettings
{
    public float SensorAspect { get; init; }
    public float ObjectDistance { get; init; }
    public float InfiniteFocalLength { get; init; }
    public float FStop { get; init; }
    public float ExposureDial { get; init; }
    public float ShutterSpeed { get; init; }
    public uint ColorTemperature { get; init; }
    public float ColorSaturation { get; init; }
    public float SunRadius { get; init; }
    public uint AnamorphicFlag { get; init; }
    public uint ObjectDistanceFlags { get; init; }
}

public sealed record CameraTemporalAaSettings
{
    public float VelocityWeight { get; init; }
    public float DepthDiffThreshold { get; init; }
    public float BaseWeight { get; init; }
    public uint Enable { get; init; }
}

public sealed record CameraLensEffectsSettings
{
    public float GhostScale { get; init; }
    public uint GhostNum { get; init; }
    public uint DiaphragmBladeCounts { get; init; }
    public float HaloScale { get; init; }
    public float BloomScale { get; init; }
    public float BloomThreshold { get; init; }
    public float BloomThresholdSoftKnee { get; init; }
    public float BloomRadius { get; init; }
    public float LensFlareThreshold { get; init; }
    public float VignettingFade { get; init; }
    public uint LowLuminanceBloomingFix { get; init; }
    public uint AberraType { get; init; }
    public float AberraAmount { get; init; }
    public float CrossFilterWeight { get; init; }
    public float CrossFilterLength { get; init; }
    public float CrossFilterAttenuation { get; init; }
    public float CrossFilterInclination { get; init; }
    public float CrossFilterAberration { get; init; }
}

public sealed record CameraHeatShimmerSettings
{
    public float Vibra { get; init; }
    public float Cycle { get; init; }
    public float Speed { get; init; }
}

/// <summary>Immutable optical parameters. Loaded declarations, seasonal selections and caller overrides form one current value.</summary>
public sealed record CameraOpticsSettings
{
    public CameraExposureSettings Exposure { get; init; } = new();
    public CameraOutputSettings Output { get; init; } = new();
    public CameraSurfaceSettings Surface { get; init; } = new();
    public CameraFxaaSettings Fxaa { get; init; } = new();
    public CameraLimbSettings Limb { get; init; } = new();
    public CameraLightShaftSettings LightShaft { get; init; } = new();
    public CameraDepthOfFieldSettings DepthOfField { get; init; } = new();
    public CameraColorGradingSettings ColorGrading { get; init; } = new();
    public CameraLensSimulationSettings LensSimulation { get; init; } = new();
    public CameraTemporalAaSettings TemporalAa { get; init; } = new();
    public CameraLensEffectsSettings LensEffects { get; init; } = new();
    public CameraHeatShimmerSettings HeatShimmer { get; init; } = new();
    public static CameraOpticsSettings FromSource(LoadedMapCameraConstructionSource source) => CameraOpticsImport.Defaults(source);
    public CameraOpticsSettings Apply(CameraOpticsPatch patch) => patch.ApplyTo(this);
}
