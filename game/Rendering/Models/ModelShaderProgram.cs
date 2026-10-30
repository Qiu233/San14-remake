using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>An explicitly selected source model VS and optional PS. Selection of a
/// combination and original raster/material state remains a separate source
/// input. No preview lighting or replacement shader is used.</summary>
public sealed class ModelShaderProgram : IAsyncDisposable
{
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public int VertexProgramIndex { get; }
    public ModelPixelShaderStage? PixelStage { get; }
    // Existing color consumers require a pixel stage; absent PS is not an
    // empty replacement shader. Shadow consumers inspect PixelStage directly.
    public int PixelProgramIndex => RequirePixelStage().ProgramIndex;
    public string VertexSourceSha256 { get; }
    public string PixelSourceSha256 => RequirePixelStage().SourceSha256;
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation => RequirePixelStage().Translation;
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings => RequirePixelStage().Bindings;
    public DxbcProgramSignatures VertexSignatures { get; }
    public DxbcProgramSignatures VertexDeviceTransportSignatures { get; }
    public IReadOnlyList<uint> ColorOutputLocations { get; }
    public int ColorAttachmentCount { get; }
    public Rid ShaderRid { get; }
    private readonly RenderDeviceResources _resources;
    private volatile bool _retiring;

    private ModelShaderProgram(ModelShaderProgramPreparation prepared, Rid shader, RenderDeviceResources resources)
    {
        Source = prepared.Source; Combination = prepared.Combination;
        VertexProgramIndex = prepared.VertexProgramIndex; VertexSourceSha256 = prepared.VertexSourceSha256;
        VertexTranslation = prepared.VertexTranslation; VertexAdapter = prepared.VertexAdapter;
        VertexBindings = prepared.VertexBindings; VertexSignatures = prepared.VertexSignatures;
        VertexDeviceTransportSignatures = prepared.VertexDeviceTransportSignatures;
        PixelStage = prepared.PixelStage; ColorOutputLocations = prepared.ColorOutputLocations;
        ColorAttachmentCount = prepared.ColorAttachmentCount; ShaderRid = shader;
        _resources = resources;
    }

    public static async Task<ModelShaderProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler, ArmySkinVertexProfile? armySkinProfile=null, bool reflectionCaptureUnusedConstantTail=false)
    {
        var prepared = ModelShaderPreparation.Prepare(source, explicitCombinationIndex, compiler, armySkinProfile,reflectionCaptureUnusedConstantTail);
        return await BuildPreparedAsync(prepared);
    }

    // Device components may adapt the prepared backend interface before upload.
    internal static async Task<ModelShaderProgram> BuildPreparedAsync(ModelShaderProgramPreparation prepared)
    {
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources = new RenderDeviceResources("RenderingDevice rejected the selected original model stages.");
            try
            {
                using var stages = new RDShaderSpirV();
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, prepared.VertexAdapter.Bytes);
                if (prepared.PixelStage is { } pixel)
                    stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, pixel.Translation.Bytes);
                Rid shader = resources.Own(rd.ShaderCreateFromSpirV(stages, "OriginalModelSourceProgram"));
                return new ModelShaderProgram(prepared, shader, resources);
            }
            catch (Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd, failure, "Model-program-partial");
                throw;
            }
        });
    }

    private ModelPixelShaderStage RequirePixelStage() => PixelStage
        ?? throw new InvalidOperationException("The selected original model combination has no pixel stage.");

    public ValueTask DisposeAsync()
    {
        _retiring = true;
        return _resources.DisposeAsync();
    }

    internal void RequireLive() => ObjectDisposedException.ThrowIf(_retiring, this);
}
