// Authored main-light controls written once into the final interop destination.
namespace San14.Data;

public static partial class MainLightConstruction
{
    private static void CopySourceFields(byte[] bytes, MainLightSettings settings)
    {
        Write(bytes, 0x10, settings.DirectionalLights[0].Color);
        Write(bytes, 0x30, settings.DirectionalLights[1].Color);
        Write(bytes, 0x50, settings.DirectionalLights[2].Color);
        Write(bytes, 0x70, settings.DirectionalLights[3].Color);
        bytes[0xF1C] = settings.Shadow.CasterPlane;
        Copy(bytes, 0x10B0, settings.Extensions.Span);
        Write(bytes, 0xE30, settings.AmbientMapsRotation * BitConverter.UInt32BitsToSingle(0x3C8EFA35u));
        bytes[0xF1D] = settings.Shadow.ReceiveShadowType;
        Write(bytes, 0xEF8, settings.EyeTwinkle.Scale);
        Write(bytes, 0xEFC, settings.EyeTwinkle.ExponentScale);
        Write(bytes, 0x9CC, settings.Ramp.Coefficient);
        Write(bytes, 0x9D0, settings.Ramp.SublightCoefficient);
        Write(bytes, 0x9D4, settings.Ramp.UScale);
        Write(bytes, 0x9D8, settings.Ramp.SublightUScale);
        Write(bytes, 0x9DC, settings.Ramp.UBias);
        Write(bytes, 0x9E0, settings.Ramp.SublightUBias);
        Write(bytes, 0x9E4, settings.Ramp.ShadowFade);
        Write(bytes, 0x9E8, settings.Ramp.OcclusionThresholdForIrShadow);
        Write(bytes, 0x9EC, settings.Ramp.IrShadowFade);
        Write(bytes, 0x9F0, settings.Ramp.LocalLightDiffuser);
        Write(bytes, 0x9F4, settings.Ramp.LocalLightScale);
        Write(bytes, 0x9F8, settings.Ramp.AmbientOcclusionFade);
        Write(bytes, 0x9FC, settings.Ramp.URangeMinimum);
        Write(bytes, 0xA00, settings.Ramp.URangeMaximum);
        Write(bytes, 0xB0C, settings.Outline.IntensityMinimum);
        Write(bytes, 0xB10, settings.Outline.IntensityMaximum);
        Write(bytes, 0xB14, settings.Outline.DepthDecayRatio);
        Write(bytes, 0xB18, settings.Outline.ConstantThicknessRatio);
        Write(bytes, 0xB58, settings.Outline.DepthScale);
        bytes[0xAD4] = settings.Ramp.RimDirectionType;
        Write(bytes, 0xE0, settings.Exposure.AmbientColor);
        Put(bytes, 0xEC, 0x3F800000u);
        Write(bytes, 0xA94, settings.HairHighlight.Threshold);
        Write(bytes, 0xA84, settings.HairHighlight.ReflectionRatio);
        Write(bytes, 0xA88, settings.HairHighlight.ColorScale);
        Write(bytes, 0xA98, settings.HairHighlight.LightElevationRatio);
        Write(bytes, 0xA9C, settings.HairHighlight.LightAzimuthRatio);
        Write(bytes, 0xB3C, settings.Outline.ColorScale);
        Write(bytes, 0x274, settings.Ramp.RimGlobalScale);
        bytes[0xB68] = settings.Outline.CalculationMode;
        Write(bytes, 0xB5C, settings.Outline.NearThickness);
        Write(bytes, 0xB60, settings.Outline.FarThickness);
        Write(bytes, 0xB64, settings.Outline.FadeMinimumThickness);
        Write(bytes, 0xAA0, settings.Edge.Thickness);
        Write(bytes, 0xAA4, settings.Edge.DepthThreshold);
        Write(bytes, 0xAB0, settings.Edge.CameraRimSharpness);
        Write(bytes, 0xAB4, settings.Edge.HighlightColor);
        Write(bytes, 0xAAC, settings.Edge.HighlightRamp);
        Write(bytes, 0xAA8, settings.Edge.ReflectionScale);
        Write(bytes, 0xAC4, settings.Edge.BackThickness);
        Write(bytes, 0xAC8, settings.Edge.BackDepthThreshold);
        Write(bytes, 0xACC, settings.Edge.BackRimScale);
        Write(bytes, 0xAD0, settings.Edge.BackRimSharpness);
    }

    private static void CopyPreparedCelestialLights(byte[] bytes)
    {
        Copy(bytes, 0xF80, bytes.AsSpan(0x0, 16));
        Copy(bytes, 0xF90, bytes.AsSpan(0x20, 16));
        Copy(bytes, 0xFA0, bytes.AsSpan(0x10, 16));
        Copy(bytes, 0xFB0, bytes.AsSpan(0x30, 16));
    }
}
