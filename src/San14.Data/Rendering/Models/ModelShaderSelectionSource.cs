using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ModelShaderDimensionRecord(int Index, int SourceOffset, byte[] RawBytes);
public sealed record ModelShaderDimensionSource(int DimensionIndex, int NativeIndexOffset,
    uint RawCount, int RecordWidth, DxbcRange SourceRange,
    IReadOnlyList<ModelShaderDimensionRecord> Records)
{
    public bool HasCapability => RawCount != 0;
    public bool HasMultipleRows => RawCount > 1;
}

/// <summary>Seven native cached indices and the signed current pass. Constructor
/// values describe initialization; they do not establish that a draw ran.</summary>
public sealed record ModelShaderSelectionCache(IReadOnlyList<uint> Indices, int CurrentPass)
{
    public ModelShaderSelectionSource? ConstructorSource { get; init; }
}
public sealed record ModelShaderSelectionSource(NamedModelShader Provider,
    LoadedShaderContainer Shader, G1ShaderBindingTables Bindings,
    IReadOnlyList<ModelShaderDimensionSource> Dimensions)
{
    /// <summary>For this newly constructed source renderer, guarded wrapper
    /// writes and variant transitions retain zero in the six other indices.
    /// External direct index mutation is outside that transition ownership.</summary>
    public bool CanResolveInitializedOtherDimensions => Provider.Object.ClassId == 0x18EB1E56 &&
        Dimensions.Count == 7 && Dimensions.Where(row => row.DimensionIndex != 2).All(row => row.RawCount <= 1);
}
public sealed record ModelShaderInitializedCombination(ModelShaderSelectionSource Source,
    ModelShaderSelectionCache ConstructorCache, ModelMatrixBindingSource Matrix,
    uint ExplicitPass, IReadOnlyList<uint> Indices, G2ShaderCombination Combination);
public sealed record ModelShaderDimensionTransition(int DimensionIndex, uint PreviousIndex,
    int CandidateIndex, bool MatcherExact, uint NextIndex, bool CommitCalled,
    ModelShaderDimensionRecord? CopiedSourceRow);
public sealed record ModelShaderFeatureTransition(G1ShaderFeatureSelection Matcher,
    ModelShaderDimensionTransition Write);
public sealed record ModelShaderPassTransition(uint RequestedPass, bool StageSucceeded,
    int PreviousCurrentPass, int NextCurrentPass, G2ShaderCombination? Combination);

/// <summary>Original source dimension declarations, explicit constructor indices,
/// guarded native index writes, and the pass writer. Conditions, plugin effects
/// and actual stage preparation success remain explicit caller inputs.</summary>
public static class ModelShaderSelectionSourceReader
{
    private static readonly int[] Widths = [48, 4, 16, 8, 40, 4];

