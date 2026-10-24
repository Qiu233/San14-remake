namespace San14.Core;

/// <summary>Consumes talent and officer relocation plans in their defined order.</summary>
internal static class PreparedPersonnelStage
{
    // Legacy prepared talent orders start real journeys here. New formal
    // talent commands already own their journeys at submission.
    internal static PreparedOrderStageResult ExecuteOwned(GameState periodEntry,
        GameState current, IPlayableTalentResolution talent)
    {
        var officers = current.Officers;
        var cities = current.Cities;
        var reports = new List<PeriodReportEntry>();
        ulong randomState = current.RandomState;
        foreach (PreparedTalentOrder task in (periodEntry.PreparedTalentOrders?.Values ??
                     Enumerable.Empty<PreparedTalentOrder>())
                     .OrderBy(task => task.ExecutorOfficerId))
        {
            if (PlayableOfficerAvailability.IsSourceBusy(current, officers[task.ExecutorOfficerId]))
            {
                reports.Add(new PeriodReportEntry(task.Kind == TalentOrderKind.Search
                    ? "talent_search_cancelled" : "talent_recruit_cancelled", task.ForceId,
                    task.CityId, task.ExecutorOfficerId));
                continue;
            }
            TalentOrderOutcome outcome = talent.Resolve(current with
            {
                Officers = officers, RandomState = randomState
            }, task);
            current = outcome.State ?? throw new InvalidDataException("talent.journey_start_state");
            officers = current.Officers;
            cities = current.Cities;
            randomState = current.RandomState;
            reports.AddRange(outcome.Reports);
        }

        foreach (PreparedOfficerMove move in (periodEntry.PreparedOfficerMoves?.Values ??
                     Enumerable.Empty<PreparedOfficerMove>())
                     .OrderBy(move => move.OfficerId))
        {
            if (PlayableOfficerAvailability.IsSourceBusy(current, officers[move.OfficerId]))
            {
                reports.Add(new PeriodReportEntry("officer_move_cancelled", move.ForceId,
                    move.DestinationCityId, move.OfficerId));
                continue;
            }
            CityState target = cities[move.DestinationCityId];
            officers[move.OfficerId] = officers[move.OfficerId] with
            {
                LocationId = target.Id, DistrictId = target.DistrictId,
                // This completed assignment actually placed the officer in the
                // destination city. Keep both independent source channels in
                // step with the domain assignment instead of discarding its
                // position and retaining membership of the previous district.
                LocationState = new(new(OfficerPositionKind.City, target.Id),
                    target.DistrictId is int districtId
                        ? new(OfficerDistrictMembershipKind.District, districtId)
                        : OfficerDistrictMembership.Unaffiliated)
            };
            reports.Add(new PeriodReportEntry("officer_move", move.ForceId,
                target.Id, move.OfficerId, 0, cities[move.SourceCityId].HexId, target.HexId));
        }

        return new(current with { Officers = officers, RandomState = randomState }, reports.ToArray());
    }
}
