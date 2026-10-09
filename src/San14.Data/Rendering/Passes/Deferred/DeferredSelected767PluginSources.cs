using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Project plugin defaults used by the owned graphics initialization
/// and prepare callbacks.</summary>
public sealed class LoadedDeferredSelected767PluginSource
{
    internal LoadedDeferredSelected767PluginSource(string locale,string ruleVersion,Vector4 genericTime)
    { Locale=locale;RuleVersion=ruleVersion;GenericTime=RenderRuleNumericEncoding.Vectors(genericTime); }
    public string Locale { get; }
    public string RuleVersion { get; }
    internal byte[] GenericTime { get; }
    public string ResourceUri=>RuleVersion;
}

/// <summary>Three exact current plugin results from one owned source generation.
/// Source post-graphics-reset Hatching contains two positive infinities and two
/// negative zeros; the authority below retains all original words.</summary>
public sealed class DeferredSelected767PluginPacket
{
    internal DeferredSelected767PluginPacket(long frame,string lightHash,string provenance,
        DeferredOcclusionInputs occ,DeferredShadowFadeInputs fade,DeferredHatchingInputs hatch)
    {
        FrameNumber=frame;EffectiveLightOwnerIdentity=lightHash;CurrentProducerProvenance=provenance;
        Occlusion=occ;ShadowFade=fade;Hatching=hatch;
        HatchingIeeeProof=DeferredSelected767HatchingIeeeProof.FromSuccessfulOwnedSource(hatch,provenance,frame,lightHash);
    }
    public long FrameNumber { get; }
    public string EffectiveLightOwnerIdentity { get; }
    public string CurrentProducerProvenance { get; }
    public DeferredOcclusionInputs Occlusion { get; }
    public DeferredShadowFadeInputs ShadowFade { get; }
    public DeferredHatchingInputs Hatching { get; }
    public DeferredSelected767HatchingIeeeProof HatchingIeeeProof { get; }

    /// <summary>The remaining three current owners are mandatory. This method
    /// supplies no refraction alias cache, viewport or projection defaults.</summary>
    public DeferredSelected767AdditionalInputs Complete(DeferredRefractionInputs refraction,
        DeferredTranslucencyViewportInputs viewport,DeferredProjectionInputs projection,
        string otherCurrentProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(refraction);ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(projection);ArgumentException.ThrowIfNullOrWhiteSpace(otherCurrentProducerProvenance);
        return new(Occlusion,refraction,ShadowFade,Hatching,viewport,projection,
            CurrentProducerProvenance+";"+otherCurrentProducerProvenance,HatchingIeeeProof);
    }
}

/// <summary>Owned successful source graphics initialization, followed by a
/// fresh exact deferred shader, fresh GenericOwner and parameter table, and
/// successful default callbacks. The data
/// stays under this owner's control. This phase includes the nonnull global
/// Hatching data/reset and is distinct from a shader created before graphics
/// initialization or a later externally edited plugin data generation.</summary>
public sealed class DeferredSelected767PluginOwner
{
    private readonly LoadedDeferredSelected767PluginSource source;
    private readonly string shaderUri,shaderHash;
    private readonly byte[] genericTime;
    private readonly string genericOwnerIdentity="owned-fresh-source-GenericOwner:"+Guid.NewGuid().ToString("N");
    private readonly DeferredOcclusionInputs occ=new(new Vector2(1,1));
    private readonly DeferredShadowFadeInputs fade=new(0);
    private DeferredSelected767PluginOwner(LoadedDeferredSelected767PluginSource source,
        LoadedDeferredTexturePlan plan)
    { this.source=source;shaderUri=plan.Shader.Source.ResourceUri;shaderHash=plan.Shader.Source.DecodedSha256;genericTime=source.GenericTime.ToArray(); }

