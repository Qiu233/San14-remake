using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data;
using San14.Data.Formats;

/// <summary>Explicit device-cache limits. Texture bytes retain the encoded source
/// mip chains; geometry is a conservative source-buffer/window bound. A budget
/// failure rejects the whole frame before uploads, rather than dropping models.</summary>
public sealed record NativeMapModelPopulationUploadLimits(long MaximumTextureBytes,
    long MaximumGeometryBytes, int MaximumPrograms, int MaximumVisibleDrawPackets);
public sealed record NativeMapModelPopulationStatistics(long AcceptedFrames, long FailedFrames,
    int PopulationInstances, int VisibleInstances, int VisibleDrawPackets, int ProgramCount,
    int GeometryCount, int TextureCount, int SamplerCount, int DrawCount, long DrawCreationCount,
    long ReservedTextureBytes, long ReservedGeometryBytes)
{
    public int LastConstantBufferUpdates { get; init; }
    public long LastConstantBufferUpdatedBytes { get; init; }
    public long FrameBatchDispatchCount { get; init; }
    public double LastPreparationMilliseconds { get; init; }
    public double LastGpuSubmissionMilliseconds { get; init; }
}
public sealed record NativeMapModelPopulationAcceptedFrame(NativeMapFrameContext Context,
    MapStaticModelPreparedFrame Frame, MapMaterialCachePrepared Materials,
    NativeMapModelPopulationStatistics Statistics)
{
    public IReadOnlyList<DeferredSourceMode16Candidate>? CurrentViewFamilyCandidates { get; init; }
    public NativeGeometryStencilCompletedFrame? NativeStencilSubmission { get; init; }
    public ModelGeometrySubmittedFrame? GeometrySubmission { get; init; }
    public NativeMapStaticQueuesCompletedFrame? StaticQueueSubmission { get; init; }
}

/// <summary>One complete scene model consumer for AfterTerrain. Owns one model
/// layer and its shared original programs, raw buffers, source texture mip chains
/// and samplers. Every render uses the accepted terrain/deferred Camera, current
/// complete population and explicit source/runtime state. Target retirement frees
/// dependent draws while retaining source caches. The compiler stays borrowed.</summary>
public sealed class NativeMapModelPopulationRenderer : IAsyncDisposable
{
    private readonly ModelGbufferLayer _layer;
    private readonly MapStaticModelAssembler _assembler;
    private readonly MapMaterialCacheFrameValidator _materialValidator;
    private readonly MapMaterialCacheSource _materialCache;
    private readonly Func<NativeMapFrameContext, MapStaticModelInstanceSet> _population;
    private readonly Func<MapStaticModelDrawContext, MapStaticModelDrawRuntime> _runtime;
    private readonly Action<MapStaticModelPreparedFrame>? _commitAcceptedState;
    private readonly Func<NativeMapFrameContext, MapStaticModelInstanceSet,
        MapStaticModelCamera, MapStaticModelVisibleSet>? _visibilityPlanner;
    private readonly NativeMapModelPopulationUploadLimits _limits;
    private readonly Dictionary<(string Source, int Image, uint View), long> _textures = [];
    private readonly Dictionary<(string Model, string Shader, int Combination), long> _geometry = [];
    private readonly HashSet<(string Source, int Combination)> _programs = [];
    private readonly ConditionalWeakTable<LoadedRdbAsset, SourceIdentity> _sourceIdentities = new();
    private sealed record SourceIdentity(string Value);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetime = new();
    private int _operations;
    private bool _retiring;
    private TaskCompletionSource? _drained;
    private Task? _disposal;
    private long _acceptedFrames, _failedFrames;
    private int _populationCount, _visibleCount, _packetCount;
    private double _preparationMilliseconds, _gpuSubmissionMilliseconds;
    public NativeMapModelPopulationAcceptedFrame? LastAcceptedFrame { get; private set; }
    public string? LastFailure { get; private set; }
    public Func<NativeMapFrameContext,MapStaticModelPreparedFrame,NativeGeometryStencilSubmission>? NativeStencilPreparation { get; set; }
    public MapMaterialCacheSource MaterialCache => _materialCache;
    public NativeMapModelPopulationStatistics Statistics => new(_acceptedFrames, _failedFrames,
        _populationCount, _visibleCount, _packetCount, _layer.ProgramCount, _layer.GeometryCount,
        _layer.TextureCount, _layer.SamplerCount, _layer.DrawCount, _layer.DrawCreationCount,
        _textures.Values.Sum(), _geometry.Values.Sum())
    {
        LastConstantBufferUpdates = _layer.LastConstantUpdates.BufferUpdates,
        LastConstantBufferUpdatedBytes = _layer.LastConstantUpdates.UpdatedBytes,
        FrameBatchDispatchCount = _layer.FrameBatchDispatchCount,
        LastPreparationMilliseconds = _preparationMilliseconds,
        LastGpuSubmissionMilliseconds = _gpuSubmissionMilliseconds
    };

