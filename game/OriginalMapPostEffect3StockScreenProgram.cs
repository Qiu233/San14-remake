using System.Security.Cryptography;
using San14.Data;

/// <summary>Verified native embedded stock VS, distinct from the counted3SPK
/// programs. Bind its exact32B vertex CB and24B stock vertex layout when drawing.
/// This object certifies compilation, not current frontend state or a draw.</summary>
public sealed record OriginalMapPostEffect3CompiledStockScreenVertex(
    LoadedOriginalMapFinalOutputToneShaderSource EmbeddedSource,
    OriginalShaderSpirv Translation, OriginalShaderSpirv DeviceTranslation,
    OriginalTranslatedShaderBindings Bindings, OriginalAdaptedVertexShader VertexAdapter);

public sealed partial class OriginalMapPostEffect3ProgramLibrary
{
    private readonly LoadedOriginalMapFinalOutputToneShaderSource? stockScreenSource;
    private OriginalMapPostEffect3CompiledStockScreenVertex? stockScreenVertex;
    /// <summary>The actual original embedded stock executable generation.
    /// The two-argument legacy library retains its existing TC source scope.</summary>
    public string ExecutableLocale=>stockScreenSource?.Locale??"TC";

    /// <summary>Resolve the explicitly supplied native stock program against
    /// the installed PE. The existing two-argument creator stays strict when a
    /// pass has no counted VS; no generic or synthetic stage index is inserted.</summary>
    public static OriginalMapPostEffect3ProgramLibrary CreateWithStockScreenVertex(
        OriginalMapPostEffect3Container source, Vkd3dShaderCompiler borrowedCompiler,
        LoadedOriginalMapFinalOutputToneShaderSource actualEmbeddedStockSource)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(borrowedCompiler);
        ArgumentNullException.ThrowIfNull(actualEmbeddedStockSource);
        var canonical = OriginalMapFinalOutputToneShaderSource.ReadCurrent(
            Path.GetDirectoryName(actualEmbeddedStockSource.PhysicalPath)!, actualEmbeddedStockSource.Locale);
        if (canonical.PhysicalPath != actualEmbeddedStockSource.PhysicalPath ||
            canonical.ExecutableSha256 != actualEmbeddedStockSource.ExecutableSha256 ||
            canonical.SourceResourceUri != actualEmbeddedStockSource.SourceResourceUri ||
            canonical.Vertex.Rva != actualEmbeddedStockSource.Vertex.Rva ||
            canonical.Vertex.Sha256 != actualEmbeddedStockSource.Vertex.Sha256 ||
            !canonical.Vertex.Bytes.Span.SequenceEqual(actualEmbeddedStockSource.Vertex.Bytes.Span))
            throw new InvalidDataException("3SPK stock vertex source differs from the verified original embedded program.");
        var owned = OriginalMapPostEffect3ContainerReader.Read(source.Bytes.Span);
        if (owned.Sha256 != source.Sha256)
            throw new InvalidDataException("3SPK container differs from its explicit source identity.");
        return new(owned, borrowedCompiler, canonical);
    }

    private bool IsSourceStockScreenPass(int index)
    {
        var state = OriginalMapPostEffect3PassStateReader.Read(Source,ExecutableLocale)[index];
        return state.AutomaticExecution == OriginalMapPostEffect3AutomaticExecution.StockScreenQuad &&
            state.UsesStockScreenShaderBinding;
    }

    private OriginalMapPostEffect3CompiledStockScreenVertex CompileStockScreenVertex()
    {
        if (stockScreenVertex is not null) return stockScreenVertex;
        var source = stockScreenSource ?? throw new NotSupportedException("Stock vertex source has not been supplied.");
        if (source.Vertex.Sha256 != OriginalMapFinalOutputToneShaderSource.VertexSha256 ||
            Convert.ToHexString(SHA256.HashData(source.Vertex.Bytes.Span)) != source.Vertex.Sha256)
            throw new InvalidDataException("Owned stock vertex source changed before compilation.");
        var translated = compiler.Compile(source.Vertex.Bytes.Span);
        if (translated.SourceProgramSha256 != source.Vertex.Sha256)
            throw new InvalidDataException("Stock vertex compiler input differs from the original PE.");
        var adapter = OriginalVertexShaderAdapter.AdaptPositionInput(MoveVertexDescriptorSet(translated.Bytes));
        var device = translated with { Bytes = adapter.Bytes, SpirvSha256 = adapter.AdaptedSha256 };
        var bindings = ReadBindings(OriginalDeferredAuxiliaryDepthCopySource.ReadVertexDefinitions(source.Vertex.Bytes.Span),
            device.Bytes, 1);
        if (bindings.Bindings is not [{ DescriptorKind: "UniformBuffer", ConstantBuffer.ByteSize: 32 }])
            throw new InvalidDataException("Actual stock vertex stage requires one exact32B vertex CB.");
        return stockScreenVertex = new(source, translated, device, bindings, adapter);
    }
}
