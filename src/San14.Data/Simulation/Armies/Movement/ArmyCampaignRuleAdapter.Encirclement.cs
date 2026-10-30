using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Consumes the campaign encirclement boundary, not Army stage11.
    /// Territory, region and report writes commit together. The optional service
    /// belongs to the application profile, independently of the world save.</summary>
    public RegionEncirclementResult TryConsumeRegionEncirclement(GameState state, TransferServiceState? service = null)
        => ConsumeRegionEncirclement(state, service, null);

    internal GameState ConsumeNormalTurnEncirclementCurrent(GameState state, SourceWorldContext current)
    {
        var result = ConsumeRegionEncirclement(state, null, current);
        return result.State ?? throw OccupationMissing(result.UnresolvedOwner ?? "encirclement_current_operation");
    }

    private RegionEncirclementResult ConsumeRegionEncirclement(GameState state, TransferServiceState? service,
        SourceWorldContext? suppliedCurrent)
    {
        try
        {
            if (!OccupationKnown(BattleRemakeCurrentSettings.ReadEncirclementEnabled(state), "encirclement_setting"))
                return new(state, [], null) { Service = service?.Copy() }; //429560 skips the manager; it does not clear its map.
            var pending = state.RegionOccupationFollowups ?? throw OccupationMissing("encirclement_queue");
            if (pending.EncirclementSeeds.Count == 0) return new(state, [], null) { Service = service?.Copy() };
            if (_scenario.Snapshot.WorldParameters?.RawField48 is not int mode || mode is >= 100 and <= 199)
                throw OccupationMissing("encirclement_special_area_reset");
            var order = state.ForceProcessingOrder?.ForceIds ?? throw OccupationMissing("encirclement_saved_force_order");
            if (order.Any(id => id is < 1 or > 51) || order.Distinct().Count() != order.Count)
                throw OccupationMissing("encirclement_force_order_identity");
            var ranks = _scenario.Snapshot.RankParameters;
            if (ranks is not { FormatMarker: 0x66, RankRecordStride: 166, Ranks.Count: 13 } ||
                state.ContentHashes.GetValueOrDefault(_scenario.FixedDataSource.ResourceUri) != _scenario.FixedDataSource.Sha256)
                throw OccupationMissing("encirclement_rank_definitions");
            var working = state;
            var serviceCopy = service?.Copy();
            var current = suppliedCurrent ?? CaptureWritableTransportRouteCurrent(working);
            var evaluations = new List<RegionEncirclementEvaluation>();
            //32C9D0 resolves saved lookup operands into a pointer-list before
            //42D6B0 starts at its tail. Seed keys use each actual receiver's
            //physical identity; rank47 belongs to that same receiver.
            var forceReferences = order.Select(raw => current.ReadForceTableReference(raw)
                ?? throw OccupationMissing("encirclement_saved_Force_pointer")).ToArray();
            foreach (var forceReference in forceReferences.Reverse())
            {
                int force = OccupationKnown(current.ReadForcePhysicalSlot(forceReference), "encirclement_Force_receiver");
                int rank = OccupationKnown(current.ReadForceRankSlot47(force), "encirclement_current_rank");
                int selected = rank < 13 ? rank : 0;
                bool allowed = false;
                if (selected != 0) //CRankData slot3 tests the physical rank index.
                {
                    var definition = ranks.Ranks[selected];
                    byte[] raw = Convert.FromHexString(definition.RawBytesHex);
                    if (definition.Id != selected || raw.Length != 166)
                        throw OccupationMissing("encirclement_rank_record");
                    allowed = (raw[162] & 1) != 0;
                }
                //Copy each force's pending seeds in insertion order, including duplicates.
                foreach (var seed in pending.EncirclementSeeds.Where(x => x.ForceId == force))
                {
                    current = suppliedCurrent ?? CaptureWritableTransportRouteCurrent(working);
                    var candidates = new List<RegionEncirclementCandidate>();
                    foreach (int candidate in allowed ? RegionEncirclementSource.ExpandCandidates(working, current, seed.AreaId, force) : [])
                    {
                        current = suppliedCurrent ?? CaptureWritableTransportRouteCurrent(working);
                        var evaluated = RegionEncirclementSource.EvaluateCandidate(working, current, candidate, force);
                        candidates.Add(evaluated);
                        if (evaluated.Enclosed && evaluated.CapturedAreas.Length != 0)
                            working = ApplyEncirclementComponent(working, current, force, evaluated, serviceCopy);
                    }
                    evaluations.Add(new(force, seed.AreaId, rank, allowed, candidates.ToArray()));
                }
            }
            //The original finally clears the entire map, including keys absent
            //from the previously saved force order. Notifications have another owner.
            return new(working with { RegionOccupationFollowups = new([], working.RegionOccupationFollowups!.Announcements,
                working.RegionOccupationFollowups.EncirclementPresentations) }, evaluations.ToArray(), null) { Service = serviceCopy };
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }
}
