using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnDisasterConsequencesCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new NormalTurnDisasterConsequencesSource(this, state, current, pool).Run();

    //298250's display flag is local to this invocation. Its camera waits and
    //R-input summaries affect presentation only. The ordered intents remain
    //pending; neither numerical execution nor this producer acknowledges them.
    private sealed partial class NormalTurnDisasterConsequencesSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        private bool cameraRequested;
        private int lastCity, lastKind, lastHex;
        private NativeMapSeasonDate Date => state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
        internal GameState Run()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("shared_pool");
            int elapsed = unchecked((state.CampaignEventWorld?.ElapsedPeriodOffset ?? throw Missing("World454_offset")) +
                (int)(state.NativeMapSeason?.AdvancedPeriods ?? throw Missing("World454_periods")));
            if (elapsed < 37 || (state.CampaignEventWorld?.ScenarioMode ?? throw Missing("World_mode")) == 2) return state;
            package._baseCapacityInputs.BindCurrent(state, current);
            foreach (int cityPhysical in Registry(RegistryKind.City))
            {
                var city = new BaseReference(BaseKind.City, cityPhysical);
                var plague = Temporary();
                var locusts = Temporary();
                try
                {
                    //223350 is the managerB0 Area iterator with only Area35
                    //membership. Collect both lists before applying either.
                    foreach (int area in Registry(RegistryKind.Area))
                    {
                        if (N(current.ReadAreaCitySlot(new(area)), "iterator_Area35") != cityPhysical) continue;
                        byte flags = N(current.ReadAreaFlags38(new(area)), "Area38");
                        if ((flags & 1) != 0) Add(plague, area);
                        if ((flags & 4) != 0) Add(locusts, area);
                    }
                    ApplyList(plague, city, 0);
                    ApplyList(locusts, city, 1);
                }
                finally { Release(locusts); Release(plague); }
            }
            if (cameraRequested) Intent(MonthlyDisasterConsequenceOperation.CameraRestore, lastKind, lastCity, lastHex);
            return state;
        }

        private void ApplyList(HexListHandle list, BaseReference groupCity, int kind)
        {
            bool firstPlayerArea = true;
            bool any = false;
            foreach (int area in Values(list))
            {
                any = true;
                var force = AreaForce(area);
                if (Player(force) && firstPlayerArea)
                {
                    int hex = current.ReadBaseCurrentHexIndex(City(area));
                    Intent(MonthlyDisasterConsequenceOperation.CameraFocus, kind, Physical(groupCity), hex);
                    cameraRequested = true; firstPlayerArea = false;
                    lastCity = Physical(groupCity); lastKind = kind; lastHex = hex;
                }
                ApplyArea(area, groupCity, kind);
            }
            if (any && Player(Force(groupCity)))
            {
                var text = package.FormatNormalTurnReport(state, current, 0x5E39,
                    new(Base0: N(current.ReadBaseObjectLink(groupCity), "summary_City4E"),
                        String0: package.ReadHistoryDataText(1, kind == 0 ? 0x3F9 : 0x3F8)));
                state = text.State;
                Intent(MonthlyDisasterConsequenceOperation.Summary, kind, Physical(groupCity),
                    current.ReadBaseCurrentHexIndex(groupCity), text.Text);
            }
        }

        private void ApplyArea(int area, BaseReference groupCity, int kind)
        {
            BaseReference? centralCity = IsCityCenter(area) ? City(area) : null;
            BaseReference? gate = null;
            foreach (int physical in Registry(RegistryKind.Gate))
            {
                var candidate = new BaseReference(BaseKind.Gate, physical);
                if (current.ReadBaseCurrentHexIndex(candidate) == AreaHex(area))
                { gate = candidate; break; }
            }
            int percentage = unchecked(Draw(P("loss-random-bound")) + 1);
            if (Advanced()) percentage = unchecked(N(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 30), "advanced30") * percentage) / 100;
            if (centralCity is not null && Valid(centralCity)) ApplyBase(centralCity, groupCity, percentage, kind);
            if (gate is not null && Valid(gate)) ApplyBase(gate, groupCity, percentage, kind);
            int plagueCap = 0;
            if (kind == 0)
            {
                plagueCap = P(Advanced() ? "advanced-plague-unit-cap" : "plague-unit-cap");
                ApplyArmies(area, groupCity, percentage, plagueCap);
            }
            if ((centralCity is null || !Valid(centralCity)) && (gate is null || !Valid(gate)))
                Intent(MonthlyDisasterConsequenceOperation.AreaOrBaseEffect, kind, Physical(groupCity), AreaHex(area));
            if (kind == 0)
            {
                int population = N(Area(area).NativeNormalAftermath?.Quantity52, "Area52");
                if (population >= 1001)
                {
                    if (Advanced()) plagueCap = P("advanced-plague-population-cap");
                    int loss = Clamp(unchecked(population * percentage) / 100, 10, plagueCap);
                    var owner = Area(area);
                    state = state with { Areas = new(state.Areas) { [area] = owner with {
                        NativeNormalAftermath = owner.NativeNormalAftermath! with { Quantity52 = (ushort)Clamp(unchecked(population - loss), 0, 60000) } } } };
                }
            }
            int securityLoss = Clamp(unchecked(Draw(P(kind == 0 ? "plague-security-random-bound" : "locust-security-random-bound")) + 2), 2, 5);
            int after = Clamp(unchecked(N(Area(area).Security, "Area54") - securityLoss), 0, 100);
            if (N(current.ReadForcePhysicalSlot(AreaForce(area)), "Area_Force60") is not (>= 46 and <= 50))
            {
                var owner = Area(area);
                state = state with { Areas = new(state.Areas) { [area] = owner with { Security = after } } };
            }
            if (kind == 0) ApplyUnlinkedPeople(area);
        }

        private void ApplyBase(BaseReference target, BaseReference groupCity, int percentage, int kind)
        {
            if (kind == 0)
            {
                int cap = P(Advanced() ? "advanced-plague-base-cap" : "plague-base-cap");
                int ready = N(current.ReadBaseResource(target, BaseResourceKind.Troops), "Base3C");
                int loss = 0;
                if (ready > 2000)
                {
                    loss = Clamp(unchecked(ready * percentage) / 100, 20, cap);
                    var write = AdjustCurrentBaseCombatQuantity(BaseCombatQuantity.ReadyTroops, ready, unchecked(-loss),
                        () => BasePostLoad.ReadTroopCapacity(target, current));
                    current.SetBaseResource(target, BaseResourceKind.Troops, write.After);
                    state = PublishCurrentBaseCombatQuantities(state, current, target, true, false, false);
                }
                int wounded = N(current.ReadArmyReturnBaseWounded40(target), "Base40");
                if (wounded > 1)
                {
                    int woundsLost = Clamp(unchecked(wounded * percentage * 2) / 100, 1, cap);
                    var write = AdjustCurrentBaseCombatQuantity(BaseCombatQuantity.WoundedTroops, wounded, unchecked(-woundsLost),
                        () => BasePostLoad.ReadTroopCapacity(target, current));
                    current.SetArmyReturnBaseWounded40(target, write.After);
                    state = PublishCurrentBaseCombatQuantities(state, current, target, false, true, false);
                }
                if (BasePlayer(target)) Effect(target, groupCity, kind, loss);
            }
            else
            {
                int before = ReadDiplomacyBaseResourceCurrent(current, target, BaseResourceKind.Supplies);
                int cap = P("locust-base-food-cap");
                if (before > 2000)
                {
                    if (Advanced()) cap = P("advanced-locust-base-food-cap");
                    int loss = Clamp(unchecked(ReadDiplomacyBaseResourceCurrent(current, target, BaseResourceKind.Supplies) * percentage) / 100, 1, cap);
                    var capacityCity = BaseCurrencyAdditionSource.CurrencyCity(target, current);
                    int capacity = BasePostLoad.ReadSuppliesCapacity(capacityCity, current);
                    int fresh = ReadDiplomacyBaseResourceCurrent(current, target, BaseResourceKind.Supplies);
                    int requested = Clamp(unchecked(fresh - loss), 0, capacity);
                    var setCity = BaseCurrencyAdditionSource.CurrencyCity(target, current);
                    int after = Clamp(requested, 0, BasePostLoad.ReadSuppliesCapacity(setCity, current));
                    current.SetBaseResource(setCity, BaseResourceKind.Supplies, after);
                    int id = Physical(setCity);
                    var owner = state.Cities.GetValueOrDefault(id) ?? throw Missing("food_City_owner");
                    state = state with { Cities = new(state.Cities) { [id] = owner with { Supplies = after } } };
                    state = BaseSupplyExpenseAccumulatorSource.AddCurrent(state, target, loss);
                }
                if (BasePlayer(target)) Effect(target, groupCity, kind, null);
            }
        }

        private void Effect(BaseReference target, BaseReference groupCity, int kind, int? loss)
            => Intent(MonthlyDisasterConsequenceOperation.AreaOrBaseEffect, kind, Physical(groupCity),
                current.ReadBaseCurrentHexIndex(target), amount: loss,
                location: N(current.ReadBaseObjectLink(target), "effect_Base4E"));
    }
}
