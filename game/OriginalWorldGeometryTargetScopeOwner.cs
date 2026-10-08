using Godot;
using San14.Data;

/// <summary>Read from the current owned Host/table by the ordered caller.
/// Q3F0 is the signed context flag word; View selection uses Q3F8.</summary>
public readonly record struct OriginalWorldGeometryCurrentCallerFields(int CurrentContextFlags3F0,
    bool CurrentTablePresent, OriginalWorldGeometryHostFields Host);

/// <summary>Actual wrapper rectangles after target binding, or the actual
/// prior row saved on entry. A disabled scissor still retains its rectangle.</summary>
public sealed record OriginalWorldGeometryDrawDomain(float Left, float Top, float Width, float Height,
    float MinimumDepth, float MaximumDepth, bool ScissorEnabled, OriginalFinalEffectScissor ScissorRectangle,
    string SourceEvidence)
{
    internal void Validate()
    {
        if (!float.IsFinite(Left) || !float.IsFinite(Top) || !float.IsFinite(Width) || !float.IsFinite(Height) ||
            Width <= 0 || Height <= 0 || !float.IsFinite(MinimumDepth) || !float.IsFinite(MaximumDepth) ||
            MinimumDepth < 0 || MinimumDepth > 1 || MaximumDepth < 0 || MaximumDepth > 1 ||
            (ScissorEnabled && (ScissorRectangle.Width < 0 || ScissorRectangle.Height < 0)) ||
            string.IsNullOrWhiteSpace(SourceEvidence))
            throw new InvalidDataException("Geometry target scope needs the actual finite viewport/depth endpoints and current scissor state.");
    }
}

/// <summary>An actual device target row outside the named render graph, such
/// as the backend's current display surface. It owns its framebuffer and
/// borrows live surface views; graph identities are never fabricated.</summary>
public interface IOriginalWorldGeometryTargetDeviceRow
{
    Rid Framebuffer { get; }
    IReadOnlyList<Rid> ColorViews { get; }
    Rid DepthView { get; }
    void RequireCurrent();
}

public sealed record OriginalWorldGeometryTargetRow
{
    public OriginalWorldGeometryTargetRow(OriginalRenderTargetFrame frame,int targetStackIndex,
        OriginalWorldGeometryDrawDomain domain)
        =>(NamedFrame,TargetStackIndex,Domain)=(frame,targetStackIndex,domain);
    public OriginalWorldGeometryTargetRow(IOriginalWorldGeometryTargetDeviceRow actualDeviceRow,int targetStackIndex,
        OriginalWorldGeometryDrawDomain domain)
        =>(DeviceRow,TargetStackIndex,Domain)=(actualDeviceRow,targetStackIndex,domain);
    public OriginalRenderTargetFrame? NamedFrame { get; }
    public IOriginalWorldGeometryTargetDeviceRow? DeviceRow { get; }
    public OriginalRenderTargetFrame Frame => NamedFrame??throw new InvalidOperationException("The current display parent is an actual device row, not a named graph frame.");
    public Rid Framebuffer => NamedFrame?.Framebuffer??DeviceRow!.Framebuffer;
    public IReadOnlyList<Rid> ColorViews => NamedFrame?.ColorViews??DeviceRow!.ColorViews;
    public Rid DepthView => NamedFrame?.DepthView??DeviceRow!.DepthView;
    public int TargetStackIndex { get; init; }
    public OriginalWorldGeometryDrawDomain Domain { get; init; }
    public void RequireCurrent() => DeviceRow?.RequireCurrent();
}

