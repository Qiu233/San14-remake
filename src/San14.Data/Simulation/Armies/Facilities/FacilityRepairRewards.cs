using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ApplyFacilityRepairRewards(GameState state, SourceWorldContext current,
        ArmyUnitReference army, ObjectReference obj, int person, FacilityRepairNumberResult numbers, int raw)
    {
        int id = RepairNeed(current.ReadPersonIdentity(person), "reward_person_identity");
        if (numbers.ReachedCapacity && RepairGrowthEligible(state, current, person, obj))
        {
            int request = unchecked(FacilityRepairParameters.Require(state, FacilityRepairParameters.PoliticsGrowthPercent) * raw) / 100;
            var growth = ApplyPersonGrowthDuringArmyOperation(state, id, 3, request, 0);
            if (growth.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved) throw RepairMissing("reward_politics_growth");
            state = growth.State;
        }
        string divisorKey = numbers.Category == 3 ? FacilityRepairParameters.ObstacleMeritDivisor
            : numbers.Category == 4 ? FacilityRepairParameters.TrapMeritDivisor : FacilityRepairParameters.OtherMeritDivisor;
        int divisor = FacilityRepairParameters.Require(state, divisorKey);
        int product = unchecked(unchecked(FacilityRepairParameters.Require(state, FacilityRepairParameters.MeritScale) * numbers.ClippedIncrement) * 10);
        if (divisor == 0 || product == int.MinValue && divisor == -1) throw RepairMissing("reward_merit_division_unsupported");
        var merit = ApplyPendingMeritDuringArmyOperation(state, id, product / divisor);
        if (merit.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved) throw RepairMissing("reward_pending_merit");
        return merit.State;
    }

    private static bool RepairGrowthEligible(GameState state, SourceWorldContext current, int person, ObjectReference obj)
    {
        if (!RepairNeed(current.ReadPersonNativeValidity(person), "growth_person_validity") ||
            !RepairNeed(current.ReadObjectNativeValidity(obj), "growth_object_validity")) return false;
        ushort home = RepairNeed(current.ReadPersonFirstLocation(person), "growth_person_home");
        if (home is >= 1 and <= 61)
        {
            var force = current.ReadPersonForce(person) ?? throw RepairMissing("growth_person_force");
            int forceId = RepairNeed(force.PhysicalSlot, "growth_person_force_identity");
            if (forceId is >= 46 and <= 50) return false;
        }
        int id = RepairNeed(current.ReadPersonIdentity(person), "growth_person_identity");
        if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id) && home is >= 1 and <= 61) return false;
        var army = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw RepairMissing("growth_person_army");
        if (!RepairNeed(current.ReadArmyUnitNativeValidity(army), "growth_person_army_validity")) return false;
        var image = NotificationArmy(state, RepairNeed(army.PhysicalSlot, "growth_army_physical"));
        if (RepairNeed(ArmyConfigurationSourceAdapter.ReadAction(state, image), "growth_army_action") != 9) return false;
        return RepairNeed(FacilityActivationCurrent.ReadEffectsEnabled(current, obj), "growth_object_enabled");
    }
}
