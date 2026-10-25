using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //One binding at the refresh boundary, before the Force loop. The route
    //capture deliberately omits these separate saved administration owners.
    private void BindPersonnelHireGenerationEconomyCurrent(GameState state, SourceWorldContext current)
    {
        //The route capture carries District identity, not the saved treasury
        //window consumed by26BB30. Restore this existing owner once.
        ForceUnregisterInputs.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        if (!_baseCapacityInputs.BindCurrent(state, current))
            throw HireEconomyMissing("Base_capacity_current_owner");
    }

    /// <summary>The four consumed341B40 outputs. Quotes borrow the generation
    /// world's current inputs; no stock, officer or random state is posted.</summary>
    internal PersonnelHireForceEconomy ReadPersonnelHireForceEconomyCurrent(
        GameState state, SourceWorldContext current, int forceId)
    {
        var date = state.NativeMapSeason?.ThirdDate ?? throw HireEconomyMissing("World34_date");
        var preview = new AreaAdministrationPreviewSource(state, current, _scenario,
            Seed.Language, _baseCapacityInputs, true);
        var abilities = CreateArmyDestructionAbilities(current);
        var areas = current.ReadRegisteredAreaReferences() ?? throw HireEconomyMissing("Area_registryB0");
        int gold = 0, food = 0, expense = 0, recruitment = 0;
        foreach (var entry in current.ReadRegisteredBases() ?? throw HireEconomyMissing("Base_registry80"))
        {
            var city = entry ?? throw HireEconomyMissing("City_pointer");
            if (city.Kind != BaseKind.City || HireEconomyK(current.ReadBaseForceSlot(city), "City_Force") != forceId) continue;
            int link = HireEconomyK(current.ReadBaseObjectLink(city), "City4E");
            if (link >= 52) continue;
            int cityGold = 0, cityFood, cityTroops = 0;
            if (current.ReadSpecialBaseCapacity(city))
            {
                //3417B0's special-city branches do not enter22D9E0.
                cityGold = cityFood = cityTroops = int.MaxValue;
            }
            else
            {
                foreach (var item in areas)
                {
                    var area = item ?? throw HireEconomyMissing("Area_pointer");
                    if (HireEconomyK(current.ReadAreaCitySlot(area), "Area35") != city.PhysicalSlot ||
                        HireEconomyK(current.ReadAreaCurrentForceSlot(area), "Area_Force") != forceId) continue;
                    int physical = HireEconomyK(area.PhysicalSlot, "Area_identity");
                    cityGold = unchecked(cityGold + ReadHireAreaGoldIncomeCurrent(state, current,
                        area, city, abilities, AssignmentCount));
                    //3417B0 offset16 uses2BD730(CityCA,2): remaining-month
                    //recruitment, independently of Army food consumption.
                    cityTroops = unchecked(cityTroops + HireEconomyValue(preview.Soldiers(physical)));
                }
                int percent = ReadHireGoldLandmarkPercent(state, current, city);
                if (percent > 0 && cityGold > 0)
                {
                    float basis = cityGold;
                    cityGold = HireEconomyTruncate((percent / 100f) * basis + basis);
                }
                cityFood = CityFoodIncomeSource.Read(state, current, city, date, abilities, _landmarkDefinitions);
            }
            gold = unchecked(gold + cityGold);
            food = unchecked(food + cityFood);
            recruitment = unchecked(recruitment + cityTroops);
            expense = unchecked(expense + ReadHireCityGoldExpense(state, current, city, date, areas, preview));
        }
        return new(gold, food, expense, recruitment);

        int AssignmentCount(int physical)
        {
            int count = (current.ReadAssignmentPersonAreaVector(physical)
                ?? throw HireEconomyMissing("Person_task_vector")).ActiveCount1E8;
            //The quote's ordinary one-task result is independent of the
            //unowned full-effort switch. A reached multi-task branch is not.
            if (count > 1) throw HireEconomyMissing("Person_multiple_tasks_full_effort_current");
            return count;
        }
    }

    //2BD250, the actual Area income scalar used by22D9E0. No draft selection
    //or UI preview context supplies the appointed officer or its task count.
    private int ReadHireAreaGoldIncomeCurrent(GameState state, SourceWorldContext current,
        AreaReference area, BaseReference city, IEffectiveOfficerAbilities abilities, Func<int, int> assignmentCount,
        int? personOverride = null)
    {
        if (!HireEconomyK(current.ReadBaseNativeValidity(city), "gold_City_validity")) return 0;
        int physical = HireEconomyK(area.PhysicalSlot, "gold_Area_identity");
        int forceId = HireEconomyK(current.ReadBaseForceSlot(city), "gold_City_Force");
        if (HireEconomyK(current.ReadAreaCurrentForceSlot(area), "gold_Area_Force") != forceId) return 0;
        if (current.ReadSpecialBaseCapacity(city))
            return unchecked(BasePostLoad.ReadGoldCapacity(city, current)
                - HireEconomyK(current.ReadBaseResource(city, BaseResourceKind.Gold), "gold_special_City_stock"));
        var force = current.ReadForceTableReference(forceId <= 51 ? forceId : 0)
            ?? throw HireEconomyMissing("gold_Force_pointer");
        int person = personOverride ?? PersonAreaAppointmentSource.Resolve(current,
            HireEconomyK(current.ReadAssignmentArea36(physical), "gold_Area36"));
        var owner = state.Areas.GetValueOrDefault(physical) ?? throw HireEconomyMissing("gold_Area_owner");
        var basis = owner.EconomySource ?? throw HireEconomyMissing("gold_Area_fixed_income");
        ushort rawBasis = U16(basis.RawField44, "gold_Area44");
        ushort population = owner.NativeNormalAftermath?.Quantity52 ?? throw HireEconomyMissing("gold_Area52");
        bool valid = HireEconomyK(current.ReadPersonNativeValidity(person), "gold_Person_validity");
        int task = 0, intelligence = 0;
        if (valid)
        {
            int full = HireEconomyK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7E), "gold_Person_trait126");
            task = full > 0 ? 100 : assignmentCount(person) is 0 or 1 ? 100
                : throw HireEconomyMissing("gold_Person_multiple_task_effort");
            int identity = HireEconomyK(current.ReadPersonIdentity(person), "gold_Person_identity");
            intelligence = abilities is TacticRemakeCurrentAbilities actual
                ? HireEconomyK(actual.ResolveDimension(state, identity, 2), "gold_Person_Intelligence")
                : abilities.Resolve(state, identity)?.Intelligence ?? throw HireEconomyMissing("gold_Person_Intelligence");
        }
        ushort progress = U16(owner.Development?.Commerce ?? throw HireEconomyMissing("gold_Commerce"), "gold_Area3C");
        byte security = owner.Security is >= 0 and <= 255 ? (byte)owner.Security : throw HireEconomyMissing("gold_Area54");
        byte bonus = HireEconomyK(current.ReadAssignmentArea6D(physical), "gold_Area6D");
        var statistics = state.RegionStatistics?.Entries.GetValueOrDefault(physical)
            ?? throw HireEconomyMissing("gold_Area_statistics");
        ushort terrain = U16(statistics.TerrainCellCount, "gold_Area76");
        ushort owned = statistics.IsOwnedCountCurrent ? U16(statistics.OwnedCellCount, "gold_Area68")
            : throw HireEconomyMissing("gold_Area68_current");
        int trait = HireEconomyK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x24), "gold_Person_trait24");
        bool forceValid = HireEconomyK(current.ReadForceNativeValidity(force), "gold_Force_validity");
        int panel = forceValid ? HireEconomyK(_baseCapacityInputs.ReadPanelModifier(state,
            HireEconomyK(force.PhysicalSlot, "gold_Force_identity"), 0x35), "gold_Force_panel35") : 0;
        int selector = HireEconomyK(BattleRemakeCurrentSettings.ReadSelectorE58(state), "gold_Settings_selector");
        int difficulty = selector != 0 ? HireEconomyK(BattleRemakeCurrentSettings.ReadInt32(state, 0x28), "gold_Difficulty") : 0;
        var player = current.ReadCurrentPlayerForce() ?? throw HireEconomyMissing("gold_Player_Force_pointer");
        int playerId = HireEconomyK(player.PhysicalSlot, "gold_Player_Force_identity");
        bool own = playerId != 0 && playerId == HireEconomyK(force.PhysicalSlot, "gold_Force_identity");
        int rate = selector == 0 ? 100 : own ? difficulty switch { 0 => 200, 1 => 150, _ => 100 }
            : difficulty == 3 ? 150 : difficulty >= 4 ? 200 : 100;
        int? advanced = selector == 0 ? HireEconomyK(BattleRemakeCurrentSettings.ReadAdvancedParameter(state,
            own ? 0x1F : 0x22), "gold_Advanced_percent") : null;
        return CeAreaIncomeMath.CalculateGold(new(rawBasis, population, security, progress, bonus,
            owned, terrain, intelligence, task, valid, trait, panel, rate, advanced, forceValid)).Gold;

        static ushort U16(int value, string field) => value is >= 0 and <= 65535 ? (ushort)value : throw HireEconomyMissing(field);
    }

    private int ReadHireCityGoldExpense(GameState state, SourceWorldContext current,
        BaseReference city, NativeMapSeasonDate date, IReadOnlyList<AreaReference?> areas,
        AreaAdministrationPreviewSource recruitment)
    {
        if (current.ReadSpecialBaseCapacity(city)) return int.MaxValue;
        int expense = date.Day is >= 21 and <= 30 ? ReadHireSalaryQuote(state, current, city) : 0;
        ReadRepair(city);
        foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw HireEconomyMissing("Gate_registryA0"))
        {
            var gate = entry ?? throw HireEconomyMissing("Gate_pointer");
            if (CurrentGateMapQueries.FindCity(gate, current) != city) continue;
            ReadRepair(gate);
        }
        int force = HireEconomyK(current.ReadBaseForceSlot(city), "expense_City_Force");
        foreach (var item in areas)
        {
            var area = item ?? throw HireEconomyMissing("expense_Area_pointer");
            if (HireEconomyK(current.ReadAreaCitySlot(area), "expense_Area35") != city.PhysicalSlot ||
                HireEconomyK(current.ReadAreaCurrentForceSlot(area), "expense_Area_Force") != force) continue;
            int physical = HireEconomyK(area.PhysicalSlot, "expense_Area_identity");
            if (date.Day is >= 21 and <= 30 && HireEconomyValue(recruitment.Soldiers(physical)) != 0)
                expense = unchecked(expense + ReadHireDomesticFeeCurrent(state, current, city, recruit: true));
        }
        expense = unchecked(expense + ReadHireAreaAdministrationGoldExpenseCurrent(state, current, city));
        //301170(quote1) returns1 from the fee/rawCC gates independently of
        //269320's gain. Its training and reward writers require quote0.
        int trainingFee = ReadHireDomesticFeeCurrent(state, current, city, recruit: false);
        ushort trainer = HireEconomyK(current.ReadAssignmentCityCC(HireEconomyK(city.PhysicalSlot, "City_identity")), "CityCC");
        if (trainingFee <= HireEconomyK(current.ReadBaseResource(city, BaseResourceKind.Gold), "training_City_Gold") && trainer != 0)
            expense = unchecked(expense + ReadHireDomesticFeeCurrent(state, current, city, recruit: false));

        //34CC40 adds the three-slot association quote only at the ruler Home.
        var forcePointer = current.ReadForceTableReference(force <= 51 ? force : 0)
            ?? throw HireEconomyMissing("expense_Force_pointer");
        if (HireEconomyK(current.ReadForceNativeValidity(forcePointer), "expense_Force_validity"))
        {
            ushort ruler = HireEconomyK(current.ReadForceRecord(force)?.RawRuler10, "Force10");
            var lookup = current.ReadPersonLookupReference(ruler <= 6000 ? ruler : 0)
                ?? throw HireEconomyMissing("ruler_pointer");
            int person = HireEconomyK(lookup.PhysicalSlot, "ruler_identity");
            int home = HireEconomyK(current.ReadPersonFirstLocation(person), "rulerE2");
            if (home == HireEconomyK(current.ReadBaseObjectLink(city), "capital4E"))
                expense = unchecked(expense + ReadHireCapitalAssociationGoldExpenseCurrent(state, current, forcePointer));
        }
        return expense;

        void ReadRepair(BaseReference target)
        {
            if (!HireEconomyK(current.ReadBaseNativeValidity(target), "repair_Base_validity")) return;
            if (state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(
                SimulationParameterKeys.CityFoodExpense.RepairGoldFee, out int fee) != true)
                throw HireEconomyMissing("repair_fee_parameter");
            if (HireEconomyK(current.ReadBaseResource(target, BaseResourceKind.Gold), "repair_Gold") < fee) return;
            if (BasePostLoad.ReadCapacities(target, current).Field46 <= BasePostLoad.ReadLinkedField14(target, current)) return;
            ushort governor = HireEconomyK(current.ReadArmyReturnBaseGovernor2E(target), "repair_Governor2E");
            if (governor is < 1 or > 6000) return;
            //Both City and Gate virtual68 are232770, the saved Base67 bit0.
            int location = HireEconomyK(current.ReadBaseObjectLink(target), "repair_Base4E");
            bool? nearby = target.Kind == BaseKind.City
                ? target.PhysicalSlot == 0 ? state.UnassignedCityHasNearbyEnemyArmy
                    : state.Cities.GetValueOrDefault(location)?.HasNearbyEnemyArmy
                : target.PhysicalSlot == 0 ? state.UnassignedGateHasNearbyEnemyArmy
                    : state.Gates.GetValueOrDefault(location)?.HasNearbyEnemyArmy;
            if (HireEconomyK(nearby, "repair_Base67_nearby_enemy")) return;
            //Base70/78 is the original ordered footprint-Hex list. Any
            //nonzero Hex16 blocks the quote, independently of Army validity.
            var footprint = current.ReadBaseHexListHandle(target) ?? throw HireEconomyMissing("repair_Base70_footprint");
            var node = current.ReadHexListHead(footprint) ?? throw HireEconomyMissing("repair_footprint_head");
            while (node.Identity.HasValue)
            {
                var hex = current.ReadHexListNodePayload(node) ?? throw HireEconomyMissing("repair_footprint_Hex");
                if (HireEconomyK(current.ReadCaptureHexWord16(HireEconomyK(hex.PhysicalSlot, "repair_Hex_identity")), "repair_Hex16") != 0) return;
                node = current.ReadHexListNodeNext(node) ?? throw HireEconomyMissing("repair_footprint_next");
            }
            expense = unchecked(expense + fee);
        }
    }

    //266C40/266CD0 share the actual Force-policy38 quote and float32 discount.
    //The scalar is read at its original call site, including a rawCC0 trainer.
    private int ReadHireDomesticFeeCurrent(GameState state, SourceWorldContext current,
        BaseReference city, bool recruit, List<ForcePolicyScratchReceipt>? policies = null)
    {
        int forceId = HireEconomyK(current.ReadBaseForceSlot(city), "domestic_City_Force");
        var force = current.ReadForceTableReference(forceId <= 51 ? forceId : 0)
            ?? throw HireEconomyMissing("domestic_Force_pointer");
        int basis = HireEconomyK(CityDomesticParameters.Read(state, recruit ? "recruitBaseCost" : "trainingBaseCost"), "domestic_base_fee");
        if (recruit) basis = unchecked(basis * 3);
        var policyQuery = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, 0x26, true);
        policies?.Add(policyQuery);
        int policy = policyQuery.Value;
        int scale = HireEconomyK(CityDomesticParameters.Read(state, "costPolicyScale"), "domestic_policy_scale");
        float discount = (float)unchecked(scale * policy) * (float)basis / 100f;
        int reduction = discount <= 0 ? 0 : discount < 1 ? 1 : HireEconomyTruncate(discount);
        return Math.Max(0, unchecked(basis - reduction));
    }

    private int ReadHireSalaryQuote(GameState state, SourceWorldContext current, BaseReference target)
    {
        int location = HireEconomyK(current.ReadBaseObjectLink(target), "salary_Base4E");
        int force = HireEconomyK(current.ReadBaseForceSlot(target), "salary_Base_Force");
        int budget = BasePostLoad.ReadGoldCapacity(CurrentGateMapQueries.FindCity(target, current), current);
        int paid = 0, captives = 0;
        var employed = new List<int>();
        foreach (var item in current.ReadRegisteredPersonPhysicalSlots() ?? throw HireEconomyMissing("salary_Person_registry100"))
        {
            int person = item ?? throw HireEconomyMissing("salary_Person_pointer");
            if (HireEconomyK(current.ReadPersonFirstLocation(person), "salary_PersonE2") != location) continue;
            byte status = HireEconomyK(current.ReadPersonStatus(person), "salary_PersonE6");
            if (HireEconomyK(current.ReadPersonOldForceSlot(person), "salary_Person_Force") == force && status is >= 2 and <= 4) employed.Add(person);
            else if (status == 6) captives++;
        }
        //Every employed quote is paid against the same capacity budget.
        //Ordering cannot change the total min(sum,budget) for these ordinary
        //nonnegative costs; negative source costs remain a separate branch.
        foreach (int person in employed)
        {
            int due = ReadNativeMonthlyOfficerSalaryCurrent(state, current, person);
            if (due < 0) throw HireEconomyMissing("negative_officer_salary_ordered_budget");
            int amount = Math.Min(budget, due);
            paid = unchecked(paid + amount); budget = unchecked(budget - amount);
        }
        for (int i = 0; i < captives; i++)
        {
            int amount = Math.Min(budget, 50);
            paid = unchecked(paid + amount); budget = unchecked(budget - amount);
        }
        if (location < 52)
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw HireEconomyMissing("salary_Gate_registryA0"))
            {
                var gate = entry ?? throw HireEconomyMissing("salary_Gate_pointer");
                if (CurrentGateMapQueries.FindCity(gate, current) == target)
                    paid = unchecked(paid + ReadHireSalaryQuote(state, current, gate));
            }
        return paid;
    }

    private int ReadHireGoldLandmarkPercent(GameState state, SourceWorldContext current, BaseReference city, int effectKind = 1)
    {
        int force = HireEconomyK(current.ReadBaseForceSlot(city), "landmark_City_Force");
        if (force is >= 46 and <= 51) return 0;
        int sum = 0;
        foreach (var row in _landmarkDefinitions.Catalog.Sceneries)
        {
            if (row.Slot == 0) continue;
            var hex = current.ReadHexTableReference(row.RawHex44 < 48400 ? row.RawHex44 : 0)
                ?? throw HireEconomyMissing("landmark_Hex_pointer");
            int areaRaw = HireEconomyK(current.ReadHexAreaSlot(hex), "landmark_Hex12");
            if (areaRaw == 0) continue;
            var landmark = state.LandmarkInvestments?.GetValueOrDefault(row.Slot)
                ?? throw HireEconomyMissing("landmark_current_owner");
            if ((HireEconomyK(landmark.Flags, "landmark47") & 1) == 0) continue;
            var area = current.ReadAreaTableReference(areaRaw < 501 ? areaRaw : 0)
                ?? throw HireEconomyMissing("landmark_Area_pointer");
            int hexForce = HireEconomyK(current.ReadHexForceSlot(hex), "landmark_Hex14");
            if (hexForce == 0 || hexForce != force ||
                HireEconomyK(current.ReadAreaCurrentForceSlot(area), "landmark_Area_Force") != hexForce ||
                HireEconomyK(current.ReadAreaCitySlot(area), "landmark_Area35") != city.PhysicalSlot) continue;
            var development = landmark.Development ?? throw HireEconomyMissing("landmark55_56");
            if (development.EffectKind != effectKind) continue;
            int effect = development.Effect;
            if (HireEconomyK(current.ReadAreaNativeValidity(area), "landmark_Area_validity"))
            {
                ushort rawPerson = HireEconomyK(current.ReadAssignmentArea36(HireEconomyK(area.PhysicalSlot, "landmark_Area_identity")), "landmark_Area36");
                if (rawPerson != 0)
                {
                    int person = PersonAreaAppointmentSource.Resolve(current, rawPerson);
                    if (HireEconomyK(current.ReadPersonNativeValidity(person), "landmark_Person_validity") &&
                        HireEconomyK(current.ReadPersonOldForceSlot(person), "landmark_Person_Force") == hexForce)
                    {
                        int trait = HireEconomyK(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7B), "landmark_trait123");
                        if (trait != 0 && effect != 0)
                        {
                            if (state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(CityFoodIncomeSource.LandmarkOfficerEffectScaleKey, out int scale) != true)
                                throw HireEconomyMissing(CityFoodIncomeSource.LandmarkOfficerEffectScaleKey);
                            float extra = (float)unchecked(scale * trait) * effect / 100f;
                            effect = unchecked(effect + (extra > 0 && extra < 1 ? 1 : extra < 0 && extra > -1 ? -1 : HireEconomyTruncate(extra)));
                        }
                    }
                }
            }
            sum = unchecked(sum + effect);
        }
        return sum;
    }

    private static int HireEconomyValue(AreaAdministrationQuery<int?> query) =>
        query.Value is int value ? value : throw HireEconomyMissing(query.UnresolvedInput ?? "area_quote");
    private static int HireEconomyTruncate(float value) => value >= -2147483648f && value < 2147483648f ? (int)value : int.MinValue;
    private static T HireEconomyK<T>(T? value, string field) where T : struct => value ?? throw HireEconomyMissing(field);
    private static InvalidDataException HireEconomyMissing(string field) => new("personnel-hire.economy." + field);
}
