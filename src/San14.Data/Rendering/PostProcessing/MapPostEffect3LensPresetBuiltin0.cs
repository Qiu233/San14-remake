using System.Numerics;

namespace San14.Data;

/// <summary>Built-in optical settings and their portable Lens Split encoding.
/// Pointer relocation remains with the pinned parameter owner.</summary>
internal static class MapPostEffect3LensPresetBuiltin0
{
    internal const string PrimitiveSha256 = "608239E0B4BDD3E6028245DBA78A2348DAFCBAD4B056EEA2226136ABCA8DCE99";

    internal static MapPostEffect3LensPresetSettings Settings { get; } = new(
        new MapPostEffect3LensOpticsSettings(
            ImageDiagonal: 0.04327f,
            ImageHeight: 0.024f,
            InfiniteFocalLengthRange: new(0.018f, 0.3f),
            BladeCount: 5,
            FStopRange: new(1.4f, 32f),
            FStopShapeRange: new(0f, 1f),
            ExposureDialRange: new(-3f, 3f),
            ShutterSpeedRange: new(0.0005f, 0.5f),
            FocusingPower: -1f,
            ExposureValueRange: 20f,
            MaximumImageFieldOfViewScale: 1f,
            InfiniteImageCircleRate: 1f,
            InfiniteVignettingStartRate: 0.7f,
            AdaptedTime: Vector2.Zero),
        new MapPostEffect3LensFlareSettings(
            Suppressed: false,
            FocusMovingGroup: 1,
            HaloReferenceGhostIndex: 15,
            LensAberration: new(0.98f, 1.02f),
            ForceExposureValueRangeToFloat16Precision: false),
        [
            new(0.0021f, 0.0673f, 0.023175f, 60.8249f),
            new(0.0335f, 0.002f, -0.011196f, 2.78805f),
            new(0.0076f, 0.0015f, -0.002168f, 1.66888f),
            new(0.039f, 0.1054f, 0.0236f, 289.988f)
        ],
        [
            Ghost(0, -1f, 0.0157f),
            Ghost(0, 0.75f, 0.0147f),
            Ghost(1, -2.5f, 0.0137f),
            Ghost(1, -0.3f, 0.0118f),
            Ghost(1, 0.25f, 0.009f),
            Ghost(1, -0.5f, 0.0081f),
            Ghost(2, -1.8f, 0.0073f),
            Ghost(2, 1.62f, 0.0068f),
            Ghost(2, 1.4f, 0.0064f),
            Ghost(2, 1.2f, 0.0085f),
            Ghost(2, -1.25f, 0.0081f),
            Ghost(2, 1.3f, 0.0078f),
            Ghost(2, -0.15f, 0.0078f),
            Ghost(3, 0.8f, 0.0044f),
            Ghost(3, -1.65f, 0.0064f),
            Ghost(3, -0.65f, 0.0082f)
        ]);

    private static readonly byte[] primitive = MapPostEffect3LensPresetEncoding.Encode(Settings);

    internal static byte[] CopyPrimitive() => primitive.ToArray();

    // These bytes were unwritten by the native constructor; this backend uses zero.
    internal static readonly int[] RetainedPaddingOffsets = [85,86,87,92,93,94,95,141,142,143];

    private static MapPostEffect3SimpleGhostSettings Ghost(int group, float positionScale, float extendOffset)
        => new(group, positionScale, extendOffset, 0.005f, new(0.15f, 0.25f, 1f));
}
