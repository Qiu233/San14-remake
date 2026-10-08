using Godot;
using San14.Data;

/// <summary>A lifetime loan for actual queue19 draws. Retire those draws
/// before disposing this loan, and release every loan before the target owner.</summary>
public sealed class OriginalDeferredEarlyShadowTargetFrameLoan : IDisposable
{
    private OriginalDeferredEarlyShadowTargetOwner? owner;
    internal OriginalDeferredEarlyShadowTargetFrameLoan(OriginalDeferredEarlyShadowTargetOwner actual) => owner = actual;
    public OriginalRenderTargetFrame Frame { get { RequireCurrent(); return owner!.Frame; } }
    public OriginalRenderTargetDeviceTexture DepthTexture { get { RequireCurrent(); return owner!.DepthTexture; } }
    public void RequireCurrent() => (owner ?? throw new ObjectDisposedException(nameof(OriginalDeferredEarlyShadowTargetFrameLoan))).RequireStorage();
    public void Dispose() { var current = owner; if (current is null) return; current.ReleaseFrameLoan(); owner = null; }
}

/// <summary>The actual selected node17 row for this request. It is target
/// metadata, never proof that node18 or the independent queue19 returned.</summary>
public sealed class OriginalDeferredEarlyShadowTargetBinding
{
    internal OriginalDeferredEarlyShadowTargetBinding(OriginalDeferredEarlyShadowTargetOwner owner,
        OriginalMapSourceCreatedNativeSessionPreparedFrame accepted, OriginalWorldGeometryTargetRow row)
        => (Owner, Accepted, Row) = (owner, accepted, row);
    public OriginalDeferredEarlyShadowTargetOwner Owner { get; }
    public OriginalMapSourceCreatedNativeSessionPreparedFrame Accepted { get; }
    public OriginalWorldGeometryTargetRow Row { get; }
    public OriginalRenderTargetFrame Frame => Row.Frame;
    public OriginalWorldGeometryDrawDomain Domain { get { RequireCurrent(); return Owner.CurrentTargetRow.Domain; } }
    public void RequireCurrent() => Owner.RequireBinding(this);
}

/// <summary>Issued after a real RD depth-only clear list ended. Submission
/// is not a fence or a whole queue19/17..20 completion.</summary>
public sealed class OriginalDeferredEarlyShadowDepthClearSubmission
{
    internal OriginalDeferredEarlyShadowDepthClearSubmission(OriginalDeferredEarlyShadowTargetOwner owner,
        OriginalMapSourceCreatedNativeSessionPreparedFrame accepted, OriginalWorldGeometryTargetRow row,
        OriginalDeferredEarlyDepthClearCommand? command, bool submitted, Rect2I? rectangle)
        => (Owner, Accepted, Row, Command, SubmittedDepthClear, EffectiveRectangle) =
            (owner, accepted, row, command, submitted, rectangle);
    public OriginalDeferredEarlyShadowTargetOwner Owner { get; }
    public OriginalMapSourceCreatedNativeSessionPreparedFrame Accepted { get; }
    public OriginalWorldGeometryTargetRow Row { get; }
    public OriginalDeferredEarlyDepthClearCommand? Command { get; }
    public bool SubmittedDepthClear { get; }
    public Rect2I? EffectiveRectangle { get; }
    public bool ProvesQueue19Completed => false;
    public string SourceEvidence => Owner.Source.NativeSourceEvidence + ";node18-actual-RD-depth-clear=" +
        SubmittedDepthClear + ";frame=" + Accepted.Context.FrameNumber + ";depth-view=" + Row.DepthView.Id;
    public void RequireCurrent() => Owner.RequireClear(this);
}

public sealed class OriginalDeferredEarlyShadowTargetPopSubmission
{
    internal OriginalDeferredEarlyShadowTargetPopSubmission(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalWorldGeometryCurrentPop source, OriginalWorldGeometryTargetRow restored)
        => (Accepted, Source, CurrentTargetRow) = (accepted, source, restored);
    public OriginalMapSourceCreatedNativeSessionPreparedFrame Accepted { get; }
    public OriginalWorldGeometryCurrentPop Source { get; }
    public OriginalWorldGeometryTargetRow CurrentTargetRow { get; }
    public bool ProvesQueue19Completed => false;
}

