using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Device owner for a CPU-prepared ordinary terrain stage pair.</summary>
public sealed class TerrainGbufferProgram : IAsyncDisposable
{
    public Rid ShaderRid { get; }
    public LoadedTerrainGbiMapResources Source { get; }
    public bool Coarse { get; }
    public TerrainShaderProgramPreparation Preparation { get; }
    public G2ShaderCombination Combination => Preparation.Stages.Combination;
    public ShaderSpirv VertexTranslation => Preparation.VertexTranslation;
    public AdaptedVertexShader VertexAdapter => Preparation.VertexAdapter;
    public ShaderSpirv PixelTranslation => Preparation.PixelTranslation;
    public TranslatedShaderBindings VertexBindings => Preparation.VertexBindings;
    public TranslatedShaderBindings PixelBindings => Preparation.PixelBindings;
    private Task? disposal;
    private TerrainGbufferProgram(Rid shader, LoadedTerrainGbiMapResources source, bool coarse,
        TerrainShaderProgramPreparation preparation)
        => (ShaderRid, Source, Coarse, Preparation) = (shader, source, coarse, preparation);

    public static async Task<TerrainGbufferProgram> BuildAsync(LoadedTerrainGbiMapResources source,
        bool coarse, Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);
        var preparation = TerrainShaderPreparation.Prepare(coarse ? source.CoarseProgram : source.DetailProgram, compiler);
        Rid shader = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var stages = new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, preparation.VertexAdapter.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, preparation.PixelTranslation.Bytes);
            Rid rid = rd.ShaderCreateFromSpirV(stages, coarse ? "OriginalCoarseTerrainGBuffer" : "OriginalDetailTerrainGBuffer");
            if (!rid.IsValid) throw new InvalidDataException("RenderingDevice rejected the terrain shader pair.");
            return rid;
        });
        return new(shader, source, coarse, preparation);
    }
    public ValueTask DisposeAsync() => new(disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(ShaderRid); return true; }));
}
