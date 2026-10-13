using San14.Data.Formats;

namespace San14.Data;

/// <summary>Prepared matrix selection input. Packing and derived words are
/// computed together once; render consumers borrow this immutable result.</summary>
public sealed class ModelMatrixBindingInput
{
    private readonly uint[] words;
    internal ModelMatrixBindingInput(G1DrawRecord draw, uint preparedWrapperCount,
        uint? paletteFirstWord, G1DrawGroup? unboundGroup)
    {
        Draw = draw;
        PreparedWrapperCount = preparedWrapperCount;
        UnboundGroupSource = unboundGroup;
        PaletteCount = (draw.Flags & 2) != 0 ? null : paletteFirstWord ??
            throw new InvalidDataException("Matrix input requires its referenced joint palette count.");
        // Preserve the resource format's OR/shift packing. Wrapper counts are
        // not masked to twelve bits; their high bits can affect the palette.
        PackedType17 = unchecked(((((ulong)(PaletteCount ?? 0) << 12) | preparedWrapperCount) << 4 |
            draw.UnknownValue16) << 32);
        words = [(uint)PackedType17 & 0xE0, (uint)(PackedType17 >> 32) & 0xF,
            (uint)(PackedType17 >> 48), (uint)(PackedType17 >> 36) & 0xFFF];
        CurrentWords = Array.AsReadOnly(words);
    }
    public G1DrawRecord Draw { get; }
    public uint PreparedWrapperCount { get; }
    public uint? PaletteCount { get; }
    public ulong PackedType17 { get; }
    public IReadOnlyList<uint> CurrentWords { get; }
    internal ReadOnlySpan<uint> Words => words;
    /// <summary>Source group whose original mode excludes a named matrix wrapper.
    /// Null means the wrapper count was supplied explicitly by the caller.</summary>
    public G1DrawGroup? UnboundGroupSource { get; }
}
public sealed class ModelMatrixBindingSource
{
    internal ModelMatrixBindingSource(LoadedShaderContainer shader, ModelMatrixBindingInput input,
        G1ShaderMatrixBindings bindings, G1ShaderMatrixBindingSelection selection)
        => (Shader, Input, Bindings, Selection) = (shader, input, bindings, selection);
    public LoadedShaderContainer Shader { get; }
    public ModelMatrixBindingInput Input { get; }
    public G1ShaderMatrixBindings Bindings { get; }
    public G1ShaderMatrixBindingSelection Selection { get; }
    /// <summary>Inserts renderer+13E8 into the third dimension of the original
    /// combination arithmetic. All other six dimensions and the pass stay explicit.</summary>
    public G2ShaderCombination ResolveCombination(ReadOnlySpan<uint> otherDimensionIndices, uint passIndex)
    {
        if (otherDimensionIndices.Length != 6 || Selection.RowIndex < 0)
            throw new InvalidDataException("Original matrix selection requires six other dimensions and a selected matrix row.");
        G1ShaderBindingTables tables = Shader.ReadG1Bindings();
        Span<uint> indices = stackalloc uint[7];
        for (int source = 0, target = 0; target < indices.Length; target++)
            indices[target] = target == 2 ? checked((uint)Selection.RowIndex) : otherDimensionIndices[source++];
        return tables.ResolveCombination(indices, passIndex);
    }
}

/// <summary>Original F5AD86 type17 packing and D28161/980AD0 input preparation.
/// The prepared primitive+50 wrapper count is supplied explicitly; it is not a
/// vertex count, a shader index, or an inferred number of matrices.</summary>
public static class ModelMatrixBindingSourceReader
{
    /// <summary>Source-backed zero wrapper count for the ordinary ModelData group
    /// branch. Native group mode 1 constructs a named matrix object and is rejected
    /// until that separate object source is supplied.</summary>
    public static ModelMatrixBindingInput? PrepareUnboundGroup(LoadedModelShaderBindings model,
        int drawIndex)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.ModelObject.Kind != DokObjectKind.Inline || model.ModelObject.ClassId != ModelShaderResources.ModelClass)
            throw new InvalidDataException("Original unbound matrix input requires a verified inline ModelData declaration.");
        G1ModelShaderIdentifiers identifiers = G1ModelShaderIdentifierReader.Read(model.Model.Geometry);
        G1DrawShaderIdentifier source = identifiers.Draws.SingleOrDefault(row => row.Draw.Index == drawIndex) ??
            throw new InvalidDataException($"Original model has no draw {drawIndex} for its matrix input.");
        if ((ushort)source.Group.Flags == 1)
            throw new InvalidDataException("Original group mode 1 requires its named matrix wrapper source; a zero count is not assumed.");
        return Prepare(source.Draw, 0, null, source.Group);
    }

    public static ModelMatrixBindingInput? Prepare(G1DrawRecord draw,
        uint preparedWrapperCount, uint? paletteFirstWord)
        => Prepare(draw, preparedWrapperCount, paletteFirstWord, null);

    private static ModelMatrixBindingInput? Prepare(G1DrawRecord draw,
        uint preparedWrapperCount, uint? paletteFirstWord, G1DrawGroup? unboundGroup)
    {
        ArgumentNullException.ThrowIfNull(draw);
        // The original signed raw draw+8 branch skips type17 creation.
        if (unchecked((int)draw.UnknownValue8) < 0) return null;
        return new(draw, preparedWrapperCount, paletteFirstWord, unboundGroup);
    }

    public static ModelMatrixBindingSource Select(LoadedShaderContainer shader,
        ModelMatrixBindingInput input)
    {
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(input);
        G1ShaderMatrixBindings bindings = shader.ReadMatrixBindings();
        G1ShaderMatrixBindingSelection selected = G1ShaderMatrixBindingMatcher.Select(
            input.Words, bindings);
        return new(shader, input, bindings, selected);
    }
}
