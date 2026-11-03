using System.Collections.Concurrent;
using System.Numerics;
using San14.Data;

internal sealed record CaptureEffectAdmission(bool Accepted, string? UnresolvedInput = null)
{
    public bool Culled { get; init; }
    public bool SourceAllocated { get; init; }
    public bool NoVisualRequired { get; init; }
    public bool Completed => Accepted || Culled || NoVisualRequired;
}

/// <summary>One map's effect instances, shared simulation arena and actual
/// device publications. The render worker owns mutable particles; UI callers
/// enqueue immutable source commands and receive the native acceptance only.</summary>
internal sealed class D3D11CaptureEffects : IDisposable
{
    private sealed record TransformUpdate(Matrix4x4 Matrix);
    private sealed record ColourUpdate(uint? Value);
    private sealed class Ticket(int kind, Matrix4x4 transform, uint? colour, object effectSource)
    {
        internal readonly int EffectKind = kind;
        internal readonly object Source = effectSource;
        internal uint Variant;
        internal ulong SourceOwner;
        internal MapEffectAdmissionHandle? Handle;
        internal ColourUpdate Colour = new(colour);
        internal uint? AppliedColour;
        internal TransformUpdate Transform = new(transform);
        internal volatile bool Live;
        internal volatile bool Retired;
        internal volatile bool StopRequested;
        // A later prepared scene cannot reject a ticket whose earlier real
        // Canvas publication still owns its acceptance decision.
        internal bool HasUnacceptedPublication;
        internal readonly TaskCompletionSource<bool> StopAccepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<CaptureEffectAdmission> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Retirement = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal G1EffectInstance? Instance;
        internal float Time;
        internal uint Tick;
    }
    private readonly GameResourceRepository repository;
    private readonly RenderResourceContext resourceContext;
    private readonly MapDomesticEffectStaticResources.Namespace domesticEffects;
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
    private ulong sequence, nextStoryOwner;
    private readonly Dictionary<object, ulong> storyOwners = new(ReferenceEqualityComparer.Instance);
    // A completed historical publication is not a live resource borrower.
    internal bool HasRetainedEffectOwners => tickets.Values.Any(ticket => !ticket.Retirement.Task.IsCompleted);
    internal bool HasStoryWork => tickets.Values.Any(ticket => ticket.SourceOwner != 0 && !ticket.Retired);
    private bool everPublished, disposed;
    private bool mainBankZeroOwnerAllocated, mainBankZeroOwnerPublished;
    private ulong? pendingMainBankZeroOwnerSequence;
    // Map and UI-parent effects use the same original manager, arena and RNG.
    // Both render workers serialize particle mutations through this gate.
    internal object EffectGate { get; } = new();
    internal MapEffectAdmissionManager EffectAdmission => admission;
    internal G1EffectContext GetEffectContext()
    {
        lock (EffectGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (context is null && !G1EffectContext.TryCreate(G1EffectContextConfiguration.ApplicationDefault, out context))
                throw new InvalidOperationException("capture_effect.particle_arena_allocation");
            return context!;
        }
    }
    internal bool HasWork => !queued.IsEmpty || active.Count != 0 || everPublished ||
        mainBankZeroOwnerAllocated && !mainBankZeroOwnerPublished;

    internal D3D11CaptureEffects(GameResourceRepository repository, RenderResourceContext resources, ulong generation,
        MapDomesticEffectStaticResources.Namespace domesticEffects)
    {
        this.repository = repository; resourceContext = resources;
        this.domesticEffects = domesticEffects;
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

    internal Task<CaptureEffectAdmission> EnqueueStory(object key, object owner, int kind, CancellationToken token)
    {
        lock (EffectGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!storyOwners.TryGetValue(owner, out ulong id)) storyOwners.Add(owner, id = checked(++nextStoryOwner));
            var candidate = new Ticket(kind, Matrix4x4.Identity, null, owner) { Variant = 1, SourceOwner = id };
            var ticket = tickets.GetOrAdd(key, candidate);
            if (ReferenceEquals(ticket, candidate)) queued.Enqueue(ticket);
            return ticket.Completion.Task.WaitAsync(token);
        }
    }

    // Called by the retained Map surface on its real render thread, before
    // native retireOwner removes this invocation's CPU and Direct draw rows.
    internal ulong RetireStoryOwnerImmediately(object owner)
    {
        lock (EffectGate)
        {
            if (!storyOwners.TryGetValue(owner, out ulong id)) return 0;
            foreach (var pair in tickets.Where(pair => pair.Value.SourceOwner == id).ToArray())
            {
                var ticket = pair.Value; ticket.Retired = true;
                if (ticket.Handle is { } handle) admission.Retire(handle, instance => instance.Dispose());
                ticket.Instance = null; ticket.Live = false; active.Remove(ticket);
                ticket.Completion.TrySetResult(new(false, "capture_effect.retired"));
                ticket.Retirement.TrySetResult(true); tickets.TryRemove(pair.Key, out _);
            }
            return id;
        }
    }

