using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //821940 writes byte4.bit2 after either actual Alliance effect branch.
    //82F780 only guides the still-open negotiation's confirmation control;
    //neither a presentation close nor an intermediate counteroffer owns34.
    private GameState RecordTutorialAllianceResolutionCurrent(GameState state)
    {
        var control = ReadTutorialControlState(state);
        if (TacticRemakeCurrentBattleControl.ReadHeader(control) is not int header || header == 0 ||
            TacticRemakeCurrentBattleControl.ReadBitmapPresence(control) != true) return state;
        return SetTutorialLessonEventCurrent(state, 34, true);
    }
}
