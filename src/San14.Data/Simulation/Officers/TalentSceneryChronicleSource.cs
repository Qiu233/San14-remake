using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1DC420 constructs14F/6F and calls8C8D50 at this source point. The
    //chronology is its actual persistent owner, not a popup acknowledgement.
    private GameState AppendTalentSceneryChronicleCurrent(GameState state, SourceWorldContext current,
        SceneryReference scenery, ForceReference capturedForce, int executor, string sceneryName)
    {
        int day = (ReadArmyDestructionSampler(state, current)
            ?? throw SearchOutcomeMissing("Scenery_news_day450")).Day450;
        string main = HistoryWriter.CustomFirstName(state, current, capturedForce, Name);
        int color = SearchNeed(current.ReadForceReportColor(capturedForce), "Scenery_news_Force49");
        int hex = SearchNeed(current.ReadAreaHexIndex(TalentSceneryArea(current, scenery)), "Scenery_news_Area3A");
        var force = current.ReadPersonForce(executor) ?? throw SearchOutcomeMissing("Scenery_news_executor_Force");
        string forceLabel = HistoryWriter.FallbackName(state, current, force, Name);
        return new CampaignChronicleWriter(HistoryWriter).Write(state, current,
            new(0x14F, 1, day, main, color, 0x6F,
                ImmutableArray.Create(forceLabel, sceneryName, string.Empty),
                ImmutableArray.Create(hex, 48400, 48400, 48400)));

        static string Name(OfficerState person, bool full)
        {
            string surname = Head(person.CurrentSurname ?? throw SearchOutcomeMissing("Scenery_news_ruler_surname"));
            return full ? surname + Head(person.CurrentGivenName ?? throw SearchOutcomeMissing("Scenery_news_ruler_given_name")) : surname;
        }
        static string Head(string value) => value.IndexOf('\0') is int end && end >= 0 ? value[..end] : value;
    }
}
