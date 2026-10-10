using System.Buffers.Binary;

namespace San14.Data;

public static partial class CameraOpticsEncoding
{
    internal static CameraOpticsSettings DecodeFinalOutput(ReadOnlySpan<byte> encoded)
    {
        byte[] bytes = encoded.ToArray();
        float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at, 4));
        uint U(int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        return new()
        {
            Exposure = new() { Gamma = F(0x174) },
            Output = new()
            {
                BloomIntensity = F(0x180), StarIntensity = F(0x184), LensFlareIntensity = F(0x188),
                ColorScaleX = F(0x18C), ColorScaleY = F(0x190), ColorScaleZ = F(0x194),
                SaturationScaleX = F(0x198), SaturationScaleY = F(0x19C), SaturationScaleZ = F(0x1A0)
            },
            Fxaa = new() { EdgeThreshold = F(0x404), EdgeThresholdMin = F(0x408), EdgeSharpness = F(0x40C), PixelRange = F(0x410) },
            Limb = new() { Weight = F(0x7B0) },
            DepthOfField = new() { DistantBlurZThreshold = F(0xE78), DistantBlurIntensity = F(0xE7C) },
            ColorGrading = new() { Lut1 = U(0x13B0), Lut2 = U(0x13B4), InterpolationRate = F(0x13B8), Intensity = F(0x13BC) }
        };
    }
    internal static CameraExposureSettings DecodeExposure(ReadOnlySpan<byte> encoded) => new()
    {
        UserScale = BinaryPrimitives.ReadSingleLittleEndian(encoded.Slice(0x170, 4)),
        Gamma = BinaryPrimitives.ReadSingleLittleEndian(encoded.Slice(0x174, 4)),
        Luminance = BinaryPrimitives.ReadSingleLittleEndian(encoded.Slice(0x17C, 4))
    };
}
