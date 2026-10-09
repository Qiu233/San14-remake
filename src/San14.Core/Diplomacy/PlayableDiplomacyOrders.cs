namespace San14.Core;

/// <summary>Shared diplomacy planning for any actor authorized by GameEngine.</summary>
public sealed class PlayableDiplomacyOrders(IOrderRules otherCommands,
    IDiplomacyPolicy policy) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order,
        CommandContext context)
    {
        if (order is not PrepareDiplomaticOrder command)
            return otherCommands.Apply(state, force, order, context);
        if (!Enum.IsDefined(command.Kind))
            return CommandResult.Reject("Unknown diplomatic command.", "diplomacy.kind");
        if (state.Diplomacy is not { } diplomacy)
            return CommandResult.RuleUnavailable("rules.diplomacy_state", "Diplomacy state is unresolved.");
        if (!state.Forces.TryGetValue(command.TargetForceId, out ForceState? target) ||
            !target.IsActive || target.Id == force.Id)
            return CommandResult.Reject("Diplomacy requires another active force.", "diplomacy.target");
        if (!state.Cities.TryGetValue(command.SourceCityId, out CityState? city) ||
            city.ForceId != force.Id)
            return CommandResult.Reject("Diplomatic command requires an owned city.", "diplomacy.city");
        if (city.DistrictId is not int districtId ||
            !state.Districts.TryGetValue(districtId, out DistrictState? district))
            return CommandResult.RuleUnavailable("rules.city_district", "Diplomatic district is unresolved.");
        if (context.DelegatedDistrictId is int delegated ? delegated != district.Id : district.Number != 1)
            return CommandResult.Reject("Diplomatic city is outside the authorized district.", "diplomacy.district_scope");
        if (!state.Officers.TryGetValue(command.ExecutorOfficerId, out OfficerState? executor) ||
            executor.ForceId != force.Id || executor.LocationId != city.Id ||
            executor.Presence != OfficerPresence.Present || executor.Deployed || PlayableOfficerAvailability.IsSourceBusy(state, executor) ||
            PlayableDiplomacy.IsOfficerOnMission(state, executor.Id) ||
            state.PreparedTalentOrders?.ContainsKey(executor.Id) == true ||
            state.PreparedOfficerMoves?.ContainsKey(executor.Id) == true ||
            state.PreparedDeployments.Values.Any(plan => plan.OfficerId == executor.Id) ||
            state.Cities.Values.Any(active => active.Duties?.RecruiterId == executor.Id ||
                active.Duties?.TrainerId == executor.Id) ||
            state.Areas.Values.Any(area => area.Administration?.OverseerId == executor.Id))
            return CommandResult.Reject("Diplomatic executor is unavailable.", "diplomacy.executor");
        if (!PlayableDiplomacy.TryGetRelation(state, force.Id, target.Id, out DiplomaticRelation level))
            return CommandResult.RuleUnavailable("rules.diplomatic_relation", "Current diplomatic relationship is unresolved.");
        long pair = PlayableDiplomacy.PairKey(force.Id, target.Id);
        if (diplomacy.Missions.Values.Any(task =>
                PlayableDiplomacy.PairKey(task.ForceId, task.TargetForceId) == pair))
            return CommandResult.Reject("A mission for these two forces is already pending.", "diplomacy.pending_pair");
        bool allied = PlayableDiplomacy.AreAllied(state, force.Id, target.Id);
        if (command.Kind == DiplomaticMissionKind.Alliance && level < DiplomaticRelation.Neutral)
            return CommandResult.Reject("Alliance requires a neutral or better relationship.", "diplomacy.relation");
        if (command.Kind == DiplomaticMissionKind.CancelAlliance && !allied)
            return CommandResult.Reject("There is no current alliance to cancel.", "diplomacy.no_alliance");
        if (command.Kind == DiplomaticMissionKind.RequestAttack &&
            (!allied || level < DiplomaticRelation.Friendly))
            return CommandResult.Reject("Attack requests require a friendly or better ally.", "diplomacy.attack_relation");
        int? targetCityForce = null;
        if (command.Kind == DiplomaticMissionKind.RequestAttack)
        {
            if (command.TargetCityId is not int targetCityId ||
                !state.Cities.TryGetValue(targetCityId, out CityState? targetCity) ||
                targetCity.ForceId is not int victim || victim == force.Id || victim == target.Id ||
                !state.Forces.TryGetValue(victim, out ForceState? victimForce) || !victimForce.IsActive ||
                !PlayableDiplomacy.IsHostile(state, target.Id, victim))
                return CommandResult.Reject("Attack request needs a city belonging to an enemy of the ally.", "diplomacy.attack_target");
            if (diplomacy.AttackRequests.Values.Any(request => request.RequestedForceId == target.Id &&
                    request.TargetCityId == targetCity.Id && request.ExpiresPeriod > state.PeriodNumber))
                return CommandResult.Reject("The ally already has an active request for that city.", "diplomacy.attack_pending");
            targetCityForce = victim;
        }
        else if (command.TargetCityId is not null)
            return CommandResult.Reject("This diplomatic command has no target city.", "diplomacy.unexpected_target_city");
        if (string.IsNullOrWhiteSpace(policy.Id))
            throw new InvalidDataException("Diplomacy policy identifier is missing.");
        if (policy.Quote(state, command) is not DiplomacyQuote quote)
            return CommandResult.RuleUnavailable("rules.diplomacy_quote", "Diplomacy cost or duration is unavailable.");
        if (quote.CommandBooks < 0 || quote.Gold < 0 || quote.TravelPeriods < 1 ||
            quote.TreatyPeriods < 0 || quote.RequestPeriods < 0 ||
            (command.Kind == DiplomaticMissionKind.Alliance && quote.TreatyPeriods < 1) ||
            (command.Kind == DiplomaticMissionKind.RequestAttack && quote.RequestPeriods < 1))
            throw new InvalidDataException("Diplomacy policy returned an invalid quote.");
        if (district.OrdersRemaining is not int remaining)
            return CommandResult.RuleUnavailable("rules.command_budget", "Diplomatic district command books are unresolved.");
        if (remaining - CommandBookAvailability.Reserved(state, district.Id) < quote.CommandBooks)
            return CommandResult.Reject("District lacks uncommitted command books.", "diplomacy.insufficient_orders");
        if (city.Gold < quote.Gold)
            return CommandResult.Reject("Source city lacks diplomatic funds.", "diplomacy.insufficient_gold");
        long arrival = (long)state.PeriodNumber + quote.TravelPeriods;
        if (arrival + Math.Max(quote.TreatyPeriods, quote.RequestPeriods) > int.MaxValue ||
            diplomacy.NextMissionId == int.MaxValue)
            return CommandResult.Reject("Diplomatic schedule or mission identifier is exhausted.", "diplomacy.capacity");
        int id = diplomacy.NextMissionId;
        var missions = new Dictionary<int, DiplomaticMission>(diplomacy.Missions)
        {
            [id] = new(id, force.Id, city.Id, district.Id, executor.Id, target.Id,
                command.Kind, state.PeriodNumber, (int)arrival, quote.TreatyPeriods,
                quote.RequestPeriods, command.TargetCityId, targetCityForce, quote.Gold,
                quote.CommandBooks, policy.Id)
        };
        return CommandResult.Accept(state with
        {
            Diplomacy = diplomacy with { Missions = missions, NextMissionId = checked(id + 1) },
            Districts = new Dictionary<int, DistrictState>(state.Districts)
            {
                [district.Id] = district with { OrdersRemaining = remaining - quote.CommandBooks }
            },
            Cities = new Dictionary<int, CityState>(state.Cities)
            {
                [city.Id] = city with { Gold = city.Gold - quote.Gold }
            }
        });
    }
}