    private NativeMapModelPopulationRenderer(Vkd3dShaderCompiler compiler, string sourceRevision,
        MapMaterialCacheSource initializedMaterialCache,
        Func<MapStaticModelAsset, int, MapStaticModelDrawSource> loadDrawSource,
        Func<NativeMapFrameContext, MapStaticModelInstanceSet> currentPopulation,
        Func<MapStaticModelDrawContext, MapStaticModelDrawRuntime> runtime,
        NativeMapModelPopulationUploadLimits limits,
        Action<MapStaticModelPreparedFrame>? commitAcceptedState,
        Func<NativeMapFrameContext, MapStaticModelInstanceSet,
            MapStaticModelCamera, MapStaticModelVisibleSet>? visibilityPlanner)
    {
        _layer = ModelGbufferLayer.Create(compiler);
        _assembler = new(sourceRevision, loadDrawSource);
        _materialValidator = new(sourceRevision);
        _materialCache = initializedMaterialCache; _population = currentPopulation; _runtime = runtime;
        _limits = limits; _commitAcceptedState = commitAcceptedState;
        _visibilityPlanner = visibilityPlanner;
    }

    public static NativeMapModelPopulationRenderer Create(Vkd3dShaderCompiler borrowedCompiler,
        string sourceRevision, MapMaterialCacheSource initializedMaterialCache,
        Func<MapStaticModelAsset, int, MapStaticModelDrawSource> loadDrawSource,
        Func<NativeMapFrameContext, MapStaticModelInstanceSet> currentPopulation,
        Func<MapStaticModelDrawContext, MapStaticModelDrawRuntime> runtime,
        NativeMapModelPopulationUploadLimits explicitUploadLimits,
        Action<MapStaticModelPreparedFrame>? commitAcceptedState = null,
        Func<NativeMapFrameContext, MapStaticModelInstanceSet,
            MapStaticModelCamera, MapStaticModelVisibleSet>? visibilityPlanner = null)
    {
        ArgumentNullException.ThrowIfNull(borrowedCompiler); ArgumentException.ThrowIfNullOrWhiteSpace(sourceRevision);
        ArgumentNullException.ThrowIfNull(initializedMaterialCache); ArgumentNullException.ThrowIfNull(loadDrawSource);
        ArgumentNullException.ThrowIfNull(currentPopulation); ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(explicitUploadLimits);
        if (explicitUploadLimits.MaximumTextureBytes <= 0 || explicitUploadLimits.MaximumGeometryBytes <= 0 ||
            explicitUploadLimits.MaximumPrograms <= 0 || explicitUploadLimits.MaximumVisibleDrawPackets <= 0)
            throw new InvalidDataException("Native population requires explicit positive source upload limits.");
        _ = initializedMaterialCache.Table; // Revalidate canonical shader and initialized table bytes.
        return new(borrowedCompiler, sourceRevision, initializedMaterialCache, loadDrawSource,
            currentPopulation, runtime, explicitUploadLimits, commitAcceptedState, visibilityPlanner);
    }

