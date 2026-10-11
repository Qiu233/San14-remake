using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Original Sky2Star native-pass0 default VS/GS/PS. The original GS
/// body is explicitly expanded into the vertex backend, retaining its math.</summary>
public sealed class SkyStarProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public ShaderSpirv VertexTranslation { get; }
    public ShaderSpirv GeometryTranslation { get; }
    public ShaderSpirv PixelTranslation { get; }
    public SkyStarAdaptedVertex VertexAdapter { get; }
    public SkyStarAnalyticPixel AnalyticPixelAdapter { get; }
    public DxbcResourceDefinitions VertexDefinitions { get; }
    public DxbcResourceDefinitions GeometryDefinitions { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    public Rid ShaderRid { get; }
    public Rid AnalyticShaderRid { get; }
    private Task? _disposal;
    private SkyStarProgram(LoadedShaderContainer source, G2ShaderCombination combination,
        ShaderSpirv vertex, ShaderSpirv geometry, ShaderSpirv pixel,
        SkyStarAdaptedVertex adapter, SkyStarAnalyticPixel analytic, DxbcResourceDefinitions vd, DxbcResourceDefinitions gd,
        TranslatedShaderBindings pb, Rid shader, Rid analyticShader)
        => (Source, Combination, VertexTranslation, GeometryTranslation, PixelTranslation, VertexAdapter,
            AnalyticPixelAdapter, VertexDefinitions, GeometryDefinitions, PixelBindings, ShaderRid, AnalyticShaderRid)
            = (source, combination, vertex, geometry, pixel, adapter, analytic, vd, gd, pb, shader, analyticShader);

    public static async Task<SkyStarProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        var combination = source.ReadG1ResourceBindings(explicitCombinationIndex).Combination;
        if (combination.Stages.Count(s => s.Entry is not null) != 3)
            throw new InvalidDataException("Star program requires all original vertex, geometry and pixel stages.");
        var vertexSlot = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null);
        var geometrySlot = combination.Stages.Single(s => s.StageCode == 2 && s.Entry is not null);
        var pixelSlot = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
        var vertex = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, vertexSlot.Entry!.ProgramIndex), 1);
        var geometry = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, geometrySlot.Entry!.ProgramIndex), 2);
        var pixel = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, pixelSlot.Entry!.ProgramIndex), 0);
        if (pixel.SourceProgramSha256 != "069F085CE59681A57144B04C878211C40EE4B62F90503B31331E731A872B40D3")
            throw new InvalidDataException("Star original pixel program hash differs.");
        var adapter = SkyStarVertexAdapter.Adapt(vertex, geometry);
        var analytic = SkyStarPixelAdapter.AdaptAnalytic(pixel);
        var vd = source.ReadResourceDefinitions(vertexSlot.Entry.ProgramIndex);
        var gd = source.ReadResourceDefinitions(geometrySlot.Entry.ProgramIndex);
        var pb = TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(pixelSlot.Entry.ProgramIndex), pixel.Bytes);
        if (pb.Bindings.Count != 3 || pb.Bindings.Any(b => b.Device.DescriptorSet != 0) ||
            pb.Bindings.Single(b => b.DescriptorKind == "UniformBuffer").Device.RequiredBufferBytes != 16 ||
            pb.Bindings.Single(b => b.DescriptorKind == "Texture").Source is not { BindPoint: 0, Dimension: 4 })
            throw new InvalidDataException("Star pixel CB/texture interface differs.");
        var shaders = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            Rid Create(byte[] fragment, string name)
            {
                using var stages = new RDShaderSpirV(); stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, fragment);
                Rid result = rd.ShaderCreateFromSpirV(stages, name);
                if (!result.IsValid) throw new InvalidDataException("Device rejected expanded original Sky2Star program.");
                return result;
            }
            Rid full = Create(pixel.Bytes, "OriginalSky2StarExpandedGS");
            try { return (Full: full, Analytic: Create(analytic.Bytes, "OriginalSky2StarAnalyticBranch")); }
            catch { rd.FreeRid(full); throw; }
        });
        return new(source, combination, vertex, geometry, pixel, adapter, analytic, vd, gd, pb, shaders.Full, shaders.Analytic);
    }
    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(AnalyticShaderRid); rd.FreeRid(ShaderRid); return true; }));
}
