using System.Collections.ObjectModel;
using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public enum DeferredEarlyShadowInterfaceBoundary { ShortTable, NullInterface, RequiresRenderer }
public sealed record DeferredEarlyShadowFinalInterface(NamedModelShader FinalTable,
    int StateCount, ModelShaderInterfaceSource? Interface,
    DeferredEarlyShadowInterfaceBoundary Boundary);
public sealed record DeferredEarlyShadowProgram(int CombinationIndex, int? VertexProgram,
    int? PixelProgram, IReadOnlyList<DxbcSignatureParameter> PixelOutputs)
{
    public IReadOnlyList<uint> DeclaredColorLocations => Array.AsReadOnly(PixelOutputs
        .Where(p => p.SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase))
        .Select(p => p.Register).ToArray());
    public bool DeclaresFragmentDepth => PixelOutputs.Any(p => p.SemanticName.Text.StartsWith("SV_Depth", StringComparison.OrdinalIgnoreCase));
}
public sealed record DeferredEarlyShadowGenericSelection(
    ModelShaderVariantTransition Variant, DeferredEarlyShadowProgram Program);

/// <summary>The actual selected state3 renderer's imported program tables.
/// Explicit current dimensions/pass choose the program. Zero color attachments
/// in graph17 do not replace this choice with an arbitrary depth-only shader.</summary>
public sealed class LoadedDeferredEarlyShadowRenderer
{
    private readonly IReadOnlyDictionary<int, IReadOnlyList<DxbcSignatureParameter>> pixelOutputs;
    internal LoadedDeferredEarlyShadowRenderer(DeferredEarlyShadowFinalInterface selected,
        LoadedShaderContainer shader, G1ShaderBindingTables bindings,
        IReadOnlyDictionary<int, IReadOnlyList<DxbcSignatureParameter>> outputs)
        => (Selected, Shader, Bindings, pixelOutputs) = (selected, shader, bindings, outputs);
    public DeferredEarlyShadowFinalInterface Selected { get; }
    public LoadedShaderContainer Shader { get; }
    public G1ShaderBindingTables Bindings { get; }
    public DeferredEarlyShadowProgram SelectProgram(ReadOnlySpan<uint> actualDimensions, uint actualPass)
    {
        var row = Bindings.ResolveCombination(actualDimensions, actualPass);
        var vertex = row.Stages.Single(s => s.StageCode == 1).Entry;
        var pixel = row.Stages.Single(s => s.StageCode == 0).Entry;
        return new(row.Index, vertex?.ProgramIndex, pixel?.ProgramIndex,
            pixel is null ? Array.Empty<DxbcSignatureParameter>() : pixelOutputs[pixel.ProgramIndex]);
    }

    /// <summary>Generic shadow 1053FF0 / 1053F80 writers. The third DWORD
    /// retains its actual current owner value; it is not reconstructed from
    /// a material name or the desired framebuffer interface.</summary>
    public static byte[] ReadGenericRequestedVariant(ReadOnlySpan<byte> actualRetainedRequested12,
        byte actualFrontend1BE, byte actualFrontend1C0, uint actualShadowContext20)
    {
        if (actualRetainedRequested12.Length != 12)
            throw new ArgumentException("The original generic shadow variant is three DWORDs.", nameof(actualRetainedRequested12));
        byte[] requested = actualRetainedRequested12.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(requested, actualFrontend1BE != 0 || actualFrontend1C0 != 0 ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(4), actualShadowContext20);
        return requested;
    }

    /// <summary>981710 with the actual generic 1054240 comparator: zero/nonzero
    /// word0 classes, exact word1, and word2 only for a nonzero word0. Cached
    /// acceptance, unchanged index and a miss preserve their original meanings.</summary>
    public ModelShaderVariantTransition SelectGenericRequestedVariant(ModelShaderVariantCache actualCache,
        ReadOnlySpan<byte> actualRequested12)
    {
        ArgumentNullException.ThrowIfNull(actualCache);
        if (Selected.Interface?.Provider?.Object.ClassId != 0x39E93192 || Bindings.Variants.RecordWidth != 12)
            throw new NotSupportedException("This comparator belongs to the actual class39E93192 generic shadow renderer.");
        return SelectShadowRequestedVariant(Bindings.Variants, actualCache, actualRequested12);
    }

