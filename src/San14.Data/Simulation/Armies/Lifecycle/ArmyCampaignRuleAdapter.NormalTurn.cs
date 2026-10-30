using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The normal host reaches this after the actual forty Army Progress blocks.
    //It does not replay planning, personnel task ticks or Army stages. A later
    //unowned Turn action fails the enclosing private operation atomically.
    internal GameState CompleteNormalTurnAfterArmyPeriodCurrent(GameState state, CampaignGroupingCheckpoint grouping,
        ArmyMapGroupingOwner groups)
    {
        if (state.NativeNormalTurnContinuation is { } continuation)
        {
            if (continuation.NextAction is not (3 or 7 or 8 or 12 or 16 or 26 or 48 or 49) ||
                state.PersonDeathContinuation is not null ||
                continuation.PendingSelfRecommendation is not null || continuation.PendingCoalitionInvitation is not null)
                throw AppearanceTurnMissing("normal_turn_requires_player_decision");
            var season = state.NativeMapSeason ?? throw AppearanceTurnMissing("resume_calendar");
            var clock = state.NativeClock ?? throw AppearanceTurnMissing("resume_clock");
            var endDate = state.Date.Next();
            if (clock.UpdatesIntoPeriod != 40 || season.ProcessedNativeOrdinal != clock.UpdateOrdinal ||
                season.ThirdDate != new NativeMapSeasonDate(checked((short)endDate.Year), checked((sbyte)endDate.Month),
                    checked((sbyte)endDate.Day), 0) ||
                continuation.NextAction is 3 or 7 or 8 or 16 or 26 && season.ThirdDate.Day > 10 ||
                continuation.NextAction == 3 && season.ThirdDate.Month != 1)
                throw AppearanceTurnMissing("resume_completed_progress_boundary");
            var current = CaptureWritableTransportRouteCurrent(state);
            var pool = current.ReadOwnedCurrentListPool() ?? throw AppearanceTurnMissing("resume_shared_pool");
            BindNormalTurnPersonnelCurrent(state, current);
            int previousMonth = season.ThirdDate.Month == 1 ? 12 : season.ThirdDate.Month - 1;
            bool monthStart = season.ThirdDate.Day <= 10;
            bool seasonal = monthStart && MapSeasonMath.SelectMonth(season.ThirdDate.Month).SeasonIndex !=
                MapSeasonMath.SelectMonth(previousMonth).SeasonIndex;
            int firstAction;
            if (continuation.NextAction is 3 or 7 or 8)
            {
                var remaining = continuation.RemainingDeathPersonPhysicalSlots
                    ?? throw AppearanceTurnMissing("resume_death_remainder");
                //The selected ruler's death has already finished in Confirm.
                //Only the captured suffix belongs to this resumed list cut.
                state = state with { NativeNormalTurnContinuation = null };
                PersonArmyCaptureInputs.BindCurrent(state, current);
                var deaths = ConsumeNormalTurnDeathPeopleCurrent(state, current, pool, remaining);
                state = deaths.State;
                if (deaths.Remaining is { } nextPeople)
                    return PauseNormalTurnDeathsCurrent(state, current, pool, continuation.NextAction, nextPeople,
                        continuation.RestoreAppRandomState, grouping, groups);
                if (deaths.Stop)
                    return RestoreNormalTurnAppAfterExit(PublishNormalTurnCurrent(state, current, pool),
                        continuation.RestoreAppRandomState);
                var early = ApplyNormalTurnEarlyActionsCurrent(state, current, pool,
                    monthStart && season.ThirdDate.Month == 1, monthStart,
                    continuation.RestoreAppRandomState, grouping, groups,
                    continuation.NextAction == 3 ? 4 : continuation.NextAction == 7 ? 8 : 9);
                state = early.State;
                if (early.Stop) return RestoreNormalTurnAppAfterExit(state, continuation.RestoreAppRandomState);
                if (state.NativeNormalTurnContinuation is not null) return state;
                firstAction = 17;
            }
            else if (continuation.NextAction == 12)
            {
                state = state with { NativeNormalTurnContinuation = null };
                var early = ApplyNormalTurnEarlyActionsCurrent(state, current, pool,
                    monthStart && season.ThirdDate.Month == 1, monthStart, continuation.RestoreAppRandomState,
                    grouping, groups, firstAction: 12);
                state = early.State;
                if (early.Stop) return RestoreNormalTurnAppAfterExit(state, continuation.RestoreAppRandomState);
                if (state.NativeNormalTurnContinuation is not null) return state;
                firstAction = 17;
            }
            else if (continuation.NextAction == 16)
            {
                state = ApplyNormalTurnSelfRecommendationsCurrent(state, current, pool,
                    continuation.RestoreAppRandomState, grouping, continuation.RemainingOfficerIds);
                if (state.NativeNormalTurnContinuation is not null) return state;
                firstAction = 17;
            }
            else if (continuation.NextAction == 26)
            {
                //The explicit coalition answer already consumed action25.
                //Keep its App stream and begin at the next original action.
                state = state with { NativeNormalTurnContinuation = null };
                firstAction = 26;
            }
            else
            {
                if (continuation.StoryPlayback?.Story is { } story && story.NextStep < story.Steps.Length)
                    throw AppearanceTurnMissing("event_playback_requires_presentation");
                firstAction = continuation.NextAction;
                //The completed Story remains available to action48's true
                //occurrence/date tail. It is cleared only after that tail.
                if (firstAction == 49) state = state with { NativeNormalTurnContinuation = null };
            }
            state = ApplyNormalTurnRemainingActionsCurrent(state, current, pool, monthStart, seasonal,
                continuation.RestoreAppRandomState, grouping, firstAction, groups: groups);
            if (state.NativeNormalTurnContinuation is not null) return state;
            return RestoreNormalTurnAppAfterExit(state, continuation.RestoreAppRandomState);
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
            random, null, mode, grouping: grouping, normalGroups: groups);
        return result.State ?? throw new InvalidDataException(result.UnresolvedOwner
            ?? "normal.turn.unresolved_action_" + result.NextAction);
    }

    private GameState ApplyNormalTurnRemainingActionsCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, bool monthStart, bool seasonal, uint restoreApp,
        CampaignGroupingCheckpoint? grouping, int firstAction = 17, Action<int>? reached = null,
        Action<string, GameState>? observe = null, ArmyMapGroupingOwner? groups = null)
    {
        for (int action = firstAction; action <= 78; action++)
        {
            reached?.Invoke(action);
            //42BFE0 prepares +4 from333DB0(month start), +8 from333DE0
            //(season change).41/52 reuse the month-start guard; all other
            //reached actions through56 use the literal true guard4F8560.
            bool enabled = action <= 27 ? monthStart : action == 28 ? seasonal :
                action is 41 or 52 or 69 or 73 or 74 ? monthStart :
                action == 76 ? monthStart && currentDate().Month == 1 : true;
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
                    >= 31 and <= 38 => ApplyNormalTurnAction31To38Current(state, current, pool, action),
                    39 or 40 => ApplyNormalTurnEconomyCurrent(state, current, pool, action,
                        groups ?? throw AppearanceTurnMissing("food_grouping_owner")),
                    41 => state, //4F8560 is the action as well as its true result.
                    >= 42 and <= 47 => ApplyNormalTurnPostEconomyCurrent(state, current, pool, action, groups),
                    48 => ApplyNormalTurnStoryPlaybackCurrent(state, current, pool, restoreApp,
                        new(groups?.Export() ?? grouping?.Roots ?? throw AppearanceTurnMissing("story_grouping_owner"),
                            groups is not null || grouping!.Changed)),
                    >= 49 and <= 56 => ApplyNormalTurnLateMaintenanceCurrent(state, current, pool, action),
                    >= 57 and <= 60 => ApplyNormalTurnLateEconomyCurrent(state, current, pool, action),
                    >= 61 and <= 66 => ApplyNormalTurnClosingMaintenanceCurrent(state, current, pool, action, groups),
                    >= 67 and <= 72 => ApplyNormalTurnTerritoryFinalizationCurrent(state, current, pool, action),
                    >= 73 and <= 77 => ApplyNormalTurnFinalOrdersCurrent(state, current, pool, action),
                    78 => CompleteNormalTurnPostProcessCurrent(state, current, pool),
                    _ => throw AppearanceTurnMissing("remaining_action_dispatch")
                };
            if (state.CampaignTurnStop is not null)
                return PublishNormalTurnCurrent(state, current, pool);
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
        NativeMapSeasonDate currentDate() => state.NativeMapSeason?.ThirdDate
            ?? throw AppearanceTurnMissing("final_action_calendar");
    }

    private static GameState RestoreNormalTurnAppAfterExit(GameState state, uint saved)
        //CTurnState exit427B30 restores+14 only when it is nonzero.
        => saved == 0 ? state : state with { CampaignAppRandom32 = new(saved) };

    private static GameState PublishNormalTurnCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool)
    {
        state = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
        state = OwnedArmyLifecycleDetachedInputs.PublishCurrent(state, current);
        var lifecycle = state.ReadArmyLifecycle() ?? throw AppearanceTurnMissing("completion_lifecycle");
        return state.WithArmyLifecycle(lifecycle with { Indexes = pool.ExportOwnedState() });
    }

    private GameState CompleteNormalTurnPostProcessCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new NormalTurnPostProcessSource(this, state, current, pool).Run();
}
