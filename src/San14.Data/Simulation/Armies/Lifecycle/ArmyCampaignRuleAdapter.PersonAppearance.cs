using San14.Core;

namespace San14.Data;

internal sealed record PersonAppearanceTurnResult(GameState? State, string? UnresolvedOwner,
    int NextAction, uint? SavedApp32);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The reached normal Turn prefix, not another six-hour stage.
    /// A missing later action discards this entire operation. No host install,
    /// normal-exit restoration, date finalizer or App acknowledgement occurs.</summary>
    internal PersonAppearanceTurnResult TryAdvancePersonAppearanceTurnPrefix(GameState state,
        CampaignNormalPeriodIntent intent, ICampaignAppRandomBinding? appRandom,
        TransferServiceState? transferService, TransferModeState? transferMode,
        Action<string, GameState>? observe = null)
        => TryAdvanceNormalTurnPrefix(state, intent.StartDate, intent.StartPeriodNumber,
            appRandom, transferService, transferMode, observe);

    private PersonAppearanceTurnResult TryAdvanceNormalTurnPrefix(GameState state,
        PeriodDate startDate, int startPeriodNumber, ICampaignAppRandomBinding? appRandom,
        TransferServiceState? transferService, TransferModeState? transferMode,
        Action<string, GameState>? observe = null, CampaignGroupingCheckpoint? grouping = null,
        ArmyMapGroupingOwner? normalGroups = null)
    {
        uint? saved = null;
        int next = 0;
        try
        {
            var season = state.NativeMapSeason ?? throw AppearanceTurnMissing("calendar");
            var clock = state.NativeClock ?? throw AppearanceTurnMissing("clock");
            var expected = startDate.Next();
            if (expected.Year is < short.MinValue or > short.MaxValue)
                throw AppearanceTurnMissing("completed_period_year_profile");
            if (state.Date != startDate || state.PeriodNumber != startPeriodNumber ||
                state.Phase != TurnPhase.Strategy || clock.AnchorDate != startDate || clock.UpdatesIntoPeriod != 40 ||
                season.ProcessedNativeOrdinal != clock.UpdateOrdinal || season.ThirdDate != new NativeMapSeasonDate(
                    checked((short)expected.Year), checked((sbyte)expected.Month), checked((sbyte)expected.Day), 0) ||
                state.NativeNormalArmyCombatCalendar?.NextCalendarCycleOrdinal != season.AdvancedCalendarSteps)
                throw AppearanceTurnMissing("completed_native_period_boundary");
            MapSeasonRuntime.ValidateOwned(season);

            // The three pure preparation predicates use the captured current
            // ThirdDate. They do not advance it or reread a Core period date.
            var date = season.ThirdDate;
            bool monthStart = date.Day <= 10;
            bool annual = monthStart && date.Month == 1;
            bool seasonal = false;
            if (monthStart)
            {
                if (date.Month is < 1 or > 12) throw AppearanceTurnMissing("prepared_month");
                int previousMonth = date.Month == 1 ? 12 : date.Month - 1;
                seasonal = MapSeasonMath.SelectMonth(date.Month).SeasonIndex !=
                    MapSeasonMath.SelectMonth(previousMonth).SeasonIndex;
            }
            var current = CaptureWritableTransportRouteCurrent(state);
            var pool = current.ReadOwnedCurrentListPool() ?? throw AppearanceTurnMissing("shared_pool");
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            PersonReturnCommands.BindCurrent(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            OwnedArmyReturnServiceInputs.BindCurrent(state, current);
            // These are explicit existing campaign bindings, not inferred from
            // a CPU host or a fabricated fresh service. Eligibility stays lazy.
            current.TransferService = transferService;
            current.ArmyReturnTransferMode = transferMode;
            current.TransferModeStateReader = () => transferMode;
            uint flags = current.ReadWorldTransferServiceFlags() ?? throw AppearanceTurnMissing("admission_World16A8");
            if ((flags & 0x100) != 0 && seasonal)
                throw AppearanceTurnMissing("admission_external_wait");
            Observe("turn.admitted");

            // index0's real guard is the seasonal flag and its action is pure1.
            next = 1;
            Observe(seasonal ? "turn.index0.executed" : "turn.index0.skipped");
            // The source getter is reached even if no later draw is needed.
            // A historical save/Start snapshot cannot substitute for the host.
            saved = (appRandom ?? throw AppearanceTurnMissing("App32_binding")).ReadCurrentState();
            var world = Context(current).ReadCurrentSamplerWorld(state)
                ?? throw AppearanceTurnMissing("current_Salt45C");
            var freshDate = state.NativeMapSeason?.ThirdDate ?? throw AppearanceTurnMissing("index1_ThirdDate");
            uint seed = unchecked((uint)(world.Salt45C + PersonReturnCommands.CalendarOrdinal(freshDate)));
            state = state with { CampaignAppRandom32 = new(seed) };
            Observe("turn.index1.seeded");
            next = 2;
            if (annual)
            {
                state = new PersonAppearanceSource(this, state, current, pool, observe).Run();
                Observe("turn.index2.completed");
            }
            else Observe("turn.index2.skipped");
            next = 3;
            if (annual)
            {
                state = new AnnualPersonLifespanSource(this, state, current, pool, observe).Run();
                Observe("turn.index3.completed");
            }
            else Observe("turn.index3.skipped");
            next = 4;
            if (annual)
            {
                state = new AnnualPersonLifespanNotificationSource(this, state, current, pool, observe).Run();
                Observe("turn.index4.completed");
            }
            else Observe("turn.index4.skipped");
            next = 5;
            if (annual)
            {
                state = new AnnualPersonPortraitTransitionSource(state, current, observe).Run();
                Observe("turn.index5.completed");
            }
            else Observe("turn.index5.skipped");
            next = 6;
            if (monthStart)
            {
                state = new MonthlyCampaignMaintenanceSource(this, state, current, pool, observe).Run();
                Observe("turn.index6.completed");
            }
            else Observe("turn.index6.skipped");
            next = 7;
            if (monthStart)
            {
                state = new CampaignNormalTurnPersonnelAgingSource(this, state, current, pool, observe).RunLifespan();
                Observe("turn.index7.completed");
            }
            else Observe("turn.index7.skipped");
            next = 8;
            if (monthStart)
            {
                state = new CampaignNormalTurnPersonnelAgingSource(this, state, current, pool, observe).RunHealth();
                Observe("turn.index8.completed");
            }
            else Observe("turn.index8.skipped");
            next = 9;
            var ending = CheckCampaignNormalTurnEndingCurrent(state, current, pool);
            state = ending.State ?? throw new InvalidDataException(ending.UnresolvedOwner);
            Observe("turn.index9.completed");
            if (ending.Stop) return new(RestoreNormalTurnAppAfterExit(state, saved ?? throw AppearanceTurnMissing("saved_App32")), null, next, saved);
            next = 10;
            if (annual)
            {
                state = ApplyNormalTurnAnnualCalendarCurrent(state, current, out bool stop);
                Observe("turn.index10.completed");
                if (stop) return new(RestoreNormalTurnAppAfterExit(state, saved ?? throw AppearanceTurnMissing("saved_App32")), null, next, saved);
            }
            else Observe("turn.index10.skipped");
            next = 11;
            //4F8560 is literally return1; it has no world or random writer.
            Observe("turn.index11.completed");
            next = 12;
            state = ApplyNormalTurnForceAbsorptionEventsCurrent(state, current, pool);
            Observe("turn.index12.completed");
            next = 13;
            state = ApplyNormalTurnSpecialForceRelationsCurrent(state, current, pool);
            Observe("turn.index13.completed");
            next = 14;
            if (monthStart)
            {
                state = ApplyNormalTurnMonthlyCaptiveEscapesCurrent(state, current, pool);
                Observe("turn.index14.completed");
            }
            else Observe("turn.index14.skipped");
            next = 15;
            if (monthStart)
            {
                state = ApplyNormalTurnMonthlyDisastersCurrent(state, current, pool);
                Observe("turn.index15.completed");
            }
            else Observe("turn.index15.skipped");
            next = 16;
            if (monthStart)
            {
                state = ApplyNormalTurnSelfRecommendationsCurrent(state, current, pool,
                    saved ?? throw AppearanceTurnMissing("saved_App32"), grouping);
                if (state.NativeNormalTurnContinuation is not null)
                {
                    Observe("turn.index16.waiting-for-player");
                    return new(state, null, next, saved);
                }
                Observe("turn.index16.completed");
            }
            else Observe("turn.index16.skipped");
            state = ApplyNormalTurnRemainingActionsCurrent(state, current, pool, monthStart, seasonal,
                saved ?? throw AppearanceTurnMissing("saved_App32"), grouping,
                reached: action => next = action,
                observe: (point, reached) => observe?.Invoke(point, GameStateCopy.Copy(reached)), groups: normalGroups);
            if (state.NativeNormalTurnContinuation is not null) return new(state, null, next, saved);
            next = state.CampaignTurnStop is null ? 79 : next;
            state = RestoreNormalTurnAppAfterExit(state, saved ?? throw AppearanceTurnMissing("saved_App32"));
            return new(state, null, next, saved);

            void Observe(string point)
            {
                if (observe is not null) observe(point, GameStateCopy.Copy(state));
            }
        }
        catch (InvalidDataException error) { return new(null, error.Message, next, saved); }
        catch (PersonArmyCaptureSource.MissingOwner error) { return new(null, error.Message, next, saved); }
    }

    private static InvalidDataException AppearanceTurnMissing(string owner) =>
        new("normal.turn." + owner);
}
