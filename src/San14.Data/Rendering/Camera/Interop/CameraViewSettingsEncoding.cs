namespace San14.Data;

/// <summary>Selected and current camera compatibility fields. Numeric offsets
/// are confined to this transport boundary.</summary>
public static class CameraViewSettingsEncoding
{
    public static IEnumerable<KeyValuePair<int, uint>> ShadowWords(CameraViewSettings settings)
    {
        var shadow = settings.Shadow;
        yield return new(0x9EC, BitConverter.SingleToUInt32Bits(shadow.MaximumDistance));
        yield return new(0x9F4, BitConverter.SingleToUInt32Bits(shadow.FirstSplit));
        yield return new(0x9F8, BitConverter.SingleToUInt32Bits(shadow.SecondSplit));
        yield return new(0x9FC, BitConverter.SingleToUInt32Bits(shadow.ThirdSplit));
        yield return new(0xA00, unchecked((uint)shadow.LightGroup));
        yield return new(0xA04, shadow.DirectionSelector);
        yield return new(0xA28, BitConverter.SingleToUInt32Bits(shadow.DirectionScale));
        yield return new(0xA2C, BitConverter.SingleToUInt32Bits(shadow.FarOverride));
        yield return new(0xA38, shadow.DepthFormat);
        yield return new(0xA3C, shadow.CascadeMode);
        yield return new(0xA84, shadow.RectangleOverride);
        yield return new(0xA88, shadow.Quality);
    }

    public static CameraViewSettings ApplyPreparedOverrides(CameraViewSettings settings,
        MapSeasonCameraPostEffectScalars current)
    {
        uint U(int offset, uint fallback) => current.TryReadWord(offset, out uint bits) ? bits : fallback;
        float F(int offset, float fallback) => BitConverter.UInt32BitsToSingle(U(offset, BitConverter.SingleToUInt32Bits(fallback)));
        var shadow = settings.Shadow;
        return new(new CameraShadowViewSettings(
            F(0x9EC, shadow.MaximumDistance), F(0x9F4, shadow.FirstSplit),
            F(0x9F8, shadow.SecondSplit), F(0x9FC, shadow.ThirdSplit),
            unchecked((int)U(0xA00, unchecked((uint)shadow.LightGroup))), U(0xA04, shadow.DirectionSelector),
            F(0xA28, shadow.DirectionScale), F(0xA2C, shadow.FarOverride),
            U(0xA38, shadow.DepthFormat), U(0xA3C, shadow.CascadeMode),
            U(0xA84, shadow.RectangleOverride), U(0xA88, shadow.Quality)),
            U(0xAC0, settings.OceanGroup), U(0x12A8, settings.RainGroup));
    }
}
