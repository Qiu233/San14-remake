using System.Buffers.Binary;
using San14.Data;

/// <summary>Exposure light import and native numeric transport.</summary>
internal static class D3D11CurrentExposureEncoding
{
    public const int ByteLength = 32;

    internal static LightExposureSettings ReadLight(ReadOnlySpan<byte> bytes)
        => new(bytes[0x10B0], bytes[0xFDC],
            new(BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(0xFA0, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(0xFA4, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(0xFA8, 4))));

    internal static void Write(D3D11CurrentExposureLight value, Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, value.AuxiliarySelector);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], value.ObjectId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], value.Present ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], value.Type);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], value.Flags);
        BinaryPrimitives.WriteSingleLittleEndian(destination[20..], value.BaselineRgb.X);
        BinaryPrimitives.WriteSingleLittleEndian(destination[24..], value.BaselineRgb.Y);
        BinaryPrimitives.WriteSingleLittleEndian(destination[28..], value.BaselineRgb.Z);
    }
}
