using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Typed projections for the current-season contact-occlusion packet.
/// The caller owns its signature, ordered light identities and packet lifecycle.</summary>
public static class ContactOcclusionCurrentEncoding
{
    public static void WriteSettings(BinaryWriter writer, CameraOcclusionSettings value)
    {
        writer.Write(value.Radius); writer.Write(value.EndRadius);
        writer.Write(value.RangeStart); writer.Write(value.RangeEnd);
        writer.Write(value.MaxRadiusRate); writer.Write(value.Contrast);
        writer.Write(value.MaxOcclusion); writer.Write(value.AngleBiasDegrees);
        writer.Write(value.Quality); writer.Write(value.Resolution);
        writer.Write(value.UseNormal); writer.Write(value.UseViewDepth);
        writer.Write(value.RayCount); writer.Write(value.StepCount);
        writer.Write(value.BlurSize); writer.Write(value.BlurSharpness);
        writer.Write(value.BlurLinearFilter); writer.Write(value.BlurAtHalfSize);
        writer.Write(value.SuppressHalo);
    }

    public static void WriteModifier(BinaryWriter writer, ContactOcclusionLightModifier value)
    {
        writer.Write(value.Present ? 1u : 0u); writer.Write(value.SuppressCopy ? 1u : 0u);
        writer.Write(value.Fade); writer.Write(value.Radius); writer.Write(value.EndRadius);
        writer.Write(value.RangeStart); writer.Write(value.RangeEnd); writer.Write(value.MaxRadiusRate);
        writer.Write(value.Contrast); writer.Write(value.MaxOcclusion);
        writer.Write(value.AngleBias); writer.Write(value.StepCount);
    }

    internal static ContactOcclusionLightModifier ImportLight(ReadOnlySpan<byte> bytes, bool present)
    {
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x1F4..]);
        return new(present, (flags & 0x20) != 0,
            Read(bytes, 0x2D0), Read(bytes, 0x2D4), Read(bytes, 0x2D8), Read(bytes, 0x2DC), Read(bytes, 0x2E0),
            Read(bytes, 0x2E4), Read(bytes, 0x2E8), Read(bytes, 0x2EC), Read(bytes, 0x2F0), Read(bytes, 0x2F4));
    }
    private static float Read(ReadOnlySpan<byte> bytes, int offset)
        => BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
}
