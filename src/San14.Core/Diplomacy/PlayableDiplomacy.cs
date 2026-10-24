namespace San14.Core;

/// <summary>Mutable diplomacy is independent of source records and of command authority.</summary>
public static class PlayableDiplomacy
{
    public const string ProvisionalOrigin = "san14:provisional-diplomacy-v1";

    public static long PairKey(int first, int second)
    {
        if (first < 0 || second < 0 || first == second)
            throw new ArgumentOutOfRangeException(nameof(first), "Diplomatic pairs require distinct nonnegative force IDs.");
        int low = Math.Min(first, second), high = Math.Max(first, second);
        return ((long)low << 32) | (uint)high;
    }

    /// <summary>Explicit prototype initialization. No original diplomatic matrix is interpreted.</summary>
    public static DiplomacyState CreateProvisional(GameState state)
    {
        var forces = state.Forces.Values.Where(force => force.IsActive).OrderBy(force => force.Id).ToArray();
        var relations = new Dictionary<long, DiplomaticRelationship>();
        for (int a = 0; a < forces.Length; a++)
            for (int b = a + 1; b < forces.Length; b++)
                relations.Add(PairKey(forces[a].Id, forces[b].Id),
                    new(forces[a].Id, forces[b].Id, DiplomaticRelation.Neutral, ProvisionalOrigin));
        return new(ProvisionalOrigin, relations, [], [], [], 1);
    }

    public static bool TryGetRelation(GameState state, int first, int second,
        out DiplomaticRelation level)
    {
        level = default;
        if (first < 0 || second < 0 || first == second || state.Diplomacy is not { } diplomacy ||
            !diplomacy.Relationships.TryGetValue(PairKey(first, second), out DiplomaticRelationship? relation))
            return false;
        level = relation.Level;
        return true;
    }

    public static bool AreAllied(GameState state, int first, int second) =>
        first != second && first >= 0 && second >= 0 &&
        state.Diplomacy?.Treaties.TryGetValue(PairKey(first, second), out DiplomaticTreaty? treaty) == true &&
        treaty.Kind == DiplomaticTreatyKind.Alliance && treaty.ExpiresPeriod > state.PeriodNumber;

    /// <summary>Prototype combat eligibility. Unknown diplomacy never asserts a treaty.</summary>
    public static bool IsHostile(GameState state, int actingForce, int? targetForce)
    {
        if (targetForce is null) return true;
        if (actingForce == targetForce) return false;
        if (actingForce < 0 || targetForce < 0) return false;
        if (state.Diplomacy is not { } diplomacy ||
            !diplomacy.Treaties.TryGetValue(PairKey(actingForce, targetForce.Value), out DiplomaticTreaty? treaty))
            return true;
        return treaty.ExpiresPeriod <= state.PeriodNumber;
    }

    public static bool IsOfficerOnMission(GameState state, int officerId) =>
        state.Diplomacy?.Missions.Values.Any(mission => mission.ExecutorOfficerId == officerId) == true ||
        state.PersonnelCommands?.Diplomacies?.Values.Any(task => task.IsAssociated && !task.IsRetiring && task.MemberIds.Contains(officerId)) == true;

    public static IReadOnlyList<DiplomaticAttackRequest> GetAttackRequests(GameState state, int forceId) =>
        state.Diplomacy?.AttackRequests.Values
            .Where(request => request.RequestedForceId == forceId && request.ExpiresPeriod > state.PeriodNumber &&
                state.Forces.TryGetValue(request.RequestingForceId, out ForceState? requesting) && requesting.IsActive &&
                state.Forces.TryGetValue(forceId, out ForceState? requested) && requested.IsActive &&
                state.Forces.TryGetValue(request.TargetForceId, out ForceState? victim) && victim.IsActive &&
                state.Cities.TryGetValue(request.TargetCityId, out CityState? target) && target.ForceId == request.TargetForceId &&
                AreAllied(state, request.RequestingForceId, forceId) && IsHostile(state, forceId, request.TargetForceId))
            .OrderBy(request => request.Id).ToArray() ?? [];

