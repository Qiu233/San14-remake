using San14.Data;

/// <summary>An ordinary node owns its beam handles and source update counter.
/// Name clips and one-shot result effects retain their separate lifetimes.</summary>
internal sealed class OrdinaryTacticMapNode : IDisposable
{
    private sealed record Beam(int Slot, object Owner, Task<CaptureEffectAdmission> Admission);
    private readonly MapSceneView map;
    private readonly OrdinaryTacticPresentationPublication publication;
    private readonly List<Beam> beams = [];
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    internal int Counter { get; private set; }
    internal Task<CaptureEffectAdmission>? Results { get; private set; }
    internal Task? Retirement { get; private set; }
    internal bool CanRetire { get; private set; }
    internal long Sequence => publication.Sequence;
    internal Task BeginRetirement() => Retirement ??= Task.WhenAll(beams.Select(beam => map.RetireTacticBeam(beam.Owner)));

    internal OrdinaryTacticMapNode(MapSceneView owner, OrdinaryTacticPresentationPublication source,
        bool resultsAlreadyPublished)
    {
        map = owner; publication = source;
        Counter = resultsAlreadyPublished ? -1 : 30;
        var placements = source.Beams.CreatePlacements(map.QueryTacticViewPose);
        if (placements.Value is null) throw new InvalidDataException(placements.UnresolvedInput);
        foreach (var placement in placements.Value)
        {
            object handleOwner = new();
            beams.Add(new(placement.SlotIndex, handleOwner,
                map.EnqueueTacticBeam(handleOwner, source.Beams.LinkedActors[placement.SlotIndex].Actor,
                    placement.Transform, lifetime.Token)));
        }
    }

    internal void Update(Func<Task<CaptureEffectAdmission>> publishResults)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var origin = map.QueryTacticViewPose(publication.Beams.PrimaryView).Resolve();
        bool held = false;
        if (OrdinaryTacticBeamMath.HasOrigin(origin))
            foreach (var beam in beams)
            {
                if (beam.Admission.IsCompletedSuccessfully && !beam.Admission.Result.Completed)
                    throw new InvalidDataException(beam.Admission.Result.UnresolvedInput);
                var linked = publication.Beams.LinkedActors[beam.Slot];
                var view = map.QueryTacticViewPose(linked.ViewIdentity);
                if (!view.NativeValid || !map.HasLiveTacticBeam(beam.Owner)) continue;
                if (OrdinaryTacticBeamMath.TryCreate(origin, view.Resolve(), out var transform))
                    map.UpdateTacticBeam(beam.Owner, transform);
                held = true;
            }
        if (Counter > -45)
        {
            if (Counter == 0) Results ??= publishResults();
            Counter--;
        }
        else CanRetire = !held;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel(); lifetime.Dispose();
        _ = BeginRetirement();
        beams.Clear();
    }
}
