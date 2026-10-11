using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Authored Star controls of one selected Camera. Their names retain
/// the declaration's meaning; runtime angles, rates and lifetimes are separate.</summary>
public sealed record SkyStarSettings(float ExposureScale, float DiameterTexel, float BlinkSpeed,
    float ShowerPeakRa, float ShowerPeakDe, float ShowerSpeed, float MeteorIncidents,
    float MeteorIncidentsFade, float ShutterSpeedScale)
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, SkyStarSettings> imported = new();

    public static SkyStarSettings Import(MapSeasonCameraPackage package)
        => imported.GetValue(package, static source => new(
            Read(source, 0x59633E4E), Read(source, 0x07F77082), Read(source, 0x4C16B0AE),
            Read(source, 0xAB79BFDB), Read(source, 0x3303DCAD), Read(source, 0x8B312E46),
            Read(source, 0x9E690338), Read(source, 0x3625C91C), Read(source, 0x268ED81D)));

    private static float Read(MapSeasonCameraPackage package, uint name)
    {
        var field = package.Camera.Properties.Single(value => value.NameHash == name);
        if (field.TypeCode != 8 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
            throw new InvalidDataException($"Star Camera property {name:X8} requires its float scalar declaration.");
        return BinaryPrimitives.ReadSingleLittleEndian(field.RawValue);
    }
}
