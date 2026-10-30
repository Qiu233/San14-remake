using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Read scalar resource properties once for a loaded immutable camera generation.</summary>
internal static class CameraOpticsImport
{
    private sealed class DefaultsValue(LoadedMapCameraConstructionSource source)
    {
        internal readonly CameraOpticsSettings Settings = ReadDefaults(source);
    }
    private sealed class SeasonalValues(MapSeasonCameraPackage source)
    {
        internal readonly CameraOpticsPatch Direct = ReadDirect(source.Camera);
        internal CameraOpticsPatch? Current;
    }
    private static readonly ConditionalWeakTable<LoadedMapCameraConstructionSource, DefaultsValue> defaults = new();
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, SeasonalValues> seasonal = new();
    internal static CameraOpticsSettings Defaults(LoadedMapCameraConstructionSource source) => defaults.GetValue(source, static s => new(s)).Settings;
    internal static CameraOpticsPatch Direct(MapSeasonCameraPackage source) => seasonal.GetValue(source, static s => new(s)).Direct;
    internal static CameraOpticsPatch Current(MapSeasonCameraPackage source)
    {
        var values = seasonal.GetValue(source, static s => new(s));
        lock (values) return values.Current ??= ReadCurrent(source.Camera);
    }
    private static CameraOpticsSettings ReadDefaults(LoadedMapCameraConstructionSource source)
    {
        var camera = source.Camera;
        var patch = new CameraOpticsPatch
        {
            ExposureUserScale = BitConverter.UInt32BitsToSingle(Read(camera, 9, 0xB90D1027u, 8, 1, 0)),
            ExposureGamma = BitConverter.UInt32BitsToSingle(Read(camera, 10, 0xABB91759u, 8, 1, 0)),
            ExposureLuminance = BitConverter.UInt32BitsToSingle(Read(camera, 88, 0x462994C1u, 8, 1, 0)),
            OutputBloomIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 12, 0xC96AEEA5u, 8, 1, 0)),
            OutputStarIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 13, 0x721A508Au, 8, 1, 0)),
            OutputLensFlareIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 14, 0xA0E5DA98u, 8, 1, 0)),
            OutputColorScaleX = BitConverter.UInt32BitsToSingle(Read(camera, 15, 0x16B54787u, 8, 3, 0)),
            OutputColorScaleY = BitConverter.UInt32BitsToSingle(Read(camera, 15, 0x16B54787u, 8, 3, 1)),
            OutputColorScaleZ = BitConverter.UInt32BitsToSingle(Read(camera, 15, 0x16B54787u, 8, 3, 2)),
            OutputSaturationScaleX = BitConverter.UInt32BitsToSingle(Read(camera, 16, 0xD40D74C8u, 8, 3, 0)),
            OutputSaturationScaleY = BitConverter.UInt32BitsToSingle(Read(camera, 16, 0xD40D74C8u, 8, 3, 1)),
            OutputSaturationScaleZ = BitConverter.UInt32BitsToSingle(Read(camera, 16, 0xD40D74C8u, 8, 3, 2)),
            ExposureRangeMinimum = BitConverter.UInt32BitsToSingle(Read(camera, 29, 0xF9989A1Cu, 8, 1, 0)),
            ExposureRangeMaximum = BitConverter.UInt32BitsToSingle(Read(camera, 30, 0x13AC572Eu, 8, 1, 0)),
            SurfaceSpecularOcclusionType = Read(camera, 956, 0xDFC9D831u, 4, 1, 0),
            FxaaEdgeThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 100, 0x307216E4u, 8, 1, 0)),
            FxaaEdgeThresholdMin = BitConverter.UInt32BitsToSingle(Read(camera, 101, 0x77CBE856u, 8, 1, 0)),
            FxaaEdgeSharpness = BitConverter.UInt32BitsToSingle(Read(camera, 102, 0x350DC6CEu, 8, 1, 0)),
            FxaaPixelRange = BitConverter.UInt32BitsToSingle(Read(camera, 103, 0xE9D823E9u, 8, 1, 0)),
            FxaaEnable = Read(camera, 691, 0x20196D95u, 5, 1, 0),
            FxaaType = Read(camera, 1025, 0x855F598Cu, 4, 1, 0),
            FxaaQualityEdgeThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 1026, 0x54A05D41u, 8, 1, 0)),
            FxaaQualityEdgeThresholdMin = BitConverter.UInt32BitsToSingle(Read(camera, 1027, 0xDF0F678Fu, 8, 1, 0)),
            FxaaQualitySubpixSharpness = BitConverter.UInt32BitsToSingle(Read(camera, 1028, 0xBEB97EB5u, 8, 1, 0)),
            FxaaQualityLevel = Read(camera, 1029, 0x4DCC7F17u, 4, 1, 0),
            FxaaQualityPerformance = Read(camera, 1030, 0x07D11283u, 4, 1, 0),
            LimbStart = BitConverter.UInt32BitsToSingle(Read(camera, 49, 0x4226C411u, 8, 1, 0)),
            LimbEnd = BitConverter.UInt32BitsToSingle(Read(camera, 50, 0x468E5E0Au, 8, 1, 0)),
            LimbWeight = BitConverter.UInt32BitsToSingle(Read(camera, 51, 0xD66B33B7u, 8, 1, 0)),
            LightShaftNoiseScale = BitConverter.UInt32BitsToSingle(Read(camera, 137, 0xAD7E9994u, 8, 1, 0)),
            LightShaftNoiseFrequency = BitConverter.UInt32BitsToSingle(Read(camera, 138, 0x834DC2A6u, 8, 1, 0)),
            LightShaftNoiseWeight = BitConverter.UInt32BitsToSingle(Read(camera, 139, 0x7AD72492u, 8, 1, 0)),
            DepthOfFieldDistantBlurZThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 373, 0x09D8471Eu, 8, 1, 0)),
            DepthOfFieldDistantBlurIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 374, 0x4CFD293Au, 8, 1, 0)),
            ColorGradingHdrLutKey = Read(camera, 932, 0x2572A4E9u, 5, 1, 0),
            ColorGradingLdrLutKey = Read(camera, 933, 0xDF7EBEEDu, 5, 1, 0),
            LensSimulationObjectDistance = BitConverter.UInt32BitsToSingle(Read(camera, 274, 0xEE9D93F9u, 8, 1, 0)),
            LensSimulationInfiniteFocalLength = BitConverter.UInt32BitsToSingle(Read(camera, 275, 0x995A7B0Eu, 8, 1, 0)),
            LensSimulationFStop = BitConverter.UInt32BitsToSingle(Read(camera, 276, 0x04CFDD5Du, 8, 1, 0)),
            LensSimulationExposureDial = BitConverter.UInt32BitsToSingle(Read(camera, 277, 0x6934EE7Cu, 8, 1, 0)),
            LensSimulationShutterSpeed = BitConverter.UInt32BitsToSingle(Read(camera, 278, 0xF2B45C7Fu, 8, 1, 0)),
            LensSimulationColorTemperature = Read(camera, 279, 0x28A60BB6u, 4, 1, 0),
            LensSimulationColorSaturation = BitConverter.UInt32BitsToSingle(Read(camera, 280, 0x39FE6CF0u, 8, 1, 0)),
            LensSimulationSunRadius = BitConverter.UInt32BitsToSingle(Read(camera, 290, 0xEB8B5927u, 8, 1, 0)),
            LensSimulationAnamorphicFlag = Read(camera, 292, 0xA925B2FFu, 4, 1, 0),
            DepthOfFieldFlags = Read(camera, 929, 0x03C0227Eu, 4, 1, 0),
            DepthOfFieldSpriteThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 955, 0x8CC2593Au, 8, 1, 0)),
            DepthOfFieldCompareBrightnessBits = Read(camera, 984, 0x182ED000u, 4, 1, 0),
            LensSimulationObjectDistanceFlags = Read(camera, 315, 0xC651B23Au, 4, 1, 0),
            TemporalAaVelocityWeight = BitConverter.UInt32BitsToSingle(Read(camera, 418, 0x4DCEB3FCu, 8, 1, 0)),
            TemporalAaDepthDiffThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 419, 0xCC53256Au, 8, 1, 0)),
            TemporalAaBaseWeight = BitConverter.UInt32BitsToSingle(Read(camera, 420, 0x0E8A7670u, 8, 1, 0)),
            TemporalAaEnable = Read(camera, 421, 0xB406C48Au, 4, 1, 0),
            LensEffectsGhostScale = BitConverter.UInt32BitsToSingle(Read(camera, 454, 0x3278E4A0u, 8, 1, 0)),
            LensEffectsGhostNum = Read(camera, 959, 0x6B7FBFDCu, 4, 1, 0),
            LensEffectsDiaphragmBladeCounts = Read(camera, 970, 0x4C1DAA58u, 4, 1, 0),
            LensEffectsHaloScale = BitConverter.UInt32BitsToSingle(Read(camera, 455, 0x60B60FF7u, 8, 1, 0)),
            LensEffectsBloomScale = BitConverter.UInt32BitsToSingle(Read(camera, 456, 0x8130A3ACu, 8, 1, 0)),
            LensEffectsBloomThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 949, 0xC81F204Du, 8, 1, 0)),
            LensEffectsBloomThresholdSoftKnee = BitConverter.UInt32BitsToSingle(Read(camera, 950, 0x119CC5BAu, 8, 1, 0)),
            LensEffectsBloomRadius = BitConverter.UInt32BitsToSingle(Read(camera, 951, 0x69C17F10u, 8, 1, 0)),
            LensEffectsLensFlareThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 952, 0x80119D80u, 8, 1, 0)),
            LensEffectsVignettingFade = BitConverter.UInt32BitsToSingle(Read(camera, 457, 0x7729E0F8u, 8, 1, 0)),
            LensEffectsLowLuminanceBloomingFix = Read(camera, 605, 0x752C9273u, 4, 1, 0),
            ExposureMinBrightness = BitConverter.UInt32BitsToSingle(Read(camera, 943, 0x3F80B302u, 8, 1, 0)),
            ExposureMaxBrightness = BitConverter.UInt32BitsToSingle(Read(camera, 944, 0x67E49830u, 8, 1, 0)),
            ExposureLowBrightnessPercent = BitConverter.UInt32BitsToSingle(Read(camera, 945, 0xD66E4FA5u, 8, 1, 0)),
            ExposureHighBrightnessPercent = BitConverter.UInt32BitsToSingle(Read(camera, 946, 0x4C05D973u, 8, 1, 0)),
            LensEffectsAberraType = Read(camera, 947, 0x6DA5B0FCu, 4, 1, 0),
            LensEffectsAberraAmount = BitConverter.UInt32BitsToSingle(Read(camera, 948, 0x0286381Au, 8, 1, 0)),
            ColorGradingLdrGradingIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 953, 0x0D04C5ACu, 8, 1, 0)),
            ColorGradingLutTransitionSpan = BitConverter.UInt32BitsToSingle(Read(camera, 966, 0x84CB7A7Bu, 8, 1, 0)),
            LensEffectsCrossFilterWeight = BitConverter.UInt32BitsToSingle(Read(camera, 1067, 0x3A35E6F5u, 8, 1, 0)),
            LensEffectsCrossFilterLength = BitConverter.UInt32BitsToSingle(Read(camera, 1068, 0x2AF009C7u, 8, 1, 0)),
            LensEffectsCrossFilterAttenuation = BitConverter.UInt32BitsToSingle(Read(camera, 1069, 0x9DC863D7u, 8, 1, 0)),
            LensEffectsCrossFilterInclination = BitConverter.UInt32BitsToSingle(Read(camera, 1070, 0xE7F79A0Fu, 8, 1, 0)),
            LensEffectsCrossFilterAberration = BitConverter.UInt32BitsToSingle(Read(camera, 1071, 0x356841BCu, 8, 1, 0)),
            ColorGradingLut1 = Read(camera, 1092, 0xBC94863Eu, 4, 1, 0),
            ColorGradingLut2 = Read(camera, 1093, 0xF9B44A5Du, 4, 1, 0),
            ColorGradingInterpolationRate = BitConverter.UInt32BitsToSingle(Read(camera, 1094, 0xDD69E8A8u, 8, 1, 0)),
            ColorGradingIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 1095, 0xC33E91F7u, 8, 1, 0)),
            HeatShimmerVibra = BitConverter.UInt32BitsToSingle(Read(camera, 1103, 0xF2555D35u, 8, 1, 0)),
            HeatShimmerCycle = BitConverter.UInt32BitsToSingle(Read(camera, 1104, 0xC7585A9Du, 8, 1, 0)),
            HeatShimmerSpeed = BitConverter.UInt32BitsToSingle(Read(camera, 1105, 0x48ECAF7Eu, 8, 1, 0)),
            ColorGradingHdrTvNit = Read(camera, 1075, 0x2F101A92u, 5, 1, 0),
            ColorGradingBaseTvNit = Read(camera, 1076, 0x1658638Bu, 5, 1, 0),
            ColorGradingTonemapType = Read(camera, 1077, 0x0B3B7FADu, 5, 1, 0),
            ColorGradingNeutralBlackIn = BitConverter.UInt32BitsToSingle(Read(camera, 1078, 0xBA41042Eu, 8, 1, 0)),
            ColorGradingNeutralBlackOut = BitConverter.UInt32BitsToSingle(Read(camera, 1079, 0x7547C33Bu, 8, 1, 0)),
        };
        var settings = patch.ApplyTo(new());
        return settings with
        {
            ReflectionProbeRsmCaptureMode = ReadReflectionProbeCaptureMode(camera),
            LensSimulation = settings.LensSimulation with { SensorAspect = BitConverter.UInt32BitsToSingle(Read(camera, 6, 0xA70685F8u, 8, 1, 0)) },
            Exposure = settings.Exposure with
            {
                LightAdaptationScale = BitConverter.UInt32BitsToSingle(Read(camera, 54, 0x907B67C1u, 8, 1, 0)),
                DarkAdaptationScale = BitConverter.UInt32BitsToSingle(Read(camera, 55, 0x800DB8FFu, 8, 1, 0))
            }
        };
    }
    private static CameraOpticsPatch ReadDirect(San14.Data.Formats.DokObject camera)
    {
        var patch = new CameraOpticsPatch
        {
            LimbStart = BitConverter.UInt32BitsToSingle(Read(camera, 49, 0x4226C411u, 8, 1, 0, true, true)),
            LimbEnd = BitConverter.UInt32BitsToSingle(Read(camera, 50, 0x468E5E0Au, 8, 1, 0, true, true)),
            LimbWeight = BitConverter.UInt32BitsToSingle(Read(camera, 51, 0xD66B33B7u, 8, 1, 0, true, true)),
            ColorGradingHdrLutKey = Read(camera, 932, 0x2572A4E9u, 5, 1, 0, true, true),
            ColorGradingLdrLutKey = Read(camera, 933, 0xDF7EBEEDu, 5, 1, 0, true, true),
            LensSimulationObjectDistance = BitConverter.UInt32BitsToSingle(Read(camera, 274, 0xEE9D93F9u, 8, 1, 0, true, true)),
            LensSimulationInfiniteFocalLength = BitConverter.UInt32BitsToSingle(Read(camera, 275, 0x995A7B0Eu, 8, 1, 0, true, true)),
            LensSimulationFStop = BitConverter.UInt32BitsToSingle(Read(camera, 276, 0x04CFDD5Du, 8, 1, 0, true, true)),
            LensSimulationExposureDial = BitConverter.UInt32BitsToSingle(Read(camera, 277, 0x6934EE7Cu, 8, 1, 0, true, true)),
            LensSimulationShutterSpeed = BitConverter.UInt32BitsToSingle(Read(camera, 278, 0xF2B45C7Fu, 8, 1, 0, true, true)),
            DepthOfFieldFlags = Read(camera, 929, 0x03C0227Eu, 4, 1, 0, true, true),
            DepthOfFieldSpriteThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 955, 0x8CC2593Au, 8, 1, 0, true, true)),
            DepthOfFieldCompareBrightnessBits = Read(camera, 984, 0x182ED000u, 4, 1, 0, true, true),
            ExposureMinBrightness = BitConverter.UInt32BitsToSingle(Read(camera, 943, 0x3F80B302u, 8, 1, 0, true, true)),
            ExposureMaxBrightness = BitConverter.UInt32BitsToSingle(Read(camera, 944, 0x67E49830u, 8, 1, 0, true, true)),
        };
        return patch;
    }
    private static CameraOpticsPatch ReadCurrent(San14.Data.Formats.DokObject camera)
    {
        var patch = new CameraOpticsPatch
        {
            SurfaceSpecularOcclusionType = Read(camera, 956, 0xDFC9D831u, 4, 1, 0, true, false),
            FxaaEdgeThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 100, 0x307216E4u, 8, 1, 0, true, false)),
            FxaaEdgeThresholdMin = BitConverter.UInt32BitsToSingle(Read(camera, 101, 0x77CBE856u, 8, 1, 0, true, false)),
            FxaaEdgeSharpness = BitConverter.UInt32BitsToSingle(Read(camera, 102, 0x350DC6CEu, 8, 1, 0, true, false)),
            FxaaPixelRange = BitConverter.UInt32BitsToSingle(Read(camera, 103, 0xE9D823E9u, 8, 1, 0, true, false)),
            FxaaEnable = Read(camera, 691, 0x20196D95u, 5, 1, 0, true, false),
            FxaaType = Read(camera, 1025, 0x855F598Cu, 4, 1, 0, true, false),
            FxaaQualityEdgeThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 1026, 0x54A05D41u, 8, 1, 0, true, false)),
            FxaaQualityEdgeThresholdMin = BitConverter.UInt32BitsToSingle(Read(camera, 1027, 0xDF0F678Fu, 8, 1, 0, true, false)),
            FxaaQualitySubpixSharpness = BitConverter.UInt32BitsToSingle(Read(camera, 1028, 0xBEB97EB5u, 8, 1, 0, true, false)),
            FxaaQualityLevel = Read(camera, 1029, 0x4DCC7F17u, 4, 1, 0, true, false),
            FxaaQualityPerformance = Read(camera, 1030, 0x07D11283u, 4, 1, 0, true, false),
            LimbStart = BitConverter.UInt32BitsToSingle(Read(camera, 49, 0x4226C411u, 8, 1, 0, true, false)),
            LimbEnd = BitConverter.UInt32BitsToSingle(Read(camera, 50, 0x468E5E0Au, 8, 1, 0, true, false)),
            LimbWeight = BitConverter.UInt32BitsToSingle(Read(camera, 51, 0xD66B33B7u, 8, 1, 0, true, false)),
            LightShaftNoiseScale = BitConverter.UInt32BitsToSingle(Read(camera, 137, 0xAD7E9994u, 8, 1, 0, true, false)),
            LightShaftNoiseFrequency = BitConverter.UInt32BitsToSingle(Read(camera, 138, 0x834DC2A6u, 8, 1, 0, true, false)),
            LightShaftNoiseWeight = BitConverter.UInt32BitsToSingle(Read(camera, 139, 0x7AD72492u, 8, 1, 0, true, false)),
            DepthOfFieldDistantBlurZThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 373, 0x09D8471Eu, 8, 1, 0, true, false)),
            DepthOfFieldDistantBlurIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 374, 0x4CFD293Au, 8, 1, 0, true, false)),
            LensSimulationObjectDistance = BitConverter.UInt32BitsToSingle(Read(camera, 274, 0xEE9D93F9u, 8, 1, 0, true, false)),
            LensSimulationInfiniteFocalLength = BitConverter.UInt32BitsToSingle(Read(camera, 275, 0x995A7B0Eu, 8, 1, 0, true, false)),
            LensSimulationFStop = BitConverter.UInt32BitsToSingle(Read(camera, 276, 0x04CFDD5Du, 8, 1, 0, true, false)),
            LensSimulationExposureDial = BitConverter.UInt32BitsToSingle(Read(camera, 277, 0x6934EE7Cu, 8, 1, 0, true, false)),
            LensSimulationShutterSpeed = BitConverter.UInt32BitsToSingle(Read(camera, 278, 0xF2B45C7Fu, 8, 1, 0, true, false)),
            LensSimulationColorTemperature = Read(camera, 279, 0x28A60BB6u, 4, 1, 0, true, false),
            LensSimulationColorSaturation = BitConverter.UInt32BitsToSingle(Read(camera, 280, 0x39FE6CF0u, 8, 1, 0, true, false)),
            LensSimulationSunRadius = BitConverter.UInt32BitsToSingle(Read(camera, 290, 0xEB8B5927u, 8, 1, 0, true, false)),
            LensSimulationAnamorphicFlag = Read(camera, 292, 0xA925B2FFu, 4, 1, 0, true, false),
            DepthOfFieldFlags = Read(camera, 929, 0x03C0227Eu, 4, 1, 0, true, false),
            DepthOfFieldSpriteThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 955, 0x8CC2593Au, 8, 1, 0, true, false)),
            DepthOfFieldCompareBrightnessBits = Read(camera, 984, 0x182ED000u, 4, 1, 0, true, false),
            TemporalAaVelocityWeight = BitConverter.UInt32BitsToSingle(Read(camera, 418, 0x4DCEB3FCu, 8, 1, 0, true, false)),
            TemporalAaDepthDiffThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 419, 0xCC53256Au, 8, 1, 0, true, false)),
            TemporalAaBaseWeight = BitConverter.UInt32BitsToSingle(Read(camera, 420, 0x0E8A7670u, 8, 1, 0, true, false)),
            TemporalAaEnable = Read(camera, 421, 0xB406C48Au, 4, 1, 0, true, false),
            LensEffectsGhostScale = BitConverter.UInt32BitsToSingle(Read(camera, 454, 0x3278E4A0u, 8, 1, 0, true, false)),
            LensEffectsGhostNum = Read(camera, 959, 0x6B7FBFDCu, 4, 1, 0, true, false),
            LensEffectsDiaphragmBladeCounts = Read(camera, 970, 0x4C1DAA58u, 4, 1, 0, true, false),
            LensEffectsHaloScale = BitConverter.UInt32BitsToSingle(Read(camera, 455, 0x60B60FF7u, 8, 1, 0, true, false)),
            LensEffectsBloomScale = BitConverter.UInt32BitsToSingle(Read(camera, 456, 0x8130A3ACu, 8, 1, 0, true, false)),
            LensEffectsBloomThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 949, 0xC81F204Du, 8, 1, 0, true, false)),
            LensEffectsBloomThresholdSoftKnee = BitConverter.UInt32BitsToSingle(Read(camera, 950, 0x119CC5BAu, 8, 1, 0, true, false)),
            LensEffectsBloomRadius = BitConverter.UInt32BitsToSingle(Read(camera, 951, 0x69C17F10u, 8, 1, 0, true, false)),
            LensEffectsLensFlareThreshold = BitConverter.UInt32BitsToSingle(Read(camera, 952, 0x80119D80u, 8, 1, 0, true, false)),
            LensEffectsVignettingFade = BitConverter.UInt32BitsToSingle(Read(camera, 457, 0x7729E0F8u, 8, 1, 0, true, false)),
            LensEffectsLowLuminanceBloomingFix = Read(camera, 605, 0x752C9273u, 4, 1, 0, true, false),
            ExposureMinBrightness = BitConverter.UInt32BitsToSingle(Read(camera, 943, 0x3F80B302u, 8, 1, 0, true, false)),
            ExposureMaxBrightness = BitConverter.UInt32BitsToSingle(Read(camera, 944, 0x67E49830u, 8, 1, 0, true, false)),
            ExposureLowBrightnessPercent = BitConverter.UInt32BitsToSingle(Read(camera, 945, 0xD66E4FA5u, 8, 1, 0, true, false)),
            ExposureHighBrightnessPercent = BitConverter.UInt32BitsToSingle(Read(camera, 946, 0x4C05D973u, 8, 1, 0, true, false)),
            LensEffectsAberraType = Read(camera, 947, 0x6DA5B0FCu, 4, 1, 0, true, false),
            LensEffectsAberraAmount = BitConverter.UInt32BitsToSingle(Read(camera, 948, 0x0286381Au, 8, 1, 0, true, false)),
            ColorGradingLdrGradingIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 953, 0x0D04C5ACu, 8, 1, 0, true, false)),
            ColorGradingLutTransitionSpan = BitConverter.UInt32BitsToSingle(Read(camera, 966, 0x84CB7A7Bu, 8, 1, 0, true, false)),
            LensEffectsCrossFilterWeight = BitConverter.UInt32BitsToSingle(Read(camera, 1067, 0x3A35E6F5u, 8, 1, 0, true, false)),
            LensEffectsCrossFilterLength = BitConverter.UInt32BitsToSingle(Read(camera, 1068, 0x2AF009C7u, 8, 1, 0, true, false)),
            LensEffectsCrossFilterAttenuation = BitConverter.UInt32BitsToSingle(Read(camera, 1069, 0x9DC863D7u, 8, 1, 0, true, false)),
            LensEffectsCrossFilterInclination = BitConverter.UInt32BitsToSingle(Read(camera, 1070, 0xE7F79A0Fu, 8, 1, 0, true, false)),
            LensEffectsCrossFilterAberration = BitConverter.UInt32BitsToSingle(Read(camera, 1071, 0x356841BCu, 8, 1, 0, true, false)),
            ColorGradingLut1 = Read(camera, 1092, 0xBC94863Eu, 4, 1, 0, true, false),
            ColorGradingLut2 = Read(camera, 1093, 0xF9B44A5Du, 4, 1, 0, true, false),
            ColorGradingInterpolationRate = BitConverter.UInt32BitsToSingle(Read(camera, 1094, 0xDD69E8A8u, 8, 1, 0, true, false)),
            ColorGradingIntensity = BitConverter.UInt32BitsToSingle(Read(camera, 1095, 0xC33E91F7u, 8, 1, 0, true, false)),
            HeatShimmerVibra = BitConverter.UInt32BitsToSingle(Read(camera, 1103, 0xF2555D35u, 8, 1, 0, true, false)),
            HeatShimmerCycle = BitConverter.UInt32BitsToSingle(Read(camera, 1104, 0xC7585A9Du, 8, 1, 0, true, false)),
            HeatShimmerSpeed = BitConverter.UInt32BitsToSingle(Read(camera, 1105, 0x48ECAF7Eu, 8, 1, 0, true, false)),
            ColorGradingHdrTvNit = Read(camera, 1075, 0x2F101A92u, 5, 1, 0, true, false),
            ColorGradingBaseTvNit = Read(camera, 1076, 0x1658638Bu, 5, 1, 0, true, false),
            ColorGradingTonemapType = Read(camera, 1077, 0x0B3B7FADu, 5, 1, 0, true, false),
            ColorGradingNeutralBlackIn = BitConverter.UInt32BitsToSingle(Read(camera, 1078, 0xBA41042Eu, 8, 1, 0, true, false)),
            ColorGradingNeutralBlackOut = BitConverter.UInt32BitsToSingle(Read(camera, 1079, 0x7547C33Bu, 8, 1, 0, true, false)),
        };
        return patch;
    }
    private static uint ReadReflectionProbeCaptureMode(San14.Data.Formats.DokObject camera)
    {
        // C3BF60 initializes raw12CC to0. The native descriptor's ordinal1014
        // is not a serialized DOK index; missing named properties retain that
        // constructor value. Validate every present authored value exactly.
        var fields = camera.Properties.Where(property => property.NameHash == 0x01DCBC1Eu).ToArray();
        if (fields.Length == 0) return 0;
        if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 ||
            fields[0].ElementSize != 4 || fields[0].RawValue.Length != 4)
            throw new InvalidDataException("Camera ReflectionProbeRSMCaptureMode declaration differs from its supported scalar resource field.");
        return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
    }
    private static uint Read(San14.Data.Formats.DokObject camera, int ordinal, uint hash, uint type, int count, int component, bool requireFinite = false, bool requireUnique = false)
    {
        var field = camera.Properties[ordinal];
        if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != type || field.ElementCount != count || field.ElementSize != 4 || field.RawValue.Length != count * 4)
            throw new InvalidDataException("Camera optical declaration differs from its supported scalar resource field.");
        if (requireUnique && camera.Properties.Count(property => property.NameHash == hash) != 1)
            throw new InvalidDataException("Camera optical declaration contains a duplicate scalar property.");
        uint bits = BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(component * 4, 4));
        if (requireFinite && type == 8 && !float.IsFinite(BitConverter.UInt32BitsToSingle(bits)))
            throw new NotSupportedException("Selected camera optics require finite scalar inputs.");
        return bits;
    }
}
