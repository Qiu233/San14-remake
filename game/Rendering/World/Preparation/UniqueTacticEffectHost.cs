using Godot;
using San14.Data;
using Matrix4x4 = System.Numerics.Matrix4x4;

internal sealed record UniqueTacticEffectFrameInputs(G1EffectDrawCamera Camera,
    Func<G1EffectDrawPacket, G1EffectGpuPacketInputs> PacketInputs, bool ReverseDepth,
    Func<int, int, Matrix4x4?> EmitterTransformProvider);

internal sealed class UniqueTacticEffectHandle
{
    internal required int Kind { get; init; }
    internal required int Layer { get; init; }
    internal required object Owner { get; init; }
    internal required MapEffectAdmissionHandle Slot { get; init; }
    internal G1EffectInstance? Instance;
    internal float Time;
    internal uint Tick;
    internal volatile bool Retired;
    internal CancellationTokenRegistration Cancellation;
    internal bool SourceAllocated => true;
    internal bool NativeAccepted { get; set; }
    // Story creation waits for an actual native publication and, for visible
    // packets, its Canvas presentation. It does not use elapsed-frame guesses.
    internal TaskCompletionSource<bool>? StoryAcceptance { get; init; }
    internal IReadOnlyList<string> Missing { get; set; } = Array.Empty<string>();
}

/// <summary>The two UI-parent draw lists share the map's actual effect manager,
/// particle arena and random state. They retain their own source cameras and
/// real offscreen native outputs, never a borrowed map-camera publication.</summary>
internal sealed class UniqueTacticEffectHost : IAsyncDisposable
{
    private sealed class LayerState(int selector, UniqueTacticEffectSurface surface, ulong generation, Vector2I size)
    {
        internal readonly int Selector = selector;
        internal readonly UniqueTacticEffectSurface Surface = surface;
        internal readonly G1EffectGpuFramePublisher Publisher = new(generation);
        internal readonly Vector2I Size = size;
        internal ulong Sequence;
        internal bool Published;
        internal byte NativeAlpha = byte.MaxValue;
        internal ulong PendingAcceptance;
        internal UniqueTacticEffectHandle[] PendingHandles = [];
        internal UniqueTacticEffectHandle[] PendingStoryHandles = [];
        internal void Accept(ulong sequence)
        {
            if (PendingAcceptance != sequence) return;
            foreach (var handle in PendingHandles) handle.NativeAccepted = true;
            foreach (var handle in PendingStoryHandles) handle.StoryAcceptance?.TrySetResult(true);
            PendingHandles = []; PendingStoryHandles = []; PendingAcceptance = 0;
        }
    }
    private readonly GameResourceRepository repository;
    private readonly RenderResourceContext resourceContext;
    private readonly D3D11CaptureEffects shared;
    private readonly G1EffectHostConfiguration limits;
    private readonly Dictionary<int, (LoadedMapEffectResources Loaded, G1EffectGpuResources Gpu)> resources = [];
    private readonly List<UniqueTacticEffectHandle> handles = [];
    private readonly LayerState[] layers;
    private readonly SemaphoreSlim advances = new(1, 1);
    private readonly Func<float> cameraLevel;
    private readonly Func<int, byte> sourceAlpha;
    private readonly Task opening;
    private Task? retirement;
    private bool disposed;
    internal Exception? LastError { get; private set; }

