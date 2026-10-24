using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IGovernanceRosterRules
{
    /// <summary>One page query, one operation-local current capture. Neither
    /// candidate evaluation nor reading the saved roster publishes anything.</summary>
    public GovernanceRosterView QueryGovernanceRoster(GameState state, int forceId)
    {
        if (!state.Forces.TryGetValue(forceId, out var force))
            return new(forceId, null, [], [], null, null, "governance.force");
        var candidates = new List<GovernanceCandidate>();
        SourceWorldContext? current = null;
        TacticRemakeCurrentAbilities? abilities = null;
        string? captureError = null;
        try
        {
            current = CaptureDeploymentPersonSelectionCurrent(state);
            _baseCapacityInputs.BindCurrent(state, current);
            abilities = new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        }
        catch (InvalidDataException error) { captureError = error.Message; }
        foreach (var person in state.Officers.Values.Where(person => person.ForceId == forceId).OrderBy(person => person.Id))
        {
            int? group = person.PolicyId is int policyId && state.Policies.TryGetValue(policyId, out var policy)
                ? policy.DepartmentCode : null;
            int? cost = PolicyCosts.TryCalculate(state, person.Id, out var quote) ? quote.Total : null;
            int? intelligence = null;
            bool? departmentEligible = null, advisorEligible = null;
            string? missing = captureError;
            try
            {
                if (current is null) throw GovernanceMissing(captureError ?? "current_capture");
                int physical = _definitions.ResolveOfficerSource(person.Id) ?? throw GovernanceMissing("person_identity");
                bool eligible = RosterPickerEligible(state, current, force, physical);
                departmentEligible = eligible && group is >= 0 and < 5;
                if (!eligible) advisorEligible = false;
                else
                {
                    intelligence = abilities!.ResolveDimension(state, person.Id, 2);
                    int? minimum = GovernanceParameter(state, GovernanceRosterParameters.AdvisorIntelligenceMinimum);
                    advisorEligible = intelligence is int ability && minimum is int threshold ? ability >= threshold : null;
                    if (advisorEligible is null) missing = "governance.advisor_current_intelligence_or_threshold";
                }
            }
            catch (InvalidDataException error) { missing = error.Message; }
            candidates.Add(new(person.Id, group, cost, intelligence, departmentEligible, advisorEligible, missing));
        }
        var roster = GovernanceRosterSource.Read(state, forceId, _scenario.Snapshot.DoctrineParameters, candidates.AsReadOnly());
        return roster with { CostLimit = ReadGovernanceCostLimit(state, forceId) };
    }

    public GovernanceRosterEvaluation EvaluateGovernanceRoster(GameState state, ForceState force,
        GovernanceRoster target, CommandContext context)
    {
        if (context.DelegatedDistrictId is not null)
            return new(false, null, null, null, null, null, "Governance requires direct force control.", null);
        if (target is null || target.Groups is null || target.Groups.Count != 5 || target.Groups.Any(group => group is null) ||
            !target.Groups.Select(group => group.Group).Order().SequenceEqual(Enumerable.Range(0, 5)) ||
            target.Groups.Any(group => group is null || group.OfficerIds is null || group.OfficerIds.Count > 7 ||
                group.OfficerIds.Any(id => id is < 1 or > 6000)))
            return new(false, null, null, null, null, null, "Governance needs five ordered department lists.", null);
        var people = target.Groups.SelectMany(group => group.OfficerIds)
            .Concat(target.AdvisorId is int advisor ? [advisor] : []).ToArray();
        if (target.AdvisorId is <= 0 or > 6000 || people.Distinct().Count() != people.Length)
            return new(false, null, null, null, null, null, "An officer cannot occupy two appointments.", null);
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            var previous = GovernanceRosterSource.Read(state, force.Id, _scenario.Snapshot.DoctrineParameters, []);
            if (!previous.Available) throw GovernanceMissing(previous.UnresolvedInput ?? "roster_owner");
            int fee = target.AdvisorId != previous.Advisor?.OfficerId && target.AdvisorId is int ? 1 : 0;
            if (target.AdvisorId is int advisorId)
            {
                int physical = _definitions.ResolveOfficerSource(advisorId) ?? throw GovernanceMissing("advisor_identity");
                if (!RosterPickerEligible(state, current, force, physical))
                    return new(false, fee, null, null, null, null, "Officer is ineligible for military adviser.", null);
                var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
                _baseCapacityInputs.BindCurrent(state, current);
                int intelligence = abilities.ResolveDimension(state, advisorId, 2) ?? throw GovernanceMissing("advisor_current_intelligence");
                int minimum = GovernanceParameter(state, GovernanceRosterParameters.AdvisorIntelligenceMinimum)
                    ?? throw GovernanceMissing("advisor_current_intelligence_minimum");
                if (intelligence < minimum)
                    return new(false, fee, null, null, null, null, "Military adviser intelligence is below the current minimum.", null);
            }
            int totalCost = force.RulerId is int ruler && PolicyCosts.TryCalculate(state, ruler, out var rulerCost)
                ? rulerCost.Total : throw GovernanceMissing("ruler_policy_cost");
            foreach (var group in target.Groups)
            {
                var before = previous.Groups.Single(row => row.Group == group.Group);
                if (before.Capacity is not int capacity) throw GovernanceMissing("department_capacity");
                if (group.OfficerIds.Count > capacity)
                    return new(false, fee, null, totalCost, null, null, "Department capacity exceeded.", null);
                foreach (int id in group.OfficerIds)
                {
                    int physical = _definitions.ResolveOfficerSource(id) ?? throw GovernanceMissing("person_identity");
                    if (!RosterPickerEligible(state, current, force, physical) ||
                        state.Officers[id].PolicyId is not int policyId ||
                        state.Policies.GetValueOrDefault(policyId)?.DepartmentCode != group.Group)
                        return new(false, null, null, null, null, null, "Officer is ineligible for this department.", null);
                    if (!before.Members.Any(old => old.OfficerId == id && RosterNativeValid(current, old.OfficerId))) fee++;
                    if (!PolicyCosts.TryCalculate(state, id, out var cost)) throw GovernanceMissing("officer_policy_cost");
                    totalCost = checked(totalCost + cost.Total);
                }
            }
            int rulerPhysical = _definitions.ResolveOfficerSource(force.RulerId!.Value) ?? throw GovernanceMissing("ruler_identity");
            int districtId = current.ReadPersonDistrictSlot(rulerPhysical) ?? throw GovernanceMissing("ruler_current_district");
            if (!state.Districts.TryGetValue(districtId, out var district) || district.ForceId != force.Id ||
                district.OrdersRemaining is not int books)
                throw GovernanceMissing("ruler_district_command_books");
            if (books - CommandBookAvailability.Reserved(state, districtId) < fee)
                return new(false, fee, districtId, totalCost, null, null, "Not enough unreserved command books.", null);
            int limit = ReadGovernanceCostLimit(state, force.Id)
                ?? throw GovernanceMissing("current_policy_cost_limit");
            if (totalCost > limit)
                return new(false, fee, districtId, totalCost, limit, null, "Policy cost limit exceeded.", null);
            bool overflow = false;
            var contributors = people.Concat(force.RulerId is int rulerId ? [rulerId] : []);
            foreach (int effect in contributors.SelectMany(id =>
                state.Officers.TryGetValue(id, out var person) && person.PolicyId is int policyId
                    ? state.Policies.GetValueOrDefault(policyId)?.EffectIds ?? throw GovernanceMissing("policy_effect_references")
                    : Array.Empty<int>()).Distinct())
            {
                int level = BattleRemakeCurrentForcePolicies.ReadGovernanceTargetLevel(state, force, target, effect, current)
                    ?? throw GovernanceMissing("target_policy_level");
                if (level > 10) overflow = true;
            }
            // These independently mutable inputs are required before any
            // appointment is stored. No fresh guess or restore repair is used.
            if (fee != 0)
            {
                _ = GovernanceParameter(state, GovernanceRosterParameters.NewAppointmentPending)
                    ?? throw GovernanceMissing("new_appointment_pending");
                _ = GovernanceParameter(state, GovernanceRosterParameters.PendingReductionPerTurn)
                    ?? throw GovernanceMissing("pending_reduction_per_turn");
                if (state.CampaignEventWorld?.CurrentUserIndex != -1)
                    throw GovernanceMissing("command_statistics_current_user");
            }
            return new(true, fee, districtId, totalCost, limit, overflow, null, null);
        }
        catch (InvalidDataException error)
        {
            return new(null, null, null, null, null, null, null, error.Message);
        }
    }

    public CommandResult ApplyGovernanceRoster(GameState state, ForceState force,
        SetGovernanceRosterOrder order, CommandContext context)
    {
        var evaluation = EvaluateGovernanceRoster(state, force, order.Roster, context);
        if (evaluation.Allowed == false)
            return CommandResult.Reject(evaluation.Reason ?? "Governance roster is invalid.", "governance.roster");
        if (evaluation.Allowed != true)
            return CommandResult.RuleUnavailable(evaluation.UnresolvedInput ?? "governance.roster_submit",
                "The ordered roster requires its current rule inputs.");
        if (evaluation.ExceedsLevelLimit == true && !order.AcceptLevelOverflow)
            return CommandResult.Reject("Confirm the policy levels above ten before submitting.", "governance.level_overflow_confirmation");
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var response = SelectGovernanceResponse(state, current, force.Id, order.Roster);
            var result = new GovernanceRosterApplication(state, current, world.Pool, force.Id).Apply(order.Roster);
            int districtId = evaluation.ChargedDistrictId ?? throw GovernanceMissing("charged_district");
            int fee = evaluation.CommandBooks ?? throw GovernanceMissing("command_books");
            var district = result.Districts[districtId];
            int books = district.OrdersRemaining ?? throw GovernanceMissing("district_command_books");
            result = result with { Districts = new(result.Districts)
                { [districtId] = district with { OrdersRemaining = Math.Clamp(unchecked(books - fee), 0, 200) } } };
            if (response is { } notification)
            {
                int ruler = current.ReadPersonForce(_definitions.ResolveOfficerSource(notification.Speaker) ??
                    throw GovernanceMissing("response_speaker"))?.PhysicalSlot is int speakerForce
                    ? result.Forces.GetValueOrDefault(speakerForce)?.RulerId ?? throw GovernanceMissing("response_ruler")
                    : throw GovernanceMissing("response_force");
                var text = FormatGovernanceResponse(result, current, notification.Appointed ? 0x57F9 : 0x57F4,
                    notification.Speaker, ruler, ReadHistoryDataText(notification.Advisor ? 1 : 0,
                        notification.Advisor ? 0x243 : 361 + notification.Group));
                result = text.State with { CampaignEffectRequests =
                    (text.State.CampaignEffectRequests ?? throw GovernanceMissing("narration_owner"))
                    .Append(CampaignEffectSource.GovernanceRoster,
                        result.NativeClock?.UpdateOrdinal ?? throw GovernanceMissing("command_revision"),
                        [new(null, null) { GovernanceNarration = new(force.Id, notification.Speaker,
                            notification.Appointed ? 0x57F9 : 0x57F4, text.Text) }]) };
            }
            return CommandResult.Accept(result);
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable(error.Message, "The governance transaction could not be completed."); }
    }

    public CommandResult CompleteGovernanceNarration(GameState state, ForceState force,
        CompleteGovernanceNarrationOrder order, CommandContext context)
    {
        if (context.DelegatedDistrictId is not null)
            return CommandResult.Reject("Governance narration belongs to direct force control.", "governance.narration_scope");
        if (state.CampaignEffectRequests is not { } pending)
            return CommandResult.RuleUnavailable("governance.narration_owner", "Pending responses are unknown.");
        var batch = pending.PendingBatches.FirstOrDefault(row => row.Sequence == order.BatchSequence);
        if (batch is null || batch.Source != CampaignEffectSource.GovernanceRoster || batch.Requests.Length == 0 ||
            batch.Requests.Any(row => row.GovernanceNarration?.ForceId != force.Id))
            return CommandResult.Reject("This governance response is no longer pending for the force.", "governance.narration_pending");
        return CommandResult.Accept(state with { CampaignEffectRequests = pending with
            { PendingBatches = pending.PendingBatches.Where(row => row.Sequence != order.BatchSequence).ToArray() } });
    }

    /// <summary>One selected response's real large portrait route. This neither
    /// formats text nor draws randoms, publishes state or consumes the response.</summary>
    public AreaAdministrationQuery<DeploymentNarrationPortraitRoute> QueryGovernanceNarrationPortrait(
        GameState state, int speakerId)
    {
        try
        {
            if (!state.Officers.TryGetValue(speakerId, out var speaker) || speaker.PortraitAppearanceIndex is not ushort appearance)
                throw GovernanceMissing("narration_portrait_appearance");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            return AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Known(
                ReadNarrationPortrait(state, current, _definitions, speakerId, appearance));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Unknown(error.Message); }
    }

    private sealed record GovernanceResponse(int Speaker, bool Appointed, bool Advisor, int Group);
    private GovernanceResponse? SelectGovernanceResponse(GameState state, SourceWorldContext current,
        int forceId, GovernanceRoster target)
    {
        var previous = GovernanceRosterSource.Read(state, forceId, _scenario.Snapshot.DoctrineParameters, []);
        GovernanceResponse? response = null;
        bool advisorChanged = previous.Advisor?.OfficerId != target.AdvisorId;
        if (advisorChanged)
        {
            if (target.AdvisorId is int advisor) response = new(advisor, true, true, 0);
            else if (previous.Advisor?.OfficerId is int old && RosterNativeValid(current, old)) response = new(old, false, true, 0);
        }
        if (advisorChanged && target.AdvisorId is not null) return response;
        bool added = false;
        foreach (var group in target.Groups.OrderBy(row => row.Group))
        {
            var old = previous.Groups.Single(row => row.Group == group.Group).Members
                .Where(row => RosterNativeValid(current, row.OfficerId)).Select(row => row.OfficerId).ToArray();
            foreach (int person in group.OfficerIds.Where(person => !old.Contains(person)))
                if (response is null || person <= response.Speaker)
                { response = new(person, true, false, group.Group); added = true; advisorChanged = false; }
            if (!added && !advisorChanged)
                foreach (int person in old.Where(person => !group.OfficerIds.Contains(person)))
                    if (response is null || person <= response.Speaker) response = new(person, false, false, group.Group);
        }
        return response;
    }

    private bool RosterPickerEligible(GameState state, SourceWorldContext current, ForceState force, int physical)
    {
        if (!RosterNeed(current.ReadPersonNativeValidity(physical), "person_validity")) return false;
        int id = RosterNeed(current.ReadPersonIdentity(physical), "person_id");
        if (id == force.RulerId) return false;
        int actualForce = current.ReadPersonForce(physical)?.PhysicalSlot ?? throw GovernanceMissing("person_force");
        if (actualForce != force.Id) return false;
        byte status = RosterNeed(current.ReadPersonStatus(physical), "person_status");
        if (status is < 1 or > 4) return false; // ForcePersonIterator, filter0.
        if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id))
        {
            ushort first = RosterNeed(current.ReadPersonFirstLocation(physical), "band_first_location");
            if (first is >= 1 and <= 61 && actualForce is not (>= 46 and <= 50)) return false;
        }
        return (RosterNeed(current.ReadArmyReturnPersonFlags196(physical), "person_flags") & 0x4000) == 0;
    }

    private bool RosterNativeValid(SourceWorldContext current, int id) =>
        _definitions.ResolveOfficerSource(id) is int physical && RosterNeed(current.ReadPersonNativeValidity(physical), "old_person_validity");

    private int? ReadGovernanceCostLimit(GameState state, int forceId)
    {
        int? basis = _force164Inputs.ReadGovernanceCostLimit(state, forceId);
        int? extra = _baseCapacityInputs.ReadPanelModifier(state, forceId, 51);
        return basis is int amount && extra is int bonus ? unchecked(amount + bonus) : null;
    }

    private static int? GovernanceParameter(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } parameters && !parameters.Float32Values.ContainsKey(key) &&
        parameters.Int32Values.TryGetValue(key, out int value) ? value : null;
    private static T RosterNeed<T>(T? value, string name) where T : struct => value ?? throw GovernanceMissing(name);
    private static InvalidDataException GovernanceMissing(string name) => new("governance." + name);
}
