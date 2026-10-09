namespace San14.Core;

/// <summary>Historical last-City military/person roster, separate from current
/// Armies. Source-resolved people and unresolved dispositions remain distinct;
/// no prisoner, surrender, soldier or cargo outcome is inferred.</summary>
public sealed record PlayableForceDefeatRecord(int ForceId, int Period,
    int CapturedCityId, int CapturingForceId, string RuleId,
    UnitState[] WithdrawnUnits, int[] UnresolvedOfficerIds, string[] MissingRuleKeys)
{
    //Absent old saves have only their explicit unresolved roster. Historical
    //Armies remain intact even after their current commanders are resolved.
    public int[]? ResolvedOfficerIds { get; init; }
    public string? NativeUnregisterRuleId { get; init; }
}

public sealed record PlayableForceDefeatObservation(int ForceId, bool WasActive,
    UnitState[] Units, int[] OfficerIds);

public sealed partial record GameState
{
    public Dictionary<int, PlayableForceDefeatRecord> ForceDefeats { get; init; } = [];
}

public sealed record PlayableForceDefeatResult(GameState State, PeriodReportEntry[] Reports);

/// <summary>The manual establishes last-city defeat. Withdrawal publication
/// and its position in the provisional period are explicitly replaceable;
/// native force/Army cleanup and person/cargo outcomes remain unresolved.</summary>
public static class PlayableForceDefeat
{
    public const string LastCityRuleId = "san14:ce-manual-last-city-force-defeat";
    public const string WithdrawalRuleId = "san14:provisional-force-defeat-withdrawal-v1";
    //The current original tables use ordinary Force1..45, tribal Force46..50,
    //and City47..51 for tribal bases. These are the original profile's IDs,
    //not a general restriction on future content or map schemas.
    public static bool IsOrdinaryForce(int forceId) => forceId is >= 1 and <= 45;
    public static bool IsConquestCity(int cityId) => cityId is not (>= 47 and <= 51);
    public static bool OwnsCity(GameState state, int forceId) => state.Cities.Values.Any(city =>
        IsConquestCity(city.Id) && city.ForceId == forceId);

    public static PlayableForceDefeatObservation ObserveBeforeDispositions(GameState state, int forceId) => new(
        forceId, state.Forces[forceId].IsActive,
        state.Units.Values.Where(unit => unit.ForceId == forceId).OrderBy(unit => unit.Id).ToArray(),
        state.Officers.Values.Where(person => person.ForceId == forceId).Select(person => person.Id).Order().ToArray());

