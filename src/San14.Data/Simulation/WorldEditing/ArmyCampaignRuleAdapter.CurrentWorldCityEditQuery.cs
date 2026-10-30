using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static readonly int[] CityEditCounterTactics = [0, 4, 6, 10, 11, 14, 15, 17, 18];

    public AreaAdministrationQuery<CurrentWorldCityEditPublication> QueryCurrentWorldCityEdit(
        GameState state, int cityId) => ReadCurrentWorldCityEditPublication(state, cityId, null);

    public AreaAdministrationQuery<CurrentWorldCityEditPublication> QueryCurrentWorldCityEditPreview(
        GameState state, int cityId, CurrentWorldCityEditValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return ReadCurrentWorldCityEditPublication(state, cityId, values);
    }

    private AreaAdministrationQuery<CurrentWorldCityEditPublication> ReadCurrentWorldCityEditPublication(
        GameState state, int cityId, CurrentWorldCityEditValues? draft)
    {
        try
        {
            if (cityId is < 1 or > 46 || !state.Cities.TryGetValue(cityId, out var owner) || owner.Id != cityId)
                throw WorldEditMissing("city_editable_identity");
            var current = CaptureWritableTransportRouteCurrent(state);
            var city = current.ReadCityTableReference(cityId) ?? throw WorldEditMissing("city_pointer");
            if (city.Kind != BaseKind.City || city.PhysicalSlot != cityId ||
                !WorldEditNeed(current.ReadBaseNativeValidity(city), "city_validity"))
                throw WorldEditMissing("city_not_current");
            bool capacityBound = _baseCapacityInputs.BindCurrent(state, current);
            int? actualSize = current.ReadCitySizeB8(city);
            if (draft?.Size is int size)
            {
                if (size is < 0 or > 4 || size == 0 && actualSize != 0)
                    throw WorldEditMissing("city_preview_size_range");
                // Only this operation-local current changes. In particular,
                // preview does not invoke the development writer or its RNG.
                current.SetCitySizeB8(cityId, (byte)size);
            }
            var sizeNumber = new CurrentWorldEditNumber(draft?.Size ?? actualSize, 1, 4,
                draft?.Size is not null || actualSize.HasValue ? null : "current-world-edit.city_size");
            var gold = Number(draft?.Gold,
                () => ReadDiplomacyBaseResourceCurrent(current, city, BaseResourceKind.Gold),
                () => BasePostLoad.ReadGoldCapacity(city, current), "gold");
            var food = Number(draft?.Food,
                () => ReadDiplomacyBaseResourceCurrent(current, city, BaseResourceKind.Supplies),
                () => BasePostLoad.ReadSuppliesCapacity(city, current), "food");
            var troops = Number(draft?.Troops,
                () => WorldEditNeed(current.ReadBaseResource(city, BaseResourceKind.Troops), "city_troops"),
                () => BasePostLoad.ReadTroopCapacity(city, current), "troops");
            var durability = Number(draft?.Durability,
                () => BasePostLoad.ReadLinkedField14(city, current),
                () => BasePostLoad.ReadCapacities(city, current).Field46, "durability");
            var morale = Number(draft?.Morale,
                () => WorldEditNeed(current.ReadBaseMorale44(city), "city_morale"),
                () =>
                {
                    int rawForce = WorldEditNeed(current.ReadBaseForceSlot(city), "city_morale_force");
                    var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
                        ?? throw WorldEditMissing("city_morale_force_pointer");
                    return BasePostLoad.ReadMoraleUpper(force, current);
                }, "morale");

            int? counter = null;
            string? counterMissing = null;
            try
            {
                var fields = CaptureBaseThreatInputs.ReadCurrent(state, city)
                    ?? throw WorldEditMissing("city_tactic_owner");
                int actual = WorldEditNeed(fields.Byte48, "city_tactic");
                counter = draft?.CounterTactic ?? actual;
                if (counter != actual && !CityEditCounterTactics.Contains(counter.Value))
                    throw WorldEditMissing("city_preview_tactic_range");
            }
            catch (InvalidDataException error) { counterMissing = error.Message; }

            var options = new List<CurrentWorldEditOption>(CityEditCounterTactics.Length);
            // An out-of-selector current value remains the actual value; it
            // does not manufacture an additional selectable radio item.
            foreach (int tacticId in CityEditCounterTactics)
            {
                if (!state.Tactics.TryGetValue(tacticId, out var tactic))
                { counterMissing ??= "current-world-edit.city_tactic_name:" + tacticId; continue; }
                options.Add(new(tacticId, tactic.Name));
            }
            if (counter is int selected)
            {
                int index = options.FindIndex(option => option.Id == selected);
                if (index > 0)
                { var first = options[index]; options.RemoveAt(index); options.Insert(0, first); }
                if (!state.Tactics.ContainsKey(selected))
                    counterMissing ??= "current-world-edit.city_tactic_name:" + selected;
            }
            return AreaAdministrationQuery<CurrentWorldCityEditPublication>.Known(new(cityId, owner.Name,
                sizeNumber, gold, food, troops, durability, morale,
                new(counter, 0, 255, counterMissing), options));

            CurrentWorldEditNumber Number(int? requested, Func<int> readValue, Func<int> readMaximum, string field)
            {
                int? value = requested, maximum = null;
                string? missing = null;
                try { value ??= readValue(); }
                catch (InvalidDataException error) { missing = error.Message; }
                try
                {
                    if (!capacityBound) throw WorldEditMissing("city_capacity_owner_" + field);
                    maximum = readMaximum();
                }
                catch (InvalidDataException error) { missing ??= error.Message; }
                //65C150 creates/rebinds all five sliders against the current
                //clone's capacities. The read-only original query never clamps.
                if (draft is not null && value is int amount && maximum is int upper)
                    value = Math.Max(0, Math.Min(amount, upper));
                return new(value, 0, maximum, missing);
            }
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<CurrentWorldCityEditPublication>.Unknown(error.Message); }
    }
}
