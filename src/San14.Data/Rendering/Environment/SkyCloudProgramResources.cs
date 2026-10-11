using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The selected Cloud shader and its imported WORK color programs.
/// Frame owners share these resources while retaining their own current
/// selection, animation and clip lifecycle.</summary>
public sealed class SkyCloudProgramResources
{
    private static readonly ConditionalWeakTable<LoadedShaderContainer, SkyCloudProgramResources> Imported = new();
    private readonly ConcurrentDictionary<uint, SkyCloudColorProgramSelection> colors = new();
    private readonly ConcurrentDictionary<(int Feature,uint Level,uint Rays,bool Earth),SkyCloudMainProgramSelection> mains = new();
    private SkyCloudProgramResources(LoadedShaderContainer shader)
    {
        Shader = shader;
        Bindings = shader.ReadG1Bindings();
        if (Bindings.Variants.RecordWidth != 36 || Bindings.PassCount != 6 || Bindings.Variants.Records.Count == 0)
            throw new NotSupportedException("Cloud WORK requires nonempty 36-byte variants and six source passes.");
        var feature = Bindings.Features.FirstOrDefault()?.Feature;
        if (feature is null || feature.Words[6] != 0 || feature.Words[7] != 1)
            throw new NotSupportedException("Cloud WORK requires renderer0/profile1 scattering in the selected feature.");
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
            throw new NotSupportedException("The selected Renderer declares no Cloud shader for WORK production.");
        return Import(repository.LoadShaderContainer("system", key));
    }

    public static SkyCloudProgramResources Import(LoadedShaderContainer shader)
    {
        ArgumentNullException.ThrowIfNull(shader);
        return Imported.GetValue(shader, static source => new(source));
    }

    public SkyCloudColorProgramSelection SelectColor(uint sourceLowCostLevel) =>
        colors.GetOrAdd(Math.Min(sourceLowCostLevel, 2u), level => SkyCloudColorProgramSelection.ImportLevel(this, level));

    public SkyCloudMainProgramSelection SelectMain(ReadOnlySpan<byte> rendererRequest,uint lowCostLevel,uint raySamples,bool earthPresent)
    {
        var feature=G1ShaderFeatureMatcher.Select(G1ShaderFeatureRowReader.ReadRow(rendererRequest).Words.ToArray(),
            Bindings.Features.Select(row=>row.Feature).ToArray());
        if(feature.RowIndex<0)throw new NotSupportedException("Main Cloud renderer feature is absent.");
        return mains.GetOrAdd((feature.RowIndex,Math.Min(lowCostLevel,2u),Math.Clamp(raySamples,4u,6u),earthPresent),
            key=>SkyCloudMainProgramSelection.Import(this,key.Feature,key.Level,key.Rays,key.Earth));
    }
}
