using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DirectionalUnsignedPair(uint First, uint Second);

public sealed record DirectionalCapacitySettings(
    uint MaximumPrimitives,
    uint GroupMask,
    uint MaximumShadowCasters,
    uint MaximumSubShadowCasters,
    uint MaximumReflectiveShadowCasters);

public sealed record DirectionalShadowSettings(
    byte CasterPlane,
    byte ReceiveShadowType,
    byte ReceiveShadowmapFilter,
    byte CasterPlaneForLocalLight,
    byte CasterPlaneForReflectiveShadowMap,
    float ReceiveShadowFade,
    float ShadowReceiverSlopeScaledBias,
    float ShadowReceiverSlopeScale);

public sealed record DirectionalHemisphereSettings(
    Vector4 WorldQuaternion,
    Vector3 SkyColor,
    Vector3 GroundColor,
    float Scale,
    Vector3 HeightSkyColor,
    Vector3 HeightGroundColor);

public sealed record DirectionalMaterialSettings(
    Vector3 Diffuse,
    float Transparency,
    Vector3 Ambient,
    Vector3 Specular,
    float SpecularDecay,
    Vector3 Emission,
    float AlphaThreshold);

public sealed record DirectionalAccessoryLightsSettings(
    float Light0Scale,
    float Light1Scale,
    float Light2Scale,
    float ScaleX,
    float ScaleZ,
    float Light3Scale,
    float SpeedX,
    float SpeedZ,
    float OffsetX,
    float OffsetZ);

public sealed record DirectionalLightMapsSettings(
    Vector3 DiffuseScaler0,
    Vector3 DiffuseScaler1,
    Vector3 DiffuseScaler2,
    Vector3 DiffuseScaler3,
    Vector3 SpecularScaler0,
    Vector3 SpecularScaler1,
    Vector3 SpecularScaler2,
    Vector3 ShadowScaler,
    float EtcScaleX,
    float EtcScaleZ,
    float MaxAmbientOcclusion,
    float DOFade,
    float AmbientDiffuseScaler);

public sealed record DirectionalRainSettings(
    float EraseStart,
    float EraseEnd,
    float SlopeLimit,
    float Scale,
    float FlowSpeedX,
    float FlowSpeedZ,
    float BlendSpeed,
    Vector3 SpecularScale,
    float MaskMapScaleX,
    float MaskMapScaleZ,
    float MaskMapOffsetX,
    float MaskMapOffsetZ);

public sealed record DirectionalSubsurfaceSettings(
    float Width,
    float Translucency,
    float TransThreshold);

public sealed record DirectionalLightProbeSettings(
    float Scale,
    float OffsetY);

public sealed record DirectionalSubLightsSettings(
    float FirstScale,
    float SecondScale,
    float ThirdScale);

public sealed record DirectionalIndirectLightingSettings(
    float IDLScalerOfLPV,
    float IDLFade,
    float ISLFade,
    float ISLScalerOfAmbientSpecularMap,
    float IDLScalerOfAmbientDiffuseMap,
    float AmbientLightScale,
    float EmiRSMEmScale,
    float KeyLight0Scale,
    float ISLFadeOfRLR,
    float DDLFade,
    float DSLFade,
    Vector3 ISLScalerOfAmbientSpecularMapColorRGB,
    Vector3 IDLScalerOfAmbientDiffuseMapColorRGB,
    Vector3 LocalSpaceSubLightColor2,
    Vector3 LocalSpaceSubLightColor3,
    float LASSpecularScale,
    float LASDiffuseScale);

public sealed record DirectionalOcclusionSettings(
    float Scale,
    float SSAOFade,
    float SSAORadius,
    float SSAOMaxRadiusRate,
    float SSAOContrast,
    float SSAOMaxOcclusion,
    float SSAOAngleBias,
    float SSAOStepNum,
    float SSAOEndRadius,
    float SSAOLerpStart,
    float SSAOLerpEnd,
    float AlbedoFade);

public sealed record DirectionalSurfaceEffectsSettings(
    float GlossinessFadeStartDistance,
    float GlossinessFadeEndDistance,
    float GlossinessFadeEndRate,
    float EmiEmScale,
    float ProjectionMapFade,
    float DecalFade,
    float NatureSnowFade,
    float ShadowMaskFade,
    float FurZWriteAlphaThreshold,
    float DecalEmissiveScale,
    float CausticsFade,
    float AleEmiScale,
    float AleAlbScale,
    float WindScale,
    float NatureRippleFade);

public sealed record DirectionalRimSettings(
    float RmScale,
    Vector3 RmGScale3);

public sealed record DirectionalIndexedRampSettings(
    float MaskFade,
    float MaskScale,
    float FirstLightRampCoefficientAdjuster,
    float ParallelSubLightsRampCoefficientAdjuster,
    float IRDFade,
    float OccThresForIRShadow,
    float IRShadowFade,
    float IRLocalLightFade,
    float IRLocalLightScale,
    float IRAFade,
    float RampCoordRangeMin,
    float RampCoordRangeMax,
    float FirstLightRampCoordinateScaler,
    float ParallelSubLightsRampCoordinateScaler,
    float MaskRotation,
    float MaskUVelocity,
    float MaskVVelocity,
    float SDOccFade,
    byte RimDirectionType,
    float FirstLightRampCoordinateBias,
    float ParallelSubLightsRampCoordinateBias);

public sealed record DirectionalHairHighlightSettings(
    float Elevation,
    float Azimuth,
    float Rotation,
    float ReflectionRatio,
    Vector3 Scaler,
    float Threshold,
    float LightElevationRatio,
    float LightAzimuthRatio);

public sealed record DirectionalEdgeSettings(
    float Thickness,
    float DepthThreshold,
    float CameraRimSharpness,
    Vector4 HighlightColor,
    float HighlightRamp,
    float ReflectScale,
    float BackThickness,
    float BackDepthThreshold,
    float BackRimScale,
    float BackRimSharpness);

public sealed record DirectionalOutlineSettings(
    float IntensityMin,
    float IntensityMax,
    float ZDecayRatio,
    float ThicknessConstantRatio,
    byte AlphaTest,
    byte Normalization,
    byte Fog,
    float LightEffectFade,
    Vector3 AmbientLightEffectScaler,
    Vector3 LPVEffectScaler,
    byte CalcMode,
    byte ColorMode,
    float ScreenSpaceTextureScaler,
    Vector3 ColorScaler,
    float ColorSaturationFade,
    float ThicknessScaler,
    float ThicknessProjScaleMax,
    float PatternMapFade,
    float ZScale,
    byte AutoHDRScaleExposure,
    float ThicknessNear,
    float ThicknessFar,
    float FadeMinThickness,
    byte CameraReverse);

