using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private (string Title, string Description, string Supplement) ReadNormalTurnResultResourceText(GameState state, int eventId)
    {
        var data = _repository.LoadMessages(Seed.Language, MessageKind.Data).Messages.Sections[3].Records;
        string Text(int index) => (uint)index < (uint)data.Count
            ? HeadHistory(data[index].Text ?? throw NormalResultMissing("Data3_text")) : string.Empty;
        bool special;
        var world = state.CampaignEventWorld;
        switch (ReadApplicationGameplay()?.UserSelectionServicePresent)
        {
            case false: special = false; break;
            case true: special = (world?.CurrentUserIndex ?? throw NormalResultMissing("WorldBC")) != -1; break;
            default:
                if (world?.CurrentUserIndex != -1) throw NormalResultMissing("user_selection_service");
                special = false; break;
        }
        if (!special) return (Text(eventId * 4 + 1), Text(eventId * 4 + 3), Text(0x1F49 + eventId * 2));
        //1D2510 derives this directory from the actual saved signed World48,
        //not scenario filenames, the current user ordinal or an editor sample.
        int raw = world?.ScenarioId ?? throw NormalResultMissing("World48");
        int scenario = raw > 99 ? raw - 100 : -1;
        if (scenario > 49) scenario -= 45;
        return (Text(scenario * 200 + 0xFA5 + eventId * 4),
            Text(scenario * 200 + 0xFA7 + eventId * 4), string.Empty);
    }
}
