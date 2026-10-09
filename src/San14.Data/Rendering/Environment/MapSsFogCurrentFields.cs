using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Selected C358A0→V418 numeric inputs for the three normal SSFog
/// layers and mask. Import once per loaded seasonal package. Current Main
/// post/quality words must subsequently overwrite their own destinations.</summary>
public static class MapSsFogCurrentFields
{
    public const int PreparedWindowBegin = 0x840;
    public const int PreparedWindowEnd = 0x9CC;
    public const int PreparedWindowWordCount = (PreparedWindowEnd - PreparedWindowBegin) / 4;

    public static byte[] ReadSelectedWordWindow(MapSeasonCameraPackage package,
        LoadedMainMapRawCameraNumericConstructionSource construction,
        LoadedMainMapRawCameraPreparationSource preparation)
    {
        ArgumentNullException.ThrowIfNull(package); ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(preparation);
        if (package.Camera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId)
            throw new InvalidDataException("SSFog inputs require the actual selected Perspective Camera.");
        byte[] result = new byte[PreparedWindowWordCount * 4];
        for (int prepared = PreparedWindowBegin; prepared < PreparedWindowEnd; prepared += 4)
        {
            int destination = LoadedMainMapRawCameraPreparationSource.EmbeddedCameraOffset + prepared;
            var range = preparation.NumericRanges.Single(r => r.DestinationCpuOffset <= destination &&
                r.DestinationCpuOffset + r.WordCount * 4 > destination);
            int raw = range.SourceCpuOffset + destination - range.DestinationCpuOffset;
            var binding = construction.DirectCpuPropertyBindings.Single(b => b.CpuOffset <= raw &&
                b.CpuOffset + b.ByteCount >= raw + 4);
            var field = package.Camera.Properties[binding.Ordinal];
            if (field.Index != binding.Ordinal || field.NameHash != binding.NameHash || field.TypeCode != binding.TypeCode ||
                field.ElementCount != binding.ElementCount || field.RawValue.Length != binding.ByteCount)
                throw new InvalidDataException("SSFog input differs from selected raw loader field at" + binding.Ordinal);
            field.RawValue.AsSpan(raw - binding.CpuOffset, 4).CopyTo(result.AsSpan(prepared - PreparedWindowBegin, 4));
        }
        return result;
    }
}