    public static PlayableForceDefeatResult AfterCityCapture(GameState state,
        int capturedCityId, int? previousForceId, int capturingForceId, int nextPeriod,
        PlayableForceDefeatObservation? beforeDispositions = null, int[]? resolvedOfficerIds = null,
        string? nativeUnregisterRuleId = null)
    {
        if (!state.Cities.TryGetValue(capturedCityId, out CityState? city) || city.ForceId != capturingForceId ||
            !state.Forces.ContainsKey(capturingForceId) || nextPeriod != checked(state.PeriodNumber + 1))
            throw new InvalidDataException("Force defeat requires this period's completed city takeover.");
        if (previousForceId is not int forceId || !IsOrdinaryForce(forceId) || forceId == capturingForceId ||
            !state.Forces.TryGetValue(forceId, out ForceState? force) ||
            !(beforeDispositions?.WasActive ?? force.IsActive) || OwnsCity(state, forceId) || state.ForceDefeats.ContainsKey(forceId))
            return new(state, []);
        if (beforeDispositions is not null && beforeDispositions.ForceId != forceId)
            throw new InvalidDataException("Force defeat observation belongs to a different Force.");

        GameState current = GameStateSnapshot.Detach(state);
        UnitState[] withdrawn = beforeDispositions?.Units ?? current.Units.Values.Where(unit => unit.ForceId == forceId).OrderBy(unit => unit.Id).ToArray();
        int[] people = beforeDispositions?.OfficerIds ?? current.Officers.Values.Where(person => person.ForceId == forceId).Select(person => person.Id).Order().ToArray();
        int[] resolved = (resolvedOfficerIds ?? []).Distinct().Order().ToArray();
        if (resolved.Any(person => !people.Contains(person)))
            throw new InvalidDataException("Resolved force-defeat Person was absent from the actual former roster.");
        string[] missing = ["force.defeat.native_cleanup", "force.defeat.officer_outcomes", "force.defeat.base_land_disposition",
            "force.defeat.soldier_cargo_disposition"];
        if (nativeUnregisterRuleId is not null)
            missing = missing.Where(key => key != "force.defeat.native_cleanup").ToArray();
        if (resolvedOfficerIds is not null && people.Except(resolved).Any() == false)
            missing = missing.Where(key => key != "force.defeat.officer_outcomes").ToArray();
        current.Forces[forceId] = current.Forces[forceId] with
        { IsActive = false, Governance = current.Forces[forceId].Governance is { } governance ? governance with { Pending = null } : null };
        current.ForceDefeats.Add(forceId, new(forceId, nextPeriod, capturedCityId, capturingForceId,
            WithdrawalRuleId, withdrawn, people.Except(resolved).ToArray(), missing)
            { ResolvedOfficerIds = resolvedOfficerIds is null ? null : resolved,
                NativeUnregisterRuleId = nativeUnregisterRuleId });
        var reports = new List<PeriodReportEntry>
        {
            new("force_defeated", forceId, capturedCityId) { LocationId = capturedCityId, RuleId = LastCityRuleId }
        };
        var currentImages = current.Units.Values.Where(unit => unit.ForceId == forceId).ToDictionary(unit => unit.Id);
        foreach (UnitState unit in withdrawn)
        {
            //A real source destructor already removed this current Army. Its
            //historical image stays above, but no provisional withdrawal or
            //native battle report is fabricated for cause4.
            if (!currentImages.ContainsKey(unit.Id)) continue;
            current.Units.Remove(unit.Id);
            OfficerState officer = current.Officers[unit.OfficerId];
            //Only the gameplay projection withdraws here. The source Army
            //receiver/header remains in PendingImage below; no source Person
            //position writer has run, so retain its actual second location.
            current.Officers[officer.Id] = officer with { Deployed = false };
            reports.Add(new("force_unit_withdrawn", forceId, null, unit.OfficerId, unit.Troops, unit.HexId, unit.HexId)
            { RuleId = WithdrawalRuleId, MissingRuleKeys = missing.ToArray() });
        }
        //Keep the actual source receiver/header and list owners. Detaching its
        //active projection does not claim the native destructor ran or free a
        //slot. Existing PendingImage is the explicit unresolved source owner.
        if (current.ArmyLifecycle is { } lifecycle)
        {
            current = current with { ArmyLifecycle = lifecycle with { Receivers = lifecycle.Receivers.WithEntries(lifecycle.Receivers.Entries.Select(slot =>
                slot.ActiveUnitId is int id && currentImages.TryGetValue(id, out UnitState? image)
                    ? slot with { Phase = ArmyReceiverPhase.Pending, ActiveUnitId = null, PendingImage = image } : slot).ToArray()) } };
        }
        if (current.Diplomacy is { } diplomacy)
        {
            var treaties = new Dictionary<long, DiplomaticTreaty>(diplomacy.Treaties);
            foreach (var pair in diplomacy.Treaties.Where(pair => pair.Value.FirstForceId == forceId || pair.Value.SecondForceId == forceId))
            {
                treaties.Remove(pair.Key);
                reports.Add(new("diplomacy_treaty_ended", pair.Value.FirstForceId, Amount: pair.Value.SecondForceId));
                reports.Add(new("diplomacy_treaty_ended", pair.Value.SecondForceId, Amount: pair.Value.FirstForceId));
            }
            var missions = new Dictionary<int, DiplomaticMission>(diplomacy.Missions);
            foreach (DiplomaticMission mission in diplomacy.Missions.Values.Where(mission => mission.ForceId == forceId || mission.TargetForceId == forceId))
            { missions.Remove(mission.Id); reports.Add(new("diplomacy_mission_cancelled", mission.ForceId, mission.SourceCityId, mission.ExecutorOfficerId, mission.TargetForceId)); }
            var requests = new Dictionary<int, DiplomaticAttackRequest>(diplomacy.AttackRequests);
            foreach (DiplomaticAttackRequest request in diplomacy.AttackRequests.Values.Where(request =>
                request.RequestingForceId == forceId || request.RequestedForceId == forceId || request.TargetForceId == forceId))
            { requests.Remove(request.Id); reports.Add(new("diplomacy_attack_request_ended", request.RequestingForceId, request.TargetCityId, Amount: request.RequestedForceId)); }
            current = current with { Diplomacy = diplomacy with { Treaties = treaties, Missions = missions, AttackRequests = requests } };
        }
        return new(current, reports.ToArray());
    }