    public static ModelShaderSelectionSource Read(NamedModelShader provider,
        LoadedShaderContainer shader)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(shader);
        if (provider.Object.ClassId != 0x18EB1E56 || provider.ShaderFileHash != shader.Source.FileId ||
            shader.Source.ResourceUri != GameResourceId.Rdb("system", provider.ShaderFileHash).ToString())
            throw new InvalidDataException("Original selection source requires its verified native creator and declared G1S.");
        G1ShaderBindingTables bindings = G1ShaderBindingReader.Read(shader.Source.Bytes);
        ReadOnlySpan<byte> bytes = shader.Source.Bytes;
        var dimensions = new ModelShaderDimensionSource[7];
        long previousEnd = 256;
        for (int dimension = 0; dimension < dimensions.Length; dimension++)
        {
            uint rawCount = bindings.RawDimensionCounts[dimension];
            int width = dimension == 6 ? bindings.Variants.RecordWidth : Widths[dimension];
            uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0x78 + dimension * 4, 4));
            long length = (long)rawCount * width;
            if (rawCount > int.MaxValue || rawOffset < previousEnd || rawOffset > int.MaxValue || length > int.MaxValue ||
                rawOffset + length > (long)bindings.Combinations.SourceRange.Offset)
                throw new InvalidDataException($"Original shader dimension {dimension} exceeds its metadata range.");
            int start = (int)rawOffset;
            // Empty source tables stay empty. The effective count of one used
            // by combination stride arithmetic is not a native matcher row.
            var records = new List<ModelShaderDimensionRecord>((int)rawCount);
            for (int index = 0; index < (int)rawCount; index++)
            {
                int offset = checked(start + index * width);
                records.Add(new(index, offset, bytes.Slice(offset, width).ToArray()));
            }
            dimensions[dimension] = new(dimension, 0x13E0 + dimension * 4, rawCount, width,
                new(start, (int)length), records.AsReadOnly());
            previousEnd = rawOffset + length;
        }
        return new(provider, shader, bindings, Array.AsReadOnly(dimensions));
    }

    public static ModelShaderSelectionCache ReadInitialCache(ModelShaderSelectionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        // TC982518..982539 explicitly zeroes all seven DWORDs and writes -1
        // to current pass+1414, independently of the declared table counts.
        return new(Array.AsReadOnly(new uint[] { 0, 0, 0, 0, 0, 0, 0 }), -1) { ConstructorSource = source };
    }

    /// <summary>Combination for a new source renderer whose index transitions
    /// are owned by the verified guarded wrappers and variant cache reader.
    /// Constructor zero is retained in dimensions with zero or one source row:
    /// a valid candidate can only be zero, and a miss retains the previous index.
    /// Current condition words need not be zero or match exactly. This method
    /// does not project an original current frame or stage preparation success.</summary>
    public static ModelShaderInitializedCombination ResolveSingleOtherDimensionsInitialized(
        ModelShaderSelectionSource source, ModelShaderSelectionCache explicitConstructorCache,
        ModelMatrixBindingSource matrix, uint explicitPass)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(explicitConstructorCache);
        ArgumentNullException.ThrowIfNull(matrix);
        if (!source.CanResolveInitializedOtherDimensions ||
            !ReferenceEquals(explicitConstructorCache.ConstructorSource, source) ||
            explicitConstructorCache.CurrentPass != -1 || explicitConstructorCache.Indices.Count != 7 ||
            explicitConstructorCache.Indices.Any(index => index != 0))
            throw new InvalidDataException("Original initialized combination requires this source's explicit constructor cache and six dimensions with at most one native row.");
        if (matrix.Shader.Source.ResourceUri != source.Shader.Source.ResourceUri ||
            matrix.Shader.Source.DecodedSha256 != source.Shader.Source.DecodedSha256 ||
            !matrix.Shader.Source.Bytes.AsSpan().SequenceEqual(source.Shader.Source.Bytes))
            throw new InvalidDataException("Original initialized combination requires the matrix selection from the same complete shader source.");
        ModelMatrixBindingSource canonical = ModelMatrixBindingSourceReader.Select(source.Shader, matrix.Input);
        if (canonical.Selection.RowIndex != matrix.Selection.RowIndex ||
            canonical.Selection.FoundZeroPenalty != matrix.Selection.FoundZeroPenalty ||
            canonical.Selection.RowIndex < 0 ||
            source.Dimensions[2].RawCount != canonical.Bindings.Rows.Count)
            throw new InvalidDataException("Original initialized combination matrix selection differs from its source matcher result.");
        uint[] indices = explicitConstructorCache.Indices.ToArray();
        indices[2] = checked((uint)canonical.Selection.RowIndex);
        G2ShaderCombination combination = source.Bindings.ResolveCombination(indices, explicitPass);
        return new(source, explicitConstructorCache, canonical, explicitPass, Array.AsReadOnly(indices), combination);
    }

    /// <summary>TC980850 calls the original weighted feature matcher with all
    /// twelve explicit current words, then applies its guarded index writer.
    /// A nearest row can be committed while MatcherExact remains false.</summary>
    public static ModelShaderFeatureTransition SelectFeature(ModelShaderSelectionSource source,
        uint previousIndex, ReadOnlySpan<uint> currentWords)
    {
        ArgumentNullException.ThrowIfNull(source);
        G1ShaderFeatureSelection selected = G1ShaderFeatureMatcher.Select(currentWords,
            source.Bindings.Features.Select(row => row.Feature).ToArray());
        return new(selected, ApplyExplicitMatcherResult(source, 0, previousIndex,
            selected.RowIndex, selected.EncounteredZeroPenalty));
    }

    /// <summary>Project the common original wrapper after an explicitly supplied
    /// native matcher result. This does not calculate an unresolved condition.
    /// Matrix and variant producers have their own source readers.</summary>
    public static ModelShaderDimensionTransition ApplyExplicitMatcherResult(
        ModelShaderSelectionSource source, int dimensionIndex, uint previousIndex,
        int candidateIndex, bool matcherExact)
    {
        ArgumentNullException.ThrowIfNull(source);
        if ((uint)dimensionIndex >= 6)
            throw new ArgumentOutOfRangeException(nameof(dimensionIndex), "Variant selection uses its separate byte cache transition.");
        ModelShaderDimensionSource dimension = source.Dimensions[dimensionIndex];
        bool commit = candidateIndex >= 0 && (uint)candidateIndex < dimension.RawCount &&
            (uint)candidateIndex != previousIndex;
        return new(dimensionIndex, previousIndex, candidateIndex, matcherExact,
            commit ? (uint)candidateIndex : previousIndex, commit,
            commit ? dimension.Records[candidateIndex] : null);
    }

    /// <summary>Source pass ordinals used by the original zero-based loop after
    /// begin succeeds. Enumerating them does not execute begin or a pass.</summary>
    public static IReadOnlyList<uint> ReadDeclaredPassSequence(ModelShaderSelectionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Bindings.PassCount > int.MaxValue)
            throw new InvalidDataException("Original source pass count exceeds supported bounds.");
        return Array.AsReadOnly(Enumerable.Range(0, (int)source.Bindings.PassCount).Select(i => (uint)i).ToArray());
    }

    /// <summary>TC97EAA0 explicitly stages literal pass zero. This projection
    /// does not write J+1414; only the separate successful 97E920 writer does.</summary>
    public static G2ShaderCombination ResolveBeginPassZero(ModelShaderSelectionSource source,
        ReadOnlySpan<uint> explicitIndices)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Bindings.ResolveCombination(explicitIndices, 0);
    }

    /// <summary>TC97E920 returns before writing current pass if 980980 fails.
    /// Success depends on current dirty state and 985020 stage preparation;
    /// the caller supplies that outcome, never inferred from passCount.</summary>
    public static ModelShaderPassTransition ApplyExplicitPassStageResult(
        ModelShaderSelectionSource source, ReadOnlySpan<uint> explicitIndices,
        int previousCurrentPass, uint requestedPass, bool stageSucceeded)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (requestedPass >= source.Bindings.PassCount)
            throw new InvalidDataException("Original requested pass exceeds its source declaration.");
        if (!stageSucceeded)
            return new(requestedPass, false, previousCurrentPass, previousCurrentPass, null);
        G2ShaderCombination combination = source.Bindings.ResolveCombination(explicitIndices, requestedPass);
        return new(requestedPass, true, previousCurrentPass, checked((int)requestedPass), combination);
    }
}
