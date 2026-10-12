using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The selected Cloud shader and its imported Main, WORK-color and
/// Pattern programs. Frame owners share resources while retaining their own
/// current shader axes, animation and clip lifecycle.</summary>
public sealed class SkyCloudProgramResources
{
    private static readonly ConditionalWeakTable<LoadedShaderContainer, SkyCloudProgramResources> Imported = new();
    private readonly ConcurrentDictionary<uint, SkyCloudColorProgramSelection> colors = new();
    private readonly ConcurrentDictionary<(int Feature,uint Level,uint Rays,bool EarthReferenceNonzero),SkyCloudMainProgramSelection> mains = new();
    private readonly ConcurrentDictionary<(int Feature,int Variant),SkyCloudPatternProgramSelection> patterns = new();
    private SkyCloudProgramResources(LoadedShaderContainer shader)
    {
        Shader = shader;
        Bindings = shader.ReadG1Bindings();
        if (Bindings.Variants.RecordWidth != 36 || Bindings.Variants.Records.Count == 0)
            throw new NotSupportedException("Cloud selection requires nonempty nine-word shader variants.");
    }
    public LoadedShaderContainer Shader { get; }
    internal G1ShaderBindingTables Bindings { get; }

    public static SkyCloudProgramResources Load(GameResourceRepository repository)
        =>Load(repository,NamedViewRendererResources.Load(repository));

    public static SkyCloudProgramResources Load(GameResourceRepository repository,NamedViewRendererResources renderer)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(renderer);
        uint key = renderer.CloudShaderFileId;
        if (key == 0)
            throw new NotSupportedException("The selected Renderer declares no Cloud shader.");
        return Import(repository.LoadShaderContainer("system", key));
    }

    public static SkyCloudProgramResources Import(LoadedShaderContainer shader)
    {
        ArgumentNullException.ThrowIfNull(shader);
        return Imported.GetValue(shader, static source => new(source));
    }

    public SkyCloudColorProgramSelection SelectColor(uint sourceLowCostLevel) =>
        colors.GetOrAdd(Math.Min(sourceLowCostLevel, 2u), level => SkyCloudColorProgramSelection.ImportLevel(this, level));

    /// <summary>The Main Cloud caller tests its current Earth reference.
    /// Retained Sky image bindings have an independent lifetime.</summary>
    public SkyCloudMainProgramSelection SelectMain(ReadOnlySpan<byte> rendererRequest,uint lowCostLevel,uint raySamples,bool earthReferenceNonzero)
    {
        var feature=G1ShaderFeatureMatcher.Select(G1ShaderFeatureRowReader.ReadRow(rendererRequest).Words.ToArray(),
            Bindings.Features.Select(row=>row.Feature).ToArray());
        if(feature.RowIndex<0)throw new NotSupportedException("Main Cloud renderer feature is absent.");
        return mains.GetOrAdd((feature.RowIndex,Math.Min(lowCostLevel,2u),Math.Clamp(raySamples,4u,6u),earthReferenceNonzero),
            key=>SkyCloudMainProgramSelection.Import(this,key.Feature,key.Level,key.Rays,key.EarthReferenceNonzero));
    }

    /// <summary>Resolve the Pattern operation from the current renderer axes.
    /// Unwritten axes come from that renderer's retained variant, rather than
    /// a default row or the later WORK-color request.</summary>
    public SkyCloudPatternProgramSelection SelectPattern(ReadOnlySpan<byte> retainedRendererVariant,
        SkyCloudPatternProgramInputs current)
    {
        var request=SkyCloudPatternProgramSelection.PrepareVariant(retainedRendererVariant,current);
        uint[] featureWords=[0,1,0,0,0,1,current.LightFogMode,current.LightFogMode==7?2u:1u,0,0,0,0];
        var feature=G1ShaderFeatureMatcher.Select(featureWords,Bindings.Features.Select(row=>row.Feature).ToArray());
        if(feature.RowIndex<0)throw new NotSupportedException("Current Pattern renderer feature is absent.");
        var variant=Bindings.Variants.Match(request)
            ??throw new NotSupportedException("Current Pattern renderer variant is absent.");
        return patterns.GetOrAdd((feature.RowIndex,variant.Index),
            key=>SkyCloudPatternProgramSelection.Import(this,key.Feature,key.Variant,request));
    }
}
