using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed record CitySizeDevelopmentApplication(GameState State, bool DevelopmentChanged);

    /// <summary>Adjust the actual City center's development for a size change.
    /// Caller owns the City size/resource writes and supplies the same current.
    /// Ordinary edited-event expansion keeps its growth-only admission.</summary>
    internal CitySizeDevelopmentApplication ApplyCitySizeDevelopmentCurrent(GameState state,
        SourceWorldContext current, BaseReference city, int before, int after, bool allowShrink)
    {
        if (city.Kind != BaseKind.City || before is < 0 or > 4 || after < 1 || allowShrink && after > 4)
            throw CitySizeDevelopmentMissing("City_size_scope");
        var area = CurrentGateMapQueries.FindArea(city, current);
        var writer = new OwnedAreaDevelopmentContext(state, current, _normalAftermathInputs);
        CeAreaDevelopmentField[] fields = [CeAreaDevelopmentField.Commerce,
            CeAreaDevelopmentField.Agriculture, CeAreaDevelopmentField.Barracks];
        ushort[] progress = fields.Select(field => CitySizeDevelopmentNeed(writer.ReadAreaProgress(area, field),
            "Area_progress")).ToArray();
        int[] original = progress.Select(CeAreaIncomeMath.Level).ToArray(), levels = original.ToArray();
        if (allowShrink && after < before)
        {
            int cap = ReadCitySizeShrinkLevelCap(state, after);
            while (unchecked(levels[0] + levels[1] + levels[2]) >= cap)
            {
                //Strict comparisons preserve the source tie priority:
                //Commerce, Agriculture, then Barracks.
                int selected = levels[0] < levels[1] ? 1 : 0;
                if (levels[2] > levels[selected]) selected = 2;
                if (levels[selected] <= 1) throw CitySizeDevelopmentMissing("shrink_level_cap");
                levels[selected]--;
            }
        }
        else if (before < after)
        {
            int required = after is 2 or 3 or 4 ? ReadEditedEventExpansionParameter(state, after, true) : 0;
            while (unchecked(levels[0] + levels[1] + levels[2]) < required)
            {
                int selected = levels[0] > levels[1] ? 1 : 0;
                if (levels[2] < levels[selected]) selected = 2;
                if (levels[selected] >= 5) throw CitySizeDevelopmentMissing("growth_level_requirement");
                levels[selected]++;
            }
        }
        int[] thresholds = [0, 100, 300, 600, 1000];
        bool changed = false;
        for (int index = 0; index < fields.Length; index++)
            if (levels[index] != original[index])
            {
                var write = _normalAftermathInputs.Development.WriteCurrentProgress(area,
                    fields[index], thresholds[levels[index] - 1], 1, writer);
                if (write.ChangedForceSlot is int force && write.ChangedForceWord1BE is short popularity)
                    current.SetRulerReleasePopularity1BE(force, popularity);
                changed = true;
            }
        return new(writer.State, changed);
    }

    private int ReadCitySizeShrinkLevelCap(GameState state, int size)
    {
        string name = size switch { 1 => "small", 2 => "medium", 3 => "large",
            _ => throw CitySizeDevelopmentMissing("shrink_size_parameter") };
        string key = "simulation.city-size-development." + name + "-shrink-level-cap";
        var bank = state.NativeArmySimulation?.Parameters ?? throw CitySizeDevelopmentMissing("shrink_parameters");
        if (bank.Float32Values.ContainsKey(key)) throw CitySizeDevelopmentMissing("shrink_parameter_type");
        if (bank.Int32Values.TryGetValue(key, out int value)) return value;
        if (Seed.Language == "SC")
        {
            //Independent SC2EC150 rule data: small6, medium9, large12.
            return size switch { 1 => 6, 2 => 9, _ => 12 };
        }
        if (Seed.Language != "TC") throw CitySizeDevelopmentMissing("shrink_locale_parameter");
        //TC2EC230's independently extracted three target-size caps.
        return size switch { 1 => 6, 2 => 9, _ => 12 };
    }

    private static T CitySizeDevelopmentNeed<T>(T? value, string field) where T : struct =>
        value ?? throw CitySizeDevelopmentMissing(field);
    private static InvalidDataException CitySizeDevelopmentMissing(string field) =>
        new("current.City_size_development." + field);
}
