namespace San14.Core;

/// <summary>Deterministic, deliberately basic AI for the provisional playable ruleset.</summary>
public static class PlayableAiPlanner
{
    public const string DistrictAutonomyProfileId = "san14:provisional-district-autonomy-v1";

    /// <summary>Legacy whole-force exclusion and primary-district planning.</summary>
    public static GameState IssueOrders(GameState state, PlayableRuleSet rules,
        IReadOnlySet<int> humanForceIds) => IssueOrders(state, rules, humanForceIds, false);

    /// <summary>Basic campaign delegation: humans keep direct control of their
    /// first district; known later districts use the same provisional planner.
    /// This does not infer original delegation settings or ruler personality.</summary>
    public static GameState IssueCampaignOrders(GameState state, PlayableRuleSet rules,
        IReadOnlySet<int> humanForceIds) => IssueOrders(state, rules, humanForceIds, true);

    private static GameState IssueOrders(GameState state, PlayableRuleSet rules,
        IReadOnlySet<int> humanForceIds, bool districtAutonomy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(humanForceIds);
        if (state.NativePlayableTerminal is not null) return state;
        if (state.RulesetId != PlayableSkeleton.RulesetId || state.Phase != TurnPhase.Strategy)
            throw new InvalidOperationException("AI orders require a playable strategy phase.");
        StateValidator.Validate(state);
        rules.Routes.ValidateWorld(state);
        GameState current = state;
        foreach (ForceState force in state.Forces.Values.Where(force => force.IsActive)
                     .OrderBy(force => force.Id))
        {
            bool human = humanForceIds.Contains(force.Id);
            if (human && !districtAutonomy) continue;
            foreach (DistrictState district in state.Districts.Values.Where(district =>
                         district.ForceId == force.Id && (districtAutonomy ? district.Number >= 1 : district.Number == 1))
                         .OrderBy(district => district.Number).ThenBy(district => district.Id))
            {
                if (human && district.Number == 1) continue;
                //Unknown or absent district leadership is not repaired by an AI
                //pass. In the temporary profile it leaves this delegation idle.
                if (districtAutonomy && district.Number > 1 &&
                    (district.LeaderId is not int leaderId ||
                     !current.Officers.TryGetValue(leaderId, out OfficerState? leader) ||
                     leader.ForceId != force.Id || leader.DistrictId != district.Id ||
                     leader.Presence != OfficerPresence.Present || PlayableOfficerAvailability.IsSourceBusy(current, leader))) continue;
                string actor = districtAutonomy ? $"ai:{force.Id}:district:{district.Id}" : $"ai:{force.Id}";
                var context = new CommandContext(actor, districtAutonomy ? district.Id : null);
                var authority = new SingleForceAuthority(force.Id, actor, districtAutonomy ? [district.Id] : []);
                current = IssueDistrictOrders(current, rules, force, district, context, authority, districtAutonomy);
            }
        }
        return current;
    }

