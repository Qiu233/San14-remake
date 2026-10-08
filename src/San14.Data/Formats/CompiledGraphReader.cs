using System.Buffers.Binary;

namespace San14.Data.Formats;

public enum OriginalCompiledGraphKind { TaskGraph, RenderGraph }

public sealed record CompiledGraphNode(int Index, int SourceOffset, int DeclaredBytes,
    TaskGraphCallMetadata Call, byte[] AlignmentPadding);

public sealed record CompiledGraphDocument(OriginalCompiledGraphKind GraphKind,
    uint HeaderSize, uint Kind, uint DeclaredNodeCount,
    uint DependencyFileHash, uint EntryIndex, uint DeclaredTotalBytes,
    IReadOnlyList<CompiledGraphNode> Nodes, byte[] RawHeader, byte[] RawTrailingBytes)
{
    public bool HasInitializableEntry => EntryIndex < Nodes.Count && Nodes[(int)EntryIndex].Call.FactoryClassId == 0;
}

/// <summary>Shared original TGK/RGK record layout, retaining source identities, unknown fields and tails.</summary>
public static class CompiledGraphReader
{
    public static CompiledGraphDocument Read(ReadOnlySpan<byte> source, OriginalCompiledGraphKind graphKind)
    {
        ReadOnlySpan<byte> magic = graphKind switch
        {
            OriginalCompiledGraphKind.TaskGraph => "_TGK0000"u8,
            OriginalCompiledGraphKind.RenderGraph => "_RGK0000"u8,
            _ => throw new ArgumentOutOfRangeException(nameof(graphKind))
        };
        if (source.Length < 32 || !source[..8].SequenceEqual(magic) || U32(source, 12) != 10)
            throw new InvalidDataException($"Unsupported original {graphKind} header or kind.");
        uint headerSize = U32(source, 8), count = U32(source, 16), total = U32(source, 28);
        if (headerSize < 32 || headerSize > source.Length || total != source.Length ||
            count > (source.Length - headerSize) / 32)
            throw new InvalidDataException($"Original {graphKind} header size, node count or total bytes exceed the source.");
        var nodes = new List<CompiledGraphNode>((int)count);
        int cursor = (int)headerSize;
        for (int i = 0; i < (int)count; i++)
        {
            if (cursor > source.Length - 32 || !source.Slice(cursor, 8).SequenceEqual("INGK0000"u8))
                throw new InvalidDataException($"Original {graphKind} node {i} has a truncated or unsupported INGK header.");
            uint bytes = U32(source, cursor + 8);
            long aligned = ((long)bytes + 3) & ~3L;
            if (bytes < 32 || aligned > source.Length - cursor)
                throw new InvalidDataException($"Original {graphKind} node {i} has invalid record or alignment bounds.");
            TaskGraphCallMetadata call;
            try { call = TaskGraphCallReader.Read(source.Slice(cursor, (int)bytes), cursor); }
            catch (InvalidDataException error)
            { throw new InvalidDataException($"Original {graphKind} node {i}: {error.Message}", error); }
            nodes.Add(new(i, cursor, (int)bytes, call, source.Slice(cursor + (int)bytes, (int)aligned - (int)bytes).ToArray()));
            cursor += (int)aligned;
        }
        // Entry selection can fail without making native file loading fail.
        // Keep FFFFFFFF/out-of-range/non-entry factory cases as source state.
        foreach (CompiledGraphNode node in nodes)
            foreach (uint target in node.Call.ConnectedNodeIndices)
                if (target >= count)
                    throw new InvalidDataException($"Original {graphKind} node {node.Index} connects to missing node {target}.");
        return new(graphKind, headerSize, 10, count, U32(source, 20), U32(source, 24), total,
            nodes.AsReadOnly(), source[..(int)headerSize].ToArray(), source[cursor..].ToArray());
    }
    private static uint U32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
}
