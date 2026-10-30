using System.Collections.ObjectModel;
using System.Globalization;
using San14.Core;

namespace San14.Data;

internal sealed record DistrictDetailEconomyPublication(
    IReadOnlyDictionary<int, string> Text,
    IReadOnlyDictionary<int, string> UnresolvedFields)
{
    internal IReadOnlyDictionary<int,int> NumericValues { get; init; } = new Dictionary<int,int>();
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //906730's eleven economic fields.22DD20 is the District physical index,
    //not the affiliated Force identity. The caller lends the current capture.
    internal DistrictDetailEconomyPublication ReadDistrictDetailEconomyCurrent(GameState state,
        SourceWorldContext current, int districtId, IReadOnlySet<int>? requestedNodes = null)
    {
        GameState working = state;
        ForceUnregisterInputs.BindCurrent(working, current);
        var district = current.ReadDistrictTableReference(districtId)
            ?? throw DistrictEconomyMissing("District_table_receiver");
        int key = DistrictEconomyNeed(current.ReadDistrictPhysicalSlot(district), "District_identity");
        bool special = key is >= 46 and <= 50;
        var text = new Dictionary<int, string>();
        var numeric = new Dictionary<int,int>();
        var missing = new Dictionary<int, string>();
        var bases = new Lazy<BaseReference[]>(ReadBases);
        var cities = new Lazy<BaseReference[]>(ReadCities);
        Lazy<ArmyUnitReference[]> armies = new(ReadArmies);
        var areas = new Lazy<IReadOnlyList<AreaReference?>>(() =>
            current.ReadRegisteredAreaReferences() ?? throw DistrictEconomyMissing("Area_registryB0"));
        var preview = new Lazy<AreaAdministrationPreviewSource>(() =>
        {
            _personAssignmentPrecallInputs.BindCurrent(working, current);
            bool bound = _baseCapacityInputs.BindCurrent(working, current);
            return new(working, current, _scenario, Seed.Language, _baseCapacityInputs, bound);
        });
        var quotes = new Lazy<DistrictEconomyCityQuote[]>(ReadQuotes);
        var restricted = new Lazy<bool>(() =>
        {
            if (DistrictEconomyNeed(ReadApplicationInformationRestriction(working), "Application_user_selection")) return true;
            if (DistrictEconomyNeed(current.EventConditions.ScenarioMode, "Scenario_mode") == 3)
                throw DistrictEconomyMissing("CCCPersonSelectState_presence");
            return false;
        });

        //Keep906730's binding order: food stock precedes341950's reached
        //Army cleanup, while gold stock is queried after that forecast.
        ReadNumber(48, () => Stock(BaseResourceKind.Supplies), suppressSpecial: true);
        ReadNumber(53, () => SumQuotes(q => q.FoodIncome), suppressSpecial: true);
        ReadNumber(37, () => SumQuotes(q => q.GoldIncome), suppressSpecial: true);
        ReadNumber(32, () => Stock(BaseResourceKind.Gold), suppressSpecial: true);
        ReadNumber(58, () => SumQuotes(q => q.FoodExpense), suppressSpecial: true);
        ReadNumber(42, () => SumQuotes(q => q.GoldExpense), suppressSpecial: true);
        ReadNumber(64, () => Troops());
        ReadNumber(74, () => Wounded());
        Read(99, () =>
        {
            if (restricted.Value) return Data(0x353);
            if (!DistrictEconomyNeed(current.ReadDistrictNativeValidity(district), "District_validity")) return "";
            return special ? Data(0x353) : Number(ReadDistrictAdministrationIntelligenceCurrent(current, key));
        });
        Read(104, () =>
        {
            if (restricted.Value || DistrictEconomyNeed(current.ReadDistrictNumber(key), "District11_number") == 1)
                return Data(0x353);
            //3645D0 reads word28 bit100; no unrelated policy fields are needed.
            byte high = DistrictEconomyNeed(current.ReadDistrictRawByte(key, 0x29), "District29_military_enabled");
            return Data((high & 1) != 0 ? 0x27F : 0x280);
        });
        ReadNumber(69, () => SumQuotes(q => q.Recruitment), suppressSpecial: true);
        return new(new ReadOnlyDictionary<int, string>(text), new ReadOnlyDictionary<int, string>(missing))
        { NumericValues = new ReadOnlyDictionary<int,int>(numeric) };

        string Data(int id) => ReadHistoryDataText(1, id);
        void Read(int node, Func<string> value, bool suppressSpecial = false)
        {
            if(requestedNodes is not null && !requestedNodes.Contains(node))return;
            try { text[node] = suppressSpecial && special ? Data(0x353) : value(); }
            catch (InvalidDataException error) { missing[node] = error.Message; }
        }

        void ReadNumber(int node,Func<int> value,bool suppressSpecial=false) => Read(node,() =>
        {
            int actual=value();numeric[node]=actual;return Number(actual);
        },suppressSpecial);

        BaseReference[] ReadBases()
        {
            //229600/334590: manager80 source order, virtual78 equals key.
            var registry = current.ReadRegisteredBases() ?? throw DistrictEconomyMissing("Base_registry80");
            var result = new List<BaseReference>();
            if (key is < 1 or > 51) return [];
            foreach (var entry in registry)
            {
                var item = entry ?? throw DistrictEconomyMissing("Base_registry_payload");
                if (DistrictEconomyNeed(current.ReadBaseDistrictSlot(item), "Base_virtual78_District") == key) result.Add(item);
            }
            return result.ToArray();
        }

        BaseReference[] ReadCities()
        {
            //224300's separate fixed City pointer table, not manager80 and
            //not the set of all District footholds.334590 has no validity gate.
            var result = new List<BaseReference>();
            if (key is < 1 or > 51) return [];
            for (int slot = 0; slot < 52; slot++)
            {
                var item = current.ReadCityTableReference(slot) ?? throw DistrictEconomyMissing("City_fixed_table");
                if (DistrictEconomyNeed(current.ReadBaseDistrictSlot(item), "City_virtual78_District") == key) result.Add(item);
            }
            return result.ToArray();
        }

        ArmyUnitReference[] ReadArmies()
        {
            //222E60/334520:22BC70 reads Commander's actual PersonE0, then
            //233560 excludes pending receivers. No Force identity substitution.
            var registry = current.ReadRegistryReferences(RegistryKind.Army)
                ?? throw DistrictEconomyMissing("Army_registry60");
            var result = new List<ArmyUnitReference>();
            if (key is < 1 or > 51) return [];
            foreach (var entry in registry)
            {
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                    throw DistrictEconomyMissing("Army_registry_payload");
                var item = new ArmyUnitReference(physical);
                if (ReadFacilityHistoryDistrict(current, item) != key) continue;
                if (DistrictEconomyNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, physical),
                    "Army233560_excluded")) continue;
                result.Add(item);
            }
            return result.ToArray();
        }

        int Stock(BaseResourceKind kind)
        {
            int sum = 0;
            foreach (var item in bases.Value)
                if (DistrictEconomyNeed(current.ReadBaseObjectLink(item), "stock_Base4E") < 52)
                    sum = unchecked(sum + DistrictEconomyNeed(current.ReadBaseResource(item, kind), "City_" + kind));
            foreach (var item in armies.Value)
            {
                var resource = current.ReadArmyReturnResourceFields(item) ?? throw DistrictEconomyMissing("Army_current_resources");
                sum = unchecked(sum + DistrictEconomyNeed(kind == BaseResourceKind.Gold ? resource.Gold5C : resource.Supplies60,
                    kind == BaseResourceKind.Gold ? "Army5C_Gold" : "Army60_Food"));
            }
            return sum;
        }

        int Troops()
        {
            int sum = 0;
            foreach (var item in cities.Value)
                sum = unchecked(sum + DistrictEconomyNeed(current.ReadBaseResource(item, BaseResourceKind.Troops), "City3C_Troops"));
            foreach (var item in armies.Value)
            {
                int owner = DistrictEconomyNeed(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(item)), "Army_Force_identity");
                if (owner is >= 46 and <= 50 && !current.IsArmyUnitBoundToSpecialForce(item)) continue;
                sum = unchecked(sum + DistrictEconomyNeed(current.ReadArmyReturnResourceFields(item)?.Ready16, "Army16_Troops"));
            }
            return sum;
        }

        int Wounded()
        {
            int sum = 0;
            foreach (var item in cities.Value)
                sum = unchecked(sum + DistrictEconomyNeed(current.ReadArmyReturnBaseWounded40(item), "City40_Wounded"));
            foreach (var item in armies.Value)
                sum = unchecked(sum + DistrictEconomyNeed(current.ReadArmyReturnResourceFields(item)?.Wounded18, "Army18_Wounded"));
            return sum;
        }

        DistrictEconomyCityQuote[] ReadQuotes()
        {
            var result = new List<DistrictEconomyCityQuote>();
            foreach (var city in bases.Value)
            {
                if (DistrictEconomyNeed(current.ReadBaseObjectLink(city), "quote_Base4E") >= 52) continue;
                int raw = DistrictEconomyNeed(current.ReadBaseForceSlot(city), "quote_City_Force");
                var force = current.ReadForceTableReference(raw is >= 0 and <= 51 ? raw : 0)
                    ?? throw DistrictEconomyMissing("quote_City_Force_pointer");
                //The District range gate is independent of each City's Force.
                //341950 retains every special City's int.MaxValue components.
                if (DistrictEconomyNeed(current.ReadForcePhysicalSlot(force), "quote_City_Force_identity") is >= 46 and <= 50)
                {
                    var infinite = AreaAdministrationQuery<int?>.Known(int.MaxValue);
                    result.Add(new(infinite, infinite, infinite, infinite, infinite));
                    continue;
                }
                var gold = DistrictEconomyQuote(() => Income(city, food: false));
                var food = DistrictEconomyQuote(() => Income(city, food: true));
                var foodExpense = DistrictEconomyQuote(() =>
                {
                    _ = preview.Value;
                    var quote = ReadCityCaptureFoodQuoteCurrent(working, current, city);
                    working = quote.State ?? working;
                    //Food stock was read before this source quote. Its retained
                    //Army list cannot represent subsequent current iterators.
                    armies = new(ReadArmies);
                    if (quote.Value is not int amount) throw DistrictEconomyMissing(quote.UnresolvedOwner ?? "City_food_quote");
                    return amount;
                });
                var recruitment = DistrictEconomyQuote(() =>
                {
                    var scalar = preview.Value;
                    int sum = 0;
                    foreach (int area in CityAreas(city)) sum = unchecked(sum + DistrictEconomyScalar(scalar.Soldiers(area, mode: 2)));
                    return sum;
                });
                var goldExpense = DistrictEconomyQuote(() => ReadHireCityGoldExpense(working, current, city,
                    working.NativeMapSeason?.ThirdDate ?? throw DistrictEconomyMissing("World34_date"), areas.Value, preview.Value));
                result.Add(new(gold, goldExpense, food, foodExpense, recruitment));
            }
            return result.ToArray();
        }

        IEnumerable<int> CityAreas(BaseReference city)
        {
            int id = DistrictEconomyNeed(city.PhysicalSlot, "quote_City_identity");
            int owner = DistrictEconomyNeed(current.ReadBaseForceSlot(city), "quote_City_Force");
            foreach (var entry in areas.Value)
            {
                var area = entry ?? throw DistrictEconomyMissing("Area_registry_payload");
                if (DistrictEconomyNeed(current.ReadAreaCitySlot(area), "Area35_City") != id ||
                    DistrictEconomyNeed(current.ReadAreaCurrentForceSlot(area), "Area_Force") != owner) continue;
                yield return DistrictEconomyNeed(area.PhysicalSlot, "Area_identity");
            }
        }

        int Income(BaseReference city, bool food)
        {
            int raw = DistrictEconomyNeed(current.ReadBaseForceSlot(city), "income_City_Force");
            var force = current.ReadForceTableReference(raw is >= 0 and <= 51 ? raw : 0)
                ?? throw DistrictEconomyMissing("income_City_Force_pointer");
            if (!DistrictEconomyNeed(current.ReadForceNativeValidity(force), "income_Force_validity")) return 0;
            var scalar = preview.Value;
            int sum = 0;
            foreach (int area in CityAreas(city))
                sum = unchecked(sum + DistrictEconomyScalar(food ? scalar.Food(area, AssignmentCount) : scalar.Gold(area, AssignmentCount)));
            int percent = ReadHireGoldLandmarkPercent(working, current, city, food ? 2 : 1);
            if (percent > 0 && sum > 0)
            {
                float basis = sum;
                sum = HireEconomyTruncate((percent / 100f) * basis + basis);
            }
            return sum;
        }

        int SumQuotes(Func<DistrictEconomyCityQuote, AreaAdministrationQuery<int?>> select)
        {
            int sum = 0;
            foreach (var quote in quotes.Value) sum = unchecked(sum + DistrictEconomyScalar(select(quote)));
            //341950 already uses each City's34CC40. Unlike Force34CD90,
            //the District getter does not append another26BB30 here.
            return sum;
        }
        int AssignmentCount(int person) => (current.ReadAssignmentPersonAreaVector(person)
            ?? throw DistrictEconomyMissing("Person_task_vector")).ActiveCount1E8;
        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    private sealed record DistrictEconomyCityQuote(AreaAdministrationQuery<int?> GoldIncome,
        AreaAdministrationQuery<int?> GoldExpense, AreaAdministrationQuery<int?> FoodIncome,
        AreaAdministrationQuery<int?> FoodExpense, AreaAdministrationQuery<int?> Recruitment);
    private static AreaAdministrationQuery<int?> DistrictEconomyQuote(Func<int> read)
    {
        try { return AreaAdministrationQuery<int?>.Known(read()); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<int?>.Unknown(error.Message); }
    }
    private static int DistrictEconomyScalar(AreaAdministrationQuery<int?> query)
        => query.Available && query.Value is int value ? value
            : throw DistrictEconomyMissing(query.UnresolvedInput ?? "City_quote_value");
    private static T DistrictEconomyNeed<T>(T? value, string field) where T : struct
        => value ?? throw DistrictEconomyMissing(field);
    private static InvalidDataException DistrictEconomyMissing(string field)
        => new("district-detail.economy." + field);
}