    private static GameState IssueDistrictOrders(GameState current, PlayableRuleSet rules,
        ForceState force, DistrictState district, CommandContext context, IOrderAuthority authority, bool districtAutonomy)
    {
        CityState? city = current.Cities.Values.Where(city => city.ForceId == force.Id &&
                city.DistrictId == district.Id && city.HexId is not null)
            .OrderByDescending(city => city.Troops).ThenBy(city => city.Id).FirstOrDefault();
        if (!districtAutonomy && city is null) return current;
        bool DistrictUnit(UnitState unit) => unit.ForceId == force.Id &&
            (!districtAutonomy || current.Officers[unit.OfficerId].DistrictId == district.Id);

        UnitState? arrived = current.Units.Values.Where(unit => DistrictUnit(unit) &&
                unit.HexId == unit.DestinationHexId && (!districtAutonomy ||
                    unit.Mission?.Kind == UnitMissionKind.Combat &&
                    (unit.MilitaryPlan is null || unit.MilitaryPlan is { Completed: true, IncidentalEnemyId: null })))
            .OrderBy(unit => unit.Id).FirstOrDefault();
        if (arrived is not null)
        {
            CityState? target = SelectEnemyCity(current, force.Id, arrived.HexId, rules.Routes);
            GameOrder followup = target?.HexId is int targetHex && targetHex != arrived.HexId
                ? new RetargetUnitOrder(force.Id, arrived.Id, targetHex)
                : new DisbandUnitOrder(force.Id, arrived.Id,
                    current.Cities.Values.FirstOrDefault(baseCity =>
                        baseCity.ForceId == force.Id && baseCity.HexId == arrived.HexId)?.Id ?? -1);
            current = ApplyIfAccepted(current, followup, rules, context, authority);
        }

        //An Army can still receive its next order after its district's last
        //city was lost. City administration and new departure need a base.
        if (city is null) return current;

        OfficerState[] available = current.Officers.Values.Where(officer => officer.ForceId == force.Id &&
                (!districtAutonomy || officer.DistrictId == district.Id) &&
                officer.LocationId == city.Id && officer.Presence == OfficerPresence.Present &&
                !officer.Deployed && !PlayableOfficerAvailability.IsSourceBusy(current, officer) &&
                current.PreparedOfficerMoves?.ContainsKey(officer.Id) != true &&
                current.PreparedTalentOrders?.ContainsKey(officer.Id) != true &&
                !PlayableDiplomacy.IsOfficerOnMission(current, officer.Id) &&
                !current.Cities.Values.Any(active => active.Duties?.RecruiterId == officer.Id ||
                    active.Duties?.TrainerId == officer.Id) &&
                !current.Areas.Values.Any(area => area.Administration?.OverseerId == officer.Id) &&
                !current.PreparedDeployments.Values.Any(plan =>
                    plan.OfficerId == officer.Id))
            .OrderBy(officer => officer.Id).ToArray();
        bool alreadyHasUnit = current.Units.Values.Any(DistrictUnit) ||
            current.PreparedDeployments.Values.Any(plan => plan.ForceId == force.Id &&
                (!districtAutonomy || plan.DistrictId == district.Id));
        OfficerState? commander = available.FirstOrDefault(officer => officer.BaseFormationIds is { Length: > 0 });
        if (!alreadyHasUnit && commander is not null && city.Troops >= 5000 &&
            city.HexId is int sourceHex && SelectEnemyCity(current, force.Id, sourceHex, rules.Routes) is
                { HexId: int destinationHex })
        {
            int formation = commander.BaseFormationIds!.First();
            var unit = new DeploymentUnitRequest(commander.Id, formation,
                Math.Min(5000, city.Troops / 4), [],
                [new DeploymentAction("move", new DeploymentTarget(DeploymentTargetKind.Hex,
                    destinationHex))])
            { Behavior = new DeploymentBehavior(true, true, false, DeploymentAfterAction.Stay) };
            current = ApplyIfAccepted(current,
                new PrepareDeploymentOrder(force.Id, city.Id, [unit]), rules, context, authority);
        }

        AreaState? vacant = city.AreaIds.Select(areaId => current.Areas[areaId])
            .FirstOrDefault(area => area.ForceId == force.Id &&
                area.Administration?.OverseerId is null);
        OfficerState? administrator = available.FirstOrDefault(officer =>
            !current.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id));
        if (vacant is not null && administrator is not null)
        {
            var focus = (AreaDevelopmentFocus)(current.PeriodNumber % 3);
            current = ApplyIfAccepted(current,
                new SetAreaAdministrationOrder(force.Id, city.Id,
                    [new AreaAdministrationChange(vacant.Id, administrator.Id, focus)]),
                rules, context, authority);
        }
        if (administrator is not null)
            foreach (CityDutyKind kind in new[] { CityDutyKind.Recruitment, CityDutyKind.Training })
            {
                CityDutyState? duties = current.Cities[city.Id].Duties;
                if (duties is null ||
                    (kind == CityDutyKind.Recruitment ? duties.RecruiterId : duties.TrainerId) is not null)
                    continue;
                current = ApplyIfAccepted(current,
                    new SetCityDutyOrder(force.Id, city.Id, kind, administrator.Id),
                    rules, context, authority);
            }
        return current;
    }

    private static GameState ApplyIfAccepted(GameState state, GameOrder order, PlayableRuleSet rules,
        CommandContext context, IOrderAuthority authority)
    {
        CommandResult result = GameEngine.Apply(state, order, rules.OrderRules, context, authority);
        return result.State ?? state;
    }

    private static CityState? SelectEnemyCity(GameState state, int forceId, int sourceHex,
        PlayableLandRoutes routes)
    {
        foreach (DiplomaticAttackRequest request in PlayableDiplomacy.GetAttackRequests(state, forceId))
            if (state.Cities.TryGetValue(request.TargetCityId, out CityState? requestedCity) &&
                requestedCity.HexId is int requestedHex && requestedCity.ForceId == request.TargetForceId &&
                PlayableDiplomacy.IsHostile(state, forceId, requestedCity.ForceId) &&
                (sourceHex == requestedHex || routes.Route(sourceHex, requestedHex).Length > 0))
                return requestedCity;
        int width = state.MapWidth;
        int sourceX = sourceHex % width, sourceY = sourceHex / width;
        return state.Cities.Values.Where(city => PlayableDiplomacy.IsHostile(state, forceId, city.ForceId) &&
                city.HexId is not null)
            .OrderBy(city => Math.Abs(city.HexId!.Value % width - sourceX) +
                Math.Abs(city.HexId!.Value / width - sourceY))
            .ThenBy(city => city.Id).FirstOrDefault();
    }
}
