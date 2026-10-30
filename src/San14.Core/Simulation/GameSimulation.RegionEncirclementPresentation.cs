namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryCompleteRegionEncirclementPresentation(GameState expectedOwner,
        RegionEncirclementPresentation expectedRequest)
    {
        if (_progress is not null) return false;
        var pending = _normalPeriod;
        var owner = pending?.Candidate ?? State;
        if (!ReferenceEquals(owner, expectedOwner) ||
            !RegionEncirclementPlaybackRules.TryComplete(owner, expectedRequest, out var next)) return false;
        if (pending is not null) _normalPeriod = pending with { Candidate = next };
        else State = next;
        return true;
    }

    /// <summary>429370's category4 obstacle destruction has an actual effect80
    /// and spatial soundC4 consumer. Queueing retirement is not its receipt.</summary>
    public bool TryCompleteOccupationMapObjectPresentation(GameState expectedOwner,
        long batchSequence, int requestIndex, MapObjectPresentationRequest expectedRequest)
    {
        var expectedBatch = expectedOwner.CampaignEffectRequests?.PendingBatches
            .FirstOrDefault(batch => batch.Sequence == batchSequence);
        if (expectedBatch is null || !MapObjectPresentationSources.Supports(expectedBatch.Source, expectedRequest)) return false;
        if (!TryReadSourcePresentationHead(expectedOwner, expectedBatch.Source,
            batchSequence, requestIndex, out var owner, out var pending, out var effects,
            out int batch, out var request) || !ReferenceEquals(request.MapObjectPresentation, expectedRequest)) return false;
        CompleteSourcePresentationHead(owner, pending, effects, batch, requestIndex,
            owner.ActiveMonthlyScene, owner.CoalitionCameraPresentation);
        return true;
    }
}
