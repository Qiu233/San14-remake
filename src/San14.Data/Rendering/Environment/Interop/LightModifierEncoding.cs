using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Shared interop encoding for named light modifier groups. Constructor roles supply their own defaults.</summary>
internal static class LightModifierEncoding
{
    internal static void WriteIndexedRim(Span<byte> destination, int index, DirectionalIndexedRimSettings value)
    {
        Put(destination, 0x388 + index * 16, value.ScaleSharp);
        Put(destination, 0x488 + index * 1, value.Mode);
        Put(destination, 0x498 + index * 1, value.Target);
        Put(destination, 0x4A8 + index * 1, value.ExtensionIndex);
        Put(destination, 0x4B8 + index * 16, value.Emission);
        Put(destination, 0x5B8 + index * 16, value.EmissionCurve);
        Put(destination, 0x6B8 + index * 32, value.ReflectionMainAndSub);
        Put(destination, 0x6C8 + index * 32, value.ReflectionLocalAndAmbient);
        Put(destination, 0x8B8 + index * 16, value.Transmission);
    }

    internal static void WriteDirtChannel(Span<byte> destination, int index, DirectionalDirtChannelSettings value)
    {
        Put(destination, 0xB9C + index * 4, value.MaskFade);
        Put(destination, 0xBAC + index * 4, value.TextureRepeat);
        Put(destination, 0xBBC + index * 64, value.Albedo);
        Put(destination, 0xBCC + index * 64, value.Reflectance);
        Put(destination, 0xBD0 + index * 64, value.Glossiness);
        Put(destination, 0xBD4 + index * 64, value.Occlusion);
        Put(destination, 0xBD8 + index * 64, value.ReflectanceAlpha);
        Put(destination, 0xBDC + index * 64, value.NormalX);
        Put(destination, 0xBE0 + index * 64, value.NormalY);
        Put(destination, 0xBE4 + index * 64, value.NormalZ);
        Put(destination, 0xBE8 + index * 64, value.NormalW);
        Put(destination, 0xBEC + index * 64, value.Emission);
    }

    internal static void WriteReflectanceLayer(Span<byte> destination, int index, DirectionalReflectanceLayerSettings value)
    {
        Put(destination, 0xCC4 + index * 4, value.FirstLayerIor);
        Put(destination, 0xCD4 + index * 4, value.FirstLayerGlossiness);
        Put(destination, 0xCE4 + index * 12, value.FirstLayerAbsorbance);
        Put(destination, 0xD14 + index * 12, value.SecondLayerIorReal);
        Put(destination, 0xD44 + index * 12, value.SecondLayerIorImaginary);
    }

    internal static void WriteMaterial(Span<byte> destination, DirectionalMaterialSettings value, DirectionalUnsignedPair flags)
    {
        Put(destination, 0x1B0, value.Diffuse);
        Put(destination, 0x1BC, value.Transparency);
        Put(destination, 0x1C0, value.Ambient);
        Put(destination, 0x1CC, value.Specular);
        Put(destination, 0x1D8, value.SpecularDecay);
        Put(destination, 0x1DC, value.Emission);
        Put(destination, 0x1E8, value.AlphaThreshold);
        Put(destination, 0x1EC, flags);
    }

    internal static void WriteLightMaps(Span<byte> destination, DirectionalLightMapsSettings value)
    {
        Put(destination, 0x1FC, value.DiffuseScaler0);
        Put(destination, 0x208, value.DiffuseScaler1);
        Put(destination, 0x214, value.DiffuseScaler2);
        Put(destination, 0x220, value.DiffuseScaler3);
        Put(destination, 0x22C, value.SpecularScaler0);
        Put(destination, 0x238, value.SpecularScaler1);
        Put(destination, 0x244, value.SpecularScaler2);
        Put(destination, 0x250, value.ShadowScaler);
        Put(destination, 0x25C, value.EtcScaleX);
        Put(destination, 0x260, value.EtcScaleZ);
        Put(destination, 0x264, value.MaxAmbientOcclusion);
        Put(destination, 0x268, value.DOFade);
        Put(destination, 0x26C, value.AmbientDiffuseScaler);
    }

    internal static void WriteSubsurface(Span<byte> destination, DirectionalSubsurfaceSettings value)
    {
        Put(destination, 0x2C0, value.Width);
        Put(destination, 0x2C4, value.Translucency);
        Put(destination, 0x2C8, value.TransThreshold);
    }

    internal static void WriteOcclusion(Span<byte> destination, DirectionalOcclusionSettings value)
    {
        Put(destination, 0x2CC, value.Scale);
        Put(destination, 0x2D0, value.SSAOFade);
        Put(destination, 0x2D4, value.SSAORadius);
        Put(destination, 0x2D8, value.SSAOEndRadius);
        Put(destination, 0x2DC, value.SSAOLerpStart);
        Put(destination, 0x2E0, value.SSAOLerpEnd);
        Put(destination, 0x2E4, value.SSAOMaxRadiusRate);
        Put(destination, 0x2E8, value.SSAOContrast);
        Put(destination, 0x2EC, value.SSAOMaxOcclusion);
        Put(destination, 0x2F0, value.SSAOAngleBias);
        Put(destination, 0x2F4, value.SSAOStepNum);
        Put(destination, 0x2F8, value.AlbedoFade);
    }

