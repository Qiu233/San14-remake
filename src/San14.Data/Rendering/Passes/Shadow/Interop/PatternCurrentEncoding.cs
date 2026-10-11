using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The current light's optional Pattern modifier. Its plane controls
/// are independent of the seven transform values used to group receivers.</summary>
public readonly record struct PatternLightModifier(bool Present, bool PreservePatternOverride,
    float Strength, Vector2 Plane, Vector2 Scale, Vector2 Velocity, Vector2 Translation);

public static class PatternCurrentEncoding
{
    public const uint PacketSignature = 0x3143504F; // OPC1: ordinary Pattern current inputs.
    public const int CameraByteLength = 32;
    public const int ModifierByteLength = 44;

    public static PatternLightModifier ReadLight(ReadOnlySpan<byte> light, bool modifierPresent)
    {
        if (light.Length != CameraDirectionalLightData.LightDataBytes)
            throw new ArgumentException("Pattern import requires the complete current light.", nameof(light));
        return new(modifierPresent, (BinaryPrimitives.ReadUInt32LittleEndian(light[0x1F4..]) & 8) != 0,
            Scalar(light, 0x290), new(Scalar(light, 0x294), Scalar(light, 0x298)),
            new(Scalar(light, 0x2A0), Scalar(light, 0x2A4)), new(Scalar(light, 0x2A8), Scalar(light, 0x2AC)),
            new(Scalar(light, 0x2B0), Scalar(light, 0x2B4)));
    }

    public static void WriteCamera(PatternCameraSettings camera, Span<byte> destination)
    {
        float[] values = [camera.Scale.X, camera.Scale.Y, camera.Velocity.X, camera.Velocity.Y,
            camera.RotationAngles.X, camera.RotationAngles.Y, camera.RotationAngles.Z];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(destination[(index * 4)..], values[index]);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[28..], camera.MergedTextureOverridesTransform ? 1u : 0u);
    }

    public static void WriteModifier(PatternLightModifier modifier, Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, modifier.Present ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], modifier.PreservePatternOverride ? 1u : 0u);
        float[] values = [modifier.Strength, modifier.Plane.X, modifier.Plane.Y,
            modifier.Scale.X, modifier.Scale.Y, modifier.Velocity.X, modifier.Velocity.Y,
            modifier.Translation.X, modifier.Translation.Y];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(destination[(8 + index * 4)..], values[index]);
    }
    private static float Scalar(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
}
