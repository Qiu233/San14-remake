using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Import View controls once per selected camera resource generation.</summary>
public static class CameraViewSettingsImport
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, CameraViewSettings> imported = new();

    public static CameraViewSettings Load(MapSeasonCameraPackage package)
        => imported.GetValue(package, static value => Read(value));

    private static CameraViewSettings Read(MapSeasonCameraPackage package)
    {
        if (package.Camera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId)
            throw new InvalidDataException("Map View controls require a Perspective Camera declaration.");
        var p = new CameraNumericPropertyReader(package.Camera);
        uint Word(int ordinal, uint hash, uint type)
        {
            if (package.Camera.Properties[ordinal].ElementSize != 4)
                throw new InvalidDataException("Map View controls require four-byte resource elements.");
            return p.Word(ordinal, hash, type);
        }
        float Scalar(int ordinal, uint hash) => BitConverter.UInt32BitsToSingle(Word(ordinal, hash, 8));
        return new(new CameraShadowViewSettings(
            Scalar(21, 0xEACC4096), Scalar(23, 0xDE2CE52E),
            Scalar(238, 0x7E026E9C), Scalar(239, 0xD7DDD8DD),
            unchecked((int)Word(24, 0x0611DBC4, 4)), Word(1061, 0x2CEF0805, 4),
            Scalar(129, 0x0D8D3389), Scalar(743, 0x9932C840),
            Word(229, 0xA3FB1464, 5), Word(26, 0x96E32C4A, 4),
            Word(578, 0x7D733C4E, 4), Word(975, 0x3E757721, 4)),
            Word(65, 0xB258D1A7, 5), Word(701, 0x6EA5A887, 4));
    }
}
