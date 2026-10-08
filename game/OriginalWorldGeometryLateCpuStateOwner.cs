using San14.Data;

public sealed record OriginalWorldGeometryLateColorState(bool BlendEnabled, byte RgbMask, bool AlphaWrite);
public sealed record OriginalWorldGeometryLateStencilState(bool Enabled, uint Compare, byte Reference,
    byte ReadMask, byte WriteMask, uint Fail, uint DepthFail, uint Pass);

/// <summary>The caller's actual mutable CPU frontend subset. Unrepresented
/// raster/blend factors/raw cache words are retained by their owning caller.</summary>
public sealed class OriginalWorldGeometryLateCpuState
{
    private readonly OriginalWorldGeometryLateColorState?[] colors;
    private bool? alphaEnabled;
    private uint? clipWord1F0;
    public OriginalWorldGeometryLateCpuState(OriginalWorldGeometryLateDepth actualDepth,
        IReadOnlyList<OriginalWorldGeometryLateColorState> actualEightColorStates, bool actualAlphaEnabled,
        OriginalWorldGeometryLateStencilState actualStencil, uint actualClipWord1F0)
    {
        ArgumentNullException.ThrowIfNull(actualEightColorStates); ArgumentNullException.ThrowIfNull(actualStencil);
        if (actualEightColorStates.Count != 8 || actualEightColorStates.Any(c => c is null) ||
            actualDepth.Stencil != actualStencil.Enabled)
            throw new InvalidDataException("Late geometry CPU state requires the actual eight color slots, not constructor defaults.");
        Depth = actualDepth; colors = actualEightColorStates.Cast<OriginalWorldGeometryLateColorState?>().ToArray();
        alphaEnabled = actualAlphaEnabled; Stencil = actualStencil; clipWord1F0 = actualClipWord1F0;
    }
    private OriginalWorldGeometryLateCpuState(OriginalWorldGeometryLateDepth actualDepth,
        OriginalWorldGeometryLateColorState?[] projection, bool? actualAlphaEnabled,
        OriginalWorldGeometryLateStencilState actualStencil, uint? actualClipWord1F0)
    {
        if (actualDepth.Stencil != actualStencil.Enabled)
            throw new InvalidDataException("Late depth and stencil projection must represent the same actual enable state.");
        Depth = actualDepth; colors = projection; alphaEnabled = actualAlphaEnabled;
        Stencil = actualStencil; clipWord1F0 = actualClipWord1F0;
    }

    /// <summary>Only the actually bound color prefix is represented. Inactive
    /// slots, an unread alpha cache and an unread clip word remain unknown
    /// until a real source writer updates those fields. This never creates
    /// native frontend defaults for unconsumed slots.</summary>
    public static OriginalWorldGeometryLateCpuState CreateActiveProjection(OriginalWorldGeometryLateDepth actualDepth,
        IReadOnlyList<OriginalWorldGeometryLateColorState> actualBoundColors, bool? actualAlphaEnabled,
        OriginalWorldGeometryLateStencilState actualStencil, uint? actualClipWord1F0)
    {
        ArgumentNullException.ThrowIfNull(actualBoundColors); ArgumentNullException.ThrowIfNull(actualStencil);
        if (actualBoundColors.Count > 8 || actualBoundColors.Any(c => c is null))
            throw new InvalidDataException("Late active color projection requires the actual bound prefix of at most eight slots.");
        var projection = new OriginalWorldGeometryLateColorState?[8];
        for (int i = 0; i < actualBoundColors.Count; i++) projection[i] = actualBoundColors[i];
        return new(actualDepth, projection, actualAlphaEnabled, actualStencil, actualClipWord1F0);
    }
    public OriginalWorldGeometryLateDepth Depth { get; internal set; }
    public bool AlphaEnabled
    {
        get => alphaEnabled ?? throw new InvalidOperationException("Current alpha cache has not been represented by this source projection.");
        internal set => alphaEnabled = value;
    }
    public bool? ActualAlphaEnabled => alphaEnabled;
    public OriginalWorldGeometryLateStencilState Stencil { get; internal set; }
    public uint ClipWord1F0
    {
        get => clipWord1F0 ?? throw new InvalidOperationException("Current clip word has not been represented by this source projection.");
        internal set => clipWord1F0 = value;
    }
    public uint? ActualClipWord1F0 => clipWord1F0;
    public IReadOnlyList<OriginalWorldGeometryLateColorState?> ColorProjection => Array.AsReadOnly(colors);
    public IReadOnlyList<OriginalWorldGeometryLateColorState> Colors => RequireActiveColors(8);
    public IReadOnlyList<OriginalWorldGeometryLateColorState> RequireActiveColors(int actualBoundCount)
    {
        if (actualBoundCount is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(actualBoundCount));
        var result = new OriginalWorldGeometryLateColorState[actualBoundCount];
        for (int i = 0; i < result.Length; i++) result[i] = colors[i] ??
            throw new InvalidOperationException("Current active color slot is not represented in this source projection.");
        return Array.AsReadOnly(result);
    }
    internal void SetActiveColors(OriginalModelOrdinaryActiveColorState actualActive)
    {
        for (int i = 0; i < actualActive.RgbaWriteMasks.Count; i++)
        {
            byte rgba = actualActive.RgbaWriteMasks[i];
            colors[i] = new(false, (byte)(rgba & 7), (rgba & 8) != 0);
        }
    }
    internal void SetStencilEnabled(bool enabled)
    {
        Stencil = Stencil with { Enabled = enabled };
        Depth = Depth with { Stencil = enabled };
    }
}

