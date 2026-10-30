using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The inherited saved advisor result is knowledge, not an override of
    //1EB480. Creation may retain this trial when actual advisor accuracy=100.
    internal (bool Succeeded, int ChancePercent, bool Injured) ResolveIntelligenceAwayTrialCurrent(
        GameState state, SourceWorldContext current, int actor, int targetForceId, int duration)
    {
        var target = current.ReadForceTableReference(targetForceId <= 51 ? targetForceId : 0)
            ?? throw IntelligenceMissing("trial_target_Force");
        if (!IntelligenceNeed(current.ReadPersonNativeValidity(actor), "trial_actor_validity") ||
            !IntelligenceNeed(current.ReadForceNativeValidity(target), "trial_target_validity")) return (false, 0, false);
        var own = current.ReadPersonForce(actor) ?? throw IntelligenceMissing("trial_own_Force");
        if (!IntelligenceNeed(current.ReadForceNativeValidity(own), "trial_own_validity")) return (false, 0, false);
        if (IntelligenceTutorialFlag(state, 88)) return (true, 0, false);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        int Ability(int person, int dimension) => IntelligenceNeed(abilities.ResolveDimension(state,
            IntelligenceNeed(current.ReadPersonIdentity(person), "trial_ability_Person10"), dimension,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "trial_ability");
        int Lookup(int id) => IntelligenceNeed((id <= 6000 ? current.ReadPersonLookupReference(id)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "trial_Person_lookup");
        int targetId = IntelligenceNeed(target.PhysicalSlot, "trial_target_identity");
        int ownId = IntelligenceNeed(own.PhysicalSlot, "trial_own_identity");
        int ruler = Lookup(IntelligenceNeed(current.ReadForceRecord(targetId)?.RawRuler10, "trial_ruler10"));
        int advisor = Lookup(IntelligenceNeed(current.ReadAssignmentForceAdvisor52(targetId), "trial_target_advisor"));
        int ownAdvisor = Lookup(IntelligenceNeed(current.ReadAssignmentForceAdvisor52(ownId), "trial_own_advisor"));
        int actorAbility = Ability(actor, 2), rulerAbility = Ability(ruler, 2);
        int targetAdvisorAbility = IntelligenceNeed(current.ReadPersonNativeValidity(advisor), "trial_target_advisor_validity") ? Ability(advisor, 2) : 0;
        int ownAdvisorAbility = IntelligenceNeed(current.ReadPersonNativeValidity(ownAdvisor), "trial_own_advisor_validity") ? Ability(ownAdvisor, 2) : 0;
        var capital = new DiplomacyCommandSource(state, current).Capital(targetId);
        var area = CurrentGateMapQueries.FindArea(capital, current);
        int areaId = IntelligenceNeed(area.PhysicalSlot, "trial_capital_Area");
        int security = IntelligenceNeed(state.Areas.GetValueOrDefault(areaId)?.Security, "trial_capital_Security54");
        if ((uint)security > byte.MaxValue) throw IntelligenceMissing("trial_capital_Security54_width");
        int count = Math.Clamp(ReadIntelligenceTargetFieldArmiesCurrent(state, current, targetId), 0, 10);
        int intelligence = unchecked((actorAbility + ownAdvisorAbility) * 100) / 240;
        int defense = unchecked((security + targetAdvisorAbility + rulerAbility) * 50) / 400;
        int armies = IntelligenceTruncate(MathF.Sqrt((float)count / 3f) * 15f);
        float factor = 1f - (((float)(duration - 5) / 5f) * 35f) / 100f;
        bool associated = Enumerable.Range(0, 3).Any(slot =>
            IntelligenceNeed(current.ReadForceAssociationTargetSlot(own, slot), "trial_association19C") == targetId);
        int chance = unchecked((associated ? 15 : 10) * IntelligenceTruncate((armies + intelligence - defense) * factor)) / 10;
        int trait = IntelligenceNeed(TacticRemakeCurrentTraits.PersonContribution(current, actor, 120), "trial_trait120");
        float correction = (float)trait * 100f / 100f;
        chance = unchecked(chance + (correction is > 0f and < 1f ? 1 : correction is < 0f and > -1f ? -1 : IntelligenceTruncate(correction)));
        var sampler = ReadArmyDestructionSampler(state, current) ?? throw IntelligenceMissing("trial_sampler");
        int identity = IntelligenceNeed(current.ReadPersonIdentity(actor), "trial_actor_id");
        if (TacticEffectSourceSamplerMath.Sample(100, 1, 21, identity, targetId, 0, sampler) < chance) return (true, chance, false);
        int health = IntelligenceNeed(OfficerHealthSourceAdapter.ReadByte(current, actor), "trial_health161");
        if (IntelligenceNeed(TacticRemakeCurrentTraits.PersonContribution(current, actor, 53), "trial_trait53") != 0 || health is 2 or 3)
            return (false, chance, false);
        int strength = Ability(actor, 1);
        int risk = IntelligenceTruncate((((double)(120 - strength) / 120d + (double)(duration - 5) / 10d) * 0.5d) * 50d);
        bool injured = TacticEffectSourceSamplerMath.Sample(100, 1, 21, identity, targetId, health + 1, sampler) < risk;
        return (false, chance, injured);
    }

    private int ReadIntelligenceTargetFieldArmiesCurrent(GameState state, SourceWorldContext current, int targetForce)
    {
        int count = 0;
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw IntelligenceMissing("trial_Army_registry"))
        {
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw IntelligenceMissing("trial_Army_pointer");
            var army = new ArmyUnitReference(physical);
            if (!IntelligenceNeed(current.ReadArmyUnitNativeValidity(army), "trial_Army_validity") ||
                IntelligenceNeed(grouping.IsExcluded233560(physical), "trial_Army_excluded") ||
                IntelligenceNeed(grouping.IsAtHome2C96E0(physical), "trial_Army_atHome")) continue;
            int rawHex = IntelligenceNeed(current.ReadArmyUnitRawHex2A(army), "trial_Army_Hex2A");
            var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw IntelligenceMissing("trial_Hex_pointer");
            if (!hex.PhysicalSlot.HasValue) continue;
            int rawArea = IntelligenceNeed(current.ReadHexAreaSlot(hex), "trial_Hex_Area12");
            var area = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0) ?? throw IntelligenceMissing("trial_Area_pointer");
            if (IntelligenceNeed(current.ReadAreaNativeValidity(area), "trial_Area_validity") &&
                IntelligenceNeed(current.ReadAreaCurrentForceSlot(area), "trial_Area_Force") == targetForce) count++;
        }
        return count;
    }

    private PersonnelIntelligenceAwayNarrationOutcome CaptureIntelligenceAwayOutcomeCurrent(GameState state,
        SourceWorldContext current, PersonnelIntelligenceTask task, int actor)
    {
        var own = current.ReadPersonForce(actor) ?? throw IntelligenceMissing("result_Force");
        int ownId = IntelligenceNeed(own.PhysicalSlot, "result_Force_identity");
        var target = current.ReadForceTableReference(task.TargetId <= 51 ? task.TargetId : 0) ?? throw IntelligenceMissing("result_target");
        int targetId = IntelligenceNeed(target.PhysicalSlot, "result_target_identity");
        int ownRuler = ReadIntelligenceRulerReceiverCurrent(current, own), targetRuler = ReadIntelligenceRulerReceiverCurrent(current, target);
        int district = IntelligenceActorDistrict(current, actor);
        int gain = ReadIntelligenceAwayGainCurrent(state, current, ownId, targetId, actor, task.DurationOrLevel);
        int before = ReadDistrictAdministrationIntelligenceCurrent(current, district);
        int loss = Math.Min(before, unchecked(gain * 10) / 100);
        var result = ResolveIntelligenceAwayTrialCurrent(state, current, actor, targetId, task.DurationOrLevel);
        return new(ownId, targetId, district, ownRuler, targetRuler, result.Succeeded, result.Injured, gain, loss, task.DurationOrLevel);
    }

    private GameState ApplyIntelligenceAwayBodyCurrent(GameState state, SourceWorldContext current, int actor,
        PersonnelIntelligenceAwayNarrationOutcome outcome)
    {
        int district = outcome.ActorDistrictPhysicalSlot;
        int before = ReadDistrictAdministrationIntelligenceCurrent(current, district);
        state = WriteIntelligenceBudgetCurrent(state, current, new DistrictReference(district),
            outcome.Succeeded ? unchecked(before + outcome.Gain) : unchecked(before - outcome.Loss));
        if (!outcome.Succeeded && outcome.Injured)
        {
            int health = IntelligenceNeed(OfficerHealthSourceAdapter.ReadByte(current, actor), "result_health");
            var next = new OfficerHealth(Math.Min(health is 0 or 1 ? health + 1 : health, 3));
            int id = IntelligenceNeed(current.ReadPersonIdentity(actor), "result_health_identity");
            var officer = state.Officers.GetValueOrDefault(id) ?? throw IntelligenceMissing("result_officer");
            current.SetOfficerHealth(id, next);
            state = state with { Officers = new(state.Officers) { [id] = officer with { Health = next } } };
        }
        state = new MonthlyCampaignMaintenanceSource(this, state, current,
            current.ReadOwnedCurrentListPool() ?? throw IntelligenceMissing("result_pool"))
            .ApplyRelation(new(outcome.OwnForcePhysicalSlot), new(outcome.TargetForcePhysicalSlot),
                -IntelligenceResultParameter(state, "relation-loss", 8), 1);
        state = ApplyTalentPendingMeritCurrent(state, current, actor,
            unchecked(IntelligenceResultParameter(state, "away-day-merit", 70) * outcome.Duration));
        if (ReadApplicationTutorialActive() == true && TacticRemakeCurrentBattleControl.ReadBitmapPresence(ReadTutorialControlState(state)) == true)
            state = SetTutorialLessonEventCurrent(state, 89, true);
        return state;
    }

    private GameState WriteIntelligenceBudgetCurrent(GameState state, SourceWorldContext current, int district, int amount)
    {
        var reference = current.ReadDistrictTableReference(district <= 51 ? district : 0) ?? throw IntelligenceMissing("budget_District");
        return WriteIntelligenceBudgetCurrent(state, current, district, reference, amount);
    }

    private GameState WriteIntelligenceBudgetCurrent(GameState state, SourceWorldContext current, DistrictReference reference, int amount)
        => WriteIntelligenceBudgetCurrent(state, current, IntelligenceNeed(reference.PhysicalSlot, "budget_retained_District"), reference, amount);

    private GameState WriteIntelligenceBudgetCurrent(GameState state, SourceWorldContext current, int district, DistrictReference reference, int amount)
    {
        int after = Math.Clamp(amount, 0, ReadHireAssociationBudgetCapacity(state, current, reference));
        for (int i = 0; i < 4; i++) current.SetDistrictRawByte(district, 0x24 + i, unchecked((byte)(after >> (i * 8))));
        var saved = state.NativeForceUnregisterCurrent ?? throw IntelligenceMissing("budget_saved_owner");
        return state with { NativeForceUnregisterCurrent = saved with { DistrictWindows10 = new(saved.DistrictWindows10)
            { [district] = Enumerable.Range(0x10, 28).Select(at => current.ReadDistrictRawByte(district, at)).ToArray() } } };
    }

    private static int IntelligenceActorDistrict(SourceWorldContext current, int actor)
    {
        int raw = IntelligenceNeed(current.ReadPersonDistrictSlot(actor), "actor_District_E0");
        var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0) ?? throw IntelligenceMissing("actor_District_pointer");
        return IntelligenceNeed(district.PhysicalSlot, "actor_District_identity");
    }

    private GameState ApplyIntelligenceJourneyRewardsCurrent(GameState state, SourceWorldContext current, int actor, int kind, int outcome)
    {
        if (!IntelligenceNeed(current.ReadPersonNativeValidity(actor), "reward_validity")) return state;
        var force = current.ReadPersonForce(actor) ?? throw IntelligenceMissing("reward_Force");
        int forceId = IntelligenceNeed(force.PhysicalSlot, "reward_Force_id");
        int home = IntelligenceNeed(current.ReadPersonFirstLocation(actor), "reward_E2");
        int id = IntelligenceNeed(current.ReadPersonIdentity(actor), "reward_Person10");
        if (home is >= 1 and <= 61 && forceId is >= 46 and <= 50) return state;
        if (Enumerable.Range(0, 5).Any(i => unchecked((uint)(id - (4111 + i * 20))) <= 9) &&
            home is >= 1 and <= 61 && forceId is not (>= 46 and <= 50)) return state;
        int xp = IntelligenceResultParameter(state, $"kind-{kind}." + (outcome == 2 ? "failure-growth" : "success-growth"),
            kind == 21 ? outcome == 2 ? 1000 : 3000 : outcome == 2 ? 500 : 3000);
        return ApplyPersonnelCommandGrowthCurrent(state, current, id, actor, 2, xp, 0);
    }
    private bool IntelligenceTutorialFlag(GameState state, int index) => ReadApplicationTutorialActive() switch
    { false => false, true => IntelligenceNeed(TacticRemakeCurrentBattleControl.Read(ReadTutorialControlState(state), index), "tutorial_flag"),
        _ => throw IntelligenceMissing("tutorial_service") };
    private static int IntelligenceResultParameter(GameState state, string name, int original) =>
        StrategicParameter(state, "intelligence.result." + name, original);
    private static int IntelligenceTruncate(double value) => !double.IsFinite(value) || value < int.MinValue || value >= 2147483648d ?
        int.MinValue : (int)value;
}