/// <summary>Current explicit-backend framebuffer selection. This is neither
/// a geometry draw nor evidence that the intervening queue nodes returned.</summary>
public sealed class OriginalWorldGeometryTargetBinding
{
    internal OriginalWorldGeometryTargetBinding(OriginalWorldGeometryTargetScopeOwner owner, int node,
        OriginalMapSourceCreatedNativeSessionPreparedFrame accepted, OriginalWorldGeometryTargetRow row)
        => (Owner, SourceNode, Accepted, Row) = (owner, node, accepted, row);
    public OriginalWorldGeometryTargetScopeOwner Owner { get; }
    public int SourceNode { get; }
    public OriginalMapSourceCreatedNativeSessionPreparedFrame Accepted { get; }
    public OriginalWorldGeometryTargetRow Row { get; }
    public OriginalRenderTargetFrame Frame => Row.Frame;
    public OriginalWorldGeometryDrawDomain Domain => Row.Domain;
    public void RequireCurrent() => Owner.RequireBound(this);
    public OriginalArmy48TargetBinding CaptureArmy48Binding()
    {
        RequireCurrent();
        if (SourceNode != 48) throw new InvalidDataException("Army queues require the actual node48 selection.");
        return OriginalArmy48TargetBinding.CaptureActualBound(Accepted.Context, Frame, Owner.Frames.ArmySource,
            new(Domain.Left, Domain.Top, Domain.Width, Domain.Height, Domain.MinimumDepth, Domain.MaximumDepth,
                Domain.ScissorEnabled ? Domain.ScissorRectangle : null, Domain.SourceEvidence), RequireCurrent,
            () => Owner.RequireRetainedArmyTarget(this));
    }
}

/// <summary>Issued only after this owner's real TextureCopy returned Ok.
/// Submission is not a GPU fence/readback or a whole-geometry completion.</summary>
public sealed class OriginalWorldGeometryDepthSamplingView
{
    internal OriginalWorldGeometryDepthSamplingView(OriginalWorldGeometryTargetScopeOwner owner,
        OriginalMapSourceCreatedNativeSessionPreparedFrame accepted, int node,
        OriginalWorldGeometryDepthCopyCommand command, Rid primary, Rid sampling, Vector2I extent)
        => (Owner, Accepted, SourcePopNode, Command, PrimaryResource, ShaderView, Extent) =
            (owner, accepted, node, command, primary, sampling, extent);
    public OriginalWorldGeometryTargetScopeOwner Owner { get; }
    public OriginalMapSourceCreatedNativeSessionPreparedFrame Accepted { get; }
    public int SourcePopNode { get; }
    public OriginalWorldGeometryDepthCopyCommand Command { get; }
    public Rid PrimaryResource { get; }
    public Rid ShaderView { get; }
    public Vector2I Extent { get; }
    public long FrameNumber => Accepted.Context.FrameNumber;
    public string SourceEvidence => Owner.Source.SourceProvenance + ";actual-RD-TextureCopy/pop=" + SourcePopNode +
        ";D32SfloatS8Uint-port;primary=" + PrimaryResource.Id + ";sampling=" + ShaderView.Id + ";frame=" + FrameNumber;
    public void RequireCurrent() => Owner.RequireSampling(this);
}

public sealed class OriginalWorldGeometryTargetPopSubmission
{
    internal OriginalWorldGeometryTargetPopSubmission(int node, OriginalWorldGeometryCurrentPop recipe,
        OriginalWorldGeometryTargetRow current, OriginalWorldGeometryDepthSamplingView? copy)
        => (SourceNode, Recipe, CurrentTargetRow, DepthCopy) = (node, recipe, current, copy);
    public int SourceNode { get; }
    public OriginalWorldGeometryCurrentPop Recipe { get; }
    /// <summary>When ShouldPop is false, this remains the bound row.</summary>
    public OriginalWorldGeometryTargetRow CurrentTargetRow { get; }
    public OriginalWorldGeometryDepthSamplingView? DepthCopy { get; }
    public bool ProvesWholeGeometryCompleted => false;
}

