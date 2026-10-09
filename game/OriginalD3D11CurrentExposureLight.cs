using System.Buffers.Binary;
using System.Numerics;

/// <summary>Current View1B20 exposure input from the owned five-light publication.
/// Only the consumed source fields cross the native boundary; no allocation
/// image, resource handles or retained constructor holes are serialized.</summary>
public readonly record struct OriginalD3D11CurrentExposureLight(
    uint AuxiliarySelector, uint ObjectId, bool Present, byte Type, byte Flags,
    Vector3 BaselineRgb)
{
    public const int ByteLength = 32;

    internal static OriginalD3D11CurrentExposureLight Capture(uint auxiliarySelector,
        IReadOnlyList<OriginalD3D11CurrentSeasonLight> lights)
    {
        if (auxiliarySelector == 0) return new(auxiliarySelector, 0, false, 0, 0, default);
        uint mask = 1u << (int)((auxiliarySelector - 1) & 31);
        var selected = lights.FirstOrDefault(light => (light.GroupMask & ~mask) == 0);
        if (selected is null) return new(auxiliarySelector, 0, false, 0, 0, default);
        var bytes = selected.CompleteData.Span;
        return new(auxiliarySelector, selected.ObjectId, true, bytes[0x10B0], bytes[0xFDC],
            new(BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(0xFA0, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(0xFA4, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(0xFA8, 4))));
    }

    internal void WriteNativeBytes(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, AuxiliarySelector);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], ObjectId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], Present ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], Type);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], Flags);
        BinaryPrimitives.WriteSingleLittleEndian(destination[20..], BaselineRgb.X);
        BinaryPrimitives.WriteSingleLittleEndian(destination[24..], BaselineRgb.Y);
        BinaryPrimitives.WriteSingleLittleEndian(destination[28..], BaselineRgb.Z);
    }
}
