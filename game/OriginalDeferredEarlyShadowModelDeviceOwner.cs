using Godot;
using San14.Data;

/// <summary>Imported final primitive18 full table and its own state3 slot.
/// A short/null interface is an explicit source boundary, never a draw.</summary>
public sealed class OriginalDeferredEarlyShadowModelSource
{
    internal OriginalDeferredEarlyShadowModelSource(OriginalDeferredEarlyShadowModelDeviceOwner owner,
        OriginalNativeWorldShaderTableState final, LoadedModelMaterials model, OriginalMapStaticModelAsset? asset, int draw,
        OriginalDeferredEarlyShadowFinalInterface selected, LoadedOriginalDeferredEarlyShadowRenderer? renderer,
        OriginalArmySkinVertexProfile? armySkinProfile)
        => (Owner, FinalTable, Model, StaticAsset, DrawIndex, SelectedInterface, Renderer, ArmySkinProfile) = (owner, final, model, asset, draw, selected, renderer, armySkinProfile);
    public OriginalDeferredEarlyShadowModelDeviceOwner Owner { get; }
    public OriginalNativeWorldShaderTableState FinalTable { get; }
    public LoadedModelMaterials Model { get; }
    public OriginalMapStaticModelAsset? StaticAsset { get; }
    /// <summary>Compatibility for the existing static loader. Army/Flag callers use Model.</summary>
    public OriginalMapStaticModelAsset Asset => StaticAsset ?? throw new InvalidOperationException("This early19 source is an actual Army/Flag model, not a static placement asset.");
    public int DrawIndex { get; }
    public OriginalDeferredEarlyShadowFinalInterface SelectedInterface { get; }
    public LoadedOriginalDeferredEarlyShadowRenderer? Renderer { get; }
    public OriginalArmySkinVertexProfile? ArmySkinProfile { get; }
    public bool HasRenderer => SelectedInterface.Boundary == OriginalDeferredEarlyShadowInterfaceBoundary.RequiresRenderer;
    public string SourceEvidence => Owner.Source.SourceEvidence + ";actual-final-table=" + FinalTable.OrderedAllocationIdentity +
        ";model=" + Model.Source.ResourceUri + "@" + Model.Source.DecodedSha256 + "/draw" + DrawIndex +
        ";state3-provider=" + (SelectedInterface.Interface?.Provider?.Object.ObjectId.ToString("X8") ?? "null") + ";not-queue19-completion";
    public void RequireCurrent() => Owner.RequireSource(this);
}

/// <summary>Resolves the caller's current dimensions/pass through this
/// state3 renderer. It does not certify the caller's current feature writers.</summary>
public sealed class OriginalDeferredEarlyShadowModelProgramSelection
{
    internal OriginalDeferredEarlyShadowModelProgramSelection(OriginalDeferredEarlyShadowModelSource source,
        OriginalDeferredEarlyShadowProgram program) => (Source, Program) = (source, program);
    public OriginalDeferredEarlyShadowModelSource Source { get; }
    public OriginalDeferredEarlyShadowProgram Program { get; }
}

