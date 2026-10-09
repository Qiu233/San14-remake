namespace San14.Data.Formats;

/// <summary>Read the original TaskGraph container without executing its nodes.</summary>
public static class TaskGraphReader
{
    public static CompiledGraphDocument Read(ReadOnlySpan<byte> source) =>
        CompiledGraphReader.Read(source, CompiledGraphKind.TaskGraph);
}
