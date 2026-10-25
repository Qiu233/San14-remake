using San14.Core;

namespace San14.Data;

public sealed class PlayableNativePeriodContinuation(ArmyCampaignRuleAdapter campaign)
    : IPlayableNativePeriodContinuationRules, IPlayableNativeArmyPeriodRules, IPlayableNativeNormalTurnRules,
      INormalTurnSelfRecommendationRules, INormalTurnCoalitionInvitationRules
{
    private IPlayableNativeArmyProgressStageRules? completedArmyStages;
    private NativeArmyFoodTurnResult? completedFoodTurn;
    private bool completedNormalTurn;
    public bool UsesSourceFacilityWork => true;

    public GameState CompleteArmyPeriod(GameState state, IPlayableNativeSubdayRules waitingRules)
    {
        completedArmyStages = null;
        completedFoodTurn = null;
        completedNormalTurn = false;
        var completed = campaign.CompleteNativeArmyPeriod(state, waitingRules);
        if (completed.State.NativePeriodContinuation?.Cursor is null)
            completedArmyStages = completed.Stages;
        return completed.State;
    }

    public GameState CompleteNormalTurn(GameState completed)
    {
        if (completed.NativeNormalTurnContinuation is { } suspended)
        {
            completedArmyStages = campaign.RestoreCompletedNormalTurnArmyStages(completed, suspended.Grouping);
            completedFoodTurn = null;
            completedNormalTurn = false;
        }
        if (completedArmyStages is not { } stages || completed.NativePeriodContinuation?.Cursor is not null)
            throw new InvalidDataException("Native normal Turn requires completed Army Progress.");
        if (completedNormalTurn)
            throw new InvalidDataException("Native normal Turn has already completed for this Army period.");
        //Progress can itself reach the saved native terminal at an earlier
        //cycle. Its real stop does not execute the later normal Turn prefix.
        var result = completed.NativePlayableTerminal is not null ? completed
            : campaign.CompleteNormalTurnAfterArmyPeriodCurrent(completed,
                (stages as ICampaignStageOperation)?.CaptureCheckpoint()
                ?? throw new InvalidDataException("Normal Turn requires its actual Army grouping checkpoint."));
        if (result.CampaignTurnStop is not null || result.NativePlayableTerminal is not null)
        {
            //The real stop keeps its reached source fields. It does not
            //consume food or pretend that the later Turn owners completed.
            stages.PublishDerivedState(result);
            completedArmyStages = null;
            completedFoodTurn = null;
        }
        completedNormalTurn = result.NativeNormalTurnContinuation is null;
        return result;
    }

    public CommandResult ConfirmNormalTurnSelfRecommendation(GameState state,
        ConfirmSelfRecommendationOrder order, CommandContext context)
        => campaign.ConfirmNormalTurnSelfRecommendation(state, order, context);

    public CommandResult ConfirmNormalTurnCoalitionInvitation(GameState state,
        ConfirmCoalitionInvitationOrder order, CommandContext context)
        => campaign.ConfirmNormalTurnCoalitionInvitation(state, order, context);

    public GameState CompleteArmyFoodTurn(GameState state)
    {
        if (completedArmyStages is null) throw new InvalidDataException("Native food Turn requires completed Progress.");
        completedFoodTurn = campaign.CompleteNativeArmyFoodTurn(state, completedArmyStages);
        return completedFoodTurn.State;
    }

    public IReadOnlySet<int> CaptureArmiesAwaitingAppearance(GameState state)
        => campaign.CaptureArmiesAwaitingAppearance(state);

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

    public GameState CompleteMovement(GameState state)
    {
        if (completedArmyStages is { } stages)
        {
            //The actual cycle already consumed26/27, facilities and military
            //orders. Only the once-per-Turn governance tail remains here.
            //CTurn60 (42F7A0) observes completed Progress membership, once
            //per Turn. It is not an Army-cycle or calendar-tick callback.
            state = campaign.CompleteCampaignExpeditionsTurn(state);
            //Normal Turn30 already consumed3029A0/27AA40/2FF6C0 in its
            //original position. Do not reduce pending personnel words twice.
            var completed = state;
            stages.PublishDerivedState(completed);
            if (completedFoodTurn is not { } food) throw new InvalidDataException("Native food Turn has not completed.");
            campaign.PublishNativeFoodTurnGroups(food);
            completedFoodTurn = null;
            completedArmyStages = null;
            return completed;
        }
        state = campaign.CompletePlayableFacilityWork(state);
        // Source stage26 refreshes Base67 after retired Armies are excluded.
        // The coarse playable movement pass runs later than its waiting ticks,
        // so consume its final positions here, once, before report publication.
        var nearby = campaign.TryRefreshBaseNearbyArmies(state);
        state = nearby.State ?? throw new InvalidDataException(
            "Period base-neighbor refresh is unresolved: " + nearby.UnresolvedOwner);
        var pending = state.ReadArmyLifecycle()?.Indexes.PendingObjectRetirements
            ?? throw new InvalidDataException("Period object retirement queue is unresolved.");
        if (pending.Count != 0)
        {
            long revision = state.NativeMapSeason?.AdvancedCalendarSteps
                ?? throw new InvalidDataException("Period object retirement calendar is unresolved.");
            var retired = campaign.CompletePendingMapObjectRetirements(state, revision);
            state = retired.State ?? throw new InvalidDataException(
                "Period object retirement is unresolved: " + retired.UnresolvedOwner);
        }
        // One Turn action, after ordinary26/27; never a daily or two-hour tick.
        state = campaign.CompleteCampaignExpeditionsTurn(state);
        return campaign.CompletePlayableGovernanceTurn(state);
    }
}
