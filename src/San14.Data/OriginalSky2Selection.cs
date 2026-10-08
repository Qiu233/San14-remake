using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalSky2VariantState { Desired, Committed }

public sealed record OriginalSky2SelectionResult(OriginalSky2VariantState VariantState,
    G1ShaderFeatureSelection RendererFeatureMatch, G1ShaderFeatureRecord RendererFeature,
    G1ShaderVariantRecord BindingVariant, uint PassIndex, IReadOnlyList<uint> Indices,
    G2ShaderCombination Combination);

/// <summary>Projection of explicit caller inputs onto the locked 340AC335 source tables.
/// Does not observe active indices, commit native state, infer quality from fog mode/profile,
/// or declare that the selected programs are supported by a renderer.</summary>
public sealed class OriginalSky2Selection
{
    private const string SourceSha256 = "CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424";
    public LoadedShaderContainer Source { get; }
    public G1ShaderConfigurations Configurations { get; }
    public G1ShaderBindingTables Tables { get; }

    private OriginalSky2Selection(LoadedShaderContainer source, G1ShaderConfigurations configurations,
        G1ShaderBindingTables tables) => (Source, Configurations, Tables) = (source, configurations, tables);

    public static OriginalSky2Selection Read(LoadedShaderContainer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Convert.ToHexString(SHA256.HashData(source.Source.Bytes)) != SourceSha256)
            throw new InvalidDataException("Sky2 selection requires the verified 340AC335 source bytes.");
        var configurations = G1ShaderConfigurationReader.Read(source.Source.Bytes, source.Container);
        var tables = source.ReadG1Bindings();
        uint[] dimensions = [2, 0, 0, 0, 0, 0, 8];
        if (tables.PassCount != 2 || !tables.RawDimensionCounts.SequenceEqual(dimensions) ||
            tables.Features.Count != 2 || tables.Variants.RecordWidth != 12 ||
            tables.Variants.Records.Count != 8 || tables.Combinations.Rows.Count != 32)
            throw new InvalidDataException("Sky2 selection source table shape is unsupported.");
        return new(source, configurations, tables);
    }

    /// <summary>118D7E0 / 118DA90 / 118CF40 setter projection. Cache byte offsets are
    /// relative to the explicitly identified parameter cache, not View CPU. This desired
    /// projection does not change the committed row tested by parameter preparation.</summary>
    public OriginalSky2SelectionResult SelectDesired(ReadOnlySpan<byte> explicitRendererFeature48,
        bool moonSetterPresent, bool sunSetterArgumentNull, byte parameterCache10B0,
        byte parameterCacheFDC, bool callerR8Bool)
    {
        Span<uint> words = stackalloc uint[3];
        words[0] = moonSetterPresent ? 1u : 0u;
        words[1] = sunSetterArgumentNull ? 1u : 0u;
        words[2] = parameterCache10B0 == 4 || (parameterCacheFDC & 2) != 0 ? 1u : 0u;
        return Project(explicitRendererFeature48, words, callerR8Bool, OriginalSky2VariantState.Desired);
    }

    /// <summary>Projects three explicitly supplied committed row DWORDs. Never reconstruct
    /// them from prepared/fallback Moon or Sun pointers. Native 118D660 compares Boolean
    /// zero/nonzero classes, so arbitrary nonzero DWORDs canonicalize to 1.</summary>
    public OriginalSky2SelectionResult SelectCommitted(ReadOnlySpan<byte> explicitRendererFeature48,
        ReadOnlySpan<uint> explicitCommittedThreeWords, bool callerR8Bool)
        => Project(explicitRendererFeature48, explicitCommittedThreeWords, callerR8Bool,
            OriginalSky2VariantState.Committed);

    private OriginalSky2SelectionResult Project(ReadOnlySpan<byte> feature48, ReadOnlySpan<uint> words,
        bool callerR8Bool, OriginalSky2VariantState state)
    {
        var current = G1ShaderFeatureRowReader.ReadRow(feature48);
        if (words.Length != 3)
            throw new InvalidDataException("Sky2 binding variant requires exactly three explicit DWORDs.");
        var match = G1ShaderFeatureMatcher.Select(current.Words.ToArray(),
            Tables.Features.Select(r => r.Feature).ToArray());
        if (match.RowIndex < 0)
            throw new InvalidDataException("Sky2 feature matcher has no result; a prior native feature index cannot be inferred.");
        Span<byte> canonical = stackalloc byte[12];
        for (int i = 0; i < 3; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(canonical.Slice(i * 4, 4), words[i] == 0 ? 0u : 1u);
        var variant = Tables.Variants.Match(canonical) ??
            throw new InvalidDataException("Sky2 Boolean binding variant is absent from the verified source.");
        uint pass = callerR8Bool ? 0u : 1u;
        // The five intervening source dimensions have effective width 1.
        // Their only valid index is 0; these are source bounds, not constructor defaults.
        uint[] indices = [(uint)match.RowIndex, 0, 0, 0, 0, 0, (uint)variant.Index];
        var combination = Tables.ResolveCombination(indices, pass);
        return new(state, match, Tables.Features[match.RowIndex], variant, pass,
            Array.AsReadOnly(indices), combination);
    }
}