/// <summary>A real device draw prepared from complete current inputs. The
/// normal source/caster/viewport scope is supplied by the caller. This packet
/// has not submitted a draw and cannot acknowledge a whole or empty queue.</summary>
public sealed class OriginalDeferredEarlyShadowModelCurrentDraw
{
    private readonly OriginalDeferredEarlyShadowModelDeviceOwner.DrawValue value;
    private readonly Action requireActualCurrentParameters;
    private readonly OriginalModelStageInputs vertex, pixel;
    internal OriginalDeferredEarlyShadowModelCurrentDraw(OriginalDeferredEarlyShadowModelDeviceOwner owner,
        OriginalDeferredEarlyShadowModelDeviceOwner.DrawValue value,
        OriginalDeferredEarlyShadowModelProgramSelection selection, OriginalDeferredEarlyShadowTargetBinding binding,
        OriginalModelStageInputs vertex, OriginalModelStageInputs pixel, Action requireActualCurrentParameters)
        => (Owner, this.value, Selection, TargetBinding, this.vertex, this.pixel, this.requireActualCurrentParameters) =
            (owner, value, selection, binding, vertex, pixel, requireActualCurrentParameters);
    public OriginalDeferredEarlyShadowModelDeviceOwner Owner { get; }
    public OriginalDeferredEarlyShadowModelProgramSelection Selection { get; }
    public OriginalDeferredEarlyShadowTargetBinding TargetBinding { get; }
    public OriginalModelShaderProgram Program => value.Program;
    public OriginalModelGeometryDevice Geometry => value.Geometry;
    public bool ProvesQueue19Completed => false;
    public void RequireCurrent()
    {
        Selection.Source.RequireCurrent(); Owner.RequireDraw(value); TargetBinding.RequireCurrent(); requireActualCurrentParameters();
        Owner.RequireCurrentVertexTransport(Selection, value.Program, vertex);
    }
    /// <summary>One legal update/list/draw/end command. Godot forbids public
    /// BufferUpdate while a draw list is open. The shared physical CB is
    /// updated before this command's independent list; caller supplies the
    /// real current draw domain and its source validation.</summary>
    public Task<OriginalModelConstantUpdateStatistics> SubmitCurrentAsync(
        OriginalWorldGeometryDrawDomain actualDrawDomain, Action requireActualCurrentDrawDomain)
        => Owner.SubmitCurrentAsync(this, value, vertex, pixel, actualDrawDomain, requireActualCurrentDrawDomain);
}

/// <summary>Source-generation cache dedicated to early19's final state3
/// interfaces. Borrows the source repository/compiler and target generations;
/// owns only original device programs, geometry and draws. No state18 source
/// packet, preview projection, replacement PS or source-ready Boolean enters.</summary>
public sealed partial class OriginalDeferredEarlyShadowModelDeviceOwner : IAsyncDisposable
{
    internal sealed record DrawValue(OriginalModelShaderProgram Program, OriginalModelGeometryDevice Geometry,
        OriginalDeferredEarlyShadowTargetOwner TargetOwner, int DrawIndex, OriginalModelRasterInputs Raster,
        IReadOnlyList<OriginalModelDeviceBinding> VertexResources, IReadOnlyList<OriginalModelDeviceBinding> PixelResources,
        OriginalModelDraw Draw);
    private readonly OriginalGameRepository repository;
    private readonly Vkd3dShaderCompiler compiler;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly LoadedOriginalDeferredEarlyShadowSelectionSource selectionSource;
    private readonly Dictionary<LoadedModelMaterials, Dictionary<(OriginalNativeWorldShaderTableState, int), OriginalDeferredEarlyShadowModelSource>> sources = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<OriginalNamedModelShader, LoadedOriginalDeferredEarlyShadowRenderer> renderers = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<LoadedOriginalDeferredEarlyShadowRenderer, OriginalDeferredEarlyShadowSelectionState> selections = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(LoadedOriginalDeferredEarlyShadowRenderer, int, OriginalArmySkinVertexProfile?), OriginalModelShaderProgram> programs = new();
    private readonly Dictionary<(LoadedModelMaterials, LoadedShaderContainer, int, OriginalArmySkinVertexProfile?), OriginalModelGeometryDevice> geometry = new();
    private readonly Dictionary<OriginalDeferredEarlyShadowTargetOwner, OriginalDeferredEarlyShadowTargetFrameLoan> targets = new(ReferenceEqualityComparer.Instance);
    private readonly List<DrawValue> draws = [];
    private bool retiring, disposed;
    public OriginalDeferredEarlyShadowModelDeviceOwner(OriginalGameRepository repository,
        LoadedOriginalDeferredEarlyShadowQueueSource source, Vkd3dShaderCompiler borrowedCompiler)
    {
        (this.repository, Source, compiler) = (repository ?? throw new ArgumentNullException(nameof(repository)),
            source ?? throw new ArgumentNullException(nameof(source)), borrowedCompiler ?? throw new ArgumentNullException(nameof(borrowedCompiler)));
        selectionSource=LoadedOriginalDeferredEarlyShadowSelectionSource.Load(repository,Source.Native.Locale);
    }
    public LoadedOriginalDeferredEarlyShadowQueueSource Source { get; }
    public int ProgramCount => programs.Count;
    public int GeometryCount => geometry.Count;
    public int DrawCount => draws.Count;

