using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed record FireCommandDefense(bool Protected, int Intelligence, int AbnormalBonus);

    private FireCommandDefense ReadFireCommandDefense(GameState state, SourceWorldContext current,
        ObjectReference obj, ArmyUnitReference? occupant, int attackerPerson, int targetHex,
        NativeArmyProgressStageMoment moment, List<PeriodReportEntry> reports, Func<string, int> parameter)
    {
        //268690 checks a present Army's trait before even considering a Base.
        if (occupant is not null && Protection(Commander(occupant), occupant, null)) return new(true, 0, 0);
        var targetBase = FireCommandBase(current, obj);
        if (targetBase is not null && OccupationKnown(current.ReadBaseNativeValidity(targetBase), "fire_probability_Base_validity"))
        {
            int governor = FireCommandPerson(current, OccupationKnown(current.ReadArmyReturnBaseGovernor2E(targetBase), "fire_probability_governor"));
            int defense = 0;
            if (OccupationKnown(current.ReadPersonNativeValidity(governor), "fire_probability_governor_validity"))
            {
                if (Protection(governor, null, targetBase)) return new(true, 0, 0);
                defense = FireCommandIntelligence(state, current, governor);
            }
            var fields = current.ReadCaptureBaseThreatFields(targetBase) ?? throw OccupationMissing("fire_probability_Base_abnormal");
            bool abnormal = OccupationKnown(fields.Byte49, "fire_probability_Base49") != 0 ||
                OccupationKnown(fields.Byte4B, "fire_probability_Base4B") != 0;
            return new(false, defense, abnormal ? parameter(SimulationParameterKeys.FireCommand.AbnormalTargetBonus) : 0);
        }
        //A recognized but invalid Base skips the ordinary Army fallback.
        if (targetBase is not null || occupant is null) return new(false, 0, 0);
        int person = Commander(occupant);
        int intelligence = OccupationKnown(current.ReadPersonNativeValidity(person), "fire_probability_target_person_validity")
            ? FireCommandIntelligence(state, current, person) : 0;
        var runtime = FireCommandTargetRuntime(state, occupant);
        return new(false, intelligence, runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0
            ? parameter(SimulationParameterKeys.FireCommand.AbnormalTargetBonus) : 0);

        int Commander(ArmyUnitReference army) => FireCommandPerson(current,
            OccupationKnown(current.ReadArmyUnitCommanderId(army), "fire_probability_target_commander"));
        bool Protection(int person, ArmyUnitReference? army, BaseReference? target)
        {
            if (!OccupationKnown(current.ReadPersonNativeValidity(person), "fire_probability_protection_person")) return false;
            if (!OccupationKnown(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 25), "fire_probability_protection_presence")) return false;
            //2AB720 chooses the first matching valid personality. Its B4 and
            //the active mask are irrelevant to this probability/log path.
            int? trait = null;
            for (int i = 0; i < 9; i++)
            {
                var candidate = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, i) ?? throw OccupationMissing("fire_probability_protection_trait");
                if (!OccupationKnown(current.ReadSkillPersonalityValidity(candidate), "fire_probability_trait_validity")) continue;
                if (OccupationKnown(current.ReadPersonalityByte(candidate, 0xB6), "fire_probability_trait_first") == 25 ||
                    OccupationKnown(current.ReadPersonalityByte(candidate, 0xBA), "fire_probability_trait_second") == 25)
                { trait = OccupationKnown(candidate.PhysicalSlot, "fire_probability_trait_identity"); break; }
            }
            if (trait.HasValue)
            {
                int district = OccupationKnown(current.ReadPersonDistrictSlot(attackerPerson), "fire_probability_report_district");
                if (!state.Districts.TryGetValue(district, out var owner)) throw OccupationMissing("fire_probability_report_district_owner");
                int? unit = army is null ? null : ArmyDestructionQueue.ResolveUnitId(state,
                    OccupationKnown(army.PhysicalSlot, "fire_probability_report_army")) ?? throw OccupationMissing("fire_probability_report_unit");
                reports.Add(new(army is null ? "fire_command_base_protection" : "fire_command_army_protection", owner.ForceId, ToHexId: targetHex)
                { UnitId = unit, LocationId = target is null ? null : OccupationKnown(current.ReadBaseObjectLink(target), "fire_probability_report_Base"),
                    PersonalityId = trait, DistrictId = district, CampaignDay = ReadFireStageSampler(state, moment).Day450,
                    RuleId = "army.progress.fire-command-protection" });
            }
            return true;
        }
    }

    private int ReadFireCommandBackfireDefense(GameState state, SourceWorldContext current,
        ObjectReference obj, ArmyUnitReference? occupant)
    {
        //268449..26845B jumps straight to zero defense when Object is invalid,
        //even if an Army occupies the hex. Do not merge with probability rules.
        if (!OccupationKnown(current.ReadObjectNativeValidity(obj), "fire_backfire_object_validity")) return 0;
        var targetBase = FireCommandBase(current, obj);
        if (targetBase is not null && OccupationKnown(current.ReadBaseNativeValidity(targetBase), "fire_backfire_Base_validity"))
        {
            int governor = FireCommandPerson(current, OccupationKnown(current.ReadArmyReturnBaseGovernor2E(targetBase), "fire_backfire_governor"));
            if (OccupationKnown(current.ReadPersonNativeValidity(governor), "fire_backfire_governor_validity"))
                return FireCommandIntelligence(state, current, governor);
        }
        if (occupant is null) return 0;
        int person = FireCommandPerson(current, OccupationKnown(current.ReadArmyUnitCommanderId(occupant), "fire_backfire_commander"));
        if (!OccupationKnown(current.ReadPersonNativeValidity(person), "fire_backfire_commander_validity")) return 0;
        var runtime = FireCommandTargetRuntime(state, occupant);
        return runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0
            ? 0 : FireCommandIntelligence(state, current, person);
    }

    private static BaseReference? FireCommandBase(SourceWorldContext current, ObjectReference obj)
    {
        if (!OccupationKnown(current.ReadObjectNativeValidity(obj), "fire_target_object_validity")) return null;
        int physical = OccupationKnown(obj.PhysicalSlot, "fire_target_object_physical");
        return physical is >= 1 and <= 51 ? current.ReadCityTableReference(physical) ?? throw OccupationMissing("fire_target_City") :
            physical is >= 52 and <= 61 ? current.ReadGateTableReference(physical - 51) ?? throw OccupationMissing("fire_target_Gate") : null;
    }
    private static int FireCommandPerson(SourceWorldContext current, int identity) =>
        OccupationKnown(current.ReadPersonLookupReference(identity <= 6000 ? identity : 0)?.PhysicalSlot, "fire_command_person_pointer");
    private int FireCommandIntelligence(GameState state, SourceWorldContext current, int person) =>
        (new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language))
            .Resolve(state, OccupationKnown(current.ReadPersonIdentity(person), "fire_command_person_identity")) ?? throw OccupationMissing("fire_command_intelligence")).Intelligence;
    private static NativeArmyTacticRuntime FireCommandTargetRuntime(GameState state, ArmyUnitReference army)
    {
        int physical = OccupationKnown(army.PhysicalSlot, "fire_target_army_physical");
        var slot = state.ReadArmyLifecycle()?.ReceiverSlots()[physical] ?? throw OccupationMissing("fire_target_receiver");
        var unit = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
        return unit?.NativeTacticRuntime ?? throw OccupationMissing("fire_target_runtime");
    }

    private static GameState ApplyFireCommandRelations(GameState state, SourceWorldContext current,
        int first, int second, Func<string, int> parameter) =>
        ForceRelationChangeCurrent.Apply(state, current, first, second,
            () => parameter(SimulationParameterKeys.FireCommand.RelationAdjustment), parameter);
}