    internal static void WriteHatchingShadow(Span<byte> destination, DirectionalHatchingShadowSettings value)
    {
        Put(destination, 0xAD8, value.RampScale);
        Put(destination, 0xADC, value.RampFade);
        Put(destination, 0xAE4, value.IndexedRampScale);
        Put(destination, 0xAE8, value.OccScale);
        Put(destination, 0xAEC, value.OccFade);
        Put(destination, 0xAF4, value.UVRepeat);
        Put(destination, 0xAFC, value.UVRot);
        Put(destination, 0xB00, value.UVVelocity);
        Put(destination, 0xB08, value.UVUpdateInterval);
    }

    internal static void WriteAlbedoRetouch(Span<byte> destination, DirectionalAlbedoRetouchSettings value)
    {
        Put(destination, 0xD74, value.FalloffScale);
        Put(destination, 0xD84, value.FalloffScaledOffset);
        Put(destination, 0xD94, value.ShadowColorLimit);
        Put(destination, 0xDA0, value.RimLightColor);
        Put(destination, 0xDAC, value.SpecularLightColor);
        Put(destination, 0xDB8, value.SpecularPower);
        Put(destination, 0xDBC, value.ReflectionFade);
        Put(destination, 0xDC0, value.ToonElevation);
        Put(destination, 0xDC4, value.ToonAzimuth);
        Put(destination, 0xDC8, value.ToonLightDirRelativeMode);
        Put(destination, 0xDCC, value.RimElevation);
        Put(destination, 0xDD0, value.RimAzimuth);
        Put(destination, 0xDD4, value.RimLightDirRelativeMode);
        Put(destination, 0xDD8, value.SpecularElevation);
        Put(destination, 0xDDC, value.SpecularAzimuth);
        Put(destination, 0xDE0, value.SpecularLightDirRelativeMode);
        Put(destination, 0xDE4, value.AlbedoRetouch2PB2HairHighlightRampThreshold);
    }

    internal static void WriteLightingScales(Span<byte> destination, DirectionalIndirectLightingSettings indirect,
        DirectionalSurfaceEffectsSettings surface, DirectionalSubLightsSettings subLights, float hemisphereScale)
    {
        Put(destination, 0x2FC, indirect.IDLScalerOfLPV);
        Put(destination, 0x300, indirect.IDLFade);
        Put(destination, 0x304, indirect.ISLFade);
        Put(destination, 0x308, indirect.ISLScalerOfAmbientSpecularMap);
        Put(destination, 0x30C, indirect.ISLScalerOfAmbientSpecularMapColorRGB);
        Put(destination, 0x318, indirect.IDLScalerOfAmbientDiffuseMap);
        Put(destination, 0x31C, indirect.IDLScalerOfAmbientDiffuseMapColorRGB);
        Put(destination, 0x328, indirect.ISLFadeOfRLR);
        Put(destination, 0x32C, indirect.DDLFade);
        Put(destination, 0x330, indirect.DSLFade);
        Put(destination, 0x33C, indirect.LASSpecularScale);
        Put(destination, 0x340, indirect.LASDiffuseScale);
        Put(destination, 0x344, surface.GlossinessFadeStartDistance);
        Put(destination, 0x348, surface.GlossinessFadeEndDistance);
        Put(destination, 0x34C, surface.GlossinessFadeEndRate);
        Put(destination, 0x350, indirect.KeyLight0Scale);
        Put(destination, 0x354, subLights.FirstScale);
        Put(destination, 0x358, subLights.SecondScale);
        Put(destination, 0x35C, subLights.ThirdScale);
        Put(destination, 0x360, indirect.AmbientLightScale);
        Put(destination, 0x364, hemisphereScale);
        Put(destination, 0x368, surface.EmiEmScale);
        Put(destination, 0x36C, indirect.EmiRSMEmScale);
        Put(destination, 0x370, surface.AleEmiScale);
        Put(destination, 0x374, surface.AleAlbScale);
        Put(destination, 0x378, surface.ProjectionMapFade);
        Put(destination, 0x37C, surface.DecalFade);
        Put(destination, 0x380, surface.DecalEmissiveScale);
        Put(destination, 0x384, surface.WindScale);
    }

    private static void Put(Span<byte> destination, int offset, byte value) => destination[offset] = value;
    private static void Put(Span<byte> destination, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], value);
    private static void Put(Span<byte> destination, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value);
    private static void Put(Span<byte> destination, int offset, Vector2 value)
    { Put(destination, offset, value.X); Put(destination, offset + 4, value.Y); }
    private static void Put(Span<byte> destination, int offset, Vector3 value)
    { Put(destination, offset, value.X); Put(destination, offset + 4, value.Y); Put(destination, offset + 8, value.Z); }
    private static void Put(Span<byte> destination, int offset, Vector4 value)
    { Put(destination, offset, value.X); Put(destination, offset + 4, value.Y); Put(destination, offset + 8, value.Z); Put(destination, offset + 12, value.W); }
    private static void Put(Span<byte> destination, int offset, DirectionalUnsignedPair value)
    { Put(destination, offset, value.First); Put(destination, offset + 4, value.Second); }
}
