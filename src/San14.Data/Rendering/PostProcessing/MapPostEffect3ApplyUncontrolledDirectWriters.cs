namespace San14.Data;

/// <summary>Explicit source ABI branch; original tests the descriptor's
/// actual58 field. Its provenance must be supplied by the caller; it is not
/// derived from SPK version, float texture format or pass count.</summary>
public enum MapPostEffect3ApplyUncontrolledVariant { LegacyCameraFields, LensPresetCameraFields }

public sealed record MapPostEffect3ApplyUncontrolledDirectPlan(long FrameNumber,
    MapPostEffect3ApplyUncontrolledVariant Variant,
    IReadOnlyList<MapPostEffect3ApplyValueWrite> Writes,string SourceEvidence)
{
    public bool IsCompleteForExecution=>false;
    public string UnsupportedInputs=>"source-specific sun/white-point preparation and remaining conditional writers; actual parameter/pass factory execution";
}

/// <summary>Direct raw Camera writes from the original no-controller module
/// preparations. Shader keys are the actual3SPK named scalar keys, not the
/// old A1C builtin80 parameter ordinals. Conditional/computed writes stay separate.</summary>
public static class MapPostEffect3ApplyUncontrolledDirectWriters
{
    public static MapPostEffect3ApplyUncontrolledDirectPlan PrepareFreshSource(
        MapCameraConstructionPacket camera,MapPostEffect3ApplyUncontrolledCameraInputs inputs,
        MapPostEffect3ApplyUncontrolledVariant explicitVariant,string actualVariantEvidence)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualVariantEvidence);inputs.ValidateSameCamera(camera);
        if(explicitVariant is not (MapPostEffect3ApplyUncontrolledVariant.LegacyCameraFields or MapPostEffect3ApplyUncontrolledVariant.LensPresetCameraFields))
            throw new NotSupportedException("Uncontrolled module ABI branch is unsupported.");
        string evidence="no-controller named scalar preparation;"+inputs.SourceEvidence+";"+actualVariantEvidence;
        var rows=explicitVariant==MapPostEffect3ApplyUncontrolledVariant.LegacyCameraFields?LegacyCameraFields:LensPresetCameraFields;
        var writes=rows.Select(r=>new MapPostEffect3ApplyValueWrite(r.Name,r.Stage,
            inputs.ReadCameraBytes(camera,r.CameraOffset,r.Bytes).ToArray(),evidence+$";Camera{r.CameraOffset:X};{r.Bytes}B"));
        return new(camera.FrameNumber,explicitVariant,Array.AsReadOnly(writes.ToArray()),evidence);
    }
    private readonly record struct Writer(string Name,MapPostEffect3WriteStage Stage,int CameraOffset,int Bytes);
    private static readonly Writer[] LegacyCameraFields=
    [
        new("ObjectDistance",MapPostEffect3WriteStage.LegacyNoControllerObjectDistance,0x12FC,4),
        new("InfiniteFocalLength",MapPostEffect3WriteStage.LegacyNoControllerInfiniteFocalLength,0x1300,4),
        new("FStop",MapPostEffect3WriteStage.LegacyNoControllerFStop,0x1304,4),
        new("ExposureDial",MapPostEffect3WriteStage.LegacyNoControllerExposureDial,0x1308,4),
        new("ShutterSpeed",MapPostEffect3WriteStage.LegacyNoControllerShutterSpeed,0x130C,4),
        new("ColorTemperature",MapPostEffect3WriteStage.LegacyNoControllerColorTemperature,0x1310,4),
        new("ColorSaturation",MapPostEffect3WriteStage.LegacyNoControllerColorSaturation,0x1314,4),
        new("FXAAType",MapPostEffect3WriteStage.LegacyNoControllerFXAAType,0x418,4),
        new("FXAAEdgeThreshold",MapPostEffect3WriteStage.LegacyNoControllerFXAAEdgeThreshold,0x404,4),
        new("FXAAEdgeThresholdMin",MapPostEffect3WriteStage.LegacyNoControllerFXAAEdgeThresholdMin,0x408,4),
        new("FXAAEdgeSharpness",MapPostEffect3WriteStage.LegacyNoControllerFXAAEdgeSharpness,0x40C,4),
        new("FXAAPixelRange",MapPostEffect3WriteStage.LegacyNoControllerFXAAPixelRange,0x410,4),
        new("FXAAQualityEdgeThreshold",MapPostEffect3WriteStage.LegacyNoControllerFXAAQualityEdgeThreshold,0x41C,4),
        new("FXAAQualityEdgeThresholdMin",MapPostEffect3WriteStage.LegacyNoControllerFXAAQualityEdgeThresholdMin,0x420,4),
        new("FXAAQualitySubpixSharpness",MapPostEffect3WriteStage.LegacyNoControllerFXAAQualitySubpixSharpness,0x424,4),
        new("FXAAQualityLevel",MapPostEffect3WriteStage.LegacyNoControllerFXAAQualityLevel,0x428,4),
        new("FXAAQualityPerformance",MapPostEffect3WriteStage.LegacyNoControllerFXAAQualityPerformance,0x42C,4),
        new("LSLarge",MapPostEffect3WriteStage.LegacyNoControllerLSLarge,0xE2C,4),
        new("LSFrequency",MapPostEffect3WriteStage.LegacyNoControllerLSFrequency,0xE30,4),
        new("LSStrong",MapPostEffect3WriteStage.LegacyNoControllerLSStrong,0xE34,4),
        new("AnamorphicFlag",MapPostEffect3WriteStage.LegacyNoControllerAnamorphicFlag,0x131C,4),
        new("CameraNear",MapPostEffect3WriteStage.LegacyNoControllerCameraNear,0x140,4),
        new("CameraFar",MapPostEffect3WriteStage.LegacyNoControllerCameraFar,0x144,4),
        new("CameraFov",MapPostEffect3WriteStage.LegacyNoControllerCameraFov,0x148,4),
        new("ViewProj",MapPostEffect3WriteStage.LegacyNoControllerViewProj,0x100,64),
        new("TemporalAAVelocityWeight",MapPostEffect3WriteStage.LegacyNoControllerTemporalAAVelocityWeight,0x1330,4),
        new("TemporalAADepthDiffThreshold",MapPostEffect3WriteStage.LegacyNoControllerTemporalAADepthDiffThreshold,0x1334,4),
        new("TemporalAABaseWeight",MapPostEffect3WriteStage.LegacyNoControllerTemporalAABaseWeight,0x1338,4),
        new("TemporalAAEnable",MapPostEffect3WriteStage.LegacyNoControllerTemporalAAEnable,0x133C,4),
        new("GhostScale",MapPostEffect3WriteStage.LegacyNoControllerGhostScale,0x1340,4),
        new("HaloScale",MapPostEffect3WriteStage.LegacyNoControllerHaloScale,0x134C,4),
        new("BloomScale",MapPostEffect3WriteStage.LegacyNoControllerBloomScale,0x1350,4),
        new("CrossFilterWeight",MapPostEffect3WriteStage.LegacyNoControllerCrossFilterWeight,0x139C,4),
        new("CrossFilterLength",MapPostEffect3WriteStage.LegacyNoControllerCrossFilterLength,0x13A0,4),
        new("CrossFilterAttenuation",MapPostEffect3WriteStage.LegacyNoControllerCrossFilterAttenuation,0x13A4,4),
        new("CrossFilterInclination",MapPostEffect3WriteStage.LegacyNoControllerCrossFilterInclination,0x13A8,4),
        new("CrossFilterAberration",MapPostEffect3WriteStage.LegacyNoControllerCrossFilterAberration,0x13AC,4),
        new("HdrTvNit",MapPostEffect3WriteStage.LegacyNoControllerHdrTvNit,0x13CC,4),
        new("BaseTvNit",MapPostEffect3WriteStage.LegacyNoControllerBaseTvNit,0x13D0,4),
        new("VignettingFade",MapPostEffect3WriteStage.LegacyNoControllerVignettingFade,0x1364,4),
    ];
    private static readonly Writer[] LensPresetCameraFields=
    [
        new("ObjectDistance",MapPostEffect3WriteStage.LensPresetNoControllerObjectDistance,0x12FC,4),
        new("InfiniteFocalLength",MapPostEffect3WriteStage.LensPresetNoControllerInfiniteFocalLength,0x1300,4),
        new("FStop",MapPostEffect3WriteStage.LensPresetNoControllerFStop,0x1304,4),
        new("ExposureDial",MapPostEffect3WriteStage.LensPresetNoControllerExposureDial,0x1308,4),
        new("ShutterSpeed",MapPostEffect3WriteStage.LensPresetNoControllerShutterSpeed,0x130C,4),
        new("ColorTemperature",MapPostEffect3WriteStage.LensPresetNoControllerColorTemperature,0x1310,4),
        new("ColorSaturation",MapPostEffect3WriteStage.LensPresetNoControllerColorSaturation,0x1314,4),
        new("FXAAType",MapPostEffect3WriteStage.LensPresetNoControllerFXAAType,0x418,4),
        new("FXAAEdgeThreshold",MapPostEffect3WriteStage.LensPresetNoControllerFXAAEdgeThreshold,0x404,4),
        new("FXAAEdgeThresholdMin",MapPostEffect3WriteStage.LensPresetNoControllerFXAAEdgeThresholdMin,0x408,4),
        new("FXAAEdgeSharpness",MapPostEffect3WriteStage.LensPresetNoControllerFXAAEdgeSharpness,0x40C,4),
        new("FXAAPixelRange",MapPostEffect3WriteStage.LensPresetNoControllerFXAAPixelRange,0x410,4),
        new("FXAAQualityEdgeThreshold",MapPostEffect3WriteStage.LensPresetNoControllerFXAAQualityEdgeThreshold,0x41C,4),
        new("FXAAQualityEdgeThresholdMin",MapPostEffect3WriteStage.LensPresetNoControllerFXAAQualityEdgeThresholdMin,0x420,4),
        new("FXAAQualitySubpixSharpness",MapPostEffect3WriteStage.LensPresetNoControllerFXAAQualitySubpixSharpness,0x424,4),
        new("FXAAQualityLevel",MapPostEffect3WriteStage.LensPresetNoControllerFXAAQualityLevel,0x428,4),
        new("FXAAQualityPerformance",MapPostEffect3WriteStage.LensPresetNoControllerFXAAQualityPerformance,0x42C,4),
        new("LSLarge",MapPostEffect3WriteStage.LensPresetNoControllerLSLarge,0xE2C,4),
        new("LSFrequency",MapPostEffect3WriteStage.LensPresetNoControllerLSFrequency,0xE30,4),
        new("LSStrong",MapPostEffect3WriteStage.LensPresetNoControllerLSStrong,0xE34,4),
        new("AnamorphicEnable",MapPostEffect3WriteStage.LensPresetNoControllerAnamorphicEnable,0x131C,4),
        new("CameraNear",MapPostEffect3WriteStage.LensPresetNoControllerCameraNear,0x140,4),
        new("CameraFar",MapPostEffect3WriteStage.LensPresetNoControllerCameraFar,0x144,4),
        new("CameraFov",MapPostEffect3WriteStage.LensPresetNoControllerCameraFov,0x148,4),
        new("ViewProj",MapPostEffect3WriteStage.LensPresetNoControllerViewProj,0x100,64),
        new("TemporalAAVelocityWeight",MapPostEffect3WriteStage.LensPresetNoControllerTemporalAAVelocityWeight,0x1330,4),
        new("TemporalAADepthDiffThreshold",MapPostEffect3WriteStage.LensPresetNoControllerTemporalAADepthDiffThreshold,0x1334,4),
        new("TemporalAABaseWeight",MapPostEffect3WriteStage.LensPresetNoControllerTemporalAABaseWeight,0x1338,4),
        new("TemporalAAEnable",MapPostEffect3WriteStage.LensPresetNoControllerTemporalAAEnable,0x133C,4),
        new("GhostScale",MapPostEffect3WriteStage.LensPresetNoControllerGhostScale,0x1340,4),
        new("HaloScale",MapPostEffect3WriteStage.LensPresetNoControllerHaloScale,0x134C,4),
        new("BloomScale",MapPostEffect3WriteStage.LensPresetNoControllerBloomScale,0x1350,4),
        new("CrossFilterWeight",MapPostEffect3WriteStage.LensPresetNoControllerCrossFilterWeight,0x139C,4),
        new("CrossFilterLength",MapPostEffect3WriteStage.LensPresetNoControllerCrossFilterLength,0x13A0,4),
        new("CrossFilterAttenuation",MapPostEffect3WriteStage.LensPresetNoControllerCrossFilterAttenuation,0x13A4,4),
        new("CrossFilterInclination",MapPostEffect3WriteStage.LensPresetNoControllerCrossFilterInclination,0x13A8,4),
        new("CrossFilterAberration",MapPostEffect3WriteStage.LensPresetNoControllerCrossFilterAberration,0x13AC,4),
        new("VignettingFade",MapPostEffect3WriteStage.LensPresetNoControllerVignettingFade,0x1364,4),
    ];
}
