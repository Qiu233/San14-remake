using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState ApplyNormalTurnTrainingCurrent(GameState state, SourceWorldContext current, BaseReference city)
    {
        if (EconomyK(current.ReadBaseObjectLink(city), "training_Base4E") >= 52) return state;
        int raw = EconomyK(current.ReadAssignmentCityCC(EconomyK(city.PhysicalSlot, "training_City")), "training_CC");
        //266CD0 precedes rawCC0, as in the original301170 caller.
        int fee = ReadHireDomesticFeeCurrent(state, current, city, recruit: false);
        if (fee > EconomyK(current.ReadBaseResource(city, BaseResourceKind.Gold), "training_Gold") || raw == 0) return state;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        int gain = Gain(city);
        state = SetNormalTurnMoraleCurrent(state, current, city,
            unchecked(EconomyK(current.ReadBaseMorale44(city), "training_City_morale") + gain));
        int person = PersonAreaAppointmentSource.Resolve(current, EconomyK(current.ReadAssignmentCityCC(EconomyK(city.PhysicalSlot, "trainer_City")), "trainer_CC"));
        if (gain > 0 && DomesticGrowthEligible(current, person))
        {
            bool busy = DomesticRewardBusy(current, person);
            state = ApplyNormalTurnAbilityExperienceCurrent(state, current, person, 0, busy ? 10 : 40);
            state = ApplyNormalTurnAbilityExperienceCurrent(state, current, person, 1, busy ? 5 : 18);
            state = ApplyTalentPendingMeritCurrent(state, current, person, 0);
        }
        if (EconomyK(current.ReadPersonNativeValidity(person), "training_trainer_validity"))
            state = ApplyTalentPendingMeritCurrent(state, current, person, unchecked((DomesticRewardBusy(current, person) ? 27 : 54) * 10));
        bool any = gain > 0;
        var gates = current.CreateNearbyTemporary();
        try
        {
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw EconomyMissing("training_GateA0"))
            {
                var gate = entry ?? throw EconomyMissing("training_Gate_pointer");
                if (CurrentGateMapQueries.FindCity(gate, current) != city) continue;
                var node = current.AllocateBaseRegistryNode(new(gates.Identity)) ?? throw EconomyMissing("training_Gate_node");
                if (node.Identity.HasValue) current.WriteBaseRegistryNodePayload(node, gate);
            }
            var cursor = current.ReadRegistryHead(new(gates.Identity)) ?? throw EconomyMissing("training_Gate_head");
            while (cursor.Identity.HasValue)
            {
                var gate = current.ReadBaseRegistryNodePayload(cursor) ?? throw EconomyMissing("training_Gate_payload");
                int amount = Gain(gate);
                state = SetNormalTurnMoraleCurrent(state, current, gate,
                    unchecked(EconomyK(current.ReadBaseMorale44(gate), "training_Gate_morale") + amount));
                any |= amount > 0;
                cursor = current.ReadRegistryNodeNext(cursor) ?? throw EconomyMissing("training_Gate_next");
            }
        }
        finally { current.ClearNearbyTemporary(gates, true); }
        return any ? AddNormalTurnBaseResourceCurrent(state, current, city, BaseResourceKind.Gold, unchecked(-fee), recordExpense: true) : state;

        int Gain(BaseReference target) => CityTrainingGainSource.Read(state, current, target,
            receiver => ReadHireDomesticFeeCurrent(state, current, receiver, recruit: false),
            (physical, dimension) => EconomyK(abilities.ResolveDimension(state,
                EconomyK(current.ReadPersonIdentity(physical), "training_Person_identity"), dimension,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "training_effective_ability"),
            (force, effect) => !EconomyK(current.ReadForceNativeValidity(force), "training_Force_validity") ? 0 :
                ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, effect, true).Value);
    }

    private static GameState SetNormalTurnMoraleCurrent(GameState state, SourceWorldContext current, BaseReference receiver, int requested)
    {
        var force = current.ReadForceTableReference(EconomyK(current.ReadBaseForceSlot(receiver), "morale_Force") is int raw && raw < 52 ? raw : 0)
            ?? throw EconomyMissing("morale_Force_pointer");
        if (EconomyK(current.ReadForcePhysicalSlot(force), "morale_Force_identity") is >= 46 and <= 50) return state;
        byte after = (byte)Math.Max(0, Math.Min(requested, BasePostLoad.ReadMoraleUpper(force, current)));
        current.SetBaseMorale44(receiver, after);
        int location = EconomyK(current.ReadBaseObjectLink(receiver), "morale_Base4E");
        if (receiver.Kind == BaseKind.City && state.Cities.TryGetValue(location, out var city))
            return state with { Cities = new(state.Cities) { [location] = city with { Morale = after, NativeReturnRuntime = (city.NativeReturnRuntime ?? throw EconomyMissing("morale_City_runtime")) with { Morale44 = after } } } };
        if (receiver.Kind == BaseKind.Gate && state.Gates.TryGetValue(location, out var gate))
            return state with { Gates = new(state.Gates) { [location] = gate with { NativeCombatRuntime = (gate.NativeCombatRuntime ?? throw EconomyMissing("morale_Gate_runtime")) with { Morale44 = after } } } };
        throw EconomyMissing("morale_domain_receiver");
    }
    private static bool DomesticRewardBusy(SourceWorldContext current, int person)
        => (EconomyK(current.ReadArmyReturnPersonFlags196(person), "reward_Person15E") & 1) != 0 || ReadHireAssociationActorBusy(current, person);
    private static bool DomesticGrowthEligible(SourceWorldContext current, int person)
    {
        if (!EconomyK(current.ReadPersonNativeValidity(person), "reward_Person_validity")) return false;
        if (EconomyK(current.ReadPersonFirstLocation(person), "reward_PersonE2") is not (>= 1 and <= 61)) return true;
        int force = EconomyK(current.ReadForcePhysicalSlot(current.ReadPersonForce(person) ?? throw EconomyMissing("reward_Force")), "reward_Force_identity");
        return force is not (>= 46 and <= 50) && !BattleRemakeCurrentRegionFactor.IsNativeBandPerson(EconomyK(current.ReadPersonIdentity(person), "reward_Person_identity"));
    }
    private GameState ApplyNormalTurnAbilityExperienceCurrent(GameState state, SourceWorldContext current, int person, int dimension, int requested)
    {
        var result = _growthSource.TryApplyWithinArmyOperation(state, current, _growthQueries,
            EconomyK(current.ReadPersonIdentity(person), "experience_Person_identity"), dimension, requested, 0);
        return result.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved ? throw EconomyMissing("experience_current_inputs") : result.State;
    }
}
