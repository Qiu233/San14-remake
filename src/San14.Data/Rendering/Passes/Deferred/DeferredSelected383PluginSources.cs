using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed class DeferredSelected383PluginPacket
{
    internal DeferredSelected383PluginPacket(long frame, string lightHash, string provenance,
        DeferredOcclusionInputs occlusion, DeferredShadowFadeInputs fade, DeferredHatchingInputs hatching)
    {
        FrameNumber = frame; EffectiveLightOwnerIdentity = lightHash; CurrentProducerProvenance = provenance;
        Occlusion = occlusion; ShadowFade = fade; Hatching = hatching;
        HatchingIeeeProof = DeferredSelected383HatchingIeeeProof.FromSuccessfulOwnedSource(hatching, frame, lightHash, provenance);
    }
    public long FrameNumber { get; }
    public string EffectiveLightOwnerIdentity { get; }
    public string CurrentProducerProvenance { get; }
    public DeferredOcclusionInputs Occlusion { get; }
    public DeferredShadowFadeInputs ShadowFade { get; }
    public DeferredHatchingInputs Hatching { get; }
    public DeferredSelected383HatchingIeeeProof HatchingIeeeProof { get; }
    public DeferredSelected383AdditionalInputs Complete(DeferredRefractionInputs refraction,
        DeferredTranslucencyViewportInputs viewport, DeferredProjectionInputs projection, string currentOtherProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(refraction); ArgumentNullException.ThrowIfNull(viewport); ArgumentNullException.ThrowIfNull(projection);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentOtherProducerProvenance);
        return new(Occlusion, refraction, ShadowFade, Hatching, viewport, projection,
            CurrentProducerProvenance + ";" + currentOtherProducerProvenance, HatchingIeeeProof);
    }
}

/// <summary>Independently owned PS383 consumer of the already verified common
/// OccFader/ShadowMask/Hatching constructors and setters. Loading their source
/// retains its historical selected767 label; their native factories are common
/// to shader9D8E811F, not per-combination implementations.</summary>
public sealed class DeferredSelected383PluginOwner
{
    private static readonly ConditionalWeakTable<LoadedShaderContainer, LoadedShaderContainer> ImportedShaders = new();
    private readonly LoadedDeferredSelected767PluginSource source;
    private readonly string shaderUri, shaderHash;
    private readonly byte[] genericTime;
    private readonly string genericOwnerIdentity = "owned-source-383-GenericOwner:" + Guid.NewGuid().ToString("N");
    private DeferredSelected383PluginOwner(LoadedDeferredSelected767PluginSource source, LoadedDeferredTexturePlan plan)
    { this.source = source; shaderUri = plan.Shader.Source.ResourceUri; shaderHash = plan.Shader.Source.DecodedSha256; genericTime = source.GenericTime.ToArray(); }
    public static DeferredSelected383PluginOwner CreateAfterSuccessfulGraphicsInitialization(
        LoadedDeferredSelected767PluginSource verifiedCommonSetterSource, LoadedDeferredTexturePlan plan)
    {
        ArgumentNullException.ThrowIfNull(verifiedCommonSetterSource); ArgumentNullException.ThrowIfNull(plan); VerifyShader(plan.Shader);
        return new(verifiedCommonSetterSource, plan);
    }
    public DeferredSelected383PluginPacket Prepare(DeferredF103SelectedSource selected,
        MapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(lighting);  VerifyShader(selected.SourcePlan.Shader);
        if (selected.Combination.Index != 510 || selected.PixelProgramIndex != 383 || selected.PixelProgramSha256 != DeferredSelected383Constants.PixelProgramSha256 ||
            selected.SourcePlan.Shader.Source.ResourceUri != shaderUri || selected.SourcePlan.Shader.Source.DecodedSha256 != shaderHash)
            throw new InvalidDataException("PS383 plugin preparation requires its actual same source shader selection.");
        var feature = selected.OwnedFeatureSource ?? throw new InvalidDataException("PS383 plugin preparation requires the actual owned kind1 feature.");
        feature.ValidateFrame(selected.Camera, lighting);
        var modifier = feature.Scratch.SelectedModifier1B0;
        if (modifier.OwnerIdentity != feature.SelectedModifierOwnerIdentity || modifier.Offset != 0x1B0 ||
            feature.SelectedModifierLightData.Length != MainLightConstruction.LightDataBytes)
            throw new InvalidDataException("PS383 plugin gates require the exact independently selected modifier H owner.");
        var light = feature.SelectedModifierLightData.Span;
        if ((light[modifier.Offset + 0x47] & 1) == 0 || (BinaryPrimitives.ReadUInt32LittleEndian(light.Slice(modifier.Offset + 0x48, 4)) & 0x40) == 0)
            throw new NotSupportedException("A current PS383 modifier enables a live Hatching/Fade setter; its values need that separate producer.");
        for (int offset = 0; offset < 12; offset += 4)
            if (!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(genericTime.AsSpan(offset, 4))))
                throw new InvalidDataException("PS383 Hatching reset requires the actual finite fresh GenericOwner Time words.");
        var hatching = CreateFreshHatchingResetParameters();
        string provenance = source.ResourceUri +
            ";actual-383-consumer;common-successful-owned-graphics-init;Occ(1,1);Fade(+0);Hatching-reset50B;" +
            "fresh-owned-source-parameter-table/GenericOwner-Time(0,0,3600,0);no-global-reuse-or-later-setter;" +
            "modifier-Fade47.bit0/Hatching48.bit6-skip;GenericOwner=" + genericOwnerIdentity + ";frame=" + feature.FrameNumber;
        return new(feature.FrameNumber, feature.EffectiveLightOwnerIdentity, provenance,
            CreateFreshOcclusionParameters(), CreateFreshShadowFadeParameters(), hatching);
    }
    /// <summary>Successful fresh OccFader data callback; later modifiers use their live setter.</summary>
    public static DeferredOcclusionInputs CreateFreshOcclusionParameters()
        => DeferredOcclusionInputs.SuccessfulDataDefaults();
    /// <summary>Successful fresh ShadowMask data callback, retained when the modifier skips its setter.</summary>
    public static DeferredShadowFadeInputs CreateFreshShadowFadeParameters() => new(0);
    /// <summary>Project rule values for a newly initialized Hatching module.
    /// The caller still supplies its current modifier gate and renderer lifecycle.</summary>
    public static DeferredHatchingInputs CreateFreshHatchingResetParameters()
    {
        float infinity = BitConverter.UInt32BitsToSingle(0x7F800000), negativeZero = BitConverter.UInt32BitsToSingle(0x80000000);
        return new(new(0,-1,infinity,0),new(0,0,infinity,0),new(1,0,negativeZero),new(negativeZero,1,0));
    }
    private static void VerifyShader(LoadedShaderContainer shader)
    {
        if (shader.Source.FileId != 0x9D8E811F || shader.Source.OverrideSource is not null)
            throw new InvalidDataException("PS383 plugin owner requires the exact original common shader source.");
        _ = ImportedShaders.GetValue(shader, static loaded =>
        {
            var configuration = G1ShaderConfigurationReader.Read(loaded.Source.Bytes).Records;
            if (configuration.Count != 16 || new[] { "OccFader", "ShadowMask", "HatchingShadow", "GBufferLayout", "SecondaryHighlight" }
                .Any(name => configuration.Count(record => record.Name == name) != 1))
                throw new InvalidDataException("PS383 common plugin factories differ from the original shader configuration.");
            return loaded;
        });
    }
}
