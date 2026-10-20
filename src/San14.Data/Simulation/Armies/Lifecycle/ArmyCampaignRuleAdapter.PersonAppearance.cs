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
    {
        uint? saved = null;
        int next = 0;
        try
        {
            var season = state.NativeMapSeason ?? throw AppearanceTurnMissing("calendar");
            var clock = state.NativeClock ?? throw AppearanceTurnMissing("clock");
            var expected = intent.StartDate.Next();
            if (expected.Year is < short.MinValue or > short.MaxValue)
                throw AppearanceTurnMissing("completed_period_year_profile");
            if (state.Date != intent.StartDate || state.PeriodNumber != intent.StartPeriodNumber ||
                state.Phase != TurnPhase.Strategy || clock.AnchorDate != intent.StartDate || clock.UpdatesIntoPeriod != 40 ||
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
            // This is the actual next unowned action, not normal Turn exit.
            // In particular, do not restore SavedApp32 here, even if nonzero.
            throw AppearanceTurnMissing("action.5");

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
