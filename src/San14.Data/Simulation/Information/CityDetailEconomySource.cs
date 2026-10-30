using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using San14.Core;

namespace San14.Data;

internal sealed record CityDetailEconomyPublication(
    IReadOnlyDictionary<int, string> Text,
    IReadOnlyDictionary<int, string> UnresolvedFields)
{
    internal IReadOnlyDictionary<int, int> NumericValues { get; init; } = new Dictionary<int, int>();
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //905A10's ordinary and retained-report branches share the page, but do
    //not share the five income getters. The caller supplies the real context.
    internal CityDetailEconomyPublication ReadCityDetailEconomyCurrent(GameState state,
        SourceWorldContext current, int cityId, bool reportContext = false)
    {
        var text = new Dictionary<int, string>();
        var missing = new Dictionary<int, string>();
        var numeric = new Dictionary<int, int>();
        var city = current.ReadCityTableReference(cityId)
            ?? throw CityDetailEconomyMissing("City_table_receiver");
        var owner = state.Cities.GetValueOrDefault(cityId)
            ?? throw CityDetailEconomyMissing("City_owner");
        var areas = new Lazy<IReadOnlyList<AreaReference?>>(() =>
            current.ReadRegisteredAreaReferences() ?? throw CityDetailEconomyMissing("Area_registryB0"));
        var preview = new Lazy<AreaAdministrationPreviewSource>(() =>
        {
            BindPersonnelHireGenerationEconomyCurrent(state, current);
            return new(state, current, _scenario, Seed.Language, _baseCapacityInputs, true);
        });
        var special = new Lazy<bool>(() =>
        {
            int raw = CityEconomyNeed(current.ReadBaseForceSlot(city), "City_Force");
            var force = current.ReadForceTableReference(raw is >= 0 and <= 51 ? raw : 0)
                ?? throw CityDetailEconomyMissing("City_Force_pointer");
            return CityEconomyNeed(current.ReadForcePhysicalSlot(force), "City_Force_identity") is >= 46 and <= 50;
        });

        ReadNumber(100, () => reportContext
            ? (owner.TurnIncomeTotals ?? throw CityDetailEconomyMissing("City_D0_income")).Gold
            : ReadAreaIncome(food: false), suppressSpecial: !reportContext);
        ReadNumber(105, () => reportContext
            ? CityEconomyNeed(current.ReadBaseReturnField50(city), "Base50_expenditure")
            : ReadGoldExpense(), suppressSpecial: !reportContext);
        ReadNumber(116, () => reportContext
            ? (owner.TurnIncomeTotals ?? throw CityDetailEconomyMissing("City_D4_income")).Food
            : ReadAreaIncome(food: true), suppressSpecial: !reportContext);
        ReadNumber(121, () => reportContext
            ? CityEconomyNeed(owner.PeriodFoodExpenditure, "Base54_expenditure")
            : ReadFoodExpense(), suppressSpecial: !reportContext);
        ReadNumber(134, () => reportContext
            ? (owner.TurnIncomeTotals ?? throw CityDetailEconomyMissing("City_D8_recruitment")).RecruitedTroops
            : ReadRecruitment(), suppressSpecial: !reportContext);

        Read(166, () => DutyName(recruiter: true));
        Read(174, () => DutyName(recruiter: false));
        Read(183, () => ReadHistoryDataText(1, special.Value ||
            (CityEconomyNeed(state.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(cityId)?.C9, "CityC9_merchant") & 1) == 0
                ? 0x339 : 0x338));
        Read(188, () => MerchantPrice(sell: false));
        Read(193, () => MerchantPrice(sell: true));
        return new(new ReadOnlyDictionary<int, string>(text),
            new ReadOnlyDictionary<int, string>(missing))
        { NumericValues = new ReadOnlyDictionary<int, int>(numeric) };

        void Read(int node, Func<string> value)
        {
            try { text[node] = value(); }
            catch (InvalidDataException error) { missing[node] = error.Message; }
        }
        void ReadNumber(int node, Func<int> value, bool suppressSpecial)
            => Read(node, () =>
            {
                if (suppressSpecial && special.Value) return ReadHistoryDataText(1, 0x353);
                int actual = value();
                numeric[node] = actual;
                return actual.ToString(CultureInfo.InvariantCulture);
            });

        int AssignmentCount(int physical) => (current.ReadAssignmentPersonAreaVector(physical)
            ?? throw CityDetailEconomyMissing("Person_task_vector")).ActiveCount1E8;

        IEnumerable<int> CityAreas()
        {
            int force = CityEconomyNeed(current.ReadBaseForceSlot(city), "City_Force");
            foreach (var entry in areas.Value)
            {
                var area = entry ?? throw CityDetailEconomyMissing("Area_pointer");
                if (CityEconomyNeed(current.ReadAreaCitySlot(area), "Area35_City") != cityId ||
                    CityEconomyNeed(current.ReadAreaCurrentForceSlot(area), "Area_Force") != force) continue;
                yield return CityEconomyNeed(area.PhysicalSlot, "Area_identity");
            }
        }

        int ReadAreaIncome(bool food)
        {
            //22D9E0/2BC080 returns before the Area loop for an invalid Force.
            if (CityEconomyNeed(current.ReadBaseObjectLink(city), "City4E") > 51) return 0;
            int raw = CityEconomyNeed(current.ReadBaseForceSlot(city), "income_City_Force");
            var force = current.ReadForceTableReference(raw is >= 0 and <= 51 ? raw : 0)
                ?? throw CityDetailEconomyMissing("income_Force_pointer");
            if (!CityEconomyNeed(current.ReadForceNativeValidity(force), "income_Force_validity")) return 0;
            int sum = 0;
            var scalar = preview.Value;
            foreach (int area in CityAreas())
                sum = unchecked(sum + CityEconomyQuery(food ? scalar.Food(area, AssignmentCount) : scalar.Gold(area, AssignmentCount)));
            int percent = ReadHireGoldLandmarkPercent(state, current, city, food ? 2 : 1);
            if (percent > 0 && sum > 0)
            {
                float basis = sum;
                sum = HireEconomyTruncate((percent / 100f) * basis + basis);
            }
            return sum;
        }

        int ReadGoldExpense()
        {
            var scalar = preview.Value;
            return ReadHireCityGoldExpense(state, current, city,
                state.NativeMapSeason?.ThirdDate ?? throw CityDetailEconomyMissing("World34_date"), areas.Value, scalar);
        }

        int ReadFoodExpense()
        {
            _ = preview.Value;
            //22FE10 ->2DC710(...,1): the same quote-only food result used by
            //normal Turn and capture, including HomeArmy and cleanup tails.
            var quote = ReadCityCaptureFoodQuoteCurrent(state, current, city);
            return quote.Value ?? throw CityDetailEconomyMissing(quote.UnresolvedOwner ?? "food_quote");
        }

        int ReadRecruitment()
        {
            if (CityEconomyNeed(current.ReadBaseObjectLink(city), "recruitment_City4E") > 51)
                throw CityDetailEconomyMissing("recruitment_nonCity_receiver");
            var scalar = preview.Value;
            int sum = 0;
            foreach (int area in CityAreas()) sum = unchecked(sum + CityEconomyQuery(scalar.Soldiers(area, mode: 2)));
            return sum;
        }

        string DutyName(bool recruiter)
        {
            if (CityEconomyNeed(current.ReadBaseObjectLink(city), "duty_City4E") >= 52) return "";
            ushort raw = CityEconomyNeed(recruiter ? current.ReadAssignmentCityCA(cityId)
                : current.ReadAssignmentCityCC(cityId), recruiter ? "CityCA_recruiter" : "CityCC_trainer");
            if (raw == 0) return "";
            var person = current.ReadPersonLookupReference(raw <= 6000 ? raw : 0)
                ?? throw CityDetailEconomyMissing("duty_Person_lookup");
            int physical = CityEconomyNeed(person.PhysicalSlot, "duty_Person_receiver");
            int id = CityEconomyNeed(current.ReadPersonIdentity(physical), "duty_Person_identity");
            var officer = state.Officers.GetValueOrDefault(id)
                ?? throw CityDetailEconomyMissing("duty_Person_current_name");
            //356350/366BA0 call22F020 directly, without a validity gate.
            return (officer.CurrentSurname ?? throw CityDetailEconomyMissing("duty_Person_surname")) +
                (officer.CurrentGivenName ?? throw CityDetailEconomyMissing("duty_Person_given_name"));
        }

        string MerchantPrice(bool sell)
        {
            if (special.Value) return ReadHistoryDataText(1, 0x353);
            int price = CityEconomyNeed(owner.MerchantFoodPerTenGold, "CityC8_merchant_price");
            if (sell) price = unchecked(price * CityMerchantSource.SellPricePercent) / 100;
            return FormatCityDetailPrice(ReadHistoryDataText(1, 0x3BC), 10, price);
        }
    }

    //196CC0's two signed decimal operands. Other resource directives remain
    //unresolved instead of becoming a guessed localized price string.
    private static string FormatCityDetailPrice(string format, int gold, int food)
    {
        int[] values = [gold, food];
        int argument = 0;
        var result = new StringBuilder();
        for (int i = 0; i < format.Length; i++)
        {
            if (format[i] != '%') { result.Append(format[i]); continue; }
            if (++i >= format.Length) throw CityDetailEconomyMissing("Data1_3BC_format");
            if (format[i] == '%') { result.Append('%'); continue; }
            if (format[i] != 'd' || argument >= values.Length)
                throw CityDetailEconomyMissing("Data1_3BC_format");
            result.Append(values[argument++].ToString(CultureInfo.InvariantCulture));
        }
        if (argument != values.Length) throw CityDetailEconomyMissing("Data1_3BC_operand_count");
        return result.ToString();
    }

    private static T CityEconomyNeed<T>(T? value, string field) where T : struct
        => value ?? throw CityDetailEconomyMissing(field);
    private static int CityEconomyQuery(AreaAdministrationQuery<int?> value)
        => value.Available && value.Value is int known ? known
            : throw CityDetailEconomyMissing(value.UnresolvedInput ?? "Area_preview_value");
    private static InvalidDataException CityDetailEconomyMissing(string field)
        => new("city-detail.economy." + field);
}
