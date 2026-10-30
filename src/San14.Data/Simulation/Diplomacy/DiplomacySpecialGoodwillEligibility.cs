using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //6E05D0 admits special Forces through the actual kind1 Advantage predicate.
    //The active tutorial's byte6.bit6 is read from its bound controller, which
    //can advance independently of an open draft's business-world baseline.
    internal bool ReadSpecialGoodwillEligibilityCurrent(GameState state, SourceWorldContext current,
        int forceId, int recipientId)
    {
        var force = current.ReadForceTableReference(forceId)
            ?? throw DiplomacyUiSource.Missing("special_goodwill_Force");
        var app = ReadApplicationGameplay();
        if (app is not null)
            app = app with { TutorialAllowsSpecialGoodwill =
                TacticRemakeCurrentBattleControl.Read(ReadTutorialControlState(state), 54) };
        var result = PersonReturnPathSource.ReadSpecialPersonnelEligibility(state, current, force, recipientId, app);
        return result.Value ?? throw DiplomacyUiSource.Missing(result.UnresolvedOwner ?? "special_goodwill_Advantage");
    }
}
