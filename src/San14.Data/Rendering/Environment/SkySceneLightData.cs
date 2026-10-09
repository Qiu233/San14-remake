using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Caller-resolved ordinary directional rows. Indices are not inferred
/// from Sun/Moon names, Camera high bits, primary-light flags or cached pointers.</summary>
public sealed record SkySceneLightRows(string SourceProvenance,
    int? SunRow, int? MoonRow, int PackageLightRow, bool RunSunRgbSetter, bool RunMoonRgbSetter);

/// <summary>Bridges explicit local LightData rows to Sky2 source inputs. The
/// source producer may first apply OriginalCameraDirectionalLightData to a
/// prepared Camera packet. This helper does not resolve publishers, visibility,
/// fallback lights, or the native setter/commit schedule.</summary>
public static class SkySceneLightData
{
    public static SkySceneLights ApplyOrdinaryRows(
        SkySceneLights previousSource, ReadOnlySpan<byte> explicitCompleteLightData,
        SkySceneLightRows explicitRows)
    {
        ArgumentNullException.ThrowIfNull(previousSource); ArgumentNullException.ThrowIfNull(explicitRows);
        if (explicitCompleteLightData.Length != CameraDirectionalLightData.LightDataBytes ||
            string.IsNullOrWhiteSpace(explicitRows.SourceProvenance) ||
            explicitRows.PackageLightRow is < 0 or > 3 || explicitRows.SunRow is < 0 or > 3 ||
            explicitRows.MoonRow is < 0 or > 3 ||
            (explicitRows.RunSunRgbSetter && explicitRows.SunRow is null) ||
            (explicitRows.RunMoonRgbSetter && explicitRows.MoonRow is null))
            throw new InvalidDataException("Sky LightData bridge requires complete source bytes and explicit ordinary row/setter selections.");
        Vector4 sun = previousSource.RawSunPosition, moon = previousSource.RawMoonPosition;
        var sunCache = previousSource.SunRgbCache1460;
        Vector3 moonRgb = previousSource.RawMoonRgb; float moonMaximum = previousSource.MoonMaximum147C;
        if (explicitRows.SunRow is int sunIndex)
        {
            sun = ReadVector(explicitCompleteLightData, sunIndex * 32);
            if (explicitRows.RunSunRgbSetter)
                sunCache = Sky2CameraLightPreparation.PrepareRgbCache(ReadRgb(explicitCompleteLightData, sunIndex * 32 + 16));
        }
        if (explicitRows.MoonRow is int moonIndex)
        {
            moon = ReadVector(explicitCompleteLightData, moonIndex * 32);
            moonRgb = ReadRgb(explicitCompleteLightData, moonIndex * 32 + 16);
            if (explicitRows.RunMoonRgbSetter)
                moonMaximum = Sky2CameraLightPreparation.PrepareRgbCache(moonRgb).Maximum;
        }
        return new(explicitRows.SourceProvenance, explicitRows.SunRow is not null, sun, sunCache,
            explicitRows.MoonRow is not null, moon, moonRgb, moonMaximum,
            ReadVector(explicitCompleteLightData, explicitRows.PackageLightRow * 32));
    }

    private static Vector4 ReadVector(ReadOnlySpan<byte> bytes, int offset)
        => new(Read(bytes, offset), Read(bytes, offset + 4), Read(bytes, offset + 8), Read(bytes, offset + 12));
    private static Vector3 ReadRgb(ReadOnlySpan<byte> bytes, int offset)
        => new(Read(bytes, offset), Read(bytes, offset + 4), Read(bytes, offset + 8));
    private static float Read(ReadOnlySpan<byte> bytes, int offset)
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4));
        if (!float.IsFinite(value)) throw new InvalidDataException("Selected Sky2 LightData field is outside the finite packet domain.");
        return value;
    }
}
