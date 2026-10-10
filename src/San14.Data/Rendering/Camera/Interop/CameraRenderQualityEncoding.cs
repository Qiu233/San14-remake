using System.Collections.Immutable;

namespace San14.Data;

internal static class CameraRenderQualityEncoding
{
    internal static CameraRenderQualityPatch Import(IEnumerable<KeyValuePair<int, uint>> encodedWrites)
    {
        var result = new CameraRenderQualityPatch();
        var compatibility = ImmutableArray.CreateBuilder<CameraCompatibilityFieldWrite>();
        foreach (var (at, bits) in encodedWrites)
        {
            float value = BitConverter.UInt32BitsToSingle(bits);
            result = at switch
            {
                0x1C4 => result with { LightAdaptationScale = value },
                0x1C8 => result with { DarkAdaptationScale = value },
                0x1D0 => result with { Cloudiness = value },
                0x210 => result with { AltitudeOffset = value },
                0x674 => result with { CloudLowCostMode = bits },
                0x7A0 => result with { CloudResolutionMode = bits },
                0x840 => result with { PrimaryFogFade = bits },
                0x84C => result with { PrimaryFogMode = bits },
                0x8E0 => result with { SecondaryFogFade = bits },
                0x868 => result with { SecondaryFogMode = bits },
                _ => result
            };
            if (at is not (0x1C4 or 0x1C8 or 0x1D0 or 0x210 or 0x674 or 0x7A0 or 0x840 or 0x84C or 0x8E0 or 0x868))
                compatibility.Add(new(at, bits));
        }
        return result with { Compatibility = compatibility.ToImmutable() };
    }
    internal static IEnumerable<KeyValuePair<int, uint>> WrittenWords(CameraRenderQualityPatch patch)
    {
        if (patch.LightAdaptationScale is float light) yield return new(0x1C4, BitConverter.SingleToUInt32Bits(light));
        if (patch.DarkAdaptationScale is float dark) yield return new(0x1C8, BitConverter.SingleToUInt32Bits(dark));
        if (patch.Cloudiness is float cloud) yield return new(0x1D0, BitConverter.SingleToUInt32Bits(cloud));
        if (patch.AltitudeOffset is float altitude) yield return new(0x210, BitConverter.SingleToUInt32Bits(altitude));
        if (patch.CloudLowCostMode is uint lowCost) yield return new(0x674, lowCost);
        if (patch.CloudResolutionMode is uint resolution) yield return new(0x7A0, resolution);
        if (patch.PrimaryFogFade is uint fade) yield return new(0x840, fade);
        if (patch.PrimaryFogMode is uint fog) yield return new(0x84C, fog);
        if (patch.SecondaryFogFade is uint secondFade) yield return new(0x8E0, secondFade);
        if (patch.SecondaryFogMode is uint secondFog) yield return new(0x868, secondFog);
        foreach (var field in patch.Compatibility) yield return new(field.EncodedOffset, field.Bits);
    }
}
