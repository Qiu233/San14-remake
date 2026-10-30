using San14.Core;

namespace San14.Data;

/// <summary>The actual map-entry connection roster followed by source-time
/// construction and retirement intentions. This owner does not discover new
/// pairs from current adjacency or acknowledge presentation requests.</summary>
public sealed class FacilitySceneConnectionOwner
{
    private IReadOnlyList<FacilitySceneConnection> connections;
    private GameState lastWorld;
    private CampaignEffectRequests lastOutbox;
    private MapObjectLifecycleState lastObjects;
    private long capturedBatchSequence;

    /// <summary>Initial must be QueryInitialFacilitySceneConnections(initialWorld)
    /// from this map entry. That source builds all current live pairs, including
    /// the current result of any pending construction. Old pending intentions
    /// therefore belong before this baseline, rather than being replayed on it.</summary>
    public FacilitySceneConnectionOwner(IReadOnlyList<FacilitySceneConnection> initial, GameState initialWorld)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(initialWorld);
        connections = Array.AsReadOnly(initial.Select(row => row ??
            throw new InvalidDataException("facility.connections.initial_pair")).ToArray());
        lastWorld = initialWorld;
        lastOutbox = initialWorld.CampaignEffectRequests ?? throw Missing("outbox");
        lastObjects = initialWorld.MapObjects ?? throw Missing("map_objects");
        capturedBatchSequence = lastOutbox.LastBatchSequence;
    }

    private FacilitySceneConnectionOwner(FacilitySceneConnectionOwner source)
    {
        // Every roster is frozen when it is installed. The fork replaces its
        // own roster and cursor without advancing the committed map owner.
        connections = source.connections;
        lastWorld = source.lastWorld;
        lastOutbox = source.lastOutbox;
        lastObjects = source.lastObjects;
        capturedBatchSequence = source.capturedBatchSequence;
    }

    public FacilitySceneConnectionOwner ForkPresentation() => new(this);

    public IReadOnlyList<FacilitySceneConnection> Capture(GameState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (ReferenceEquals(current, lastWorld)) return connections;
        var outbox = current.CampaignEffectRequests ?? throw Missing("outbox");
        var objects = current.MapObjects ?? throw Missing("map_objects");
        if (outbox.LastBatchSequence < capturedBatchSequence)
            throw Missing("another_campaign_or_restored_outbox");
        if (ReferenceEquals(outbox, lastOutbox) && ReferenceEquals(objects, lastObjects))
        { lastWorld = current; return connections; }

        List<FacilitySceneConnection>? changed = null;
        IReadOnlyList<FacilitySceneConnection> Rows() => changed is null ? connections : changed;
        List<FacilitySceneConnection> Change() => changed ??= new(connections);
        void Remove(MapObjectReceiverId receiver)
        {
            if (Rows().Any(row => row.ReceiverId == receiver || row.AdjacentReceiverId == receiver))
                Change().RemoveAll(row => row.ReceiverId == receiver || row.AdjacentReceiverId == receiver);
        }

        // A batch is immutable after append except that acknowledgments remove
        // requests. Consume each newly appended batch in source array/index
        // order once; later removal shifts request indices but cannot replay it.
        foreach (var batch in outbox.PendingBatches)
        {
            if (batch.Sequence <= capturedBatchSequence) continue;
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                var request = batch.Requests[index];
                if (request.MapObjectPresentation is { Event: MapObjectPresentationEvent.RetirementVisualDetach } retired)
                    Remove(retired.ReceiverId);
                else if (request.FacilityConstructionPresentation is
                    { Operation: FacilityConstructionPresentationOperation.ConnectAdjacentFacility } created)
                {
                    var adjacent = created.AdjacentReceiverId ?? throw Missing("adjacent_receiver");
                    if (!Rows().Any(row => row.ReceiverId == created.ReceiverId && row.AdjacentReceiverId == adjacent ||
                        row.ReceiverId == adjacent && row.AdjacentReceiverId == created.ReceiverId))
                        Change().Add(new(created.ReceiverId, adjacent));
                }
            }
        }

        // Reset receivers retain their directory identities. Remove their old
        // connections now, so subsequent reuse cannot revive a former pair.
        // Only endpoints in this small roster are examined, not the whole map.
        foreach (var pair in Rows().ToArray())
            if (Reset(pair.ReceiverId) || Reset(pair.AdjacentReceiverId))
                Change().Remove(pair);

        if (changed is not null && !connections.SequenceEqual(changed))
            connections = Array.AsReadOnly(changed.ToArray());
        capturedBatchSequence = outbox.LastBatchSequence;
        lastWorld = current; lastOutbox = outbox; lastObjects = objects;
        return connections;

        bool Reset(MapObjectReceiverId receiver)
        {
            var row = objects.Find(receiver) ?? throw Missing("receiver");
            return row.Subject is null || row.Pose?.Kind == MapObjectPoseKind.None;
        }
    }

    private static InvalidDataException Missing(string owner) => new("facility.connections." + owner);
}