    // Generic and Tree2 shadow vtables both select 1054240. Their imported
    // variant widths differ; preserve the complete cache, including module tails.
    internal static ModelShaderVariantTransition SelectShadowRequestedVariant(
        G1ShaderVariantTable table, ModelShaderVariantCache actualCache, ReadOnlySpan<byte> requested)
    {
        if (table.RecordWidth < 12 || actualCache.CommittedBytes.Length != table.RecordWidth || requested.Length != table.RecordWidth)
            throw new ArgumentException("Shadow selection requires the complete imported variant cache.");
        if (actualCache.CommittedIndex < table.Records.Count &&
            GenericMatches(actualCache.CommittedBytes, requested))
            return Transition(ModelShaderVariantTransitionKind.CachedAcceptance, true,
                actualCache.CommittedIndex, actualCache.CommittedBytes, requested, false);
        foreach (var row in table.Records)
        {
            if (!GenericMatches(row.RawBytes.Span, requested)) continue;
            uint selectedIndex = checked((uint)row.Index);
            bool changed = selectedIndex != actualCache.CommittedIndex;
            return Transition(ModelShaderVariantTransitionKind.SourceRowMatch, true, selectedIndex,
                changed ? row.RawBytes.Span : actualCache.CommittedBytes,
                changed ? row.RawBytes.Span : requested, changed);
        }
        return Transition(ModelShaderVariantTransitionKind.MissRevert, false,
            actualCache.CommittedIndex, actualCache.CommittedBytes, actualCache.CommittedBytes, false);
    }

    public DeferredEarlyShadowGenericSelection SelectGenericCurrentProgram(
        ReadOnlySpan<uint> actualDimensions, uint actualPass, ModelShaderVariantCache actualCache,
        byte actualFrontend1BE, byte actualFrontend1C0, uint actualShadowContext20)
    {
        if (actualDimensions.Length != 7)
            throw new ArgumentException("The original G1 combination has seven dimensions.", nameof(actualDimensions));
        var requested = ReadGenericRequestedVariant(actualCache.RequestedBytes,
            actualFrontend1BE, actualFrontend1C0, actualShadowContext20);
        var transition = SelectGenericRequestedVariant(actualCache, requested);
        // 97E8E0 returns the source pass count even when 981710 reverts a miss.
        // The retained committed combination remains the actual program choice.
        uint[] dimensions = actualDimensions.ToArray();
        dimensions[6] = transition.Next.CommittedIndex;
        return new(transition, SelectProgram(dimensions, actualPass));
    }

    private static bool GenericMatches(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> requested)
    {
        uint a = BinaryPrimitives.ReadUInt32LittleEndian(candidate);
        uint b = BinaryPrimitives.ReadUInt32LittleEndian(requested);
        return (a == 0) == (b == 0) &&
            BinaryPrimitives.ReadUInt32LittleEndian(candidate.Slice(4)) == BinaryPrimitives.ReadUInt32LittleEndian(requested.Slice(4)) &&
            (a == 0 || BinaryPrimitives.ReadUInt32LittleEndian(candidate.Slice(8)) == BinaryPrimitives.ReadUInt32LittleEndian(requested.Slice(8)));
    }
    private static ModelShaderVariantTransition Transition(ModelShaderVariantTransitionKind kind,
        bool success, uint index, ReadOnlySpan<byte> committed, ReadOnlySpan<byte> requested, bool callback)
        => new(kind, success, new(index, committed.ToArray(), requested.ToArray()), callback);
}

/// <summary>Loaded graph19 contracts only. Its current shadow CPU20/pass
/// matrices, View19A0 counters, caster membership/order and GPU completion are
/// supplied by their actual owners, never inferred from ordinary54/55 draws.</summary>
public sealed class LoadedDeferredEarlyShadowQueueSource
{
    private readonly Dictionary<NamedModelShader, DeferredEarlyShadowFinalInterface> interfaces
        = new(ReferenceEqualityComparer.Instance);
    internal LoadedDeferredEarlyShadowQueueSource(LoadedDeferredEarlyPredecessorSource predecessor,
        WorldGeometryLateCpuNativeSource native) => (Predecessor, Native) = (predecessor, native);
    public LoadedDeferredEarlyPredecessorSource Predecessor { get; }
    public WorldGeometryLateCpuNativeSource Native { get; }
    public byte StateIndex => 3;
    public byte RenderType => 2;
    public byte CallerBankSelector => 2;
    public ulong SourcePacked728 => 0x0000001000430220;
    public string SourceEvidence => $"{RenderRuleIdentity.For("DeferredEarlyShadowQueue",Native.Locale)};" +
        "E80190-EA6470-D43190-render2/state3/bank2;D44B01-View1BD0-record20;" +
        "actual-current-shadow-CPU-passes-and-casters-required;not-pixel-completion";