    /// <summary>Current-owned production binding. Captures one prepared source
    /// population frame per accepted native context, checking its Main Camera
    /// frame/revision and every early matrix word. Camera ticking and source
    /// visibility publication occurred once before the terrain submission.</summary>
    public static NativeMapModelPopulationRenderer CreateForCurrentOwnedSource(Vkd3dShaderCompiler borrowedCompiler,
        string sourceRevision, MapMaterialCacheSource initializedMaterialCache,
        Func<MapStaticModelAsset, int, MapStaticModelDrawSource> loadDrawSource,
        Func<NativeMapFrameContext, MapStaticModelCurrentOwnedFrame> currentPreparedFrame,
        Func<MapStaticModelDrawContext, MapStaticModelDrawRuntime> runtime,
        NativeMapModelPopulationUploadLimits explicitUploadLimits,
        Action<MapStaticModelPreparedFrame>? commitAcceptedState = null)
    {
        ArgumentNullException.ThrowIfNull(currentPreparedFrame);
        var frames = new ConditionalWeakTable<NativeMapFrameContext, MapStaticModelCurrentOwnedFrame>();
        MapStaticModelCurrentOwnedFrame Resolve(NativeMapFrameContext context) => frames.GetValue(context, c =>
        {
            var frame = currentPreparedFrame(c) ?? throw new InvalidDataException("Current owned model source frame is unavailable.");
            if (frame.SourceRevision != sourceRevision || frame.FrameNumber != c.FrameNumber || c.PreparedCamera is null)
                throw new InvalidDataException("Current owned model source frame differs from its accepted native revision/frame/Camera.");
            var camera = frame.PreparedCamera;
            bool Same(Matrix4x4 a, Matrix4x4 b) => Rows(a).Select(BitConverter.SingleToUInt32Bits)
                .SequenceEqual(Rows(b).Select(BitConverter.SingleToUInt32Bits));
            if (!Same(camera.SourceWorldToView, c.PreparedCamera.SourceWorldToView) ||
                !Same(camera.SourceWorldToProjection, c.PreparedCamera.SourceWorldToProjection) ||
                !new[] {camera.SourceEye.X,camera.SourceEye.Y,camera.SourceEye.Z,camera.SourceEye.W}.Select(BitConverter.SingleToUInt32Bits)
                    .SequenceEqual(new[] {c.PreparedCamera.SourceEye.X,c.PreparedCamera.SourceEye.Y,c.PreparedCamera.SourceEye.Z,c.PreparedCamera.SourceEye.W}.Select(BitConverter.SingleToUInt32Bits)))
                throw new InvalidDataException("Current owned Main Camera words differ from the accepted terrain/model Camera.");
            return frame;
        });
        return Create(borrowedCompiler, sourceRevision, initializedMaterialCache, loadDrawSource,
            context => Resolve(context).Population, runtime, explicitUploadLimits, commitAcceptedState,
            (context, _, _) => Resolve(context).Visibility);
    }

