using San14.Data.Formats;

namespace San14.Data;

/// <summary>Selected ordinary terrain stages and their imported constant layouts.
/// Program indices are resource declarations, not capabilities in themselves.</summary>
public sealed class TerrainShaderStages
{
    private TerrainShaderStages(LoadedShaderContainer source, G2ShaderCombination combination,
        int vertex, int pixel, TerrainGbufferConstantLayout vertexConstants, TerrainGbufferConstantLayout pixelConstants)
        => (Source, Combination, VertexProgramIndex, PixelProgramIndex, VertexConstants, PixelConstants) =
            (source, combination, vertex, pixel, vertexConstants, pixelConstants);
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public int VertexProgramIndex { get; }
    public int PixelProgramIndex { get; }
    public TerrainGbufferConstantLayout VertexConstants { get; }
    public TerrainGbufferConstantLayout PixelConstants { get; }

    internal static TerrainShaderStages Import(LoadedShaderContainer source, G2ShaderCombination combination,
        bool coarse, bool extraAlbedo)
    {
        var vertices = combination.Stages.Where(s => s.StageCode == 1 && s.Entry is not null).ToArray();
        var pixels = combination.Stages.Where(s => s.StageCode == 0 && s.Entry is not null).ToArray();
        if (vertices.Length != 1 || pixels.Length != 1 ||
            combination.Stages.Any(s => s.StageCode is not (0 or 1) && s.Entry is not null))
            throw new NotSupportedException("Ordinary terrain requires one vertex and one pixel stage.");
        int vertex = vertices[0].Entry!.ProgramIndex, pixel = pixels[0].Entry!.ProgramIndex;
        var vertexDefinitions = source.ReadResourceDefinitions(vertex);
        var pixelDefinitions = source.ReadResourceDefinitions(pixel);
        RequireTextures(vertexDefinitions, true, coarse, extraAlbedo);
        RequireTextures(pixelDefinitions, false, coarse, extraAlbedo);
        RequireStageSignatures(source.ReadSignatures(vertex), source.ReadSignatures(pixel));
        var outputs = source.ReadSignatures(pixel).Parts.Where(p => p.Kind == DxbcSignatureKind.Output)
            .SelectMany(p => p.Parameters).OrderBy(p => p.Register).ToArray();
        if (outputs.Length != 5 || outputs.Where((p, index) => p.Register != index ||
            !p.SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase) || p.ComponentType != 3 || p.Mask != 15).Any())
            throw new NotSupportedException("Ordinary terrain requires five float4 color outputs.");
        var vertexConstants = TerrainGbufferConstantLayout.Import(vertexDefinitions, true);
        var pixelConstants = TerrainGbufferConstantLayout.Import(pixelDefinitions, false);
        return new(source, combination, vertex, pixel, vertexConstants, pixelConstants);
    }

    private static void RequireTextures(DxbcResourceDefinitions definitions, bool vertex, bool coarse, bool extraAlbedo)
    {
        uint[] slots = vertex ? [8u] : Enumerable.Range(3, coarse ? 4 : 7).Select(i => (uint)i)
            .Concat(extraAlbedo ? [10u] : Array.Empty<uint>()).ToArray();
        foreach (uint kind in new[] { 2u, 3u })
        {
            var bindings = definitions.Bindings.Where(b => b.InputType == kind).OrderBy(b => b.BindPoint).ToArray();
            if (bindings.Select(b => b.BindPoint).Distinct().Count() != bindings.Length ||
                bindings.Any(b => !slots.Contains(b.BindPoint) || b.BindCount != 1 ||
                    kind == 2 && (b.ReturnType != 5 || b.Dimension != (!vertex && b.BindPoint is >= 7 and <= 9 ? 5u : 4u))))
                throw new NotSupportedException("Terrain textures and samplers differ from the supplied ordinary input slots.");
        }
        if (definitions.Bindings.Any(b => b.InputType is not (0 or 2 or 3)))
            throw new NotSupportedException("Terrain input has an unsupported resource binding kind.");
    }

    private static void RequireStageSignatures(DxbcProgramSignatures vertex, DxbcProgramSignatures pixel)
    {
        var vertexInputs = vertex.Parts.Where(p => p.Kind == DxbcSignatureKind.Input).SelectMany(p => p.Parameters).ToArray();
        var attributes = vertexInputs.Where(p => p.RawSystemValue == 0).ToArray();
        byte[] masks = [15, 7, 7, 15, 15];
        if (attributes.Length != masks.Length || attributes.Where((p, i) => p.Register != i + 1 ||
            p.ComponentType != 3 || p.Mask == 0 || (p.Mask & ~masks[i]) != 0).Any())
            throw new NotSupportedException("Terrain vertex signature differs from the supplied position and patch streams.");
        var system = vertexInputs.Where(p => p.RawSystemValue != 0).ToArray();
        if (system.Length != 1 || system[0].RawSystemValue != 6 || system[0].ComponentType != 1 || system[0].Mask != 1)
            throw new NotSupportedException("Terrain vertex stage requires the supplied vertex identifier system input.");
        var outputs = vertex.Parts.Where(p => p.Kind == DxbcSignatureKind.Output).SelectMany(p => p.Parameters).ToArray();
        foreach (var input in pixel.Parts.Where(p => p.Kind == DxbcSignatureKind.Input).SelectMany(p => p.Parameters))
        {
            var matches = outputs.Where(p => p.SemanticName.Text.Equals(input.SemanticName.Text, StringComparison.OrdinalIgnoreCase) &&
                p.SemanticIndex == input.SemanticIndex).ToArray();
            if (matches.Length != 1 || matches[0].Register != input.Register || matches[0].RawSystemValue != input.RawSystemValue ||
                matches[0].ComponentType != input.ComponentType || (matches[0].Mask & input.Mask) != input.Mask)
                throw new NotSupportedException("Terrain vertex output does not supply the selected pixel input semantics and components.");
        }
    }
}