/// <summary>Persistent actual EA36C27D storage plus the explicit backend's
/// node17/18/20 target-row scope. Bind is selection, not an empty geometry
/// draw. Pop restores only the row/viewport/scissor; raster is caller-owned.
/// This component never creates shader, caster or queue19 success receipts.</summary>
public sealed class OriginalDeferredEarlyShadowTargetOwner : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly OriginalDeferredEarlyShadowTargetScopeState state = new();
    private OriginalMapSourceCreatedNativeSessionPreparedFrame? accepted;
    private Func<OriginalWorldGeometryCurrentCallerFields>? readCurrent;
    private Action? requireCurrent;
    private OriginalDeferredEarlyShadowTargetBinding? binding;
    private OriginalDeferredEarlyShadowDepthClearSubmission? clear;
    private int frameLoans;
    private bool retiring, disposed, faulted;
    private OriginalDeferredEarlyShadowTargetOwner(LoadedOriginalDeferredEarlyPredecessorSource source,
        LoadedOriginalWorldGeometryStageSource generic, LoadedOriginalDeferredEarlyShadowTargetRectangleSource rectangles,
        OriginalWorldGeometryBindNode bind, OriginalRenderTargetFrame frame)
        => (Source, GenericTargetSource, RectangleSource, BindSource, Frame, DepthTexture) =
            (source, generic, rectangles, bind, frame, frame.Textures[source.ShadowDepthDeclaration.Object.ObjectId]);
    public LoadedOriginalDeferredEarlyPredecessorSource Source { get; }
    public LoadedOriginalWorldGeometryStageSource GenericTargetSource { get; }
    public LoadedOriginalDeferredEarlyShadowTargetRectangleSource RectangleSource { get; }
    public OriginalWorldGeometryBindNode BindSource { get; }
    public OriginalRenderTargetFrame Frame { get; }
    public OriginalRenderTargetDeviceTexture DepthTexture { get; }
    public OriginalWorldGeometryTargetRow CurrentTargetRow { get { RequireCurrent(); return state.Current; } }

    /// <summary>Call once at the resource import/device generation boundary.
    /// The fixed source dimensions are explicit allocation inputs. No clear,
    /// queue19 or original active wrapper is claimed by allocation.</summary>
    public static async Task<OriginalDeferredEarlyShadowTargetOwner> BuildAsync(
        LoadedOriginalDeferredEarlyPredecessorSource source, LoadedOriginalWorldGeometryStageSource actualGenericSource,
        LoadedOriginalDeferredEarlyShadowTargetRectangleSource actualRectangleSource,
        bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualGenericSource);
        ArgumentNullException.ThrowIfNull(actualRectangleSource);
        if (actualRectangleSource.Locale != actualGenericSource.Native.Locale)
            throw new InvalidDataException("Early target rectangles require the same loaded executable locale.");
        if (!source.NativeSourceEvidence.Contains("/" + actualGenericSource.Native.Locale + "@" + actualGenericSource.Native.ExecutableSha256 + ";", StringComparison.Ordinal))
            throw new InvalidDataException("Early shadow and generic target commands require the same imported native source.");
        var bind = OriginalWorldGeometryStageSource.ReadLoadedBindDeclaration(source.ShadowDepthTargets17);
        if (bind.NodeIndex != 17 || bind.SourceFlag != 5 || bind.CpuFlags20 != 0xC0000000 ||
            bind.Colors.Count != 0 || bind.Depth.ObjectNameHash != 0xEA36C27D || bind.Depth.SurfaceId != 0 ||
            bind.Depth.Formats.TargetViewFormat != 40 || bind.Depth.Formats.ShaderViewFormat != 41 ||
            source.ShadowDepthPopFlags != 0)
            throw new InvalidDataException("Early shadow requires its own fixed depth-only node17 declaration and pop20 flags0.");
        var definition = source.ShadowDepthDeclaration;
        var frame = await OriginalRenderTargetFrame.BuildAsync(source.ShadowDepthTargets17,
            new Dictionary<uint, Vector2I> { [definition.Object.ObjectId] = new(definition.Width, definition.Height) }, allowReadback);
        return new(source, actualGenericSource, actualRectangleSource, bind, frame);
    }

    public OriginalDeferredEarlyShadowTargetFrameLoan BorrowFrame()
    {
        gate.Wait();
        try { RequireStorage(); frameLoans = checked(frameLoans + 1); return new(this); }
        finally { gate.Release(); }
    }
    internal void ReleaseFrameLoan()
    {
        gate.Wait();
        try { if (frameLoans <= 0) throw new InvalidOperationException("Early shadow target has no live frame loan."); frameLoans--; }
        finally { gate.Release(); }
    }
    public void BeginCurrent(OriginalMapSourceCreatedNativeSessionPreparedFrame actualAccepted,
        OriginalWorldGeometryTargetRow actualPriorTargetRow,
        Func<OriginalWorldGeometryCurrentCallerFields> currentFields, Action requireActualAcceptedScope)
    {
        ArgumentNullException.ThrowIfNull(actualAccepted); ArgumentNullException.ThrowIfNull(actualPriorTargetRow);
        ArgumentNullException.ThrowIfNull(currentFields); ArgumentNullException.ThrowIfNull(requireActualAcceptedScope);
        gate.Wait();
        try
        {
            RequireStorage(); requireActualAcceptedScope(); actualPriorTargetRow.RequireCurrent(); actualPriorTargetRow.Domain.Validate();
            if (accepted is not null || actualAccepted.MainCamera.ExecutableLocale != GenericTargetSource.Native.Locale ||
                actualAccepted.Context.FrameNumber != actualAccepted.MainCamera.FrameNumber ||
                actualAccepted.Context.PreparedCamera is null || !actualPriorTargetRow.Framebuffer.IsValid)
                throw new InvalidDataException("Early shadow target entry requires its current accepted context and actual parent row.");
            state.Begin(actualPriorTargetRow);
            accepted = actualAccepted; readCurrent = currentFields; requireCurrent = requireActualAcceptedScope;
            binding = null; clear = null;
        }
        finally { gate.Release(); }
    }

    /// <summary>The Flag5 decoder uses the actual primary depth surface's
    /// full extent, retaining this frontend row's depth endpoints and scissor
    /// enable. Cascade atlas rectangles are published later by queue19.</summary>
    public async Task<OriginalDeferredEarlyShadowTargetBinding?> Bind17Async()
    {
        await gate.WaitAsync();
        try
        {
            RequireCurrent(); state.RequireNext(17); var fields = readCurrent!();
            if (fields.CurrentContextFlags3F0 < 0 || !fields.CurrentTablePresent) { state.SkipBind(); return null; }
            var prior = state.Current.Domain;
            var extent = DepthTexture.ExplicitExtent;
            var currentViewport = new OriginalWorldMainDisplayViewport(checked((int)prior.Width),checked((int)prior.Height),
                checked((int)prior.Left),checked((int)prior.Top),BitConverter.SingleToUInt32Bits(prior.MinimumDepth),
                BitConverter.SingleToUInt32Bits(prior.MaximumDepth));
            var rectangles = RectangleSource.PrepareNode17CurrentDomain(BindSource,extent.X,extent.Y,
                currentViewport,prior.ScissorEnabled);
            var viewport = rectangles.Viewport;
            var actualPostBindDomain = new OriginalWorldGeometryDrawDomain(viewport.X,viewport.Y,viewport.Width,viewport.Height,
                BitConverter.UInt32BitsToSingle(viewport.MinimumDepthBits),BitConverter.UInt32BitsToSingle(viewport.MaximumDepthBits),
                rectangles.ScissorEnabled,new(rectangles.ScissorX,rectangles.ScissorY,rectangles.ScissorWidth,rectangles.ScissorHeight),
                rectangles.SourceEvidence);
            actualPostBindDomain.Validate();
            _ = GenericTargetSource.PrepareNullOptionalHostTargets(BindSource, fields.Host);
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => { RequireCurrent(); ValidateDeviceRow(rd, new(Frame, 0, actualPostBindDomain)); return true; });
            return binding = new(this, accepted!, state.Push(Frame, actualPostBindDomain));
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    public async Task<OriginalDeferredEarlyShadowDepthClearSubmission> Clear18Async()
    {
        await gate.WaitAsync();
        try
        {
            RequireCurrent(); state.RequireNext(18); var fields = readCurrent!(); var row = state.Current;
            var command = Source.PrepareNode18NativeDepthClear(fields.CurrentContextFlags3F0, row.DepthView.IsValid);
            Rect2I? rectangle = null; bool submitted = false;
            if (command is not null)
            {
                rectangle = OriginalDeferredEarlyShadowTargetScopeState.PrepareEffectiveClearRectangle(row.Domain);
                if (rectangle.Value.Size.X != 0 && rectangle.Value.Size.Y != 0)
                {
                    await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
                    {
                        RequireCurrent(); ValidateDeviceRow(rd, row);
                        using var format = rd.TextureGetFormat(row.DepthView);
                        if (format.Format is not (RenderingDevice.DataFormat.D32Sfloat or RenderingDevice.DataFormat.D32SfloatS8Uint) ||
                            format.Mipmaps != 1 || format.ArrayLayers != 1 || format.Samples != RenderingDevice.TextureSamples.Samples1)
                            throw new NotSupportedException("Node18 full clear requires its current single-level depth surface.");
                        var actualExtent = new Vector2I(checked((int)format.Width), checked((int)format.Height));
                        if (rectangle.Value.Position != Vector2I.Zero || rectangle.Value.Size != actualExtent)
                            throw new NotSupportedException("Node18 partial rectangle requires the original draw-clear branch; full depth clear cannot replace it.");
                        long list = rd.DrawListBegin(row.Framebuffer, RenderingDevice.DrawFlags.ClearDepth,
                            Array.Empty<Color>(), BitConverter.UInt32BitsToSingle(command.DepthBits), 0);
                        if (list < 0) throw new InvalidDataException("Device rejected the actual node18 depth clear.");
                        rd.DrawListEnd(); return true;
                    });
                    submitted = true;
                }
            }
            state.CompleteClear(); return clear = new(this, accepted!, row, command, submitted, rectangle);
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }

    /// <summary>Publishes the actual v68 viewport/scissor and retained
    /// frontend depth endpoints before a queue19 draw. This changes only the
    /// current domain; it never acknowledges a shader or completed draw.</summary>
    public void PublishCurrentQueue19Domain(OriginalWorldGeometryDrawDomain actualDomain)
    {
        ArgumentNullException.ThrowIfNull(actualDomain);
        gate.Wait();
        try { RequireCurrent(); if (binding is null) throw new InvalidOperationException("Queue19 has no current node17 binding.");
            actualDomain.Validate(); state.PublishQueue19Domain(actualDomain); }
        finally { gate.Release(); }
    }

    /// <summary>Caller invokes this at the actual node20 position, after its
    /// independently owned queue19. This restores parent attachments/RECTs,
    /// retaining the current frontend depth endpoints and scissor enable, and
    /// does not claim that queue19 ran or issue a depth copy.</summary>
    public async Task<OriginalDeferredEarlyShadowTargetPopSubmission> Pop20Async()
    {
        await gate.WaitAsync();
        try
        {
            RequireCurrent(); state.RequireNext(20); var fields = readCurrent!();
            bool pop = fields.CurrentContextFlags3F0 >= 0 && fields.CurrentTablePresent;
            var recipe = new OriginalWorldGeometryCurrentPop(pop, Source.ShadowDepthPopFlags & 0xFCFFFFFF, false, false);
            if (pop)
            {
                var parent = state.Parent;
                await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => { RequireCurrent(); ValidateDeviceRow(rd, parent); return true; });
            }
            var row = state.Pop(pop); if (pop) binding = null;
            return new(accepted!, recipe, row);
        }
        catch { faulted = true; throw; }
        finally { gate.Release(); }
    }
    public void EndCurrent()
    {
        gate.Wait();
        try { RequireCurrent(); state.End(); accepted = null; readCurrent = null; requireCurrent = null; binding = null; clear = null; }
        finally { gate.Release(); }
    }
    internal void RequireBinding(OriginalDeferredEarlyShadowTargetBinding actual)
    {
        RequireCurrent();
        if (!ReferenceEquals(actual, binding) || !ReferenceEquals(actual.Accepted, accepted) ||
            !ReferenceEquals(actual.Row.NamedFrame, state.Current.NamedFrame) || actual.Row.TargetStackIndex != state.Current.TargetStackIndex)
            throw new InvalidDataException("Early shadow binding was replaced, popped, ended or belongs to another invocation.");
    }
    internal void RequireClear(OriginalDeferredEarlyShadowDepthClearSubmission actual)
    {
        RequireCurrent();
        if (!ReferenceEquals(actual, clear) || !ReferenceEquals(actual.Accepted, accepted))
            throw new InvalidDataException("Early shadow clear submission is foreign or its current invocation ended.");
    }
    internal void RequireStorage()
    {
        ObjectDisposedException.ThrowIf(retiring || disposed, this);
        if (faulted) throw new InvalidOperationException("Failed early shadow target scope must retire before reuse.");
    }
    private void RequireCurrent() { RequireStorage(); if (accepted is null) throw new InvalidOperationException("No current early shadow target scope."); requireCurrent!(); }
    private static void ValidateDeviceRow(RenderingDevice rd, OriginalWorldGeometryTargetRow row)
    {
        row.RequireCurrent();
        if (!rd.FramebufferIsValid(row.Framebuffer) || row.ColorViews.Any(t => !rd.TextureIsValid(t)) ||
            row.DepthView.IsValid && !rd.TextureIsValid(row.DepthView))
            throw new ObjectDisposedException("Early shadow target row framebuffer/views");
    }
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (disposed) return;
            if (frameLoans != 0) throw new InvalidOperationException("Retire the actual queue19 draws and release their frame loans before early shadow storage.");
            retiring = true; binding = null; clear = null;
            await Frame.DisposeAsync(); state.Retire();
            accepted = null; readCurrent = null; requireCurrent = null; disposed = true;
        }
        finally { gate.Release(); }
    }
}