    public static DeferredSelected767PluginOwner CreateAfterSuccessfulGraphicsInitialization(
        LoadedDeferredSelected767PluginSource source,LoadedDeferredTexturePlan plan)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(plan);
        VerifyShader(plan.Shader);
        return new(source,plan);
    }

    /// <summary>Restore the successful default/preflight/prepare path for this
    /// frame. Actual selected modifier gates must skip the named Hatching/Fade
    /// updates. Clearing either gate requires a separately restored live setter
    /// owner; its source values are never inferred from a property or texture.</summary>
    public DeferredSelected767PluginPacket Prepare(DeferredF103SelectedSource selected,
        MapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(lighting);
        selected.Validate();VerifyShader(selected.SourcePlan.Shader);
        if(selected.Combination.Index!=1022||selected.PixelProgramIndex!=767||
            selected.PixelProgramSha256!=DeferredSelected767Constants.PixelProgramSha256||
            selected.SourcePlan.Shader.Source.ResourceUri!=shaderUri||selected.SourcePlan.Shader.Source.DecodedSha256!=shaderHash)
            throw new InvalidDataException("Owned plugin prepare requires the same exact source1022/PS767 shader.");
        var feature=selected.OwnedFeatureSource??throw new InvalidDataException("Plugin prepare requires the actual owned kind1 feature.");
        feature.ValidateFrame(selected.Camera,lighting);
        var modifier=feature.Scratch.SelectedModifier1B0;
        if(modifier.OwnerIdentity!=feature.SelectedModifierOwnerIdentity||modifier.Offset!=0x1B0||
            feature.SelectedModifierLightData.Length!=MainLightConstruction.LightDataBytes)
            throw new InvalidDataException("Plugin gates require the exact selected effective LightData modifier reference.");
        ReadOnlySpan<byte> light=feature.SelectedModifierLightData.Span;
        if((light[modifier.Offset+0x47]&1)==0||
            (BinaryPrimitives.ReadUInt32LittleEndian(light.Slice(modifier.Offset+0x48,4))&0x40)==0)
            throw new NotSupportedException("A live F103 ShadowMask or Hatching setter replaced the fresh owned source data scope.");
        // C+88 points to the GenericOwner parameter cache, separately from L.
        // This owner creates that exact fresh cache. Its constructor time
        // literal is (0,0,3600,0); no external global reuse or later time setter
        // is part of this owned phase. Hatching reads its first three words.
        for(int at=0;at<12;at+=4)
            if(!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(genericTime.AsSpan(at,4))))
                throw new InvalidDataException("Source Hatching reset requires its actual owned finite GenericOwner time triple.");
        float positiveInfinity=BitConverter.UInt32BitsToSingle(0x7F800000),negativeZero=BitConverter.UInt32BitsToSingle(0x80000000);
        var hatch=new DeferredHatchingInputs(new(0,-1,positiveInfinity,0),new(0,0,positiveInfinity,0),
            new(1,0,negativeZero),new(negativeZero,1,0));
        string provenance=source.ResourceUri+
            ";successful-owned-graphics-init/Occ-data1-1/Hatching-reset50B;fresh-source-shader/default-parameter-table;"+
            "fresh-GenericOwner-cache-time(0,0,3600,0)/no-global-reuse-or-later-time-setter;GenericOwner="+genericOwnerIdentity+";"+
            "current-modifier-Fade47.bit0/Hatching48.bit6-skip;prepare-v28/v48;Light="+
            lighting.Binding.LightOwnerIdentity+";frame="+feature.FrameNumber;
        return new(feature.FrameNumber,feature.EffectiveLightOwnerIdentity,provenance,occ,fade,hatch);
    }

    private static void VerifyShader(LoadedShaderContainer shader)
    {
        if(shader.Source.FileId!=0x9D8E811F||shader.Source.OverrideSource is not null)
            throw new InvalidDataException("Plugin owner requires the unmodified exact system9D8E811F source shader.");
        var records=G1ShaderConfigurationReader.Read(shader.Source.Bytes).Records;
        // The directory has16 records:14 registered plugins plus the separate
        // GBufferLayout and SecondaryHighlight configuration records.
        if(records.Count!=16||new[]{"OccFader","ShadowMask","HatchingShadow","GBufferLayout","SecondaryHighlight"}.Any(name=>records.Count(r=>r.Name==name)!=1))
            throw new InvalidDataException("Required exact source plugin factories differ.");
    }
}
