using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The administration tail of2DC710(City,gold0,quote1). Its gold output
    //is unused by the food caller, but all reached queries and shared-pool
    //temporaries still run. No stock, durability, morale or reward is posted.
    internal void ReadCityFoodQuoteAdministrationTailCurrent(GameState state, SourceWorldContext current,
        BaseReference city, List<ForcePolicyScratchReceipt> policies)
    {
        var savedPolicy = current.BaseForcePolicyEffectReader;
        //Base268230's durability and2690D0's morale-limit calls really visit
        //41D690. Reuse their existing numerical queries, recording each actual
        //scratch once rather than issuing another policy call afterward.
        current.BaseForcePolicyEffectReader = (force, effect) => Policy(force, effect);
        try
        {
            if (FoodTailNeed(current.ReadBaseObjectLink(city), "City4E") < 52)
            {
                ReadRepair(city);
                var gates = current.CreateNearbyTemporary();
                try
                {
                    FillMappedGates(gates);
                    foreach (var gate in ReadGates(gates)) ReadRepair(gate);
                    //param2=0 still constructs the recruiter packet and visits
                    //the City-area iterator. Its scalar recruitment callback
                    //is short-circuited only after the ownership comparison.
                    _ = FoodTailNeed(current.ReadAssignmentCityCA(FoodTailNeed(city.PhysicalSlot, "recruiter_City")), "CityCA");
                    int physicalCity = FoodTailNeed(city.PhysicalSlot, "recruiter_City_identity");
                    if (physicalCity is >= 1 and <= 51)
                        foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw FoodTailMissing("recruiter_AreaB0"))
                        {
                            var area = entry ?? throw FoodTailMissing("recruiter_Area_pointer");
                            if (FoodTailNeed(current.ReadAreaCitySlot(area), "recruiter_Area35") != physicalCity) continue;
                            int force = FoodTailNeed(current.ReadBaseForceSlot(city), "recruiter_City_Force");
                            _ = FoodTailNeed(current.ReadAreaCurrentForceSlot(area), "recruiter_Area_Force") == force;
                        }
                }
                finally { current.ClearNearbyTemporary(gates, true); }
            }
            _ = ReadHireAreaAdministrationGoldExpenseCurrent(state, current, city, policies);
            if (ReadTraining() && FoodTailNeed(current.ReadBaseObjectLink(city), "training_tail_City4E") < 52)
            {
                //The enclosing2DC710 repeats the CC lookup and266CD0 after
                //successful301170. This is a distinct policy38 scratch.
                ushort trainer = FoodTailNeed(current.ReadAssignmentCityCC(FoodTailNeed(city.PhysicalSlot, "training_tail_City")), "training_tail_CC");
                _ = trainer <= 6000 ? current.ReadPersonLookupReference(trainer) ?? throw FoodTailMissing("training_tail_Person_pointer")
                    : current.ReadTransferPersonTableReference(0) ?? throw FoodTailMissing("training_tail_Person0_pointer");
                _ = ReadHireDomesticFeeCurrent(state, current, city, recruit: false, policies);
            }
        }
        finally { current.BaseForcePolicyEffectReader = savedPolicy; }

        int Policy(ForceReference force, int effect)
        {
            var query = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, effect, true);
            policies.Add(query); return query.Value;
        }
        void ReadRepair(BaseReference target)
        {
            if (!FoodTailNeed(current.ReadBaseNativeValidity(target), "repair_Base_validity")) return;
            int fee = state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(
                SimulationParameterKeys.CityFoodExpense.RepairGoldFee, out int known) == true
                ? known : throw FoodTailMissing("repair_fee_parameter");
            if (FoodTailNeed(current.ReadBaseResource(target, BaseResourceKind.Gold), "repair_Gold") < fee) return;
            int capacity = BasePostLoad.ReadCapacities(target, current).Field46;
            int wall = BasePostLoad.ReadLinkedField14(target, current);
            if (capacity <= wall) return;
            int governor = FoodTailNeed(current.ReadArmyReturnBaseGovernor2E(target), "repair_Governor2E");
            if (governor is < 1 or > 6000) return;
            int location = FoodTailNeed(current.ReadBaseObjectLink(target), "repair_Base4E");
            bool? nearby = target.Kind == BaseKind.City
                ? target.PhysicalSlot == 0 ? state.UnassignedCityHasNearbyEnemyArmy : state.Cities.GetValueOrDefault(location)?.HasNearbyEnemyArmy
                : target.PhysicalSlot == 0 ? state.UnassignedGateHasNearbyEnemyArmy : state.Gates.GetValueOrDefault(location)?.HasNearbyEnemyArmy;
            if (FoodTailNeed(nearby, "repair_Base67_nearby_enemy")) return;
            var footprint = current.ReadBaseHexListHandle(target) ?? throw FoodTailMissing("repair_Base78");
            var node = current.ReadHexListHead(footprint) ?? throw FoodTailMissing("repair_footprint_head");
            while (node.Identity.HasValue)
            {
                var hex = current.ReadHexListNodePayload(node) ?? throw FoodTailMissing("repair_footprint_Hex");
                int physical = FoodTailNeed(hex.PhysicalSlot, "repair_footprint_Hex_identity");
                if (FoodTailNeed(current.ReadCaptureHexWord16(physical), "repair_footprint_Hex16") != 0) return;
                node = current.ReadHexListNodeNext(node) ?? throw FoodTailMissing("repair_footprint_next");
            }
            var obj = current.ReadObjectTableReference(location <= 3000 ? location : 0) ?? throw FoodTailMissing("repair_Object_pointer");
            //The native predicate returns1 even for an invalid linked object;
            //quote1 suppresses only the following2B3FA0 and stock/wall writers.
            _ = FoodTailNeed(current.ReadObjectNativeValidity(obj), "repair_Object_validity");
        }
        bool ReadTraining()
        {
            if (FoodTailNeed(current.ReadBaseObjectLink(city), "training_City4E") >= 52) return false;
            int physical = FoodTailNeed(city.PhysicalSlot, "training_City_identity");
            ushort trainer = FoodTailNeed(current.ReadAssignmentCityCC(physical), "training_CityCC");
            _ = trainer <= 6000 ? current.ReadPersonLookupReference(trainer) ?? throw FoodTailMissing("training_Person_pointer")
                : current.ReadTransferPersonTableReference(0) ?? throw FoodTailMissing("training_Person0_pointer");
            int fee = ReadHireDomesticFeeCurrent(state, current, city, recruit: false, policies);
            if (fee > FoodTailNeed(current.ReadBaseResource(city, BaseResourceKind.Gold), "training_Gold") || trainer == 0) return false;
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            _ = Gain(city);
            var gates = current.CreateNearbyTemporary();
            try
            {
                FillMappedGates(gates);
                foreach (var gate in ReadGates(gates)) _ = Gain(gate);
            }
            finally { current.ClearNearbyTemporary(gates, true); }
            return true;

            int Gain(BaseReference target) => CityTrainingGainSource.Read(state, current, target,
                receiver => ReadHireDomesticFeeCurrent(state, current, receiver, recruit: false, policies),
                (person, dimension) => FoodTailNeed(abilities.ResolveDimension(state,
                    FoodTailNeed(current.ReadPersonIdentity(person), "training_ability_Person_ID"), dimension,
                    (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "training_effective_ability"),
                Policy);
        }
        void FillMappedGates(HexListHandle list)
        {
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw FoodTailMissing("mapped_GateA0"))
            {
                var gate = entry ?? throw FoodTailMissing("mapped_Gate_pointer");
                if (CurrentGateMapQueries.FindCity(gate, current) != city) continue;
                var node = current.AllocateBaseRegistryNode(new(list.Identity)) ?? throw FoodTailMissing("mapped_Gate_node");
                if (node.Identity.HasValue) current.WriteBaseRegistryNodePayload(node, gate);
            }
        }
        IEnumerable<BaseReference> ReadGates(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw FoodTailMissing("mapped_Gate_head");
            while (node.Identity.HasValue)
            {
                yield return current.ReadBaseRegistryNodePayload(node) ?? throw FoodTailMissing("mapped_Gate_payload");
                node = current.ReadRegistryNodeNext(node) ?? throw FoodTailMissing("mapped_Gate_next");
            }
        }
    }
    private static T FoodTailNeed<T>(T? value, string field) where T : struct => value ?? throw FoodTailMissing(field);
    private static InvalidDataException FoodTailMissing(string field) => new("city.food_quote.tail." + field);
}
