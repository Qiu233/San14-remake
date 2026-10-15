using System.Numerics;

namespace San14.Data;

public static partial class MainLightConstruction
{
    // These are main-light constructor defaults, independent of a directional declaration's optional fallbacks.
    private static readonly DirectionalMaterialSettings DefaultMaterial = new(
        Diffuse: Vector3.One,
        Transparency: 1f,
        Ambient: Vector3.One,
        Specular: Vector3.One,
        SpecularDecay: 1f,
        Emission: Vector3.One,
        AlphaThreshold: 1f);

    private static readonly DirectionalLightMapsSettings DefaultLightMaps = new(
        DiffuseScaler0: Vector3.Zero,
        DiffuseScaler1: Vector3.One,
        DiffuseScaler2: Vector3.Zero,
        DiffuseScaler3: Vector3.One,
        SpecularScaler0: Vector3.Zero,
        SpecularScaler1: Vector3.One,
        SpecularScaler2: Vector3.Zero,
        ShadowScaler: Vector3.Zero,
        EtcScaleX: 32000f,
        EtcScaleZ: 32000f,
        MaxAmbientOcclusion: 1f,
        DOFade: 1f,
        AmbientDiffuseScaler: 0f);

    private static readonly DirectionalSubsurfaceSettings DefaultSubsurface = new(
        Width: 0.012f,
        Translucency: 0f,
        TransThreshold: 0.0001f);

    private static readonly DirectionalOcclusionSettings DefaultOcclusion = new(
        Scale: 1f,
        SSAOFade: 1f,
        SSAORadius: 1f,
        SSAOMaxRadiusRate: 1f,
        SSAOContrast: 1f,
        SSAOMaxOcclusion: 1f,
        SSAOAngleBias: 1f,
        SSAOStepNum: 1f,
        SSAOEndRadius: 1f,
        SSAOLerpStart: 1f,
        SSAOLerpEnd: 1f,
        AlbedoFade: 0f);

    private static readonly DirectionalIndexedRimSettings DefaultIndexedRim = new(
        ScaleSharp: new(0f, 0f, 0f, 2f),
        Mode: 0,
        Target: 2,
        ExtensionIndex: byte.MaxValue,
        Emission: Vector4.Zero,
        EmissionCurve: Vector4.Zero,
        ReflectionMainAndSub: Vector4.One,
        ReflectionLocalAndAmbient: Vector4.One,
        Transmission: Vector4.One);

    private static readonly DirectionalDirtChannelSettings DefaultDirtChannel = new(
        MaskFade: 0f,
        TextureRepeat: 1f,
        Albedo: Vector4.Zero,
        Reflectance: 0f,
        Glossiness: 0f,
        Occlusion: 0f,
        ReflectanceAlpha: 0f,
        NormalX: 0f,
        NormalY: 0f,
        NormalZ: 0f,
        NormalW: 0f,
        Emission: Vector3.Zero);

    private static readonly DirectionalReflectanceLayerSettings DefaultReflectanceLayer = new(
        FirstLayerIor: 1f,
        FirstLayerGlossiness: 1f,
        FirstLayerAbsorbance: Vector3.One,
        SecondLayerIorReal: Vector3.One,
        SecondLayerIorImaginary: Vector3.One);

    private static readonly DirectionalHatchingShadowSettings DefaultHatching = new(
        RampScale: 0f,
        RampFade: new(-1f, 0f),
        OccScale: 0f,
        OccFade: Vector2.Zero,
        UVRepeat: Vector2.One,
        UVRot: 0f,
        UVVelocity: Vector2.Zero,
        UVUpdateInterval: 0f,
        IndexedRampScale: 0f);

    private static readonly DirectionalAlbedoRetouchSettings DefaultAlbedoRetouch = new(
        FalloffScale: new(5.33333f, 5.33333f, 25.6f, 25.6f),
        FalloffScaledOffset: new(0.833333f, -0.5f, -7.2f, -12f),
        ShadowColorLimit: new(0.5f, 0.5f, 0.5f),
        RimLightColor: Vector3.One,
        SpecularLightColor: Vector3.One,
        SpecularPower: 10f,
        ReflectionFade: 1f,
        ToonElevation: 24.1f,
        ToonAzimuth: -63.4f,
        ToonLightDirRelativeMode: 0,
        RimElevation: 44.2f,
        RimAzimuth: -111.3f,
        RimLightDirRelativeMode: 0,
        SpecularElevation: 0f,
        SpecularAzimuth: 0f,
        SpecularLightDirRelativeMode: 0,
        AlbedoRetouch2PB2HairHighlightRampThreshold: 0f);

    private static readonly DirectionalIndirectLightingSettings DefaultIndirectLighting = new(
        IDLScalerOfLPV: 1f,
        IDLFade: 1f,
        ISLFade: 1f,
        ISLScalerOfAmbientSpecularMap: 1f,
        IDLScalerOfAmbientDiffuseMap: 1f,
        AmbientLightScale: 1f,
        EmiRSMEmScale: 1f,
        KeyLight0Scale: 1f,
        ISLFadeOfRLR: 1f,
        DDLFade: 1f,
        DSLFade: 1f,
        ISLScalerOfAmbientSpecularMapColorRGB: Vector3.One,
        IDLScalerOfAmbientDiffuseMapColorRGB: Vector3.One,
        LocalSpaceSubLightColor2: Vector3.Zero,
        LocalSpaceSubLightColor3: Vector3.Zero,
        LASSpecularScale: 1f,
        LASDiffuseScale: 1f);

