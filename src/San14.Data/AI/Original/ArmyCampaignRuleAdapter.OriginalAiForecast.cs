using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The strategy-entry economic forecast pass. Quotes retain
    /// their actual lifecycle state, while planning needs and Force lists
    /// remain with their independent producers.</summary>
    internal GameState RefreshOriginalAiForecastsCurrent(GameState state, SourceWorldContext current)
    {
        _ = state.OriginalAiStrategyMemory ?? throw AiForecastMissing("saved_memory");
        BindPersonnelHireGenerationEconomyCurrent(state, current);
        var date = state.NativeMapSeason?.ThirdDate ?? throw AiForecastMissing("current_date");
        int month = date.Month + 1;
        if (month > 12) month = 1;
        if (month is < 1 or > 12) throw AiForecastMissing("next_month");
        //The income quote consumes next month as a scalar. This does not
        //advance World time or select payroll through an invented day.
        var incomeDate = date with { Month = checked((sbyte)month) };
        var working = state;
        var forecasts = new Dictionary<int, (int Gold, int Food)>();
        foreach (var entry in current.ReadRegisteredBases() ?? throw AiForecastMissing("Base_registry"))
        {
            var at = entry ?? throw AiForecastMissing("Base_pointer");
            int location = AiForecastKnown(current.ReadBaseObjectLink(at), "Base_location");
            if (location is < 1 or > 61) throw AiForecastMissing("Base_location_identity");
            if (!(working.OriginalAiStrategyMemory ?? throw AiForecastMissing("saved_memory"))
                .Footholds.ContainsKey(location)) throw AiForecastMissing("saved_foothold_" + location);

            int gold, food;
            if (location < 52)
            {
                //The source quotes the City receiver resolved from Base4E.
                var city = current.ReadCityTableReference(location) ?? throw AiForecastMissing("City_pointer");
                int cost1Gold = ReadProposalCityGoldExpense(working, current, city, true);
                int cost1Food = FoodExpense(city);
                var income1 = ReadProposalCityIncomeQuote(working, current, city, incomeDate);
                int cost0Gold = ReadProposalCityGoldExpense(working, current, city, false);
                int cost0Food = FoodExpense(city);
                //2BC080(includeRegular=0,quote=1) has zero gold/food outputs.
                gold = unchecked(unchecked(-cost0Gold * 2) - cost1Gold + income1.Gold);
                food = unchecked(unchecked(-cost0Food * 2) - cost1Food + income1.Food);
            }
            else
            {
                //229FD0 returns the Area at this Base's actual Object Hex.
                //The Area's City35 is independent of the Gate's mapped City.
                int rawHex = current.ReadBaseCurrentHexIndex(at);
                var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                    ?? throw AiForecastMissing("Gate_Hex_pointer");
                int rawArea = hex.PhysicalSlot is null ? 0
                    : AiForecastKnown(current.ReadHexAreaSlot(hex), "Gate_Hex_Area");
                var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0)
                    ?? throw AiForecastMissing("Gate_Area_pointer");
                int citySlot = AiForecastKnown(current.ReadAreaCitySlot(area), "Gate_Area_City");
                var city = current.ReadCityTableReference(citySlot <= 51 ? citySlot : 0)
                    ?? throw AiForecastMissing("Gate_Area_City_pointer");
                int areaGold = 0, areaFood = 0;
                if (AiForecastKnown(current.ReadBaseNativeValidity(city), "Gate_Area_City_validity"))
                {
                    int cityForce = AiForecastKnown(current.ReadBaseForceSlot(city), "Gate_Area_City_Force");
                    if (AiForecastKnown(current.ReadAreaCurrentForceSlot(area), "Gate_Area_Force") == cityForce)
                    {
                        var force = current.ReadForceTableReference(cityForce <= 51 ? cityForce : 0)
                            ?? throw AiForecastMissing("Gate_Area_Force_pointer");
                        var abilities = CreateArmyDestructionAbilities(current);
                        areaGold = ReadHireAreaGoldIncomeCurrent(working, current, area, city, abilities,
                            person => AiForecastKnown(current.ReadAssignmentPersonAreaVector(person)?.ActiveCount1E8,
                                "Gate_Area_Person_tasks"));
                        areaFood = CityFoodIncomeSource.ReadArea(working, current, area, city, force, month, abilities);
                    }
                }
                //2DC710 returns before touching outputs for Gate4E52..61;
                //both caller-cleared expense packets therefore remain zero.
                gold = unchecked(areaGold * 3);
                food = unchecked(areaFood * 3);
            }
            forecasts[location] = (gold, food);
        }

        var memory = working.OriginalAiStrategyMemory ?? throw AiForecastMissing("saved_memory");
        var rows = memory.Footholds.ToBuilder();
        foreach (var (location, forecast) in forecasts)
        {
            if (!rows.TryGetValue(location, out var row)) throw AiForecastMissing("saved_foothold_" + location);
            rows[location] = row with { GoldForecast = forecast.Gold, FoodForecast = forecast.Food };
        }
        return working with { OriginalAiStrategyMemory = memory with { Footholds = rows.ToImmutable() } };

        int FoodExpense(BaseReference city)
        {
            var quote = ReadCityCaptureFoodQuoteCurrent(working, current, city);
            int value = quote.Value ?? throw AiForecastMissing(quote.UnresolvedOwner ?? "City_food_quote");
            working = quote.State ?? working;
            return value;
        }
    }

    private static T AiForecastKnown<T>(T? value, string field) where T : struct =>
        value ?? throw AiForecastMissing(field);
    private static InvalidDataException AiForecastMissing(string field) => new("original_ai.forecast." + field);
}