/// <summary>Target-allocation lifetime for the independent E13 sample image
/// and the explicit backend's geometry target rows. Build allocates the port
/// copy image; it does not claim the original native creator ran. Draw owners
/// must borrow the current Frame/Domain, then retire before this owner/storage.
/// Pop restores attachments/rectangles only; raster/depth/stencil are untouched.</summary>
public sealed class OriginalWorldGeometryTargetScopeOwner : IAsyncDisposable
{
    private const uint DepthName = 0xE13DDB4F;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly OriginalRenderTargetDeviceTexture primary;
    private readonly OriginalWorldGeometryTargetScopeState state;
    private readonly OriginalWorldGeometryBindNode? rs4Bind;
    private Rid sampling;
    private bool retiring, disposed, faulted;
    private OriginalMapSourceCreatedNativeSessionPreparedFrame? accepted;
    private Func<OriginalWorldGeometryCurrentCallerFields>? readCurrent;
    private Action? requireCurrent;
    private OriginalWorldGeometryTargetBinding? binding;
    private OriginalWorldGeometryDepthSamplingView? publication;
    private OriginalWorldGeometryTargetScopeOwner(LoadedOriginalWorldGeometryStageSource source,
        OriginalWorldGeometryTargetFrames frames, OriginalRenderTargetDeviceTexture depth, Rid sample)
    {
        (Source, Frames, primary, sampling) = (source, frames, depth, sample);
        state = new(frames.Rs4 is not null);
        rs4Bind = frames.Rs4 is null ? null :
            OriginalWorldGeometryStageSource.ReadLoadedBindDeclaration(frames.Rs4.Source.Targets43);
    }
    public LoadedOriginalWorldGeometryStageSource Source { get; }
    public OriginalWorldGeometryTargetFrames Frames { get; }
    public OriginalWorldGeometryTargetRow CurrentTargetRow { get { RequireCurrent(); return state.Current; } }

