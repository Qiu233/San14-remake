using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The defined fog fields used by current rendering. Compatibility
/// allocation tail bytes stay at the interchange boundary.</summary>
public sealed record DirectionalLightFogState(uint Mode, SkyWeatherFogValues Values)
{
    internal static DirectionalLightFogState Read(ReadOnlySpan<byte> bytes) => new(
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x10..]),
        new(Four(bytes, 0), Float(bytes, 0x14), Float(bytes, 0x18), Float(bytes, 0x1C),
            Three(bytes, 0x20), Float(bytes, 0x2C), Three(bytes, 0x30), Float(bytes, 0x3C),
            Float(bytes, 0x40), Float(bytes, 0x44)));

    private static float Float(ReadOnlySpan<byte> bytes, int at) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]);
    private static Vector3 Three(ReadOnlySpan<byte> bytes, int at) =>
        new(Float(bytes, at), Float(bytes, at + 4), Float(bytes, at + 8));
    private static Vector4 Four(ReadOnlySpan<byte> bytes, int at) =>
        new(Float(bytes, at), Float(bytes, at + 4), Float(bytes, at + 8), Float(bytes, at + 12));
}

/// <summary>One prepared camera's current lighting inputs, shared by the frame's
/// registered lights. Direction conversion is needed only by mode six.</summary>
public sealed class DirectionalLightCurrentCamera
{
    private readonly Lazy<(Vector4 First, Vector4 Second)> quaternions;
    private readonly Vector3 firstDirection, secondDirection;

    private DirectionalLightCurrentCamera(ReadOnlySpan<byte> camera)
    {
        Vector3 firstTarget = Three(camera, 0x2AC), secondTarget = Three(camera, 0x2F0);
        firstDirection = Three(camera, 0x2B8); secondDirection = Three(camera, 0x2FC);
        quaternions = new(() => (
            CameraDirectionalLightData.QuaternionFromPositiveZ(firstTarget),
            CameraDirectionalLightData.QuaternionFromPositiveZ(secondTarget)));
        FirstColor = Four(camera, 0x2D4); SecondColor = Four(camera, 0x318);
        SunBaseline = Four(camera, 0x2C4); MoonBaseline = Four(camera, 0x308);
        SourceHighBitSwappedRows = (BinaryPrimitives.ReadUInt32LittleEndian(camera[0x3B0..]) & 0x80000000u) != 0;
        AmbientScaleBits = BinaryPrimitives.ReadUInt32LittleEndian(camera[0x3B8..]);
        ThirdLightIntensity = Float(camera, 0x1F0); ThirdLightMix = Float(camera, 0x1F4);
        Fog = DirectionalLightFogState.Read(camera[0x340..]);
    }

    public Vector4 FirstColor { get; }
    public Vector4 SecondColor { get; }
    public Vector4 SunBaseline { get; }
    public Vector4 MoonBaseline { get; }
    public bool SourceHighBitSwappedRows { get; }
    public uint AmbientScaleBits { get; }
    public float ThirdLightIntensity { get; }
    public float ThirdLightMix { get; }
    public DirectionalLightFogState Fog { get; }
    internal Vector3 LensFlareSourceVector => firstDirection;

    public static DirectionalLightCurrentCamera Capture(ReadOnlySpan<byte> preparedCamera)
    {
        if (preparedCamera.Length != CameraDirectionalLightData.CameraPrefixBytes)
            throw new InvalidDataException("Current lighting requires the complete prepared camera prefix.");
        return new(preparedCamera);
    }

    internal (Vector4 First, Vector4 Second) PrepareMode6Directions()
    {
        var result = quaternions.Value;
        // These directions also feed the compatibility sun/moon records.
        Finite(firstDirection); Finite(secondDirection);
        return result;
    }

    private static void Finite(Vector3 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new InvalidDataException("Camera light direction requires finite consumed floats.");
    }
    private static float Float(ReadOnlySpan<byte> bytes, int at) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]);
    private static Vector3 Three(ReadOnlySpan<byte> bytes, int at) =>
        new(Float(bytes, at), Float(bytes, at + 4), Float(bytes, at + 8));
    private static Vector4 Four(ReadOnlySpan<byte> bytes, int at) =>
        new(Float(bytes, at), Float(bytes, at + 4), Float(bytes, at + 8), Float(bytes, at + 12));
}
