using System.Collections.ObjectModel;
using System.Globalization;
using San14.Core;

namespace San14.Data;

internal sealed record AreaDetailEconomyPublication(
    IReadOnlyDictionary<int, string> Text,
    IReadOnlyDictionary<int, string> UnresolvedFields)
{
    public IReadOnlyList<CityInformationGauge> Gauges { get; init; } = [];
    //Complete RawAlign byte after931F20/931FF0, not a text preset index.
    public IReadOnlyDictionary<int, int> TextAlignments { get; init; } = new Dictionary<int, int>();
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //902610's current Area values. The caller lends one complete current;
    //this reader does not install opening development or capture another World.
    internal AreaDetailEconomyPublication ReadAreaDetailEconomyCurrent(GameState state,
        SourceWorldContext current, int areaId)
    {
        var area = current.ReadAreaTableReference(areaId)
            ?? throw AreaEconomyMissing("Area_table_receiver");
        int physical = AreaEconomyNeed(area.PhysicalSlot, "Area_identity");
        var text = new Dictionary<int, string>();
        var missing = new Dictionary<int, string>();
        var alignments = new Dictionary<int, int> { [31] = 0x01, [39] = 0x01, [47] = 0x01 };
        var gauges = new List<CityInformationGauge>();
        var development = new OwnedAreaDevelopmentContext(state, current, _normalAftermathInputs);
        var force = new Lazy<ForceReference>(() =>
        {
            int raw = AreaEconomyNeed(current.ReadAreaCurrentForceSlot(area), "Area_representative_Hex14");
            return current.ReadForceTableReference(raw <= 51 ? raw : 0)
                ?? throw AreaEconomyMissing("Area_Force_pointer");
        });
        var special = new Lazy<bool>(() =>
            AreaEconomyNeed(current.ReadForcePhysicalSlot(force.Value), "Area_Force_identity") is >= 46 and <= 50);
        var city = new Lazy<BaseReference>(() =>
        {
            int raw = AreaEconomyNeed(current.ReadAreaCitySlot(area), "Area35_City");
            return current.ReadCityTableReference(raw <= 51 ? raw : 0)
                ?? throw AreaEconomyMissing("Area_City_pointer");
        });
        var preview = new Lazy<AreaAdministrationPreviewSource>(() =>
        {
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            bool bound = _baseCapacityInputs.BindCurrent(state, current);
            return new(state, current, _scenario, Seed.Language, _baseCapacityInputs, bound);
        });
        var landmarks = new Lazy<(int Count, int Gold, int Food, int Troops)>(() =>
        {
            //32DF00 returns before the table loop for an invalid Area. Its
            //caller initialized all four outputs to zero before that call.
            if (!AreaEconomyNeed(current.ReadAreaNativeValidity(area), "landmark_Area_validity")) return (0, 0, 0, 0);
            int cityId = AreaEconomyNeed(current.ReadAreaCitySlot(area), "landmark_Area35");
            if (cityId == 0) return (0, 0, 0, 0);
            int owner = AreaEconomyNeed(current.ReadAreaCurrentForceSlot(area), "landmark_Area_Force");
            if (owner == 0) return (0, 0, 0, 0);
            //The common source32EAC0 reader's optional Area membership is the
            //sole difference here;22A800's actual trait/effect kernel is shared.
            return ReadCityInformationLandmarks(state, current, cityId, owner, physical);
        });

        ReadDevelopment(31, 0x63C, CeAreaDevelopmentField.Commerce);
        ReadDevelopment(39, 0x649, CeAreaDevelopmentField.Agriculture);
        ReadDevelopment(47, 0x653, CeAreaDevelopmentField.Barracks);
        Read(56, () => special.Value ? Data(0x353) :
            Number(AreaEconomyNeed(state.Areas.GetValueOrDefault(physical)?.NativeNormalAftermath?.Quantity52, "Area52_population")));
        Read(60, () => Number(Security()));
        try { gauges.Add(new(61, Security(), 100, 1)); }
        catch (InvalidDataException error) { missing[61] = error.Message; }
        ReadIncome(78, () => preview.Value.Gold(physical, AssignmentCount));
        ReadIncome(83, () => preview.Value.Food(physical, AssignmentCount));
        ReadIncome(88, () => preview.Value.Soldiers(physical, mode: 2));
        Read(103, () => Number(landmarks.Value.Count));
        Read(109, () => FormatSceneryPanelNumber(Data(0x862), landmarks.Value.Gold));
        Read(111, () => FormatSceneryPanelNumber(Data(0x862), landmarks.Value.Food));
        Read(113, () => FormatSceneryPanelNumber(Data(0x862), landmarks.Value.Troops));
        Read(127, () => Number(AreaEconomyNeed(current.ReadAreaFacilityCapacity(area), "Area6E_facility_capacity")));
        return new(new ReadOnlyDictionary<int, string>(text),
            new ReadOnlyDictionary<int, string>(missing))
        {
            Gauges = gauges.AsReadOnly(),
            TextAlignments = new ReadOnlyDictionary<int, int>(alignments)
        };

        string Data(int id) => ReadHistoryDataText(1, id);
        void Read(int node, Func<string> value)
        {
            try { text[node] = value(); }
            catch (InvalidDataException error) { missing[node] = error.Message; }
        }
        void ReadDevelopment(int node, int unit, CeAreaDevelopmentField field)
        {
            var display = new Lazy<CeAreaDevelopmentDisplay>(() =>
            {
                ushort raw = AreaEconomyNeed(development.ReadAreaProgress(area, field), "Area_current_" + field);
                var rules = _normalAftermathInputs.Development;
                int level = rules.ReadLevel(raw);
                if (level == 5) return new(level, null, null, true);
                int lower = level == 1 ? 0 : rules.LevelThresholds[level - 2];
                return new(level, raw - lower, rules.LevelThresholds[level - 1] - lower, true);
            });
            Read(node, () => FormatSceneryPanelNumber(Data(0x384), display.Value.Level) + Data(unit));
            Read(node + 1, () =>
            {
                if (special.Value)
                {
                    alignments[node + 1] = 0x10;
                    return Data(0x353);
                }
                return display.Value.Progress is int progress ? Number(progress) : Data(0x353);
            });
            Read(node + 2, () => special.Value ? "" : Data(0x60C));
            Read(node + 3, () => special.Value ? "" :
                display.Value.RequiredProgress is int required ? Number(required) : Data(0x353));
        }
        void ReadIncome(int node, Func<AreaAdministrationQuery<int?>> value)
            => Read(node, () =>
            {
                //35C8B0/35B020/363880 suppress a special Area owner before
                //checking City validity. An invalid City produces an empty label.
                if (special.Value) return Data(0x353);
                if (!AreaEconomyNeed(current.ReadBaseNativeValidity(city.Value), "income_City_validity")) return "";
                var query = value();
                return query.Available && query.Value is int scalar ? Number(scalar)
                    : throw AreaEconomyMissing(query.UnresolvedInput ?? "income_current_scalar");
            });
        int AssignmentCount(int person) => (current.ReadAssignmentPersonAreaVector(person)
            ?? throw AreaEconomyMissing("Person_task_vector")).ActiveCount1E8;
        int Security()
        {
            int value = state.Areas.GetValueOrDefault(physical)?.Security ?? throw AreaEconomyMissing("Area54_security");
            return value is >= 0 and <= byte.MaxValue ? value : throw AreaEconomyMissing("Area54_byte_domain");
        }
        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    private static T AreaEconomyNeed<T>(T? value, string field) where T : struct
        => value ?? throw AreaEconomyMissing(field);
    private static InvalidDataException AreaEconomyMissing(string field)
        => new("area-detail.economy." + field);
}
