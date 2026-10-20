using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The selected camera publishes the complete vegetation wind settings.</summary>
internal static class CameraVegetationWindImport
{
    private sealed record Imported(VegetationWindSettings Settings);
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, Imported> selected = new();

    internal static VegetationWindSettings Direct(MapSeasonCameraPackage package)
        => selected.GetValue(package, static source => new(Read(source))).Settings;

    private static VegetationWindSettings Read(MapSeasonCameraPackage source)
    {
        float Scalar(uint hash, int count = 1, int component = 0)
        {
            var field = source.Camera.Properties.SingleOrDefault(value => value.NameHash == hash);
            if (field is null || field.TypeCode != 8 || field.ElementCount != count || field.RawValue.Length != count * 4)
                throw new InvalidDataException($"Selected camera vegetation wind {hash:X8} has an unsupported resource layout.");
            return BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(component * 4, 4)));
        }
        // Direction, generator, jitter and both spatial ranges all participate
        // in the direct selected-camera copy; Main's declaration is a fallback.
        return new(
            Scalar(0xB5CA685Eu, 4, 0), Scalar(0xB5CA685Eu, 4, 1),
            Scalar(0xB5CA685Eu, 4, 2), Scalar(0xB5CA685Eu, 4, 3),
            Scalar(0x105016AAu), Scalar(0x640F1927u), Scalar(0xA28C4EA0u), Scalar(0x8781E6DDu),
            Scalar(0x66B95B5Bu), Scalar(0x8510B38Fu), Scalar(0x98BA0001u), Scalar(0xDB67A6E4u),
            Scalar(0x149DBBA2u), Scalar(0x349E2198u), Scalar(0x74886A4Cu), Scalar(0x251D99AAu), Scalar(0xE257C8DCu));
    }
}
