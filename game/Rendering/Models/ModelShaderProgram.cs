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
    private Task? _disposal;

    private ModelShaderProgram(ModelShaderProgramPreparation prepared, Rid shader)
    {
        Source = prepared.Source; Combination = prepared.Combination;
        VertexProgramIndex = prepared.VertexProgramIndex; VertexSourceSha256 = prepared.VertexSourceSha256;
        VertexTranslation = prepared.VertexTranslation; VertexAdapter = prepared.VertexAdapter;
        VertexBindings = prepared.VertexBindings; VertexSignatures = prepared.VertexSignatures;
        VertexDeviceTransportSignatures = prepared.VertexDeviceTransportSignatures;
        PixelStage = prepared.PixelStage; ColorOutputLocations = prepared.ColorOutputLocations;
        ColorAttachmentCount = prepared.ColorAttachmentCount; ShaderRid = shader;
    }

    public static async Task<ModelShaderProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler, ArmySkinVertexProfile? armySkinProfile=null)
    {
        var prepared = ModelShaderPreparation.Prepare(source, explicitCombinationIndex, compiler, armySkinProfile);
        return await BuildPreparedAsync(prepared);
    }

    // Device components may adapt the prepared backend interface before upload.
    internal static async Task<ModelShaderProgram> BuildPreparedAsync(ModelShaderProgramPreparation prepared)
    {
        Rid shader = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var stages = new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, prepared.VertexAdapter.Bytes);
            if (prepared.PixelStage is { } pixel)
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, pixel.Translation.Bytes);
            Rid value = rd.ShaderCreateFromSpirV(stages, "OriginalModelSourceProgram");
            if (!value.IsValid) throw new InvalidDataException("RenderingDevice rejected the selected original model stages.");
            return value;
        });
        return new(prepared, shader);
    }

    private ModelPixelShaderStage RequirePixelStage() => PixelStage
        ?? throw new InvalidOperationException("The selected original model combination has no pixel stage.");

    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(ShaderRid); return true; }));

    internal void RequireLive() => ObjectDisposedException.ThrowIf(_disposal is not null, this);
}
