using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Original stock terrain VS/PS pair with separate stage descriptors.
/// Creation alone does not fill constants, supply a draw or replace the map.</summary>
public sealed class TerrainGbufferProgram : IAsyncDisposable
{
    public Rid ShaderRid { get; }
    public LoadedTerrainGbiMapResources Source { get; }
    public bool Coarse { get; }
    public G2ShaderCombination Combination { get; }
    public ShaderSpirv VertexTranslation { get; }
    public AdaptedVertexShader VertexAdapter { get; }
    public ShaderSpirv PixelTranslation { get; }
    public TranslatedShaderBindings VertexBindings { get; }
    public TranslatedShaderBindings PixelBindings { get; }
    private Task? _disposal;

    private TerrainGbufferProgram(Rid shader, LoadedTerrainGbiMapResources source, bool coarse,
        G2ShaderCombination combination, ShaderSpirv vertex, AdaptedVertexShader adapter,
        ShaderSpirv pixel, TranslatedShaderBindings vertexBindings,
        TranslatedShaderBindings pixelBindings)
        => (ShaderRid, Source, Coarse, Combination, VertexTranslation, VertexAdapter, PixelTranslation,
            VertexBindings, PixelBindings) = (shader, source, coarse, combination, vertex, adapter, pixel,
                vertexBindings, pixelBindings);

    public static async Task<TerrainGbufferProgram> BuildAsync(LoadedTerrainGbiMapResources source,
        bool coarse, Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        G2ShaderCombination combination = coarse ? source.CoarseCombination : source.Combination;
        if (combination.Stages.Any(s => s.StageCode is not (0 or 1) && s.Entry is not null))
            throw new InvalidDataException("Stock terrain adapter requires exactly vertex and pixel stages.");
        G2ShaderCombinationStage vertexStage = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null);
        G2ShaderCombinationStage pixelStage = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null);
        LoadedShaderContainer shaderSource = source.Shader.Shader;
        int vi = vertexStage.Entry!.ProgramIndex, pi = pixelStage.Entry!.ProgramIndex;
        ShaderSpirv vertex = compiler.Compile(shaderSource.Container.GetProgramBytes(shaderSource.Source.Bytes, vi), 1);
        AdaptedVertexShader adapted = VertexShaderAdapter.Adapt(vertex.Bytes, 0, 0, 0);
        ShaderSpirv pixel = compiler.Compile(shaderSource.Container.GetProgramBytes(shaderSource.Source.Bytes, pi));
        TranslatedShaderBindings vertexBindings = TranslatedShaderBindingReader.Read(shaderSource.ReadResourceDefinitions(vi), adapted.Bytes);
        TranslatedShaderBindings pixelBindings = TranslatedShaderBindingReader.Read(shaderSource.ReadResourceDefinitions(pi), pixel.Bytes);
        if (vertexBindings.Bindings.Any(b => b.Device.DescriptorSet != 1) || pixelBindings.Bindings.Any(b => b.Device.DescriptorSet != 0))
            throw new InvalidDataException("Terrain stage descriptor sets are not isolated.");
        foreach (var input in pixelBindings.Interface.StageVariables.Where(v => v.StorageClass == 1 && v.Location is not null))
        {
            var candidates = vertexBindings.Interface.StageVariables.Where(v => v.StorageClass == 3 && v.Location == input.Location).ToArray();
            if (candidates.Length != 1 || !Compatible(candidates[0].Type, input.Type))
                throw new InvalidDataException("Original terrain vertex output does not satisfy the pixel input location/type.");
        }
        var outputs = pixelBindings.Interface.StageVariables.Where(v => v.StorageClass == 3).OrderBy(v => v.Location).ToArray();
        if (outputs.Length != 5 || outputs.Where((v, index) => v.Location != (uint)index ||
                v.Type is not { Kind: "vector", ComponentCount: 4, ComponentType: { Kind: "float", Bits: 32 } }).Any())
            throw new InvalidDataException("Original terrain pixel output differs from the five float4 MRT contract.");
        Rid shader = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var stages = new RDShaderSpirV();
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapted.Bytes);
            stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, pixel.Bytes);
            Rid rid = rd.ShaderCreateFromSpirV(stages, coarse ? "OriginalCoarseTerrainGBuffer" : "OriginalDetailTerrainGBuffer");
            if (!rid.IsValid) throw new InvalidDataException("RenderingDevice rejected the original terrain shader pair.");
            return rid;
        });
        return new(shader, source, coarse, combination, vertex, adapted, pixel, vertexBindings, pixelBindings);
    }

    private static bool Compatible(SpirvInterfaceType output, SpirvInterfaceType input)
    {
        if (output.Kind != input.Kind) return false;
        if (output.Kind == "vector")
            return output.ComponentCount >= input.ComponentCount && Compatible(output.ComponentType!, input.ComponentType!);
        return output.Kind is "float" or "int" && output.Bits == input.Bits && output.Signed == input.Signed;
    }

    public ValueTask DisposeAsync() => new(_disposal ??= TerrainDeviceTexture.OnRenderThreadAsync(rd =>
    { rd.FreeRid(ShaderRid); return true; }));
}
