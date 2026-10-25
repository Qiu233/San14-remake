using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public IPlayableTalentResolution CreateTalentResolutionRules() => new TalentRules(this);

    public TalentCommandView QueryTalentSearch(GameState state, int forceId, int cityId, int targetHexId)
        => ReadTalentView(state, forceId, cityId, TalentOrderKind.Search, targetHexId, null);

    public TalentCommandView QueryTalentRecruit(GameState state, int forceId, int cityId, int? targetOfficerId = null)
        => ReadTalentView(state, forceId, cityId, TalentOrderKind.Recruit, null, targetOfficerId);

    private TalentCommandView ReadTalentView(GameState state, int forceId, int cityId,
        TalentOrderKind kind, int? targetHex, int? targetId)
    {
        var executors = new List<int>();
        var targets = new List<int>();
        int? cost = null;
        try
        {
            if (state.Phase != TurnPhase.Strategy || !state.Cities.TryGetValue(cityId, out var city) || city.ForceId != forceId)
                return new(executors, targets, false, null, null, 0);
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            PersonReturnCommands.BindCurrent(state, current);
            foreach (int? raw in current.ReadRegisteredPersonPhysicalSlots() ?? throw TalentCommandMissing("Person_registry"))
            {
                int person = TalentKnown(raw, "Person_pointer");
                if (!TalentKnown(current.ReadPersonNativeValidity(person), "Person_validity")) continue;
                int id = TalentKnown(current.ReadPersonIdentity(person), "Person_identity");
                var sourceForce = current.ReadPersonForce(person) ?? throw TalentCommandMissing("Person_Force");
                int own = TalentKnown(sourceForce.PhysicalSlot, "Person_Force_identity");
                if (kind == TalentOrderKind.Recruit && id != 0 && own != forceId &&
                    TalentKnown(current.ReadPersonStatus(person), "target_status") is >= 1 and <= 6)
                    targets.Add(id);
                if (own != forceId || TalentKnown(current.ReadPersonFirstLocation(person), "Person_Home") != cityId ||
                    PersonCurrentBusySource.Read(current, person, TalentCommandMissing)) continue;
                if (!state.Officers.TryGetValue(id, out var actor) || actor.Deployed ||
                    state.PreparedTalentOrders?.ContainsKey(id) == true ||
                    state.PreparedOfficerMoves?.ContainsKey(id) == true ||
                    state.PreparedDeployments.Values.Any(plan => plan.OfficerId == id) ||
                    PlayableDiplomacy.IsOfficerOnMission(state, id)) continue;
                executors.Add(id);
            }
            // Keep the complete target publication even before the player has
            // selected a target; eligibility is independent of success odds.
            bool targetAllowed;
            if (kind == TalentOrderKind.Search)
            {
                if (targetHex is not (>= 0 and < 48400)) targetAllowed = false;
                else
                {
                    var hex = current.ReadHexTableReference(targetHex.Value) ?? throw TalentCommandMissing("target_Hex");
                    int areaId = TalentKnown(current.ReadHexAreaSlot(hex), "target_Area");
                    var area = current.ReadAreaTableReference(areaId <= 500 ? areaId : 0) ?? throw TalentCommandMissing("Area_pointer");
                    targetAllowed = TalentKnown(current.ReadAreaNativeValidity(area), "Area_validity") &&
                        TalentKnown(current.ReadAreaCurrentForceSlot(area), "Area_Force") == forceId;
                }
            }
            else targetAllowed = targetId.HasValue && targets.Contains(targetId.Value);
            cost = PersonReturnCommands.ReadTalentBookCost(state, kind);
            if (state.PersonnelCommands?.Talents is null) throw TalentCommandMissing("saved_task_owner");
            if (city.DistrictId is not int districtId || !state.Districts.TryGetValue(districtId, out var district))
                throw TalentCommandMissing("source_District");
            int books = district.OrdersRemaining ?? throw TalentCommandMissing("District_books");
            return new(executors, targets, targetAllowed && executors.Count != 0 &&
                books - CommandBookAvailability.Reserved(state, districtId) >= cost, null, cost, 0);
        }
        catch (InvalidDataException missing) { return new(executors, targets, null, missing.Message, cost, 0); }
    }

    private GameState StartTalentCurrent(GameState state, PreparedTalentOrder task)
    {
        var world = CaptureOwnedArmyReturnWorld(state);
        var current = world.Context;
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        var date = state.NativeMapSeason?.ThirdDate ?? throw TalentCommandMissing("current_date");
        var started = PersonReturnCommands.StartTalentTask(state, current, task, date,
            (working, home) =>
            {
                var packet = ApplyPersonFreedomGovernorCurrent(working, current, home);
                var result = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
                return result;
            },
            (working, actor, target, kind) => ResolveTalentAdvisorSampleCurrent(working, current,
                actor, target, kind, () => ReadArmyDestructionSampler(working, current)
                    ?? throw TalentCommandMissing("sampler")), ReadApplicationGameplay());
        var updated = started.State ?? throw TalentCommandMissing(started.UnresolvedOwner ?? "creation");
        return PersonReturnCommands.PublishCurrent(updated, current);
    }

    private sealed class TalentRules(ArmyCampaignRuleAdapter package) : IPlayableTalentResolution, IPlayableTalentCommandRules
    {
        public TalentCommandView QueryTalentSearch(GameState state, int forceId, int cityId, int targetHexId)
            => package.QueryTalentSearch(state, forceId, cityId, targetHexId);
        public TalentCommandView QueryTalentRecruit(GameState state, int forceId, int cityId, int? targetOfficerId = null)
            => package.QueryTalentRecruit(state, forceId, cityId, targetOfficerId);

        public CommandResult PrepareTalent(GameState state, ForceState force, PreparedTalentOrder task, CommandContext context)
        {
            if (!Enum.IsDefined(task.Kind) || (task.Kind == TalentOrderKind.Search ? task.RecruitMode != 0 :
                task.RecruitMode is not (0 or 10 or 30)))
                return CommandResult.Reject("Talent recruitment mode is invalid.", "talent.recruit_mode");
            var view = task.Kind == TalentOrderKind.Search
                ? package.QueryTalentSearch(state, force.Id, task.CityId, task.TargetHexId ?? -1)
                : package.QueryTalentRecruit(state, force.Id, task.CityId, task.TargetOfficerId);
            if (view.CanSubmit is null) return CommandResult.RuleUnavailable(view.MissingRuleKey ?? "talent.query", "Talent source rules are unresolved.");
            if (!view.CanSubmit.Value || !view.ExecutorOfficerIds.Contains(task.ExecutorOfficerId))
                return CommandResult.Reject("Talent executor or target is unavailable.", "talent.qualification");
            try { return CommandResult.Accept(package.StartTalentCurrent(state, task)); }
            catch (InvalidDataException missing) { return CommandResult.RuleUnavailable(missing.Message, "Talent command could not be registered."); }
        }

        public TalentOrderOutcome Resolve(GameState current, PreparedTalentOrder task)
        {
            var result = package.StartTalentCurrent(current, task);
            return new(null, result.RandomState) { State = result };
        }
    }

    private static T TalentKnown<T>(T? value, string field) where T : struct => value ?? throw TalentCommandMissing(field);
    private static InvalidDataException TalentCommandMissing(string field) => new("talent.current." + field);
}
