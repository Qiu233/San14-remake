namespace San14.Core;

/// <summary>A destination quote is independent of original prisoner and surrender rules.
/// Null, or a quote without a destination, preserves the unresolved officer outcome.</summary>
public sealed record PlayableBaseCaptureAftermathQuote(int? DestinationLocationId,
    string RuleId, string[] MissingRuleKeys);

public interface IPlayableBaseCaptureAftermathRules
{
    PlayableBaseCaptureAftermathQuote? Quote(GameState postCapture, int capturedLocationId,
        int defendingForceId, OfficerState officer, PlayableLandRoutes routes);
}

public sealed record PlayableBaseCaptureAftermathResult(GameState State,
    PeriodReportEntry[] Reports);

/// <summary>Immediate evacuation to the closest reachable friendly base is an
/// explicitly provisional policy. It does not resolve prisoners, surrender,
/// original travel time or a defending force without a reachable base.</summary>
public sealed class ProvisionalPlayableBaseCaptureAftermathRules : IPlayableBaseCaptureAftermathRules
{
    public const string RuleId = "san14:provisional-base-capture-evacuation-v1";
    public static ProvisionalPlayableBaseCaptureAftermathRules Instance { get; } = new();
    private ProvisionalPlayableBaseCaptureAftermathRules() { }

    public PlayableBaseCaptureAftermathQuote? Quote(GameState postCapture,
        int capturedLocationId, int defendingForceId, OfficerState officer,
        PlayableLandRoutes routes)
    {
        int? origin = postCapture.Cities.TryGetValue(capturedLocationId, out CityState? city)
            ? city.HexId : postCapture.Gates[capturedLocationId].HexId;
        if (origin is not int originHex)
            return new(null, RuleId, ["capture.officer_origin"]);
        var candidates = postCapture.Cities.Values
            .Where(candidate => candidate.Id != capturedLocationId && candidate.ForceId == defendingForceId)
            .Select(candidate => (candidate.Id, candidate.HexId, candidate.DistrictId))
            .Concat(postCapture.Gates.Values
                .Where(candidate => candidate.Id != capturedLocationId && candidate.ForceId == defendingForceId)
                .Select(candidate => (candidate.Id, candidate.HexId, candidate.DistrictId)))
            .ToArray();
        if (candidates.Length == 0)
            return new(null, RuleId, ["capture.officer_home"]);
        var reachable = candidates
            .Where(candidate => candidate.HexId is not null && candidate.DistrictId is int districtId &&
                postCapture.Districts.TryGetValue(districtId, out DistrictState? district) &&
                district.ForceId == defendingForceId)
            .Select(candidate => (candidate.Id, Distance: candidate.HexId == originHex
                ? 0 : routes.Route(originHex, candidate.HexId!.Value).Length))
            .Where(candidate => candidate.Distance > 0 ||
                candidates.Any(home => home.Id == candidate.Id && home.HexId == originHex))
            .OrderBy(candidate => candidate.Distance).ThenBy(candidate => candidate.Id)
            .ToArray();
        return reachable.Length == 0
            ? new(null, RuleId, ["capture.officer_route"])
            : new(reachable[0].Id, RuleId, []);
    }
}

/// <summary>Settles residents after ownership has changed. The caller supplies
/// the previous owner explicitly. Native deployed commanders and busy Return
/// members use their own Home writer before provisional resident evacuation.
/// Unknown native outcomes preserve the live state.</summary>
public static class PlayableBaseCaptureAftermath
{
    public const string UnresolvedRuleId = "capture.officer_evacuation";
    public const string EvacuatedReportKind = "base_capture_officer_evacuated";
    public const string UnresolvedReportKind = "base_capture_officer_unresolved";
    public const string ReturningHomeUnresolvedRuleId = "capture.returning_person_home";
    public const string ArmyHomeUnresolvedRuleId = "capture.deployed_army_home";

