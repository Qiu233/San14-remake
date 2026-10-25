using System.Collections.Concurrent;
using System.Numerics;
using San14.Data;

internal sealed record CaptureEffectAdmission(bool Accepted, string? UnresolvedInput = null);

/// <summary>One map's effect instances, shared simulation arena and actual
/// device publications. The render worker owns mutable particles; UI callers
/// enqueue immutable source commands and receive the native acceptance only.</summary>
internal sealed class D3D11CaptureEffects : IDisposable
{
    private sealed class Ticket(MonthlyTerritoryVisual visual)
    {
        internal readonly MonthlyTerritoryVisual Visual = visual;
        internal readonly TaskCompletionSource<CaptureEffectAdmission> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal G1EffectInstance? Instance;
        internal float Time;
        internal uint Tick;
    }
    private readonly GameResourceRepository repository;
    private readonly RenderResourceContext resourceContext;
    private readonly G1EffectHostConfiguration limits;
    private readonly G1EffectGpuFramePublisher publisher;
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
    }

    internal Task<CaptureEffectAdmission> Enqueue(object source, MonthlyTerritoryVisual visual,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var candidate = new Ticket(visual);
        var ticket = tickets.GetOrAdd(source, candidate);
        if (ReferenceEquals(ticket, candidate)) queued.Enqueue(ticket);
        //A presentation workspace may stop waiting without stopping a cue
        //already admitted by the scene. Map retirement owns that cancellation.
        return ticket.Completion.Task.WaitAsync(cancellationToken);
    }

    internal G1EffectGpuPublication? PrepareFrame(G1EffectMapInputFrame frame, double elapsedSeconds,
        int framesPerSecond,
        IG1EffectDirectComputeDevice? device)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        directView = frame.DirectView;
        if (directDraws is null && device is not null && directView.HasValue)
            directDraws = new(device, () => directView ?? throw new InvalidOperationException("capture_effect.current_direct_view"));
        Func<G1EffectGenericRuntime, IG1EffectDirectParticleStorage?>? directStorage = directDraws is null
            ? null : directDraws.CreateStorage;
        while (queued.TryDequeue(out var ticket))
        {
            try
            {
                if (context is null && !G1EffectContext.TryCreate(G1EffectContextConfiguration.ApplicationDefault, out context))
                    throw new InvalidOperationException("capture_effect.particle_arena_allocation");
                var visual = ticket.Visual;
                if (!resources.TryGetValue(visual.EffectKind, out var resource))
                {
                    lock (repository)
                    {
                        var loaded = MapEffectResources.Load(repository, visual.EffectKind, resourceContext);
                        resource = (loaded, new(repository, loaded));
                    }
                    resources.Add(visual.EffectKind, resource);
                }
                var matrix = Matrix4x4.CreateScale(visual.Scale) * Matrix4x4.CreateTranslation(
                    visual.Position.X, visual.Position.Y, visual.Position.Z);
                ticket.Instance = new(resource.Loaded, context!, matrix, directStorage);
                if (visual.ForceColor is { } colour) ticket.Instance.SetColourPacked(colour.ToAbgr32());
                active.Add(ticket);
            }
            catch (Exception error) { ticket.Completion.TrySetResult(new(false, error.Message)); }
        }
        foreach (var ticket in active)
        {
            //This World clock is independent of Army/period pause and its
            //animation-admission flag. Each real Main request supplies time.
            if (ticket.Instance!.IsInitialized)
            { ticket.Time += (float)elapsedSeconds * framesPerSecond; ticket.Tick++; }
            ticket.Instance.Advance(new(framesPerSecond, ticket.Tick, ticket.Time));
            if (ticket.Instance!.Missing.Count != 0)
                ticket.Completion.TrySetResult(new(false, ticket.Instance.Missing[0].Reason));
            else if (ticket.Instance.HasPendingDirectStorage)
                ticket.Completion.TrySetResult(new(false, "capture_effect.direct_storage_owner"));
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
                frame, publisher, resources[ticket.Visual.EffectKind].Gpu, ref directOrdinal);
            if (!direct.IsComplete) ticket.Completion.TrySetResult(new(false, direct.Missing[0].Reason));
        }
        var batch = G1EffectDrawWriter.Build(frame.CpuCamera, ready.SelectMany(ticket => ticket.Instance!.EnumerateRenderSources())
                .Where(source => !source.Declaration.UsesDirectBirth),
            limits.BucketCount, limits.QueueCapacity, limits.DynamicCapacity32, limits.RowCapacity);
        var gpu = resources[ready[0].Visual.EffectKind].Gpu;
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
        foreach (var ticket in active.Where(ticket => ticket.Instance!.IsFinished).ToArray())
        { active.Remove(ticket); ticket.Instance!.Dispose(); }
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

    internal void Forget(object source) => tickets.TryRemove(source, out _);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; CancelPending();
        foreach (var ticket in active) ticket.Instance?.Dispose();
        active.Clear(); context?.Dispose(); context = null;
        tickets.Clear(); awaitingAcceptance.Clear(); resources.Clear();
    }
}