public sealed record DirectionalWeatheringSettings(
    float AlbedoFade,
    float ReflectanceFade,
    float GlossinessFade,
    float WetDebug,
    byte WetDebugType,
    float WetScaler,
    int TextureBlendIdDebug,
    float TextureBlendFadeDebug);

public sealed record DirectionalAlbedoRetouchSettings(
    Vector4 FalloffScale,
    Vector4 FalloffScaledOffset,
    Vector3 ShadowColorLimit,
    Vector3 RimLightColor,
    Vector3 SpecularLightColor,
    float SpecularPower,
    float ReflectionFade,
    float ToonElevation,
    float ToonAzimuth,
    byte ToonLightDirRelativeMode,
    float RimElevation,
    float RimAzimuth,
    byte RimLightDirRelativeMode,
    float SpecularElevation,
    float SpecularAzimuth,
    byte SpecularLightDirRelativeMode,
    float AlbedoRetouch2PB2HairHighlightRampThreshold);

public sealed record DirectionalShadowOutlineSettings(
    float Width,
    float Threshold,
    float Fade,
    byte SamplingType);

public sealed record DirectionalIllustrationSettings(
    float AmbientDiffuseUpScale,
    float AmbientLightDownScale,
    Vector3 FogColorRGB,
    float FogUpScale);

public sealed record DirectionalEyeSettings(
    float EyetEyetRGBScale,
    float EyetEyetAlphaScale,
    float EyeTwinkleGlobalScale,
    float EyeTwinkleGlobalExponentScale,
    float EyetEyetLSERGBFade,
    float EyetEyetLSEAlphaFade,
    float EyeTwinkleGlobalShadowFade);

public sealed record DirectionalCelestialSettings(
    float MonthDay,
    float DateTime,
    float Longitude,
    float Latitude,
    float Altitude,
    float NorthDir,
    float MoonAge,
    uint CelestialOption);

public sealed record DirectionalHatchingShadowSettings(
    float RampScale,
    Vector2 RampFade,
    float OccScale,
    Vector2 OccFade,
    Vector2 UVRepeat,
    float UVRot,
    Vector2 UVVelocity,
    float UVUpdateInterval,
    float IndexedRampScale);

public sealed record DirectionalResourcesSettings(
    uint Fog,
    uint Provider,
    uint LightProbe,
    uint AmbientDiffuseMap,
    uint IndexedRamp,
    uint HairHighlight,
    uint IndexedRampAdditionalMask,
    uint OutlineColor,
    uint DirtArray,
    uint AlbedoRetouchEnvironmentMap,
    uint IllustrationGlobalMask,
    uint WeatheringArray,
    uint WeatheringSecondaryArray,
    uint EyeTwinkle,
    uint DarkMapEmission,
    uint AlbedoRetouch2GlobalMask);

public sealed record DirectionalLightRowSettings(
    Vector4 WorldQuaternion,
    Vector3 Color,
    float Fade);

public sealed record DirectionalIndexedRimSettings(
    Vector4 ScaleSharp,
    byte Mode,
    byte Target,
    byte ExtensionIndex,
    Vector4 Emission,
    Vector4 EmissionCurve,
    Vector4 ReflectionMainAndSub,
    Vector4 ReflectionLocalAndAmbient,
    Vector4 Transmission);

public sealed record DirectionalDirtChannelSettings(
    float MaskFade,
    float TextureRepeat,
    Vector4 Albedo,
    float Reflectance,
    float Glossiness,
    float Occlusion,
    float ReflectanceAlpha,
    float NormalX,
    float NormalY,
    float NormalZ,
    float NormalW,
    Vector3 Emission);

public sealed record DirectionalReflectanceLayerSettings(
    float FirstLayerIor,
    float FirstLayerGlossiness,
    Vector3 FirstLayerAbsorbance,
    Vector3 SecondLayerIorReal,
    Vector3 SecondLayerIorImaginary);

public sealed record DirectionalWeatheringMaskSettings(
    float Fade,
    float MinimumFade);

public sealed record DirectionalWeatheringTextureSettings(
    uint Array,
    uint SecondaryArray);

public sealed record DirectionalOutlineColorSettings(
    Vector3 Color);

public sealed record DirectionalRampRimSettings(
    float Elevation,
    float Azimuth,
    float Scale,
    float Sharpness,
    Vector3 ColorScale);

/// <summary>Authored directional-light controls captured once per declaration generation.
/// Defaults and optional shape fallback belong to import; packed LightData belongs to interop.</summary>
public sealed class DirectionalLightSettings
{
    public Vector3 WorldPosition { get; }
    public Vector3 AmbientColor { get; }
    public Vector3 EnvironmentMapScale { get; }
    public byte CurrentFieldWriterFlags { get; }
    public DirectionalUnsignedPair EnvironmentMaps { get; }
    public DirectionalUnsignedPair MaterialFlags { get; }
    public ReadOnlyMemory<byte> Extensions { get; }
    public IReadOnlyList<uint> RainMaps { get; }
    public DirectionalCapacitySettings Capacity { get; }
    public DirectionalShadowSettings Shadow { get; }
    public DirectionalHemisphereSettings Hemisphere { get; }
    public DirectionalMaterialSettings Material { get; }
    public DirectionalAccessoryLightsSettings AccessoryLights { get; }
    public DirectionalLightMapsSettings LightMaps { get; }
    public DirectionalRainSettings Rain { get; }
    public DirectionalSubsurfaceSettings Subsurface { get; }
    public DirectionalLightProbeSettings LightProbe { get; }
    public DirectionalSubLightsSettings SubLights { get; }
    public DirectionalIndirectLightingSettings IndirectLighting { get; }
    public DirectionalOcclusionSettings Occlusion { get; }
    public DirectionalSurfaceEffectsSettings SurfaceEffects { get; }
    public DirectionalRimSettings Rim { get; }
    public DirectionalIndexedRampSettings IndexedRamp { get; }
    public DirectionalHairHighlightSettings HairHighlight { get; }
    public DirectionalEdgeSettings Edge { get; }
    public DirectionalOutlineSettings Outline { get; }
    public DirectionalWeatheringSettings Weathering { get; }
    public DirectionalAlbedoRetouchSettings AlbedoRetouch { get; }
    public DirectionalShadowOutlineSettings ShadowOutline { get; }
    public DirectionalIllustrationSettings Illustration { get; }
    public DirectionalEyeSettings Eye { get; }
    public DirectionalCelestialSettings Celestial { get; }
    public DirectionalHatchingShadowSettings HatchingShadow { get; }
    public DirectionalResourcesSettings Resources { get; }
    public IReadOnlyList<DirectionalLightRowSettings> Lights { get; }
    public IReadOnlyList<DirectionalIndexedRimSettings> Rims { get; }
    public IReadOnlyList<DirectionalDirtChannelSettings> DirtChannels { get; }
    public IReadOnlyList<DirectionalReflectanceLayerSettings> ReflectanceLayers { get; }
    public IReadOnlyList<DirectionalWeatheringMaskSettings> WeatheringMasks { get; }
    public IReadOnlyList<DirectionalWeatheringTextureSettings> AdditionalWeatheringTextures { get; }
    public IReadOnlyList<DirectionalOutlineColorSettings> OutlineColors { get; }
    public IReadOnlyList<DirectionalRampRimSettings> RampRims { get; }

