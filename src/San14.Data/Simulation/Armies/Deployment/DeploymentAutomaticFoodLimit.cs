using San14.Core;

namespace San14.Data;

/// <summary>The automatic draft's null-Army food limit. This is not the
/// consumption rule for a registered Army. Constants were extracted offline
/// from both language versions; current resources and settings remain owned.</summary>
internal static class DeploymentAutomaticFoodLimit
{
    private const int PredictionDuration = 30;
    private const int FoodLimitScale = 10;
    private const int ReferenceTroops = 1_000_000;
    private const int FoodExpenseNumerator = 40;
    private const int FoodExpenseDivisor = 30;
    private const int DurationGrowth = 3;
    private const int TerrainCorrectionPercent = 75;
    private const int AdvancedExpenseParameter = 41;

    internal static int Read(GameState state, SourceWorldContext current, BaseReference sourceBase)
    {
        if (!Need(current.ReadBaseNativeValidity(sourceBase), "source_base_validity")) return 0;

        SurfaceTypeReference terrain = current.ReadSurfaceTypeTableReference(1)
            ?? throw Missing("baseline_terrain_pointer");
        int terrainCost = ArmyRouteTerrainCost.Read(state, current, terrain);
        int basic = Math.Max(1, unchecked(ReferenceTroops * FoodExpenseNumerator) / FoodExpenseDivisor / 10);

        // Separate scalar binary32 operations follow the source's actual
        // ordering. In particular, a known zero terrain cost reaches NaN.
        float duration = PredictionDuration;
        float terrainDuration = (float)terrainCost * duration;
        float ratio = (float)unchecked(terrainCost * PredictionDuration) / terrainDuration;
        float correction = 1f - ratio;
        correction *= TerrainCorrectionPercent;
        correction /= 100f;
        float value = 1f - correction;
        value *= basic;
        float growth = DurationGrowth * duration;
        growth += 1000f;
        value *= growth;
        value /= 1000f;
        // All four modifiers are explicitly zero for a null Army. Keeping
        // these operations also preserves the source's signed-zero behavior.
        value -= 0f;
        value -= 0f;
        value -= 0f;
        value += 0f;
        int expense = Math.Max(0, Cvtt(value));

        int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_E58");
        if (selector == 0)
        {
            int percent = Need(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, AdvancedExpenseParameter),
                "advanced_food_expense_41");
            // This signed low32 product and division happen after the clamp;
            // the source does not clamp the result for a second time.
            expense = unchecked(percent * expense) / 100;
        }

        int food = Need(current.ReadBaseResource(sourceBase, BaseResourceKind.Supplies), "source_base_food");
        // The outer getter uses binary64, not the inner binary32 arithmetic.
        double perMillion = (double)expense / ReferenceTroops;
        double denominator = FoodLimitScale * perMillion;
        return Cvtt((double)food / denominator);
    }

    private static int Cvtt(float value) =>
        !float.IsFinite(value) || value < -2147483648f || value >= 2147483648f
            ? int.MinValue : (int)value;

    private static int Cvtt(double value) =>
        !double.IsFinite(value) || value < -2147483648.0 || value >= 2147483648.0
            ? int.MinValue : (int)value;

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("deployment.automatic_food_limit." + field);
}