    internal UniqueTacticEffectHost(GameResourceRepository repository, RenderResourceContext resourceContext,
        string nativeDll, D3D11CaptureEffects shared, Control back, Control front,
        Func<float> sourceCameraLevel, ulong resourceGeneration, Func<int, byte> sourceAlpha, bool singleBackLayer = false)
    {
        this.repository = repository; this.resourceContext = resourceContext; this.shared = shared;
        cameraLevel = sourceCameraLevel; limits = G1EffectHostConfiguration.Load(repository);
        this.sourceAlpha = sourceAlpha ?? throw new ArgumentNullException(nameof(sourceAlpha));
        var backSurface = new UniqueTacticEffectSurface { Name = "UniqueEffectOutput2" };
        back.AddChild(backSurface);
        Vector2I Extent(Control pane)
        {
            var size = new Vector2I(checked((int)pane.Size.X), checked((int)pane.Size.Y));
            if (size.X <= 0 || size.Y <= 0) throw new InvalidDataException("Unique effect pane has no registered target extent.");
            return size;
        }
        if (singleBackLayer) layers = [new(2, backSurface, resourceGeneration, Extent(back))];
        else
        {
            var frontSurface = new UniqueTacticEffectSurface { Name = "UniqueEffectOutput3" };
            front.AddChild(frontSurface);
            layers = [new(2, backSurface, resourceGeneration, Extent(back)), new(3, frontSurface, resourceGeneration, Extent(front))];
        }
        foreach (var layer in layers)
            layer.Surface.Presented += layer.Accept;
        opening = OpenAsync(nativeDll);
    }

    private async Task OpenAsync(string nativeDll)
    {
        try
        {
            foreach (var layer in layers) await layer.Surface.OpenAsync(nativeDll, layer.Selector, layer.Size.X, layer.Size.Y);
        }
        catch (Exception error) { LastError = error; }
    }

    internal Vector2I TargetSize(int selector) => layers.Single(layer => layer.Selector == selector).Size;

