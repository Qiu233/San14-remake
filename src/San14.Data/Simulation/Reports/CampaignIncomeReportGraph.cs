using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record IncomeReportGraphPublication(int UpperBound, bool ActionPointsVisible,
    NativeMapSeasonDate Date, ImmutableDictionary<int, NormalTurnDistrictResourceHistory> Districts)
{
    public NormalTurnDistrictResourceHistory District(byte number) => Districts.GetValueOrDefault(number)
        ?? throw new InvalidDataException("income-report.current.graph_District_history");
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>67BAD0 reads the real ten-period writer, scanning slots1..9
    /// across all eight districts once when the report is constructed.</summary>
    public IncomeReportGraphPublication ReadIncomeReportGraph(CampaignIncomeReportPublication report)
    {
        var history = report.World.PlayerDistrictResourceHistory ?? throw IncomeMissing("graph_history");
        bool points = IncomeNeed(TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(report.TutorialControl),
            "graph_selected_lesson") == -1;
        int maximum = 0;
        for (int district = 1; district <= 8; district++)
        {
            var row = history.GetValueOrDefault(district) ?? throw IncomeMissing("graph_history_row");
            foreach (var series in new[] { row.Gold, row.Food, row.Troops, row.MilitaryIntelligence })
                if (series.IsDefault || series.Length != 10) throw IncomeMissing("graph_history_series");
            for (int slot = 1; slot < 10; slot++)
            {
                maximum = Math.Max(maximum, Math.Max(row.Gold[slot], Math.Max(row.Food[slot], row.Troops[slot])));
                if (points) maximum = Math.Max(maximum, row.MilitaryIntelligence[slot]);
            }
        }
        return new(unchecked(((maximum - 1) / 50000 + 1) * 50000), points,
            report.World.NativeMapSeason?.ThirdDate ?? throw IncomeMissing("graph_date"), history);
    }
}