    internal void CompleteStoryOwnerRetirement(object owner, ulong token)
    {
        lock (EffectGate)
            if (storyOwners.TryGetValue(owner, out ulong actual) && actual == token) storyOwners.Remove(owner);
    }

    internal G1EffectGpuPublication? PrepareFrame(G1EffectMapInputFrame frame, double elapsedSeconds,
        int framesPerSecond, float sourceCameraLevel,
        IG1EffectDirectComputeDevice? device, G1EffectMapInputFrame? storyFrame = null)
    {
        lock (EffectGate) return PrepareFrameCore(frame, elapsedSeconds, framesPerSecond, sourceCameraLevel, device, storyFrame);
    }

    private G1EffectGpuPublication? PrepareFrameCore(G1EffectMapInputFrame frame, double elapsedSeconds,
        int framesPerSecond, float sourceCameraLevel, IG1EffectDirectComputeDevice? device, G1EffectMapInputFrame? storyFrame)
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
                CompletePreparation(ticket,new(false, "capture_effect.retired"));
                ticket.Retirement.TrySetResult(true); continue;
            }
            var allocated = admission.TryAllocate(ticket.EffectKind, ticket.Source,
                Volatile.Read(ref ticket.Transform).Matrix, sourceCameraLevel, ticket.Variant);
            if (!allocated.Allocated)
            {
                CompletePreparation(ticket,new(false) { Culled = true });
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
                CompletePreparation(ticket,new(false, "capture_effect.retired")); continue;
            }
            try
            {
                if (ticket.StopRequested)
                {
                    admission.RequestStop(handle);
                    ticket.StopRequested = false;
                    ticket.StopAccepted.TrySetResult(true);
                    if (!admission.IsValid(handle))
                    {
                        ticket.Live = false; active.Remove(ticket); ticket.Retirement.TrySetResult(true);
                        continue;
                    }
                }
                var requestedColour = Volatile.Read(ref ticket.Colour).Value;
                // Colour changes follow the original instance-rebuild rule.
                // A stop already accepted by the worker must keep its tail.
                if (!ticket.StopAccepted.Task.IsCompleted && ticket.Instance is not null &&
                    requestedColour != ticket.AppliedColour)
                {
                    if (!admission.ReleaseInstance(handle, ticket.Source, instance => instance.Dispose()))
                        throw new InvalidOperationException("capture_effect.colour_source_owner");
                    ticket.Instance = null;
                }
                admission.TryUpdateTransform(handle, ticket.Source, Volatile.Read(ref ticket.Transform).Matrix);
                var selectedFrame = ticket.Variant == 1
                    ? storyFrame ?? throw new InvalidOperationException("story_effect.view1_current_frame") : frame;
                directView = selectedFrame.DirectView;
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
                            var loaded = ticket.EffectKind is >= 68 and <= 71
                                ? domesticEffects.Require(ticket.EffectKind)
                                : MapEffectResources.Load(repository, ticket.EffectKind, resourceContext);
                            resource = (loaded, new(repository, loaded));
                        }
                        resources.Add(ticket.EffectKind, resource);
                    }
                    var instance = new G1EffectInstance(resource.Loaded, context!, slot.Transform, directStorage);
                    if (!admission.AttachInstance(handle, instance))
                    { instance.Dispose(); throw new InvalidOperationException("capture_effect.instance_slot_owner"); }
                    ticket.Instance = instance;
                    if (ticket.Variant == 0) mainBankZeroOwnerAllocated = true;
                    if (requestedColour is uint colour) instance.SetColourPacked(colour);
                    ticket.AppliedColour = requestedColour;
                    instance.EmitterTransformProvider = selectedFrame.EmitterTransformProvider;
                }
                if (ticket.Instance is { } actual)
                {
                    actual.SetTransform(slot.Transform);
                    actual.EmitterTransformProvider = selectedFrame.EmitterTransformProvider;
                    if (admission.TryConsumeStop(handle)) actual.BeginStop();
                    //The real View clock remains independent of simulation pause.
                    if (actual.IsInitialized) { ticket.Time += (float)elapsedSeconds * framesPerSecond; ticket.Tick++; }
                    actual.Advance(new(framesPerSecond, ticket.Tick, ticket.Time));
                    if (actual.Missing.Count != 0) CompletePreparation(ticket,new(false, actual.Missing[0].Reason));
                    else if (actual.HasPendingDirectStorage) CompletePreparation(ticket,new(false, "capture_effect.direct_storage_owner"));
                }
                if (ticket.Variant == 1 && ticket.Instance is { IsInitialized: true, IsFinished: true, HasPendingDirectStorage: false } finished && finished.Missing.Count == 0)
                    CompletePreparation(ticket,new(false) { NoVisualRequired = true, SourceAllocated = true });
                bool retained = admission.Collect(handle, instance => instance.Dispose());
                if (!admission.HasInstance(handle)) ticket.Instance = null;
                ticket.Live = admission.IsValid(handle);
                if (!retained)
                {
                    active.Remove(ticket); ticket.Retirement.TrySetResult(true);
                    //Only the actual source visibility/camera gate certifies a
                    //culled attempt. It is distinct from native acceptance.
                    if ((slot.LifecycleFlags & 1) == 0) CompletePreparation(ticket,new(false) { Culled = true });
                }
            }
            catch (Exception error)
            {
                CompletePreparation(ticket,new(false, error.Message));
                admission.Retire(handle, instance => instance.Dispose());
                ticket.Instance = null; ticket.Live = false; active.Remove(ticket); ticket.Retirement.TrySetResult(true);
            }
        }
        //Unprepared GPU populations retain their actual pending instances and
        //source requests. They cannot be submitted as ordinary CPU particles.
        var ready = active.Where(ticket => ticket.Instance is { IsInitialized: true, HasPendingDirectStorage: false } instance &&
            instance.Missing.Count == 0).ToArray();
        if (ready.Length == 0 && !everPublished && (!mainBankZeroOwnerAllocated || mainBankZeroOwnerPublished)) return null;
        ulong currentSequence = ++sequence;
        if (ready.Length == 0)
        {
            everPublished = false;
            if (mainBankZeroOwnerAllocated && !mainBankZeroOwnerPublished)
            {
                // A real renderer allocation can have an empty live queue.
                // Publish its ownership once; it outlives particle retirement.
                var owned = publisher.Build(currentSequence, [], mainBankZeroOwner: true).Publication;
                pendingMainBankZeroOwnerSequence ??= currentSequence;
                return owned;
            }
            return publisher.Clear(currentSequence);
        }
        long directOrdinal = 0;
        foreach (var ticket in ready)
        {
            if (directDraws is null) continue;
            var direct = directDraws.Prepare(currentSequence, ticket.Instance!.Resources, ticket.Instance,
                ForTicket(ticket), publisher, resources[ticket.EffectKind].Gpu, ref directOrdinal);
            if (!direct.IsComplete) CompletePreparation(ticket,new(false, direct.Missing[0].Reason));
        }
        var sources = new List<G1EffectGpuFrameSource>();
        foreach (var group in ready.GroupBy(ticket => ticket.Variant))
        {
            var selectedFrame = group.Key == 1
                ? storyFrame ?? throw new InvalidOperationException("story_effect.view1_current_frame") : frame;
            var owners = new Dictionary<G1EffectParticleRenderSource, ulong>(ReferenceEqualityComparer.Instance);
            foreach (var ticket in group)
                foreach (var source in ticket.Instance!.EnumerateRenderSources().Where(source => !source.Declaration.UsesDirectBirth))
                    owners.Add(source, ticket.SourceOwner);
            var batch = G1EffectDrawWriter.Build(selectedFrame.CpuCamera, owners.Keys,
                limits.BucketCount, limits.QueueCapacity, limits.DynamicCapacity32, limits.RowCapacity);
            // Both scopes borrow this one current queue/pool. AA1B60 mode3
            // consumes the reverse physical tail; ordinary188 consumes head38.
            var head = G1EffectSpecialTail.SelectHead(batch);
            sources.Add(new(resources[group.First().EffectKind].Gpu, head, packet =>
            {
                var input = selectedFrame.PacketInputs(packet);
                if (group.Key == 0) return input;
                if (packet.Source is null || !owners.TryGetValue(packet.Source, out ulong owner))
                    throw new InvalidOperationException("story_effect.draw_source_owner");
                return input with { Scope = input.Scope with { SourceOwner = owner } };
            }, resource => resources[resource.Kind].Gpu));
            if (group.Key == 0 && selectedFrame.SpecialPacketInputs is { } specialInputs)
            {
                var tail = G1EffectSpecialTail.Select(batch);
                foreach (uint node in new[] { 141u, 147u })
                    sources.Add(new(resources[group.First().EffectKind].Gpu, tail,
                        packet => specialInputs(node, packet), resource => resources[resource.Kind].Gpu));
            }
        }
        var built = publisher.Build(currentSequence, sources, mainBankZeroOwnerAllocated);
        //Only inputs reached by the selected shader stages gate submission.
        //Unused optional providers remain diagnostics on the prepared frame.
        var missing = built.Missing.FirstOrDefault()?.Reason;
        if (missing is null && built.RequiresClusteredLighting)
            missing = directDraws is null ? "Current Effect material has no normal node71 device owner."
                : directDraws.PrepareCurrentLighting(frame);
        if (missing is not null)
        {
            foreach (var ticket in ready) CompletePreparation(ticket,new(false, missing));
            if (!everPublished) return null;
            everPublished = false; return publisher.Clear(currentSequence);
        }
        everPublished = true;
        if (mainBankZeroOwnerAllocated && !mainBankZeroOwnerPublished)
            pendingMainBankZeroOwnerSequence ??= currentSequence;
        var awaiting=ready.Where(ticket => !ticket.Completion.Task.IsCompleted).ToArray();
        foreach(var ticket in awaiting)ticket.HasUnacceptedPublication=true;
        awaitingAcceptance[currentSequence] = awaiting;
        return built.Publication;

        G1EffectMapInputFrame ForTicket(Ticket ticket)
        {
            if (ticket.Variant == 0) return frame;
            var selected = storyFrame ?? throw new InvalidOperationException("story_effect.view1_current_frame");
            return selected with { PacketInputs = packet =>
            {
                var inputs = selected.PacketInputs(packet);
                return inputs with { Scope = inputs.Scope with { SourceOwner = ticket.SourceOwner } };
            }};
        }
    }

    private static void CompletePreparation(Ticket ticket,CaptureEffectAdmission result)
    {
        if(!ticket.HasUnacceptedPublication)ticket.Completion.TrySetResult(result);
    }

    internal void Accept(G1EffectGpuPublication? publication)
    {
        if (publication is null) return;
        if (pendingMainBankZeroOwnerSequence == publication.Sequence)
        {
            mainBankZeroOwnerPublished = true;
            pendingMainBankZeroOwnerSequence = null;
        }
        if (!awaitingAcceptance.TryRemove(publication.Sequence, out var accepted)) return;
        foreach (var ticket in accepted)
        {
            ticket.HasUnacceptedPublication=false;
            ticket.Completion.TrySetResult(new(true) { SourceAllocated = ticket.Handle is not null });
        }
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

    // UI publishes one immutable value; only the render worker rebuilds
    // particles. A retired or stopping workspace cannot recolour another owner.
    internal bool SetColour(object source, object expectedOwner, uint colour)
    {
        if (!tickets.TryGetValue(source, out var ticket) || !ReferenceEquals(ticket.Source, expectedOwner) ||
            ticket.Retired || ticket.StopRequested || ticket.StopAccepted.Task.IsCompleted ||
            ticket.Retirement.Task.IsCompleted) return false;
        if (Volatile.Read(ref ticket.Colour).Value != colour) Volatile.Write(ref ticket.Colour, new(colour));
        return true;
    }

    internal Task WaitForRetirement(object source) =>
        tickets.TryGetValue(source, out var ticket) ? ticket.Retirement.Task : Task.CompletedTask;

    // Hard retirement is executed by the render worker before its next global
    // publication. Other effect owners and their shared arena remain intact.
    internal Task Retire(object source)
    {
        if (!tickets.TryGetValue(source, out var ticket)) return Task.CompletedTask;
        ticket.Retired = true; return ticket.Retirement.Task;
    }

    //37F170 stops emission through the normal instance lifecycle. This does
    //not hard-retire the effect or a different owner's shared manager slot.
    internal Task RequestStop(object source)
    {
        if (!tickets.TryGetValue(source, out var ticket) || ticket.Retired || ticket.Retirement.Task.IsCompleted)
            return Task.CompletedTask;
        ticket.StopRequested = true;
        //Natural completion may race the UI's stop request. In that case the
        //render worker's actual retirement also completes the source attempt.
        return Task.WhenAny(ticket.StopAccepted.Task,ticket.Retirement.Task);
    }

    internal void Forget(object source) => tickets.TryRemove(source, out _);

    public void Dispose()
    {
        lock (EffectGate) DisposeCore();
    }

    private void DisposeCore()
    {
        if (disposed) return;
        disposed = true; CancelPending();
        foreach (var ticket in active)
        {
            if (ticket.Handle is { } handle) admission.Retire(handle, instance => instance.Dispose());
            ticket.Instance = null; ticket.Live = false; ticket.Retirement.TrySetResult(true);
        }
        foreach (var ticket in tickets.Values)
        { ticket.Retirement.TrySetResult(true); ticket.StopAccepted.TrySetCanceled(); }
        active.Clear(); context?.Dispose(); context = null;
        tickets.Clear(); storyOwners.Clear(); awaitingAcceptance.Clear(); resources.Clear();
    }
}
