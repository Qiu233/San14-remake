using Godot;
using San14.Data;
using San14.Data.Formats;

public enum SkyCubeOperation { Copy, Filter }

/// <summary>Caller-selected original cube copy/filter shader pair. The active
/// feature variant is not inferred from the native pass index.</summary>
public sealed class SkyCubeProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public Rid ShaderRid { get; }
    public SkyCubeOperation Operation { get; }
    public SkyAmbientFilterSelection SourceSampleSelection { get; }
    public int FilterSampleCount => Operation == SkyCubeOperation.Filter ? checked((int)SourceSampleSelection.SampleCount) : 0;
    private readonly RenderDeviceResources resources;

    private SkyCubeProgram(LoadedShaderContainer source, G2ShaderCombination combination,
        ShaderSpirv vertex, AdaptedVertexShader adapter, ShaderSpirv pixel,
        TranslatedShaderBindings vertexBindings, TranslatedShaderBindings pixelBindings, Rid shader,
        SkyCubeOperation operation, SkyAmbientFilterSelection sampleSelection, RenderDeviceResources resources)
    {
        (Source, Combination, VertexTranslation, VertexAdapter, PixelTranslation, VertexBindings, PixelBindings, ShaderRid,Operation)
            = (source, combination, vertex, adapter, pixel, vertexBindings, pixelBindings, shader,operation);
        SourceSampleSelection=sampleSelection;
        this.resources=resources;
    }

    public static async Task<SkyCubeProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler, SkyCubeOperation operation = SkyCubeOperation.Copy)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        if(operation is not (SkyCubeOperation.Copy or SkyCubeOperation.Filter))
            throw new InvalidDataException("Unknown sky cube operation.");
        var sampleSelection=SkyAmbientFilterSelection.FromExplicitCombination(source,
            explicitCombinationIndex,operation==SkyCubeOperation.Filter);
        var combination = operation==SkyCubeOperation.Filter?
            sampleSelection.FilterCombination:sampleSelection.CopyCombination;
        if (combination.Stages.Any(s => s.Entry is not null && s.StageCode is not (0 or 1)))
            throw new InvalidDataException("Sky cube source requires vertex and pixel stages only.");
        var vs = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null);
        var ps = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
        var vertexBytes = source.Container.GetProgramBytes(source.Source.Bytes, vs.Entry!.ProgramIndex);
        var pixelBytes = source.Container.GetProgramBytes(source.Source.Bytes, ps.Entry!.ProgramIndex);
        var vertex = compiler.Compile(vertexBytes, 1);
        var pixel = compiler.Compile(pixelBytes);
        var adapter = VertexShaderAdapter.Adapt(vertex.Bytes, 0, 0, 0);
        var vertexBindings = TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(vs.Entry.ProgramIndex), adapter.Bytes);
        var pixelBindings = TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(ps.Entry.ProgramIndex), pixel.Bytes);
        if (vertexBindings.Bindings.Count != 1 || pixelBindings.Bindings.Count != (operation==SkyCubeOperation.Copy?3:5) ||
            vertexBindings.Bindings.Any(b => b.Device.DescriptorSet != 1) || pixelBindings.Bindings.Any(b => b.Device.DescriptorSet != 0) ||
            vertexBindings.Interface.StageVariables.Any(v => v.StorageClass == 1 && v.Location is not null))
            throw new InvalidDataException("Sky cube descriptors or vertex-ID geometry differ from the source contract.");
        foreach (var binding in vertexBindings.Bindings.Concat(pixelBindings.Bindings))
            if (binding.DescriptorKind == "UniformBuffer" && (binding.Source.BindPoint != 0 ||
                binding.Device.RequiredBufferBytes != 16 || binding.ConstantBuffer is not { ByteSize: 16 } buffer ||
                buffer.Variables.Count != 1 || buffer.Variables[0].Name.Text != "g_vMipInfo" ||
                buffer.Variables[0].StartOffset != 0 || buffer.Variables[0].ByteSize != 16))
                throw new NotSupportedException("Godot cube draws require a 16-byte g_vMipInfo buffer at slot zero and field offset zero.");
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources = new RenderDeviceResources("Device rejected the original sky cube shader pair.");
            try
            {
                using var stages = new RDShaderSpirV();
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, pixel.Bytes);
                Rid shader = resources.Own(rd.ShaderCreateFromSpirV(stages, "OriginalSkyCube" + operation));
                return new SkyCubeProgram(source, combination, vertex, adapter, pixel, vertexBindings, pixelBindings, shader,operation,sampleSelection,resources);
            }
            catch (Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd, failure, "Sky-cube-program-partial");
                throw;
            }
        });
    }

    public ValueTask DisposeAsync() => resources.DisposeAsync();
}
