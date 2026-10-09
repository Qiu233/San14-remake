namespace San14.Core;

/// <summary>Consumes talent and officer relocation plans in their defined order.</summary>
internal static class PreparedPersonnelStage
{
    // The dictionaries belong to this period transaction. The talent provider
    // receives the period-entry world with current officers/RNG, as before;
    // unrelated pending work and original source snapshots are not normalized.
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
            TalentOrderOutcome outcome = talent.Resolve(periodEntry with
            {
                Officers = officers, RandomState = randomState
            }, task);
            randomState = outcome.RandomState;
            if (outcome.JoinedOfficerId is not int joinedId)
            {
                reports.Add(new PeriodReportEntry(task.Kind == TalentOrderKind.Search
                    ? "talent_search_none" : "talent_recruit_failed", task.ForceId,
                    task.CityId, task.Kind == TalentOrderKind.Search
                        ? task.ExecutorOfficerId : task.TargetOfficerId));
                continue;
            }
            if (!officers.TryGetValue(joinedId, out OfficerState? joined) ||
                joined.ForceId is not null || joined.LocationId != task.CityId ||
                (task.Kind == TalentOrderKind.Search
                    ? joined.Presence != OfficerPresence.Undiscovered
                    : joined.Id != task.TargetOfficerId || joined.Role != OfficerRole.Free ||
                      joined.Presence != OfficerPresence.Present))
                throw new InvalidDataException("Talent outcome does not match the pending source officer.");
            officers[joinedId] = joined with
            {
                ForceId = task.ForceId, DistrictId = task.DistrictId,
                Role = OfficerRole.Officer, Presence = OfficerPresence.Present,
                Deployed = false
            };
            reports.Add(new PeriodReportEntry(task.Kind == TalentOrderKind.Search
                ? "talent_search_joined" : "talent_recruited", task.ForceId,
                task.CityId, joinedId));
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
                NativeLocationRuntime = officers[move.OfficerId].NativeLocationRuntime is { } location
                    ? location with { Second11C = null } : null
            };
            reports.Add(new PeriodReportEntry("officer_move", move.ForceId,
                target.Id, move.OfficerId, 0, cities[move.SourceCityId].HexId, target.HexId));
        }

        return new(current with { Officers = officers, RandomState = randomState }, reports.ToArray());
    }
}
