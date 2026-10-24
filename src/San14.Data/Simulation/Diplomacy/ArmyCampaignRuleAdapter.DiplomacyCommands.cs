using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public IDiplomacyPolicy CreateDiplomacyRules() => new DiplomacyRules(this);

    public DiplomacyCommandAvailability QueryDiplomacyCommand(GameState state, int forceId, int cityId,
        DiplomaticMissionKind kind, int? targetForceId = null, int allianceMonths = 12,
        int? targetCityId = null, int giftGold = 0, int giftSupplies = 0)
    {
        var actors = new List<int>();
        var targets = new List<int>();
        int? books = null;
        try
        {
            if (state.Phase != TurnPhase.Strategy || !state.Cities.TryGetValue(cityId, out var city) || city.ForceId != forceId)
                return new(actors, targets, false, null, books);
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            PersonReturnCommands.BindCurrent(state, current);
            var qualification = new DiplomacyCommandSource(state, current);
            foreach (var force in state.Forces.Values)
            {
                if (force.Id == forceId || !force.IsActive) continue;
                try { qualification.RequireInteraction(forceId, force.Id); targets.Add(force.Id); }
                catch (ArgumentException) { }
            }
            foreach (var officer in state.Officers.Values)
            {
                if (officer.ForceId != forceId || officer.LocationId != cityId || officer.Deployed ||
                    officer.Presence != OfficerPresence.Present || PlayableOfficerAvailability.IsSourceBusy(state, officer) ||
                    PlayableDiplomacy.IsOfficerOnMission(state, officer.Id) ||
                    state.PreparedTalentOrders?.ContainsKey(officer.Id) == true || state.PreparedOfficerMoves?.ContainsKey(officer.Id) == true ||
                    state.PreparedDeployments.Values.Any(plan => plan.OfficerId == officer.Id)) continue;
                int person = current.ResolveOfficerSource(officer.Id) ?? throw DiplomacyMissing("query_actor_physical");
                if (!TalentKnown(current.ReadPersonNativeValidity(person), "diplomacy_query_actor_validity") ||
                    TalentKnown(current.ReadPersonStatus(person), "diplomacy_query_actor_status") is < 1 or > 4 ||
                    PersonCurrentBusySource.Read(current, person, DiplomacyMissing)) continue;
                actors.Add(officer.Id);
            }
            if (targetForceId is not int recipient || !targets.Contains(recipient) || actors.Count == 0)
                return new(actors, targets, false, null, books);
            var order = new PrepareDiplomaticOrder(forceId, cityId, actors[0], recipient, kind, allianceMonths, targetCityId)
                { GiftGold = giftGold, GiftSupplies = giftSupplies };
            var packet = ReadDiplomacySubmissionCurrent(state, current, order, state.Forces[forceId], new("diplomacy-query"));
            books = packet.Books;
            return new(actors, targets, true, null, books);
        }
        catch (ArgumentException) { return new(actors, targets, false, null, books); }
        catch (InvalidDataException error) { return new(actors, targets, null, error.Message, books); }
    }

    public IReadOnlyList<DiplomacyNegotiationRequest> QueryDiplomacyNegotiations(GameState state, int forceId)
    {
        var tasks = (state.PersonnelCommands?.Diplomacies?.Values.AsEnumerable() ?? [])
            .Where(task => task.AwaitingNegotiation && task.NativeKind != 6 && !task.IsRetiring &&
                (task.ForceId == forceId || task.TargetForceId == forceId)).OrderBy(task => task.DispatchSlot).ToArray();
        if (tasks.Length == 0) return [];
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        return tasks.Select(task => new DiplomacyNegotiationRequest(task.Id, task.ForceId, task.TargetForceId,
            task.MemberIds.FirstOrDefault(), task.Kind, task.InitialOffer, task.AllianceMonths,
            task.TargetCityId, task.NegotiationDate ?? throw DiplomacyMissing("negotiation_reached_date"))
            { Negotiation = task.Negotiation ?? ReadInitialDiplomacyNegotiationCurrent(state, current, task) }).ToArray();
    }

    private CommandResult SubmitDiplomacyCurrent(GameState state, ForceState force,
        PrepareDiplomaticOrder order, CommandContext context)
    {
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            var packet = ReadDiplomacySubmissionCurrent(state, current, order, force, context);
            var date = state.NativeMapSeason?.ThirdDate ?? throw DiplomacyMissing("creation_date");
            var started = PersonReturnCommands.StartDiplomacyTask(state, current, order, packet, date,
                (working, home) =>
                {
                    ApplyPersonFreedomGovernorCurrent(working, current, home);
                    return OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
                });
            return CommandResult.Accept(PersonReturnCommands.PublishCurrent(started, current));
        }
        catch (ArgumentException error) { return CommandResult.Reject(error.Message, "diplomacy.qualification"); }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, "Diplomatic source rules are unresolved."); }
    }

    private sealed class DiplomacyRules(ArmyCampaignRuleAdapter package) : IPlayableDiplomacyCommandRules
    {
        public string Id => "san14:ce-native-diplomacy";
        public DiplomacyQuote? Quote(GameState state, PrepareDiplomaticOrder order)
            => throw new InvalidOperationException("Native diplomacy uses its shared personnel command initializer.");
        public DiplomacyResolution Resolve(GameState state, DiplomaticMission mission)
            => throw new InvalidOperationException("Native diplomacy requires its reached journey and negotiation decision.");
        public CommandResult SubmitDiplomacy(GameState state, ForceState force, PrepareDiplomaticOrder order, CommandContext context)
            => package.SubmitDiplomacyCurrent(state, force, order, context);
        public CommandResult ConfirmDiplomacy(GameState state, ForceState force, ConfirmDiplomaticNegotiationOrder order, CommandContext context)
            => package.ConfirmDiplomacyCurrent(state, force, order, context);
        public GameState AdvanceDiplomacyPeriod(GameState state, int nextPeriodNumber)
        {
            if (nextPeriodNumber != state.PeriodNumber + 1) throw new ArgumentOutOfRangeException(nameof(nextPeriodNumber));
            if (state.Diplomacy?.Missions.Count > 0) throw DiplomacyMissing("legacy_provisional_missions");
            return package.SynchronizeDiplomacyProjection(state);
        }
    }

    private static InvalidDataException DiplomacyMissing(string field) => new("diplomacy.current." + field);
}