    // Null means the original source allocation rejected this attempt. It says
    // nothing about GPU readiness; an allocated handle exposes that separately.
    internal UniqueTacticEffectHandle? TryCreate(int kind, int layer, object owner, CancellationToken lifetime)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        bool tactic = kind is >= 98 and <= 111 && layer is 2 or 3;
        bool story = kind is >= 118 and <= 129 && layer == 2;
        if ((!tactic && !story) || !layers.Any(row => row.Selector == layer))
            throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentNullException.ThrowIfNull(owner);
        lock (shared.EffectGate)
        {
            var allocation = shared.EffectAdmission.TryAllocate(kind, owner, Matrix4x4.Identity, cameraLevel(), (uint)layer);
            if (!allocation.Allocated) return null;
            var handle = new UniqueTacticEffectHandle { Kind = kind, Layer = layer, Owner = owner, Slot = allocation.Handle!,
                StoryAcceptance = story ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null };
            handles.Add(handle);
            if (lifetime.CanBeCanceled) handle.Cancellation = lifetime.Register(() => handle.Retired = true);
            return handle;
        }
    }

    internal void StopLinked(object owner)
    {
        lock (shared.EffectGate)
            foreach (var handle in handles.Where(h => ReferenceEquals(h.Owner, owner) && h.Kind is >= 103 and <= 106))
                shared.EffectAdmission.RequestStop(handle.Slot);
    }

    internal void StopOwned(object owner)
    {
        lock (shared.EffectGate)
            foreach (var handle in handles.Where(h => ReferenceEquals(h.Owner, owner))) handle.Retired = true;
    }

    // Source37F9C0(...,0): release now, rather than the soft emission stop.
    internal void RetireOwnedImmediately(object owner)
    {
        lock (shared.EffectGate)
            foreach (var handle in handles.Where(h => ReferenceEquals(h.Owner, owner)).ToArray()) Retire(handle);
    }

    internal async Task AdvanceAsync(double elapsedSeconds, int framesPerSecond,
        Func<int, UniqueTacticEffectFrameInputs> currentInputs)
    {
        await advances.WaitAsync();
        try
        {
            if (disposed) return;
            await opening;
            if (LastError is not null) throw LastError;
            foreach (var layer in layers)
            {
                byte paneAlpha = sourceAlpha(layer.Selector);
                // E11A20 omits its offscreen command for source byte zero.
                // Particle simulation remains independent of that UI draw.
                if (paneAlpha == 0) layer.Surface.ApplySourceAlpha(0);
                var input = currentInputs(layer.Selector);
                G1EffectGpuFrameBuild? frame = null;
                UniqueTacticEffectHandle[] submitted = [];
                UniqueTacticEffectHandle[] drawn = [];
                lock (shared.EffectGate)
                {
                    var admission = shared.EffectAdmission;
                    var view = new MapEffectAdmissionView(input.Camera.ClipMatrix,
                        new(input.Camera.Eye.X, input.Camera.Eye.Y, input.Camera.Eye.Z), cameraLevel());
                    foreach (var handle in handles.Where(h => h.Layer == layer.Selector).ToArray())
                    {
                        if (handle.Retired) { Retire(handle); continue; }
                        try
                        {
                            bool create = admission.UpdateAdmission(handle.Slot, view);
                            if (create)
                            {
                                if (!resources.TryGetValue(handle.Kind, out var resource))
                                {
                                    lock (repository)
                                    {
                                        var loaded = MapEffectResources.Load(repository, handle.Kind, resourceContext);
                                        resource = (loaded, new(repository, loaded));
                                    }
                                    resources.Add(handle.Kind, resource);
                                }
                                var instance = new G1EffectInstance(resource.Loaded, shared.GetEffectContext(), Matrix4x4.Identity)
                                    { EmitterTransformProvider = input.EmitterTransformProvider };
                                if (!admission.AttachInstance(handle.Slot, instance))
                                { instance.Dispose(); throw new InvalidOperationException("unique_effect.instance_owner"); }
                                handle.Instance = instance;
                            }
                            if (handle.Instance is { } actual)
                            {
                                actual.EmitterTransformProvider = input.EmitterTransformProvider;
                                if (admission.TryConsumeStop(handle.Slot)) actual.BeginStop();
                                if (actual.IsInitialized) { handle.Time += (float)elapsedSeconds * framesPerSecond; handle.Tick++; }
                                actual.Advance(new(framesPerSecond, handle.Tick, handle.Time));
                                handle.Missing = actual.Missing.Select(m => m.Reason).ToArray();
                                if (actual.HasPendingDirectStorage) handle.Missing = handle.Missing.Append("unique_effect.actual_direct_storage").ToArray();
                            }
                            if (handle.Instance is { IsInitialized: true, IsFinished: true, HasPendingDirectStorage: false } && handle.Missing.Count == 0)
                                handle.StoryAcceptance?.TrySetResult(true); // actual finished instance, no remaining visual work
                            if (!admission.Collect(handle.Slot, actual => actual.Dispose())) Retire(handle);
                            else if (!admission.HasInstance(handle.Slot)) handle.Instance = null;
                        }
                        catch (Exception error) { handle.Missing = [error.Message]; }
                    }
                    submitted = handles.Where(h => h.Layer == layer.Selector && !h.Retired && h.Missing.Count == 0 &&
                        h.Instance is { IsInitialized: true, HasPendingDirectStorage: false }).ToArray();
                    if (submitted.Length != 0 && paneAlpha != 0)
                    {
                        var sourceOwners = new Dictionary<G1EffectParticleRenderSource, UniqueTacticEffectHandle>(ReferenceEqualityComparer.Instance);
                        foreach (var handle in submitted)
                            foreach (var source in handle.Instance!.EnumerateRenderSources()) sourceOwners.Add(source, handle);
                        var batch = G1EffectDrawWriter.Build(input.Camera, sourceOwners.Keys,
                            limits.BucketCount, limits.QueueCapacity, limits.DynamicCapacity32, limits.RowCapacity);
                        drawn = batch.Packets.Where(p => p.Source is not null).Select(p => sourceOwners[p.Source!]).Distinct().ToArray();
                        frame = layer.Publisher.Build(++layer.Sequence,
                            [new(resources[submitted[0].Kind].Gpu, batch, input.PacketInputs, r => resources[r.Kind].Gpu)]);
                        if (!frame.IsComplete)
                        {
                            foreach (var handle in submitted) handle.Missing = frame.Missing.Select(m => m.Reason).ToArray();
                            frame = null;
                        }
                    }
                    if (handles.FirstOrDefault(h => h.Layer == layer.Selector && h.Missing.Count != 0) is { } failed)
                        throw new InvalidOperationException(string.Join("; ", failed.Missing));
                }
                if (paneAlpha == 0) continue;
                G1EffectGpuPublication? publication = frame?.Publication;
                // Even an empty partial-alpha image must be initialized by its
                // real clearA=0 branch before the pane can expose that output.
                if (publication is null && (layer.Published || layer.NativeAlpha != paneAlpha))
                    publication = layer.Publisher.Clear(++layer.Sequence);
                if (publication is null) { layer.Surface.ApplySourceAlpha(paneAlpha); continue; }
                var receipt = await layer.Surface.SubmitAsync(publication, input.ReverseDepth, frame?.DrawCount ?? 0, paneAlpha);
                if (receipt.Sequence != publication.Sequence) throw new InvalidDataException("UI effect native sequence mismatch.");
                if (frame is not null && receipt.DrawCount != frame.DrawCount)
                    throw new InvalidDataException("UI effect native draw count differs from its actual publication.");
                layer.Published = frame is not null;
                layer.NativeAlpha = paneAlpha;
                layer.PendingAcceptance = publication.Sequence;
                layer.PendingHandles = frame is { DrawCount: > 0 } ? drawn : [];
                if (frame is { DrawCount: 0 })
                {
                    // The real initialized instance produced no draw packets;
                    // the native clear/resource publication has actually fenced.
                    foreach (var handle in submitted) handle.StoryAcceptance?.TrySetResult(true);
                    layer.PendingStoryHandles = [];
                }
                else layer.PendingStoryHandles = frame is not null ? submitted : [];
                layer.Accept(layer.Surface.LastPresentedSequence);
            }
        }
        catch (Exception error)
        {
            LastError = error;
            foreach (var handle in handles) handle.StoryAcceptance?.TrySetException(error);
            throw;
        }
        finally { advances.Release(); }
    }

    private void Retire(UniqueTacticEffectHandle handle)
    {
        shared.EffectAdmission.Retire(handle.Slot, actual => actual.Dispose());
        handle.Instance = null; handle.Retired = true; handle.Cancellation.Dispose(); handles.Remove(handle);
        // Visual completion never clears the caller's invocation descriptor.
        handle.StoryAcceptance?.TrySetCanceled();
    }

    // Detach synchronously before the Story view's parent may QueueFree. The
    // strong host reference keeps the detached surfaces alive until GPU drain.
    internal Task RetireDetachedAsync()
    {
        if (retirement is not null) return retirement;
        disposed = true;
        foreach (var layer in layers) layer.Surface.DetachForRetirement();
        return retirement = DisposeCoreAsync();
    }

    public ValueTask DisposeAsync()
    {
        if (retirement is not null) return new(retirement);
        disposed = true;
        retirement = DisposeCoreAsync(); return new(retirement);
    }

    private async Task DisposeCoreAsync()
    {
        // Cancel a first-Canvas visibility wait before joining the advance
        // which may be awaiting it. Surfaces still drain in-flight native work.
        var closing = layers.Select(layer => layer.Surface.CloseAsync()).ToArray();
        await advances.WaitAsync();
        try
        {
            await opening;
            lock (shared.EffectGate) foreach (var handle in handles.ToArray()) Retire(handle);
            await Task.WhenAll(closing);
            foreach (var layer in layers)
            {
                if (GodotObject.IsInstanceValid(layer.Surface)) layer.Surface.QueueFree();
            }
            resources.Clear();
        }
        finally { advances.Release(); }
    }
}
