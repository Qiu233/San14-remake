using System.Collections.ObjectModel;
using System.Globalization;
using San14.Core;

namespace San14.Data;

internal sealed record ForceDetailEconomyPublication(
    IReadOnlyDictionary<int, string> Text,
    IReadOnlyDictionary<int, string> UnresolvedFields)
{
    public IReadOnlyDictionary<int, int> TextStyles { get; init; } = new Dictionary<int, int>();
    internal IReadOnlyDictionary<int, int> NumericValues { get; init; } = new Dictionary<int, int>();
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //906C50: one borrowed current, native Force foothold/Army iteration, and
    //the ordinary341B40 forecast. No ForceSummary or report totals substitute.
    internal ForceDetailEconomyPublication ReadForceDetailEconomyCurrent(GameState state,
        SourceWorldContext current, int forceId, IReadOnlySet<int>? requestedNodes = null)
    {
        GameState working = state;
        var force = current.ReadForceTableReference(forceId)
            ?? throw ForceEconomyMissing("Force_table_receiver");
        int key = ForceEconomyNeed(current.ReadForcePhysicalSlot(force), "Force_identity");
        bool special = key is >= 46 and <= 50;
        var text = new Dictionary<int, string>();
        var missing = new Dictionary<int, string>();
        var styles = new Dictionary<int, int>();
        var numericValues = new Dictionary<int, int>();
        var bases = new Lazy<BaseReference[]>(ReadBases);
        var armies = new Lazy<ArmyUnitReference[]>(ReadArmies);
        var areas = new Lazy<IReadOnlyList<AreaReference?>>(() =>
            current.ReadRegisteredAreaReferences() ?? throw ForceEconomyMissing("Area_registryB0"));
        var bound = new Lazy<bool>(() =>
        {
            BindPersonnelHireGenerationEconomyCurrent(working, current);
            return true;
        });
        var preview = new Lazy<AreaAdministrationPreviewSource>(() =>
        {
            _ = bound.Value;
            return new(working, current, _scenario, Seed.Language, _baseCapacityInputs, true);
        });
        var quotes = new Lazy<ForceEconomyCityQuote[]>(ReadCityQuotes);
        var association = new Lazy<int>(() =>
        {
            _ = bound.Value;
            return ReadHireCapitalAssociationGoldExpenseCurrent(working, current, force);
        });
        var goldExpense = new Lazy<int>(() => special ? int.MaxValue :
            unchecked(SumQuotes(q => q.GoldExpense) + association.Value));
        var goldIncome = new Lazy<int>(() => special ? int.MaxValue : SumQuotes(q => q.GoldIncome));
        var foodIncome = new Lazy<int>(() => special ? int.MaxValue : SumQuotes(q => q.FoodIncome));
        var foodExpense = new Lazy<int>(() => special ? int.MaxValue : SumQuotes(q => q.FoodExpense));

        //The original computes both signed nets before binding the visible
        //stock fields. Their341B40 quote may consume reached pending Army tails.
        Read(84, () => unchecked(goldIncome.Value - goldExpense.Value), signed: true);
        Read(105, () => unchecked(foodIncome.Value - foodExpense.Value), signed: true);
        Read(69, GoldStock, suppressSpecial: true);
        Read(74, () => goldIncome.Value, suppressSpecial: true);
        Read(79, () => goldExpense.Value, suppressSpecial: true);
        Read(90, FoodStock, suppressSpecial: true);
        Read(95, () => foodIncome.Value, suppressSpecial: true);
        Read(100, () => foodExpense.Value, suppressSpecial: true);
        Read(111, TroopStock);
        Read(116, () => SumQuotes(q => q.Recruitment), suppressSpecial: true);
        Read(121, WoundedStock);
        return new(new ReadOnlyDictionary<int, string>(text),
            new ReadOnlyDictionary<int, string>(missing))
        { TextStyles = new ReadOnlyDictionary<int, int>(styles),
            NumericValues = new ReadOnlyDictionary<int, int>(numericValues) };

        void Read(int node, Func<int> scalar, bool suppressSpecial = false, bool signed = false)
        {
            if (requestedNodes is not null && !requestedNodes.Contains(node)) return;
            try
            {
                if (suppressSpecial && special)
                {
                    text[node] = ReadHistoryDataText(1, 0x353);
                    styles[node] = 0;
                    return;
                }
                int value = scalar();
                numericValues[node] = value;
                text[node] = value.ToString(CultureInfo.InvariantCulture);
                styles[node] = signed && value < 0 ? 16 : 0;
            }
            catch (InvalidDataException error) { missing[node] = error.Message; }
        }

        BaseReference[] ReadBases()
        {
            //2296D0/3347B0: manager80 order, keys1..51, virtualB0 equals key.
            var registry = current.ReadRegisteredBases() ?? throw ForceEconomyMissing("Base_registry80");
            var result = new List<BaseReference>();
            if (key is < 1 or > 51) return [];
            foreach (var entry in registry)
            {
                var item = entry ?? throw ForceEconomyMissing("Base_registry_payload");
                if (ForceEconomyNeed(current.ReadBaseForceSlot(item), "Base_Force") == key) result.Add(item);
            }
            return result.ToArray();
        }

        ArmyUnitReference[] ReadArmies()
        {
            //222FD0/334740 additionally excludes233560 pending receivers.
            var registry = current.ReadRegistryReferences(RegistryKind.Army)
                ?? throw ForceEconomyMissing("Army_registry60");
            var result = new List<ArmyUnitReference>();
            if (key is < 1 or > 51) return [];
            foreach (var entry in registry)
            {
                if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                    throw ForceEconomyMissing("Army_registry_payload");
                var item = new ArmyUnitReference(physical);
                if (current.ReadArmyUnitRawForceSlot(item) != key) continue;
                if (ForceEconomyNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, physical),
                    "Army233560_excluded")) continue;
                result.Add(item);
            }
            return result.ToArray();
        }

        int GoldStock()
        {
            int sum = 0;
            foreach (var item in bases.Value)
                if (ForceEconomyNeed(current.ReadBaseObjectLink(item), "gold_Base4E") < 52)
                    sum = unchecked(sum + ForceEconomyNeed(current.ReadBaseResource(item, BaseResourceKind.Gold), "City34_Gold"));
            foreach (var item in armies.Value)
                sum = unchecked(sum + ForceEconomyNeed(current.ReadArmyReturnResourceFields(item)?.Gold5C, "Army5C_Gold"));
            return sum;
        }

        int FoodStock()
        {
            int sum = 0;
            foreach (var item in bases.Value)
                if (ForceEconomyNeed(current.ReadBaseObjectLink(item), "food_Base4E") < 52)
                    sum = unchecked(sum + ForceEconomyNeed(current.ReadBaseResource(item, BaseResourceKind.Supplies), "City38_Food"));
            return sum;
        }

        int TroopStock()
        {
            int sum = 0;
            foreach (var item in bases.Value)
                sum = unchecked(sum + ForceEconomyNeed(current.ReadBaseResource(item, BaseResourceKind.Troops), "Base3C_Troops"));
            foreach (var item in armies.Value)
            {
                //3503F0: ordinary Force Armies always count. A special-force
                //Army counts only when232860's actual commander-home gate holds.
                int armyForce = ForceEconomyNeed(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(item)),
                    "Army_Force_identity");
                if (armyForce is >= 46 and <= 50 && !current.IsArmyUnitBoundToSpecialForce(item)) continue;
                sum = unchecked(sum + ForceEconomyNeed(current.ReadArmyReturnResourceFields(item)?.Ready16, "Army16_Troops"));
            }
            return sum;
        }

        int WoundedStock()
        {
            int sum = 0;
            foreach (var item in bases.Value)
                sum = unchecked(sum + ForceEconomyNeed(current.ReadArmyReturnBaseWounded40(item), "Base40_Wounded"));
            foreach (var item in armies.Value)
                sum = unchecked(sum + ForceEconomyNeed(current.ReadArmyReturnResourceFields(item)?.Wounded18, "Army18_Wounded"));
            return sum;
        }

        ForceEconomyCityQuote[] ReadCityQuotes()
        {
            var result = new List<ForceEconomyCityQuote>();
            foreach (var city in bases.Value)
            {
                if (ForceEconomyNeed(current.ReadBaseObjectLink(city), "quote_Base4E") >= 52) continue;
                //3417B0 fills Gold/Food, then food expense and remaining-month
                //recruitment;341B40 next asks34CC40 for this City's gold expense.
                var gold = ForceEconomyQuote(() => Income(city, food: false));
                var food = ForceEconomyQuote(() => Income(city, food: true));
                var foodCost = ForceEconomyQuote(() =>
                {
                    _ = preview.Value;
                    var value = ReadCityCaptureFoodQuoteCurrent(working, current, city);
                    if (value.Value is not int expense)
                        throw ForceEconomyMissing(value.UnresolvedOwner ?? "City_food_quote");
                    working = value.State ?? working;
                    return expense;
                });
                var recruitment = ForceEconomyQuote(() =>
                {
                    var scalar = preview.Value;
                    int sum = 0;
                    foreach (int area in CityAreas(city)) sum = unchecked(sum + ForceEconomyScalar(scalar.Soldiers(area, mode: 2)));
                    return sum;
                });
                var goldCost = ForceEconomyQuote(() => ReadHireCityGoldExpense(working, current, city,
                    working.NativeMapSeason?.ThirdDate ?? throw ForceEconomyMissing("World34_date"), areas.Value, preview.Value));
                result.Add(new(gold, goldCost, food, foodCost, recruitment));
            }
            return result.ToArray();
        }

        IEnumerable<int> CityAreas(BaseReference city)
        {
            int id = ForceEconomyNeed(city.PhysicalSlot, "quote_City_identity");
            int owner = ForceEconomyNeed(current.ReadBaseForceSlot(city), "quote_City_Force");
            foreach (var entry in areas.Value)
            {
                var area = entry ?? throw ForceEconomyMissing("Area_registry_payload");
                if (ForceEconomyNeed(current.ReadAreaCitySlot(area), "Area35_City") != id ||
                    ForceEconomyNeed(current.ReadAreaCurrentForceSlot(area), "Area_Force") != owner) continue;
                yield return ForceEconomyNeed(area.PhysicalSlot, "Area_identity");
            }
        }

        int Income(BaseReference city, bool food)
        {
            int owner = ForceEconomyNeed(current.ReadBaseForceSlot(city), "income_City_Force");
            var cityForce = current.ReadForceTableReference(owner is >= 0 and <= 51 ? owner : 0)
                ?? throw ForceEconomyMissing("income_Force_pointer");
            if (!ForceEconomyNeed(current.ReadForceNativeValidity(cityForce), "income_Force_validity")) return 0;
            int sum = 0;
            var scalar = preview.Value;
            foreach (int area in CityAreas(city))
                sum = unchecked(sum + ForceEconomyScalar(food ? scalar.Food(area, AssignmentCount) : scalar.Gold(area, AssignmentCount)));
            int percent = ReadHireGoldLandmarkPercent(working, current, city, food ? 2 : 1);
            if (percent > 0 && sum > 0)
            {
                float basis = sum;
                sum = HireEconomyTruncate((percent / 100f) * basis + basis);
            }
            return sum;
        }

        int AssignmentCount(int physical) => (current.ReadAssignmentPersonAreaVector(physical)
            ?? throw ForceEconomyMissing("Person_task_vector")).ActiveCount1E8;
        int SumQuotes(Func<ForceEconomyCityQuote, AreaAdministrationQuery<int?>> select)
        {
            int sum = 0;
            foreach (var item in quotes.Value) sum = unchecked(sum + ForceEconomyScalar(select(item)));
            return sum;
        }
    }

    private sealed record ForceEconomyCityQuote(AreaAdministrationQuery<int?> GoldIncome,
        AreaAdministrationQuery<int?> GoldExpense, AreaAdministrationQuery<int?> FoodIncome,
        AreaAdministrationQuery<int?> FoodExpense, AreaAdministrationQuery<int?> Recruitment);
    private static AreaAdministrationQuery<int?> ForceEconomyQuote(Func<int> read)
    {
        try { return AreaAdministrationQuery<int?>.Known(read()); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<int?>.Unknown(error.Message); }
    }
    private static int ForceEconomyScalar(AreaAdministrationQuery<int?> query)
        => query.Available && query.Value is int value ? value
            : throw ForceEconomyMissing(query.UnresolvedInput ?? "City_quote_value");
    private static T ForceEconomyNeed<T>(T? value, string field) where T : struct
        => value ?? throw ForceEconomyMissing(field);
    private static InvalidDataException ForceEconomyMissing(string field)
        => new("force-detail.economy." + field);
}