public sealed record OriginalWorldGeometryLatePrimitive(string Identity, OriginalNamedModelShader ActualFinalTable);
public sealed record OriginalWorldGeometryLateRecord(OriginalModelOrdinaryDispatcherRecord Record,
    int FirstBankStoredCount, int FirstBankCapacity, IReadOnlyList<OriginalWorldGeometryLatePrimitive> ActualOrderedFirstBank);

/// <summary>Required actual parameter/view scope implementation. These are
/// real current producers, not no-op adapters or completion booleans. The
/// owner below supplies no implementation for the unresolved D19050 writer.
/// Its result remains CPU updates only and never certifies whole node return.</summary>
public interface IOriginalWorldGeometryLateCpuParameterSource
{
    void RequireCurrent(OriginalWorldGeometryTargetBinding actual53);
    void BeginActualViewportScope(int sourceNode);
    void RestoreActualViewportScope(int sourceNode);
    void WriteActualContextFlags3F0(uint flags);
    float ReadActualParameterTime(); // EA7191..A1: (FD8 * FD4) + FD0, scalar source order.
    void SetActualParameterTime(float actualTime);
    void EnterActualGenericScope58(); // 97BBA0, including actual 97D430 consumers.
    void SetActualGenericWordD4(uint value); // D71980, including current dirty/cache semantics.
    void PrepareActualRecordParameters58(OriginalWorldGeometryLateRecord actualRecord);
    void CleanupActualRecord58(OriginalWorldGeometryLateRecord actualRecord);
    void ClearActualSixGenericResources58(); // D1BA00's six actual setters/loans.
}

/// <summary>Applied fields and visited CPU tuples. This deliberately has no
/// AL, GPU completion or whole-source58-return authority: parameter work and
/// actual final-table publication are separately supplied current producers.</summary>
public sealed record OriginalWorldGeometryLateCpuUpdates(int SourceNode, bool HasCurrentView,
    bool EnteredDispatcher, int VisitedRecords, int VisitedFirstBankPrimitives, int SourceNullShaderPrimitives)
{
    public bool ProvesGpuDrawSubmitted => false;
    public bool ProvesWholeNativeNodeReturned => false;
}

