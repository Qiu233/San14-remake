using System.Collections.Concurrent;
using System.Numerics;
using San14.Data;

internal sealed record CaptureEffectAdmission(bool Accepted, string? UnresolvedInput = null)
{
    public bool Culled { get; init; }
    public bool NoVisualRequired { get; init; }
    public bool Completed => Accepted || Culled || NoVisualRequired;
}

/// <summary>One map's effect instances, shared simulation arena and actual
/// device publications. The render worker owns mutable particles; UI callers
/// enqueue immutable source commands and receive the native acceptance only.</summary>
internal sealed class D3D11CaptureEffects : IDisposable
{
    private sealed record TransformUpdate(Matrix4x4 Matrix);
    private sealed class Ticket(int kind, Matrix4x4 transform, uint? colour, object effectSource)
    {
        internal readonly int EffectKind = kind;
        internal readonly object Source = effectSource;
        internal MapEffectAdmissionHandle? Handle;
        internal readonly uint? Colour = colour;
        internal TransformUpdate Transform = new(transform);
        internal volatile bool Live;
        internal volatile bool Retired;
        internal readonly TaskCompletionSource<CaptureEffectAdmission> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Retirement = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal G1EffectInstance? Instance;
        internal float Time;
        internal uint Tick;
    }
    private readonly GameResourceRepository repository;
    private readonly RenderResourceContext resourceContext;
    private readonly G1EffectHostConfiguration limits;
    private readonly G1EffectGpuFramePublisher publisher;
    private readonly MapEffectAdmissionManager admission;
    private readonly ConcurrentDictionary<object, Ticket> tickets = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentQueue<Ticket> queued = new();
    private readonly ConcurrentDictionary<ulong, Ticket[]> awaitingAcceptance = new();
    private readonly Dictionary<int, (LoadedMapEffectResources Loaded, G1EffectGpuResources Gpu)> resources = [];
    private readonly List<Ticket> active = [];
    private G1EffectContext? context;
    private G1EffectMapDirectDraws? directDraws;
    private G1EffectDirectComputeView? directView;
    private ulong sequence;
    private bool everPublished, disposed;
    internal bool HasWork => !queued.IsEmpty || active.Count != 0 || everPublished;

    internal D3D11CaptureEffects(GameResourceRepository repository, RenderResourceContext resources, ulong generation)
    {
        this.repository = repository; resourceContext = resources;
        limits = G1EffectHostConfiguration.Load(repository);
        publisher = new(generation);
        admission = new(repository.LoadEffectInfo());
    }

    internal Task<CaptureEffectAdmission> Enqueue(object source, MonthlyTerritoryVisual visual,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var matrix = Matrix4x4.CreateScale(visual.Scale) * Matrix4x4.CreateTranslation(
            visual.Position.X, visual.Position.Y, visual.Position.Z);
        return Enqueue(source, visual.EffectKind, matrix, visual.ForceColor?.ToAbgr32(), cancellationToken);
    }

    internal Task<CaptureEffectAdmission> Enqueue(object source, int kind, Matrix4x4 transform,
        uint? colour, CancellationToken cancellationToken, object? effectSource = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var candidate = new Ticket(kind, transform, colour, effectSource ?? source);
        var ticket = tickets.GetOrAdd(source, candidate);
        if (ReferenceEquals(ticket, candidate)) queued.Enqueue(ticket);
        //A presentation workspace may stop waiting without stopping a cue
        //already admitted by the scene. Map retirement owns that cancellation.
        return ticket.Completion.Task.WaitAsync(cancellationToken);
    }