    private static readonly DirectionalSurfaceEffectsSettings DefaultSurfaceEffects = new(
        GlossinessFadeStartDistance: 0f,
        GlossinessFadeEndDistance: 0f,
        GlossinessFadeEndRate: 1f,
        EmiEmScale: 1f,
        ProjectionMapFade: 1f,
        DecalFade: 1f,
        NatureSnowFade: 1f,
        ShadowMaskFade: 0f,
        FurZWriteAlphaThreshold: 0f,
        DecalEmissiveScale: 1f,
        CausticsFade: 0f,
        AleEmiScale: 1f,
        AleAlbScale: 1f,
        WindScale: 1f,
        NatureRippleFade: 1f);

    private static readonly DirectionalSubLightsSettings DefaultSubLights = new(
        FirstScale: 1f,
        SecondScale: 1f,
        ThirdScale: 1f);

    private static void WriteDefaultModifiers(byte[] bytes)
    {
        LightModifierEncoding.WriteMaterial(bytes, DefaultMaterial, new(uint.MaxValue, 0));
        LightModifierEncoding.WriteLightMaps(bytes, DefaultLightMaps);
        LightModifierEncoding.WriteSubsurface(bytes, DefaultSubsurface);
        LightModifierEncoding.WriteOcclusion(bytes, DefaultOcclusion);
        LightModifierEncoding.WriteLightingScales(bytes, DefaultIndirectLighting,
            DefaultSurfaceEffects, DefaultSubLights, hemisphereScale: 1);
        for (int index = 0; index < 16; index++)
            LightModifierEncoding.WriteIndexedRim(bytes, index, DefaultIndexedRim);
        for (int index = 0; index < 4; index++)
        {
            LightModifierEncoding.WriteDirtChannel(bytes, index, DefaultDirtChannel);
            LightModifierEncoding.WriteReflectanceLayer(bytes, index, DefaultReflectanceLayer);
        }
        LightModifierEncoding.WriteHatchingShadow(bytes, DefaultHatching);
        LightModifierEncoding.WriteAlbedoRetouch(bytes, DefaultAlbedoRetouch);
        WriteDefaultLightHeader(bytes);
        WriteDefaultEyeAndShadowControls(bytes);
        WriteDefaultRampAndOutlineControls(bytes);
        WriteDefaultEnvironmentControls(bytes);
    }

    private static void WriteDefaultLightHeader(byte[] bytes)
    {
        for (int index = 0; index < 4; index++)
            Write(bytes, 0x80 + index * 4, 1f); // Per-light fade.
        Write(bytes, 0xF0, new Vector4(0, 0, 0, 1)); // Hemisphere orientation.
        Write(bytes, 0x2A0, 32000f); // Accessory-light X scale.
        Write(bytes, 0x2A4, 32000f); // Accessory-light Z scale.
        // Main initializes probe scale only; the probe offset remains retained.
        Write(bytes, 0x2B8, 40f);
    }

    private static void WriteDefaultEyeAndShadowControls(byte[] bytes)
    {
        Write(bytes, 0x280, 1f); // Eye RGB scale.
        Write(bytes, 0x284, 1f); // Eye alpha scale.
        Write(bytes, 0x288, 1f); // Eye local-space RGB fade.
        Write(bytes, 0x28C, 1f); // Eye local-space alpha fade.
        Write(bytes, 0xF00, 1f); // Eye-twinkle shadow fade.
        bytes[0xF1E] = 1; // Shadow-map filter.
        bytes[0xF1F] = 1; // Local-light caster plane.
    }

    private static void WriteDefaultRampAndOutlineControls(byte[] bytes)
    {
        Write(bytes, 0x270, 0.01f); // Ramp rim scale.
        Write(bytes, 0x9BC, 20f); // Indexed-ramp mask scale.
        Write(bytes, 0x9C0, 45f); // Indexed-ramp mask rotation.
        // Alternate ramp entries retain sharpness and white color scaling.
        // Their other fields and the empty authored entries stay zero.
        Write(bytes, 0xA30, 10f);
        Write(bytes, 0xA34, Vector3.One);
        Write(bytes, 0xA68, 10f);
        Write(bytes, 0xA6C, Vector3.One);
        Write(bytes, 0xB2C, Vector3.One); // Outline light-propagation scale.
        Write(bytes, 0xB38, 1f); // Outline screen-space texture scale.
        Write(bytes, 0xB50, 350f); // Outline maximum projection scale.
        bytes[0xB6B] = 1; // Outline normalization.
    }

    private static void WriteDefaultEnvironmentControls(byte[] bytes)
    {
        Write(bytes, 0xB70, DefaultSurfaceEffects.NatureSnowFade);
        Write(bytes, 0xB74, DefaultSurfaceEffects.NatureRippleFade);
        Write(bytes, 0xDE8, Vector3.One); // Environment-map scale.
        // Fixed shader control words have no authored Main-light field.
        Write(bytes, 0xE1C, 1f);
        Write(bytes, 0xE20, 5000f);
        Write(bytes, 0xE24, 0.5f);
        Write(bytes, 0xE28, 1f);
        Write(bytes, 0x334, 1f); // Fixed indirect-light control component.
        Write(bytes, 0x338, 1f); // Fixed indirect-light control component.
        Write(bytes, 0xFC0, 1f); // Initial month/day.
        Write(bytes, 0xFD8, 0.5f); // Initial moon age.
    }
}
