using San14.Data.Formats;

namespace San14.Data;

/// <summary>Complete CPU terrain program preparation. Device shader creation
/// and retirement belong to the renderer.</summary>
public sealed record TerrainShaderProgramPreparation(TerrainShaderStages Stages,
    ShaderSpirv VertexTranslation, AdaptedVertexShader VertexAdapter, ShaderSpirv PixelTranslation,
    TranslatedShaderBindings VertexBindings, TranslatedShaderBindings PixelBindings);

public static class TerrainShaderPreparation
{
    public static TerrainShaderProgramPreparation Prepare(TerrainShaderStages stages, Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(stages); ArgumentNullException.ThrowIfNull(compiler);
        var source = stages.Source;
        var vertex = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, stages.VertexProgramIndex), 1);
        var adapter = VertexShaderAdapter.Adapt(vertex.Bytes, 0, 0, 0);
        var pixel = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, stages.PixelProgramIndex));
        var vb = TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(stages.VertexProgramIndex), adapter.Bytes);
        var pb = TranslatedShaderBindingReader.Read(source.ReadResourceDefinitions(stages.PixelProgramIndex), pixel.Bytes);
        if (vb.Bindings.Any(b => b.Device.DescriptorSet != 1) || pb.Bindings.Any(b => b.Device.DescriptorSet != 0))
            throw new InvalidDataException("Terrain stage descriptor sets are not isolated.");
        // The renderer supplies one position stream and the complete ordinary
        // patch instance stream, at these five actual device input locations.
        uint[] widths = [4, 3, 3, 4, 4];
        var attributes = vb.Interface.StageVariables.Where(v => v.StorageClass == 1 && v.Location is not null)
            .OrderBy(v => v.Location).ToArray();
        if (attributes.Length != widths.Length || attributes.Where((v, i) => v.Location != i + 1 ||
            v.Type is not { Kind: "vector", ComponentType: { Kind: "float", Bits: 32 } } ||
            v.Type.ComponentCount is < 1 || v.Type.ComponentCount > widths[i]).Any())
            throw new NotSupportedException("Terrain vertex inputs differ from the supplied position and patch streams.");
        foreach (var input in pb.Interface.StageVariables.Where(v => v.StorageClass == 1 && v.Location is not null))
        {
            var candidates = vb.Interface.StageVariables.Where(v => v.StorageClass == 3 && v.Location == input.Location).ToArray();
            if (candidates.Length != 1 || !Compatible(candidates[0].Type, input.Type))
                throw new InvalidDataException("Terrain vertex output does not satisfy the pixel input location/type.");
        }
        var outputs = pb.Interface.StageVariables.Where(v => v.StorageClass == 3).OrderBy(v => v.Location).ToArray();
        if (outputs.Length != 5 || outputs.Where((v, i) => v.Location != i ||
            v.Type is not { Kind: "vector", ComponentCount: 4, ComponentType: { Kind: "float", Bits: 32 } }).Any())
            throw new InvalidDataException("Terrain pixel output differs from the five float4 MRT contract.");
        return new(stages, vertex, adapter, pixel, vb, pb);
    }

    private static bool Compatible(SpirvInterfaceType output, SpirvInterfaceType input)
    {
        if (output.Kind != input.Kind) return false;
        if (output.Kind == "vector")
            return output.ComponentCount >= input.ComponentCount && Compatible(output.ComponentType!, input.ComponentType!);
        return output.Kind is "float" or "int" && output.Bits == input.Bits && output.Signed == input.Signed;
    }
}
