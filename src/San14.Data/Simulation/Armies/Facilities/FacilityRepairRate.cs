using San14.Core;

namespace San14.Data;

public static class FacilityRepairRate
{
    public static int Read(GameState state, SourceWorldContext current, ObjectReference obj,
        ArmyUnitReference army, TacticRemakeCurrentAbilities abilities, OwnedBaseCapacityInputs panels)
    {
        if (!Need(current.ReadObjectNativeValidity(obj), "rate_object_validity") ||
            !Need(current.ReadArmyUnitNativeValidity(army), "rate_army_validity")) return 0;
        var fields = current.ReadPhysicalObject(Need(obj.PhysicalSlot, "rate_object_pointer"))
            ?? throw Missing("rate_object_fields");
        var definition = current.ReadObjectDefinitionReference(fields.RawField12 <= 30 ? fields.RawField12 : 0)
            ?? throw Missing("rate_definition_pointer");
        ushort id = Need(current.ReadArmyUnitCommanderId(army), "rate_commander");
        var person = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("rate_person_pointer");
        int physical = Need(person.PhysicalSlot, "rate_person_identity");
        if (!Need(current.ReadObjectDefinitionValidity(definition), "rate_definition_validity") ||
            !Need(current.ReadPersonNativeValidity(physical), "rate_person_validity")) return 0;
        byte category = Need(current.ReadObjectDefinitionRaw2C(definition), "rate_category");
        int politics = Need(abilities.ResolveDimension(state, id, 3,
            (force, effect) => panels.ReadPanelModifier(state, force, effect)), "rate_current_politics");
        ushort troops = Need(current.ReadArmyReturnResourceFields(army)?.Ready16, "rate_troops");
        int trait;
        var actualArmy = PersonArmyLocationSource.ReadCommanderArmy(current, physical) ?? throw Missing("rate_person_army");
        if (Need(current.ReadArmyUnitNativeValidity(actualArmy), "rate_person_army_validity"))
        {
            var values = TacticRemakeCurrentTraits.ArmyContributions(current, physical,
                current.ReadArmyReturnTraitFields(actualArmy)?.Traits64) ?? throw Missing("rate_army_trait28");
            trait = values[28];
        }
        else trait = Need(TacticRemakeCurrentTraits.PersonContribution(current, physical, 28), "rate_person_trait28");
        var force = current.ReadPersonForce(physical) ?? throw Missing("rate_person_force");
        int forceId = Need(force.PhysicalSlot, "rate_force_identity");
        int policy = !Need(current.ReadForceNativeValidity(force), "rate_force_validity") ? 0 :
            state.Forces.TryGetValue(forceId, out var owner)
                ? Need(BattleRemakeCurrentForcePolicies.ReadForceEffect(state, owner, 42, current), "rate_policy42")
                : throw Missing("rate_force_owner");
        return Calculate(state, troops, politics, category, trait, policy,
            Need(TacticRemakeCurrentBattleControl.Read(state, 51), "rate_control51"));
    }

    /// <summary>Each assignment preserves the source float32 instruction order.
    /// Products in the two bonuses wrap before conversion to float32.</summary>
    public static int Calculate(GameState state, ushort troops, int politics, byte category,
        int trait28, int policy42, bool tutorialTriple)
    {
        int P(string key) => FacilityRepairParameters.Require(state, key);
        float normalization = P(FacilityRepairParameters.TroopNormalization);
        float divisor = P(FacilityRepairParameters.PoliticsDivisor);
        float factorDivisor = P(FacilityRepairParameters.FactorDivisor);
        float tickDivisor = P(FacilityRepairParameters.TickDivisor);
        if (normalization == 0 || divisor == 0 || factorDivisor == 0 || tickDivisor == 0)
            throw Missing("rate_zero_divisor");
        float q = MathF.Sqrt((float)troops / normalization);
        float a = (float)politics * (float)P(FacilityRepairParameters.PoliticsScale);
        a = a / divisor;
        a = a + (float)P(FacilityRepairParameters.BaseFactor);
        a = a / factorDivisor;
        a = a * q;
        float fraction = (float)(category == 4 ? P(FacilityRepairParameters.TrapPercent)
            : category == 3 ? P(FacilityRepairParameters.ObstaclePercent) : 100) / 100f;
        int basis = Math.Max(Truncate((fraction * a) / tickDivisor), P(FacilityRepairParameters.MinimumIncrement));
        int traitBonus = Truncate((float)unchecked(unchecked(trait28 * P(FacilityRepairParameters.TraitScale)) * basis) / 100f);
        if (trait28 > 0) traitBonus = Math.Max(1, traitBonus);
        int policyBonus = policy42 > 0 ? Math.Max(1,
            Truncate((float)unchecked(unchecked(P(FacilityRepairParameters.Policy42Scale) * policy42) * basis) / 100f)) : 0;
        int result = unchecked(unchecked(basis + traitBonus) + policyBonus);
        return tutorialTriple ? unchecked(result * 3) : result;
    }

    private static int Truncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
        ? throw Missing("rate_float_to_int_unsupported") : (int)value;
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => FacilityRepairParameters.Missing(field);
}
