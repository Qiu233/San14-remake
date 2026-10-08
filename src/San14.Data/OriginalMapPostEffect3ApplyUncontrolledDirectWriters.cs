namespace San14.Data;

/// <summary>Explicit source ABI branch; original EB7E32 tests the descriptor's
/// actual58 field. Its provenance must be supplied by the caller; it is not
/// derived from SPK version, float texture format or pass count.</summary>
public enum OriginalMapPostEffect3ApplyUncontrolledVariant { NativeE70780, NativeE71690 }

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
        if(explicitVariant is not (OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE70780 or OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE71690))
            throw new NotSupportedException("Uncontrolled module ABI branch is unsupported.");
        uint native=explicitVariant==OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE70780?0xE70780u:0xE71690u;
        string evidence=$"TC:{native:X};named114AEB0/114B0F0;"+inputs.SourceEvidence+";"+actualVariantEvidence;
        var rows=explicitVariant==OriginalMapPostEffect3ApplyUncontrolledVariant.NativeE70780?E70780:E71690;
        var writes=rows.Select(r=>new OriginalMapPostEffect3ApplyValueWrite(r.Name,r.CallSite,native,
            inputs.ReadCameraBytes(camera,r.CameraOffset,r.Bytes).ToArray(),evidence+$";Camera{r.CameraOffset:X};{r.Bytes}B"));
        return new(camera.FrameNumber,explicitVariant,Array.AsReadOnly(writes.ToArray()),evidence);
    }
    private readonly record struct Writer(string Name,uint CallSite,int CameraOffset,int Bytes);
    private static readonly Writer[] E70780=
    [
        new("ObjectDistance",0x00E7080Eu,0x12FC,4),
        new("InfiniteFocalLength",0x00E7083Au,0x1300,4),
        new("FStop",0x00E70866u,0x1304,4),
        new("ExposureDial",0x00E70892u,0x1308,4),
        new("ShutterSpeed",0x00E708BEu,0x130C,4),
        new("ColorTemperature",0x00E708EAu,0x1310,4),
        new("ColorSaturation",0x00E70916u,0x1314,4),
        new("FXAAType",0x00E7097Eu,0x418,4),
        new("FXAAEdgeThreshold",0x00E709AAu,0x404,4),
        new("FXAAEdgeThresholdMin",0x00E709D6u,0x408,4),
        new("FXAAEdgeSharpness",0x00E70A02u,0x40C,4),
        new("FXAAPixelRange",0x00E70A2Eu,0x410,4),
        new("FXAAQualityEdgeThreshold",0x00E70A5Au,0x41C,4),
        new("FXAAQualityEdgeThresholdMin",0x00E70A86u,0x420,4),
        new("FXAAQualitySubpixSharpness",0x00E70AB2u,0x424,4),
        new("FXAAQualityLevel",0x00E70ADEu,0x428,4),
        new("FXAAQualityPerformance",0x00E70B0Au,0x42C,4),
        new("LSLarge",0x00E71062u,0xE2C,4),
        new("LSFrequency",0x00E7108Eu,0xE30,4),
        new("LSStrong",0x00E710BAu,0xE34,4),
        new("AnamorphicFlag",0x00E710E6u,0x131C,4),
        new("CameraNear",0x00E71112u,0x140,4),
        new("CameraFar",0x00E7113Eu,0x144,4),
        new("CameraFov",0x00E7116Au,0x148,4),
        new("ViewProj",0x00E71196u,0x100,64),
        new("TemporalAAVelocityWeight",0x00E71246u,0x1330,4),
        new("TemporalAADepthDiffThreshold",0x00E71272u,0x1334,4),
        new("TemporalAABaseWeight",0x00E7129Eu,0x1338,4),
        new("TemporalAAEnable",0x00E712CAu,0x133C,4),
        new("GhostScale",0x00E712F6u,0x1340,4),
        new("HaloScale",0x00E71322u,0x134C,4),
        new("BloomScale",0x00E7134Eu,0x1350,4),
        new("CrossFilterWeight",0x00E7137Au,0x139C,4),
        new("CrossFilterLength",0x00E713A6u,0x13A0,4),
        new("CrossFilterAttenuation",0x00E713D2u,0x13A4,4),
        new("CrossFilterInclination",0x00E713FEu,0x13A8,4),
        new("CrossFilterAberration",0x00E7142Au,0x13AC,4),
        new("HdrTvNit",0x00E71456u,0x13CC,4),
        new("BaseTvNit",0x00E71482u,0x13D0,4),
        new("VignettingFade",0x00E714AEu,0x1364,4),
    ];
    private static readonly Writer[] E71690=
    [
        new("ObjectDistance",0x00E71729u,0x12FC,4),
        new("InfiniteFocalLength",0x00E71755u,0x1300,4),
        new("FStop",0x00E71781u,0x1304,4),
        new("ExposureDial",0x00E717ADu,0x1308,4),
        new("ShutterSpeed",0x00E717D9u,0x130C,4),
        new("ColorTemperature",0x00E71805u,0x1310,4),
        new("ColorSaturation",0x00E71831u,0x1314,4),
        new("FXAAType",0x00E71897u,0x418,4),
        new("FXAAEdgeThreshold",0x00E718C3u,0x404,4),
        new("FXAAEdgeThresholdMin",0x00E718EFu,0x408,4),
        new("FXAAEdgeSharpness",0x00E7191Bu,0x40C,4),
        new("FXAAPixelRange",0x00E71947u,0x410,4),
        new("FXAAQualityEdgeThreshold",0x00E71973u,0x41C,4),
        new("FXAAQualityEdgeThresholdMin",0x00E7199Fu,0x420,4),
        new("FXAAQualitySubpixSharpness",0x00E719CBu,0x424,4),
        new("FXAAQualityLevel",0x00E719F7u,0x428,4),
        new("FXAAQualityPerformance",0x00E71A23u,0x42C,4),
        new("LSLarge",0x00E71FABu,0xE2C,4),
        new("LSFrequency",0x00E71FD7u,0xE30,4),
        new("LSStrong",0x00E72003u,0xE34,4),
        new("AnamorphicEnable",0x00E7202Fu,0x131C,4),
        new("CameraNear",0x00E7205Bu,0x140,4),
        new("CameraFar",0x00E72087u,0x144,4),
        new("CameraFov",0x00E720B3u,0x148,4),
        new("ViewProj",0x00E720DFu,0x100,64),
        new("TemporalAAVelocityWeight",0x00E7218Fu,0x1330,4),
        new("TemporalAADepthDiffThreshold",0x00E721BBu,0x1334,4),
        new("TemporalAABaseWeight",0x00E721E7u,0x1338,4),
        new("TemporalAAEnable",0x00E72213u,0x133C,4),
        new("GhostScale",0x00E7223Fu,0x1340,4),
        new("HaloScale",0x00E7226Bu,0x134C,4),
        new("BloomScale",0x00E72297u,0x1350,4),
        new("CrossFilterWeight",0x00E722C3u,0x139C,4),
        new("CrossFilterLength",0x00E722EFu,0x13A0,4),
        new("CrossFilterAttenuation",0x00E7231Bu,0x13A4,4),
        new("CrossFilterInclination",0x00E72347u,0x13A8,4),
        new("CrossFilterAberration",0x00E72373u,0x13AC,4),
        new("VignettingFade",0x00E7239Fu,0x1364,4),
    ];
}
