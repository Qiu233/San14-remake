using San14.Core;

namespace San14.Data;

public sealed class PlayableNativePeriodContinuation(ArmyCampaignRuleAdapter campaign)
    : IPlayableNativePeriodContinuationRules
{
    public GameState CompleteWaiting(GameState state)
    {
        if (state.ReadArmyLifecycle()?.Indexes.ChangedProcessing is not null)
        {
            var changed = campaign.CompleteChangedTransportRoutes(state);
            if (changed.State is { } continued) state = continued;
            else if (OwnedCurrentPlayerForce.ReadRawForce3A(state) is byte player)
                state = state with { PeriodReports = [.. state.PeriodReports,
                    new("army_route_unresolved", player) { RuleId = changed.UnresolvedOwner }] };
        }
        return campaign.AdvancePlayableCalendar(state);
    }
}
