using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The normal host reaches this after the actual forty Army Progress blocks.
    //It does not replay planning, personnel task ticks or Army stages. A later
    //unowned Turn action fails the enclosing private operation atomically.
    internal GameState CompleteNormalTurnAfterArmyPeriodCurrent(GameState state)
    {
        var app = state.CampaignAppRandom32 ?? throw AppearanceTurnMissing("App32_current_state");
        //This is the latest operation's stream, including all Progress draws,
        //not the host's earlier Start/load snapshot. Saving it consumes no draw.
        var random = new SharedSourceRandom32(app.State);
        var mode = ReadApplicationGameplay()?.TutorialServicePresent switch
        {
            false => new TransferModeState(false, null),
            true => new TransferModeState(true, null),
            _ => null
        };
        //The ordinary application does not supply an achievement-service
        //bitmap. Keep that independent owner unknown for branches that read it.
        var result = TryAdvanceNormalTurnPrefix(state, state.Date, state.PeriodNumber,
            random, null, mode);
        return result.State ?? throw new InvalidDataException(result.UnresolvedOwner
            ?? "normal.turn.unresolved_action_" + result.NextAction);
    }
}