/// <summary>Backend target rectangles plus current frontend depth/scissor
/// state. The original pop restores rectangles but does not write those globals.
/// It does not call or acknowledge the independent queue19.</summary>
internal sealed class OriginalDeferredEarlyShadowTargetScopeState
{
    private OriginalWorldGeometryTargetRow? prior, current;
    private int next;
    private bool pushed;
    public OriginalWorldGeometryTargetRow Current => current ?? throw new InvalidOperationException("No current early shadow target row.");
    public OriginalWorldGeometryTargetRow Parent
    {
        get
        {
            if (!pushed) throw new InvalidOperationException("Node20 has no node17-owned parent to restore.");
            var saved = prior!; var global = Current.Domain;
            if (BitConverter.SingleToUInt32Bits(saved.Domain.MinimumDepth) == BitConverter.SingleToUInt32Bits(global.MinimumDepth) &&
                BitConverter.SingleToUInt32Bits(saved.Domain.MaximumDepth) == BitConverter.SingleToUInt32Bits(global.MaximumDepth) &&
                saved.Domain.ScissorEnabled == global.ScissorEnabled) return saved;
            return saved with { Domain = saved.Domain with { MinimumDepth = global.MinimumDepth, MaximumDepth = global.MaximumDepth,
                ScissorEnabled = global.ScissorEnabled,
                SourceEvidence = saved.Domain.SourceEvidence + ";opcode3-restored-RECTs/current-frontend-depth-scissor-enable" } };
        }
    }
    public void Begin(OriginalWorldGeometryTargetRow actual)
    {
        if (current is not null) throw new InvalidOperationException("Previous early shadow target scope remains active.");
        actual.Domain.Validate(); if (actual.TargetStackIndex < 0) throw new InvalidDataException("Actual parent stack index is invalid.");
        prior = current = actual; next = 17; pushed = false;
    }
    public void RequireNext(int node) { if (current is null || node != next) throw new InvalidOperationException("Early target commands must follow17/18/20; queue19 belongs to its actual caller."); }
    public void SkipBind() { RequireNext(17); next = 18; }
    public OriginalWorldGeometryTargetRow Push(OriginalRenderTargetFrame actual, OriginalWorldGeometryDrawDomain domain)
    { RequireNext(17); current = new(actual, checked(Current.TargetStackIndex + 1), domain); pushed = true; next = 18; return current; }
    public void CompleteClear() { RequireNext(18); next = 20; }
    public void PublishQueue19Domain(OriginalWorldGeometryDrawDomain actualDomain)
    { RequireNext(20); if (!pushed) throw new InvalidOperationException("Queue19 requires its actual node17 row.");
        actualDomain.Validate(); current = Current with { Domain = actualDomain }; }
    public OriginalWorldGeometryTargetRow Pop(bool shouldPop)
    { RequireNext(20); if (shouldPop) { current = Parent; pushed = false; } next = 21; return Current; }
    public void End()
    {
        if (next != 21 || pushed || current is null || prior is null || current.TargetStackIndex != prior.TargetStackIndex ||
            !ReferenceEquals(current.NamedFrame,prior.NamedFrame) || !ReferenceEquals(current.DeviceRow,prior.DeviceRow))
            throw new InvalidOperationException("Early target commands did not return to their actual parent attachments.");
        Retire();
    }
    public void Retire() { prior = current = null; next = 0; pushed = false; }
    public static Rect2I PrepareEffectiveClearRectangle(OriginalWorldGeometryDrawDomain domain)
    {
        domain.Validate();
        int Int(float value) => value >= int.MinValue && value <= int.MaxValue && value == MathF.Truncate(value)
            ? checked((int)value) : throw new InvalidDataException("Native opcode4 clear requires the actual integer row60 rectangle.");
        int x = Int(domain.Left), y = Int(domain.Top), width = Int(domain.Width), height = Int(domain.Height);
        if (domain.ScissorEnabled)
        {
            var clip = domain.ScissorRectangle;
            // Exact95EE46..81 integer operations on row58/row60.
            if (clip.Left > x) { width = checked(width - (clip.Left - x)); x = clip.Left; }
            if (clip.Top > y) { height = checked(height - (clip.Top - y)); y = clip.Top; }
            width = Math.Min(width, clip.Width); height = Math.Min(height, clip.Height);
        }
        if (width < 0 || height < 0) throw new NotSupportedException("Negative original clear rectangle needs the independent draw-clear branch.");
        return new(x, y, width, height);
    }
}