    /// <summary>Load/import once for the actual primitive's final table.
    /// Caller obtains it from LoadFinalOwnedTable(asset,draw), including its
    /// real material-override policy; Main visibility is not required here.</summary>
    public OriginalDeferredEarlyShadowModelSource LoadSource(OriginalNativeWorldShaderTableState actualFinalTable,
        OriginalMapStaticModelAsset actualAsset, int actualDrawIndex)
    {
        ArgumentNullException.ThrowIfNull(actualFinalTable); ArgumentNullException.ThrowIfNull(actualAsset);
        gate.Wait();
        try
        {
            RequireLive();
            if (!actualAsset.Draws.Any(draw => draw.Index == actualDrawIndex))
                throw new InvalidDataException("Early19 source draw is absent from this actual loaded Model.");
            return LoadModelSource(actualFinalTable, actualAsset.TextureResources.Model, actualDrawIndex, actualAsset);
        }
        finally { gate.Release(); }
    }
    public OriginalDeferredEarlyShadowModelProgramSelection SelectCurrentProgram(OriginalDeferredEarlyShadowModelSource actualSource,
        ReadOnlySpan<uint> actualDimensions7, uint actualPass)
    {
        RequireSource(actualSource);
        if (actualSource.Renderer is not { } renderer)
            throw new InvalidOperationException("Actual short/null state3 interface has no device program; its source skip is not a draw.");
        var program = renderer.SelectProgram(actualDimensions7, actualPass);
        if (program.VertexProgram is null || program.DeclaredColorLocations.Count != 0 || program.DeclaresFragmentDepth)
            throw new NotSupportedException("Early19 Model component requires an actual VS and absent/zero-color PS without fragment-depth output.");
        return new(actualSource, program);
    }
    /// <summary>The actual state3 provider owns one retained seven-index and
    /// variant cache. Current draw writers update it; requests and primitives
    /// never recreate the renderer's initialized selection state.</summary>
    public OriginalDeferredEarlyShadowCurrentSelection PrepareCurrentSelection(
        OriginalDeferredEarlyShadowModelSource actualSource,OriginalModelMatrixBindingInput actualType17,
        uint actualShadowCpu20,byte actualFrontend1BE,byte actualFrontend1C0)
    {
        gate.Wait();
        try
        {
            RequireSource(actualSource);
            var renderer=actualSource.Renderer??throw new InvalidOperationException("A skipped state3 interface has no current renderer selection.");
            if(!selections.TryGetValue(renderer,out var state))
                selections.Add(renderer,state=selectionSource.CreateOwnedRenderer(renderer));
            return state.PrepareCurrent(actualType17,actualShadowCpu20,actualFrontend1BE,actualFrontend1C0);
        }
        finally {gate.Release();}
    }
    public OriginalDeferredEarlyShadowModelProgramSelection SelectCurrentProgram(
        OriginalDeferredEarlyShadowModelSource actualSource,OriginalDeferredEarlyShadowCurrentSelection current,uint actualPass)
    {
        if(!ReferenceEquals(current.State.Renderer,actualSource.Renderer))
            throw new InvalidDataException("Current state3 selection belongs to another actual provider.");
        return SelectCurrentProgram(actualSource,current.Indices.ToArray(),actualPass);
    }
    public async Task<OriginalModelShaderProgram> GetProgramAsync(OriginalDeferredEarlyShadowModelProgramSelection actualSelection)
    {
        ArgumentNullException.ThrowIfNull(actualSelection);
        await gate.WaitAsync();
        try { RequireSource(actualSelection.Source); return await ProgramAsync(actualSelection); }
        finally { gate.Release(); }
    }
    private async Task<OriginalModelShaderProgram> ProgramAsync(OriginalDeferredEarlyShadowModelProgramSelection selection)
    {
        var renderer = selection.Source.Renderer!; var key = (renderer, selection.Program.CombinationIndex, selection.Source.ArmySkinProfile);
        if (programs.TryGetValue(key, out var cached)) return cached;
        var program = await OriginalModelShaderProgram.BuildPreparedAsync(PrepareProgramSourceCore(selection));
        try
        {
            if (program.VertexProgramIndex != selection.Program.VertexProgram ||
                program.PixelStage?.ProgramIndex != selection.Program.PixelProgram || program.ColorAttachmentCount != 0)
                throw new InvalidDataException("Early19 device stages differ from their actual state3 combination.");
            programs.Add(key, program); return program;
        }
        catch { await program.DisposeAsync(); throw; }
    }
    public async Task<OriginalDeferredEarlyShadowModelCurrentDraw> PrepareCurrentDrawAsync(
        OriginalDeferredEarlyShadowModelProgramSelection actualSelection, OriginalDeferredEarlyShadowTargetBinding actualBinding,
        OriginalModelStageInputs actualCompleteVertex, OriginalModelStageInputs actualCompletePixel,
        OriginalModelRasterInputs actualRaster, Action requireActualCurrentParameters)
    {
        ArgumentNullException.ThrowIfNull(actualSelection); ArgumentNullException.ThrowIfNull(actualBinding);
        ArgumentNullException.ThrowIfNull(actualRaster); ArgumentNullException.ThrowIfNull(requireActualCurrentParameters);
        await gate.WaitAsync();
        try
        {
            RequireSource(actualSelection.Source); actualBinding.RequireCurrent(); requireActualCurrentParameters();
            if (!ReferenceEquals(actualBinding.Owner.Source, Source.Predecessor) || actualBinding.Frame.ColorViews.Count != 0 ||
                actualRaster.Attachments is not { Count: 0 })
                throw new InvalidDataException("Early19 draws require their own current EA target generation and zero color attachment raster.");
            var program = await ProgramAsync(actualSelection); var model = actualSelection.Source.Model;
            RequireCurrentVertexTransport(actualSelection, program, actualCompleteVertex);
            var key = (model, actualSelection.Source.Renderer!.Shader, program.VertexProgramIndex, actualSelection.Source.ArmySkinProfile);
            if (!geometry.TryGetValue(key, out var uploaded))
            {
                var resolution = OriginalModelVertexInputs.Resolve(model.Geometry, program.VertexDeviceTransportSignatures, program.VertexBindings.Interface);
                var singleInstanceStreams = actualSelection.Source.ArmySkinProfile is not null
                    ? OriginalArmySkinVertexAdapter.SingleInstanceSourceStreams(model.Geometry, actualSelection.Source.DrawIndex) : null;
                var packet = OriginalModelDeviceGeometry.Prepare(model.Source.Bytes, model.Source.DecodedSha256, resolution.Bindings, singleInstanceStreams);
                uploaded = actualSelection.Source.StaticAsset is null
                    ? await OriginalModelGeometryDevice.BuildSelectedInputStreamsAsync(model.Source.ResourceUri, packet)
                    : await OriginalModelGeometryDevice.BuildAsync(model.Source.ResourceUri, packet);
                geometry.Add(key, uploaded);
            }
            var vertex = Snapshot(actualCompleteVertex); var pixel = Snapshot(actualCompletePixel);
            var prior = draws.FirstOrDefault(value => ReferenceEquals(value.Program, program) && ReferenceEquals(value.Geometry, uploaded) &&
                ReferenceEquals(value.TargetOwner, actualBinding.Owner) && value.DrawIndex == actualSelection.Source.DrawIndex &&
                SameRaster(value.Raster, actualRaster) && value.VertexResources.SequenceEqual(vertex.Resources) && value.PixelResources.SequenceEqual(pixel.Resources));
            DrawValue value;
            if (prior is not null)
            {
                // OriginalDraw checks every selected RDEF CB, including absent PS.
                _ = prior.Draw.SnapshotUpdate(vertex.Constants, pixel.Constants); value = prior;
            }
            else
            {
                if (!targets.TryGetValue(actualBinding.Owner, out var loan)) targets.Add(actualBinding.Owner, loan = actualBinding.Owner.BorrowFrame());
                var frame = loan.Frame;
                var target = new OriginalModelDrawTarget(frame.Framebuffer, frame.ColorViews, frame.DepthView,
                    frame.Textures.Values.Select(t => t.StorageRid).Distinct().ToArray(), frame.Source.Graph.Source.ResourceUri);
                var raster = actualRaster with { Attachments = Array.AsReadOnly(actualRaster.Attachments.ToArray()) };
                var draw = await OriginalModelDraw.BuildAsync(program, uploaded, actualSelection.Source.DrawIndex, target, vertex, pixel, raster);
                value = new(program, uploaded, actualBinding.Owner, actualSelection.Source.DrawIndex, raster, vertex.Resources, pixel.Resources, draw); draws.Add(value);
            }
            return new(this, value, actualSelection, actualBinding, vertex, pixel, requireActualCurrentParameters);
        }
        finally { gate.Release(); }
    }
    private static OriginalModelStageInputs Snapshot(OriginalModelStageInputs actual)
    {
        ArgumentNullException.ThrowIfNull(actual); ArgumentNullException.ThrowIfNull(actual.Constants); ArgumentNullException.ThrowIfNull(actual.Resources);
        return new(actual.Constants.ToDictionary(kv => kv.Key, kv => (ReadOnlyMemory<byte>)kv.Value.ToArray()), Array.AsReadOnly(actual.Resources.ToArray()));
    }
    private static bool SameRaster(OriginalModelRasterInputs left, OriginalModelRasterInputs right) =>
        left.Cull == right.Cull && left.FrontFace == right.FrontFace && left.DepthTest == right.DepthTest && left.DepthWrite == right.DepthWrite &&
        left.DepthCompare == right.DepthCompare && left.Attachments.SequenceEqual(right.Attachments) &&
        (left.NativeStencil is null ? right.NativeStencil is null : left.NativeStencil.SamePipeline(right.NativeStencil));
    internal void RequireSource(OriginalDeferredEarlyShadowModelSource actual)
    {
        RequireLive(); ArgumentNullException.ThrowIfNull(actual);
        if (!ReferenceEquals(actual.Owner, this)) throw new InvalidDataException("Early19 source belongs to another source/device generation.");
        actual.FinalTable.Registry.RequireCurrent(actual.FinalTable);
        if (!sources.TryGetValue(actual.Model, out var rows) ||
            !rows.TryGetValue((actual.FinalTable, actual.DrawIndex), out var retained) || !ReferenceEquals(retained, actual))
            throw new ObjectDisposedException("Early19 imported model source");
    }
    internal void RequireDraw(DrawValue actual)
    {
        RequireLive(); if (!draws.Contains(actual)) throw new ObjectDisposedException("Early19 prepared draw target/resources");
        actual.Draw.RequireDrawLive(); targets[actual.TargetOwner].RequireCurrent();
    }
    internal async Task<OriginalModelConstantUpdateStatistics> SubmitCurrentAsync(
        OriginalDeferredEarlyShadowModelCurrentDraw actual, DrawValue value,
        OriginalModelStageInputs vertex, OriginalModelStageInputs pixel,
        OriginalWorldGeometryDrawDomain actualDomain, Action requireActualCurrentDrawDomain)
    {
        ArgumentNullException.ThrowIfNull(actualDomain); ArgumentNullException.ThrowIfNull(requireActualCurrentDrawDomain);
        await gate.WaitAsync();
        try
        {
            actual.RequireCurrent(); requireActualCurrentDrawDomain(); actualDomain.Validate();
            if (!ReferenceEquals(actualDomain, actual.TargetBinding.Domain))
                throw new InvalidDataException("Early19 draw domain must be the actual current queue19 target-row publication.");
            if (actualDomain.MinimumDepth != 0 || actualDomain.MaximumDepth != 1)
                throw new NotSupportedException("Early19 backend requires the actual source viewport depth range0..1; it cannot default unknown endpoints.");
            int Integer(float field) => field == MathF.Truncate(field) ? checked((int)field) :
                throw new NotSupportedException("Early19 backend closes the actual integer source viewport rectangle.");
            var viewport = new Rect2I(Integer(actualDomain.Left), Integer(actualDomain.Top), Integer(actualDomain.Width), Integer(actualDomain.Height));
            var extent = value.TargetOwner.DepthTexture.ExplicitExtent;
            if (viewport.Position.X < 0 || viewport.Position.Y < 0 || viewport.End.X > extent.X || viewport.End.Y > extent.Y)
                throw new NotSupportedException("Early19 current viewport must lie within the actual EA target extent.");
            Rect2I? scissor = actualDomain.ScissorEnabled ? new Rect2I(actualDomain.ScissorRectangle.Left,
                actualDomain.ScissorRectangle.Top, actualDomain.ScissorRectangle.Width, actualDomain.ScissorRectangle.Height).Intersection(viewport) : null;
            if (scissor is { } clip && (clip.Size.X <= 0 || clip.Size.Y <= 0))
                throw new NotSupportedException("Early19 empty scissor requires its separate no-fragment backend transport; zero Godot scissor cannot replace it.");
            var inputs = value.Draw.SnapshotUpdate(vertex.Constants, pixel.Constants);
            return await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                actual.RequireCurrent(); requireActualCurrentDrawDomain();
                if (!ReferenceEquals(actualDomain, actual.TargetBinding.Domain))
                    throw new InvalidDataException("Early19 current draw domain changed before its actual device command.");
                // BufferUpdate is legal here only before opening our list.
                // Distinct sequential lists preserve graph dependencies on
                // the shared CB: uploadA -> drawA -> uploadB -> drawB.
                var written = value.Draw.UpdateOnRenderThread(rd, inputs.Vertex, inputs.Pixel);
                long list = rd.DrawListBegin(value.Draw.Target.Framebuffer, RenderingDevice.DrawFlags.DefaultAll,
                    region: new Rect2(viewport.Position, viewport.Size));
                try
                {
                    if (scissor is { } clipped) rd.DrawListEnableScissor(list, new Rect2I(clipped.Position - viewport.Position, clipped.Size));
                    else rd.DrawListDisableScissor(list);
                    value.Draw.DrawInList(rd, list);
                }
                finally { rd.DrawListEnd(); }
                return written;
            });
        }
        finally { gate.Release(); }
    }
    private void RequireLive() => ObjectDisposedException.ThrowIf(retiring || disposed, this);
    /// <summary>Await before the caller retires borrowed target/material
    /// resources. Programs/geometry can remain in the loaded-generation cache.</summary>
    public async Task RetireTargetAsync(OriginalDeferredEarlyShadowTargetOwner actualTarget)
    {
        await gate.WaitAsync();
        try
        {
            RequireLive();
            for (int i = draws.Count - 1; i >= 0; i--)
                if (ReferenceEquals(draws[i].TargetOwner, actualTarget)) { await draws[i].Draw.DisposeAsync(); draws.RemoveAt(i); }
            if (targets.TryGetValue(actualTarget, out var loan)) { loan.Dispose(); targets.Remove(actualTarget); }
        }
        finally { gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (disposed) return; retiring = true;
            while (draws.Count != 0) { await draws[^1].Draw.DisposeAsync(); draws.RemoveAt(draws.Count - 1); }
            foreach (var key in programs.Keys.ToArray()) { await programs[key].DisposeAsync(); programs.Remove(key); }
            foreach (var key in geometry.Keys.ToArray()) { await geometry[key].DisposeAsync(); geometry.Remove(key); }
            foreach (var key in targets.Keys.ToArray()) { targets[key].Dispose(); targets.Remove(key); }
            sources.Clear(); selections.Clear(); renderers.Clear(); disposed = true;
        }
        finally { gate.Release(); }
    }
}