    /// <summary>D434's RenderType2 common gate, independent of primitive
    /// counts: bit11 enables the first caster family, bit12 the second.
    /// An absent actual Common leaves both enabled as the original locals do.</summary>
    public (bool FirstCasterFamily, bool SecondCasterFamily) ReadCommonCasterFamilies(
        bool actualCommon240Present, uint actualCommonFlags8)
        => actualCommon240Present ? ((actualCommonFlags8 & 0x800) != 0, (actualCommonFlags8 & 0x1000) != 0) : (true, true);

    /// <summary>D58B10's shadow counter bank. This returns the source bank
    /// index, not its count, membership or sorted tuples.</summary>
    public static int ReadShadowRecordBankIndex(bool actualRecord10AliasesRecord18) => actualRecord10AliasesRecord18 ? 2 : 0;

    public DeferredEarlyShadowFinalInterface ReadFinalInterface(NamedModelShader actualFinalTable)
    {
        ArgumentNullException.ThrowIfNull(actualFinalTable);
        if (interfaces.TryGetValue(actualFinalTable, out var cached)) return cached;
        if (!actualFinalTable.HasVerifiedType16Creator)
            throw new NotSupportedException("Early shadow requires the actual final primitive18 type16 table.");
        int count = checked(actualFinalTable.DependencyObjectHashes.Count + 1);
        var slot = count <= StateIndex ? null : ModelShaderResources.ResolveType16Interface(actualFinalTable, StateIndex);
        var selected = new DeferredEarlyShadowFinalInterface(actualFinalTable, count, slot,
            slot is null ? DeferredEarlyShadowInterfaceBoundary.ShortTable : slot.IsNull
                ? DeferredEarlyShadowInterfaceBoundary.NullInterface : DeferredEarlyShadowInterfaceBoundary.RequiresRenderer);
        interfaces.Add(actualFinalTable, selected); return selected;
    }

    /// <summary>Import/reload boundary. Current selection later uses retained
    /// tables and never reopens/re-hashes a container or rewrites a program.</summary>
    public LoadedDeferredEarlyShadowRenderer LoadRenderer(GameResourceRepository repository,
        DeferredEarlyShadowFinalInterface actualSelectedInterface)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualSelectedInterface);
        var provider = actualSelectedInterface.Interface?.Provider ?? throw new InvalidDataException("A short/null state3 slot has no renderer.");
        var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
        var bindings = shader.ReadG1Bindings();
        var outputs = bindings.Combinations.Rows.SelectMany(row => row.Stages.Where(s => s.StageCode == 0 && s.Entry is not null))
            .Select(s => s.Entry!.ProgramIndex).Distinct().ToDictionary(index => index, index =>
                (IReadOnlyList<DxbcSignatureParameter>)Array.AsReadOnly(shader.ReadSignatures(index).Parts
                    .Where(p => p.Kind == DxbcSignatureKind.Output).SelectMany(p => p.Parameters).ToArray()));
        return new(actualSelectedInterface, shader, bindings,
            new ReadOnlyDictionary<int, IReadOnlyList<DxbcSignatureParameter>>(outputs));
    }
}

public static class DeferredEarlyShadowQueueSource
{
    /// <summary>Reuse the already imported predecessor and complete FC64
    /// native windows. This does not execute or mark graph17..20 completed.</summary>
    public static LoadedDeferredEarlyShadowQueueSource Read(LoadedDeferredEarlyPredecessorSource actualPredecessor,
        WorldGeometryLateCpuNativeSource actualLoadedNative)
    {
        ArgumentNullException.ThrowIfNull(actualPredecessor); ArgumentNullException.ThrowIfNull(actualLoadedNative);
        if (actualPredecessor.ShadowQueue19.Index != 19 || actualPredecessor.ShadowQueueRenderType != 2 ||
            actualPredecessor.ShadowQueueShaderState != 3 || actualPredecessor.ShadowQueueSourceFlags != 0x1008)
            throw new InvalidDataException("Early shadow needs its actual node19 source binding.");
        return new(actualPredecessor, actualLoadedNative);
    }
}
