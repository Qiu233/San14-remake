using System.Collections.Immutable;

namespace San14.Data;

/// <summary>Camera caller controls outside the optical settings. Unclassified non-optical writes stay at the compatibility boundary.</summary>
public sealed record CameraRenderQualityPatch
{
    public float? LightAdaptationScale { get; init; }
    public float? DarkAdaptationScale { get; init; }
    public float? Cloudiness { get; init; }
    public float? AltitudeOffset { get; init; }
    public uint? CloudLowCostMode { get; init; }
    public uint? CloudResolutionMode { get; init; }
    public uint? PrimaryFogFade { get; init; }
    public uint? PrimaryFogMode { get; init; }
    public uint? SecondaryFogFade { get; init; }
    public uint? SecondaryFogMode { get; init; }
    internal ImmutableArray<CameraCompatibilityFieldWrite> Compatibility { get; init; } = [];
    public CameraRenderQualityPatch Then(CameraRenderQualityPatch later) => later with
    {
        LightAdaptationScale = later.LightAdaptationScale ?? LightAdaptationScale,
        DarkAdaptationScale = later.DarkAdaptationScale ?? DarkAdaptationScale,
        Cloudiness = later.Cloudiness ?? Cloudiness,
        AltitudeOffset = later.AltitudeOffset ?? AltitudeOffset,
        CloudLowCostMode = later.CloudLowCostMode ?? CloudLowCostMode,
        CloudResolutionMode = later.CloudResolutionMode ?? CloudResolutionMode,
        PrimaryFogFade = later.PrimaryFogFade ?? PrimaryFogFade,
        PrimaryFogMode = later.PrimaryFogMode ?? PrimaryFogMode,
        SecondaryFogFade = later.SecondaryFogFade ?? SecondaryFogFade,
        SecondaryFogMode = later.SecondaryFogMode ?? SecondaryFogMode,
        Compatibility = later.Compatibility.IsEmpty ? Compatibility : later.Compatibility
    };
}

internal readonly record struct CameraCompatibilityFieldWrite(int EncodedOffset, uint Bits);
