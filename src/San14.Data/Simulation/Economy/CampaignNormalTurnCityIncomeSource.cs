using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2BC080(mode0). Income and recruitment are posted together: recruitment
    //fees can spend this City's incoming gold before the final stock writes.
    private GameState ApplyNormalTurnCityIncomeCurrent(GameState state, SourceWorldContext current,
        BaseReference city, NativeMapSeasonDate date)
    {
        int location = EconomyK(current.ReadBaseObjectLink(city), "income_Base4E");
        if (location >= 52) return state;
        var force = current.ReadForceTableReference(EconomyK(current.ReadBaseForceSlot(city), "income_Force") is int raw && raw < 52 ? raw : 0)
            ?? throw EconomyMissing("income_Force_pointer");
        if (!EconomyK(current.ReadForceNativeValidity(force), "income_Force_validity")) return state;
        int forceId = EconomyK(current.ReadForcePhysicalSlot(force), "income_Force_identity");
        bool monthStart = date.Day == 1;
        state = state with { Areas = new(state.Areas) };
        var abilities = CreateArmyDestructionAbilities(current);
        var preview = new AreaAdministrationPreviewSource(state, current, _scenario, Seed.Language, _baseCapacityInputs, true);
        var recruits = new SortedDictionary<int, int>();
        int gold = 0, food = 0, troops = 0;
        foreach (var item in current.ReadRegisteredAreaReferences() ?? throw EconomyMissing("income_AreaB0"))
        {
            var area = item ?? throw EconomyMissing("income_Area_pointer");
            if (EconomyK(current.ReadAreaCitySlot(area), "income_Area35") != city.PhysicalSlot) continue;
            int id = EconomyK(area.PhysicalSlot, "income_Area_identity");
            var owner = state.Areas.GetValueOrDefault(id) ?? throw EconomyMissing("income_Area_owner");
            state.Areas[id] = owner with { TurnIncomeTotals = new(0, 0, 0) };
            if (!monthStart || EconomyK(current.ReadAreaCurrentForceSlot(area), "income_Area_Force") != forceId) continue;
            int earnedGold = ReadHireAreaGoldIncomeCurrent(state, current, area, city, abilities, AssignmentCount);
            int earnedFood = CityFoodIncomeSource.ReadArea(state, current, area, city, force, date.Month, abilities);
            gold = unchecked(gold + earnedGold); food = unchecked(food + earnedFood);
            int recruited = HireEconomyValue(preview.Soldiers(id, mode: 0, recruitmentPolicy: force =>
                ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, 0x1C, true).Value));
            int mapped = EconomyK(current.ReadAreaCitySlot(area), "recruit_Area35");
            var mappedCity = current.ReadCityTableReference(mapped < 52 ? mapped : 0)
                ?? throw EconomyMissing("recruit_City_pointer");
            int mappedLocation = EconomyK(current.ReadBaseObjectLink(mappedCity), "recruit_City4E");
            bool threatened = mappedCity.PhysicalSlot == 0 ? EconomyK(state.UnassignedCityHasNearbyEnemyArmy, "recruit_fallback_Base67")
                : EconomyK(state.Cities.GetValueOrDefault(mappedLocation)?.HasNearbyEnemyArmy, "recruit_Base67");
            recruited = unchecked(recruited * (threatened ? 60 : 100)) / 100;
            if (recruited != 0) recruits[id] = recruited;
            var latest = state.Areas[id];
            state.Areas[id] = latest with { TurnIncomeTotals = new(
                unchecked((ushort)Math.Max(0, earnedGold)), unchecked((ushort)Math.Max(0, earnedFood)), 0) };
        }
        int troopPercent = 0;
        if (forceId != 51 && forceId is not (>= 46 and <= 50))
        {
            int goldPercent = ReadHireGoldLandmarkPercent(state, current, city);
            int foodPercent = ReadHireGoldLandmarkPercent(state, current, city, 2);
            troopPercent = ReadHireGoldLandmarkPercent(state, current, city, 3);
            if (goldPercent > 0 && gold > 0) gold = HireEconomyTruncate(goldPercent / 100f * (float)gold + (float)gold);
            if (foodPercent > 0 && food > 0) food = HireEconomyTruncate(foodPercent / 100f * (float)food + (float)food);
        }
        if (monthStart)
        {
            bool addedGold = false, addedFood = false;
            foreach (int? item in current.ReadRegisteredPersonPhysicalSlots() ?? throw EconomyMissing("income_Person100"))
            {
                int person = EconomyK(item, "income_Person_pointer");
                if (location is < 1 or > 61 || EconomyK(current.ReadPersonFirstLocation(person), "income_PersonE2") != location ||
                    EconomyK(current.ReadPersonStatus(person), "income_PersonE6") is not (>= 1 and <= 4) || ReadHireAssociationActorBusy(current, person)) continue;
                if (!addedGold)
                {
                    int extra = EconomyK(TacticRemakeCurrentTraits.PersonContribution(current, person, 71), "income_resident_trait71");
                    if (extra != 0) addedGold = true;
                    gold = Math.Min(unchecked(gold + extra), unchecked(BasePostLoad.ReadGoldCapacity(city, current)
                        - EconomyK(current.ReadBaseResource(city, BaseResourceKind.Gold), "income_Gold")));
                }
                if (!addedFood)
                {
                    int extra = EconomyK(TacticRemakeCurrentTraits.PersonContribution(current, person, 72), "income_resident_trait72");
                    if (extra != 0) addedFood = true;
                    food = Math.Min(unchecked(food + extra), unchecked(BasePostLoad.ReadSuppliesCapacity(city, current)
                        - EconomyK(current.ReadBaseResource(city, BaseResourceKind.Supplies), "income_Food")));
                }
                if (addedGold && addedFood) break;
            }
        }
        int budget = unchecked(EconomyK(current.ReadBaseResource(city, BaseResourceKind.Gold), "recruit_Gold") + gold);
        int fee = ReadHireDomesticFeeCurrent(state, current, city, recruit: true);
        int fees = 0;
        int initialTroops = EconomyK(current.ReadBaseResource(city, BaseResourceKind.Troops), "recruit_City3C");
        int capacity = BasePostLoad.ReadTroopCapacity(city, current);
        var center = PersonFreedomHomeSource.FindBaseArea229FD0(current, city);
        int centerId = EconomyK(center.PhysicalSlot, "recruit_center_Area");
        if (recruits.TryGetValue(centerId, out int centerCount))
        {
            Recruit(centerId, centerCount);
            recruits.Remove(centerId);
        }
        foreach (var row in recruits) Recruit(row.Key, row.Value);
        state = AddNormalTurnBaseResourceCurrent(state, current, city, BaseResourceKind.Gold, unchecked(-fees), recordExpense: true);
        if (troops != 0)
        {
            if (forceId != 51 && forceId is not (>= 46 and <= 50) && troopPercent > 0 && troops > 0)
                troops = Math.Min(unchecked(capacity - initialTroops), HireEconomyTruncate(troopPercent / 100f * (float)troops + (float)troops));
            int morale = EconomyK(current.ReadBaseMorale44(city), "recruit_Morale44");
            int numerator = unchecked(morale * initialTroops + troops * 10);
            int weighted = (int)((long)numerator / unchecked(troops + initialTroops));
            state = SetNormalTurnMoraleCurrent(state, current, city, weighted);
        }
        state = AddNormalTurnBaseResourceCurrent(state, current, city, BaseResourceKind.Gold, gold, recordIncome: true);
        state = AddNormalTurnBaseResourceCurrent(state, current, city, BaseResourceKind.Supplies, food, recordIncome: true);
        state = AddNormalTurnBaseResourceCurrent(state, current, city, BaseResourceKind.Troops, troops, recordIncome: true);
        int recruiter = PersonAreaAppointmentSource.Resolve(current, EconomyK(current.ReadAssignmentCityCA(EconomyK(city.PhysicalSlot, "recruit_City_identity")), "recruit_CityCA"));
        if (troops > 0 && DomesticGrowthEligible(current, recruiter))
        {
            bool busy = DomesticRewardBusy(current, recruiter);
            state = ApplyNormalTurnAbilityExperienceCurrent(state, current, recruiter, 1, busy ? 6 : 30);
            state = ApplyTalentPendingMeritCurrent(state, current, recruiter, 0);
        }
        if (EconomyK(current.ReadPersonNativeValidity(recruiter), "recruit_Person_validity"))
            state = ApplyTalentPendingMeritCurrent(state, current, recruiter, unchecked((DomesticRewardBusy(current, recruiter) ? 27 : 54) * 10));
        return state;

        void Recruit(int areaId, int amount)
        {
            if (amount <= 0 || fee > budget || troops >= unchecked(capacity - initialTroops)) return;
            budget = unchecked(budget - fee); fees = unchecked(fees + fee);
            troops = Math.Min(unchecked(capacity - initialTroops), unchecked(troops + amount));
            var owner = state.Areas.GetValueOrDefault(areaId) ?? throw EconomyMissing("recruit_Area_owner");
            var population = owner.NativeNormalAftermath ?? throw EconomyMissing("recruit_Area52_owner");
            if (forceId is not (>= 46 and <= 50))
                population = population with { Quantity52 = (ushort)Math.Clamp(unchecked(EconomyK(population.Quantity52, "recruit_Area52") - amount), 0, 60000) };
            var totals = owner.TurnIncomeTotals ?? throw EconomyMissing("recruit_Area_income_reset");
            state.Areas[areaId] = owner with { NativeNormalAftermath = population,
                TurnIncomeTotals = totals with { RecruitedTroops = unchecked((ushort)Math.Max(0, amount)) } };
        }
        int AssignmentCount(int person) => (current.ReadAssignmentPersonAreaVector(person)
            ?? throw EconomyMissing("income_Person_vector")).ActiveCount1E8;
    }
}
