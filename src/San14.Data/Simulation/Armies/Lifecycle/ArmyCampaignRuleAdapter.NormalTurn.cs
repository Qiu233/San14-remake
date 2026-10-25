using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The normal host reaches this after the actual forty Army Progress blocks.
    //It does not replay planning, personnel task ticks or Army stages. A later
    //unowned Turn action fails the enclosing private operation atomically.
    internal GameState CompleteNormalTurnAfterArmyPeriodCurrent(GameState state, CampaignGroupingCheckpoint grouping)
    {
        if (state.NativeNormalTurnContinuation is { } continuation)
        {
            if (continuation.NextAction is not (16 or 26) ||
                continuation.PendingSelfRecommendation is not null || continuation.PendingCoalitionInvitation is not null)
                throw AppearanceTurnMissing("normal_turn_requires_player_decision");
            var season = state.NativeMapSeason ?? throw AppearanceTurnMissing("resume_calendar");
            var clock = state.NativeClock ?? throw AppearanceTurnMissing("resume_clock");
            var endDate = state.Date.Next();
            if (clock.UpdatesIntoPeriod != 40 || season.ProcessedNativeOrdinal != clock.UpdateOrdinal ||
                season.ThirdDate != new NativeMapSeasonDate(checked((short)endDate.Year), checked((sbyte)endDate.Month),
                    checked((sbyte)endDate.Day), 0) || season.ThirdDate.Day > 10)
                throw AppearanceTurnMissing("resume_completed_progress_boundary");
            var current = CaptureWritableTransportRouteCurrent(state);
            var pool = current.ReadOwnedCurrentListPool() ?? throw AppearanceTurnMissing("resume_shared_pool");
            BindNormalTurnPersonnelCurrent(state, current);
            int firstAction;
            if (continuation.NextAction == 16)
            {
                state = ApplyNormalTurnSelfRecommendationsCurrent(state, current, pool,
                    continuation.RestoreAppRandomState, grouping, continuation.RemainingOfficerIds);
                if (state.NativeNormalTurnContinuation is not null) return state;
                firstAction = 17;
            }
            else
            {
                //The explicit coalition answer already consumed action25.
                //Keep its App stream and begin at the next original action.
                state = state with { NativeNormalTurnContinuation = null };
                firstAction = 26;
            }
            int previousMonth = season.ThirdDate.Month == 1 ? 12 : season.ThirdDate.Month - 1;
            bool seasonal = MapSeasonMath.SelectMonth(season.ThirdDate.Month).SeasonIndex !=
                MapSeasonMath.SelectMonth(previousMonth).SeasonIndex;
            state = ApplyNormalTurnRemainingActionsCurrent(state, current, pool, true, seasonal,
                continuation.RestoreAppRandomState, grouping, firstAction);
            if (state.NativeNormalTurnContinuation is not null) return state;
            //The real App restoration belongs to the still-unimplemented Turn
            //tail. A later frontier must not publish or acknowledge completion.
            throw AppearanceTurnMissing("action.31");
        }
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
            random, null, mode, grouping: grouping);
        return result.State ?? throw new InvalidDataException(result.UnresolvedOwner
            ?? "normal.turn.unresolved_action_" + result.NextAction);
    }

    private GameState ApplyNormalTurnRemainingActionsCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, bool monthStart, bool seasonal, uint restoreApp,
        CampaignGroupingCheckpoint? grouping, int firstAction = 17, Action<int>? reached = null,
        Action<string, GameState>? observe = null)
    {
        for (int action = firstAction; action <= 30; action++)
        {
            reached?.Invoke(action);
            //42BFE0 prepares +4 from333DB0(month start), +8 from333DE0
            //(season change).29/30 use the literal true guard4F8560.
            bool enabled = action <= 27 ? monthStart : action == 28 ? seasonal : true;
            if (enabled)
                state = action switch
                {
                    17 => ApplyNormalTurnMonthlyDefectionsCurrent(state, current, pool),
                    18 => ApplyNormalTurnMonthlyFixedEmergenceCurrent(state, current, pool),
                    19 => ApplyNormalTurnMonthlyLoyaltyCurrent(state, current, pool),
                    20 => ApplyNormalTurnMonthlyGovernorIndependenceCurrent(state, current, pool),
                    21 => ApplyNormalTurnMonthlyIncursionsCurrent(state, current, pool),
                    22 => ApplyNormalTurnMonthlyGemRenewalCurrent(state, current),
                    23 => ApplyNormalTurnMonthlyCityExpansionCurrent(state, current, pool),
                    24 => ApplyNormalTurnMonthlyCoalitionExpiryCurrent(state, current, pool),
                    25 => ApplyNormalTurnMonthlyCoalitionFormationCurrent(state, current, pool, restoreApp, grouping),
                    26 => ApplyNormalTurnMonthlyEmperorEventsCurrent(state, current, pool),
                    27 => ApplyNormalTurnMonthlyItemEventsCurrent(state, current, pool),
                    28 => ApplyNormalTurnSeasonalForeignTradeCurrent(state, current),
                    29 => ApplyNormalTurnDisasterConsequencesCurrent(state, current, pool),
                    30 => GovernancePendingSource.ApplyCurrent(state, current, pool),
                    _ => throw AppearanceTurnMissing("remaining_action_dispatch")
                };
            if (state.NativeNormalTurnContinuation is not null)
            {
                //The original temporary lists have been released by action25.
                //Publish this exact current/pool before exposing its choice.
                state = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
                state = OwnedArmyLifecycleDetachedInputs.PublishCurrent(state, current);
                var lifecycle = state.ReadArmyLifecycle() ?? throw AppearanceTurnMissing("pause_lifecycle");
                state = state.WithArmyLifecycle(lifecycle with { Indexes = pool.ExportOwnedState() });
                observe?.Invoke($"turn.index{action}.waiting-for-player", state);
                return state;
            }
            observe?.Invoke($"turn.index{action}." + (enabled ? "completed" : "skipped"), state);
        }
        return state;
    }
}
