namespace San14.Data;

/// <summary>MAINNUM1 compatibility fields. Memory offsets belong only to this encoding boundary.</summary>
internal static class CameraStartupEncoding
{
    internal static D3D11MainCameraInitialWord[] Words(CameraStartupSettings settings) =>
    [
        new(0x9ec, BitConverter.SingleToUInt32Bits(settings.Shadow.MaximumDistance)),
        new(0x9f0, BitConverter.SingleToUInt32Bits(settings.Shadow.NearDistance)),
        new(0x9f4, BitConverter.SingleToUInt32Bits(settings.Shadow.FirstSplit)),
        new(0x9f8, BitConverter.SingleToUInt32Bits(settings.Shadow.SecondSplit)),
        new(0x9fc, BitConverter.SingleToUInt32Bits(settings.Shadow.ThirdSplit)),
        new(0xa00, settings.Shadow.LightGroup),
        new(0xa04, settings.Shadow.DirectionSelector),
        new(0xa08, BitConverter.SingleToUInt32Bits(settings.Shadow.FirstDepthBias)),
        new(0xa10, BitConverter.SingleToUInt32Bits(settings.Shadow.SecondDepthBias)),
        new(0xa18, BitConverter.SingleToUInt32Bits(settings.Shadow.ThirdDepthBias)),
        new(0xa20, BitConverter.SingleToUInt32Bits(settings.Shadow.FourthDepthBias)),
        new(0xa28, BitConverter.SingleToUInt32Bits(settings.Shadow.DirectionScale)),
        new(0xa2c, BitConverter.SingleToUInt32Bits(settings.Shadow.FarOverride)),
        new(0xa38, settings.Shadow.DepthFormat),
        new(0xa3c, settings.Shadow.CascadeMode),
        new(0xa40, settings.Shadow.PerCascadeBias),
        new(0xa84, settings.Shadow.RectangleOverride),
        new(0xa88, settings.Shadow.Quality),
        new(0xb1c, BitConverter.SingleToUInt32Bits(settings.TerritoryReferenceHeight)),
        new(0xdc0, BitConverter.SingleToUInt32Bits(settings.Wind.DirectionX)),
        new(0xdc4, BitConverter.SingleToUInt32Bits(settings.Wind.DirectionY)),
        new(0xdc8, BitConverter.SingleToUInt32Bits(settings.Wind.DirectionZ)),
        new(0xdcc, BitConverter.SingleToUInt32Bits(settings.Wind.DirectionW)),
        new(0xdd0, BitConverter.SingleToUInt32Bits(settings.Wind.BaseStrength)),
        new(0xdd4, BitConverter.SingleToUInt32Bits(settings.Wind.StrengthVariation)),
        new(0xdd8, BitConverter.SingleToUInt32Bits(settings.Wind.RiseSeconds)),
        new(0xddc, BitConverter.SingleToUInt32Bits(settings.Wind.FallSeconds)),
        new(0xde0, BitConverter.SingleToUInt32Bits(settings.Wind.PeakSeconds)),
        new(0xde4, BitConverter.SingleToUInt32Bits(settings.Wind.MinimumStrength)),
        new(0xde8, BitConverter.SingleToUInt32Bits(settings.Wind.RestSeconds)),
        new(0xdec, BitConverter.SingleToUInt32Bits(settings.Wind.RestVariation)),
        new(0xdf4, BitConverter.SingleToUInt32Bits(settings.Wind.JitterDegrees)),
        new(0xdf8, BitConverter.SingleToUInt32Bits(settings.Wind.JitterFrequency)),
        new(0x141c, BitConverter.SingleToUInt32Bits(settings.Wind.InteractionExtent)),
        new(0x1438, BitConverter.SingleToUInt32Bits(settings.Wind.WindExtent)),
        new(0x143c, BitConverter.SingleToUInt32Bits(settings.Wind.StrengthScale)),
        new(0x1510, settings.ExposureRectangleFlags)
    ];
    internal static CameraStartupSettings ApplyPreparedOverrides(CameraStartupSettings settings,
        MapSeasonCameraPostEffectScalars? current, uint exposureRectangleFlags)
    {
        uint U(int offset, uint fallback) => current is not null && current.TryReadWord(offset, out uint bits) ? bits : fallback;
        float F(int offset, float fallback) => BitConverter.UInt32BitsToSingle(U(offset, BitConverter.SingleToUInt32Bits(fallback)));
        return new(new ShadowStartupSettings(
            F(0x9ec, settings.Shadow.MaximumDistance),
            F(0x9f0, settings.Shadow.NearDistance),
            F(0x9f4, settings.Shadow.FirstSplit),
            F(0x9f8, settings.Shadow.SecondSplit),
            F(0x9fc, settings.Shadow.ThirdSplit),
            U(0xa00, settings.Shadow.LightGroup),
            U(0xa04, settings.Shadow.DirectionSelector),
            F(0xa08, settings.Shadow.FirstDepthBias),
            F(0xa10, settings.Shadow.SecondDepthBias),
            F(0xa18, settings.Shadow.ThirdDepthBias),
            F(0xa20, settings.Shadow.FourthDepthBias),
            F(0xa28, settings.Shadow.DirectionScale),
            F(0xa2c, settings.Shadow.FarOverride),
            U(0xa38, settings.Shadow.DepthFormat),
            U(0xa3c, settings.Shadow.CascadeMode),
            U(0xa40, settings.Shadow.PerCascadeBias),
            U(0xa84, settings.Shadow.RectangleOverride),
            U(0xa88, settings.Shadow.Quality)),
            new VegetationWindSettings(
            F(0xdc0, settings.Wind.DirectionX),
            F(0xdc4, settings.Wind.DirectionY),
            F(0xdc8, settings.Wind.DirectionZ),
            F(0xdcc, settings.Wind.DirectionW),
            F(0xdd0, settings.Wind.BaseStrength),
            F(0xdd4, settings.Wind.StrengthVariation),
            F(0xdd8, settings.Wind.RiseSeconds),
            F(0xddc, settings.Wind.FallSeconds),
            F(0xde0, settings.Wind.PeakSeconds),
            F(0xde4, settings.Wind.MinimumStrength),
            F(0xde8, settings.Wind.RestSeconds),
            F(0xdec, settings.Wind.RestVariation),
            F(0xdf4, settings.Wind.JitterDegrees),
            F(0xdf8, settings.Wind.JitterFrequency),
            F(0x141c, settings.Wind.InteractionExtent),
            F(0x1438, settings.Wind.WindExtent),
            F(0x143c, settings.Wind.StrengthScale)),
            F(0xb1c, settings.TerritoryReferenceHeight), exposureRectangleFlags);
    }
}
