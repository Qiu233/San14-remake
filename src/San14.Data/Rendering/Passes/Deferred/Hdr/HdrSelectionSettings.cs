namespace San14.Data;

public enum HdrColorMode : uint { Default = 0, Ocean = 0x50, CameraSpecular = 0x60 }
public enum HdrReflectionMode : uint { Available, Unavailable, Extended }

/// <summary>Current context predicates consumed by HDR selection. These groups
/// remain neutral where the resource format does not identify an effect name.</summary>
public readonly record struct HdrContextSelection(bool SharedTargets, bool LightPair,
    bool CombinedLighting, bool FirstLighting, bool SecondLighting,
    bool VolumetricFog, bool ExtendedReflection, bool RefractionSource);

public readonly record struct HdrSelectionSettings(HdrColorMode ColorMode,
    uint CameraSpecularOcclusion, HdrContextSelection Context,
    bool ReflectionAvailable, bool RefractionResourcesAvailable,
    bool LocalLightQueriesEmpty, bool PreRendererPresent);

/// <summary>Logical feature request. Resource encoding is confined to HdrShaderCodec.</summary>
public readonly record struct HdrFeatureKey(uint Flags, uint DirectionalLights,
    uint PointLights, uint SpotLights, uint HemisphereLights, uint DirectionalMode,
    uint FogMode, uint FogQuality, uint ShadowMode, uint ShadowQuality,
    uint LocalShadowMode, uint MaterialMode);

/// <summary>Complete selection key, including resource constants not written by
/// the current rules. Neutral condition-group names avoid inventing effect semantics.</summary>
public readonly record struct HdrVariantKey(uint BaseMode, uint SpecularOcclusion,
    uint SharedTargets, uint FirstLighting, uint SecondLighting, uint SharedAuxiliary,
    uint Constant6, uint Constant7, uint Constant8, uint LightPair,
    uint LightPairAuxiliary, uint CombinedLighting, uint LocalLightQueriesEmpty,
    uint VolumetricFog, uint RefractionResources, HdrReflectionMode Reflection,
    uint RefractionSource, uint SharedDistance, uint Constant18, uint Constant19,
    uint Constant20, uint Constant21, uint Constant22, uint Constant23);

internal enum HdrVariantField
{
    SpecularOcclusion, SharedTargets, FirstLighting, SecondLighting, SharedAuxiliary,
    LightPair, LightPairAuxiliary, CombinedLighting, LocalLightQueriesEmpty,
    VolumetricFog, RefractionResources, Reflection, RefractionSource, SharedDistance
}

internal readonly record struct HdrShaderPlugins(bool Ocean, bool Refraction,
    bool ThinSpecularSource, bool VolumetricFog);

public readonly record struct HdrOceanRecord(uint EnabledGroups,uint Group)
{
    public bool IsAdmitted=>Group is 1 or 2 or 4 or 8 && (EnabledGroups&Group)!=0;
    public uint StencilReference=>Group|(uint)HdrColorMode.Ocean;
}
public sealed record HdrOceanSelection(HdrProgramSelection Selection,uint StencilReference);
