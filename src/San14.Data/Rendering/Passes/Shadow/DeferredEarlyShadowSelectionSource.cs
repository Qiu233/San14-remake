using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DeferredEarlyShadowCurrentSelection(
    DeferredEarlyShadowSelectionState State,
    ModelShaderDimensionTransition MatrixWrite,
    ModelShaderVariantTransition? VariantWrite,
    IReadOnlyList<uint> Indices,
    IReadOnlyList<uint> DeclaredPasses,
    bool ClearsFrontend1C0)
{
    public DeferredEarlyShadowProgram ReadProgram(uint actualPass)
        => State.Renderer.SelectProgram(Indices.ToArray(), actualPass);
}

/// <summary>One source-created generic state3 renderer's small retained selection
/// fields. Current matrix/variant writes run for each primitive scope. Shader
/// stage preparation, modifier effects, constant values and draw return remain
/// operations of the actual device and parameter owners.</summary>
public sealed class DeferredEarlyShadowSelectionState
{
    private readonly uint[] indices = new uint[7];
    private readonly G1ShaderMatrixBindings matrixRows;
    private ModelShaderVariantCache variant;
    private bool variantDirty;
    private int currentPass = -1;
    internal DeferredEarlyShadowSelectionState(LoadedDeferredEarlyShadowSelectionSource source,
        LoadedDeferredEarlyShadowRenderer renderer)
    {
        Source = source; Renderer = renderer;
        var counts = renderer.Bindings.RawDimensionCounts;
        if (renderer.Selected.Interface?.Provider?.Object.ClassId != 0x39E93192 || counts.Count != 7 ||
            renderer.Bindings.Variants.RecordWidth != 12 || renderer.Bindings.Variants.Records.Count == 0 ||
            Enumerable.Range(0, 7).Where(i => i is not 2 and not 6).Any(i => counts[i] > 1))
            throw new NotSupportedException("The finite state3 source owner requires generic class39 and single/empty nonmatrix, nonvariant dimensions.");
        matrixRows = renderer.Shader.ReadMatrixBindings();
        // ABFA80 invokes981F00 after its generic constructor: seven explicit
        // zero stores, pass=-1, and the entire first12B row in both caches.
        var first = renderer.Bindings.Variants.Records[0].RawBytes;
        variant = new(0, first.ToArray(), first.ToArray());
    }
    public LoadedDeferredEarlyShadowSelectionSource Source { get; }
    public LoadedDeferredEarlyShadowRenderer Renderer { get; }
    public int CurrentPass1414 => currentPass;
    public IReadOnlyList<uint> CurrentIndices => Array.AsReadOnly(indices.ToArray());
    public ReadOnlyMemory<byte> RequestedVariant => variant.RequestedBytes;
    public ReadOnlyMemory<byte> CommittedVariant => variant.CommittedBytes;
    public string SourceEvidence => Source.SourceEvidence + ";same-generic39-owned-cache;current-type17/Shadow20/frontend-flags;not-stage-or-draw-return";

    /// <summary>Project980CC0's guarded matrix writer,1053FF0's format writer,
    /// then1053F80/97E8E0's current variant selection. The third requested word
    /// is retained by this owner. This finite path has no external variant
    /// parameter setter; callers needing such a setter must provide that source
    /// operation rather than recreate this state for every draw.</summary>
    public DeferredEarlyShadowCurrentSelection PrepareCurrent(
        ModelMatrixBindingInput actualType17, uint actualShadowCpu20,
        byte actualFrontend1BE, byte actualFrontend1C0)
    {
        ArgumentNullException.ThrowIfNull(actualType17);
        var match = G1ShaderMatrixBindingMatcher.Select(actualType17.Words, matrixRows);
        uint previous = indices[2];
        bool commit = match.RowIndex >= 0 && (uint)match.RowIndex < matrixRows.Rows.Count && (uint)match.RowIndex != previous;
        if (commit) indices[2] = checked((uint)match.RowIndex);
        var matrixWrite = new ModelShaderDimensionTransition(2, previous, match.RowIndex,
            match.FoundZeroPenalty, indices[2], commit,
            commit ? new(match.RowIndex, matrixRows.Rows[match.RowIndex].SourceOffset,
                matrixRows.Rows[match.RowIndex].RawBytes) : null);

        byte[] requested = variant.RequestedBytes.ToArray();
        if (BinaryPrimitives.ReadUInt32LittleEndian(requested.AsSpan(4)) != actualShadowCpu20)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(4), actualShadowCpu20);
            variantDirty = true;
        }
        bool needsAlpha = actualFrontend1BE != 0 || actualFrontend1C0 != 0;
        // Native compares zero/nonzero classes; an already-nonzero current
        // word is preserved, not normalized again on every invocation.
        if ((BinaryPrimitives.ReadUInt32LittleEndian(requested) != 0) != needsAlpha)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(requested, needsAlpha ? 1u : 0u);
            variantDirty = true;
        }
        variant = variant with { RequestedBytes = requested };
        ModelShaderVariantTransition? transition = null;
        if (variantDirty)
        {
            transition = Renderer.SelectGenericRequestedVariant(variant, requested);
            variant = transition.Next; indices[6] = variant.CommittedIndex;
            variantDirty = false; //981710 clears20 before comparison/miss.
        }
        var passes = Array.AsReadOnly(Enumerable.Range(0, checked((int)Renderer.Bindings.PassCount))
            .Select(p => checked((uint)p)).ToArray());
        return new(this, matrixWrite, transition, Array.AsReadOnly(indices.ToArray()), passes, actualFrontend1C0 != 0);
    }

    /// <summary>Only the actual97E920 stage result publishes1414. Preparation
    /// failure retains its previous value; a program choice alone is not success.</summary>
    public ModelShaderPassTransition AcceptActualPassStageReturn(
        DeferredEarlyShadowCurrentSelection actualSelection, uint actualPass, bool actualStageSucceeded)
    {
        if (!ReferenceEquals(actualSelection.State, this) || actualPass >= Renderer.Bindings.PassCount)
            throw new ArgumentException("State3 pass result belongs to another renderer or is outside its source pass range.");
        int previous = currentPass;
        if (!actualStageSucceeded) return new(actualPass, false, previous, previous, null);
        var combination = Renderer.Bindings.ResolveCombination(actualSelection.Indices.ToArray(), actualPass);
        currentPass = checked((int)actualPass);
        return new(actualPass, true, previous, currentPass, combination);
    }
}

/// <summary>Load/reload source proof; retained lookup never rereads executable
/// or G1S. The source renderer is created once per actual state3 provider.</summary>
public sealed class LoadedDeferredEarlyShadowSelectionSource
{
    internal LoadedDeferredEarlyShadowSelectionSource(string locale, string evidence)
        => (Locale, SourceEvidence) = (locale, evidence);
    public string Locale { get; }
    public string SourceEvidence { get; }
    public static LoadedDeferredEarlyShadowSelectionSource Load(GameResourceRepository repository, string locale)
        => RenderSamplerSourceAdapters.LoadEarlyShadowSelectionSource(repository.SourceDirectory, locale);
    public DeferredEarlyShadowSelectionState CreateOwnedRenderer(LoadedDeferredEarlyShadowRenderer renderer)
        => new(this, renderer);
    /// <summary>Once at a real fresh backend construction. This is shared
    /// by its shaders; recreating it per draw would discard current RT0 blend.</summary>
    public DeferredEarlyShadowFrontendFields ConstructFreshFrontendFields() => new(this);
}
