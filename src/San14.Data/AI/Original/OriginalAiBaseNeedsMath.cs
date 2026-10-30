namespace San14.Data.AI.Original;

internal readonly record struct OriginalAiFoodNeedInputs(int UsableTroops, int IncomingTroops,
    int FoodForecast, int CurrentFood, int IncomingFood);
internal readonly record struct OriginalAiGoldNeedInputs(int GoldForecast, int CurrentGold, int IncomingGold);

/// <summary>The signed32 operations of ordinary TC09E160/09E3C0. Forecasts
/// and needs are signed quantities; no stock clamp or rounding-up is applied.</summary>
internal static class OriginalAiBaseNeedsMath
{
    internal static int CalculateFood(OriginalAiFoodNeedInputs input, int troopConsumptionPercent,
        int supplyHorizon, int incomeHorizon)
    {
        int troops = unchecked(input.IncomingTroops + input.UsableTroops);
        int consumption = unchecked(troops * troopConsumptionPercent) / 100;
        int horizon = unchecked(consumption * supplyHorizon);
        int forecast = unchecked(input.FoodForecast / 3 * incomeHorizon);
        return unchecked(horizon - forecast - input.CurrentFood - input.IncomingFood);
    }

    internal static int CalculateGold(OriginalAiGoldNeedInputs input, int incomeHorizon) =>
        unchecked(-(input.CurrentGold + unchecked(incomeHorizon * (input.GoldForecast / 3)) + input.IncomingGold));

    /// <summary>The source normalizes only target categories5 and6. Every
    /// other category retains its raw target identity for this comparison.</summary>
    internal static int TransportTargetLocation(byte kind, ushort identity) => kind switch
    { 5 => identity <= 51 ? identity : 0,
      6 => identity is >= 1 and <= 10 ? identity + 51 : 0,
      _ => identity };
}