    /// <summary>Can be assigned directly to the scene's AfterTerrain callback.
    /// Returned packets and material consumers belong to this accepted target,
    /// Camera and frame number. Commit callbacks must be nonthrowing cache stores.</summary>
    public Task<NativeMapModelPopulationAcceptedFrame> PrepareAndDrawAsync(NativeMapFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context); Accept(); return RenderAcceptedAsync(context);
    }

    private async Task<NativeMapModelPopulationAcceptedFrame> RenderAcceptedAsync(NativeMapFrameContext context)
    {
        await _gate.WaitAsync();
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var current = PrepareCurrentFrame(context);
            var prepared = current.Prepared;
            var materials = current.Materials;
            var native = NativeStencilPreparation?.Invoke(context, prepared);
            if (native is not null && !ReferenceEquals(native.Context,context))
                throw new InvalidDataException("Native geometry stencil preparation belongs to another scene context.");
            var submittedPackets = native?.Packets ?? prepared.Packets;
            ReserveUploads(submittedPackets);
            _preparationMilliseconds = watch.Elapsed.TotalMilliseconds; watch.Restart();
            var submission = await _layer.RenderAsync(context.TerrainFrame, submittedPackets);
            var stencilCompletion = native?.Complete(submission);
            _gpuSubmissionMilliseconds = watch.Elapsed.TotalMilliseconds;
            _commitAcceptedState?.Invoke(prepared);
            _acceptedFrames++; _populationCount = current.Population.Instances.Count;
            _visibleCount = prepared.Visibility.Visible.Count; _packetCount = prepared.Packets.Count;
            LastFailure = null;
            return LastAcceptedFrame = new(context, prepared, materials, Statistics)
            { CurrentViewFamilyCandidates=prepared.CurrentViewFamilyCandidates, NativeStencilSubmission=stencilCompletion,GeometrySubmission=submission };
        }
        catch (Exception error) { _failedFrames++; LastFailure = error.Message; throw; }
        finally { _gate.Release(); Complete(); }
    }

    internal sealed record PreparedCurrentFrame(MapStaticModelInstanceSet Population,
        MapStaticModelPreparedFrame Prepared, MapMaterialCachePrepared Materials);

    private PreparedCurrentFrame PrepareCurrentFrame(NativeMapFrameContext context)
    {
            if (context.FrameNumber <= 0 || context.PreparedCamera is null)
                throw new InvalidDataException("Native population requires the same accepted prepared Camera as terrain/deferred.");
            if (context.TerrainFrame.ColorViews.Count != 5)
                throw new InvalidDataException("Native population requires the complete original five-MRT target.");
            var sourceCamera = context.PreparedCamera;
            _ = NativeMatrixMath.Invert(sourceCamera.SourceWorldToView, out _);
            _ = NativeMatrixMath.Invert(sourceCamera.SourceWorldToProjection, out _);
            var camera = MapStaticModelCamera.FromExplicitRows(Rows(sourceCamera.SourceWorldToProjection),
                Rows(Matrix4x4.Transpose(sourceCamera.SourceWorldToView))[..12]);
            var population = _population(context) ?? throw new InvalidDataException("Native population source is unavailable.");
            if (!population.Complete)
                throw new InvalidDataException("Native population has unresolved source instances: " +
                    string.Join("; ", population.Unresolved.Select(f => f.InstanceKey + ": " + f.Reason)));
            var visibility = _visibilityPlanner is null ? MapStaticModelInstances.SelectVisible(population.Instances, camera) :
                _visibilityPlanner(context, population, camera) ?? throw new InvalidDataException("Native model visibility planner returned no complete plan.");
            ValidateVisibility(population, camera, visibility);
            var prepared = _assembler.Prepare(visibility, _runtime);
            if (!prepared.Complete)
                throw new InvalidDataException("Native population has unresolved source draws: " +
                    string.Join("; ", prepared.Unresolved.Select(f => f.InstanceKey + "/" + f.DrawIndex + ": " + f.Reason)));
            foreach (var state in prepared.MaterialStates)
            {
                var graph = state.Context.Selection.Source!.GraphSource.Graph.Source;
                if (graph.ResourceUri != context.TerrainFrame.Source.Graph.Source.ResourceUri ||
                    graph.DecodedSha256 != context.TerrainFrame.Source.Graph.Source.DecodedSha256)
                    throw new InvalidDataException("Native model queue and current terrain target belong to different source graphs.");
            }
            var materials = _materialValidator.Prepare(_materialCache, prepared.Packets);
            return new(population, prepared, materials);
    }

    private void ReserveUploads(IReadOnlyList<ModelGbufferInstancePacket> submittedPackets)
    {
            var reservation = Reserve(submittedPackets);
            // The layer retains successful prefix uploads even if a later draw
            // fails. Reserve every attempted cache key before entering the GPU.
            foreach (var item in reservation.Textures) _textures.TryAdd(item.Key, item.Value);
            foreach (var item in reservation.Geometry) _geometry.TryAdd(item.Key, item.Value);
            _programs.UnionWith(reservation.Programs);
    }

    /// <summary>Prepare both current static queues once. The returned lease
    /// keeps the renderer and target retirement serialized through54,55 and
    /// acceptance; disposal releases an abandoned attempt without committing
    /// material history.</summary>
    public async Task<StaticQueueLease> PrepareQueuesAsync(NativeMapFrameContext context,
        MapSourceCreatedStaticPrimitiveAdmissionPlan admission,
        NativeStaticStencilCurrentOwner currentStencilOwner)
    {
        ArgumentNullException.ThrowIfNull(context); Accept();
        await _gate.WaitAsync();
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var prepared = PrepareCurrentFrame(context);
            var plan = NativeMapStaticQueuePlan.Prepare(context, admission, prepared.Prepared, currentStencilOwner);
            ReserveUploads(plan.SourceCandidates.Select(c => c.Packet).ToArray());
            _preparationMilliseconds = watch.Elapsed.TotalMilliseconds;
            _gpuSubmissionMilliseconds = 0;
            return new(this, prepared, plan);
        }
        catch (Exception error)
        {
            _failedFrames++; LastFailure = error.Message;
            _gate.Release(); Complete(); throw;
        }
    }

    /// <summary>The scene calls Draw/Complete sequentially. Disposal drains a
    /// real pending draw before it releases the shared target lifetime.</summary>
    public sealed class StaticQueueLease : IAsyncDisposable
    {
        private NativeMapModelPopulationRenderer? owner;
        private readonly PreparedCurrentFrame prepared;
        private readonly List<ModelGeometrySubmittedPrimitive> submitted = [];
        private ModelGeometrySubmittedFrame? finalSubmission;
        private int nextNode = 54;
        private bool failed;
        private bool disposing;
        private Task<ModelGeometrySubmittedFrame>? pendingDraw;
        private Task? disposal;
        internal StaticQueueLease(NativeMapModelPopulationRenderer owner,
            PreparedCurrentFrame prepared, NativeMapStaticQueuePlan plan)
            => (this.owner, this.prepared, Plan) = (owner, prepared, plan);
        public NativeMapStaticQueuePlan Plan { get; }

        public Task<ModelGeometrySubmittedFrame> DrawQueueAsync(int node,
            IStaticWorldQueueOrderSource actualOrder,
            NativeMapStaticActiveColorSourceOwner? actualActiveColor = null)
        {
            var currentOwner = owner ?? throw new ObjectDisposedException(nameof(StaticQueueLease));
            if (disposing || failed || node != nextNode || pendingDraw is { IsCompleted: false })
                throw new InvalidOperationException("Static queues must execute54 then55 once within this active attempt.");
            return pendingDraw = DrawCurrentQueueAsync(currentOwner, node, actualOrder, actualActiveColor);
        }

        private async Task<ModelGeometrySubmittedFrame> DrawCurrentQueueAsync(
            NativeMapModelPopulationRenderer currentOwner, int node,
            IStaticWorldQueueOrderSource actualOrder,
            NativeMapStaticActiveColorSourceOwner? actualActiveColor)
        {
            try
            {
                var ordered = Plan.ReadOrderedQueue(node, actualOrder);
                actualActiveColor?.RequireCurrent(Plan,node);
                var packets = ordered.Select(c => actualActiveColor?.PrepareActualDrawPacket(c) ?? c.Packet).ToArray();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var actual = await currentOwner._layer.RenderOrdinaryWorldQueueAsync(Plan.Context.TerrainFrame,
                    node, packets, () =>
                    {
                        actualOrder.RequireCurrent(Plan,node);
                        actualActiveColor?.RequireCurrent(Plan,node);
                    });
                actual.RequireCurrent();
                if (actual.Primitives.Count != ordered.Count || actual.Primitives.Where((p,i) =>
                    p.PacketKey != ordered[i].Packet.InstanceKey ||
                    !ReferenceEquals(p.NativeStencil, ordered[i].Packet.Raster.NativeStencil)).Any())
                    throw new InvalidDataException("Actual static queue submission differs from this complete ordered packet roster.");
                submitted.AddRange(actual.Primitives);
                finalSubmission = actual; nextNode++;
                currentOwner._gpuSubmissionMilliseconds += watch.Elapsed.TotalMilliseconds;
                return actual;
            }
            catch (Exception error)
            { failed = true; currentOwner._failedFrames++; currentOwner.LastFailure = error.Message; throw; }
        }

        public NativeMapModelPopulationAcceptedFrame CompleteAccepted()
        {
            var currentOwner = owner ?? throw new ObjectDisposedException(nameof(StaticQueueLease));
            if (disposing || failed || nextNode != 56 || finalSubmission is null || pendingDraw is { IsCompleted: false })
                throw new InvalidOperationException("Static acceptance requires successful complete54 and55 submissions.");
            finalSubmission.RequireCurrent();
            var completion = new NativeMapStaticQueuesCompletedFrame(Plan, finalSubmission, submitted.ToArray());
            currentOwner._commitAcceptedState?.Invoke(prepared.Prepared);
            currentOwner._acceptedFrames++; currentOwner._populationCount = prepared.Population.Instances.Count;
            currentOwner._visibleCount = prepared.Prepared.Visibility.Visible.Count;
            currentOwner._packetCount = prepared.Prepared.Packets.Count; currentOwner.LastFailure = null;
            var accepted = currentOwner.LastAcceptedFrame = new(Plan.Context, prepared.Prepared, prepared.Materials, currentOwner.Statistics)
            { CurrentViewFamilyCandidates = prepared.Prepared.CurrentViewFamilyCandidates,
                GeometrySubmission = finalSubmission, StaticQueueSubmission = completion };
            Release(); return accepted;
        }

        private void Release()
        {
            var released = Interlocked.Exchange(ref owner, null);
            if (released is not null) { released._gate.Release(); released.Complete(); }
        }
        public ValueTask DisposeAsync()
        {
            disposing = true;
            return new(disposal ??= DrainAndReleaseAsync());
        }
        private async Task DrainAndReleaseAsync()
        {
            try { if (pendingDraw is not null) await pendingDraw; }
            catch { /* The draw already records and returns its failure. */ }
            finally { Release(); }
        }
    }

    private static void ValidateVisibility(MapStaticModelInstanceSet population,
        MapStaticModelCamera camera, MapStaticModelVisibleSet selected)
    {
        if (!selected.Camera.WorldToProjectionRows.SequenceEqual(camera.WorldToProjectionRows) ||
            !selected.Camera.WorldToViewRows.SequenceEqual(camera.WorldToViewRows))
            throw new InvalidDataException("Native model visibility plan uses a different accepted Camera.");
        string[] all = selected.Visible.Select(i => i.InstanceKey).Concat(selected.OutsideFrustum).Concat(selected.Disabled).ToArray();
        if (all.Length != population.Instances.Count || all.Distinct(StringComparer.Ordinal).Count() != all.Length ||
            !all.Order(StringComparer.Ordinal).SequenceEqual(population.Instances.Select(i => i.InstanceKey).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Native model visibility plan must retain exactly the complete population partition.");
        var originals = population.Instances.ToDictionary(i => i.InstanceKey, StringComparer.Ordinal);
        foreach (var visible in selected.Visible)
        {
            var source = originals[visible.InstanceKey];
            if (!ReferenceEquals(source.Asset, visible.Asset) || source.CurrentSourceIdentity != visible.CurrentSourceIdentity ||
                !source.BoundsTransform.PositionRows.SequenceEqual(visible.BoundsTransform.PositionRows) ||
                !source.BoundsTransform.NormalRows.SequenceEqual(visible.BoundsTransform.NormalRows) ||
                source.DrawTransforms.Count != visible.DrawTransforms.Count || source.DrawTransforms.Any(p =>
                    !visible.DrawTransforms.TryGetValue(p.Key, out var matrix) ||
                    !p.Value.PositionRows.SequenceEqual(matrix.PositionRows) || !p.Value.NormalRows.SequenceEqual(matrix.NormalRows)))
                throw new InvalidDataException("Native model visibility planner changed an original instance asset or transform.");
        }
    }

    /// <summary>Assign to BeforeTargetRetirement. Await before the parent frees
    /// framebuffer or sampled views. Source programs/geometry/textures remain
    /// cached for the next accepted target; the last frame is no longer usable.</summary>
    public Task BeforeTargetRetirementAsync(NativeMapTargetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_lifetime)
        {
            // Either lifetime order is safe: a parent can retire its targets
            // before this owner, or await this owner's already requested drain.
            if (_retiring) return _disposal!;
            _operations++;
        }
        return RetireAcceptedAsync();
    }

    private async Task RetireAcceptedAsync()
    {
        await _gate.WaitAsync();
        try { await _layer.RetireTargetAsync(); LastAcceptedFrame = null; _visibleCount = 0; _packetCount = 0; }
        finally { _gate.Release(); Complete(); }
    }

    private sealed record Reservation(
        Dictionary<(string Source, int Image, uint View), long> Textures,
        Dictionary<(string Model, string Shader, int Combination), long> Geometry,
        HashSet<(string Source, int Combination)> Programs);

    private Reservation Reserve(IReadOnlyList<ModelGbufferInstancePacket> packets)
    {
        if (packets.Count > _limits.MaximumVisibleDrawPackets)
            throw new InvalidDataException("Complete visible model population exceeds the explicit draw-packet limit.");
        var textures = new Dictionary<(string Source, int Image, uint View), long>();
        var geometry = new Dictionary<(string Model, string Shader, int Combination), long>();
        var programs = new HashSet<(string Source, int Combination)>();
        foreach (var packet in packets)
        {
            programs.Add((Identity(packet.Shader.Source), packet.ExplicitCombinationIndex));
            var geometryKey = (Identity(packet.Model.Source), Identity(packet.Shader.Source), packet.ExplicitCombinationIndex);
            if (!_geometry.ContainsKey(geometryKey) && !geometry.ContainsKey(geometryKey))
            {
                var draws = G1ModelDrawReader.Read(packet.Model.Geometry).Draws;
                geometry.Add(geometryKey, checked(packet.Model.Source.Bytes.LongLength + draws.Sum(d => checked((long)d.IndexCount * 2))));
            }
            foreach (var texture in packet.Vertex.OwnedTextures.Concat(packet.Pixel.OwnedTextures).Where(t => t.DescriptorKind == "Texture"))
            {
                var key = (Identity(texture.Source.Source), texture.ImageIndex, texture.NativeViewFormat);
                if (_textures.ContainsKey(key) || textures.ContainsKey(key)) continue;
                var image = G1TextureContainerReader.Read(texture.Source.Source.Bytes).Textures[texture.ImageIndex];
                if (image.LayerCount != 1 || image.SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain)
                    throw new InvalidDataException("Native population source texture requires its complete original single-layer mip chain.");
                textures.Add(key, image.Mips.Sum(m => (long)m.BytesPerLayer));
            }
        }
        if (checked(_textures.Values.Sum() + textures.Values.Sum()) > _limits.MaximumTextureBytes ||
            checked(_geometry.Values.Sum() + geometry.Values.Sum()) > _limits.MaximumGeometryBytes ||
            _programs.Union(programs).Count() > _limits.MaximumPrograms)
            throw new InvalidDataException("Complete model source uploads exceed the explicit cache limits; no population subset was substituted.");
        return new(textures, geometry, programs);
    }

    private string Identity(LoadedRdbAsset source) => _sourceIdentities.GetValue(source, s => new(string.Join("|",
        _assembler.SourceRevision, s.ResourceUri, s.DecodedSha256, s.FileId.ToString("X8"), s.Bytes.LongLength,
        s.IndexSource.ResourceUri, s.IndexSource.PhysicalPath, s.IndexSource.Sha256,
        s.OverrideSource?.ResourceUri, s.OverrideSource?.PhysicalPath, s.OverrideSource?.Sha256))).Value;

    private static float[] Rows(Matrix4x4 m) => [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
    private void Accept()
    { lock (_lifetime) { ObjectDisposedException.ThrowIf(_retiring, this); _operations++; } }
    private void Complete()
    { lock (_lifetime) { if (--_operations == 0 && _retiring) _drained?.TrySetResult(); } }
    public ValueTask DisposeAsync()
    {
        lock (_lifetime)
        {
            if (_disposal is not null) return new(_disposal);
            _retiring = true;
            Task pending = _operations == 0 ? Task.CompletedTask : (_drained = new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = completion.Task; _ = DisposeCoreAsync(pending, completion); return new(_disposal);
        }
    }
    private async Task DisposeCoreAsync(Task pending, TaskCompletionSource completion)
    {
        try
        {
            await pending; await _gate.WaitAsync();
            try
            {
                await _layer.DisposeAsync(); _assembler.Invalidate(); _materialValidator.Invalidate(); _textures.Clear(); _geometry.Clear(); _programs.Clear();
                LastAcceptedFrame = null; _populationCount = 0; _visibleCount = 0; _packetCount = 0;
            }
            finally { _gate.Release(); }
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
    }
}