    /// <summary>
    /// Called once at the start of ten-day resolution with the upcoming absolute period.
    /// Leaves date and phase to the engine and appends typed reports to the supplied state.
    /// A future mission may remain pending through reporting and saving.
    /// </summary>
    public static GameState Advance(GameState state, int nextPeriodNumber, IDiplomacyPolicy policy)
    {
        if (policy is IPlayableDiplomacyCommandRules source)
            return source.AdvanceDiplomacyPeriod(state, nextPeriodNumber);
        if ((long)state.PeriodNumber + 1 != nextPeriodNumber)
            throw new ArgumentOutOfRangeException(nameof(nextPeriodNumber), "Diplomacy must advance exactly one period.");
        if (state.Diplomacy is not { } current) return state;
        Validate(state);
        var relations = new Dictionary<long, DiplomaticRelationship>(current.Relationships);
        var treaties = new Dictionary<long, DiplomaticTreaty>(current.Treaties);
        var missions = new Dictionary<int, DiplomaticMission>(current.Missions);
        var requests = new Dictionary<int, DiplomaticAttackRequest>(current.AttackRequests);
        var reports = state.PeriodReports.ToList();
        ulong random = state.RandomState;
        foreach ((long key, DiplomaticTreaty treaty) in current.Treaties.OrderBy(pair => pair.Key))
        {
            if (treaty.ExpiresPeriod > nextPeriodNumber && Active(treaty.FirstForceId) && Active(treaty.SecondForceId)) continue;
            treaties.Remove(key);
            AddBoth(treaty.ExpiresPeriod <= nextPeriodNumber ? "diplomacy_treaty_expired" : "diplomacy_treaty_ended",
                treaty.FirstForceId, treaty.SecondForceId);
        }
        foreach ((int id, DiplomaticAttackRequest request) in current.AttackRequests.OrderBy(pair => pair.Key))
        {
            if (request.ExpiresPeriod > nextPeriodNumber && Active(request.RequestingForceId) &&
                Active(request.RequestedForceId) && Active(request.TargetForceId) &&
                state.Cities.TryGetValue(request.TargetCityId, out CityState? targetCity) &&
                targetCity.ForceId == request.TargetForceId &&
                Hostile(request.RequestedForceId, request.TargetForceId) &&
                Allied(request.RequestingForceId, request.RequestedForceId)) continue;
            requests.Remove(id);
            reports.Add(new("diplomacy_attack_request_ended", request.RequestingForceId,
                request.TargetCityId, Amount: request.RequestedForceId));
        }
        foreach (DiplomaticMission mission in current.Missions.Values.OrderBy(task => task.Id))
        {
            bool validExecutor = state.Officers.TryGetValue(mission.ExecutorOfficerId, out OfficerState? executor) &&
                executor.ForceId == mission.ForceId && executor.Presence == OfficerPresence.Present && !executor.Deployed &&
                !PlayableOfficerAvailability.IsSourceBusy(state, executor);
            if (mission.ArrivalPeriod > nextPeriodNumber && validExecutor && Active(mission.ForceId) && Active(mission.TargetForceId))
                continue;
            missions.Remove(mission.Id);
            long key = PairKey(mission.ForceId, mission.TargetForceId);
            if (!validExecutor || !Active(mission.ForceId) || !Active(mission.TargetForceId) ||
                !relations.TryGetValue(key, out DiplomaticRelationship? relationship) || !Eligible(mission, relationship.Level))
            {
                AddMission("diplomacy_mission_cancelled", mission);
                continue;
            }
            if (mission.PolicyId != policy.Id)
                throw new InvalidDataException($"Diplomatic mission {mission.Id} requires policy {mission.PolicyId}.");
            GameState evaluating = state with
            {
                RandomState = random,
                Diplomacy = current with { Relationships = relations, Treaties = treaties,
                    Missions = missions, AttackRequests = requests }
            };
            DiplomacyResolution outcome = policy.Resolve(evaluating, mission);
            if (outcome.RelationIncrease < 0 || (mission.Kind != DiplomaticMissionKind.Goodwill && outcome.RelationIncrease != 0))
                throw new InvalidDataException("Diplomacy policy returned an invalid relationship change.");
            random = outcome.RandomState;
            if (!outcome.Accepted)
            {
                AddMission("diplomacy_mission_failed", mission);
                continue;
            }
            switch (mission.Kind)
            {
                case DiplomaticMissionKind.Goodwill:
                    relations[key] = relationship with
                    {
                        Level = (DiplomaticRelation)Math.Min((long)DiplomaticRelation.Close,
                            (long)relationship.Level + outcome.RelationIncrease), Origin = mission.PolicyId
                    };
                    AddMission("diplomacy_goodwill", mission);
                    break;
                case DiplomaticMissionKind.Alliance:
                    treaties[key] = new(Math.Min(mission.ForceId, mission.TargetForceId),
                        Math.Max(mission.ForceId, mission.TargetForceId), DiplomaticTreatyKind.Alliance,
                        nextPeriodNumber, checked(nextPeriodNumber + mission.TreatyPeriods), mission.PolicyId);
                    AddMission("diplomacy_alliance", mission);
                    break;
                case DiplomaticMissionKind.CancelAlliance:
                    treaties.Remove(key);
                    foreach (int id in requests.Values.Where(request =>
                                 PairKey(request.RequestingForceId, request.RequestedForceId) == key)
                                 .Select(request => request.Id).ToArray())
                        requests.Remove(id);
                    AddMission("diplomacy_alliance_cancelled", mission);
                    break;
                case DiplomaticMissionKind.RequestAttack:
                    requests[mission.Id] = new(mission.Id, mission.ForceId, mission.TargetForceId,
                        mission.TargetCityId!.Value, mission.TargetCityForceId!.Value, nextPeriodNumber,
                        checked(nextPeriodNumber + mission.RequestPeriods), mission.PolicyId);
                    AddMission("diplomacy_attack_requested", mission);
                    break;
                default:
                    throw new InvalidDataException("Unknown diplomatic mission kind.");
            }
        }
        return state with
        {
            RandomState = random, PeriodReports = reports.ToArray(),
            Diplomacy = current with { Relationships = relations, Treaties = treaties,
                Missions = missions, AttackRequests = requests }
        };

        bool Active(int id) => state.Forces.TryGetValue(id, out ForceState? force) && force.IsActive;
        bool Allied(int first, int second) => treaties.TryGetValue(PairKey(first, second), out DiplomaticTreaty? treaty) &&
            treaty.Kind == DiplomaticTreatyKind.Alliance && treaty.ExpiresPeriod > nextPeriodNumber;
        bool Hostile(int first, int second) => !treaties.TryGetValue(PairKey(first, second), out DiplomaticTreaty? treaty) ||
            treaty.ExpiresPeriod <= nextPeriodNumber;
        bool Eligible(DiplomaticMission mission, DiplomaticRelation level) => mission.Kind switch
        {
            DiplomaticMissionKind.Goodwill => true,
            DiplomaticMissionKind.Alliance => level >= DiplomaticRelation.Neutral,
            DiplomaticMissionKind.CancelAlliance => Allied(mission.ForceId, mission.TargetForceId),
            DiplomaticMissionKind.RequestAttack => level >= DiplomaticRelation.Friendly &&
                Allied(mission.ForceId, mission.TargetForceId) &&
                mission.TargetCityId is int id && state.Cities.TryGetValue(id, out CityState? city) &&
                city.ForceId == mission.TargetCityForceId &&
                mission.TargetCityForceId is int victim && Active(victim) && Hostile(mission.TargetForceId, victim),
            _ => false
        };
        void AddMission(string kind, DiplomaticMission mission) => reports.Add(new(kind,
            mission.ForceId, mission.TargetCityId ?? mission.SourceCityId,
            mission.ExecutorOfficerId, Amount: mission.TargetForceId));
        void AddBoth(string kind, int first, int second)
        {
            reports.Add(new(kind, first, Amount: second));
            reports.Add(new(kind, second, Amount: first));
        }
    }

