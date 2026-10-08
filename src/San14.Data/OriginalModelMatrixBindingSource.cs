using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalModelMatrixBindingInput(G1DrawRecord Draw,
    uint PreparedWrapperCount, uint? PaletteCount, ulong PackedType17,
    IReadOnlyList<uint> CurrentWords)
{
    /// <summary>Source group whose original mode excludes a named matrix wrapper.
    /// Null means the wrapper count was supplied explicitly by the caller.</summary>
    public G1DrawGroup? UnboundGroupSource { get; init; }
}
public sealed record OriginalModelMatrixBindingSource(LoadedShaderContainer Shader,
    OriginalModelMatrixBindingInput Input, G1ShaderMatrixBindings Bindings,
    G1ShaderMatrixBindingSelection Selection)
{
    /// <summary>Inserts renderer+13E8 into the third dimension of the original
    /// combination arithmetic. All other six dimensions and the pass stay explicit.</summary>
    public G2ShaderCombination ResolveCombination(ReadOnlySpan<uint> otherDimensionIndices, uint passIndex)
    {
        if (otherDimensionIndices.Length != 6 || Selection.RowIndex < 0)
            throw new InvalidDataException("Original matrix selection requires six other dimensions and a selected matrix row.");
        G1ShaderBindingTables tables = G1ShaderBindingReader.Read(Shader.Source.Bytes);
        if (tables.RawDimensionCounts[2] != Bindings.Rows.Count)
            throw new InvalidDataException("Original matrix row count differs from the third source combination dimension.");
        Span<uint> indices = stackalloc uint[7];
        for (int source = 0, target = 0; target < indices.Length; target++)
            indices[target] = target == 2 ? checked((uint)Selection.RowIndex) : otherDimensionIndices[source++];
        return tables.ResolveCombination(indices, passIndex);
    }
}

/// <summary>Original F5AD86 type17 packing and D28161/980AD0 input preparation.
/// The prepared primitive+50 wrapper count is supplied explicitly; it is not a
/// vertex count, a shader index, or an inferred number of matrices.</summary>
public static class OriginalModelMatrixBindingSourceReader
{
    /// <summary>Source-backed zero wrapper count for the ordinary ModelData group
    /// branch. Native group mode 1 constructs a named matrix object and is rejected
    /// until that separate object source is supplied.</summary>
    public static OriginalModelMatrixBindingInput? PrepareUnboundGroup(LoadedModelShaderBindings model,
        int drawIndex)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.ModelObject.Kind != DokObjectKind.Inline || model.ModelObject.ClassId != OriginalModelShaderResources.ModelClass)
            throw new InvalidDataException("Original unbound matrix input requires a verified inline ModelData declaration.");
        G1ModelShaderIdentifiers identifiers = G1ModelShaderIdentifierReader.Read(model.Model.Geometry);
        G1DrawShaderIdentifier source = identifiers.Draws.SingleOrDefault(row => row.Draw.Index == drawIndex) ??
            throw new InvalidDataException($"Original model has no draw {drawIndex} for its matrix input.");
        if ((ushort)source.Group.Flags == 1)
            throw new InvalidDataException("Original group mode 1 requires its named matrix wrapper source; a zero count is not assumed.");
        OriginalModelMatrixBindingInput? input = Prepare(source.Draw, 0, null);
        return input is null ? null : input with { UnboundGroupSource = source.Group };
    }

    public static OriginalModelMatrixBindingInput? Prepare(G1DrawRecord draw,
        uint preparedWrapperCount, uint? paletteFirstWord)
    {
        ArgumentNullException.ThrowIfNull(draw);
        // The original signed raw draw+8 branch skips type17 creation.
        if (unchecked((int)draw.UnknownValue8) < 0) return null;
        uint paletteCount;
        if ((draw.Flags & 2) != 0) paletteCount = 0;
        else paletteCount = paletteFirstWord ?? throw new InvalidDataException(
            "Original type17 input requires its referenced 10006 palette first word.");
        // These are the original OR/shift operations. In particular the wrapper
        // count is not masked to twelve bits before packing; high bits can spill.
        ulong packed = unchecked(((((ulong)paletteCount << 12) | preparedWrapperCount) << 4 |
            draw.UnknownValue16) << 32);
        var words = new uint[]
        {
            (uint)packed & 0xE0, (uint)(packed >> 32) & 0xF,
            (uint)(packed >> 48), (uint)(packed >> 36) & 0xFFF
        };
        return new(draw, preparedWrapperCount, (draw.Flags & 2) != 0 ? null : paletteCount,
            packed, Array.AsReadOnly(words));
    }

    public static OriginalModelMatrixBindingSource Select(LoadedShaderContainer shader,
        OriginalModelMatrixBindingInput input)
    {
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(input);
        OriginalModelMatrixBindingInput? canonical = Prepare(input.Draw, input.PreparedWrapperCount, input.PaletteCount);
        if (canonical is null || canonical.PackedType17 != input.PackedType17 || canonical.PaletteCount != input.PaletteCount ||
            !canonical.CurrentWords.SequenceEqual(input.CurrentWords) ||
            (input.UnboundGroupSource is { } group && ((ushort)group.Flags == 1 ||
                input.PreparedWrapperCount != 0 || !group.DrawIndices.Contains(input.Draw.Index))))
            throw new InvalidDataException("Original matrix input differs from its declared type17 source packing or group branch.");
        G1ShaderMatrixBindings bindings = G1ShaderMatrixBindingReader.Read(shader.Source.Bytes);
        G1ShaderMatrixBindingSelection selected = G1ShaderMatrixBindingMatcher.Select(
            input.CurrentWords.ToArray(), bindings.Rows);
        return new(shader, input, bindings, selected);
    }
}
