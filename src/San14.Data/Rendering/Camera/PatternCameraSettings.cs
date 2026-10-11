using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Authored ordinary Pattern transform. Camera kind, live resource
/// bindings, current time and light modifiers belong to their own owners.
/// RotationAngles retain the declaration's degree units.</summary>
public sealed record PatternCameraSettings(Vector2 Scale, Vector2 Velocity,
    Vector3 RotationAngles, bool MergedTextureOverridesTransform);

public static class PatternCameraSettingsImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, PatternCameraSettings> Imported = new();

    public static PatternCameraSettings Load(MapSeasonCameraPackage package)
        => Imported.GetValue(package, Read);

    private static PatternCameraSettings Read(MapSeasonCameraPackage package)
    {
        if (package.Camera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId)
            throw new InvalidDataException("Pattern settings require a Perspective camera declaration.");
        uint Word(uint hash, ushort type)
        {
            var fields = package.Camera.Properties.Where(field => field.NameHash == hash).ToArray();
            if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != 1 ||
                fields[0].ElementSize != 4 || fields[0].RawValue.Length != 4)
                throw new InvalidDataException("Pattern settings require a unique typed scalar declaration.");
            return BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
        }
        float Scalar(uint hash) => BitConverter.UInt32BitsToSingle(Word(hash, 8));
        return new(new(Scalar(0x2280A0FA), Scalar(0xC3D45CFC)),
            new(Scalar(0x700D565D), Scalar(0x1161125F)),
            new(Scalar(0xC3FB2C2F), Scalar(0x35884FC0), Scalar(0x448A1A29)),
            Word(0x46D194A5, 4) != 0);
    }
}