    public static void Validate(GameState state)
    {
        if (state.Diplomacy is not { } diplomacy) return;
        if (string.IsNullOrWhiteSpace(diplomacy.Origin) || diplomacy.Relationships is null ||
            diplomacy.Treaties is null || diplomacy.Missions is null || diplomacy.AttackRequests is null ||
            diplomacy.NextMissionId < 1)
            throw new InvalidDataException("Diplomacy state is invalid.");
        foreach ((long key, DiplomaticRelationship relation) in diplomacy.Relationships)
            if (relation is null || !PairMatches(key, relation.FirstForceId, relation.SecondForceId) ||
                !Enum.IsDefined(relation.Level) || string.IsNullOrWhiteSpace(relation.Origin))
                throw new InvalidDataException("Diplomatic relationship is invalid.");
        foreach ((long key, DiplomaticTreaty treaty) in diplomacy.Treaties)
            if (treaty is null || !PairMatches(key, treaty.FirstForceId, treaty.SecondForceId) ||
                !diplomacy.Relationships.ContainsKey(key) || !Enum.IsDefined(treaty.Kind) ||
                treaty.StartedPeriod < 0 || treaty.StartedPeriod > state.PeriodNumber ||
                treaty.ExpiresPeriod <= state.PeriodNumber || treaty.ExpiresPeriod <= treaty.StartedPeriod ||
                string.IsNullOrWhiteSpace(treaty.Origin))
                throw new InvalidDataException("Diplomatic treaty is invalid or expired.");
        var executors = new HashSet<int>();
        var pendingPairs = new HashSet<long>();
        foreach ((int id, DiplomaticMission mission) in diplomacy.Missions)
        {
            if (mission is null || id != mission.Id || id < 1 || id >= diplomacy.NextMissionId ||
                !PairExists(mission.ForceId, mission.TargetForceId) || !state.Cities.ContainsKey(mission.SourceCityId) ||
                !state.Districts.ContainsKey(mission.DistrictId) ||
                !state.Officers.ContainsKey(mission.ExecutorOfficerId) || !executors.Add(mission.ExecutorOfficerId) ||
                !pendingPairs.Add(PairKey(mission.ForceId, mission.TargetForceId)) ||
                !Enum.IsDefined(mission.Kind) || mission.SubmittedPeriod < 0 ||
                mission.SubmittedPeriod > state.PeriodNumber || mission.ArrivalPeriod <= state.PeriodNumber ||
                mission.ArrivalPeriod <= mission.SubmittedPeriod || mission.PaidGold < 0 || mission.PaidCommandBooks < 0 ||
                mission.TreatyPeriods < 0 || mission.RequestPeriods < 0 || string.IsNullOrWhiteSpace(mission.PolicyId) ||
                (long)mission.ArrivalPeriod + Math.Max(mission.TreatyPeriods, mission.RequestPeriods) > int.MaxValue ||
                (mission.Kind == DiplomaticMissionKind.Alliance && mission.TreatyPeriods < 1) ||
                (mission.Kind == DiplomaticMissionKind.RequestAttack &&
                    (mission.RequestPeriods < 1 || mission.TargetCityId is not int city || !state.Cities.ContainsKey(city) ||
                     mission.TargetCityForceId is not int victim || !state.Forces.ContainsKey(victim) ||
                     victim == mission.ForceId || victim == mission.TargetForceId)) ||
                (mission.Kind != DiplomaticMissionKind.RequestAttack &&
                    (mission.TargetCityId is not null || mission.TargetCityForceId is not null)))
                throw new InvalidDataException("Diplomatic mission is invalid.");
            int officer = mission.ExecutorOfficerId;
            if (state.Officers[officer].Deployed ||
                state.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer) ||
                state.PreparedTalentOrders?.ContainsKey(officer) == true ||
                state.PreparedOfficerMoves?.ContainsKey(officer) == true ||
                state.Cities.Values.Any(city => city.Duties?.RecruiterId == officer || city.Duties?.TrainerId == officer) ||
                state.Areas.Values.Any(area => area.Administration?.OverseerId == officer))
                throw new InvalidDataException("Diplomatic executor has another active task.");
        }
        foreach ((int id, DiplomaticAttackRequest request) in diplomacy.AttackRequests)
            if (request is null || id != request.Id || id < 1 || id >= diplomacy.NextMissionId ||
                diplomacy.Missions.ContainsKey(id) || !PairExists(request.RequestingForceId, request.RequestedForceId) ||
                !state.Forces.ContainsKey(request.TargetForceId) || request.TargetForceId == request.RequestingForceId ||
                request.TargetForceId == request.RequestedForceId || !state.Cities.ContainsKey(request.TargetCityId) ||
                request.AcceptedPeriod < 0 || request.AcceptedPeriod > state.PeriodNumber ||
                request.ExpiresPeriod <= state.PeriodNumber || request.ExpiresPeriod <= request.AcceptedPeriod ||
                string.IsNullOrWhiteSpace(request.Origin))
                throw new InvalidDataException("Diplomatic attack request is invalid or expired.");

        bool PairMatches(long key, int first, int second) => first < second && PairExists(first, second) &&
            key == PairKey(first, second);
        bool PairExists(int first, int second) => first >= 0 && second >= 0 && first != second &&
            state.Forces.ContainsKey(first) && state.Forces.ContainsKey(second);
    }
}