    public static async Task<OriginalWorldGeometryTargetScopeOwner> BuildAsync(
        LoadedOriginalWorldGeometryStageSource source, OriginalWorldGeometryTargetFrames actualSharedFrames)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualSharedFrames);
        ValidateFrame(source.ArmyTargets48, actualSharedFrames.ArmyFrame);
        ValidateFrame(source.GeometryTargets53, actualSharedFrames.OrdinaryFrame);
        if (actualSharedFrames.Rs4 is { } rs4)
        {
            if (rs4.Source.Native.Locale != source.Native.Locale ||
                rs4.Source.Native.ExecutableSha256 != source.Native.ExecutableSha256)
                throw new InvalidDataException("RS4 target prefix and geometry scope need the same loaded executable profile.");
            ValidateFrame(OriginalWorldGeometryStageSource.ReadLoadedBindDeclaration(rs4.Source.Targets43), rs4.Frame);
            rs4.RequireCurrent(actualSharedFrames.OrdinaryFrame);
        }
        var depth = actualSharedFrames.OrdinaryFrame.Textures[DepthName];
        if (depth.StorageRid != actualSharedFrames.ArmyFrame.Textures[DepthName].StorageRid ||
            !actualSharedFrames.ArmyFrame.Textures.All(t => actualSharedFrames.OrdinaryFrame.Textures.TryGetValue(t.Key, out var same) &&
                same.StorageRid == t.Value.StorageRid && same.ExplicitExtent == t.Value.ExplicitExtent))
            throw new InvalidDataException("Geometry target scopes must borrow the same actual color/depth storage generation.");
        Rid copy = await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            ValidateDeviceFrame(rd, actualSharedFrames.ArmyFrame); ValidateDeviceFrame(rd, actualSharedFrames.OrdinaryFrame);
            ValidateDeviceDepth(rd, depth);
            using var format = new RDTextureFormat {
                Format = RenderingDevice.DataFormat.D32SfloatS8Uint, Width = checked((uint)depth.ExplicitExtent.X),
                Height = checked((uint)depth.ExplicitExtent.Y), Depth = 1, ArrayLayers = 1, Mipmaps = 1,
                TextureType = RenderingDevice.TextureType.Type2D, Samples = RenderingDevice.TextureSamples.Samples1,
                // Same real depth TextureCopy transport as the Early Mirror.
                // Godot needs this bit to classify depth copies; no draw here attaches it.
                UsageBits = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                    RenderingDevice.TextureUsageBits.CanCopyToBit | RenderingDevice.TextureUsageBits.CanCopyFromBit };
            using var view = new RDTextureView();
            Rid result = rd.TextureCreate(format, view);
            if (!result.IsValid) throw new InvalidDataException("Device rejected the independent geometry depth sample image.");
            return result;
        });
        return new(source, actualSharedFrames, depth, copy);
    }

    /// <summary>The caller provides the same accepted Camera/context, actual
    /// prior row and current Host/table getter. Nothing is inferred from Output73.</summary>
    public void BeginCurrent(OriginalMapSourceCreatedNativeSessionPreparedFrame actualAccepted,
        OriginalWorldGeometryTargetRow actualPriorRow, Func<OriginalWorldGeometryCurrentCallerFields> currentFields,
        Action requireActualAcceptedScope)
    {
        ArgumentNullException.ThrowIfNull(actualAccepted); ArgumentNullException.ThrowIfNull(actualPriorRow);
        ArgumentNullException.ThrowIfNull(currentFields); ArgumentNullException.ThrowIfNull(requireActualAcceptedScope);
        gate.Wait();
        try
        {
            RequireLive(); requireActualAcceptedScope(); actualPriorRow.Domain.Validate();
            actualPriorRow.RequireCurrent();
            if (accepted is not null || actualAccepted.Context.FrameNumber <= 0 || actualAccepted.Context.PreparedCamera is null ||
                actualAccepted.MainCamera.FrameNumber != actualAccepted.Context.FrameNumber ||
                actualAccepted.MainCamera.ExecutableLocale != Source.Native.Locale ||
                !ReferenceEquals(actualAccepted.Context.TerrainFrame, Frames.OrdinaryFrame) ||
                !actualPriorRow.Framebuffer.IsValid || actualPriorRow.TargetStackIndex < 0)
                throw new InvalidDataException("Geometry target entry needs the current accepted source/context, shared generation and actual prior row.");
            state.Begin(actualPriorRow);
            accepted = actualAccepted; readCurrent = currentFields; requireCurrent = requireActualAcceptedScope;
            binding = null; publication = null;
        }
        finally { gate.Release(); }
    }

    /// <summary>Selects a valid actual framebuffer row for explicit PSO draws.
    /// RenderingDevice has no separate global target-bind call. This does not
    /// begin/end an empty draw list, clear targets or submit any geometry.</summary>
    public async Task<OriginalWorldGeometryTargetBinding?> BindAsync(int sourceNode,
        OriginalWorldGeometryDrawDomain? actualPostBindDomain)
    {
        await gate.WaitAsync();
        try
        {
            RequireCurrent(); state.RequireNext(sourceNode);
            var node = sourceNode == 43 ? rs4Bind ??
                throw new InvalidDataException("This geometry generation has no loaded RS4 prefix.") : Source.BindNode(sourceNode);
            var fields = readCurrent!();
            // E931CE/D5 and E931DB/DE, paired SC E941BE..CE:
            // the same signed Q3F0/table gates precede target selection.
            if (fields.CurrentContextFlags3F0 < 0 || !fields.CurrentTablePresent)
            { state.SkipBind(sourceNode); return null; }
            ArgumentNullException.ThrowIfNull(actualPostBindDomain);
            actualPostBindDomain.Validate();
            _ = Source.PrepareNullOptionalHostTargets(node, fields.Host);
            var frame = sourceNode == 43 ? Frames.Rs4!.Frame : sourceNode == 48 ? Frames.ArmyFrame : Frames.OrdinaryFrame;
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd => { RequireCurrent(); ValidateDeviceFrame(rd, frame); return true; });
            var row = state.Push(sourceNode, frame, actualPostBindDomain);
            return binding = new(this, sourceNode, accepted!, row);
        }
        finally { gate.Release(); }
    }

    public async Task<OriginalWorldGeometryTargetPopSubmission> PopAsync(int sourceNode)
    {
        await gate.WaitAsync();
        try
        {
            RequireCurrent(); state.RequireNext(sourceNode);
            var fields = readCurrent!();
            var recipe = sourceNode == 47
                ? Frames.Rs4!.Source.PrepareCurrentPop(fields.CurrentContextFlags3F0, fields.CurrentTablePresent, fields.Host)
                : Source.PrepareCurrentPop(sourceNode, fields.CurrentContextFlags3F0, fields.CurrentTablePresent, fields.Host);
            OriginalWorldGeometryDepthSamplingView? copied = null;
            if (recipe.ShouldPop)
            {
                var parent = state.Parent;
                if (!recipe.CopiesDepthBeforePop)
                {
                    await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
                    { RequireCurrent(); ValidateDeviceRow(rd, parent); return true; });
                }
                else
                {
                    var commands = Source.PrepareDepthCopyCommands(sourceNode, new(primary.StorageRid.Id, sampling.Id, 1, 0, 1));
                    if (commands.Count != 1)
                        throw new InvalidDataException("This geometry scope requires its actual independent D1 copy before target pop.");
                    var command = commands[0];
                    await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
                    {
                        RequireCurrent(); ValidateDeviceDepth(rd, primary); ValidateDeviceRow(rd, parent);
                        if (!rd.TextureIsValid(sampling)) throw new ObjectDisposedException("Geometry depth sample image");
                        var error = rd.TextureCopy(primary.StorageRid, sampling, Vector3.Zero, Vector3.Zero,
                            new Vector3(primary.ExplicitExtent.X, primary.ExplicitExtent.Y, 1), 0, 0, 0, 0);
                        if (error != Error.Ok) throw new InvalidDataException("Geometry depth copy before pop" + sourceNode + " failed: " + error);
                        return true;
                    });
                    copied = new(this, accepted!, sourceNode, command, primary.StorageRid, sampling, primary.ExplicitExtent);
                }
            }
            var restored = state.Pop(sourceNode, recipe.ShouldPop);
            if (recipe.ShouldPop) binding = null;
            if (copied is not null) publication = copied;
            return new(sourceNode, recipe, restored, copied);
        }
        catch { faulted = true; publication = null; binding = null; throw; }
        finally { gate.Release(); }
    }

    public OriginalWorldGeometryDepthSamplingView RequireFinal59SamplingView()
    {
        RequireCurrent();
        var value = publication ?? throw new InvalidDataException("No actual depth copy has been submitted in this invocation.");
        if (value.SourcePopNode != 59 || !state.ReturnedToPrior)
            throw new InvalidDataException("Early final-depth sampling requires actual node59 copy and balanced target return, after the caller's57/58.");
        value.RequireCurrent(); return value;
    }

    /// <summary>Ends this target-row invocation only. The caller still owns
    /// geometry queue/terrain57/58 completion and borrowed draw retirement.</summary>
    public void EndCurrent()
    {
        gate.Wait();
        try { RequireCurrent(); state.End(); accepted = null; readCurrent = null; requireCurrent = null; binding = null; publication = null; }
        finally { gate.Release(); }
    }
    internal void RequireBound(OriginalWorldGeometryTargetBinding actual)
    {
        RequireCurrent();
        if (!ReferenceEquals(binding, actual) || !ReferenceEquals(actual.Accepted, accepted) || !ReferenceEquals(actual.Row, state.Current))
            throw new InvalidDataException("Geometry target binding was popped, replaced or belongs to another current scope.");
    }
    // Successful Army draws remain facts after pop52. They still borrow this
    // current scope's actual target generation until consumers finish.
    internal void RequireRetainedArmyTarget(OriginalWorldGeometryTargetBinding actual)
    {
        RequireCurrent();
        if (!ReferenceEquals(actual.Accepted, accepted) || actual.SourceNode != 48 ||
            !ReferenceEquals(actual.Frame, Frames.ArmyFrame))
            throw new InvalidDataException("Army completion belongs to another current scope or target generation.");
    }
    internal void RequireSampling(OriginalWorldGeometryDepthSamplingView actual)
    {
        RequireCurrent();
        if (!ReferenceEquals(publication, actual) || !ReferenceEquals(actual.Accepted, accepted) ||
            actual.PrimaryResource != primary.StorageRid || actual.ShaderView != sampling)
            throw new InvalidDataException("Depth sampling publication was replaced, failed, ended or retired.");
    }
    private void RequireLive()
    { ObjectDisposedException.ThrowIf(retiring || disposed, this); if (faulted) throw new InvalidOperationException("Failed geometry target scope must retire before reuse."); }
    private void RequireCurrent()
    { RequireLive(); if (accepted is null) throw new InvalidOperationException("Geometry target scope has no current invocation."); requireCurrent!(); }

    private static void ValidateFrame(OriginalWorldGeometryBindNode node, OriginalRenderTargetFrame frame)
    {
        if (frame.Source.Node.Index != node.NodeIndex || frame.Source.Flag != node.SourceFlag ||
            frame.ColorViews.Count != node.Colors.Count || !frame.Framebuffer.IsValid || !frame.DepthView.IsValid ||
            !frame.Source.ColorTargets.Select(t => (t.Slot, t.ObjectNameHash, t.SurfaceId))
                .SequenceEqual(node.Colors.Select(t => (t.Slot, t.ObjectNameHash, t.SurfaceId))) ||
            frame.Source.Graph.Source.ResourceUri != GameResourceId.Rdb("system", 0x024496EE).ToString() ||
            frame.Source.Graph.Source.DecodedSha256 != OriginalTerrainGbufferTargetSources.GraphSha256 ||
            frame.Source.TextureObjects.Source.ResourceUri != node.Depth.Texture.Source.Source.ResourceUri ||
            frame.Source.TextureObjects.Source.DecodedSha256 != node.Depth.Texture.Source.Source.DecodedSha256 ||
            frame.Source.DepthObjectNameHash != DepthName || frame.Source.DepthSurfaceId != 0)
            throw new InvalidDataException("Geometry scope requires its actual loaded48/53 framebuffer and source attachment order.");
        foreach (var attachment in node.Colors.Append(node.Depth))
        {
            if (!frame.Textures.TryGetValue(attachment.ObjectNameHash, out var actual) || !actual.StorageRid.IsValid ||
                actual.Source.PixelFormat != attachment.Texture.PixelFormat || actual.Source.Kind != attachment.Texture.Kind ||
                actual.Source.LevelNum != 1 || actual.Source.MinMipLevel != 0 || actual.Source.MultiSample != 0 ||
                actual.TargetFormat != OriginalRenderTargetFrame.ViewFormat(attachment.Formats.TargetViewFormat) ||
                actual.ExplicitExtent.X <= 0 || actual.ExplicitExtent.Y <= 0)
                throw new InvalidDataException("Geometry framebuffer differs from its current source texture/view metadata.");
        }
    }
    private static void ValidateDeviceFrame(RenderingDevice rd, OriginalRenderTargetFrame frame)
    {
        if (!rd.FramebufferIsValid(frame.Framebuffer) || frame.ColorViews.Any(t => !rd.TextureIsValid(t)) ||
            (frame.Source.DepthTarget is not null && !rd.TextureIsValid(frame.DepthView)))
            throw new ObjectDisposedException("Geometry target row framebuffer/views");
    }
    private static void ValidateDeviceRow(RenderingDevice rd,OriginalWorldGeometryTargetRow row)
    {
        row.RequireCurrent();
        if(!rd.FramebufferIsValid(row.Framebuffer)||row.ColorViews.Any(view=>!rd.TextureIsValid(view))||
            (row.DepthView.IsValid&&!rd.TextureIsValid(row.DepthView)))
            throw new ObjectDisposedException("Geometry current device target row framebuffer/views");
    }
    private static void ValidateDeviceDepth(RenderingDevice rd, OriginalRenderTargetDeviceTexture depth)
    {
        if (!rd.TextureIsValid(depth.StorageRid)) throw new ObjectDisposedException("Geometry primary depth");
        using var actual = rd.TextureGetFormat(depth.StorageRid);
        if (actual.Format != RenderingDevice.DataFormat.D32SfloatS8Uint || actual.Width != depth.ExplicitExtent.X ||
            actual.Height != depth.ExplicitExtent.Y || actual.Mipmaps != 1 || actual.ArrayLayers != 1 ||
            actual.Samples != RenderingDevice.TextureSamples.Samples1 ||
            !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.CanCopyFromBit))
            throw new InvalidDataException("Geometry depth needs the current exact copyable single-mip/layer D32SfloatS8Uint owner.");
    }
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (disposed) return;
            retiring = true; publication = null; binding = null;
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
            { if (sampling.IsValid) { rd.FreeRid(sampling); sampling = default; } return true; });
            accepted = null; readCurrent = null; requireCurrent = null; state.Retire(); disposed = true;
        }
        finally { gate.Release(); }
    }
}

