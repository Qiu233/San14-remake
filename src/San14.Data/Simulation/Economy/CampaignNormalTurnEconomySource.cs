using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnEconomyCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int action, ArmyMapGroupingOwner groups)
    {
        if (action == 40) return ApplyNormalTurnForceEconomyCurrent(state, current, pool);
        if (action != 39) throw EconomyMissing("action_dispatch");
        if (Seed.Language != "TC") throw EconomyMissing("SC_current_rules");
        //Food's established writers update the operation's dictionaries. Copy
        //these owned tables once here, never once for each Base or quote.
        state = state with { Cities = new(state.Cities), Gates = new(state.Gates), Units = new(state.Units) };
        BindPersonnelHireGenerationEconomyCurrent(state, current);
        var date = state.NativeMapSeason?.ThirdDate ?? throw EconomyMissing("World34_date");
        var effects = new List<CampaignEffectRequest>();
        foreach (var entry in current.ReadRegisteredBases() ?? throw EconomyMissing("Base80"))
        {
            var receiver = entry ?? throw EconomyMissing("Base80_payload");
            int location = EconomyK(current.ReadBaseObjectLink(receiver), "Base4E");
            if (location < 52)
            {
                var force = current.ReadForceTableReference(EconomyK(current.ReadBaseForceSlot(receiver), "Base_Force") is int raw && raw < 52 ? raw : 0)
                    ?? throw EconomyMissing("Base_Force_pointer");
                if (EconomyK(current.ReadForceNativeValidity(force), "Force_validity"))
                {
                    ushort recruiter = EconomyK(current.ReadAssignmentCityCA(EconomyK(receiver.PhysicalSlot, "City_pointer")), "CityCA");
                    if (recruiter is >= 1 and <= 6000)
                        state = SetTurnRecruitmentPeriods(state, current, EconomyK(receiver.PhysicalSlot, "CF_City"),
                            (byte)Math.Min(3, EconomyK(current.ReadCaptureCityByteCF(EconomyK(receiver.PhysicalSlot, "CF_City")), "CityCF") + 1));
                }
                if (state.Cities.TryGetValue(location, out var city))
                    state = state with { Cities = new(state.Cities) { [location] = city with { TurnIncomeTotals = new(0, 0, 0) } } };
                state = ResetCityMerchantCityForTurn(state, current, location);
            }
            if (receiver.Kind == BaseKind.City && state.Cities.TryGetValue(location, out var cityOwner))
                state = state with { Cities = new(state.Cities) { [location] = cityOwner with { PeriodGoldExpenditure = 0, PeriodFoodExpenditure = 0 } } };
            else if (receiver.Kind == BaseKind.Gate && state.Gates.TryGetValue(location, out var gateOwner))
                state = state with { Gates = new(state.Gates) { [location] = gateOwner with { PeriodGoldExpenditure = 0, PeriodFoodExpenditure = 0 } } };
            state = ApplyNormalTurnCityIncomeCurrent(state, current, receiver, date);
            var ownerForce = current.ReadForceTableReference(EconomyK(current.ReadBaseForceSlot(receiver), "food_Force") is int ownerRaw && ownerRaw < 52 ? ownerRaw : 0)
                ?? throw EconomyMissing("food_Force_pointer");
            if (EconomyK(current.ReadForceNativeValidity(ownerForce), "food_Force_validity") && location < 52)
            {
                if (date.Day == 1) state = CompleteNativeMonthlySalary(state, current, receiver);
                state = CompleteNativeArmyFoodForBaseCurrent(state, current, receiver, groups).State;
            }
            state = ApplyNormalTurnAreaAdministrationCurrent(state, current, pool, receiver, effects);
            Flush();
            state = ApplyNormalTurnDomesticPolicyCurrent(state, current, pool, receiver, effects);
            Flush();
            state = ApplyNormalTurnTrainingCurrent(state, current, receiver);
            if (date.Day == 1)
            {
                state = ApplyNormalTurnCityGrowthCurrent(state, current, pool, receiver);
                state = RefreshCityMerchantMonth(state, current, receiver);
            }
            state = ApplyNormalTurnBaseRepairCurrent(state, current, pool, receiver);
        }
        if (date.Day == 1)
        {
            for (int city = 0; city < 52; city++) state = SetTurnRecruitmentPeriods(state, current, city, 0);
            for (int area = 0; area < 501; area++) current.SetAssignmentArea6D(area, 0);
        }
        state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
        Flush();
        return state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw EconomyMissing("lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });

        void Flush()
        {
            if (effects.Count == 0) return;
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw EconomyMissing("pending_owner"))
                .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw EconomyMissing("revision"), effects) };
            effects.Clear();
        }
    }

    private static GameState SetTurnRecruitmentPeriods(GameState state, SourceWorldContext current, int city, byte value)
    {
        var owner = state.NativeBaseCaptureCurrent ?? throw EconomyMissing("CF_owner");
        var before = owner.Cities.GetValueOrDefault(city) ?? throw EconomyMissing("CF_City_owner");
        current.SetCaptureCityByteCF(city, value);
        return state with { NativeBaseCaptureCurrent = owner with { Cities = new(owner.Cities) { [city] = before with { CF = value } } } };
    }
}