/// <summary>Small per-invocation CPU state executor for the ordinary57/58
/// branch. Source loads persist; current fields/rosters are read each call.
/// The caller applies this mutable subset to its own frontend, stock and
/// parameter owner before59. No target binding, draw or pixel lease is made.</summary>
public sealed class OriginalWorldGeometryLateCpuStateOwner
{
    private readonly LoadedOriginalWorldGeometryLateCpuSource source;
    private readonly LoadedOriginalModelFrontendColorSource colorSource;
    private readonly OriginalWorldGeometryTargetBinding binding;
    private readonly Func<OriginalWorldGeometryLateCurrentContext> current;
    private readonly IOriginalWorldGeometryLateCpuParameterSource parameters;
    private int phase;
    public OriginalWorldGeometryLateCpuStateOwner(LoadedOriginalWorldGeometryLateCpuSource loadedSource,
        LoadedOriginalModelFrontendColorSource loadedColorSource,
        OriginalWorldGeometryTargetBinding actual53, OriginalWorldGeometryLateCpuState actualState,
        Func<OriginalWorldGeometryLateCurrentContext> readActualCurrentContext,
        IOriginalWorldGeometryLateCpuParameterSource actualParameters)
    {
        ArgumentNullException.ThrowIfNull(loadedSource); ArgumentNullException.ThrowIfNull(loadedColorSource);
        ArgumentNullException.ThrowIfNull(actual53);
        ArgumentNullException.ThrowIfNull(actualState); ArgumentNullException.ThrowIfNull(readActualCurrentContext);
        ArgumentNullException.ThrowIfNull(actualParameters);
        source = loadedSource; colorSource = loadedColorSource; binding = actual53; State = actualState;
        current = readActualCurrentContext; parameters = actualParameters;
        if (colorSource.Locale != source.Native.Locale || colorSource.ExecutableSha256 != source.Native.ExecutableSha256)
            throw new InvalidDataException("Late CPU/color source imports belong to different executable profiles.");
        RequireCurrent();
    }
    public OriginalWorldGeometryLateCpuState State { get; }
    private void RequireCurrent()
    {
        binding.RequireCurrent(); parameters.RequireCurrent(binding);
        if (binding.SourceNode != 53 || binding.Accepted.MainCamera.ExecutableLocale != source.Native.Locale)
            throw new InvalidDataException("Late geometry CPU state needs this actual node53/current source profile.");
    }
    public OriginalWorldGeometryLateCpuUpdates Apply57(bool actualCommon240Present, bool actualCommon240Bit2,
        object? currentView1B18)
    {
        RequireCurrent(); if (phase != 0) throw new InvalidOperationException("Source57 CPU update must run once before58.");
        var recipe = source.Prepare57(current(), actualCommon240Present, actualCommon240Bit2, currentView1B18);
        try
        {
            if (recipe.HasCurrentView)
            {
                State.Depth = recipe.Depth!.Value;
                State.SetStencilEnabled(false);
                parameters.BeginActualViewportScope(57);
                try
                {
                    parameters.WriteActualContextFlags3F0(recipe.ResultContextFlags3F0);
                    if (recipe.RequiresSpecialized1B18Consumer)
                        throw new NotSupportedException("Current nonnull View1B18 requires its actual RenderingType1 consumer; known entry writes are not undone.");
                }
                finally { parameters.RestoreActualViewportScope(57); }
            }
        }
        catch { phase = -1; throw; }
        phase = 1; return new(57, recipe.HasCurrentView, false, 0, 0, 0);
    }
    public OriginalWorldGeometryLateCpuUpdates Apply58(int actualView19D4Stored, int actualView19D0Capacity,
        object? currentView1C18, OriginalMapFreshCommonResourceSnapshot actualCommon, float actualCommonTime254,
        int actualBoundColorCount, IReadOnlyList<OriginalWorldGeometryLateRecord> completeCurrentKind1Records)
    {
        RequireCurrent(); if (phase != 1) throw new InvalidOperationException("Source58 CPU update follows actual57 update.");
        ArgumentNullException.ThrowIfNull(actualCommon); ArgumentNullException.ThrowIfNull(completeCurrentKind1Records);
        var ctx = current(); var recipe = source.Prepare58Entry(ctx, actualCommon.CommonViewResource240Present,
            actualCommon.CommonViewFlags8, actualView19D4Stored, actualView19D0Capacity, currentView1C18);
        if (!recipe.HasCurrentView) { phase = 2; return new(58, false, false, 0, 0, 0); }
        if (!float.IsFinite(actualCommonTime254) ||
            (recipe.ViewRecordCount is int actualRows && completeCurrentKind1Records.Count != actualRows) ||
            actualBoundColorCount != binding.Frame.ColorViews.Count ||
            actualCommon.FrameNumber != binding.Accepted.Context.FrameNumber ||
            actualCommon.SelectedViewOwnerIdentity != binding.Accepted.Feature.Binding.ViewIdentity)
            throw new InvalidDataException("Source58 needs the same current Common/clock and actual complete View19D4 record rows.");
        int visited = 0, primitives = 0, nullShaders = 0;
        try
        {
            RunNode58ParameterScope(parameters, actualCommonTime254, () =>
            {
            if (recipe.EntersOrdinaryDispatcher)
            {
                State.Depth = recipe.Depth!.Value;
                State.SetStencilEnabled(false);
                var activeColors = colorSource.PrepareOrdinaryQueueActiveColorEntry(source.Queue58, actualBoundColorCount);
                State.SetActiveColors(activeColors);
                // Actual selected current Aux; the shared helper handles the
                // explicitly bounded source-created no-overlay scope.
                var auxMasks = colorSource.ReadCurrentAuxiliaryMasks(actualCommon);
                parameters.EnterActualGenericScope58(); parameters.SetActualGenericWordD4(0);
                State.SetStencilEnabled(true);
                State.Stencil = State.Stencil with { Fail = 0, DepthFail = 0, Pass = 2 };
                foreach (var row in completeCurrentKind1Records)
                {
                    RequireCurrent(); visited++;
                    int count = LoadedOriginalWorldGeometryLateCpuSource.EffectiveCurrentCount(row.FirstBankStoredCount, row.FirstBankCapacity);
                    if (row.ActualOrderedFirstBank.Count != count || row.Record.CurrentSelectedCounterCount != count)
                        throw new InvalidDataException("Source58 first bank roster/count must remain complete, independent of state2 shader selection.");
                    var stencil = source.Prepare58Record(row.Record, actualCommon, ctx.ContextFlags3E4);
                    if (!stencil.RecordSelected) continue;
                    State.Stencil = State.Stencil with { Compare = stencil.NativeCompareCode, Reference = stencil.Reference,
                        ReadMask = stencil.ReadMask, WriteMask = stencil.WriteMask };
                    var recordColors = colorSource.PrepareLateRecordActiveColor(activeColors, row.Record.RecordMask78, auxMasks);
                    if (recordColors.HasRecordMaskWriter) State.SetActiveColors(recordColors.DrawState);
                    if (count != 0)
                    {
                        parameters.PrepareActualRecordParameters58(row); // required D19050 producer, not an empty adapter.
                        State.AlphaEnabled = false; // D363EB..D3642D before any typed16 lookup.
                        foreach (var primitive in row.ActualOrderedFirstBank)
                        {
                            if (string.IsNullOrWhiteSpace(primitive.Identity) || primitive.ActualFinalTable is null)
                                throw new InvalidDataException("State2 consumer requires each actual current final typed16 table.");
                            // The actual final material-table producer supplies this owner;
                            // a base shader or neighbouring state18 provider is not a substitute.
                            var selected = OriginalModelShaderResources.ResolveType16Interface(primitive.ActualFinalTable, source.ShaderStateIndex);
                            if (!selected.IsNull) throw new NotSupportedException("Nonnull current state2 requires its actual shader/draw consumer.");
                            primitives++; nullShaders++;
                        }
                        parameters.CleanupActualRecord58(row);
                    }
                    if (recordColors.HasRecordMaskWriter) State.SetActiveColors(recordColors.ExitState);
                }
                State.SetStencilEnabled(false);
                if (actualCommon.CommonViewResource240Present) State.ClipWord1F0 = 0;
                parameters.ClearActualSixGenericResources58();
            }
            }, () => State.Depth = source.Prepare58ExitDepth(current().ContextFlags3E4, State.Depth));
        }
        catch { phase = -1; throw; }
        phase = 2; return new(58, true, recipe.EntersOrdinaryDispatcher, visited, primitives, nullShaders);
    }

    // Isolated scope sequencing, also used by the CPU callback-failure fixture.
    // This has no accepted node, current frame or completion authority.
    internal static void RunNode58ParameterScope(IOriginalWorldGeometryLateCpuParameterSource actualParameters,
        float actualCurrentTime, Action executeCurrentDispatcher, Action applyExitDepth)
    {
        actualParameters.BeginActualViewportScope(58);
        bool saved = false; float savedTime = 0;
        try
        {
            savedTime = actualParameters.ReadActualParameterTime(); saved = true;
            actualParameters.SetActualParameterTime(actualCurrentTime);
            executeCurrentDispatcher();
        }
        finally
        {
            try { if (saved) actualParameters.SetActualParameterTime(savedTime); }
            finally
            {
                try { if (saved) applyExitDepth(); }
                finally { actualParameters.RestoreActualViewportScope(58); }
            }
        }
    }
}
