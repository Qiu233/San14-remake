namespace San14.Data.Formats;

/// <summary>Read the original RenderGraph container using its proven shared call layout.</summary>
public static class RenderGraphReader
{
    public static CompiledGraphDocument Read(ReadOnlySpan<byte> source) =>
        CompiledGraphReader.Read(source, OriginalCompiledGraphKind.RenderGraph);
}
