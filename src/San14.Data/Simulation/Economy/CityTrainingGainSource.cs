using San14.Core;

namespace San14.Data;

//269320/2693F0 current numerical query, shared by the page and actual Turn.
internal static class CityTrainingGainSource
{
    internal static int Read(GameState state, SourceWorldContext current, BaseReference original,
        Func<BaseReference, int> fee, Func<int, int, int> ability, Func<ForceReference, int, int> policy)
    {
        var associated = CurrentGateMapQueries.FindCity(original, current);
        int id = K(associated.PhysicalSlot, "training_associated_city");
        ushort raw = K(current.ReadAssignmentCityCC(id), "training_associated_trainer");
        int trainer = PersonAreaAppointmentSource.Resolve(current, raw);
        if (!K(current.ReadPersonNativeValidity(trainer), "training_trainer_validity")) return 0;
        int cost = fee(associated);
        if (cost > Gold(associated)) return 0;
        if (raw == 0) return 0;
        int morale = Morale(original), upper = MoraleCap(original);
        if (morale >= upper) return 0;
        if (Troops(original) == 0) return 0;
        return Inner(original, trainer);
        int Inner(BaseReference city, int trainer)
    {
        int initialMorale = Morale(city); if (initialMorale >= 200) return 0;
        int troops = Troops(city); if (troops == 0) return 0;
        ushort flags = K(OfficerBehaviorSourceAdapter.EncodeFlags(current.ReadPersonBehavior(trainer)), "training_flags");
        bool half = (flags & 1) != 0;
        if (!half)
        {
            half = K(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, trainer), "training_army_validity");
            if (!half)
            {
                var linked = PersonLinkedEntityLookup.Find(trainer, current);
                half = K(current.ReadLinkedEntityNativeValidity(linked), "training_linked_validity");
            }
        }
        int leadership = ability(trainer, 0); if (half) leadership /= 2;
        int strength = ability(trainer, 1); if (half) strength /= 2;
        int baseParameter = Parameter("trainingBaseGain");
        int sqrtMorale = Morale(city);
        int sqrtScale = Parameter("trainingMoraleSqrtScale");
        float sqrt = MathF.Sqrt((float)unchecked(sqrtMorale * sqrtScale));
        int divisor = Parameter("trainingTroopsDivisor");
        float troopPart = (float)troops / (float)divisor;
        float firstSum = (float)(sqrt + troopPart);
        int offset = Parameter("trainingDenominatorOffset");
        int rawDenominator = Cvtt((float)(firstSum + (float)offset));
        _ = MoraleCap(city);
        int traitScale = Parameter("trainingTraitScale");
        int denominator = Math.Max(1, rawDenominator);
        int basis = unchecked(baseParameter + unchecked(unchecked(leadership + strength) * 2) / denominator);
        int trait = K(TacticRemakeCurrentTraits.CommanderEffectMaximum(current, trainer, 16), "training_trait16");
        int traitGain = unchecked(unchecked(trait * traitScale) * basis) / 100;
        var force = current.ReadPersonForce(trainer) ?? throw Missing("training_person_force");
        int policyValue = policy(force, 31);
        int scale = Parameter("trainingPolicyScale");
        int policyGain = unchecked(unchecked(scale * policyValue) * basis) / 100;
        return unchecked(unchecked(policyGain + traitGain) + basis);
    }
        int Gold(BaseReference city) => K(current.ReadBaseResource(city, BaseResourceKind.Gold), "gold");
        int Troops(BaseReference city) => K(current.ReadBaseResource(city, BaseResourceKind.Troops), "troops");
        int Morale(BaseReference city) => K(current.ReadBaseMorale44(city), "morale");
        int MoraleCap(BaseReference city) => BasePostLoad.ReadMoraleUpper(current.ReadForceTableReference(
            K(current.ReadBaseForceSlot(city), "Force") is int raw && raw < 52 ? raw : 0) ?? throw Missing("Force_pointer"), current);
        int Parameter(string name) => K(CityDomesticParameters.Read(state, name), "parameter." + name);
    }
    private static T K<T>(T? value,string field) where T:struct => value ?? throw Missing(field);
    private static int Cvtt(float value) => value >= 2147483648f || value < -2147483648f || float.IsNaN(value) ? int.MinValue : (int)value;
    private static InvalidDataException Missing(string field) => new("current.CityTraining." + field);
}
