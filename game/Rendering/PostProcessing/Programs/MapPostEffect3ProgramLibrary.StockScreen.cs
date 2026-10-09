using San14.Data;

/// <summary>Verified native embedded stock VS, distinct from the counted3SPK
/// programs. Bind its exact32B vertex CB and24B stock vertex layout when drawing.
/// This object certifies compilation, not current frontend state or a draw.</summary>
public sealed record MapPostEffect3CompiledStockScreenVertex(
    LoadedMapFinalOutputToneShaderSource EmbeddedSource,
    ShaderSpirv Translation, ShaderSpirv DeviceTranslation,
    TranslatedShaderBindings Bindings, AdaptedVertexShader VertexAdapter);

public sealed partial class MapPostEffect3ProgramLibrary
{
    private readonly LoadedMapFinalOutputToneShaderSource? stockScreenSource;
    private MapPostEffect3CompiledStockScreenVertex? stockScreenVertex;
    /// <summary>The actual original embedded stock executable generation.
    /// The two-argument legacy library retains its existing TC source scope.</summary>
    public string ExecutableLocale=>stockScreenSource?.Locale??"TC";

    /// <summary>Resolve the explicitly supplied native stock program against
    /// the installed PE. The existing two-argument creator stays strict when a
    /// pass has no counted VS; no generic or synthetic stage index is inserted.</summary>
    public static MapPostEffect3ProgramLibrary CreateWithStockScreenVertex(
        MapPostEffect3Container source, Vkd3dShaderCompiler borrowedCompiler,
        LoadedMapFinalOutputToneShaderSource actualEmbeddedStockSource)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(borrowedCompiler);
        ArgumentNullException.ThrowIfNull(actualEmbeddedStockSource);
        return new(source, borrowedCompiler, actualEmbeddedStockSource);
    }

    private bool IsSourceStockScreenPass(int index)
    {
        var state = MapPostEffect3PassStateReader.Read(Source,ExecutableLocale)[index];
        return state.AutomaticExecution == MapPostEffect3AutomaticExecution.StockScreenQuad &&
            state.UsesStockScreenShaderBinding;
    }

    private MapPostEffect3CompiledStockScreenVertex CompileStockScreenVertex()
    {
        if (stockScreenVertex is not null) return stockScreenVertex;
        var source = stockScreenSource ?? throw new NotSupportedException("Stock vertex source has not been supplied.");
        var translated = compiler.Compile(source.Vertex.Bytes.Span);
        var adapter = VertexShaderAdapter.AdaptPositionInput(MoveVertexDescriptorSet(translated.Bytes));
        var device = translated with { Bytes = adapter.Bytes, SpirvSha256 = adapter.AdaptedSha256 };
        var bindings = ReadBindings(DeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(source.Vertex.Bytes.Span),
            device.Bytes, 1);
        if (bindings.Bindings is not [{ DescriptorKind: "UniformBuffer", ConstantBuffer.ByteSize: 32 }])
            throw new InvalidDataException("Actual stock vertex stage requires one exact32B vertex CB.");
        return stockScreenVertex = new(source, translated, device, bindings, adapter);
    }
}
