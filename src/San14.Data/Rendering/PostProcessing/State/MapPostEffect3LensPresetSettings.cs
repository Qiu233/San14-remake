using System.Numerics;

namespace San14.Data;

/// <summary>Optical controls used by lens intermediate, depth of field and flare preparation.
/// Range names follow the declared lens parameters; distances use the existing metre units.</summary>
internal sealed record MapPostEffect3LensOpticsSettings(
    float ImageDiagonal,
    float ImageHeight,
    Vector2 InfiniteFocalLengthRange,
    int BladeCount,
    Vector2 FStopRange,
    Vector2 FStopShapeRange,
    Vector2 ExposureDialRange,
    Vector2 ShutterSpeedRange,
    float FocusingPower,
    float ExposureValueRange,
    float MaximumImageFieldOfViewScale,
    float InfiniteImageCircleRate,
    float InfiniteVignettingStartRate,
    Vector2 AdaptedTime);

/// <summary>Flare controls. Suppressed is the Split byte tested by the actual
/// LensFlareEnable/IntoSunLight gate, rather than an inferred resource flag.</summary>
internal sealed record MapPostEffect3LensFlareSettings(
    bool Suppressed,
    int FocusMovingGroup,
    int HaloReferenceGhostIndex,
    Vector2 LensAberration,
    bool ForceExposureValueRangeToFloat16Precision);

/// <summary>The four named scalar columns of LensGroupInformation:
/// WideSpace_init, TeleSpace_init and DistanceFormula_a/b.</summary>
internal readonly record struct MapPostEffect3LensGroupSettings(
    float WideSpaceInitial,
    float TeleSpaceInitial,
    float DistanceFormulaA,
    float DistanceFormulaB);

/// <summary>SimpleGhostInfo's group reference, position scale, extension,
/// reflection rate and three-component coating colour.</summary>
internal readonly record struct MapPostEffect3SimpleGhostSettings(
    int GroupIndex,
    float PositionScale,
    float ExtendOffset,
    float ReflectionRate,
    Vector3 CoatingColor);

/// <summary>Owned lens settings. Lens groups and ghosts remain separate
/// collections; their ABI counts and pointers are derived during encoding.</summary>
internal sealed class MapPostEffect3LensPresetSettings
{
    internal MapPostEffect3LensPresetSettings(MapPostEffect3LensOpticsSettings optics,
        MapPostEffect3LensFlareSettings flare,
        IReadOnlyList<MapPostEffect3LensGroupSettings> lensGroups,
        IReadOnlyList<MapPostEffect3SimpleGhostSettings> simpleGhosts)
    {
        Optics = optics; Flare = flare;
        LensGroups = Array.AsReadOnly(lensGroups.ToArray());
        SimpleGhosts = Array.AsReadOnly(simpleGhosts.ToArray());
    }

    internal MapPostEffect3LensOpticsSettings Optics { get; }
    internal MapPostEffect3LensFlareSettings Flare { get; }
    internal IReadOnlyList<MapPostEffect3LensGroupSettings> LensGroups { get; }
    internal IReadOnlyList<MapPostEffect3SimpleGhostSettings> SimpleGhosts { get; }
}
