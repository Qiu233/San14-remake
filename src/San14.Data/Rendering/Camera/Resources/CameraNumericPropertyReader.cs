using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Validate and decode a numeric resource property at the import boundary.</summary>
internal readonly struct CameraNumericPropertyReader(DokObject camera)
{
    internal uint Word(int ordinal, uint hash, uint type, int count = 1, int component = 0)
    {
        if ((uint)ordinal >= camera.Properties.Count)
            throw new InvalidDataException($"Camera numeric property {ordinal} is missing from its declaration.");
        var field = camera.Properties[ordinal];
        if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != type ||
            field.ElementCount != count || field.RawValue.Length != count * 4)
            throw new InvalidDataException($"Camera numeric property {ordinal} has an unsupported resource layout.");
        return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(component * 4, 4));
    }
    internal float Scalar(int ordinal, uint hash, int count = 1, int component = 0)
        => BitConverter.UInt32BitsToSingle(Word(ordinal, hash, 8, count, component));
}