    internal G1EffectGpuPublication? PrepareFrame(G1EffectMapInputFrame frame, double elapsedSeconds,
        int framesPerSecond, float sourceCameraLevel,
        IG1EffectDirectComputeDevice? device)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        directView = frame.DirectView;
        if (directDraws is null && device is not null && directView.HasValue)
            directDraws = new(device, () => directView ?? throw new InvalidOperationException("capture_effect.current_direct_view"));
        Func<G1EffectGenericRuntime, IG1EffectDirectParticleStorage?>? directStorage = directDraws is null
            ? null : directDraws.CreateStorage;
        var eye = frame.GpuCamera.Eye;
        var view = new MapEffectAdmissionView(frame.GpuCamera.WorldToProjection,
            new(eye.X, eye.Y, eye.Z), sourceCameraLevel);
        while (queued.TryDequeue(out var ticket))
        {
            if (ticket.Retired)
            {
                ticket.Completion.TrySetResult(new(false, "capture_effect.retired"));
                ticket.Retirement.TrySetResult(true); continue;
            }
            var allocated = admission.TryAllocate(ticket.EffectKind, ticket.Source,
                Volatile.Read(ref ticket.Transform).Matrix, sourceCameraLevel);
            if (!allocated.Allocated)
            {
                ticket.Completion.TrySetResult(new(false) { Culled = true });
                ticket.Retirement.TrySetResult(true); continue;
            }
            ticket.Handle = allocated.Handle;
            ticket.Live = true; active.Add(ticket);
        }
        foreach (var ticket in active.ToArray())
        {
            var handle = ticket.Handle!;
            if (ticket.Retired)
            {
                admission.Retire(handle, instance => instance.Dispose());
                ticket.Instance = null; ticket.Live = false; active.Remove(ticket);
                ticket.Retirement.TrySetResult(true);
                ticket.Completion.TrySetResult(new(false, "capture_effect.retired")); continue;
            }
            try
            {
                admission.TryUpdateTransform(handle, ticket.Source, Volatile.Read(ref ticket.Transform).Matrix);
                bool create = admission.UpdateAdmission(handle, view);
                var slot = admission.ReadSlot(handle) ?? throw new InvalidOperationException("capture_effect.source_slot");
                if (create)
                {
                    if (context is null && !G1EffectContext.TryCreate(G1EffectContextConfiguration.ApplicationDefault, out context))
                        throw new InvalidOperationException("capture_effect.particle_arena_allocation");
                    if (!resources.TryGetValue(ticket.EffectKind, out var resource))
                    {
                        lock (repository)
                        {
                            var loaded = MapEffectResources.Load(repository, ticket.EffectKind, resourceContext);
                            resource = (loaded, new(repository, loaded));
                        }
                        resources.Add(ticket.EffectKind, resource);
                    }
                    var instance = new G1EffectInstance(resource.Loaded, context!, slot.Transform, directStorage);
                    if (!admission.AttachInstance(handle, instance))
                    { instance.Dispose(); throw new InvalidOperationException("capture_effect.instance_slot_owner"); }
                    ticket.Instance = instance;
                    if (ticket.Colour is uint colour) instance.SetColourPacked(colour);
                }
                if (ticket.Instance is { } actual)
                {
                    actual.SetTransform(slot.Transform);
                    if (admission.TryConsumeStop(handle)) actual.BeginStop();
                    //The real View clock remains independent of simulation pause.
                    if (actual.IsInitialized) { ticket.Time += (float)elapsedSeconds * framesPerSecond; ticket.Tick++; }
                    actual.Advance(new(framesPerSecond, ticket.Tick, ticket.Time));
                    if (actual.Missing.Count != 0) ticket.Completion.TrySetResult(new(false, actual.Missing[0].Reason));
                    else if (actual.HasPendingDirectStorage) ticket.Completion.TrySetResult(new(false, "capture_effect.direct_storage_owner"));
                }
                bool retained = admission.Collect(handle, instance => instance.Dispose());
                if (!admission.HasInstance(handle)) ticket.Instance = null;
                ticket.Live = admission.IsValid(handle);
                if (!retained)
                {
                    active.Remove(ticket); ticket.Retirement.TrySetResult(true);
                    //Only the actual source visibility/camera gate certifies a
                    //culled attempt. It is distinct from native acceptance.
                    if ((slot.LifecycleFlags & 1) == 0) ticket.Completion.TrySetResult(new(false) { Culled = true });
                }
            }
            catch (Exception error)
            {
                ticket.Completion.TrySetResult(new(false, error.Message));
                admission.Retire(handle, instance => instance.Dispose());
                ticket.Instance = null; ticket.Live = false; active.Remove(ticket); ticket.Retirement.TrySetResult(true);
            }
        }
        //Unprepared GPU populations retain their actual pending instances and
        //source requests. They cannot be submitted as ordinary CPU particles.
        var ready = active.Where(ticket => ticket.Instance is { IsInitialized: true, HasPendingDirectStorage: false } instance &&
            instance.Missing.Count == 0).ToArray();
        if (ready.Length == 0 && !everPublished) return null;
        ulong currentSequence = ++sequence;
        if (ready.Length == 0) { everPublished = false; return publisher.Clear(currentSequence); }
        long directOrdinal = 0;
        foreach (var ticket in ready)
        {
            if (directDraws is null) continue;
            var direct = directDraws.Prepare(currentSequence, ticket.Instance!.Resources, ticket.Instance,
                frame, publisher, resources[ticket.EffectKind].Gpu, ref directOrdinal);
            if (!direct.IsComplete) ticket.Completion.TrySetResult(new(false, direct.Missing[0].Reason));
        }
        var batch = G1EffectDrawWriter.Build(frame.CpuCamera, ready.SelectMany(ticket => ticket.Instance!.EnumerateRenderSources())
                .Where(source => !source.Declaration.UsesDirectBirth),
            limits.BucketCount, limits.QueueCapacity, limits.DynamicCapacity32, limits.RowCapacity);
        var gpu = resources[ready[0].EffectKind].Gpu;
        var built = publisher.Build(currentSequence,
            [new(gpu, batch, frame.PacketInputs, resource => resources[resource.Kind].Gpu)]);
        //Only inputs reached by the selected shader stages gate submission.
        //Unused optional providers remain diagnostics on the prepared frame.
        var missing = built.Missing.FirstOrDefault()?.Reason;
        if (missing is not null)
        {
            foreach (var ticket in ready) ticket.Completion.TrySetResult(new(false, missing));
            if (!everPublished) return null;
            everPublished = false; return publisher.Clear(currentSequence);
        }
        everPublished = true;
        awaitingAcceptance[currentSequence] = ready.Where(ticket => !ticket.Completion.Task.IsCompleted).ToArray();
        return built.Publication;
    }

    internal void Accept(G1EffectGpuPublication? publication)
    {
        if (publication is null || !awaitingAcceptance.TryRemove(publication.Sequence, out var accepted)) return;
        foreach (var ticket in accepted) ticket.Completion.TrySetResult(new(true));
    }

    internal void CancelPending()
    {
        foreach (var ticket in tickets.Values) ticket.Completion.TrySetCanceled();
    }

    internal bool IsLive(object source) => tickets.TryGetValue(source, out var ticket) && !ticket.Retired &&
        (ticket.Live || !ticket.Completion.Task.IsCompleted);

    internal bool SetTransform(object source, Matrix4x4 transform)
    {
        if (!tickets.TryGetValue(source, out var ticket) || ticket.Retired) return false;
        Volatile.Write(ref ticket.Transform, new(transform)); return true;
    }

    // Hard retirement is executed by the render worker before its next global
    // publication. Other effect owners and their shared arena remain intact.
    internal Task Retire(object source)
    {
        if (!tickets.TryGetValue(source, out var ticket)) return Task.CompletedTask;
        ticket.Retired = true; return ticket.Retirement.Task;
    }

    internal void Forget(object source) => tickets.TryRemove(source, out _);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; CancelPending();
        foreach (var ticket in active)
        {
            if (ticket.Handle is { } handle) admission.Retire(handle, instance => instance.Dispose());
            ticket.Instance = null; ticket.Live = false; ticket.Retirement.TrySetResult(true);
        }
        foreach (var ticket in tickets.Values) ticket.Retirement.TrySetResult(true);
        active.Clear(); context?.Dispose(); context = null;
        tickets.Clear(); awaitingAcceptance.Clear(); resources.Clear();
    }
}
