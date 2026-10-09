namespace San14.Data;

/// <summary>Explicit source ABI branch; original tests the descriptor's
/// actual58 field. Its provenance must be supplied by the caller; it is not
/// derived from SPK version, float texture format or pass count.</summary>
public enum OriginalMapPostEffect3ApplyUncontrolledVariant { LegacyCameraFields, LensPresetCameraFields }

public sealed record OriginalMapPostEffect3ApplyUncontrolledDirectPlan(long FrameNumber,
    OriginalMapPostEffect3ApplyUncontrolledVariant Variant,
    IReadOnlyList<OriginalMapPostEffect3ApplyValueWrite> Writes,string SourceEvidence)
{
    public bool IsCompleteForExecution=>false;
    public string UnsupportedInputs=>"source-specific sun/white-point preparation and remaining conditional writers; actual parameter/pass factory execution";
}

/// <summary>Direct raw Camera writes from the original no-controller module
/// preparations. Shader keys are the actual3SPK named scalar keys, not the
/// old A1C builtin80 parameter ordinals. Conditional/computed writes stay separate.</summary>
public static class OriginalMapPostEffect3ApplyUncontrolledDirectWriters
{
    public static OriginalMapPostEffect3ApplyUncontrolledDirectPlan PrepareFreshSource(
        OriginalMapCameraConstructionPacket camera,OriginalMapPostEffect3ApplyUncontrolledCameraInputs inputs,
        OriginalMapPostEffect3ApplyUncontrolledVariant explicitVariant,string actualVariantEvidence)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualVariantEvidence);inputs.ValidateSameCamera(camera);
        if(explicitVariant is not (OriginalMapPostEffect3ApplyUncontrolledVariant.LegacyCameraFields or OriginalMapPostEffect3ApplyUncontrolledVariant.LensPresetCameraFields))
            throw new NotSupportedException("Uncontrolled module ABI branch is unsupported.");
        string evidence="no-controller named scalar preparation;"+inputs.SourceEvidence+";"+actualVariantEvidence;
        var rows=explicitVariant==OriginalMapPostEffect3ApplyUncontrolledVariant.LegacyCameraFields?LegacyCameraFields:LensPresetCameraFields;
        var writes=rows.Select(r=>new OriginalMapPostEffect3ApplyValueWrite(r.Name,r.Stage,
            inputs.ReadCameraBytes(camera,r.CameraOffset,r.Bytes).ToArray(),evidence+$";Camera{r.CameraOffset:X};{r.Bytes}B"));
        return new(camera.FrameNumber,explicitVariant,Array.AsReadOnly(writes.ToArray()),evidence);
    }
    private readonly record struct Writer(string Name,OriginalMapPostEffect3WriteStage Stage,int CameraOffset,int Bytes);
    private static readonly Writer[] LegacyCameraFields=
    [
        new("ObjectDistance",OriginalMapPostEffect3WriteStage.LegacyNoControllerObjectDistance,0x12FC,4),
        new("InfiniteFocalLength",OriginalMapPostEffect3WriteStage.LegacyNoControllerInfiniteFocalLength,0x1300,4),
        new("FStop",OriginalMapPostEffect3WriteStage.LegacyNoControllerFStop,0x1304,4),
        new("ExposureDial",OriginalMapPostEffect3WriteStage.LegacyNoControllerExposureDial,0x1308,4),
        new("ShutterSpeed",OriginalMapPostEffect3WriteStage.LegacyNoControllerShutterSpeed,0x130C,4),
        new("ColorTemperature",OriginalMapPostEffect3WriteStage.LegacyNoControllerColorTemperature,0x1310,4),
        new("ColorSaturation",OriginalMapPostEffect3WriteStage.LegacyNoControllerColorSaturation,0x1314,4),
        new("FXAAType",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAType,0x418,4),
        new("FXAAEdgeThreshold",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAEdgeThreshold,0x404,4),
        new("FXAAEdgeThresholdMin",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAEdgeThresholdMin,0x408,4),
        new("FXAAEdgeSharpness",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAEdgeSharpness,0x40C,4),
        new("FXAAPixelRange",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAPixelRange,0x410,4),
        new("FXAAQualityEdgeThreshold",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAQualityEdgeThreshold,0x41C,4),
        new("FXAAQualityEdgeThresholdMin",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAQualityEdgeThresholdMin,0x420,4),
        new("FXAAQualitySubpixSharpness",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAQualitySubpixSharpness,0x424,4),
        new("FXAAQualityLevel",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAQualityLevel,0x428,4),
        new("FXAAQualityPerformance",OriginalMapPostEffect3WriteStage.LegacyNoControllerFXAAQualityPerformance,0x42C,4),
        new("LSLarge",OriginalMapPostEffect3WriteStage.LegacyNoControllerLSLarge,0xE2C,4),
        new("LSFrequency",OriginalMapPostEffect3WriteStage.LegacyNoControllerLSFrequency,0xE30,4),
        new("LSStrong",OriginalMapPostEffect3WriteStage.LegacyNoControllerLSStrong,0xE34,4),
        new("AnamorphicFlag",OriginalMapPostEffect3WriteStage.LegacyNoControllerAnamorphicFlag,0x131C,4),
        new("CameraNear",OriginalMapPostEffect3WriteStage.LegacyNoControllerCameraNear,0x140,4),
        new("CameraFar",OriginalMapPostEffect3WriteStage.LegacyNoControllerCameraFar,0x144,4),
        new("CameraFov",OriginalMapPostEffect3WriteStage.LegacyNoControllerCameraFov,0x148,4),
        new("ViewProj",OriginalMapPostEffect3WriteStage.LegacyNoControllerViewProj,0x100,64),
        new("TemporalAAVelocityWeight",OriginalMapPostEffect3WriteStage.LegacyNoControllerTemporalAAVelocityWeight,0x1330,4),
        new("TemporalAADepthDiffThreshold",OriginalMapPostEffect3WriteStage.LegacyNoControllerTemporalAADepthDiffThreshold,0x1334,4),
        new("TemporalAABaseWeight",OriginalMapPostEffect3WriteStage.LegacyNoControllerTemporalAABaseWeight,0x1338,4),
        new("TemporalAAEnable",OriginalMapPostEffect3WriteStage.LegacyNoControllerTemporalAAEnable,0x133C,4),
        new("GhostScale",OriginalMapPostEffect3WriteStage.LegacyNoControllerGhostScale,0x1340,4),
        new("HaloScale",OriginalMapPostEffect3WriteStage.LegacyNoControllerHaloScale,0x134C,4),
        new("BloomScale",OriginalMapPostEffect3WriteStage.LegacyNoControllerBloomScale,0x1350,4),
        new("CrossFilterWeight",OriginalMapPostEffect3WriteStage.LegacyNoControllerCrossFilterWeight,0x139C,4),
        new("CrossFilterLength",OriginalMapPostEffect3WriteStage.LegacyNoControllerCrossFilterLength,0x13A0,4),
        new("CrossFilterAttenuation",OriginalMapPostEffect3WriteStage.LegacyNoControllerCrossFilterAttenuation,0x13A4,4),
        new("CrossFilterInclination",OriginalMapPostEffect3WriteStage.LegacyNoControllerCrossFilterInclination,0x13A8,4),
        new("CrossFilterAberration",OriginalMapPostEffect3WriteStage.LegacyNoControllerCrossFilterAberration,0x13AC,4),
        new("HdrTvNit",OriginalMapPostEffect3WriteStage.LegacyNoControllerHdrTvNit,0x13CC,4),
        new("BaseTvNit",OriginalMapPostEffect3WriteStage.LegacyNoControllerBaseTvNit,0x13D0,4),
        new("VignettingFade",OriginalMapPostEffect3WriteStage.LegacyNoControllerVignettingFade,0x1364,4),
    ];
    private static readonly Writer[] LensPresetCameraFields=
    [
        new("ObjectDistance",OriginalMapPostEffect3WriteStage.LensPresetNoControllerObjectDistance,0x12FC,4),
        new("InfiniteFocalLength",OriginalMapPostEffect3WriteStage.LensPresetNoControllerInfiniteFocalLength,0x1300,4),
        new("FStop",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFStop,0x1304,4),
        new("ExposureDial",OriginalMapPostEffect3WriteStage.LensPresetNoControllerExposureDial,0x1308,4),
        new("ShutterSpeed",OriginalMapPostEffect3WriteStage.LensPresetNoControllerShutterSpeed,0x130C,4),
        new("ColorTemperature",OriginalMapPostEffect3WriteStage.LensPresetNoControllerColorTemperature,0x1310,4),
        new("ColorSaturation",OriginalMapPostEffect3WriteStage.LensPresetNoControllerColorSaturation,0x1314,4),
        new("FXAAType",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAType,0x418,4),
        new("FXAAEdgeThreshold",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAEdgeThreshold,0x404,4),
        new("FXAAEdgeThresholdMin",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAEdgeThresholdMin,0x408,4),
        new("FXAAEdgeSharpness",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAEdgeSharpness,0x40C,4),
        new("FXAAPixelRange",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAPixelRange,0x410,4),
        new("FXAAQualityEdgeThreshold",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAQualityEdgeThreshold,0x41C,4),
        new("FXAAQualityEdgeThresholdMin",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAQualityEdgeThresholdMin,0x420,4),
        new("FXAAQualitySubpixSharpness",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAQualitySubpixSharpness,0x424,4),
        new("FXAAQualityLevel",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAQualityLevel,0x428,4),
        new("FXAAQualityPerformance",OriginalMapPostEffect3WriteStage.LensPresetNoControllerFXAAQualityPerformance,0x42C,4),
        new("LSLarge",OriginalMapPostEffect3WriteStage.LensPresetNoControllerLSLarge,0xE2C,4),
        new("LSFrequency",OriginalMapPostEffect3WriteStage.LensPresetNoControllerLSFrequency,0xE30,4),
        new("LSStrong",OriginalMapPostEffect3WriteStage.LensPresetNoControllerLSStrong,0xE34,4),
        new("AnamorphicEnable",OriginalMapPostEffect3WriteStage.LensPresetNoControllerAnamorphicEnable,0x131C,4),
        new("CameraNear",OriginalMapPostEffect3WriteStage.LensPresetNoControllerCameraNear,0x140,4),
        new("CameraFar",OriginalMapPostEffect3WriteStage.LensPresetNoControllerCameraFar,0x144,4),
        new("CameraFov",OriginalMapPostEffect3WriteStage.LensPresetNoControllerCameraFov,0x148,4),
        new("ViewProj",OriginalMapPostEffect3WriteStage.LensPresetNoControllerViewProj,0x100,64),
        new("TemporalAAVelocityWeight",OriginalMapPostEffect3WriteStage.LensPresetNoControllerTemporalAAVelocityWeight,0x1330,4),
        new("TemporalAADepthDiffThreshold",OriginalMapPostEffect3WriteStage.LensPresetNoControllerTemporalAADepthDiffThreshold,0x1334,4),
        new("TemporalAABaseWeight",OriginalMapPostEffect3WriteStage.LensPresetNoControllerTemporalAABaseWeight,0x1338,4),
        new("TemporalAAEnable",OriginalMapPostEffect3WriteStage.LensPresetNoControllerTemporalAAEnable,0x133C,4),
        new("GhostScale",OriginalMapPostEffect3WriteStage.LensPresetNoControllerGhostScale,0x1340,4),
        new("HaloScale",OriginalMapPostEffect3WriteStage.LensPresetNoControllerHaloScale,0x134C,4),
        new("BloomScale",OriginalMapPostEffect3WriteStage.LensPresetNoControllerBloomScale,0x1350,4),
        new("CrossFilterWeight",OriginalMapPostEffect3WriteStage.LensPresetNoControllerCrossFilterWeight,0x139C,4),
        new("CrossFilterLength",OriginalMapPostEffect3WriteStage.LensPresetNoControllerCrossFilterLength,0x13A0,4),
        new("CrossFilterAttenuation",OriginalMapPostEffect3WriteStage.LensPresetNoControllerCrossFilterAttenuation,0x13A4,4),
        new("CrossFilterInclination",OriginalMapPostEffect3WriteStage.LensPresetNoControllerCrossFilterInclination,0x13A8,4),
        new("CrossFilterAberration",OriginalMapPostEffect3WriteStage.LensPresetNoControllerCrossFilterAberration,0x13AC,4),
        new("VignettingFade",OriginalMapPostEffect3WriteStage.LensPresetNoControllerVignettingFade,0x1364,4),
    ];
}
