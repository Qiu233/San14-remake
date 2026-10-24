using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2E98E0(param3=2,param4=1). It terminates the actual recipient's
    //request after its report and relationship effects; no new request or
    //invented completion is created. The enclosing caller owns the current.
    internal GameState CancelDiplomacyAttackRequestCurrent(GameState state, SourceWorldContext current,
        int receiverForceId, List<CampaignEffectRequest> effects)
    {
        var receiver = current.ReadForceTableReference(receiverForceId is >= 0 and <= 51 ? receiverForceId : 0)
            ?? throw DiplomacyMissing("request_end_receiver");
        var force = state.Forces.GetValueOrDefault(receiverForceId) ?? throw DiplomacyMissing("request_end_Force_owner");
        var objective = force.CombatObjective ?? throw DiplomacyMissing("request_end_objective");
        int progress = objective.Progress ?? throw DiplomacyMissing("request_end_progress162");
        int delta;
        if (progress == 0) delta = RequestParameter("request-zero-relation-adjustment");
        else if (progress >= RequestParameter("request-progress-high-threshold"))
            delta = RequestParameter("request-completed-relation-adjustment");
        else if (progress >= RequestParameter("request-progress-low-threshold"))
            delta = RequestParameter("request-partial-relation-adjustment");
        else delta = RequestParameter("request-low-progress-current-adjustment");
        int requesting = objective.RequestingForceId ?? throw DiplomacyMissing("request_end_requester15F");
        int location = objective.TargetLocationId ?? throw DiplomacyMissing("request_end_target160");
        var requester = current.ReadForceTableReference(requesting is >= 0 and <= 51 ? requesting : 0)
            ?? throw DiplomacyMissing("request_end_requester");
        state = WriteDiplomacyAttackRequestEndCurrent(state, current, receiverForceId, requesting, location, effects);
        if (TalentKnown(current.ReadForceNativeValidity(receiver), "request_end_receiver_validity") &&
            TalentKnown(current.ReadForceNativeValidity(requester), "request_end_requester_validity"))
        {
            if (delta > 0)
            {
                var first = PersonReturnPathSource.ReadForceAdvantage(state, current, receiver, 8, 100);
                int firstValue = first.Value ?? throw DiplomacyMissing(first.UnresolvedOwner ?? "request_end_first_Advantage8");
                var second = PersonReturnPathSource.ReadForceAdvantage(state, current, requester, 8, 100);
                int secondValue = second.Value ?? throw DiplomacyMissing(second.UnresolvedOwner ?? "request_end_second_Advantage8");
                delta = unchecked((first.QualifiedStates.Length != 0 ? firstValue : secondValue) * delta) / 100;
            }
            state = ApplyDiplomacyRelationCurrent(state, current, receiverForceId, requesting, delta, effects);
        }
        force = state.Forces.GetValueOrDefault(receiverForceId) ?? throw DiplomacyMissing("request_end_latest_Force");
        objective = force.CombatObjective ?? throw DiplomacyMissing("request_end_latest_objective");
        return state with { Forces = new(state.Forces) { [force.Id] = force with
            { CombatObjective = objective with { RequestingForceId = 0, TargetLocationId = 0, RemainingMonths = 0, Progress = 0 } } } };

        int RequestParameter(string key)
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw DiplomacyMissing("request_end_parameters");
            string name = "diplomacy:" + key;
            return !parameters.Float32Values.ContainsKey(name) && parameters.Int32Values.TryGetValue(name, out int value)
                ? value : throw DiplomacyMissing("parameter_" + name);
        }
    }

    private GameState InstallFreshDiplomacyRequestTerminationParameters(GameState state, bool preserveExisting = false)
    {
        if (Seed.Language != "TC") return state;
        var native = state.NativeArmySimulation ?? throw DiplomacyMissing("fresh_request_end_parameters");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var (key, value) in new (string, int)[]
        {
            ("request-progress-high-threshold", 800),
            ("request-progress-low-threshold", 200),
            ("request-completed-relation-adjustment", 50),
            ("request-partial-relation-adjustment", 20),
            ("request-zero-relation-adjustment", -30)
        })
        {
            string name = "diplomacy:" + key;
            if (values.ContainsKey(name) && preserveExisting) continue;
            if (values.ContainsKey(name) || native.Parameters.Float32Values.ContainsKey(name))
                throw DiplomacyMissing("fresh_request_end_parameter_conflict");
            values.Add(name, value);
        }
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }
}