    public uint MaximumPrimitiveCount => Capacity.MaximumPrimitives;
    public uint SourceGroupMask => Capacity.GroupMask;
    public byte Classification => Shadow.CasterPlaneForReflectiveShadowMap;
    public uint ProviderObjectKey => Resources.Provider;

    public DirectionalLightDeclaration ToDeclaration(uint objectId) => new(objectId, SourceGroupMask,
        Array.AsReadOnly(Lights.Select(value => value.WorldQuaternion).ToArray()),
        Array.AsReadOnly(Lights.Select(value => value.Color).ToArray()));

    internal static DirectionalLightSettings Import(DokObject declaration) => new(new OptionalFields(declaration));

    private DirectionalLightSettings(OptionalFields reader)
    {
        WorldPosition = reader.Three(0x27D889E5u, new Vector3(0.0f, 0.0f, 0.0f));
        CurrentFieldWriterFlags = reader.Byte(0x05D92ACCu, 0, 1);
        AmbientColor = reader.Three(0x95E12FCBu, new Vector3(0.0f, 0.0f, 0.0f));
        EnvironmentMaps = reader.UnsignedPair(0xB1205167u, 0u, 0u);
        MaterialFlags = reader.UnsignedPair(0x58264100u, 4294967295u, 0u);
        Extensions = reader.Bytes(0x4439EF34u, 8);
        RainMaps = reader.UnsignedThree(0x72774CA0u);
        EnvironmentMapScale = reader.Three(0x3E554A05u, new Vector3(1.0f, 1.0f, 1.0f));
        Capacity = new(
            reader.Unsigned(0xDB26951Du, 8192u), reader.Unsigned(0xC0C5A5B5u, 0u), reader.Unsigned(0x6F486116u, 1000u),
            reader.Unsigned(0xAEBE6302u, 100u), reader.Unsigned(0x1439399Bu, 5000u));
        Shadow = new(
            reader.Byte(0xD7E3FF50u, 0, 1), reader.Byte(0x2DAB8C43u, 0, 1), reader.Byte(0xE6126451u, 0, 1),
            reader.Byte(0xA8889604u, 0, 1), reader.Byte(0x8A44928Du, 0, 1), reader.Float(0x560887C1u, 1.0f),
            reader.Float(0x3B1F64B1u, 0.0f), reader.Float(0x00C1E78Eu, 0.0f));
        Hemisphere = new(
            reader.Four(0x028E5549u, new Vector4(0.0f, 0.0f, 0.0f, 1.0f)), reader.Three(0xDA9AB52Bu, new Vector3(0.0f, 0.0f, 0.0f)), reader.Three(0xD74A096Du, new Vector3(0.0f, 0.0f, 0.0f)),
            reader.Float(0xA5F933FAu, 0.0f), reader.Three(0xB0CC8850u, new Vector3(0.0f, 0.0f, 0.0f)), reader.Three(0xFA95C232u, new Vector3(0.0f, 0.0f, 0.0f)));
        Material = new(
            reader.Three(0xB01A6CDFu, new Vector3(1.0f, 1.0f, 1.0f)), reader.Float(0xBCC18265u, 1.0f), reader.Three(0x9004EA95u, new Vector3(1.0f, 1.0f, 1.0f)),
            reader.Three(0xE04F9B9Au, new Vector3(1.0f, 1.0f, 1.0f)), reader.Float(0xEB70E034u, 1.0f), reader.Three(0x216C0856u, new Vector3(1.0f, 1.0f, 1.0f)),
            reader.Float(0x216C84F0u, 1.0f));
        AccessoryLights = new(
            reader.Float(0x996E1E0Bu, 0.0f), reader.Float(0x9B47712Au, 0.0f), reader.Float(0x9D20C449u, 0.0f),
            reader.Float(0xA2644CADu, 32000.0f), reader.Float(0xA616F2EBu, 32000.0f), reader.Float(0x9EFA1768u, 0.0f),
            reader.Float(0x37B9F6AAu, 0.0f), reader.Float(0x3B6C9CE8u, 0.0f), reader.Float(0x423766A0u, 0.0f),
            reader.Float(0xB4D98822u, 0.0f));
        LightMaps = new(
            reader.Three(0x2EAA596Cu, new Vector3(0.0f, 0.0f, 0.0f)), reader.Three(0x78DBF2ADu, new Vector3(1.0f, 1.0f, 1.0f)), reader.Three(0xC30D8BEEu, new Vector3(0.0f, 0.0f, 0.0f)),
            reader.Three(0x0D3F252Fu, new Vector3(1.0f, 1.0f, 1.0f)), reader.Three(0xCA039C01u, new Vector3(0.0f, 0.0f, 0.0f)), reader.Three(0xC6052AE0u, new Vector3(1.0f, 1.0f, 1.0f)),
            reader.Three(0xC206B9BFu, new Vector3(0.0f, 0.0f, 0.0f)), reader.Three(0xFBD8684Eu, new Vector3(0.0f, 0.0f, 0.0f)), reader.Float(0x80953024u, 32000.0f),
            reader.Float(0x09B8F462u, 32000.0f), reader.Float(0xE110502Du, 1.0f), reader.Float(0xEDE00FEDu, 1.0f),
            reader.Float(0xEBA9F018u, 0.0f));
        Rain = new(
            reader.Float(0xD107D830u, 0.0f), reader.Float(0xFDB556A9u, 5000.0f), reader.Float(0x737EADA4u, 0.5f),
            reader.Float(0x5314E64Au, 0.01f), reader.Float(0x5DFA8CD3u, 0.04f), reader.Float(0x11B16155u, 0.04f),
            reader.Float(0xD9B4A58Au, 1.0f), reader.Three(0xA9DD24ADu, new Vector3(5.0f, 5.0f, 5.0f)), reader.Float(0xC230D096u, 10.0f),
            reader.Float(0x4B5494D4u, 10.0f), reader.Float(0xCC8A8D89u, 32000.0f), reader.Float(0x67DF510Bu, 32000.0f));
        Subsurface = new(
            reader.Float(0xAFE76568u, 0.012f), reader.Float(0xAA0E4A51u, 0.0f), reader.Float(0xD288A15Fu, 0.0001f));
        LightProbe = new(
            reader.Float(0xB2F674C5u, 1.0f), reader.Float(0x1D204E21u, 40.0f));
        SubLights = new(
            reader.Float(0x8157802Fu, 1.0f), reader.Float(0x75A0F14Eu, 1.0f), reader.Float(0x69EA626Du, 1.0f));
        IndirectLighting = new(
            reader.Float(0x6ADA34C2u, 1.0f), reader.Float(0x4F49EF33u, 1.0f), reader.Float(0x4F4A2782u, 1.0f),
            reader.Float(0xE3EB705Eu, 1.0f), reader.Float(0xAAD35562u, 1.0f), reader.Float(0xD982DF94u, 1.0f),
            reader.Float(0xE70BE82Eu, 0.0f), reader.Float(0x8D184691u, 1.0f), reader.Float(0xC5A1EA83u, 1.0f),
            reader.Float(0x4F49EE98u, 1.0f), reader.Float(0x4F4A26E7u, 1.0f), reader.Three(0x952A6114u, new Vector3(1.0f, 1.0f, 1.0f)),
            reader.Three(0x663875ECu, new Vector3(1.0f, 1.0f, 1.0f)), reader.Three(0x716D5320u, new Vector3(0.0f, 0.0f, 0.0f)), reader.Three(0xF59DA021u, new Vector3(0.0f, 0.0f, 0.0f)),
            reader.Float(0x2EB832A9u, 0.0f), reader.Float(0xA46F421Au, 0.0f));
        Occlusion = new(
            reader.Float(0xE6136468u, 1.0f), reader.Float(0xC136E4BCu, 1.0f), reader.Float(0x10641492u, 1.0f),
            reader.Float(0x0442EC6Au, 1.0f), reader.Float(0x390CC1C2u, 1.0f), reader.Float(0x915AD809u, 0.0f),
            reader.Float(0x7FFCD1D4u, 0.0f), reader.Float(0x6946B326u, 0.0f), reader.Float(0xAEE9F013u, 1.0f),
            reader.Float(0xE5D49275u, 1.0f), reader.Float(0x482B4C1Cu, 1.0f), reader.Float(0x6427BC47u, 0.0f));
        SurfaceEffects = new(
            reader.Float(0x35D24BBBu, 0.0f), reader.Float(0x243A17A2u, 0.0f), reader.Float(0x125DBB57u, 1.0f),
            reader.Float(0xA5AAC51Cu, 1.0f), reader.Float(0x8AEC76D7u, 1.0f), reader.Float(0xDCCC00F7u, 1.0f),
            reader.Float(0x56018586u, 1.0f), reader.Float(0x7F66FA31u, 0.0f), reader.Float(0x32B9C54Eu, 0.0f),
            reader.Float(0xDEFA3BA6u, 1.0f), reader.Float(0x9790B39Du, 0.0f), reader.Float(0x210824EEu, 1.0f),
            reader.Float(0x0E264D18u, 1.0f), reader.Float(0x528A5FBEu, 1.0f), reader.Float(0xD9D15973u, 0.0f));
        Rim = new(
            reader.Float(0xBBDB893Au, 0.01f), reader.Three(0x6A81F3D4u, new Vector3(1.0f, 1.0f, 1.0f)));
        IndexedRamp = new(
            reader.Float(0xFCE22644u, 0.0f), reader.Float(0x47AB397Eu, 20.0f), reader.Float(0x7EE891A3u, 0.0f),
            reader.Float(0x9A64BAC5u, 0.0f), reader.Float(0xF72CB905u, 1.0f), reader.Float(0xA212C787u, 1.0f),
            reader.Float(0x49BAFD81u, 1.0f), reader.Float(0x068DD30Cu, 0.0f), reader.Float(0x24150C46u, 1.0f),
            reader.Float(0x52771F68u, 1.0f), reader.Float(0xD92B8B44u, 0.0f), reader.Float(0x956B1B72u, 1.0f),
            reader.Float(0xEB985294u, 1.0f), reader.Float(0x077838B4u, 0.0f), reader.Float(0x2F221D06u, 45.0f),
            reader.Float(0x1255B656u, 0.0f), reader.Float(0xD6E79875u, 0.0f), reader.Float(0x8380F5A2u, 0.0f),
            reader.Byte(0xC76FBD1Fu, 0, 1), reader.Float(0x01719D65u, 1.0f), reader.Float(0x414D50C3u, 1.0f));
        HairHighlight = new(
            reader.Float(0x81BD1707u, 0.0f), reader.Float(0xD9843F56u, 0.0f), reader.Float(0xD2D2B3ACu, 0.0f),
            reader.Float(0x88E6D68Au, 0.0f), reader.Three(0x74B28F82u, new Vector3(0.0f, 0.0f, 0.0f)), reader.Float(0x7522DE15u, 0.0f),
            reader.Float(0x914E514Eu, 1.0f), reader.Float(0x9AB3211Fu, 1.0f));
        Edge = new(
            reader.Float(0x0F73F325u, 0.0f), reader.Float(0xB4032341u, 1.0f), reader.Float(0x71903129u, 1.0f),
            reader.Four(0xE86D4A68u, new Vector4(1.0f, 1.0f, 1.0f, 0.0f)), reader.Float(0xF31241B3u, 0.0f), reader.Float(0x3D6C5BA6u, 0.0f),
            reader.Float(0xDF5156ECu, 0.0f), reader.Float(0x4A531908u, 1.0f), reader.Float(0xD7580FB4u, 0.0f),
            reader.Float(0x8B8A92ABu, 1.0f));
        Outline = new(
            reader.Float(0x567F8C3Fu, 0.0f), reader.Float(0xF57EDDEDu, 0.0f), reader.Float(0xE74D8D09u, 0.0f),
            reader.Float(0xDC4A0E2Bu, 0.0f), reader.Byte(0x3A2A830Eu, 0, 1), reader.Byte(0xF73E3F9Bu, 0, 1),
            reader.Byte(0xCEABBBBCu, 0, 1), reader.Float(0x47113921u, 0.0f), reader.Three(0x40653B87u, new Vector3(0.0f, 0.0f, 0.0f)),
            reader.Three(0x9B7B5EA9u, new Vector3(1.0f, 1.0f, 1.0f)), reader.Byte(0x5CDA1BA6u, 0, 1), reader.Byte(0x5C8D7EA4u, 1, 1),
            reader.Float(0xFAB30F55u, 1.0f), reader.Three(0x24D473E9u, new Vector3(1.0f, 1.0f, 1.0f)), reader.Float(0x01F9CF2Fu, 0.0f),
            reader.Float(0x983D847Au, 0.0f), reader.Float(0xF09AA849u, 350.0f), reader.Float(0xFCA82A76u, 0.0f),
            reader.Float(0xFE80308Eu, 0.0f), reader.Byte(0x4B9B4B94u, 0, 1), reader.Float(0x8B75BEBAu, 0.0f),
            reader.Float(0xF5DF0F9Bu, 0.0f), reader.Float(0x826C4960u, 0.0f), reader.Byte(0x2649C5BBu, 0, 1));
        Weathering = new(
            reader.Float(0x2E7D08DAu, 0.0f), reader.Float(0x84156E77u, 0.0f), reader.Float(0xC7668D7Du, 0.0f),
            reader.Float(0x4520C762u, 0.0f), reader.Byte(0xDED6F548u, 0, 1), reader.Float(0xA8A3603Du, 0.0f),
            reader.Integer(0x101A0171u, 0), reader.Float(0x3D45A430u, 0.0f));
        AlbedoRetouch = new(
            reader.Four(0xAE1E2F02u, new Vector4(5.33333f, 5.33333f, 25.6f, 25.6f)), reader.Four(0x9F277ACBu, new Vector4(0.833333f, -0.5f, -7.2f, -12.0f)), reader.Three(0xACCD4B44u, new Vector3(0.5f, 0.5f, 0.5f)),
            reader.Three(0x46799F69u, new Vector3(1.0f, 1.0f, 1.0f)), reader.Three(0x990920BCu, new Vector3(1.0f, 1.0f, 1.0f)), reader.Float(0x2E37FDEAu, 10.0f),
            reader.Float(0x2AD2EAB3u, 1.0f), reader.Float(0xC8BA2029u, 24.1f), reader.Float(0xB33722BAu, -63.4f),
            reader.Byte(0x9A58ABECu, 0, 1), reader.Float(0xD0768B13u, 44.2f), reader.Float(0x95F67262u, -111.3f),
            reader.Byte(0x08C5FAF0u, 0, 1), reader.Float(0x117EEF52u, 0.0f), reader.Float(0x99A64163u, 0.0f),
            reader.Byte(0x959BE195u, 0, 1), reader.Float(0xFC010718u, 0.0f));
        ShadowOutline = new(
            reader.Float(0xCB250F37u, 0.0f), reader.Float(0xAC608EF2u, 0.0f), reader.Float(0x246B9279u, 0.0f),
            reader.Byte(0x7A1C38FEu, 0, 1));
        Illustration = new(
            reader.Float(0xEC179996u, 0.0f), reader.Float(0x80597C7Bu, 0.0f), reader.Three(0x1C1B8839u, new Vector3(0.0f, 0.0f, 0.0f)),
            reader.Float(0x93F16EC0u, 0.0f));
        Eye = new(
            reader.Float(0x2AC69744u, 1.0f), reader.Float(0xFBC9D815u, 1.0f), reader.Float(0xE1BCBA93u, 1.0f),
            reader.Float(0x84E351E4u, 1.0f), reader.Float(0x7CA93A36u, 0.0f), reader.Float(0xFE6E4545u, 0.0f),
            reader.Float(0x7D117C8Du, 1.0f));
        Celestial = new(
            reader.Float(0x35E0871Cu, 1.0f), reader.Float(0x016ABD7Bu, 0.0f), reader.Float(0x76FD51D1u, 0.0f),
            reader.Float(0x0C1E638Cu, 0.0f), reader.Float(0x0C1E8B82u, 8.0f), reader.Float(0x670CB6A8u, 0.0f),
            reader.Float(0xB9A5C422u, 0.5f), reader.Unsigned(0x8509C9D3u, 0u));
        HatchingShadow = new(
            reader.Float(0xCD33DB3Bu, 0.0f), reader.Two(0xEFDF70B5u, new Vector2(-1.0f, 0.0f)), reader.Float(0xFFE91A48u, 0.0f),
            reader.Two(0x4317ED8Eu, new Vector2(0.0f, 0.0f)), reader.Two(0xDB5D92E7u, new Vector2(1.0f, 1.0f)), reader.Float(0xB9D15939u, 0.0f),
            reader.Two(0xA6C9CB25u, new Vector2(0.0f, 0.0f)), reader.Float(0xD2BA5E74u, 0.0f), reader.Float(0x91D918DCu, 0.0f));
        Resources = new(
            reader.Unsigned(0x5E8410AAu, 0u), reader.Unsigned(0xA7DFA945u, 0u), reader.Unsigned(0xD6D7F8D2u, 0u),
            reader.Unsigned(0x183E0816u, 0u), reader.Unsigned(0x556972D8u, 0u), reader.Unsigned(0x28C86C17u, 0u),
            reader.Unsigned(0x31FA3265u, 0u), reader.Unsigned(0x2FCAA686u, 0u), reader.Unsigned(0x4C403289u, 0u),
            reader.Unsigned(0xB53291BBu, 0u), reader.Unsigned(0x8E299EA4u, 0u), reader.Unsigned(0x89351110u, 0u),
            reader.Unsigned(0x61FC02F4u, 0u), reader.Unsigned(0x05288AA2u, 0u), reader.Unsigned(0x0B97F17Eu, 0u),
            reader.Unsigned(0x8C0825F3u, 0u));
        Lights = Array.AsReadOnly(new DirectionalLightRowSettings[]
        {
            new(reader.Four(0xF52EA910u, new Vector4(0.0f, 0.0f, 0.0f, 1.0f)), reader.Three(0x754E8B8Du, new Vector3(1.0f, 1.0f, 1.0f)), reader.Float(0x05BE2F9Cu, 1.0f)),
            new(reader.Four(0xB65C0542u, new Vector4(0.0f, 0.0f, 0.0f, 1.0f)), reader.Three(0xF600D81Bu, new Vector3(0.0f, 0.0f, 0.0f)), reader.Float(0x5B107EAAu, 1.0f)),
            new(reader.Four(0x0705E343u, new Vector4(0.0f, 0.0f, 0.0f, 1.0f)), reader.Three(0xFDB27D7Au, new Vector3(0.0f, 0.0f, 0.0f)), reader.Float(0x5CC55749u, 1.0f)),
            new(reader.Four(0x57AFC144u, new Vector4(0.0f, 0.0f, 0.0f, 1.0f)), reader.Three(0x056422D9u, new Vector3(0.0f, 0.0f, 0.0f)), reader.Float(0x5E7A2FE8u, 1.0f)),
        });
        Rims = Array.AsReadOnly(new DirectionalIndexedRimSettings[]
        {
            new(
                reader.Four(0x6F505F83u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0xE11E9440u, 0, 1),
                reader.Byte(0x59A21FD2u, 0, 1),
                reader.Byte(0x9A960DB2u, 0, 0),
                reader.Four(0xAA47A5FBu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xBBD74026u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xF5D5FE80u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xA9702012u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x3B0ABA5Bu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x3CFAC144u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0xCFA19AC1u, 0, 1),
                reader.Byte(0xB37D8A13u, 0, 1),
                reader.Byte(0xEB3FEBB3u, 0, 0),
                reader.Four(0xFAF183FCu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x425CFAC5u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xF7AF519Fu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xBE58E853u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x1C9C983Au, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x0AA52305u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0xBE24A142u, 0, 1),
                reader.Byte(0x0D58F454u, 0, 1),
                reader.Byte(0x3BE9C9B4u, 0, 0),
                reader.Four(0x4B9B61FDu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xC8E2B564u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xF988A4BEu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xD341B094u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xFE2E7619u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0xD84F84C6u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0xACA7A7C3u, 0, 1),
                reader.Byte(0x67345E95u, 0, 1),
                reader.Byte(0x8C93A7B5u, 0, 0),
                reader.Four(0x9C453FFEu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x4F687003u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xFB61F7DDu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xE82A78D5u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xDFC053F8u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0xA5F9E687u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x9B2AAE44u, 0, 1),
                reader.Byte(0xC10FC8D6u, 0, 1),
                reader.Byte(0xDD3D85B6u, 0, 0),
                reader.Four(0xECEF1DFFu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xD5EE2AA2u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xFD3B4AFCu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xFD134116u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xC15231D7u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x73A44848u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x89ADB4C5u, 0, 1),
                reader.Byte(0x1AEB3317u, 0, 1),
                reader.Byte(0x2DE763B7u, 0, 0),
                reader.Four(0x3D98FC00u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x5C73E541u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xFF149E1Bu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x11FC0957u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xA2E40FB6u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x414EAA09u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x7830BB46u, 0, 1),
                reader.Byte(0x74C69D58u, 0, 1),
                reader.Byte(0x7E9141B8u, 0, 0),
                reader.Four(0x8E42DA01u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xE2F99FE0u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x00EDF13Au, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x26E4D198u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x8475ED95u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x0EF90BCAu, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x66B3C1C7u, 0, 1),
                reader.Byte(0xCEA20799u, 0, 1),
                reader.Byte(0xCF3B1FB9u, 0, 0),
                reader.Four(0xDEECB802u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x697F5A7Fu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x02C74459u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x3BCD99D9u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x6607CB74u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0xDCA36D8Bu, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x5536C848u, 0, 1),
                reader.Byte(0x287D71DAu, 0, 1),
                reader.Byte(0x1FE4FDBAu, 0, 0),
                reader.Four(0x2F969603u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xF005151Eu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x04A09778u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x50B6621Au, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x4799A953u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0xAA4DCF4Cu, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x43B9CEC9u, 0, 1),
                reader.Byte(0x8258DC1Bu, 0, 1),
                reader.Byte(0x708EDBBBu, 0, 0),
                reader.Four(0x80407404u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x768ACFBDu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x0679EA97u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x659F2A5Bu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x292B8732u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0xAB52F314u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x29376891u, 0, 1),
                reader.Byte(0xFED723E3u, 0, 1),
                reader.Byte(0xC69A5183u, 0, 0),
                reader.Four(0xD64BE9CCu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x2BA9B6F5u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xB6E275CFu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x4764E223u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x3C76386Au, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x92F4C973u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x0B153230u, 0, 1),
                reader.Byte(0xE06901C2u, 0, 1),
                reader.Byte(0x8B2C33A2u, 0, 0),
                reader.Four(0x9ADDCBEBu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x75DB5036u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xF0338690u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xCF952202u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x8D20166Bu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x7A969FD2u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0xECF2FBCFu, 0, 1),
                reader.Byte(0xC1FADFA1u, 0, 1),
                reader.Byte(0x4FBE15C1u, 0, 0),
                reader.Four(0x5F6FAE0Au, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xC00CE977u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x29849751u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x57C561E1u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xDDC9F46Cu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x62387631u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0xCED0C56Eu, 0, 1),
                reader.Byte(0xA38CBD80u, 0, 1),
                reader.Byte(0x144FF7E0u, 0, 0),
                reader.Four(0x24019029u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x0A3E82B8u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x62D5A812u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xDFF5A1C0u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x2E73D26Du, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x49DA4C90u, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0xB0AE8F0Du, 0, 1),
                reader.Byte(0x851E9B5Fu, 0, 1),
                reader.Byte(0xD8E1D9FFu, 0, 0),
                reader.Four(0xE8937248u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x54701BF9u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x9C26B8D3u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x6825E19Fu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x7F1DB06Eu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
            new(
                reader.Four(0x317C22EFu, new Vector4(1.0f, 1.0f, 1.0f, 2.0f)),
                reader.Byte(0x928C58ACu, 0, 1),
                reader.Byte(0x66B0793Eu, 0, 1),
                reader.Byte(0x9D73BC1Eu, 0, 0),
                reader.Four(0xAD255467u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0x9EA1B53Au, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xD577C994u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xF056217Eu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Four(0xCFC78E6Fu, new Vector4(0.0f, 0.0f, 0.0f, 0.0f))),
        });
        DirtChannels = Array.AsReadOnly(new DirectionalDirtChannelSettings[]
        {
            new(
                reader.Float(0xE0481EE1u, 0.0f),
                reader.Float(0x38815A4Cu, 1.0f),
                reader.Four(0x92EB8FE4u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Float(0x59AD05F3u, 0.0f),
                reader.Float(0x8F785F5Au, 0.0f),
                reader.Float(0xE160D88Au, 0.0f),
                reader.Float(0x3A3E80E4u, 0.0f),
                reader.Float(0x20097BC2u, 0.0f),
                reader.Float(0x21E2CEE1u, 0.0f),
                reader.Float(0x23BC2200u, 0.0f),
                reader.Float(0x1E3028A3u, 0.0f),
                reader.Three(0x55059ECEu, new Vector3(0.0f, 0.0f, 0.0f))),
            new(
                reader.Float(0xD8DCBF0Cu, 0.0f),
                reader.Float(0x3115FA77u, 1.0f),
                reader.Four(0x1B9F05D9u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Float(0xE2607BE8u, 0.0f),
                reader.Float(0x182BD54Fu, 0.0f),
                reader.Float(0x6A144E7Fu, 0.0f),
                reader.Float(0xC2F1F6D9u, 0.0f),
                reader.Float(0xA8BCF1B7u, 0.0f),
                reader.Float(0xAA9644D6u, 0.0f),
                reader.Float(0xAC6F97F5u, 0.0f),
                reader.Float(0xA6E39E98u, 0.0f),
                reader.Three(0xDDB914C3u, new Vector3(0.0f, 0.0f, 0.0f))),
            new(
                reader.Float(0xD57D64F1u, 0.0f),
                reader.Float(0x2DB6A05Cu, 1.0f),
                reader.Four(0x884DAFD4u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Float(0x4F0F25E3u, 0.0f),
                reader.Float(0x84DA7F4Au, 0.0f),
                reader.Float(0xD6C2F87Au, 0.0f),
                reader.Float(0x2FA0A0D4u, 0.0f),
                reader.Float(0x156B9BB2u, 0.0f),
                reader.Float(0x1744EED1u, 0.0f),
                reader.Float(0x191E41F0u, 0.0f),
                reader.Float(0x13924893u, 0.0f),
                reader.Three(0x4A67BEBEu, new Vector3(0.0f, 0.0f, 0.0f))),
            new(
                reader.Float(0xD4D0B952u, 0.0f),
                reader.Float(0x2D09F4BDu, 1.0f),
                reader.Four(0x37A3D1D3u, new Vector4(0.0f, 0.0f, 0.0f, 0.0f)),
                reader.Float(0xFE6547E2u, 0.0f),
                reader.Float(0x3430A149u, 0.0f),
                reader.Float(0x86191A79u, 0.0f),
                reader.Float(0xDEF6C2D3u, 0.0f),
                reader.Float(0xC4C1BDB1u, 0.0f),
                reader.Float(0xC69B10D0u, 0.0f),
                reader.Float(0xC87463EFu, 0.0f),
                reader.Float(0xC2E86A92u, 0.0f),
                reader.Three(0xF9BDE0BDu, new Vector3(0.0f, 0.0f, 0.0f))),
        });
        ReflectanceLayers = Array.AsReadOnly(new DirectionalReflectanceLayerSettings[]
        {
            new(
                reader.Float(0xD7251CCBu, 1.0f),
                reader.Float(0xE1DB7E7Du, 1.0f),
                reader.Three(0xE8B074B7u, new Vector3(1.0f, 1.0f, 1.0f)),
                reader.Three(0x7F3A1ACCu, new Vector3(1.0f, 1.0f, 1.0f)),
                reader.Three(0x5A899175u, new Vector3(1.0f, 1.0f, 1.0f))),
            new(
                reader.Float(0x5B5569CCu, 1.0f),
                reader.Float(0x6A0BBE5Cu, 1.0f),
                reader.Three(0x70E0B496u, new Vector3(1.0f, 1.0f, 1.0f)),
                reader.Three(0x81136DEBu, new Vector3(1.0f, 1.0f, 1.0f)),
                reader.Three(0x5C62E494u, new Vector3(1.0f, 0.0f, 0.0f))),
            new(
                reader.Float(0xDF85B6CDu, 1.0f),
                reader.Float(0xF23BFE3Bu, 1.0f),
                reader.Three(0xF910F475u, new Vector3(1.0f, 1.0f, 1.0f)),
                reader.Three(0x82ECC10Au, new Vector3(1.0f, 1.0f, 1.0f)),
                reader.Three(0x5E3C37B3u, new Vector3(0.0f, 0.0f, 0.0f))),
            new(
                reader.Float(0x63B603CEu, 1.0f),
                reader.Float(0x7A6C3E1Au, 1.0f),
                reader.Three(0x81413454u, new Vector3(1.0f, 1.0f, 1.0f)),
                reader.Three(0x84C61429u, new Vector3(1.0f, 1.0f, 1.0f)),
                reader.Three(0x60158AD2u, new Vector3(0.0f, 0.0f, 0.0f))),
        });
        WeatheringMasks = Array.AsReadOnly(new DirectionalWeatheringMaskSettings[]
        {
            new(reader.Float(0xA37748A9u, 0.0f), reader.Float(0xF208E8D7u, 0.0f)),
            new(reader.Float(0x9F78D788u, 0.0f), reader.Float(0x2B59F998u, 0.0f)),
            new(reader.Float(0x9B7A6667u, 0.0f), reader.Float(0x64AB0A59u, 0.0f)),
            new(reader.Float(0x977BF546u, 0.0f), reader.Float(0x9DFC1B1Au, 0.0f)),
            new(reader.Float(0x937D8425u, 0.0f), reader.Float(0xD74D2BDBu, 0.0f)),
            new(reader.Float(0x8F7F1304u, 0.0f), reader.Float(0x109E3C9Cu, 0.0f)),
            new(reader.Float(0x8B80A1E3u, 0.0f), reader.Float(0x49EF4D5Du, 0.0f)),
            new(reader.Float(0x878230C2u, 0.0f), reader.Float(0x83405E1Eu, 0.0f)),
            new(reader.Float(0x8383BFA1u, 0.0f), reader.Float(0xBC916EDFu, 0.0f)),
            new(reader.Float(0x7F854E80u, 0.0f), reader.Float(0xF5E27FA0u, 0.0f)),
            new(reader.Float(0x688747B8u, 0.0f), reader.Float(0x528B5B68u, 0.0f)),
            new(reader.Float(0xECB794B9u, 0.0f), reader.Float(0x435C62C7u, 0.0f)),
            new(reader.Float(0x70E7E1BAu, 0.0f), reader.Float(0x342D6A26u, 0.0f)),
            new(reader.Float(0xF5182EBBu, 0.0f), reader.Float(0x24FE7185u, 0.0f)),
            new(reader.Float(0x79487BBCu, 0.0f), reader.Float(0x15CF78E4u, 0.0f)),
            new(reader.Float(0xFD78C8BDu, 0.0f), reader.Float(0x06A08043u, 0.0f)),
            new(reader.Float(0x81A915BEu, 0.0f), reader.Float(0xF77187A2u, 0.0f)),
            new(reader.Float(0x05D962BFu, 0.0f), reader.Float(0xE8428F01u, 0.0f)),
            new(reader.Float(0x8A09AFC0u, 0.0f), reader.Float(0xD9139660u, 0.0f)),
            new(reader.Float(0x0E39FCC1u, 0.0f), reader.Float(0xC9E49DBFu, 0.0f)),
            new(reader.Float(0x6488D697u, 0.0f), reader.Float(0x8BDC6C29u, 0.0f)),
            new(reader.Float(0xE8B92398u, 0.0f), reader.Float(0x7CAD7388u, 0.0f)),
            new(reader.Float(0x6CE97099u, 0.0f), reader.Float(0x6D7E7AE7u, 0.0f)),
            new(reader.Float(0xF119BD9Au, 0.0f), reader.Float(0x5E4F8246u, 0.0f)),
        });
        AdditionalWeatheringTextures = Array.AsReadOnly(new DirectionalWeatheringTextureSettings[]
        {
            new(reader.Unsigned(0xBB0A56C2u, 0u), reader.Unsigned(0x6ACF7382u, 0u)),
            new(reader.Unsigned(0xF45B6783u, 0u), reader.Unsigned(0x5BA07AE1u, 0u)),
            new(reader.Unsigned(0x2DAC7844u, 0u), reader.Unsigned(0x4C718240u, 0u)),
        });
        OutlineColors = Array.AsReadOnly(new DirectionalOutlineColorSettings[]
        {
            new(reader.Three(0xE04B53F1u, new Vector3(0.0f, 0.0f, 0.0f))),
            new(reader.Three(0xC2291D90u, new Vector3(0.0f, 0.0f, 0.0f))),
            new(reader.Three(0xA406E72Fu, new Vector3(0.0f, 0.0f, 0.0f))),
            new(reader.Three(0x85E4B0CEu, new Vector3(0.0f, 0.0f, 0.0f))),
        });
        RampRims = Array.AsReadOnly(new DirectionalRampRimSettings[]
        {
            new(
                reader.Float(0xE21D0A23u, 0.0f),
                reader.Float(0x11BC7932u, 0.0f),
                reader.Float(0xC4DCD05Eu, 15.0f),
                reader.Float(0x55F41F87u, 10.0f),
                reader.Three(0x0ADD42B1u, new Vector3(1.0f, 1.0f, 1.0f))),
            new(
                reader.Float(0x09F6B621u, 0.0f),
                reader.Float(0x2591C7F2u, 0.0f),
                reader.Float(0xE882C246u, 15.0f),
                reader.Float(0x6AF7713Du, 10.0f),
                reader.Three(0x4AB23D53u, new Vector3(1.0f, 1.0f, 1.0f))),
            new(
                reader.Float(0xF6EC42E5u, 0.0f),
                reader.Float(0x5C0F9274u, 0.0f),
                reader.Float(0x4B9873A0u, 15.0f),
                reader.Float(0xD4B20649u, 10.0f),
                reader.Three(0xDCA18473u, new Vector3(1.0f, 1.0f, 1.0f))),
            new(
                reader.Float(0x01F3BFE3u, 0.0f),
                reader.Float(0xD31F8134u, 0.0f),
                reader.Float(0x65A33188u, 15.0f),
                reader.Float(0x2FA98CFFu, 10.0f),
                reader.Three(0xDE545E15u, new Vector3(1.0f, 1.0f, 1.0f))),
        });
        if ((CurrentFieldWriterFlags & 2) != 0)
            throw new NotSupportedException("Directional optional current-field writer is not implemented.");
    }

    // This transient reader retains no declaration bytes after the typed owner is imported.
    private sealed class OptionalFields
    {
        private readonly Dictionary<uint, DokProperty> properties;
        public OptionalFields(DokObject declaration)
        {
            ArgumentNullException.ThrowIfNull(declaration);
            properties = new();
            foreach (var property in declaration.Properties)
            {
                if (!properties.TryAdd(property.NameHash, property))
                    throw new InvalidDataException($"Directional property {property.NameHash:X8} is duplicated.");
                int width = property.TypeCode switch
                { 0 or 1 => 1, 2 or 3 => 2, 4 or 5 or 8 => 4, 10 => 16, 12 => 8, _ => 0 };
                if (width == 0 || property.ElementSize != width || property.ElementCount < 0 ||
                    property.RawValue.Length != (long)width * property.ElementCount)
                    throw new InvalidDataException($"Directional property {property.NameHash:X8} has an invalid extent.");
            }
        }
        private byte[]? Value(uint hash, uint type, int count) =>
            properties.TryGetValue(hash, out var value) && value.TypeCode == type && value.ElementCount == count
                ? value.RawValue : null;
        public byte Byte(uint hash, byte fallback, uint type) => Value(hash, type, 1) is { } v ? v[0] : fallback;
        public uint Unsigned(uint hash, uint fallback) => Value(hash, 5, 1) is { } v ? BinaryPrimitives.ReadUInt32LittleEndian(v) : fallback;
        public int Integer(uint hash, int fallback) => Value(hash, 4, 1) is { } v ? BinaryPrimitives.ReadInt32LittleEndian(v) : fallback;
        public float Float(uint hash, float fallback) => Value(hash, 8, 1) is { } v ? BinaryPrimitives.ReadSingleLittleEndian(v) : fallback;
        public Vector2 Two(uint hash, Vector2 fallback) => Value(hash, 8, 2) is { } v ? new(F(v, 0), F(v, 4)) : fallback;
        public Vector3 Three(uint hash, Vector3 fallback) => Value(hash, 8, 3) is { } v ? new(F(v, 0), F(v, 4), F(v, 8)) : fallback;
        public Vector4 Four(uint hash, Vector4 fallback) => Value(hash, 8, 4) is { } v ? new(F(v, 0), F(v, 4), F(v, 8), F(v, 12)) : fallback;
        public DirectionalUnsignedPair UnsignedPair(uint hash, uint first, uint second) =>
            Value(hash, 5, 2) is { } v ? new(U(v, 0), U(v, 4)) : new(first, second);
        public IReadOnlyList<uint> UnsignedThree(uint hash) =>
            Array.AsReadOnly(Value(hash, 5, 3) is { } v ? new[] { U(v, 0), U(v, 4), U(v, 8) } : new uint[3]);
        public ReadOnlyMemory<byte> Bytes(uint hash, int count) => Value(hash, 1, count)?.ToArray() ?? new byte[count];
        private static float F(byte[] bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset, 4));
        private static uint U(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    }
}
