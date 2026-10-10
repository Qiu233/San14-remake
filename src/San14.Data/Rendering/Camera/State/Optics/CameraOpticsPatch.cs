namespace San14.Data;

/// <summary>Only written parameters are present. An omitted value retains the prior settings, including raw integer flag encodings.</summary>
public sealed record CameraOpticsPatch
{
    public float? ExposureUserScale { get; init; }
    public float? ExposureGamma { get; init; }
    public float? ExposureLuminance { get; init; }
    public float? OutputBloomIntensity { get; init; }
    public float? OutputStarIntensity { get; init; }
    public float? OutputLensFlareIntensity { get; init; }
    public float? OutputColorScaleX { get; init; }
    public float? OutputColorScaleY { get; init; }
    public float? OutputColorScaleZ { get; init; }
    public float? OutputSaturationScaleX { get; init; }
    public float? OutputSaturationScaleY { get; init; }
    public float? OutputSaturationScaleZ { get; init; }
    public float? ExposureRangeMinimum { get; init; }
    public float? ExposureRangeMaximum { get; init; }
    public uint? SurfaceSpecularOcclusionType { get; init; }
    public float? FxaaEdgeThreshold { get; init; }
    public float? FxaaEdgeThresholdMin { get; init; }
    public float? FxaaEdgeSharpness { get; init; }
    public float? FxaaPixelRange { get; init; }
    public uint? FxaaEnable { get; init; }
    public uint? FxaaType { get; init; }
    public float? FxaaQualityEdgeThreshold { get; init; }
    public float? FxaaQualityEdgeThresholdMin { get; init; }
    public float? FxaaQualitySubpixSharpness { get; init; }
    public uint? FxaaQualityLevel { get; init; }
    public uint? FxaaQualityPerformance { get; init; }
    public float? LimbStart { get; init; }
    public float? LimbEnd { get; init; }
    public float? LimbWeight { get; init; }
    public float? LightShaftNoiseScale { get; init; }
    public float? LightShaftNoiseFrequency { get; init; }
    public float? LightShaftNoiseWeight { get; init; }
    public float? DepthOfFieldDistantBlurZThreshold { get; init; }
    public float? DepthOfFieldDistantBlurIntensity { get; init; }
    public uint? ColorGradingHdrLutKey { get; init; }
    public uint? ColorGradingLdrLutKey { get; init; }
    public float? LensSimulationObjectDistance { get; init; }
    public float? LensSimulationInfiniteFocalLength { get; init; }
    public float? LensSimulationFStop { get; init; }
    public float? LensSimulationExposureDial { get; init; }
    public float? LensSimulationShutterSpeed { get; init; }
    public uint? LensSimulationColorTemperature { get; init; }
    public float? LensSimulationColorSaturation { get; init; }
    public float? LensSimulationSunRadius { get; init; }
    public uint? LensSimulationAnamorphicFlag { get; init; }
    public uint? DepthOfFieldFlags { get; init; }
    public float? DepthOfFieldSpriteThreshold { get; init; }
    public uint? DepthOfFieldCompareBrightnessBits { get; init; }
    public uint? LensSimulationObjectDistanceFlags { get; init; }
    public float? TemporalAaVelocityWeight { get; init; }
    public float? TemporalAaDepthDiffThreshold { get; init; }
    public float? TemporalAaBaseWeight { get; init; }
    public uint? TemporalAaEnable { get; init; }
    public float? LensEffectsGhostScale { get; init; }
    public uint? LensEffectsGhostNum { get; init; }
    public uint? LensEffectsDiaphragmBladeCounts { get; init; }
    public float? LensEffectsHaloScale { get; init; }
    public float? LensEffectsBloomScale { get; init; }
    public float? LensEffectsBloomThreshold { get; init; }
    public float? LensEffectsBloomThresholdSoftKnee { get; init; }
    public float? LensEffectsBloomRadius { get; init; }
    public float? LensEffectsLensFlareThreshold { get; init; }
    public float? LensEffectsVignettingFade { get; init; }
    public uint? LensEffectsLowLuminanceBloomingFix { get; init; }
    public float? ExposureMinBrightness { get; init; }
    public float? ExposureMaxBrightness { get; init; }
    public float? ExposureLowBrightnessPercent { get; init; }
    public float? ExposureHighBrightnessPercent { get; init; }
    public uint? LensEffectsAberraType { get; init; }
    public float? LensEffectsAberraAmount { get; init; }
    public float? ColorGradingLdrGradingIntensity { get; init; }
    public float? ColorGradingLutTransitionSpan { get; init; }
    public float? LensEffectsCrossFilterWeight { get; init; }
    public float? LensEffectsCrossFilterLength { get; init; }
    public float? LensEffectsCrossFilterAttenuation { get; init; }
    public float? LensEffectsCrossFilterInclination { get; init; }
    public float? LensEffectsCrossFilterAberration { get; init; }
    public uint? ColorGradingLut1 { get; init; }
    public uint? ColorGradingLut2 { get; init; }
    public float? ColorGradingInterpolationRate { get; init; }
    public float? ColorGradingIntensity { get; init; }
    public float? HeatShimmerVibra { get; init; }
    public float? HeatShimmerCycle { get; init; }
    public float? HeatShimmerSpeed { get; init; }
    public uint? ColorGradingHdrTvNit { get; init; }
    public uint? ColorGradingBaseTvNit { get; init; }
    public uint? ColorGradingTonemapType { get; init; }
    public float? ColorGradingNeutralBlackIn { get; init; }
    public float? ColorGradingNeutralBlackOut { get; init; }
    public CameraOpticsSettings ApplyTo(CameraOpticsSettings current) => current with
    {
        Exposure = current.Exposure with
        {
            UserScale = ExposureUserScale ?? current.Exposure.UserScale,
            Gamma = ExposureGamma ?? current.Exposure.Gamma,
            Luminance = ExposureLuminance ?? current.Exposure.Luminance,
            RangeMinimum = ExposureRangeMinimum ?? current.Exposure.RangeMinimum,
            RangeMaximum = ExposureRangeMaximum ?? current.Exposure.RangeMaximum,
            MinBrightness = ExposureMinBrightness ?? current.Exposure.MinBrightness,
            MaxBrightness = ExposureMaxBrightness ?? current.Exposure.MaxBrightness,
            LowBrightnessPercent = ExposureLowBrightnessPercent ?? current.Exposure.LowBrightnessPercent,
            HighBrightnessPercent = ExposureHighBrightnessPercent ?? current.Exposure.HighBrightnessPercent,
        },
        Output = current.Output with
        {
            BloomIntensity = OutputBloomIntensity ?? current.Output.BloomIntensity,
            StarIntensity = OutputStarIntensity ?? current.Output.StarIntensity,
            LensFlareIntensity = OutputLensFlareIntensity ?? current.Output.LensFlareIntensity,
            ColorScaleX = OutputColorScaleX ?? current.Output.ColorScaleX,
            ColorScaleY = OutputColorScaleY ?? current.Output.ColorScaleY,
            ColorScaleZ = OutputColorScaleZ ?? current.Output.ColorScaleZ,
            SaturationScaleX = OutputSaturationScaleX ?? current.Output.SaturationScaleX,
            SaturationScaleY = OutputSaturationScaleY ?? current.Output.SaturationScaleY,
            SaturationScaleZ = OutputSaturationScaleZ ?? current.Output.SaturationScaleZ,
        },
        Surface = current.Surface with
        {
            SpecularOcclusionType = SurfaceSpecularOcclusionType ?? current.Surface.SpecularOcclusionType,
        },
        Fxaa = current.Fxaa with
        {
            EdgeThreshold = FxaaEdgeThreshold ?? current.Fxaa.EdgeThreshold,
            EdgeThresholdMin = FxaaEdgeThresholdMin ?? current.Fxaa.EdgeThresholdMin,
            EdgeSharpness = FxaaEdgeSharpness ?? current.Fxaa.EdgeSharpness,
            PixelRange = FxaaPixelRange ?? current.Fxaa.PixelRange,
            Enable = FxaaEnable ?? current.Fxaa.Enable,
            Type = FxaaType ?? current.Fxaa.Type,
            QualityEdgeThreshold = FxaaQualityEdgeThreshold ?? current.Fxaa.QualityEdgeThreshold,
            QualityEdgeThresholdMin = FxaaQualityEdgeThresholdMin ?? current.Fxaa.QualityEdgeThresholdMin,
            QualitySubpixSharpness = FxaaQualitySubpixSharpness ?? current.Fxaa.QualitySubpixSharpness,
            QualityLevel = FxaaQualityLevel ?? current.Fxaa.QualityLevel,
            QualityPerformance = FxaaQualityPerformance ?? current.Fxaa.QualityPerformance,
        },
        Limb = current.Limb with
        {
            Start = LimbStart ?? current.Limb.Start,
            End = LimbEnd ?? current.Limb.End,
            Weight = LimbWeight ?? current.Limb.Weight,
        },
        LightShaft = current.LightShaft with
        {
            NoiseScale = LightShaftNoiseScale ?? current.LightShaft.NoiseScale,
            NoiseFrequency = LightShaftNoiseFrequency ?? current.LightShaft.NoiseFrequency,
            NoiseWeight = LightShaftNoiseWeight ?? current.LightShaft.NoiseWeight,
        },
        DepthOfField = current.DepthOfField with
        {
            DistantBlurZThreshold = DepthOfFieldDistantBlurZThreshold ?? current.DepthOfField.DistantBlurZThreshold,
            DistantBlurIntensity = DepthOfFieldDistantBlurIntensity ?? current.DepthOfField.DistantBlurIntensity,
            Flags = DepthOfFieldFlags ?? current.DepthOfField.Flags,
            SpriteThreshold = DepthOfFieldSpriteThreshold ?? current.DepthOfField.SpriteThreshold,
            CompareBrightnessBits = DepthOfFieldCompareBrightnessBits ?? current.DepthOfField.CompareBrightnessBits,
        },
        ColorGrading = current.ColorGrading with
        {
            HdrLutKey = ColorGradingHdrLutKey ?? current.ColorGrading.HdrLutKey,
            LdrLutKey = ColorGradingLdrLutKey ?? current.ColorGrading.LdrLutKey,
            LdrGradingIntensity = ColorGradingLdrGradingIntensity ?? current.ColorGrading.LdrGradingIntensity,
            LutTransitionSpan = ColorGradingLutTransitionSpan ?? current.ColorGrading.LutTransitionSpan,
            Lut1 = ColorGradingLut1 ?? current.ColorGrading.Lut1,
            Lut2 = ColorGradingLut2 ?? current.ColorGrading.Lut2,
            InterpolationRate = ColorGradingInterpolationRate ?? current.ColorGrading.InterpolationRate,
            Intensity = ColorGradingIntensity ?? current.ColorGrading.Intensity,
            HdrTvNit = ColorGradingHdrTvNit ?? current.ColorGrading.HdrTvNit,
            BaseTvNit = ColorGradingBaseTvNit ?? current.ColorGrading.BaseTvNit,
            TonemapType = ColorGradingTonemapType ?? current.ColorGrading.TonemapType,
            NeutralBlackIn = ColorGradingNeutralBlackIn ?? current.ColorGrading.NeutralBlackIn,
            NeutralBlackOut = ColorGradingNeutralBlackOut ?? current.ColorGrading.NeutralBlackOut,
        },
        LensSimulation = current.LensSimulation with
        {
            ObjectDistance = LensSimulationObjectDistance ?? current.LensSimulation.ObjectDistance,
            InfiniteFocalLength = LensSimulationInfiniteFocalLength ?? current.LensSimulation.InfiniteFocalLength,
            FStop = LensSimulationFStop ?? current.LensSimulation.FStop,
            ExposureDial = LensSimulationExposureDial ?? current.LensSimulation.ExposureDial,
            ShutterSpeed = LensSimulationShutterSpeed ?? current.LensSimulation.ShutterSpeed,
            ColorTemperature = LensSimulationColorTemperature ?? current.LensSimulation.ColorTemperature,
            ColorSaturation = LensSimulationColorSaturation ?? current.LensSimulation.ColorSaturation,
            SunRadius = LensSimulationSunRadius ?? current.LensSimulation.SunRadius,
            AnamorphicFlag = LensSimulationAnamorphicFlag ?? current.LensSimulation.AnamorphicFlag,
            ObjectDistanceFlags = LensSimulationObjectDistanceFlags ?? current.LensSimulation.ObjectDistanceFlags,
        },
        TemporalAa = current.TemporalAa with
        {
            VelocityWeight = TemporalAaVelocityWeight ?? current.TemporalAa.VelocityWeight,
            DepthDiffThreshold = TemporalAaDepthDiffThreshold ?? current.TemporalAa.DepthDiffThreshold,
            BaseWeight = TemporalAaBaseWeight ?? current.TemporalAa.BaseWeight,
            Enable = TemporalAaEnable ?? current.TemporalAa.Enable,
        },
        LensEffects = current.LensEffects with
        {
            GhostScale = LensEffectsGhostScale ?? current.LensEffects.GhostScale,
            GhostNum = LensEffectsGhostNum ?? current.LensEffects.GhostNum,
            DiaphragmBladeCounts = LensEffectsDiaphragmBladeCounts ?? current.LensEffects.DiaphragmBladeCounts,
            HaloScale = LensEffectsHaloScale ?? current.LensEffects.HaloScale,
            BloomScale = LensEffectsBloomScale ?? current.LensEffects.BloomScale,
            BloomThreshold = LensEffectsBloomThreshold ?? current.LensEffects.BloomThreshold,
            BloomThresholdSoftKnee = LensEffectsBloomThresholdSoftKnee ?? current.LensEffects.BloomThresholdSoftKnee,
            BloomRadius = LensEffectsBloomRadius ?? current.LensEffects.BloomRadius,
            LensFlareThreshold = LensEffectsLensFlareThreshold ?? current.LensEffects.LensFlareThreshold,
            VignettingFade = LensEffectsVignettingFade ?? current.LensEffects.VignettingFade,
            LowLuminanceBloomingFix = LensEffectsLowLuminanceBloomingFix ?? current.LensEffects.LowLuminanceBloomingFix,
            AberraType = LensEffectsAberraType ?? current.LensEffects.AberraType,
            AberraAmount = LensEffectsAberraAmount ?? current.LensEffects.AberraAmount,
            CrossFilterWeight = LensEffectsCrossFilterWeight ?? current.LensEffects.CrossFilterWeight,
            CrossFilterLength = LensEffectsCrossFilterLength ?? current.LensEffects.CrossFilterLength,
            CrossFilterAttenuation = LensEffectsCrossFilterAttenuation ?? current.LensEffects.CrossFilterAttenuation,
            CrossFilterInclination = LensEffectsCrossFilterInclination ?? current.LensEffects.CrossFilterInclination,
            CrossFilterAberration = LensEffectsCrossFilterAberration ?? current.LensEffects.CrossFilterAberration,
        },
        HeatShimmer = current.HeatShimmer with
        {
            Vibra = HeatShimmerVibra ?? current.HeatShimmer.Vibra,
            Cycle = HeatShimmerCycle ?? current.HeatShimmer.Cycle,
            Speed = HeatShimmerSpeed ?? current.HeatShimmer.Speed,
        },
    };
    public CameraOpticsPatch Then(CameraOpticsPatch later) => this with
    {
        ExposureUserScale = later.ExposureUserScale ?? ExposureUserScale,
        ExposureGamma = later.ExposureGamma ?? ExposureGamma,
        ExposureLuminance = later.ExposureLuminance ?? ExposureLuminance,
        OutputBloomIntensity = later.OutputBloomIntensity ?? OutputBloomIntensity,
        OutputStarIntensity = later.OutputStarIntensity ?? OutputStarIntensity,
        OutputLensFlareIntensity = later.OutputLensFlareIntensity ?? OutputLensFlareIntensity,
        OutputColorScaleX = later.OutputColorScaleX ?? OutputColorScaleX,
        OutputColorScaleY = later.OutputColorScaleY ?? OutputColorScaleY,
        OutputColorScaleZ = later.OutputColorScaleZ ?? OutputColorScaleZ,
        OutputSaturationScaleX = later.OutputSaturationScaleX ?? OutputSaturationScaleX,
        OutputSaturationScaleY = later.OutputSaturationScaleY ?? OutputSaturationScaleY,
        OutputSaturationScaleZ = later.OutputSaturationScaleZ ?? OutputSaturationScaleZ,
        ExposureRangeMinimum = later.ExposureRangeMinimum ?? ExposureRangeMinimum,
        ExposureRangeMaximum = later.ExposureRangeMaximum ?? ExposureRangeMaximum,
        SurfaceSpecularOcclusionType = later.SurfaceSpecularOcclusionType ?? SurfaceSpecularOcclusionType,
        FxaaEdgeThreshold = later.FxaaEdgeThreshold ?? FxaaEdgeThreshold,
        FxaaEdgeThresholdMin = later.FxaaEdgeThresholdMin ?? FxaaEdgeThresholdMin,
        FxaaEdgeSharpness = later.FxaaEdgeSharpness ?? FxaaEdgeSharpness,
        FxaaPixelRange = later.FxaaPixelRange ?? FxaaPixelRange,
        FxaaEnable = later.FxaaEnable ?? FxaaEnable,
        FxaaType = later.FxaaType ?? FxaaType,
        FxaaQualityEdgeThreshold = later.FxaaQualityEdgeThreshold ?? FxaaQualityEdgeThreshold,
        FxaaQualityEdgeThresholdMin = later.FxaaQualityEdgeThresholdMin ?? FxaaQualityEdgeThresholdMin,
        FxaaQualitySubpixSharpness = later.FxaaQualitySubpixSharpness ?? FxaaQualitySubpixSharpness,
        FxaaQualityLevel = later.FxaaQualityLevel ?? FxaaQualityLevel,
        FxaaQualityPerformance = later.FxaaQualityPerformance ?? FxaaQualityPerformance,
        LimbStart = later.LimbStart ?? LimbStart,
        LimbEnd = later.LimbEnd ?? LimbEnd,
        LimbWeight = later.LimbWeight ?? LimbWeight,
        LightShaftNoiseScale = later.LightShaftNoiseScale ?? LightShaftNoiseScale,
        LightShaftNoiseFrequency = later.LightShaftNoiseFrequency ?? LightShaftNoiseFrequency,
        LightShaftNoiseWeight = later.LightShaftNoiseWeight ?? LightShaftNoiseWeight,
        DepthOfFieldDistantBlurZThreshold = later.DepthOfFieldDistantBlurZThreshold ?? DepthOfFieldDistantBlurZThreshold,
        DepthOfFieldDistantBlurIntensity = later.DepthOfFieldDistantBlurIntensity ?? DepthOfFieldDistantBlurIntensity,
        ColorGradingHdrLutKey = later.ColorGradingHdrLutKey ?? ColorGradingHdrLutKey,
        ColorGradingLdrLutKey = later.ColorGradingLdrLutKey ?? ColorGradingLdrLutKey,
        LensSimulationObjectDistance = later.LensSimulationObjectDistance ?? LensSimulationObjectDistance,
        LensSimulationInfiniteFocalLength = later.LensSimulationInfiniteFocalLength ?? LensSimulationInfiniteFocalLength,
        LensSimulationFStop = later.LensSimulationFStop ?? LensSimulationFStop,
        LensSimulationExposureDial = later.LensSimulationExposureDial ?? LensSimulationExposureDial,
        LensSimulationShutterSpeed = later.LensSimulationShutterSpeed ?? LensSimulationShutterSpeed,
        LensSimulationColorTemperature = later.LensSimulationColorTemperature ?? LensSimulationColorTemperature,
        LensSimulationColorSaturation = later.LensSimulationColorSaturation ?? LensSimulationColorSaturation,
        LensSimulationSunRadius = later.LensSimulationSunRadius ?? LensSimulationSunRadius,
        LensSimulationAnamorphicFlag = later.LensSimulationAnamorphicFlag ?? LensSimulationAnamorphicFlag,
        DepthOfFieldFlags = later.DepthOfFieldFlags ?? DepthOfFieldFlags,
        DepthOfFieldSpriteThreshold = later.DepthOfFieldSpriteThreshold ?? DepthOfFieldSpriteThreshold,
        DepthOfFieldCompareBrightnessBits = later.DepthOfFieldCompareBrightnessBits ?? DepthOfFieldCompareBrightnessBits,
        LensSimulationObjectDistanceFlags = later.LensSimulationObjectDistanceFlags ?? LensSimulationObjectDistanceFlags,
        TemporalAaVelocityWeight = later.TemporalAaVelocityWeight ?? TemporalAaVelocityWeight,
        TemporalAaDepthDiffThreshold = later.TemporalAaDepthDiffThreshold ?? TemporalAaDepthDiffThreshold,
        TemporalAaBaseWeight = later.TemporalAaBaseWeight ?? TemporalAaBaseWeight,
        TemporalAaEnable = later.TemporalAaEnable ?? TemporalAaEnable,
        LensEffectsGhostScale = later.LensEffectsGhostScale ?? LensEffectsGhostScale,
        LensEffectsGhostNum = later.LensEffectsGhostNum ?? LensEffectsGhostNum,
        LensEffectsDiaphragmBladeCounts = later.LensEffectsDiaphragmBladeCounts ?? LensEffectsDiaphragmBladeCounts,
        LensEffectsHaloScale = later.LensEffectsHaloScale ?? LensEffectsHaloScale,
        LensEffectsBloomScale = later.LensEffectsBloomScale ?? LensEffectsBloomScale,
        LensEffectsBloomThreshold = later.LensEffectsBloomThreshold ?? LensEffectsBloomThreshold,
        LensEffectsBloomThresholdSoftKnee = later.LensEffectsBloomThresholdSoftKnee ?? LensEffectsBloomThresholdSoftKnee,
        LensEffectsBloomRadius = later.LensEffectsBloomRadius ?? LensEffectsBloomRadius,
        LensEffectsLensFlareThreshold = later.LensEffectsLensFlareThreshold ?? LensEffectsLensFlareThreshold,
        LensEffectsVignettingFade = later.LensEffectsVignettingFade ?? LensEffectsVignettingFade,
        LensEffectsLowLuminanceBloomingFix = later.LensEffectsLowLuminanceBloomingFix ?? LensEffectsLowLuminanceBloomingFix,
        ExposureMinBrightness = later.ExposureMinBrightness ?? ExposureMinBrightness,
        ExposureMaxBrightness = later.ExposureMaxBrightness ?? ExposureMaxBrightness,
        ExposureLowBrightnessPercent = later.ExposureLowBrightnessPercent ?? ExposureLowBrightnessPercent,
        ExposureHighBrightnessPercent = later.ExposureHighBrightnessPercent ?? ExposureHighBrightnessPercent,
        LensEffectsAberraType = later.LensEffectsAberraType ?? LensEffectsAberraType,
        LensEffectsAberraAmount = later.LensEffectsAberraAmount ?? LensEffectsAberraAmount,
        ColorGradingLdrGradingIntensity = later.ColorGradingLdrGradingIntensity ?? ColorGradingLdrGradingIntensity,
        ColorGradingLutTransitionSpan = later.ColorGradingLutTransitionSpan ?? ColorGradingLutTransitionSpan,
        LensEffectsCrossFilterWeight = later.LensEffectsCrossFilterWeight ?? LensEffectsCrossFilterWeight,
        LensEffectsCrossFilterLength = later.LensEffectsCrossFilterLength ?? LensEffectsCrossFilterLength,
        LensEffectsCrossFilterAttenuation = later.LensEffectsCrossFilterAttenuation ?? LensEffectsCrossFilterAttenuation,
        LensEffectsCrossFilterInclination = later.LensEffectsCrossFilterInclination ?? LensEffectsCrossFilterInclination,
        LensEffectsCrossFilterAberration = later.LensEffectsCrossFilterAberration ?? LensEffectsCrossFilterAberration,
        ColorGradingLut1 = later.ColorGradingLut1 ?? ColorGradingLut1,
        ColorGradingLut2 = later.ColorGradingLut2 ?? ColorGradingLut2,
        ColorGradingInterpolationRate = later.ColorGradingInterpolationRate ?? ColorGradingInterpolationRate,
        ColorGradingIntensity = later.ColorGradingIntensity ?? ColorGradingIntensity,
        HeatShimmerVibra = later.HeatShimmerVibra ?? HeatShimmerVibra,
        HeatShimmerCycle = later.HeatShimmerCycle ?? HeatShimmerCycle,
        HeatShimmerSpeed = later.HeatShimmerSpeed ?? HeatShimmerSpeed,
        ColorGradingHdrTvNit = later.ColorGradingHdrTvNit ?? ColorGradingHdrTvNit,
        ColorGradingBaseTvNit = later.ColorGradingBaseTvNit ?? ColorGradingBaseTvNit,
        ColorGradingTonemapType = later.ColorGradingTonemapType ?? ColorGradingTonemapType,
        ColorGradingNeutralBlackIn = later.ColorGradingNeutralBlackIn ?? ColorGradingNeutralBlackIn,
        ColorGradingNeutralBlackOut = later.ColorGradingNeutralBlackOut ?? ColorGradingNeutralBlackOut,
    };
    internal CameraOpticsPatch ForPreparedTail() => this with
    {
        ExposureUserScale = null,
        ExposureGamma = null,
        ExposureLuminance = null,
        OutputBloomIntensity = null,
        OutputStarIntensity = null,
        OutputLensFlareIntensity = null,
        OutputColorScaleX = null,
        OutputColorScaleY = null,
        OutputColorScaleZ = null,
        OutputSaturationScaleX = null,
        OutputSaturationScaleY = null,
        OutputSaturationScaleZ = null,
        ExposureRangeMinimum = null,
        ExposureRangeMaximum = null,
    };
}
