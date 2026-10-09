using San14.Data.Formats;

namespace San14.Data;

public enum ModelShaderVariantTransitionKind { CachedAcceptance, SourceRowMatch, MissRevert }
public sealed record ModelShaderVariantCache(uint CommittedIndex,
    byte[] CommittedBytes, byte[] RequestedBytes);
public sealed record ModelShaderVariantTransition(ModelShaderVariantTransitionKind Kind,
    bool Success, ModelShaderVariantCache Next, bool RequiresCommitCallback);
public sealed record ModelShaderVariantSource(NamedModelShader Provider,
    LoadedShaderContainer Shader, G1ShaderVariantTable Table);

/// <summary>Original variant byte-cache transition for the verified native shader
/// creator. Actual requested bytes remain caller input; constructor state does
/// not establish current frame activity. Callback side effects are reported.</summary>
public static class ModelShaderVariantSourceReader
{
    public static ModelShaderVariantSource Read(NamedModelShader provider,
        LoadedShaderContainer shader)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(shader);
        if (provider.Object.ClassId != 0x18EB1E56 || provider.ShaderFileHash != shader.Source.FileId ||
            shader.Source.ResourceUri != GameResourceId.Rdb("system", provider.ShaderFileHash).ToString())
            throw new InvalidDataException("Original variant source requires its verified native creator and exact declared G1S.");
        G1ShaderBindingTables binding = G1ShaderBindingReader.Read(shader.Source.Bytes);
        if (binding.Variants.Records.Count == 0)
            throw new InvalidDataException("Original shader has no variant capability; a constructor byte cache is not synthesized.");
        return new(provider, shader, binding.Variants);
    }

    public static ModelShaderVariantCache ReadInitialCache(ModelShaderVariantSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateTable(source.Table);
        byte[] originalFirstRow = source.Table.Records[0].RawBytes;
        return new(0, originalFirstRow.ToArray(), originalFirstRow.ToArray());
    }

    public static ModelShaderVariantTransition SelectRequested(ModelShaderVariantSource source,
        uint explicitCommittedIndex, ReadOnlySpan<byte> explicitCommittedBytes, ReadOnlySpan<byte> explicitRequestedBytes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateTable(source.Table);
        if (explicitCommittedBytes.Length != source.Table.RecordWidth || explicitRequestedBytes.Length != source.Table.RecordWidth)
            throw new InvalidDataException("Original variant transition requires both complete explicit cache byte ranges.");

        // 98172E/981747 accepts equal caches when the cached index is in range;
        // it does not recheck those bytes against the source row.
        if (explicitCommittedIndex < source.Table.Records.Count && explicitCommittedBytes.SequenceEqual(explicitRequestedBytes))
            return Result(ModelShaderVariantTransitionKind.CachedAcceptance, true, explicitCommittedIndex,
                explicitCommittedBytes, explicitRequestedBytes, false);
        foreach (G1ShaderVariantRecord row in source.Table.Records)
        {
            if (!explicitRequestedBytes.SequenceEqual(row.RawBytes)) continue;
            uint chosen = checked((uint)row.Index);
            bool changed = chosen != explicitCommittedIndex;
            // 9817FF skips the commit callback for the same index. Stale
            // committed bytes must therefore remain stale in this branch.
            return changed ? Result(ModelShaderVariantTransitionKind.SourceRowMatch, true, chosen,
                row.RawBytes, row.RawBytes, true) :
                Result(ModelShaderVariantTransitionKind.SourceRowMatch, true, chosen,
                    explicitCommittedBytes, explicitRequestedBytes, false);
        }
        // The verified base/native virtual+180 hook is an exact no-op. A miss
        // copies committed -> requested, preserves the original uint32 index
        // (even FFFFFFFF), and returns false.
        return Result(ModelShaderVariantTransitionKind.MissRevert, false, explicitCommittedIndex,
            explicitCommittedBytes, explicitCommittedBytes, false);
    }

    private static ModelShaderVariantTransition Result(ModelShaderVariantTransitionKind kind,
        bool success, uint index, ReadOnlySpan<byte> committed, ReadOnlySpan<byte> requested, bool callback) =>
        new(kind, success, new(index, committed.ToArray(), requested.ToArray()), callback);

    private static void ValidateTable(G1ShaderVariantTable table)
    {
        if (table.RecordWidth <= 0 || table.Records.Count == 0 ||
            table.Records.Where((row, index) => row.Index != index || row.RawBytes.Length != table.RecordWidth).Any())
            throw new InvalidDataException("Original variant source row ordinals or complete byte widths are invalid.");
    }
}
