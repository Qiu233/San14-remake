using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    //414900 calls the raw2670D0 scalar. The ordinary monthly preview adds
    //resident, personality and policy modifiers after this leaf; those later
    //modifiers must not be added a second time by a proposal.
    private void CityApplyDevelopment(ProposalEntry entry)
    {
        int area = entry.Arguments.Parameter2, person = PersonPhysical(entry.ProposerPersonId);
        int focus = K(Current.ReadAssignmentArea72(area), "development_focus");
        int before = CityProgress(area, focus, false);
        var amount = CityRawDevelopment(area, person, focus);
        int after = Math.Min(1000, unchecked(CityProgress(area, focus, false) + amount.Gain +
            amount.Effort / Rule("development-effort-divisor", 50) + Rule("development-extra-gain", 1)));
        if (focus is >= 1 and <= 3) WriteAreaDevelopment(area, focus - 1, after);
        int dimension = focus switch { 1 => 2, 2 => 3, 3 => 1, _ => 255 };
        Reward(entry.ProposerPersonId, entry.Kind, dimension,
            amount.Effort / Rule("development-reward-divisor", 50) + Rule("development-reward-base", 10), false);
        int label = focus switch { 2 => 0x4F1E, 3 => 0x4F20, _ => 0x4F1C };
        AddResult(FormatProposal(label, new(Area0: area)).Text, CityNumber(before), CityNumber(after), person);
        History(entry, label + 1, new(Person0: entry.ProposerPersonId, Area0: area, Integers: [after]), TargetHex(entry));
    }

    private void CityApplySecurity(ProposalEntry entry)
    {
        int area = entry.Arguments.Parameter2, person = PersonPhysical(entry.ProposerPersonId);
        int before = K(CityArea(area).Security, "security_before");
        int leadership = PersonAbility(entry.ProposerPersonId, 0), strength = PersonAbility(entry.ProposerPersonId, 1);
        int effort = unchecked(leadership + strength);
        var relations = CityNeighborRelations(area, person, false);
        int adjusted = unchecked(leadership * CityTaskPercent(person)) / 100;
        if (CityAway(person)) adjusted /= 2;
        int basis = unchecked(unchecked((relations.Close * -5 + 100 + relations.Other * 5) * adjusted) / 100 * 3) /
            (K(CityArea(area).Security, "security_before") + 30);
        CityChangeSecurity(area, unchecked(basis + effort / Rule("security-effort-divisor", 100) + Rule("security-extra-gain", 1)));
        int reward = effort / Rule("security-reward-divisor", 100) + Rule("security-reward-base", 10);
        Reward(entry.ProposerPersonId, entry.Kind, 0, reward, false);
        Reward(entry.ProposerPersonId, entry.Kind, 1, reward, false);
        int after = K(CityArea(area).Security, "security_after");
        AddResult(FormatProposal(0x4F22, new(Area0: area)).Text, CityNumber(before), CityNumber(after), person);
        History(entry, 0x4F23, new(Person0: entry.ProposerPersonId, Area0: area, Integers: [after]), TargetHex(entry));
    }

    private void CityApplyInvestment(ProposalEntry entry)
    {
        int area = entry.Arguments.Parameter2;
        byte before = K(Current.ReadAssignmentArea6D(area), "investment_before");
        byte after = (byte)Math.Min(unchecked(before + 1), 3);
        Current.SetAssignmentArea6D(area, after);
        //6D already has one saved owner shared with monthly production.
        State = PersonAssignmentPrecallInputs.PublishAreaAppointments(State, Current, [], [area], []);
        Reward(entry.ProposerPersonId, entry.Kind, 3, Rule("investment-reward", 10), false);
        AddResult(FormatProposal(0x4F24, new(Area0: area)).Text,
            FormatProposal(0x4F25 + before, new()).Text, FormatProposal(0x4F25 + after, new()).Text,
            PersonPhysical(entry.ProposerPersonId));
        int history = after switch { 2 => 0x4F2A, 3 => 0x4F2B, _ => 0x4F29 };
        History(entry, history, new(Person0: entry.ProposerPersonId, Area0: area), TargetHex(entry));
    }

    private void CityApplyConscription(ProposalEntry entry)
    {
        var city = CityAssociatedCity(CityBase(entry.Arguments.Parameter2));
        int cityId = K(city.PhysicalSlot, "conscription_city_identity");
        int initialTroops = CityResource(city, BaseResourceKind.Troops);
        int limit = Rule("conscription-area-count", 1), completed = 0, recruited = 0;
        while (completed < limit)
        {
            var candidates = CityAreas().Where(area => CityConscriptionArea(area, cityId, entry.ProposerPersonId)).ToList();
            //419A30 uses the actual income, population and area identity.
            candidates = candidates.OrderByDescending(area => CityRecruitQuote(area, entry.ProposerPersonId))
                .ThenByDescending(area => K(CityArea(area).NativeNormalAftermath?.Quantity52, "conscription_sort_population"))
                .ThenBy(area => area).ToList();
            bool changed = false;
            foreach (int area in candidates)
            {
                if (completed >= limit) break;
                int gain = Math.Min(CityRecruitQuote(area, entry.ProposerPersonId),
                    BasePostLoad.ReadTroopCapacity(city, Current) - CityResource(city, BaseResourceKind.Troops));
                if (gain <= 0) continue;
                //The original writes troops before its weighted-morale query.
                int afterTroops = Math.Clamp(unchecked(CityResource(city, BaseResourceKind.Troops) + gain), 0,
                    BasePostLoad.ReadTroopCapacity(city, Current));
                int afterMorale = unchecked(CityMorale(city) * afterTroops + Rule("conscription-incoming-morale", 10) * gain) /
                    unchecked(afterTroops + gain);
                int securityLoss = Rule("conscription-security-loss", 0);
                var owner = CityArea(area);
                var aftermath = owner.NativeNormalAftermath ?? throw Missing("conscription_area_aftermath");
                int population = Math.Clamp(unchecked(K(aftermath.Quantity52, "conscription_population") - gain), 0, 60000);
                State = State with { Areas = new(State.Areas) { [area] = owner with
                    { NativeNormalAftermath = aftermath with { Quantity52 = (ushort)population } } } };
                CityWriteResource(city, BaseResourceKind.Troops, afterTroops);
                CityWriteMorale(city, afterMorale);
                CityChangeSecurity(area, -securityLoss);
                recruited = unchecked(recruited + gain); completed++; changed = true;
            }
            if (!changed) break;
        }
        Reward(entry.ProposerPersonId, entry.Kind, 1,
            recruited / Rule("conscription-reward-divisor", 1000) + Rule("conscription-reward-base", 50), false);
        int afterTroopsResult = CityResource(city, BaseResourceKind.Troops);
        ushort location = K(Current.ReadBaseObjectLink(city), "conscription_result_city");
        AddResult(FormatProposal(0x4F2C, new(Base0: location)).Text, CityNumber(initialTroops), CityNumber(afterTroopsResult),
            PersonPhysical(entry.ProposerPersonId));
        History(entry, 0x4F2D, new(Person0: entry.ProposerPersonId, Base0: location, Integers: [afterTroopsResult]), TargetHex(entry));
    }

    private void CityApplyTraining(ProposalEntry entry)
    {
        var target = CityBase(entry.Arguments.Parameter2);
        int initialMorale = CityMorale(target);
        int repetitions = Rule("training-repetitions", 1), total = 0;
        for (int i = 0; i < repetitions && CityMorale(target) < CityMoraleCap(target); i++)
        {
            int before = CityMorale(target);
            CityWriteMorale(target, unchecked(before + CityTrainingGain(target, entry.ProposerPersonId)));
            total = unchecked(total + CityMorale(target) - before);
        }
        int amount = total / Rule("training-reward-divisor", 30) + Rule("training-reward-base", 20);
        Reward(entry.ProposerPersonId, entry.Kind, 1, amount, false);
        Reward(entry.ProposerPersonId, entry.Kind, 0, amount, false);
        int afterMorale = CityMorale(target);
        ushort location = K(Current.ReadBaseObjectLink(target), "training_result_base");
        AddResult(FormatProposal(0x4F2E, new(Base0: location)).Text, CityNumber(initialMorale), CityNumber(afterMorale),
            PersonPhysical(entry.ProposerPersonId));
        History(entry, 0x4F2F, new(Person0: entry.ProposerPersonId, Base0: location, Integers: [afterMorale]), TargetHex(entry));
    }

    private (int Gain, int Effort) CityRawDevelopment(int area, int person, int focus)
    {
        if (focus is < 1 or > 3) return (0, 0);
        int dimension = focus == 1 ? 2 : focus == 2 ? 3 : 1;
        int effective = unchecked(PersonAbility(PersonIdentity(person), dimension) * CityTaskPercent(person)) / 100;
        if (CityAway(person)) effective /= 2;
        var relations = CityNeighborRelations(area, person, true);
        var officer = State.Officers.GetValueOrDefault(PersonIdentity(person)) ?? throw Missing("development_person_owner");
        int doctrine = K(OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState), "development_doctrine");
        bool specialty = focus == 1 ? doctrine is 1 or 5 : focus == 2 ? doctrine is 4 or 6 : doctrine is 2 or 3;
        int progress = CityProgress(area, focus, false), level = CeAreaIncomeMath.Level((ushort)progress);
        int effort = unchecked((relations.Close * -5 + 100 + relations.Other * 5) * effective) / 100 + (specialty ? 10 : 0);
        if (level == 5) effort = unchecked(effort + CityRequiredCurrent("development-completed-level-bonus"));
        byte flags = State.NativeArmySimulation?.Forces.GetValueOrDefault(CityAreaForce(area))?.Flags193 ?? throw Missing("development_force_flags193");
        int gain = unchecked(unchecked(unchecked(effort * 300) / 100 * level) / (30 + progress / 5) * ((flags & 1) != 0 ? 120 : 100)) / 100 + ((flags & 1) != 0 ? 2 : 0);
        return (gain, effort);
    }

    private (int Close, int Other) CityNeighborRelations(int area, int person, bool excludeSelf)
    {
        byte mask = Scenario.Snapshot.Areas[area].RegionalNeighborMask;
        int city = K(Current.ReadAreaCitySlot(new(area)), "neighbor_city");
        int close = 0, other = 0;
        for (int i = 0; i < 8; i++)
        {
            int raw = K(Current.ReadAreaNeighborSlot(new(area), i), "neighbor_slot");
            var neighbor = Current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw Missing("neighbor_pointer");
            if (!K(Current.ReadAreaNativeValidity(neighbor), "neighbor_validity")) continue;
            if ((mask & (1 << i)) == 0 || K(Current.ReadAreaCitySlot(neighbor), "neighbor_city") != city) continue;
            int target = PersonAreaAppointmentSource.Resolve(Current,
                K(Current.ReadAssignmentArea36(K(neighbor.PhysicalSlot, "neighbor_identity")), "neighbor_person"));
            if (!K(Current.ReadPersonNativeValidity(target), "neighbor_person_validity") || excludeSelf && PersonIdentity(target) == PersonIdentity(person)) continue;
            int tier = OwnedPersonRelationInputs.ComputeCurrent(State, Current, person, target, 0).Tier;
            if (tier == 1) close++; else if (tier != 2) other++;
        }
        return (close, other);
    }
    private int CityTaskPercent(int person)
    {
        if (K(TacticRemakeCurrentTraits.PersonContribution(Current, person, 0x7E), "task_trait126") > 0) return 100;
        return CityTasks(person) switch { 0 or 1 => 100, 2 => 64, 3 => 44, _ => CityRequiredCurrent("multiple-task-effort") };
    }
    private bool CityAway(int person)
    {
        ushort flags = K(OfficerBehaviorSourceAdapter.EncodeFlags(Current.ReadPersonBehavior(person)), "city_person_flags");
        if ((flags & 1) != 0) return true;
        if (K(PersonArmyLocationSource.ReadCommanderArmyValidity(State, Current, person), "city_person_army_validity")) return true;
        return K(Current.ReadLinkedEntityNativeValidity(PersonLinkedEntityLookup.Find(person, Current)), "city_person_linked_validity");
    }
    private int CityRequiredCurrent(string key)
    {
        //The ordinary image initializes these scalar fallbacks in the
        //zero-filled data tail. Current parameter overrides remain explicit.
        return key is "development-completed-level-bonus" or "multiple-task-effort"
            ? Rule(key, 0) : RequiredRule(key);
    }
    private static string CityNumber(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private void CityChangeSecurity(int area, int amount)
    {
        if (CityAreaForce(area) is >= 46 and <= 50) return;
        var owner = CityArea(area);
        int value = Math.Clamp(unchecked(K(owner.Security, "security_current") + amount), 0, 100);
        State = State with { Areas = new(State.Areas) { [area] = owner with { Security = value } } };
    }
    private int CityRecruitQuote(int area, int personId) => CityQuote(Preview.Soldiers(area, checked((ushort)personId), 1));
    private void CityWriteResource(BaseReference target, BaseResourceKind resource, int value)
    {
        int id = K(target.PhysicalSlot, "resource_target_identity");
        Current.SetBaseResource(target, resource, value);
        if (target.Kind == BaseKind.City)
        {
            var owner = State.Cities.GetValueOrDefault(id) ?? throw Missing("resource_city_owner");
            var after = resource switch { BaseResourceKind.Gold => owner with { Gold = value },
                BaseResourceKind.Supplies => owner with { Supplies = value }, _ => owner with { Troops = value } };
            State = State with { Cities = new(State.Cities) { [id] = after } };
        }
        else if (resource == BaseResourceKind.Troops)
        {
            var owner = State.Gates.GetValueOrDefault(id) ?? throw Missing("resource_gate_owner");
            State = State with { Gates = new(State.Gates) { [id] = owner with { Troops = value } } };
        }
        else throw Missing("resource_gate_currency_routing");
    }
    private void CityWriteMorale(BaseReference target, int value)
    {
        if (!BasePostLoad.WriteMoraleCurrent(target, value, Current)) return;
        byte after = K(Current.ReadBaseMorale44(target), "morale_stored_value");
        int id = K(target.PhysicalSlot, "morale_target_identity");
        if (target.Kind == BaseKind.City)
        {
            var owner = State.Cities.GetValueOrDefault(id) ?? throw Missing("morale_city_owner");
            var runtime = owner.NativeReturnRuntime ?? throw Missing("morale_city_runtime");
            State = State with { Cities = new(State.Cities) { [id] = owner with
                { Morale = after, NativeReturnRuntime = runtime with { Morale44 = after } } } };
        }
        else
        {
            var owner = State.Gates.GetValueOrDefault(id) ?? throw Missing("morale_gate_owner");
            var runtime = owner.NativeCombatRuntime ?? throw Missing("morale_gate_runtime");
            State = State with { Gates = new(State.Gates) { [id] = owner with
                { NativeCombatRuntime = runtime with { Morale44 = after } } } };
        }
    }
    private int CityTrainingGain(BaseReference target, int personId)
    {
        int troops = CityResource(target, BaseResourceKind.Troops), morale = CityMorale(target);
        if (morale >= 200 || troops == 0) return 0;
        int person = PersonPhysical(personId), leadership = PersonAbility(personId, 0), strength = PersonAbility(personId, 1);
        if (CityAway(person)) { leadership /= 2; strength /= 2; }
        int P(string name) => K(CityDomesticParameters.Read(State, name), "training_parameter." + name);
        float root = MathF.Sqrt((float)unchecked(morale * P("trainingMoraleSqrtScale")));
        float sum = (float)(root + (float)troops / P("trainingTroopsDivisor"));
        float full = (float)(sum + P("trainingDenominatorOffset"));
        int denominator = float.IsNaN(full) || full >= 2147483648f || full < -2147483648f ? int.MinValue : (int)full;
        int basis = unchecked(P("trainingBaseGain") + unchecked(unchecked(leadership + strength) * 2) / Math.Max(1, denominator));
        int trait = K(TacticRemakeCurrentTraits.CommanderEffectMaximum(Current, person, 16), "training_trait16");
        var force = Current.ReadPersonForce(person) ?? throw Missing("training_force");
        int policy = ForcePolicyScratchCurrent.ReadBeforeFreedom(State, Current, force, 31, true).Value;
        return unchecked(basis + unchecked(unchecked(trait * P("trainingTraitScale")) * basis) / 100 +
            unchecked(unchecked(policy * P("trainingPolicyScale")) * basis) / 100);
    }
}
