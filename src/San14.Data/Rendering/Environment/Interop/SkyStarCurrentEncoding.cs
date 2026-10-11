using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Current Main Star author settings and prepared controller transform.
/// The native owner retains particle and twinkle history independently.</summary>
public static class SkyStarCurrentEncoding
{
    public const int ByteLength = 108;
    public const string NativeFilename = "current-state.bin";

    public static void Write(SkyStarSettings settings, Matrix4x4 geometryToWorld,
        float brightness, Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            throw new ArgumentException("Main Star needs its complete current packet.", nameof(destination));
        BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x3153534D); // MSS1
        float[] values = [settings.ExposureScale, settings.DiameterTexel, settings.BlinkSpeed,
            settings.ShowerPeakRa, settings.ShowerPeakDe, settings.ShowerSpeed, settings.MeteorIncidents,
            settings.MeteorIncidentsFade, settings.ShutterSpeedScale,
            geometryToWorld.M11, geometryToWorld.M12, geometryToWorld.M13, geometryToWorld.M14,
            geometryToWorld.M21, geometryToWorld.M22, geometryToWorld.M23, geometryToWorld.M24,
            geometryToWorld.M31, geometryToWorld.M32, geometryToWorld.M33, geometryToWorld.M34,
            geometryToWorld.M41, geometryToWorld.M42, geometryToWorld.M43, geometryToWorld.M44, brightness];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(destination[(4 + index * 4)..], values[index]);
    }

    public static void WriteNativeInputs(string directory, SkyStarSettings settings,
        Matrix4x4 geometryToWorld, float brightness)
    {
        Directory.CreateDirectory(directory);
        byte[] bytes = new byte[ByteLength];
        Write(settings, geometryToWorld, brightness, bytes);
        File.WriteAllBytes(Path.Combine(directory, NativeFilename), bytes);
    }
}
