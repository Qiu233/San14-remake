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
    public Rid ShaderRid { get; private set; }
    public Rid AnalyticShaderRid { get; private set; }
    private readonly object lifetimeGate = new();
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
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var program = new SkyStarProgram(source, combination, vertex, geometry, pixel,
                adapter, analytic, vd, gd, pb, default, default);
            Rid Create(byte[] fragment, string name)
            {
                using var stages = new RDShaderSpirV(); stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.Bytes);
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, fragment);
                Rid result = rd.ShaderCreateFromSpirV(stages, name);
                if (!result.IsValid) throw new InvalidDataException("Device rejected expanded original Sky2Star program.");
                return result;
            }
            try
            {
                program.ShaderRid = Create(pixel.Bytes, "OriginalSky2StarExpandedGS");
                program.AnalyticShaderRid = Create(analytic.Bytes, "OriginalSky2StarAnalyticBranch");
                return program;
            }
            catch (Exception constructionFailure)
            {
                try { program.Release(rd); }
                catch (Exception retirementFailure)
                {
                    var partial = new NativeMapBorrowerRetirementPackage("Star-program-partial/" + Guid.NewGuid().ToString("N"), program);
                    partial.RetainFailure();
                    throw new NativeMapBorrowerConstructionException(constructionFailure, retirementFailure, partial);
                }
                throw;
            }
        });
    }

    private void Release(RenderingDevice rd)
    {
        if (AnalyticShaderRid.IsValid) { rd.FreeRid(AnalyticShaderRid); AnalyticShaderRid = default; }
        if (ShaderRid.IsValid) { rd.FreeRid(ShaderRid); ShaderRid = default; }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate)
        {
            if (_disposal is null || _disposal.IsFaulted)
                _disposal = TerrainDeviceTexture.OnRenderThreadAsync(rd => { Release(rd); return true; });
            return new(_disposal);
        }
    }
}
