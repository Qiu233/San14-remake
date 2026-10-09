using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The third SSFog layer's selected Camera word. The first two
/// prepared modes have MainPostSelection writers; this word retains the
/// selected C358A0 direct copy followed by C409C0/V418. Not raw CPU94C.</summary>
public static class MapSsFogCurrentLayerSource
{
    public const int PreparedThirdModeOffset = 0x94C;
    public const int RawThirdModeOffset = 0x6C0;
    public const int SelectedPropertyOrdinal = 537;
    public const uint SelectedPropertyHash = 0xEB090BC5;

    /// <summary>Import once with the selected source package. Paired Camera
    /// interpolation and later explicit layer edits require their own writer.</summary>
    public static uint ReadSelectedThirdLayerMode(MapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Camera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId)
            throw new InvalidDataException("SSFog third layer needs the actual selected Perspective Camera.");
        var field = package.Camera.Properties[SelectedPropertyOrdinal];
        if (field.Index != SelectedPropertyOrdinal || field.NameHash != SelectedPropertyHash ||
            field.TypeCode != 4 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
            throw new InvalidDataException("SSFog third layer differs from its original raw6C0 loader binding.");
        return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
    }
}