    public static PlayableBaseCaptureAftermathResult Settle(GameState postCapture,
        int capturedLocationId, int? previousForceId, PlayableLandRoutes routes,
        IPlayableBaseCaptureAftermathRules? rules = null,
        IPlayableCaptureReturningPersonRules? returningPersonRules = null,
        CityState? beforeCaptureCity = null,
        IPlayableCaptureArmyHomeRules? armyHomeRules = null)
    {
        ArgumentNullException.ThrowIfNull(postCapture);
        ArgumentNullException.ThrowIfNull(routes);
        routes.ValidateWorld(postCapture);
        var captured = Base(postCapture, capturedLocationId);
        int? owner = captured.ForceId;
        int? sourceHex = captured.HexId;
        if (owner == previousForceId || owner is null || !postCapture.Forces.ContainsKey(owner.Value) ||
            previousForceId is int previous && !postCapture.Forces.ContainsKey(previous))
            throw new InvalidDataException("Base aftermath requires a completed takeover and its actual previous owner.");

        // This helper can run inside the period transaction, whose consumed
        // prepared orders have not yet been cleared. The full StateValidator
        // belongs to the caller's completed period boundary.
        GameState current = GameStateSnapshot.Detach(postCapture);
        var reports = new List<PeriodReportEntry>();
        if (current.Cities.TryGetValue(capturedLocationId, out CityState? capturedCity) &&
            capturedCity.NativeReturnRuntime is { } capturedFields &&
            //The ordinary CE takeover writer preserves2E when the new
            //District is valid (TC2851B0→26DA10→26D2E0). Changing the Core
            //Governor projection cannot invent a native clear or unknown.
            //The later actual governor updater owns its replacement.
            !(current.NativeArmySimulation is not null && capturedCity.DistrictId is >= 1 and <= 51))
            current.Cities[capturedCity.Id] = capturedCity with
            { NativeReturnRuntime = capturedFields with { Governor2E = null } };
        if (previousForceId is not int defendingForceId)
            return new(current, []);

        var capturedArmies = postCapture.Units.Values.Where(army => army.ForceId == defendingForceId &&
                     army.NativeArmyOrigin is not null && army.Mission?.Kind != UnitMissionKind.Transport &&
                     postCapture.Officers[army.OfficerId].LocationId == capturedLocationId)
            .ToDictionary(army => army.Id);
        //The source CFoothold Person iterator uses the manager100 chain;
        //Core UnitId/allocation order does not determine its callback order.
        int[]? armyOrder = capturedArmies.Count == 0 ? [] :
            armyHomeRules?.ReadCapturedArmyHomeOrder(current, capturedLocationId, defendingForceId);
        foreach (int unitId in armyOrder ?? [])
        {
            if (!capturedArmies.Remove(unitId, out UnitState? army)) continue;
            var changed = beforeCaptureCity is null ? null : armyHomeRules?.ChangeCapturedArmyHome(current,
                capturedLocationId, defendingForceId, current.Units[army.Id], beforeCaptureCity);
            if (changed is null)
                Unresolved(postCapture.Officers[army.OfficerId], ArmyHomeUnresolvedRuleId, [ArmyHomeUnresolvedRuleId]);
            else
            {
                current = changed.State;
                var destination = Base(current, changed.DestinationLocationId);
                reports.Add(new("army_home_changed", defendingForceId,
                    current.Cities.ContainsKey(changed.DestinationLocationId) ? changed.DestinationLocationId : null,
                    army.OfficerId, FromHexId: sourceHex, ToHexId: destination.HexId)
                { LocationId = changed.DestinationLocationId, RuleId = changed.RuleId });
            }
        }
        foreach (UnitState army in capturedArmies.Values)
            Unresolved(postCapture.Officers[army.OfficerId], ArmyHomeUnresolvedRuleId, [ArmyHomeUnresolvedRuleId]);

        IPlayableBaseCaptureAftermathRules policy = rules ?? ProvisionalPlayableBaseCaptureAftermathRules.Instance;
        PlayableBaseCaptureAftermathQuote? provisionalQuote = null;
        foreach (OfficerState officer in postCapture.Officers.Values
                     .Where(officer => officer.ForceId == defendingForceId &&
                         officer.LocationId == capturedLocationId && current.Officers[officer.Id].LocationId == capturedLocationId && !officer.Deployed &&
                         !postCapture.Units.Values.Any(unit => unit.OfficerId == officer.Id) &&
                         officer.Presence is null or OfficerPresence.Present)
                     //Finite source Home changes read actual governor/status
                     //owners. Resolve them before provisional evacuation can
                     //invalidate those fields on other residents.
                     .OrderBy(officer => PlayableOfficerAvailability.IsSourceBusy(postCapture, officer) ? 0 : 1)
                     .ThenBy(officer => officer.Id))
        {
            if (officer.Presence is null)
            {
                Unresolved(officer, UnresolvedRuleId, ["capture.officer_presence"]);
                continue;
            }
            if (PlayableOfficerAvailability.IsCaptive(current.Officers[officer.Id]))
            {
                Unresolved(officer, "capture.captive_person_outcome", ["capture.captive_person_outcome"]);
                continue;
            }
            if (PlayableOfficerAvailability.IsSourceBusy(current, current.Officers[officer.Id]))
            {
                //A live Return member is elsewhere. The provisional resident
                //evacuation must not clear its independently owned E4/status.
                var returned = beforeCaptureCity is not null
                    ? returningPersonRules?.ChangeCapturedHome(current, capturedLocationId,
                        defendingForceId, current.Officers[officer.Id], beforeCaptureCity)
                    : returningPersonRules?.ChangeCapturedHome(current,
                        capturedLocationId, defendingForceId, current.Officers[officer.Id]);
                if (returned is null)
                    Unresolved(officer, ReturningHomeUnresolvedRuleId, [ReturningHomeUnresolvedRuleId]);
                else
                {
                    current = returned.State;
                    var destination = Base(current, returned.DestinationLocationId);
                    reports.Add(new(EvacuatedReportKind, defendingForceId,
                        current.Cities.ContainsKey(returned.DestinationLocationId) ? returned.DestinationLocationId : null,
                        officer.Id, FromHexId: sourceHex, ToHexId: destination.HexId)
                    { LocationId = returned.DestinationLocationId, RuleId = returned.RuleId });
                }
                continue;
            }
            // Optional quote providers never receive caller-owned dictionaries,
            // arrays or the mutable transaction used to publish the result.
            PlayableBaseCaptureAftermathQuote? quote;
            if (ReferenceEquals(policy, ProvisionalPlayableBaseCaptureAftermathRules.Instance))
                quote = provisionalQuote ??= policy.Quote(postCapture,
                    capturedLocationId, defendingForceId, officer, routes);
            else
            {
                GameState quoteState = GameStateSnapshot.Detach(postCapture);
                quote = policy.Quote(quoteState, capturedLocationId, defendingForceId,
                    quoteState.Officers[officer.Id], routes);
            }
            if (quote is null)
            {
                Unresolved(officer, UnresolvedRuleId, [UnresolvedRuleId]);
                continue;
            }
            if (string.IsNullOrWhiteSpace(quote.RuleId) || quote.MissingRuleKeys is null ||
                quote.MissingRuleKeys.Any(string.IsNullOrWhiteSpace) ||
                quote.DestinationLocationId.HasValue == (quote.MissingRuleKeys.Length != 0))
                throw new InvalidDataException("Base aftermath quote must resolve a destination or explicitly identify missing rules.");
            if (quote.DestinationLocationId is not int destinationId)
            {
                Unresolved(officer, quote.RuleId, quote.MissingRuleKeys.ToArray());
                continue;
            }
            (int? destinationOwner, int? destinationHex, int? destinationDistrict) = Base(current, destinationId);
            if (destinationId == capturedLocationId || destinationOwner != defendingForceId ||
                destinationHex is not int arrivalHex || destinationDistrict is not int districtId ||
                !current.Districts.TryGetValue(districtId, out DistrictState? district) ||
                district.ForceId != defendingForceId || sourceHex is not int departureHex ||
                departureHex != arrivalHex && routes.Route(departureHex, arrivalHex).Length == 0)
                throw new InvalidDataException("Base aftermath quoted a foreign, unknown or unreachable destination.");

            ClearAppointments(current, officer.Id, districtId);
            OfficerState resident = current.Officers[officer.Id];
            OfficerRole role = officer.Role;
            if (role == OfficerRole.Governor &&
                !current.Cities.Values.Any(city => city.GovernorId == officer.Id) &&
                !current.Gates.Values.Any(gate => gate.GovernorId == officer.Id) ||
                role == OfficerRole.DistrictLeader &&
                !current.Districts.Values.Any(active => active.LeaderId == officer.Id))
                role = OfficerRole.Officer;
            current.Officers[officer.Id] = resident with
            {
                LocationId = destinationId, DistrictId = districtId, Role = role,
                //No native location/status writer ran in this provisional
                //evacuation. Keep its actual E4/status; later source governor
                //queries can exclude this Person at the old physical location.
                LocationState = resident.LocationState,
                AbilityState = resident.AbilityState
            };
            reports.Add(new(EvacuatedReportKind, defendingForceId,
                current.Cities.ContainsKey(destinationId) ? destinationId : null,
                officer.Id, FromHexId: departureHex, ToHexId: arrivalHex)
            { LocationId = destinationId, RuleId = quote.RuleId });
        }
        return new(current, reports.ToArray());

        void Unresolved(OfficerState officer, string ruleId, string[] missingRules) =>
            reports.Add(new(UnresolvedReportKind, defendingForceId,
                current.Cities.ContainsKey(capturedLocationId) ? capturedLocationId : null,
                officer.Id, FromHexId: sourceHex, ToHexId: sourceHex)
            { LocationId = capturedLocationId, RuleId = ruleId, MissingRuleKeys = missingRules });
    }

