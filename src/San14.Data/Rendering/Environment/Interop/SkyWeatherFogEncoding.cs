using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Known Fog72 and weather camera fields at the transport boundary.</summary>
public static class SkyWeatherFogEncoding
{
    public static byte[] Encode(SkyWeatherFogValues fog)
    {
        byte[] bytes = new byte[72];
        Put4(bytes, 0, fog.Background);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x10), 7);
        Put(bytes, 0x14, fog.Start); Put(bytes, 0x18, fog.End); Put(bytes, 0x1C, fog.PlanetRadius);
        Put3(bytes, 0x20, fog.RayleighCoefficients); Put(bytes, 0x2C, fog.Density);
        Put3(bytes, 0x30, fog.MieCoefficients); Put(bytes, 0x3C, fog.Anisotropy);
        Put(bytes, 0x40, fog.Pivot); Put(bytes, 0x44, fog.Blend);
        return bytes;
    }

    public static byte[] MaterializeRecord(SkyWeatherFogValues fog, ReadOnlySpan<byte> tail)
    {
        if (tail.Length != 24) throw new InvalidDataException("Fog96 requires its explicit retained24-byte tail.");
        byte[] record = new byte[96]; Encode(fog).CopyTo(record, 0); tail.CopyTo(record.AsSpan(72)); return record;
    }

    public static byte[] ApplyToCamera(ReadOnlySpan<byte> retainedCamera, SkyWeatherResult weather)
    {
        byte[] prefix = retainedCamera.ToArray();
        Put(prefix, 0x22C, weather.Saturation);
        Encode(weather.Fog.Values).CopyTo(prefix, 0x340);
        return prefix;
    }

    internal static SkyFogProjectionInputs DecodeProjection(ReadOnlySpan<byte> record, int profile, bool enabled)
    {
        if (record.Length != 96) throw new InvalidDataException("Original sky fog requires the complete96-byte source record.");
        uint mode = BinaryPrimitives.ReadUInt32LittleEndian(record[0x10..]);
        bool active = enabled && mode != 0;
        float pivot=0, blend=0, radius=0, start=0, end=0;
        if (profile != 1 || active)
        { pivot=Read(record,0x40); blend=Read(record,0x44); radius=Read(record,0x1C); }
        if (profile != 1 && !active)
        { start=Read(record,0x14); end=Read(record,0x18); }
        return new(mode,pivot,blend,radius,start,end);
    }

    private static float Read(ReadOnlySpan<byte> record, int at)
    {
        float value=BinaryPrimitives.ReadSingleLittleEndian(record[at..]);
        if (!float.IsFinite(value)) throw new InvalidDataException($"Nonfinite original mode7 fog input at record+{at:X}.");
        return value;
    }

    private static void Put(Span<byte> bytes, int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes[at..], value);
    private static void Put3(Span<byte> bytes, int at, Vector3 v)
    { Put(bytes, at, v.X); Put(bytes, at+4, v.Y); Put(bytes, at+8, v.Z); }
    private static void Put4(Span<byte> bytes, int at, Vector4 v)
    { Put3(bytes, at, new(v.X,v.Y,v.Z)); Put(bytes, at+12, v.W); }
}