    public static void Validate(GameState state)
    {
        if (state.ForceDefeats is null) throw new InvalidDataException("Force defeat records are missing.");
        foreach ((int id, PlayableForceDefeatRecord record) in state.ForceDefeats)
        {
            if (record is null || id != record.ForceId || !IsOrdinaryForce(id) ||
                !state.Forces.TryGetValue(id, out ForceState? force) || force.IsActive || OwnsCity(state, id) ||
                record.Period < 1 || record.Period > state.PeriodNumber ||
                !state.Cities.ContainsKey(record.CapturedCityId) || !state.Forces.ContainsKey(record.CapturingForceId) || record.CapturingForceId == id ||
                string.IsNullOrWhiteSpace(record.RuleId) || record.WithdrawnUnits is null || record.UnresolvedOfficerIds is null ||
                record.MissingRuleKeys is null || record.MissingRuleKeys.Any(string.IsNullOrWhiteSpace) ||
                record.UnresolvedOfficerIds.Distinct().Count() != record.UnresolvedOfficerIds.Length ||
                record.UnresolvedOfficerIds.Any(person => !state.Officers.ContainsKey(person)) ||
                record.ResolvedOfficerIds is { } resolved && (resolved.Distinct().Count() != resolved.Length ||
                    resolved.Any(person => !state.Officers.ContainsKey(person) || record.UnresolvedOfficerIds.Contains(person))) ||
                record.WithdrawnUnits.Any(unit => unit is null) || record.WithdrawnUnits.Select(unit => unit.Id).Distinct().Count() != record.WithdrawnUnits.Length ||
                state.Units.Values.Any(unit => unit.ForceId == id) || state.PreparedDeployments.Values.Any(plan => plan.ForceId == id))
                throw new InvalidDataException("Force defeat record has invalid identity, phase or remaining active military assets.");
            foreach (UnitState unit in record.WithdrawnUnits)
            {
                if (unit.Id < 0 || unit.ForceId != id ||
                    !record.UnresolvedOfficerIds.Contains(unit.OfficerId) && !(record.ResolvedOfficerIds?.Contains(unit.OfficerId) ?? false) ||
                    unit.HexId < 0 || unit.HexId >= (long)state.MapWidth * state.MapHeight || unit.DestinationHexId < 0 || unit.DestinationHexId >= (long)state.MapWidth * state.MapHeight ||
                    unit.Troops < 1 || unit.Supplies < 0 || unit.Morale < 0 || unit.WoundedTroops < 0 ||
                    unit.SourceLocationId is int source && !state.Cities.ContainsKey(source) && !state.Gates.ContainsKey(source) ||
                    unit.FormationId is int formation && !state.Formations.ContainsKey(formation) ||
                    unit.TacticIds is { } tactics && tactics.Any(tactic => !state.Tactics.ContainsKey(tactic)) ||
                    unit.Mission is { } mission && (!Enum.IsDefined(mission.Kind) || !Enum.IsDefined(mission.Arrival) || mission.CargoGold < 0))
                    throw new InvalidDataException("Withdrawn Army has invalid quantities, identities or cargo.");
                //Validate a historical receiver's complete saved modules without
                //promoting it to a live Army or requiring a deployed commander.
                GameState view = state with { Units = new() { [unit.Id] = unit } };
                NativeSubdayStateValidation.Validate(view);
                PlayableMilitaryStateValidation.Validate(view);
                PlayableTacticStateValidation.Validate(view);
            }
        }
    }
}
