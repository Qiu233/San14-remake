using System.Buffers.Binary;
using System.Collections.ObjectModel;

namespace San14.Data;

public sealed partial class OriginalMapSeasonCameraPostEffectScalars
{
    // The finite current reader census joins every binding to native V468/V418
    // DWORD routes. No geometry, pointers or noncopied1078 flag is included.
    internal OriginalMapSeasonCameraPostEffectScalars WithSelectedCurrentReaders(
        OriginalMapSeasonCameraSelection selected,OriginalMapCameraConstructionPacket camera)
    {
        ValidateForCamera(camera);selected.Validate();
        if(selected.SecondPackage is not null)throw new NotSupportedException("Current reader copies need a direct selected package.");
        var values=words.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        foreach(var binding in CurrentReaderBindings)
        {
            var field=selected.FirstPackage.Camera.Properties[binding.Ordinal];
            if(field.Index!=binding.Ordinal||field.NameHash!=binding.Hash||field.TypeCode!=binding.Type||
                field.ElementCount!=1||field.ElementSize!=4||field.RawValue.Length!=4)
                throw new InvalidDataException("Selected current reader changed its original scalar binding.");
            uint bits=BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
            if(binding.Type==8&&!float.IsFinite(BitConverter.UInt32BitsToSingle(bits)))
                throw new NotSupportedException("Selected current reader leaves the recovered finite scalar domain.");
            values[binding.Prepared]=bits;
        }
        return new(camera,new ReadOnlyDictionary<int,uint>(values),selectionEvidence+
            ";current-reader-census/72-exact-selected-DWORD-routes/V468-before-Main-post37;geometry/pointers/1078-separate");
    }
    private static readonly Binding[] CurrentReaderBindings=
    [
        new(0x3C0,956,0xDFC9D831,4), // SkylightSpecularOcclusionType
        new(0x404,100,0x307216E4,8), // FXAAEdgeThreshold
        new(0x408,101,0x77CBE856,8), // FXAAEdgeThresholdMin
        new(0x40C,102,0x350DC6CE,8), // FXAAEdgeSharpness
        new(0x410,103,0xE9D823E9,8), // FXAAPixelRange
        new(0x414,691,0x20196D95,5), // FXAAEnable
        new(0x418,1025,0x855F598C,4), // FXAAType
        new(0x41C,1026,0x54A05D41,8), // FXAAQualityEdgeThreshold
        new(0x420,1027,0xDF0F678F,8), // FXAAQualityEdgeThresholdMin
        new(0x424,1028,0xBEB97EB5,8), // FXAAQualitySubpixSharpness
        new(0x428,1029,0x4DCC7F17,4), // FXAAQualityLevel
        new(0x42C,1030,0x07D11283,4), // FXAAQualityPerformance
        new(0x7A8,49,0x4226C411,8), // LimbDarkeningStart
        new(0x7AC,50,0x468E5E0A,8), // LimbDarkeningEnd
        new(0x7B0,51,0xD66B33B7,8), // LimbDarkeningWeight
        new(0xE2C,137,0xAD7E9994,8), // LightShaftNoiseScale
        new(0xE30,138,0x834DC2A6,8), // LightShaftNoiseFrequency
        new(0xE34,139,0x7AD72492,8), // LightShaftNoiseWeight
        new(0xE78,373,0x09D8471E,8), // DOFDistantBlurZThreshold
        new(0xE7C,374,0x4CFD293A,8), // DOFDistantBlurIntensity
        new(0x12FC,274,0xEE9D93F9,8), // LensSimulationObjectDistance
        new(0x1300,275,0x995A7B0E,8), // LensSimulationInfiniteFocalLength
        new(0x1304,276,0x04CFDD5D,8), // LensSimulationFStop
        new(0x1308,277,0x6934EE7C,8), // LensSimulationExposureDial
        new(0x130C,278,0xF2B45C7F,8), // LensSimulationShutterSpeed
        new(0x1310,279,0x28A60BB6,4), // LensSimulationColorTemperature
        new(0x1314,280,0x39FE6CF0,8), // LensSimulationColorSaturation
        new(0x1318,290,0xEB8B5927,8), // LensSimulationSunRadius
        new(0x131C,292,0xA925B2FF,4), // LensSimulationAnamorphicFlag
        new(0x1320,929,0x03C0227E,4), // LensSimulationDOFFlag
        new(0x1324,955,0x8CC2593A,8), // LensSimulationSpriteDOFThreshold
        new(0x1328,984,0x182ED000,4), // LensSimulationDOFCompareBrightness
        new(0x1330,418,0x4DCEB3FC,8), // LensSimulationTemporalAAVelocityWeight
        new(0x1334,419,0xCC53256A,8), // LensSimulationTemporalAADepthDiffThreshold
        new(0x1338,420,0x0E8A7670,8), // LensSimulationTemporalAABaseWeight
        new(0x133C,421,0xB406C48A,4), // LensSimulationTemporalAAEnable
        new(0x1340,454,0x3278E4A0,8), // LensSimulationGhostScale
        new(0x1344,959,0x6B7FBFDC,4), // LensSimulationGhostNum
        new(0x1348,970,0x4C1DAA58,4), // LensSimulationDiaphragmBladeCounts
        new(0x134C,455,0x60B60FF7,8), // LensSimulationHaloScale
        new(0x1350,456,0x8130A3AC,8), // LensSimulationBloomScale
        new(0x1354,949,0xC81F204D,8), // LensSimulationBloomThreshold
        new(0x1358,950,0x119CC5BA,8), // LensSimulationBloomThresholdSoftKnee
        new(0x135C,951,0x69C17F10,8), // LensSimulationBloomRadius
        new(0x1360,952,0x80119D80,8), // LensSimulationLensFlareThreshold
        new(0x1364,457,0x7729E0F8,8), // LensSimulationVignettingFade
        new(0x1368,605,0x752C9273,4), // LensSimulationLowLuminanceBloomingFix
        new(0x136C,943,0x3F80B302,8), // LensSimulationMinBrightness
        new(0x1370,944,0x67E49830,8), // LensSimulationMaxBrightness
        new(0x1374,945,0xD66E4FA5,8), // LensSimulationLowBrightnessPercent
        new(0x1378,946,0x4C05D973,8), // LensSimulationHighBrightnessPercent
        new(0x137C,947,0x6DA5B0FC,4), // LensSimulationAberraType
        new(0x1380,948,0x0286381A,8), // LensSimulationAberraAmount
        new(0x1384,953,0x0D04C5AC,8), // LensSimulationLdrGradingIntensity
        new(0x1388,966,0x84CB7A7B,8), // LensSimulationLutTransitionSpan
        new(0x139C,1067,0x3A35E6F5,8), // LensSimulationCrossFilterWeight
        new(0x13A0,1068,0x2AF009C7,8), // LensSimulationCrossFilterLength
        new(0x13A4,1069,0x9DC863D7,8), // LensSimulationCrossFilterAttenuation
        new(0x13A8,1070,0xE7F79A0F,8), // LensSimulationCrossFilterInclination
        new(0x13AC,1071,0x356841BC,8), // LensSimulationCrossFilterAberration
        new(0x13B0,1092,0xBC94863E,4), // LensSimulationColorCorrectionLut1
        new(0x13B4,1093,0xF9B44A5D,4), // LensSimulationColorCorrectionLut2
        new(0x13B8,1094,0xDD69E8A8,8), // LensSimulationColorCorrectionInterpolationRate
        new(0x13BC,1095,0xC33E91F7,8), // LensSimulationColorCorrectionIntensity
        new(0x13C0,1103,0xF2555D35,8), // LensSimulationDOFHeatSimmerVibra
        new(0x13C4,1104,0xC7585A9D,8), // LensSimulationDOFHeatSimmerCycle
        new(0x13C8,1105,0x48ECAF7E,8), // LensSimulationDOFHeatSimmerSpeed
        new(0x13CC,1075,0x2F101A92,5), // ColorGradingHdrTvNit
        new(0x13D0,1076,0x1658638B,5), // ColorGradingBaseTvNit
        new(0x13D4,1077,0x0B3B7FAD,5), // ColorGradingTonemapType
        new(0x13D8,1078,0xBA41042E,8), // ColorGradingNeutralBlackIn
        new(0x13DC,1079,0x7547C33B,8), // ColorGradingNeutralBlackOut
    ];
}
