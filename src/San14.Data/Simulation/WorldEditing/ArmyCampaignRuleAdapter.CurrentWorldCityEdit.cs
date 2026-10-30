using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private CommandResult ApplyCurrentWorldCityEditCurrent(GameState state, EditCurrentWorldCityOrder order)
    {
        if (order.Values is not { } values)
            return CommandResult.Reject("City edit values are missing.", "world_edit.city_values");
        if (values.Size is < 0 or > 4 || values.Gold is < 0 || values.Food is < 0 || values.Troops is < 0 ||
            values.Durability is < 0 || values.Morale is < 0 or > 255 ||
            values.CounterTactic is < 0 or > 255)
            return CommandResult.Reject("City edit values are outside the source controls.", "world_edit.city_range");
        if (order.CityId is < 1 or > 46 || !state.Cities.ContainsKey(order.CityId))
            return CommandResult.Reject("City is outside the editable selection.", "world_edit.city_target");

        //The reached2FE2B0 quote includes training/administration and real
        //person-linked tasks. Borrow the complete existing transaction input
        //scope, as deployment cancellation and transport warning already do.
        var world = CaptureOwnedArmyReturnWorld(state);
        var current = world.Context;
        var pool = world.Pool;
        var city = current.ReadCityTableReference(order.CityId) ?? throw WorldEditMissing("city_pointer");
        if (city.Kind != BaseKind.City || city.PhysicalSlot != order.CityId ||
            !WorldEditNeed(current.ReadBaseNativeValidity(city), "city_validity"))
            return CommandResult.Reject("City is no longer an editable receiver.", "world_edit.city_target");
        if (!_baseCapacityInputs.BindCurrent(state, current)) throw WorldEditMissing("city_capacity_owner");
        var working = state;
        bool edited = false;
        if (values.Size is int size)
        {
            int before = WorldEditNeed(current.ReadCitySizeB8(city), "city_size");
            if (size == 0 && before != size)
                return CommandResult.Reject("The size control cannot select zero.", "world_edit.city_size_range");
            if (before != size)
            {
                var development = ApplyCitySizeDevelopmentCurrent(working, current, city, before, size, true);
                working = development.State;
                //621C40 takes the real development writer's return value;
                //a raw size change alone does not synthesize its dirty output.
                edited = development.DevelopmentChanged;
                current.SetCitySizeB8(order.CityId, (byte)size);
                working = working with { Cities = new(working.Cities)
                    { [order.CityId] = working.Cities[order.CityId] with { Size = (CitySize)(size - 1) } } };
                if (before > size) working = ClearCityExpansionOccurrences(working, current, order.CityId, size);
                working = RefreshCityEditDamageTier(working, current, city);
            }
        }

        var resources = new StoryBaseResourcesCurrent(working, current);
        Currency(BaseResourceKind.Gold, values.Gold);
        Currency(BaseResourceKind.Supplies, values.Food);
        if (values.Troops is int troops && WorldEditNeed(current.ReadBaseResource(city, BaseResourceKind.Troops), "city_troops") != troops)
        { resources.SetCityEditTroops(city, troops); edited = true; }
        if (values.Durability is int durability && BasePostLoad.ReadLinkedField14(city, current) != durability)
        { resources.SetCityEditEndurance(city, durability); edited = true; }
        working = resources.State;
        if (values.Morale is int morale && WorldEditNeed(current.ReadBaseMorale44(city), "city_morale") != morale)
        {
            //241790 suppresses special-Force stores.624190 still marks its
            //requested changed branch, exactly as the original setter caller.
            bool written = BasePostLoad.WriteMoraleCurrent(city, morale, current);
            working = PublishCurrentBaseCombatQuantities(working, current, city, false, false, written);
            edited = true;
        }
        if (values.CounterTactic is int counter)
        {
            var fields = current.ReadCaptureBaseThreatFields(city) ?? CaptureBaseThreatInputs.ReadCurrent(working, city)
                ?? throw WorldEditMissing("city_tactic_owner");
            int before = WorldEditNeed(fields.Byte48, "city_tactic");
            if (before != counter)
            {
                if (!CityEditCounterTactics.Contains(counter))
                    return CommandResult.Reject("The counter tactic is not offered by the source selector.", "world_edit.city_tactic_range");
                var native = working.NativeArmySimulation ?? throw WorldEditMissing("city_tactic_parameter_owner");
                working = working with { NativeArmySimulation = native with { Parameters = native.Parameters with
                    { Int32Values = new(native.Parameters.Int32Values) { [CaptureBaseThreatInputs.Key(city, 0x48)] = counter } } } };
                current.SetCaptureBaseThreatFields(city, fields with { Byte48 = (byte)counter });
                edited = true;
            }
        }
        if (!edited) return CommandResult.Accept(working);
        working = MarkCurrentWorldEdited(working, CurrentWorldEditKind.City);
        OwnedArmyReturnServiceInputs.BindCurrent(working, current);
        int rawForce = WorldEditNeed(current.ReadBaseForceSlot(city), "city_force");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current)
            ?? throw WorldEditMissing("city_player_force");
        if (WorldEditNeed(current.ReadForceNativeValidity(player), "city_player_validity") &&
            rawForce == WorldEditNeed(current.ReadForcePhysicalSlot(player), "city_player_identity"))
        {
            //The exact2FE2B0 consumer already used by deployment cancellation:
            //a real food quote/pending cleanup and the saved warning bit.
            working = RefreshCancelledDeploymentFoodWarningCurrent(working, current, city, FacilityOperation.Playable(working));
            working = PublishNormalTurnCurrent(working, current, pool);
        }
        return CommandResult.Accept(working);

        void Currency(BaseResourceKind kind, int? requested)
        {
            if (requested is not int amount) return;
            if (ReadDiplomacyBaseResourceCurrent(current, city, kind) == amount) return;
            resources.SetCityEditCurrency(city, kind, amount);
            edited = true;
        }
    }

    private sealed partial class StoryBaseResourcesCurrent
    {
        internal void SetCityEditCurrency(BaseReference city, BaseResourceKind kind, int value) => SetCurrency(city, kind, value);
        internal void SetCityEditTroops(BaseReference city, int value) => SetTroops(city, value);
        internal void SetCityEditEndurance(BaseReference city, int value) => SetEndurance(city, value, true);
    }

    private static GameState RefreshCityEditDamageTier(GameState state, SourceWorldContext current, BaseReference city)
    {
        byte tier = BasePostLoad.ReadFlag66(city, current);
        current.SetBasePostLoadFlag66(city, tier);
        int id = WorldEditNeed(city.PhysicalSlot, "city_tier_identity");
        return state with { Cities = new(state.Cities) { [id] = state.Cities[id] with { DamageTier = tier } } };
    }

    private static GameState ClearCityExpansionOccurrences(GameState state, SourceWorldContext current, int city, int size)
    {
        var row = CityEditExpansionFlags.FirstOrDefault(row => row.City == city);
        if (row.City == 0) return state;
        var history = state.CampaignEvents ?? throw WorldEditMissing("city_expansion_occurrence_owner");
        bool[]? occurred = null;
        if (size == 1) Clear(row.Middle);
        if (size <= 2) Clear(row.Large);
        if (size <= 3) Clear(row.Giant);
        return occurred is null ? state : state with { CampaignEvents = history with { Occurred = occurred } };
        void Clear(int index)
        {
            if ((uint)index >= history.Occurred.Length) throw WorldEditMissing("city_expansion_occurrence_index");
            current.SetWorldEventFlag(index, false);
            if (!history.Occurred[index]) return;
            occurred ??= history.Occurred.ToArray();
            occurred[index] = false;
        }
    }

    //Exact46 typed rows from the independent TC and SC editor tables.
    private static readonly (int City, int Middle, int Large, int Giant)[] CityEditExpansionFlags =
    [
        (1,63,109,155),(2,64,110,156),(3,65,111,157),(4,66,112,158),
        (5,67,113,159),(6,68,114,160),(7,69,115,161),(8,70,116,162),
        (9,71,117,163),(10,72,118,164),(11,73,119,165),(12,74,120,166),
        (13,75,121,167),(14,76,122,168),(15,77,123,169),(16,78,124,170),
        (17,79,125,171),(18,80,126,172),(19,81,127,173),(20,82,128,174),
        (21,83,129,175),(22,84,130,176),(23,85,131,177),(24,86,132,178),
        (25,87,133,179),(26,88,134,180),(27,89,135,181),(28,90,136,182),
        (29,91,137,183),(30,92,138,184),(31,93,139,185),(32,94,140,186),
        (33,95,141,187),(34,96,142,188),(35,97,143,189),(36,98,144,190),
        (37,99,145,191),(38,100,146,192),(39,101,147,193),(40,102,148,194),
        (41,103,149,195),(42,104,150,196),(43,105,151,197),(44,106,152,198),
        (45,107,153,199),(46,108,154,200)
    ];
}
