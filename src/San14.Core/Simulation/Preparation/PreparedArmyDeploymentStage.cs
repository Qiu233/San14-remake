namespace San14.Core;

/// <summary>Executes departure accounting, target expiry and ordinary Army creation.</summary>
internal static class PreparedArmyDeploymentStage
{
    // Current aggregates are owned by the caller's period transaction. Replaced
    // dictionaries and source owner receipts are returned together; no stage
    // state or mutable world is retained after this method completes.
    internal static PreparedOrderStageResult ExecuteOwned(GameState periodEntry,
        GameState current, PlayableLandRoutes routes, IOrderRules deploymentCancellation,
        IPlayableArmyCreationRules? armyCreationRules)
    {
        GameState basis = current;
        ArmyIdentitySequence identities = current.ArmyIdentities!;
        var cities = current.Cities;
        var gates = current.Gates;
        var districts = current.Districts;
        var officers = current.Officers;
        var units = current.Units;
        var structures = current.Structures!;
        var reports = new List<PeriodReportEntry>();
        ulong randomState = current.RandomState;
        bool IsHostile(int actor, int? target) => PlayableDiplomacy.IsHostile(basis, actor, target);
        GameState CurrentWorld() => basis with
        {
            Cities = cities, Gates = gates, Districts = districts, Officers = officers,
            Units = units, RandomState = randomState, ArmyIdentities = identities
        };
        //Resolve disappearance against the current pre-allocation roster.
        //New armies have distinct identities; lost goals still need their ordinary cancellation report.
        int[] lostPreparedUnitTargets = periodEntry.PreparedDeployments.Values
            .Where(plan => PlayableMilitaryTargetLifecycle.PreparedUnitTargetIsUnavailable(plan, units, IsHostile))
            .Select(plan => plan.Id).ToArray();
        PlayableMilitaryTargetLifecycle.EndUnavailableTargets(units, cities, gates,
            officers, basis.Territory, routes, periodEntry.MapWidth, reports,
            IsHostile, structures);
        foreach (PreparedDeployment plan in periodEntry.PreparedDeployments.Values.OrderBy(plan => plan.Id))
        {
            if (PlayableOfficerAvailability.IsSourceBusy(basis, officers[plan.OfficerId]))
            {
                CommandResult cancelled = deploymentCancellation.Apply(CurrentWorld(), periodEntry.Forces[plan.ForceId],
                    new CancelPreparedDeploymentOrder(plan.ForceId, plan.Id), new("source-busy-cancellation", plan.DistrictId));
                if (!cancelled.Accepted)
                    throw new InvalidDataException($"Unavailable officer deployment cancellation failed: {cancelled.Error}");
                basis = cancelled.State!;
                cities = basis.Cities; gates = basis.Gates; districts = basis.Districts;
                reports.Add(new PeriodReportEntry("deployment_cancelled", plan.ForceId,
                    cities.ContainsKey(plan.SourceLocationId) ? plan.SourceLocationId : null,
                    plan.OfficerId, plan.TroopsDebited ? plan.Troops : 0)
                { LocationId = plan.SourceLocationId });
                continue;
            }
            int nextUnitId = identities.Candidate;
            int sourceHex;
            int morale;
            if (cities.TryGetValue(plan.SourceLocationId, out CityState? city))
            {
                sourceHex = city.HexId ?? throw new InvalidDataException("Deployment source has no map cell.");
                morale = city.Morale ?? 100;
                if (!plan.TroopsDebited) city = city with { Troops = checked(city.Troops - plan.Troops) };
                cities[city.Id] = city;
            }
            else
            {
                GateState gate = gates[plan.SourceLocationId];
                sourceHex = gate.HexId ?? throw new InvalidDataException("Deployment source has no map cell.");
                morale = 100;
                if (!plan.TroopsDebited) gates[gate.Id] = gate with { Troops = checked(gate.Troops - plan.Troops) };
            }
            if (!plan.CommandBookDebited)
            {
                DistrictState district = districts[plan.DistrictId];
                districts[district.Id] = district with
                {
                    OrdersRemaining = checked(district.OrdersRemaining!.Value - 1)
                };
            }
            DeploymentAction action = plan.Actions.Last();
            bool occupyArea = action.ActionKey == "occupy" &&
                              action.Target.Kind == DeploymentTargetKind.Area;
            bool buildStructure = action.ActionKey == "build" &&
                                  action.Target.Kind == DeploymentTargetKind.Hex &&
                                  PlayableConstructionRules.IsSupportedKind(action.StructureKindId);
            int routeOrigin = plan.WaypointHexIds.Length > 0 ? plan.WaypointHexIds[^1] : sourceHex;
            int destination = PlayableWaypointMarch.ResolveDestination(action, plan.ForceId,
                routeOrigin, cities, gates, units, basis.Territory,
                structures, routes, IsHostile, validate: false) ?? sourceHex;
            UnitState deployed = new UnitState(nextUnitId, plan.ForceId, plan.OfficerId,
                sourceHex, destination, plan.Troops, 0, new(morale, null))
            {
                SourceLocationId = plan.SourceLocationId, Formations = new(plan.FormationId, null, null),
                TacticRuntime = UnitTacticRuntime.Empty(),
                WoundedTroops = 0, // Explicitly newly formed provisional Army; legacy/source units remain nullable.
                TacticIds = plan.TacticIds.ToArray(), Behavior = plan.Behavior,
                Mission = occupyArea
                    ? new UnitMission(UnitMissionKind.OccupyArea, TargetAreaId: action.Target.Id)
                    : buildStructure
                    ? new UnitMission(UnitMissionKind.BuildStructure,
                        StructureKindId: action.StructureKindId)
                    : new UnitMission(UnitMissionKind.Combat)
            };
            deployed = PlayableWaypointMarch.AttachDeployment(deployed, plan);
            if (armyCreationRules is not null)
            {
                GameState creationState = CurrentWorld();
                GameState detachedCreation = GameStateSnapshot.Detach(creationState with
                { Units = new(units) { [deployed.Id] = deployed } });
                UnitState candidate = detachedCreation.Units[deployed.Id];
                detachedCreation.Units.Remove(deployed.Id);
                PreparedDeployment detachedPlan = detachedCreation.PreparedDeployments[plan.Id];
                PlayableArmyCreationResolution created = armyCreationRules.Create(detachedCreation, detachedPlan, candidate)
                    ?? throw new InvalidDataException("Current ordinary Army construction inputs are unresolved.");
                var published = PlayableArmyCreation.PublishCreated(creationState, deployed, created);
                basis = published;
                cities = published.Cities; gates = published.Gates; districts = published.Districts;
                officers = published.Officers; units = published.Units;
                randomState = published.RandomState; identities = published.ArmyIdentities!;
                deployed = units[nextUnitId];
            }
            else
            {
                units.Add(nextUnitId, deployed);
                identities = identities.Advance();
                officers[plan.OfficerId] = officers[plan.OfficerId] with { Deployed = true };
            }
            reports.Add(new PeriodReportEntry("deploy", plan.ForceId,
                cities.ContainsKey(plan.SourceLocationId) ? plan.SourceLocationId : null,
                plan.OfficerId, plan.Troops, sourceHex, destination)
            { LocationId = plan.SourceLocationId });
            if (lostPreparedUnitTargets.Contains(plan.Id))
                units[deployed.Id] = PlayableMilitaryTargetLifecycle.CancelPreparedUnitTarget(deployed,
                    cities, gates, officers, routes, periodEntry.MapWidth, reports);
        }

        return new(CurrentWorld(), reports.ToArray());
    }
}
