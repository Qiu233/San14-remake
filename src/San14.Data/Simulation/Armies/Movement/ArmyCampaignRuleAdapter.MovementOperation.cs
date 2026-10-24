using San14.Core;

namespace San14.Data;

/// <summary>A numerical stage subset, not a completed playable period. Its
/// state cannot be adopted as a Reporting session: contact, other stages and
/// the normal-combat cursor still belong to their actual consumers.</summary>
public sealed record OrdinaryMovementSubsetResult(GameState? State, string? UnresolvedOwner,
    NativeMapSeasonDate? BlockedDate, int? UnresolvedArmyId, int CalendarCycles, int WaitingUpdates);
public sealed record OrdinaryMovementCycle(long CalendarOrdinal, NativeMapSeasonDate Date,
    GameState State, IReadOnlyList<int> MovedActorOrder);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Runs only the already implemented ordinary move/stay numerical
    /// operation. The caller owns this private transaction and must discard it
    /// on failure. This method is deliberately not wired into GameplaySession.
    /// Observer state is borrowed only for the duration of that callback.</summary>
    public OrdinaryMovementSubsetResult AdvanceOrdinaryMovementSubsetOwned(GameState state,
        IPlayableNativeSubdayRules waitingRules, int waitingUpdates,
        Action<OrdinaryMovementCycle>? observe = null)
        => AdvanceCurrentArmyTimelineSubsetOwned(state, waitingRules, waitingUpdates, false, observe);

    /// <summary>Combines actual movement, the reached10h combat candidate
    /// operation,8/22 personnel journeys, retirement/Base67 and commands.
    /// A reached unsupported combat pair rejects the private operation. This
    /// still does not claim the remaining stages or a complete playable turn.</summary>
    public OrdinaryMovementSubsetResult AdvanceCurrentArmyTimelineSubsetOwned(GameState state,
        IPlayableNativeSubdayRules waitingRules, int waitingUpdates,
        Action<OrdinaryMovementCycle>? observe = null)
        => AdvanceCurrentArmyTimelineSubsetOwned(state, waitingRules, waitingUpdates, true, observe);

    private OrdinaryMovementSubsetResult AdvanceCurrentArmyTimelineSubsetOwned(GameState state,
        IPlayableNativeSubdayRules waitingRules, int waitingUpdates, bool includeCombatAndPeople,
        Action<OrdinaryMovementCycle>? observe)
    {
        if (state.NativeMapSeason is not { } initialSeason || state.NativeClock is not { } initialClock ||
            initialSeason.ProcessedNativeOrdinal != initialClock.UpdateOrdinal || initialSeason.ThirdDate.Hour % 6 != 0 ||
            waitingUpdates < 1 || waitingUpdates > NativeSubdayStateValidation.UpdatesPerPeriod - initialClock.UpdatesIntoPeriod)
            return Missing("current_clock_boundary", null, null, 0, 0);
        // Unsupported branches reject this independent subset; they are not
        // executed as no-ops or sent through the coarse six-step path.
        foreach (var army in state.Units.Values)
        {
            byte? action = ArmyConfigurationSourceAdapter.ReadAction(state, army);
            bool move = action is 1 or 13 && army.Mission?.Kind == UnitMissionKind.Combat && army.MilitaryPlan is not
                { Completed: false, Action.ActionKey: not ("move" or "stay") };
            bool enter = army.NativeTacticRuntime?.Status10 == 1 && action is 2 or 3 &&
                army.Mission?.Kind == UnitMissionKind.Combat && army.MilitaryPlan is { Action.ActionKey: "enter" };
            bool attack = includeCombatAndPeople && army.NativeTacticRuntime?.Status10 == 1 && action is >= 4 and <= 7 &&
                army.Mission?.Kind == UnitMissionKind.Combat && army.MilitaryPlan is { Action.ActionKey: "attack" };
            bool withdraw = action is 11 or 12 && army.Mission?.Kind == UnitMissionKind.Retreat;
            bool transport = army.NativeTacticRuntime?.Status10 == 2 && action == 2 && army.Mission?.Kind == UnitMissionKind.Transport;
            if (army.NativeArmyOrigin is null || army.NativeTacticRuntime is not { Status10: 1 or 2 } || !(move || enter || attack || withdraw || transport))
                return Missing("ordinary_move_stay_enter_withdraw_scope", initialSeason.ThirdDate, army.Id, 0, 0);
        }

        //One operation-local current/pool also owns actual29F220 publication
        //and its stage26 retirement. It is not recaptured for each actor/cut.
        OwnedArmyReturnWorld returnWorld;
        try { returnWorld = CaptureOwnedArmyReturnWorld(state); }
        catch (InvalidDataException error) { return Missing(error.Message, initialSeason.ThirdDate, null, 0, 0); }
        var current = returnWorld.Context;
        var arena = state.ReadArmyLifecycle();
        if (arena is null) return Missing("lifecycle", initialSeason.ThirdDate, null, 0, 0);
        var queries = new ArmyMarchCurrentProducer(_repository, this, _map, operationCurrent: current);
        var movement = new ArmyMarchSourceRuntime(queries, new(this, current), current);
        GameState working = state;
        int cycles = 0, updates = 0;
        long elapsedDays = initialSeason.AdvancedDays;
        NativeMapSeasonDate date = initialSeason.ThirdDate;
        try
        {
            for (int block = 0; block < waitingUpdates; block++)
            {
                var season = working.NativeMapSeason!;
                for (int cycle = 0; cycle < NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock; cycle++)
                {
                    long ordinal = checked(season.AdvancedCalendarSteps + cycle);
                    if (NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(date))
                    {
                        var manager = queries.ReadCompleteCurrentManager60Chain(working,
                            new(ordinal, date, (byte)NativeArmyProgressStage.Waiting, cycle, NativeArmyProgressStageBoundary.StageEntry));
                        if (manager is null) return Missing("waiting_manager", date, null, cycles, updates);
                        working = NativeSubdayRuntime.AdvanceCurrentManagerOwned(working, waitingRules,
                            manager.Select(actor => actor.UnitId).ToArray());
                        updates++;
                    }
                    var prepare = new NativeArmyProgressStageMoment(ordinal, date,
                        (byte)NativeArmyProgressStage.PrepareMovement, cycle, NativeArmyProgressStageBoundary.StageEntry);
                    var queue = movement.PrepareCurrentStage7(working, prepare);
                    if (queue is null) return Missing("stage7_current_inputs", date, null, cycles, updates);
                    var moved = ArmyMovementStage.AdvanceCurrentQueueOwned(working,
                        prepare with { SchedulerStageIndex = (byte)NativeArmyProgressStage.Movement }, queue, movement);
                    if (!moved.Complete) return Missing("stage8_reached_branch", date, moved.UnresolvedArmyId, cycles, updates);
                    working = moved.State;
                    if (date.Hour == 0)
                    {
                        var appeared = ConsumeOrdinaryAppearance(working,
                            prepare with { SchedulerStageIndex = (byte)NativeArmyProgressStage.Appearance }, current);
                        if (appeared.State is null)
                            return Missing(appeared.UnresolvedOwner ?? "stage9_appearance", date, null, cycles, updates);
                        working = appeared.State;
                    }
                    if (includeCombatAndPeople)
                    {
                        if (date.Hour == 10)
                            working = CompleteCurrentArmyCombatCut(working, current, date, ordinal);
                        if (date.Hour is 8 or 22)
                        {
                            var returned = CompletePersonReturnCut(working, current, returnWorld.Pool, date);
                            working = returned.State ?? throw new InvalidDataException(
                                returned.UnresolvedOwner ?? "current.movement_person_return");
                            foreach (var receipt in returned.Receipts)
                            {
                                if (receipt.Arrival is not { } arrival) continue;
                                int id = _definitions.ReadOfficerSourceIdentity(arrival.PersonPhysical)
                                    ?? throw new InvalidDataException("current.movement_return_Person_identity");
                                var officer = working.Officers[id];
                                working = working with { PeriodReports = [.. working.PeriodReports,
                                    new("officer_return", officer.ForceId ?? 0, officer.LocationId, officer.Id)
                                    { RuleId = "ce-202360-Return8_22-32B190" }] };
                            }
                        }
                    }
                    var pendingHandle = current.ReadArmyReturnPending70Descriptor()
                        ?? throw new InvalidDataException("current.movement_pending70_descriptor");
                    var pendingHead = current.ReadRegistryHead(pendingHandle)
                        ?? throw new InvalidDataException("current.movement_pending70_head");
                    if (pendingHead.Identity is not null)
                    {
                        //Actual26 numeric cleanup. This independent operation
                        //does not mutate the live adapter's rendering/group
                        //cache or manufacture a presentation acknowledgement.
                        var released = OwnedArmyLifecycleAuxiliaries.ConsumePendingCurrent(working, current, returnWorld.Pool);
                        if (released is null) return Missing("stage26_retirement", date, null, cycles, updates);
                        working = released;
                    }
                    if (includeCombatAndPeople)
                    {
                        var nearby = RefreshBaseNearbyArmiesCurrent(working, current);
                        working = nearby.State ?? throw new InvalidDataException(
                            nearby.UnresolvedOwner ?? "current.movement_base67");
                        if (returnWorld.Pool.PendingObjectRetirements is null)
                            throw new InvalidDataException("current.movement_Object_retirement_queue");
                        if (returnWorld.Pool.PendingObjectRetirements.Count != 0)
                            throw new InvalidDataException("current.movement_Object_retirement_borrowed_owner");
                    }
                    long dayAtWriter = elapsedDays;
                    var maintained = CompleteOrdinaryArmyCommands(working, current, date, ordinal,
                        reached => unchecked((reached.NativeArmySimulation?.World.SamplerEpoch
                            ?? throw new InvalidDataException("current.movement_campaign_epoch")).Day450 + (int)dayAtWriter));
                    if (maintained.State is null)
                        return Missing(maintained.UnresolvedOwner ?? "stage28_commands", date, null, cycles, updates);
                    working = maintained.State;
                    observe?.Invoke(new(ordinal, date, working, moved.AppliedActorOrder));
                    var calendarStep = MapSeasonRuntime.StepCalendarWriter(date);
                    if (calendarStep.DayAdvanced) elapsedDays++;
                    date = calendarStep.Date;
                    cycles++;
                }
                var advancement = MapSeasonRuntime.AdvanceToOwnedClock(season, working.NativeClock);
                if (!advancement.Available) return Missing("calendar_writer", date, null, cycles, updates);
                working = working with { NativeMapSeason = advancement.State };
            }
            var pool = current.ReadOwnedCurrentListPool();
            if (pool is null) return Missing("current_pool", date, null, cycles, updates);
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
            // No normal-combat cursor, Core Date/Phase or Reporting finalizer
            // is advanced here. A movement subset is not a complete turn.
            return new(working, null, null, null, cycles, updates);
        }
        catch (InvalidDataException error)
        {
            return Missing(error.Message, date, null, cycles, updates);
        }

        static OrdinaryMovementSubsetResult Missing(string owner, NativeMapSeasonDate? at, int? actor, int cycles, int updates)
            => new(null, "ordinary_movement_subset." + owner, at, actor, cycles, updates);
    }
}
