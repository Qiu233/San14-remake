using Godot;
using San14.Data;
using System.Security.Cryptography;

public sealed partial class OriginalDeferredPixelProgram
{
    public OriginalDeferredEarlyCausticsNullSpirv? EarlyCausticsNullSpecialization { get; private init; }

    public static async Task<OriginalDeferredPixelProgram> BuildAsync(
        OriginalDeferredEarlyF103SelectedSource selected, Vkd3dShaderCompiler compiler,
        OriginalDeferredEarlyCausticsSourceNull sourceNull)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(sourceNull); selected.Validate(); sourceNull.Validate(selected);
        var source = selected.SourcePlan.Shader;
        byte[] vertex = source.Container.GetProgramBytes(source.Source.Bytes, selected.VertexProgramIndex).ToArray();
        if (Convert.ToHexString(SHA256.HashData(vertex)) != "C51D71A59DC89E4B23B68395180A41473B35914AD33BCE80C92AF636A379757D")
            throw new InvalidDataException("Early Caustics program requires the original audited fullscreen vertex program.");
        var original = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, selected.PixelProgramIndex));
        var sourceBindings = OriginalTranslatedShaderBindingReader.Read(selected.PixelDefinitions, original.Bytes);
        var adapted = OriginalDeferredEarlyCausticsNullAdapter.Adapt(original, sourceBindings, selected, sourceNull);
        var translated = original with { Bytes = adapted.Bytes, SpirvSha256 = adapted.Sha256 };
        var bindings = OriginalDeferredEarlyCausticsNullAdapter.ReadDeviceBindings(sourceBindings, adapted);
        Rid shader = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            using var vertexSource = new RDShaderSource { SourceVertex = OriginalDeferredFullscreenShader.VertexSource };
            using var adapter = rd.ShaderCompileSpirVFromSource(vertexSource);
            string error = adapter.GetStageCompileError(RenderingDevice.ShaderStage.Vertex);
            if (error.Length != 0) throw new InvalidDataException("Early fullscreen adapter compilation failed: " + error);
            using var combined = new RDShaderSpirV();
            combined.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.GetStageBytecode(RenderingDevice.ShaderStage.Vertex));
            combined.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, translated.Bytes);
            Rid created = rd.ShaderCreateFromSpirV(combined, "OriginalEarly95SourceCausticsNull");
            if (!created.IsValid) throw new InvalidDataException("Device rejected the source early Caustics NULL program.");
            return created;
        });
        return new OriginalDeferredPixelProgram(shader, source, selected.Combination, translated, bindings, original, sourceBindings, null)
            { EarlyCausticsNullSpecialization = adapted };
    }
}
