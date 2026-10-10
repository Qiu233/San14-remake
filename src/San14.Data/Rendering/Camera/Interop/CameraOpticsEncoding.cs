using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Legacy camera packet boundary. Offsets here identify encoded fields, never settings ownership.</summary>
public static partial class CameraOpticsEncoding
{
    public static void WritePrefix(CameraOpticsSettings settings, Span<byte> retainedPrefix)
    {
        BinaryPrimitives.WriteSingleLittleEndian(retainedPrefix.Slice(0x1C4, 4), settings.Exposure.LightAdaptationScale);
        BinaryPrimitives.WriteSingleLittleEndian(retainedPrefix.Slice(0x1C8, 4), settings.Exposure.DarkAdaptationScale);
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x170, 4), BitConverter.SingleToUInt32Bits(settings.Exposure.UserScale));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x174, 4), BitConverter.SingleToUInt32Bits(settings.Exposure.Gamma));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x17C, 4), BitConverter.SingleToUInt32Bits(settings.Exposure.Luminance));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x180, 4), BitConverter.SingleToUInt32Bits(settings.Output.BloomIntensity));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x184, 4), BitConverter.SingleToUInt32Bits(settings.Output.StarIntensity));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x188, 4), BitConverter.SingleToUInt32Bits(settings.Output.LensFlareIntensity));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x18C, 4), BitConverter.SingleToUInt32Bits(settings.Output.ColorScaleX));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x190, 4), BitConverter.SingleToUInt32Bits(settings.Output.ColorScaleY));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x194, 4), BitConverter.SingleToUInt32Bits(settings.Output.ColorScaleZ));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x198, 4), BitConverter.SingleToUInt32Bits(settings.Output.SaturationScaleX));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x19C, 4), BitConverter.SingleToUInt32Bits(settings.Output.SaturationScaleY));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x1A0, 4), BitConverter.SingleToUInt32Bits(settings.Output.SaturationScaleZ));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x1B8, 4), BitConverter.SingleToUInt32Bits(settings.Exposure.RangeMinimum));
        BinaryPrimitives.WriteUInt32LittleEndian(retainedPrefix.Slice(0x1BC, 4), BitConverter.SingleToUInt32Bits(settings.Exposure.RangeMaximum));
    }
    public static CameraOpticsSettings DecodePrefix(LoadedMapCameraConstructionSource source, ReadOnlySpan<byte> encodedPrefix,
        CameraOpticsSettings? retainedOptics = null)
    {
        var patch = new CameraOpticsPatch
        {
            ExposureUserScale = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x170, 4))),
            ExposureGamma = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x174, 4))),
            ExposureLuminance = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x17C, 4))),
            OutputBloomIntensity = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x180, 4))),
            OutputStarIntensity = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x184, 4))),
            OutputLensFlareIntensity = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x188, 4))),
            OutputColorScaleX = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x18C, 4))),
            OutputColorScaleY = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x190, 4))),
            OutputColorScaleZ = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x194, 4))),
            OutputSaturationScaleX = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x198, 4))),
            OutputSaturationScaleY = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x19C, 4))),
            OutputSaturationScaleZ = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x1A0, 4))),
            ExposureRangeMinimum = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x1B8, 4))),
            ExposureRangeMaximum = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(encodedPrefix.Slice(0x1BC, 4))),
        };
        var settings = (retainedOptics ?? CameraOpticsSettings.FromSource(source)).Apply(patch);
        return settings with { Exposure = settings.Exposure with
        {
            LightAdaptationScale = BinaryPrimitives.ReadSingleLittleEndian(encodedPrefix.Slice(0x1C4, 4)),
            DarkAdaptationScale = BinaryPrimitives.ReadSingleLittleEndian(encodedPrefix.Slice(0x1C8, 4))
        } };
    }
    public static bool TryRead(CameraOpticsPatch patch, int encodedOffset, out uint bits)
    {
        uint? value = encodedOffset switch
        {
            0x170 => patch.ExposureUserScale is float f170 ? BitConverter.SingleToUInt32Bits(f170) : null,
            0x174 => patch.ExposureGamma is float f174 ? BitConverter.SingleToUInt32Bits(f174) : null,
            0x17C => patch.ExposureLuminance is float f17C ? BitConverter.SingleToUInt32Bits(f17C) : null,
            0x180 => patch.OutputBloomIntensity is float f180 ? BitConverter.SingleToUInt32Bits(f180) : null,
            0x184 => patch.OutputStarIntensity is float f184 ? BitConverter.SingleToUInt32Bits(f184) : null,
            0x188 => patch.OutputLensFlareIntensity is float f188 ? BitConverter.SingleToUInt32Bits(f188) : null,
            0x18C => patch.OutputColorScaleX is float f18C ? BitConverter.SingleToUInt32Bits(f18C) : null,
            0x190 => patch.OutputColorScaleY is float f190 ? BitConverter.SingleToUInt32Bits(f190) : null,
            0x194 => patch.OutputColorScaleZ is float f194 ? BitConverter.SingleToUInt32Bits(f194) : null,
            0x198 => patch.OutputSaturationScaleX is float f198 ? BitConverter.SingleToUInt32Bits(f198) : null,
            0x19C => patch.OutputSaturationScaleY is float f19C ? BitConverter.SingleToUInt32Bits(f19C) : null,
            0x1A0 => patch.OutputSaturationScaleZ is float f1A0 ? BitConverter.SingleToUInt32Bits(f1A0) : null,
            0x1B8 => patch.ExposureRangeMinimum is float f1B8 ? BitConverter.SingleToUInt32Bits(f1B8) : null,
            0x1BC => patch.ExposureRangeMaximum is float f1BC ? BitConverter.SingleToUInt32Bits(f1BC) : null,
            0x3C0 => patch.SurfaceSpecularOcclusionType,
            0x404 => patch.FxaaEdgeThreshold is float f404 ? BitConverter.SingleToUInt32Bits(f404) : null,
            0x408 => patch.FxaaEdgeThresholdMin is float f408 ? BitConverter.SingleToUInt32Bits(f408) : null,
            0x40C => patch.FxaaEdgeSharpness is float f40C ? BitConverter.SingleToUInt32Bits(f40C) : null,
            0x410 => patch.FxaaPixelRange is float f410 ? BitConverter.SingleToUInt32Bits(f410) : null,
            0x414 => patch.FxaaEnable,
            0x418 => patch.FxaaType,
            0x41C => patch.FxaaQualityEdgeThreshold is float f41C ? BitConverter.SingleToUInt32Bits(f41C) : null,
            0x420 => patch.FxaaQualityEdgeThresholdMin is float f420 ? BitConverter.SingleToUInt32Bits(f420) : null,
            0x424 => patch.FxaaQualitySubpixSharpness is float f424 ? BitConverter.SingleToUInt32Bits(f424) : null,
            0x428 => patch.FxaaQualityLevel,
            0x42C => patch.FxaaQualityPerformance,
            0x7A8 => patch.LimbStart is float f7A8 ? BitConverter.SingleToUInt32Bits(f7A8) : null,
            0x7AC => patch.LimbEnd is float f7AC ? BitConverter.SingleToUInt32Bits(f7AC) : null,
            0x7B0 => patch.LimbWeight is float f7B0 ? BitConverter.SingleToUInt32Bits(f7B0) : null,
            0xE2C => patch.LightShaftNoiseScale is float fE2C ? BitConverter.SingleToUInt32Bits(fE2C) : null,
            0xE30 => patch.LightShaftNoiseFrequency is float fE30 ? BitConverter.SingleToUInt32Bits(fE30) : null,
            0xE34 => patch.LightShaftNoiseWeight is float fE34 ? BitConverter.SingleToUInt32Bits(fE34) : null,
            0xE78 => patch.DepthOfFieldDistantBlurZThreshold is float fE78 ? BitConverter.SingleToUInt32Bits(fE78) : null,
            0xE7C => patch.DepthOfFieldDistantBlurIntensity is float fE7C ? BitConverter.SingleToUInt32Bits(fE7C) : null,
            0xEB8 => patch.ColorGradingHdrLutKey,
            0xEBC => patch.ColorGradingLdrLutKey,
            0x12FC => patch.LensSimulationObjectDistance is float f12FC ? BitConverter.SingleToUInt32Bits(f12FC) : null,
            0x1300 => patch.LensSimulationInfiniteFocalLength is float f1300 ? BitConverter.SingleToUInt32Bits(f1300) : null,
            0x1304 => patch.LensSimulationFStop is float f1304 ? BitConverter.SingleToUInt32Bits(f1304) : null,
            0x1308 => patch.LensSimulationExposureDial is float f1308 ? BitConverter.SingleToUInt32Bits(f1308) : null,
            0x130C => patch.LensSimulationShutterSpeed is float f130C ? BitConverter.SingleToUInt32Bits(f130C) : null,
            0x1310 => patch.LensSimulationColorTemperature,
            0x1314 => patch.LensSimulationColorSaturation is float f1314 ? BitConverter.SingleToUInt32Bits(f1314) : null,
            0x1318 => patch.LensSimulationSunRadius is float f1318 ? BitConverter.SingleToUInt32Bits(f1318) : null,
            0x131C => patch.LensSimulationAnamorphicFlag,
            0x1320 => patch.DepthOfFieldFlags,
            0x1324 => patch.DepthOfFieldSpriteThreshold is float f1324 ? BitConverter.SingleToUInt32Bits(f1324) : null,
            0x1328 => patch.DepthOfFieldCompareBrightnessBits,
            0x132C => patch.LensSimulationObjectDistanceFlags,
            0x1330 => patch.TemporalAaVelocityWeight is float f1330 ? BitConverter.SingleToUInt32Bits(f1330) : null,
            0x1334 => patch.TemporalAaDepthDiffThreshold is float f1334 ? BitConverter.SingleToUInt32Bits(f1334) : null,
            0x1338 => patch.TemporalAaBaseWeight is float f1338 ? BitConverter.SingleToUInt32Bits(f1338) : null,
            0x133C => patch.TemporalAaEnable,
            0x1340 => patch.LensEffectsGhostScale is float f1340 ? BitConverter.SingleToUInt32Bits(f1340) : null,
            0x1344 => patch.LensEffectsGhostNum,
            0x1348 => patch.LensEffectsDiaphragmBladeCounts,
            0x134C => patch.LensEffectsHaloScale is float f134C ? BitConverter.SingleToUInt32Bits(f134C) : null,
            0x1350 => patch.LensEffectsBloomScale is float f1350 ? BitConverter.SingleToUInt32Bits(f1350) : null,
            0x1354 => patch.LensEffectsBloomThreshold is float f1354 ? BitConverter.SingleToUInt32Bits(f1354) : null,
            0x1358 => patch.LensEffectsBloomThresholdSoftKnee is float f1358 ? BitConverter.SingleToUInt32Bits(f1358) : null,
            0x135C => patch.LensEffectsBloomRadius is float f135C ? BitConverter.SingleToUInt32Bits(f135C) : null,
            0x1360 => patch.LensEffectsLensFlareThreshold is float f1360 ? BitConverter.SingleToUInt32Bits(f1360) : null,
            0x1364 => patch.LensEffectsVignettingFade is float f1364 ? BitConverter.SingleToUInt32Bits(f1364) : null,
            0x1368 => patch.LensEffectsLowLuminanceBloomingFix,
            0x136C => patch.ExposureMinBrightness is float f136C ? BitConverter.SingleToUInt32Bits(f136C) : null,
            0x1370 => patch.ExposureMaxBrightness is float f1370 ? BitConverter.SingleToUInt32Bits(f1370) : null,
            0x1374 => patch.ExposureLowBrightnessPercent is float f1374 ? BitConverter.SingleToUInt32Bits(f1374) : null,
            0x1378 => patch.ExposureHighBrightnessPercent is float f1378 ? BitConverter.SingleToUInt32Bits(f1378) : null,
            0x137C => patch.LensEffectsAberraType,
            0x1380 => patch.LensEffectsAberraAmount is float f1380 ? BitConverter.SingleToUInt32Bits(f1380) : null,
            0x1384 => patch.ColorGradingLdrGradingIntensity is float f1384 ? BitConverter.SingleToUInt32Bits(f1384) : null,
            0x1388 => patch.ColorGradingLutTransitionSpan is float f1388 ? BitConverter.SingleToUInt32Bits(f1388) : null,
            0x139C => patch.LensEffectsCrossFilterWeight is float f139C ? BitConverter.SingleToUInt32Bits(f139C) : null,
            0x13A0 => patch.LensEffectsCrossFilterLength is float f13A0 ? BitConverter.SingleToUInt32Bits(f13A0) : null,
            0x13A4 => patch.LensEffectsCrossFilterAttenuation is float f13A4 ? BitConverter.SingleToUInt32Bits(f13A4) : null,
            0x13A8 => patch.LensEffectsCrossFilterInclination is float f13A8 ? BitConverter.SingleToUInt32Bits(f13A8) : null,
            0x13AC => patch.LensEffectsCrossFilterAberration is float f13AC ? BitConverter.SingleToUInt32Bits(f13AC) : null,
            0x13B0 => patch.ColorGradingLut1,
            0x13B4 => patch.ColorGradingLut2,
            0x13B8 => patch.ColorGradingInterpolationRate is float f13B8 ? BitConverter.SingleToUInt32Bits(f13B8) : null,
            0x13BC => patch.ColorGradingIntensity is float f13BC ? BitConverter.SingleToUInt32Bits(f13BC) : null,
            0x13C0 => patch.HeatShimmerVibra is float f13C0 ? BitConverter.SingleToUInt32Bits(f13C0) : null,
            0x13C4 => patch.HeatShimmerCycle is float f13C4 ? BitConverter.SingleToUInt32Bits(f13C4) : null,
            0x13C8 => patch.HeatShimmerSpeed is float f13C8 ? BitConverter.SingleToUInt32Bits(f13C8) : null,
            0x13CC => patch.ColorGradingHdrTvNit,
            0x13D0 => patch.ColorGradingBaseTvNit,
            0x13D4 => patch.ColorGradingTonemapType,
            0x13D8 => patch.ColorGradingNeutralBlackIn is float f13D8 ? BitConverter.SingleToUInt32Bits(f13D8) : null,
            0x13DC => patch.ColorGradingNeutralBlackOut is float f13DC ? BitConverter.SingleToUInt32Bits(f13DC) : null,
            _ => null
        };
        bits = value.GetValueOrDefault();
        return value.HasValue;
    }
    public static uint Read(CameraOpticsSettings settings, int encodedOffset) => encodedOffset switch
    {
        0x170 => BitConverter.SingleToUInt32Bits(settings.Exposure.UserScale),
        0x174 => BitConverter.SingleToUInt32Bits(settings.Exposure.Gamma),
        0x17C => BitConverter.SingleToUInt32Bits(settings.Exposure.Luminance),
        0x180 => BitConverter.SingleToUInt32Bits(settings.Output.BloomIntensity),
        0x184 => BitConverter.SingleToUInt32Bits(settings.Output.StarIntensity),
        0x188 => BitConverter.SingleToUInt32Bits(settings.Output.LensFlareIntensity),
        0x18C => BitConverter.SingleToUInt32Bits(settings.Output.ColorScaleX),
        0x190 => BitConverter.SingleToUInt32Bits(settings.Output.ColorScaleY),
        0x194 => BitConverter.SingleToUInt32Bits(settings.Output.ColorScaleZ),
        0x198 => BitConverter.SingleToUInt32Bits(settings.Output.SaturationScaleX),
        0x19C => BitConverter.SingleToUInt32Bits(settings.Output.SaturationScaleY),
        0x1A0 => BitConverter.SingleToUInt32Bits(settings.Output.SaturationScaleZ),
        0x1B8 => BitConverter.SingleToUInt32Bits(settings.Exposure.RangeMinimum),
        0x1BC => BitConverter.SingleToUInt32Bits(settings.Exposure.RangeMaximum),
        0x3C0 => settings.Surface.SpecularOcclusionType,
        0x404 => BitConverter.SingleToUInt32Bits(settings.Fxaa.EdgeThreshold),
        0x408 => BitConverter.SingleToUInt32Bits(settings.Fxaa.EdgeThresholdMin),
        0x40C => BitConverter.SingleToUInt32Bits(settings.Fxaa.EdgeSharpness),
        0x410 => BitConverter.SingleToUInt32Bits(settings.Fxaa.PixelRange),
        0x414 => settings.Fxaa.Enable,
        0x418 => settings.Fxaa.Type,
        0x41C => BitConverter.SingleToUInt32Bits(settings.Fxaa.QualityEdgeThreshold),
        0x420 => BitConverter.SingleToUInt32Bits(settings.Fxaa.QualityEdgeThresholdMin),
        0x424 => BitConverter.SingleToUInt32Bits(settings.Fxaa.QualitySubpixSharpness),
        0x428 => settings.Fxaa.QualityLevel,
        0x42C => settings.Fxaa.QualityPerformance,
        0x7A8 => BitConverter.SingleToUInt32Bits(settings.Limb.Start),
        0x7AC => BitConverter.SingleToUInt32Bits(settings.Limb.End),
        0x7B0 => BitConverter.SingleToUInt32Bits(settings.Limb.Weight),
        0xE2C => BitConverter.SingleToUInt32Bits(settings.LightShaft.NoiseScale),
        0xE30 => BitConverter.SingleToUInt32Bits(settings.LightShaft.NoiseFrequency),
        0xE34 => BitConverter.SingleToUInt32Bits(settings.LightShaft.NoiseWeight),
        0xE78 => BitConverter.SingleToUInt32Bits(settings.DepthOfField.DistantBlurZThreshold),
        0xE7C => BitConverter.SingleToUInt32Bits(settings.DepthOfField.DistantBlurIntensity),
        0xEB8 => settings.ColorGrading.HdrLutKey,
        0xEBC => settings.ColorGrading.LdrLutKey,
        0x12FC => BitConverter.SingleToUInt32Bits(settings.LensSimulation.ObjectDistance),
        0x1300 => BitConverter.SingleToUInt32Bits(settings.LensSimulation.InfiniteFocalLength),
        0x1304 => BitConverter.SingleToUInt32Bits(settings.LensSimulation.FStop),
        0x1308 => BitConverter.SingleToUInt32Bits(settings.LensSimulation.ExposureDial),
        0x130C => BitConverter.SingleToUInt32Bits(settings.LensSimulation.ShutterSpeed),
        0x1310 => settings.LensSimulation.ColorTemperature,
        0x1314 => BitConverter.SingleToUInt32Bits(settings.LensSimulation.ColorSaturation),
        0x1318 => BitConverter.SingleToUInt32Bits(settings.LensSimulation.SunRadius),
        0x131C => settings.LensSimulation.AnamorphicFlag,
        0x1320 => settings.DepthOfField.Flags,
        0x1324 => BitConverter.SingleToUInt32Bits(settings.DepthOfField.SpriteThreshold),
        0x1328 => settings.DepthOfField.CompareBrightnessBits,
        0x132C => settings.LensSimulation.ObjectDistanceFlags,
        0x1330 => BitConverter.SingleToUInt32Bits(settings.TemporalAa.VelocityWeight),
        0x1334 => BitConverter.SingleToUInt32Bits(settings.TemporalAa.DepthDiffThreshold),
        0x1338 => BitConverter.SingleToUInt32Bits(settings.TemporalAa.BaseWeight),
        0x133C => settings.TemporalAa.Enable,
        0x1340 => BitConverter.SingleToUInt32Bits(settings.LensEffects.GhostScale),
        0x1344 => settings.LensEffects.GhostNum,
        0x1348 => settings.LensEffects.DiaphragmBladeCounts,
        0x134C => BitConverter.SingleToUInt32Bits(settings.LensEffects.HaloScale),
        0x1350 => BitConverter.SingleToUInt32Bits(settings.LensEffects.BloomScale),
        0x1354 => BitConverter.SingleToUInt32Bits(settings.LensEffects.BloomThreshold),
        0x1358 => BitConverter.SingleToUInt32Bits(settings.LensEffects.BloomThresholdSoftKnee),
        0x135C => BitConverter.SingleToUInt32Bits(settings.LensEffects.BloomRadius),
        0x1360 => BitConverter.SingleToUInt32Bits(settings.LensEffects.LensFlareThreshold),
        0x1364 => BitConverter.SingleToUInt32Bits(settings.LensEffects.VignettingFade),
        0x1368 => settings.LensEffects.LowLuminanceBloomingFix,
        0x136C => BitConverter.SingleToUInt32Bits(settings.Exposure.MinBrightness),
        0x1370 => BitConverter.SingleToUInt32Bits(settings.Exposure.MaxBrightness),
        0x1374 => BitConverter.SingleToUInt32Bits(settings.Exposure.LowBrightnessPercent),
        0x1378 => BitConverter.SingleToUInt32Bits(settings.Exposure.HighBrightnessPercent),
        0x137C => settings.LensEffects.AberraType,
        0x1380 => BitConverter.SingleToUInt32Bits(settings.LensEffects.AberraAmount),
        0x1384 => BitConverter.SingleToUInt32Bits(settings.ColorGrading.LdrGradingIntensity),
        0x1388 => BitConverter.SingleToUInt32Bits(settings.ColorGrading.LutTransitionSpan),
        0x139C => BitConverter.SingleToUInt32Bits(settings.LensEffects.CrossFilterWeight),
        0x13A0 => BitConverter.SingleToUInt32Bits(settings.LensEffects.CrossFilterLength),
        0x13A4 => BitConverter.SingleToUInt32Bits(settings.LensEffects.CrossFilterAttenuation),
        0x13A8 => BitConverter.SingleToUInt32Bits(settings.LensEffects.CrossFilterInclination),
        0x13AC => BitConverter.SingleToUInt32Bits(settings.LensEffects.CrossFilterAberration),
        0x13B0 => settings.ColorGrading.Lut1,
        0x13B4 => settings.ColorGrading.Lut2,
        0x13B8 => BitConverter.SingleToUInt32Bits(settings.ColorGrading.InterpolationRate),
        0x13BC => BitConverter.SingleToUInt32Bits(settings.ColorGrading.Intensity),
        0x13C0 => BitConverter.SingleToUInt32Bits(settings.HeatShimmer.Vibra),
        0x13C4 => BitConverter.SingleToUInt32Bits(settings.HeatShimmer.Cycle),
        0x13C8 => BitConverter.SingleToUInt32Bits(settings.HeatShimmer.Speed),
        0x13CC => settings.ColorGrading.HdrTvNit,
        0x13D0 => settings.ColorGrading.BaseTvNit,
        0x13D4 => settings.ColorGrading.TonemapType,
        0x13D8 => BitConverter.SingleToUInt32Bits(settings.ColorGrading.NeutralBlackIn),
        0x13DC => BitConverter.SingleToUInt32Bits(settings.ColorGrading.NeutralBlackOut),
        _ => throw new InvalidDataException("Camera optics do not own the requested encoded field.")
    };
    public static IEnumerable<KeyValuePair<int, uint>> WrittenWords(CameraOpticsPatch patch)
    {
        if (TryRead(patch, 0x170, out uint v170)) yield return new(0x170, v170);
        if (TryRead(patch, 0x174, out uint v174)) yield return new(0x174, v174);
        if (TryRead(patch, 0x17C, out uint v17C)) yield return new(0x17C, v17C);
        if (TryRead(patch, 0x180, out uint v180)) yield return new(0x180, v180);
        if (TryRead(patch, 0x184, out uint v184)) yield return new(0x184, v184);
        if (TryRead(patch, 0x188, out uint v188)) yield return new(0x188, v188);
        if (TryRead(patch, 0x18C, out uint v18C)) yield return new(0x18C, v18C);
        if (TryRead(patch, 0x190, out uint v190)) yield return new(0x190, v190);
        if (TryRead(patch, 0x194, out uint v194)) yield return new(0x194, v194);
        if (TryRead(patch, 0x198, out uint v198)) yield return new(0x198, v198);
        if (TryRead(patch, 0x19C, out uint v19C)) yield return new(0x19C, v19C);
        if (TryRead(patch, 0x1A0, out uint v1A0)) yield return new(0x1A0, v1A0);
        if (TryRead(patch, 0x1B8, out uint v1B8)) yield return new(0x1B8, v1B8);
        if (TryRead(patch, 0x1BC, out uint v1BC)) yield return new(0x1BC, v1BC);
        if (TryRead(patch, 0x3C0, out uint v3C0)) yield return new(0x3C0, v3C0);
        if (TryRead(patch, 0x404, out uint v404)) yield return new(0x404, v404);
        if (TryRead(patch, 0x408, out uint v408)) yield return new(0x408, v408);
        if (TryRead(patch, 0x40C, out uint v40C)) yield return new(0x40C, v40C);
        if (TryRead(patch, 0x410, out uint v410)) yield return new(0x410, v410);
        if (TryRead(patch, 0x414, out uint v414)) yield return new(0x414, v414);
        if (TryRead(patch, 0x418, out uint v418)) yield return new(0x418, v418);
        if (TryRead(patch, 0x41C, out uint v41C)) yield return new(0x41C, v41C);
        if (TryRead(patch, 0x420, out uint v420)) yield return new(0x420, v420);
        if (TryRead(patch, 0x424, out uint v424)) yield return new(0x424, v424);
        if (TryRead(patch, 0x428, out uint v428)) yield return new(0x428, v428);
        if (TryRead(patch, 0x42C, out uint v42C)) yield return new(0x42C, v42C);
        if (TryRead(patch, 0x7A8, out uint v7A8)) yield return new(0x7A8, v7A8);
        if (TryRead(patch, 0x7AC, out uint v7AC)) yield return new(0x7AC, v7AC);
        if (TryRead(patch, 0x7B0, out uint v7B0)) yield return new(0x7B0, v7B0);
        if (TryRead(patch, 0xE2C, out uint vE2C)) yield return new(0xE2C, vE2C);
        if (TryRead(patch, 0xE30, out uint vE30)) yield return new(0xE30, vE30);
        if (TryRead(patch, 0xE34, out uint vE34)) yield return new(0xE34, vE34);
        if (TryRead(patch, 0xE78, out uint vE78)) yield return new(0xE78, vE78);
        if (TryRead(patch, 0xE7C, out uint vE7C)) yield return new(0xE7C, vE7C);
        if (TryRead(patch, 0xEB8, out uint vEB8)) yield return new(0xEB8, vEB8);
        if (TryRead(patch, 0xEBC, out uint vEBC)) yield return new(0xEBC, vEBC);
        if (TryRead(patch, 0x12FC, out uint v12FC)) yield return new(0x12FC, v12FC);
        if (TryRead(patch, 0x1300, out uint v1300)) yield return new(0x1300, v1300);
        if (TryRead(patch, 0x1304, out uint v1304)) yield return new(0x1304, v1304);
        if (TryRead(patch, 0x1308, out uint v1308)) yield return new(0x1308, v1308);
        if (TryRead(patch, 0x130C, out uint v130C)) yield return new(0x130C, v130C);
        if (TryRead(patch, 0x1310, out uint v1310)) yield return new(0x1310, v1310);
        if (TryRead(patch, 0x1314, out uint v1314)) yield return new(0x1314, v1314);
        if (TryRead(patch, 0x1318, out uint v1318)) yield return new(0x1318, v1318);
        if (TryRead(patch, 0x131C, out uint v131C)) yield return new(0x131C, v131C);
        if (TryRead(patch, 0x1320, out uint v1320)) yield return new(0x1320, v1320);
        if (TryRead(patch, 0x1324, out uint v1324)) yield return new(0x1324, v1324);
        if (TryRead(patch, 0x1328, out uint v1328)) yield return new(0x1328, v1328);
        if (TryRead(patch, 0x132C, out uint v132C)) yield return new(0x132C, v132C);
        if (TryRead(patch, 0x1330, out uint v1330)) yield return new(0x1330, v1330);
        if (TryRead(patch, 0x1334, out uint v1334)) yield return new(0x1334, v1334);
        if (TryRead(patch, 0x1338, out uint v1338)) yield return new(0x1338, v1338);
        if (TryRead(patch, 0x133C, out uint v133C)) yield return new(0x133C, v133C);
        if (TryRead(patch, 0x1340, out uint v1340)) yield return new(0x1340, v1340);
        if (TryRead(patch, 0x1344, out uint v1344)) yield return new(0x1344, v1344);
        if (TryRead(patch, 0x1348, out uint v1348)) yield return new(0x1348, v1348);
        if (TryRead(patch, 0x134C, out uint v134C)) yield return new(0x134C, v134C);
        if (TryRead(patch, 0x1350, out uint v1350)) yield return new(0x1350, v1350);
        if (TryRead(patch, 0x1354, out uint v1354)) yield return new(0x1354, v1354);
        if (TryRead(patch, 0x1358, out uint v1358)) yield return new(0x1358, v1358);
        if (TryRead(patch, 0x135C, out uint v135C)) yield return new(0x135C, v135C);
        if (TryRead(patch, 0x1360, out uint v1360)) yield return new(0x1360, v1360);
        if (TryRead(patch, 0x1364, out uint v1364)) yield return new(0x1364, v1364);
        if (TryRead(patch, 0x1368, out uint v1368)) yield return new(0x1368, v1368);
        if (TryRead(patch, 0x136C, out uint v136C)) yield return new(0x136C, v136C);
        if (TryRead(patch, 0x1370, out uint v1370)) yield return new(0x1370, v1370);
        if (TryRead(patch, 0x1374, out uint v1374)) yield return new(0x1374, v1374);
        if (TryRead(patch, 0x1378, out uint v1378)) yield return new(0x1378, v1378);
        if (TryRead(patch, 0x137C, out uint v137C)) yield return new(0x137C, v137C);
        if (TryRead(patch, 0x1380, out uint v1380)) yield return new(0x1380, v1380);
        if (TryRead(patch, 0x1384, out uint v1384)) yield return new(0x1384, v1384);
        if (TryRead(patch, 0x1388, out uint v1388)) yield return new(0x1388, v1388);
        if (TryRead(patch, 0x139C, out uint v139C)) yield return new(0x139C, v139C);
        if (TryRead(patch, 0x13A0, out uint v13A0)) yield return new(0x13A0, v13A0);
        if (TryRead(patch, 0x13A4, out uint v13A4)) yield return new(0x13A4, v13A4);
        if (TryRead(patch, 0x13A8, out uint v13A8)) yield return new(0x13A8, v13A8);
        if (TryRead(patch, 0x13AC, out uint v13AC)) yield return new(0x13AC, v13AC);
        if (TryRead(patch, 0x13B0, out uint v13B0)) yield return new(0x13B0, v13B0);
        if (TryRead(patch, 0x13B4, out uint v13B4)) yield return new(0x13B4, v13B4);
        if (TryRead(patch, 0x13B8, out uint v13B8)) yield return new(0x13B8, v13B8);
        if (TryRead(patch, 0x13BC, out uint v13BC)) yield return new(0x13BC, v13BC);
        if (TryRead(patch, 0x13C0, out uint v13C0)) yield return new(0x13C0, v13C0);
        if (TryRead(patch, 0x13C4, out uint v13C4)) yield return new(0x13C4, v13C4);
        if (TryRead(patch, 0x13C8, out uint v13C8)) yield return new(0x13C8, v13C8);
        if (TryRead(patch, 0x13CC, out uint v13CC)) yield return new(0x13CC, v13CC);
        if (TryRead(patch, 0x13D0, out uint v13D0)) yield return new(0x13D0, v13D0);
        if (TryRead(patch, 0x13D4, out uint v13D4)) yield return new(0x13D4, v13D4);
        if (TryRead(patch, 0x13D8, out uint v13D8)) yield return new(0x13D8, v13D8);
        if (TryRead(patch, 0x13DC, out uint v13DC)) yield return new(0x13DC, v13DC);
    }
    public static IReadOnlyList<int> PostTailOffsets { get; } = Array.AsReadOnly(new int[] { 0x1320, 0x1324, 0x1328, 0x132C, 0x1330, 0x1334, 0x1338, 0x133C, 0x1340, 0x1344, 0x1348, 0x134C, 0x1350, 0x1354, 0x1358, 0x135C, 0x1360, 0x1364, 0x1368, 0x136C, 0x1370, 0x1374, 0x1378, 0x137C, 0x1380, 0x1384, 0x1388, 0x139C, 0x13A0, 0x13A4, 0x13A8, 0x13AC, 0x13B0, 0x13B4, 0x13B8, 0x13BC, 0x13C0, 0x13C4, 0x13C8, 0x13CC, 0x13D0, 0x13D4, 0x13D8, 0x13DC });
    public static IReadOnlyList<int> UncontrolledOffsets { get; } = Array.AsReadOnly(new int[] { 0x7A8, 0x7AC, 0x414, 0x418, 0x41C, 0x420, 0x424, 0x428, 0x42C, 0xE2C, 0xE30, 0xE34, 0x1300, 0x1304, 0x1308, 0x130C, 0x1310, 0x1314, 0x1318, 0x131C, 0x12FC });
    public static uint ReadPostTail(CameraOpticsSettings settings, int encodedOffset)
    {
        if (!PostTailOffsets.Contains(encodedOffset)) throw new InvalidDataException("The encoded post tail does not own this field.");
        return Read(settings, encodedOffset);
    }
    public static uint ReadUncontrolled(CameraOpticsSettings settings, int encodedOffset)
    {
        if (!UncontrolledOffsets.Contains(encodedOffset)) throw new NotSupportedException("The encoded uncontrolled input does not own this field.");
        return Read(settings, encodedOffset);
    }
    public static ReadOnlyMemory<byte> ReadUncontrolledRange(MapCameraConstructionPacket camera, int offset, int count)
    {
        if (offset < 0 || count <= 0 || offset % 4 != 0 || count % 4 != 0 || offset > int.MaxValue - count)
            throw new InvalidDataException("Encoded camera range is invalid.");
        if (offset + count <= camera.Prefix.Length) return camera.Prefix.Slice(offset, count).ToArray();
        byte[] result = new byte[count];
        for (int at = 0; at < count; at += 4)
        {
            int current = offset + at;
            if (!UncontrolledOffsets.Contains(current) && !PostTailOffsets.Contains(current) && current is not (0x404 or 0x408 or 0x40C or 0x410))
                throw new NotSupportedException("Encoded camera range contains a field with no optical producer.");
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(at, 4), Read(camera.Optics, current));
        }
        return result;
    }
}