    private static (int? ForceId, int? HexId, int? DistrictId) Base(GameState state, int locationId)
    {
        if (state.Cities.TryGetValue(locationId, out CityState? city))
            return (city.ForceId, city.HexId, city.DistrictId);
        if (state.Gates.TryGetValue(locationId, out GateState? gate))
            return (gate.ForceId, gate.HexId, gate.DistrictId);
        throw new InvalidDataException("Base aftermath references a missing city or gate.");
    }

    private static void ClearAppointments(GameState state, int officerId, int destinationDistrictId)
    {
        foreach (CityState city in state.Cities.Values.ToArray())
        {
            bool governor = city.GovernorId == officerId;
            CityDutyState? duties = city.Duties;
            bool recruiter = duties?.RecruiterId == officerId;
            bool trainer = duties?.TrainerId == officerId;
            if (!governor && !recruiter && !trainer) continue;
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
        foreach (GateState gate in state.Gates.Values.Where(gate => gate.GovernorId == officerId).ToArray())
            state.Gates[gate.Id] = gate with { GovernorId = null };
        foreach (AreaState area in state.Areas.Values.Where(area =>
                     area.Administration?.OverseerId == officerId).ToArray())
            state.Areas[area.Id] = area with
            { Administration = area.Administration! with { OverseerId = null } };
        foreach (DistrictState district in state.Districts.Values.Where(district =>
                     district.Id != destinationDistrictId && district.LeaderId == officerId).ToArray())
        {
            state.Districts[district.Id] = district with { LeaderId = null };
            // This operation actually cleared the appointment. Preserve that
            // known empty word in its existing saved source owner as well.
            if (state.NativeForceUnregisterCurrent?.DistrictWindows10.TryGetValue(district.Id, out var fields) == true)
            { fields[2] = 0; fields[3] = 0; }
        }
    }
}
