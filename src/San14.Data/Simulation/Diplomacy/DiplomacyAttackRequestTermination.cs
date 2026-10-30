using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2E98E0(param3=2,param4=1). It terminates the actual recipient's
    //request after its report and relationship effects; no new request or
    //invented completion is created. The enclosing caller owns the current.
    internal GameState CancelDiplomacyAttackRequestCurrent(GameState state, SourceWorldContext current,
        int receiverForceId, List<CampaignEffectRequest> effects)
        => TerminateDiplomacyAttackRequestCurrent(state, current,
            DiplomacyForce(current, receiverForceId), null, 2, effects);

    //2E98E0 selects its relationship amount from param1, before selecting
    //the actual request holder. In kinds3/4 those are different forces.
    private GameState TerminateDiplomacyAttackRequestCurrent(GameState state, SourceWorldContext current,
        ForceReference source, BaseReference? captured, int kind, List<CampaignEffectRequest> effects, bool showRelationChange = true)
    {
        int sourceId = TalentKnown(source.PhysicalSlot, "request_end_source_Force");
        int delta;
        if (kind == 1) delta = RequestParameter("request-completed-relation-adjustment");
        else
        {
            int progress = AttackRequestObjective(state, sourceId).Progress
                ?? throw DiplomacyMissing("request_end_source_progress162");
            if (progress == 0) delta = RequestParameter("request-zero-relation-adjustment");
            else if (progress >= RequestParameter("request-progress-high-threshold"))
                delta = RequestParameter("request-completed-relation-adjustment");
            else if (progress >= RequestParameter("request-progress-low-threshold"))
                delta = RequestParameter("request-partial-relation-adjustment");
            else delta = RequestParameter("request-low-progress-current-adjustment");
        }

        ForceReference receiver;
        if (kind is 1 or 2) receiver = source;
        else if (kind == 3)
            receiver = FindRequestHolder(force =>
                TalentKnown(AttackRequestObjective(state, force).RequestingForceId, "request_end_holder_requester15F") == sourceId);
        else if (kind == 4)
        {
            var target = captured ?? throw DiplomacyMissing("request_end_captured_Base");
            bool valid = TalentKnown(current.ReadBaseNativeValidity(target), "request_end_captured_validity");
            receiver = FindRequestHolder(force => valid &&
                TalentKnown(AttackRequestObjective(state, force).TargetLocationId, "request_end_holder_target160") ==
                    TalentKnown(current.ReadBaseObjectLink(target), "request_end_captured_location4E"));
        }
        else throw DiplomacyMissing("request_end_kind");
        int receiverForceId = TalentKnown(receiver.PhysicalSlot, "request_end_receiver_Force");
        var objective = AttackRequestObjective(state, receiverForceId);
        int requesting = objective.RequestingForceId ?? throw DiplomacyMissing("request_end_requester15F");
        int location = objective.TargetLocationId ?? throw DiplomacyMissing("request_end_target160");
        var requester = kind == 3 ? source : DiplomacyForce(current, requesting);
        int requesterId = TalentKnown(requester.PhysicalSlot, "request_end_requester_Force");
        state = WriteDiplomacyAttackRequestEndCurrent(state, current, receiverForceId, requesting, location, effects,
            kind, sourceId);
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
            state = showRelationChange
                ? ApplyDiplomacyRelationCurrent(state, current, receiverForceId, requesterId, delta, effects)
                : ForceRelationChangeCurrent.ApplyWithoutPresentation(state, current, receiverForceId, requesterId, delta,
                    key => ArmyDestructionParameterSeed.ReadCurrent(state, key) ?? throw DiplomacyMissing("relation_parameter_" + key));
        }
        var force = state.Forces.GetValueOrDefault(receiverForceId) ?? throw DiplomacyMissing("request_end_latest_Force");
        objective = force.CombatObjective ?? throw DiplomacyMissing("request_end_latest_objective");
        return state with { Forces = new(state.Forces) { [force.Id] = force with
            { CombatObjective = objective with { RequestingForceId = 0, TargetLocationId = 0, RemainingMonths = 0, Progress = 0 } } } };

        int RequestParameter(string key)
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw DiplomacyMissing("request_end_parameters");
            if (!HasDiplomacyParameterProfile(parameters.Language,parameters.RuleVersion))
                throw DiplomacyMissing("request_end_parameter_profile");
            string name = "diplomacy:" + key;
            if (parameters.Float32Values.ContainsKey(name)) throw DiplomacyMissing("parameter_kind_" + name);
            if (parameters.Int32Values.TryGetValue(name, out int value)) return value;
            //Independent SC2E986B consumes the qualified loader-zero owner.
            //Do not extend this missing-key default to an unqualified locale.
            if (parameters.Language == "SC" && key == "request-low-progress-current-adjustment") return 0;
            throw DiplomacyMissing("parameter_" + name);
        }

        ForceReference FindRequestHolder(Func<int, bool> matches)
        {
            foreach (int physical in ReadAttackRequestForceOrder(current))
                if (matches(physical)) return DiplomacyForce(current, physical);
            //The original would have no selected receiver; do not invent a
            //request or clear the outer loop's force instead.
            throw DiplomacyMissing("request_end_selected_holder");
        }
    }

    private GameState InstallFreshDiplomacyRequestTerminationParameters(GameState state, bool preserveExisting = false)
    {
        if (Seed.Language is not ("TC" or "SC")) return state;
        var native = state.NativeArmySimulation ?? throw DiplomacyMissing("fresh_request_end_parameters");
        if (native.Parameters.Language != Seed.Language ||
            !HasDiplomacyParameterProfile(native.Parameters.Language,native.Parameters.RuleVersion))
            throw DiplomacyMissing("fresh_request_end_parameter_profile");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        var rules = new List<(string Key, int Value)>
        {
            ("request-progress-high-threshold", 800),
            ("request-progress-low-threshold", 200),
            ("request-completed-relation-adjustment", 50),
            ("request-partial-relation-adjustment", 20),
            ("request-zero-relation-adjustment", -30)
        };
        if (Seed.Language == "SC") rules.Add(("request-low-progress-current-adjustment", 0));
        foreach (var (key, value) in rules)
        {
            string name = "diplomacy:" + key;
            if (native.Parameters.Float32Values.ContainsKey(name))
                throw DiplomacyMissing("fresh_request_end_parameter_kind");
            if (values.ContainsKey(name) && preserveExisting) continue;
            if (values.ContainsKey(name))
                throw DiplomacyMissing("fresh_request_end_parameter_conflict");
            values.Add(name, value);
        }
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }
}
