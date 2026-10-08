namespace San14.Core;

public sealed record PlayableOfficerArrivalAftermathResult(GameState State,
    PeriodReportEntry[] Reports);

/// <summary>Provisional appointment cleanup after the caller has actually
/// returned an officer and removed its active Army. This does not execute the
/// original governor selection, Person location writer or Army return lifecycle.</summary>
public static class PlayableOfficerArrivalAftermath
{
    public const string RuleId = "san14:provisional-officer-arrival-assignments-v1";
    public const string AssignmentsClearedReportKind = "officer_arrival_assignments_cleared";

    /// <summary>Returns a detached current world and only the newly generated
    /// reports. Unknown, foreign, absent or still-deployed arrivals are preserved.
    /// The caller publishes these reports at its completed transaction boundary.</summary>
    public static PlayableOfficerArrivalAftermathResult Settle(GameState postArrival,
        IEnumerable<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(postArrival);
        ArgumentNullException.ThrowIfNull(officerIds);
        // An arrival can occur inside period advancement before consumed queues
        // have been cleared. Full StateValidator validation belongs to the caller.
        // Detach before enumerating an external ID source; no provider can retain
        // a reference to this result's dictionaries or auxiliary arrays.
        GameState current = GameStateSnapshot.Detach(postArrival);
        int[] selected = officerIds.Distinct().Order().ToArray();
        var reports = new List<PeriodReportEntry>();
        foreach (int officerId in selected)
        {
            if (!current.Officers.TryGetValue(officerId, out OfficerState? officer) || officer.Id != officerId ||
                officer.Deployed || officer.Presence != OfficerPresence.Present ||
                PlayableOfficerAvailability.IsSourceBusy(current, officer) ||
                current.Units.Values.Any(unit => unit.OfficerId == officerId) ||
                officer.ForceId is not int forceId || !current.Forces.ContainsKey(forceId) ||
                officer.LocationId is not int locationId || officer.DistrictId is not int districtId ||
                !current.Districts.TryGetValue(districtId, out DistrictState? district) ||
                district.Id != districtId || district.ForceId != forceId ||
                !FriendlyArrival(current, locationId, forceId, districtId))
                continue;

            int removed = ClearAssignments(current, officerId, locationId, forceId, districtId);
            OfficerRole role = officer.Role;
            if (role == OfficerRole.Governor &&
                    !current.Cities.Values.Any(city => city.Id == locationId && city.ForceId == forceId &&
                        city.DistrictId == districtId && city.GovernorId == officerId) &&
                    !current.Gates.Values.Any(gate => gate.Id == locationId && gate.ForceId == forceId &&
                        gate.DistrictId == districtId && gate.GovernorId == officerId) ||
                role == OfficerRole.DistrictLeader && district.LeaderId != officerId)
                role = OfficerRole.Officer;

            if (role != officer.Role)
                current.Officers[officerId] = officer with
                {
                    Role = role,
                    // Core roles are not native byte encodings. A provisional
                    // demotion invalidates only that independent status owner.
                    NativeAbilityRuntime = officer.NativeAbilityRuntime is { } ability
                        ? ability with { Status11E = null } : null
                };
            if (removed == 0 && role == officer.Role) continue;
            reports.Add(new(AssignmentsClearedReportKind, forceId,
                current.Cities.ContainsKey(locationId) ? locationId : null, officerId, removed)
            { LocationId = locationId, RuleId = RuleId });
        }
        return new(current, reports.ToArray());
    }

    private static bool FriendlyArrival(GameState state, int locationId, int forceId, int districtId)
    {
        if (state.Cities.TryGetValue(locationId, out CityState? city))
            return city.Id == locationId && city.ForceId == forceId && city.DistrictId == districtId;
        return state.Gates.TryGetValue(locationId, out GateState? gate) && gate.Id == locationId &&
            gate.ForceId == forceId && gate.DistrictId == districtId;
    }

    private static int ClearAssignments(GameState state, int officerId, int locationId,
        int forceId, int districtId)
    {
        int removed = 0;
        foreach (CityState city in state.Cities.Values.ToArray())
        {
            if (city.Id == locationId && city.ForceId == forceId && city.DistrictId == districtId) continue;
            bool governor = city.GovernorId == officerId;
            CityDutyState? duties = city.Duties;
            bool recruiter = duties?.RecruiterId == officerId;
            bool trainer = duties?.TrainerId == officerId;
            if (!governor && !recruiter && !trainer) continue;
            removed += (governor ? 1 : 0) + (recruiter ? 1 : 0) + (trainer ? 1 : 0);
            state.Cities[city.Id] = city with
            {
                GovernorId = governor ? null : city.GovernorId,
                Duties = duties is null ? null : duties with
                {
                    RecruiterId = recruiter ? null : duties.RecruiterId,
                    TrainerId = trainer ? null : duties.TrainerId,
                    PreparationPeriods = recruiter ? 0 : duties.PreparationPeriods
                },
                NativeReturnRuntime = governor && city.NativeReturnRuntime is { } fields
                    ? fields with { Governor2E = null } : city.NativeReturnRuntime
            };
        }
        foreach (GateState gate in state.Gates.Values.Where(gate => gate.GovernorId == officerId &&
                     (gate.Id != locationId || gate.ForceId != forceId || gate.DistrictId != districtId)).ToArray())
        {
            state.Gates[gate.Id] = gate with { GovernorId = null };
            removed++;
        }
        foreach (AreaState area in state.Areas.Values.Where(area => area.Administration?.OverseerId == officerId &&
                     (area.CityId != locationId || area.ForceId is int areaOwner && areaOwner != forceId)).ToArray())
        {
            // Preserve focus and an unknown same-city territory owner. Neither
            // is established by changing the officer's base appointment.
            state.Areas[area.Id] = area with { Administration = area.Administration! with { OverseerId = null } };
            removed++;
        }
        foreach (DistrictState other in state.Districts.Values.Where(other => other.LeaderId == officerId &&
                     (other.Id != districtId || other.ForceId != forceId)).ToArray())
        {
            state.Districts[other.Id] = other with { LeaderId = null };
            removed++;
        }
        return removed;
    }
}
