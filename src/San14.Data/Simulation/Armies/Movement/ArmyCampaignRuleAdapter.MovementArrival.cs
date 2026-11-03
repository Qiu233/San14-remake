using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The2/3/11/12 suffix after an actual2DA620 position store.
    /// The return packet publishes people, resources and pending70; it does
    /// not release the Army receiver. That belongs to this cut's stage26.</summary>
    internal GameState CompleteMovementBaseArrival(GameState state, SourceWorldContext current, UnitState army,
        NativeArmyProgressStageMoment? moment)
    {
        byte action = ArmyConfigurationSourceAdapter.ReadAction(state, army)
            ?? throw new InvalidDataException("current.movement_arrival.action37");
        if (action is not (2 or 3 or 11 or 12)) return state;
        BaseReference destination;
        bool enter = true;
        int location;
        ushort goal;
        if (action is 2 or 11)
        {
            var eligibility = ArmyCityEntryEligibilitySource.ReadAfterCommittedMove(state, current, army);
            if (eligibility.Receipt is not { } receipt)
                throw new InvalidDataException(eligibility.UnresolvedOwner ?? "current.movement_arrival.City_position");
            if (!receipt.Eligible && !receipt.Reports2E) return state;
            enter = receipt.Eligible;
            goal = receipt.GoalHex2B5610
                ?? throw new InvalidDataException("current.movement_arrival.City_goal");
            destination = receipt.TargetBase
                ?? throw new InvalidDataException("current.movement_arrival.City_pointer");
            int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
            var target = OwnedArmyLifecycleTargets.Read(state, state.ReadArmyLifecycle()!.ReceiverSlots()[physical].ReceiverId);
            location = target?.Identity3A is ushort raw ? raw <= 51 ? raw : 0
                : throw new InvalidDataException("current.movement_arrival.City_identity");
        }
        else
        {
            int physical = army.NativeArmyOrigin?.NativeSlotIndex
                ?? throw new InvalidDataException("current.movement_arrival.Army_pointer");
            var arena = state.ReadArmyLifecycle()
                ?? throw new InvalidDataException("current.movement_arrival.lifecycle");
            if (OwnedArmyLifecycleTargets.Read(state, arena.ReceiverSlots()[physical].ReceiverId)
                is not { Kind38: 6, Identity3A: ushort identity })
                throw new InvalidDataException("current.movement_arrival.Gate_target");
            var gate = current.ReadGateTableReference(identity <= 10 ? identity : 0)
                ?? throw new InvalidDataException("current.movement_arrival.Gate_pointer");
            goal = OrdinaryArmyRouteSource.ReadCurrentTarget(state, army, current)
                ?? throw new InvalidDataException("current.movement_arrival.Gate_goal");
            if (ArmyMarchSourceAdapter.ReadCurrentHex(state, army) != goal) return state;
            //2D99C0 passes the actual selected Base to29F220. Its validity,
            //commander and affiliation checks remain inside that consumer.
            location = identity is >= 1 and <= 10 ? identity + 51 : 0;
            destination = location == 0 ? current.ReadCityTableReference(0)
                ?? throw new InvalidDataException("current.movement_arrival.invalid_Gate_Base0") : gate;
        }
        int armyPhysical = army.NativeArmyOrigin?.NativeSlotIndex
            ?? throw new InvalidDataException("current.movement_arrival.Army_pointer");
        var returned = state;
        if (enter)
        {
            var pool = current.ReadOwnedCurrentListPool()
                ?? throw new InvalidDataException("current.movement_arrival.current_pool");
            var world = BorrowOwnedArmyReturnWorld(state, current, pool);
            var prepared = PrepareOwnedArmyReturnCurrent(state, new(armyPhysical), destination, 0,
                current, pool, new MovementReturnGovernorQueries(state, _governorInputs), new(false, false),
                army.NativeTacticRuntime?.Status10 == 2 ? army.NativeTacticRuntime.Flags26 : null, moment: moment);
            if (prepared.Suspension is { } suspension)
            {
                if (moment is null) throw new InvalidDataException("current.movement_arrival.suspension_date");
                returned = world.CommitSuspension(state, suspension);
                var owner = returned.ArmyReturnCaptiveContinuation
                    ?? throw new InvalidDataException("current.movement_arrival.suspension_owner");
                returned = returned with { ArmyReturnCaptiveContinuation = owner with
                {
                    Caller = ArmyReturnCaptiveCaller.MovementArrival,
                    ArrivalTail = new(action, goal, location, moment) { MarchCallerTailPending = true }
                } };
                return OwnedArmyLifecycleDetachedInputs.PublishCurrent(returned, current);
            }
            if (prepared.Packet is not { } packet)
                throw new InvalidDataException(prepared.UnresolvedOwner ?? "current.movement_arrival.return_packet");
            returned = world.CommitPrepared(state, packet)
                ?? throw new InvalidDataException("current.movement_arrival.return_publication");
            returned = OwnedArmyLifecycleDetachedInputs.PublishCurrent(returned, current);
        }
        if (action is 11 or 12) return returned;
        if (moment is null) throw new InvalidDataException("current.movement_arrival.speech_date");
        var operation = new FacilityOperation(() => moment.PreWriterThirdDate, moment.CalendarCycleOrdinal,
            _ => throw new InvalidDataException("current.movement_arrival.unused_campaign_day"));
        return AppendArmyCommandSpeech(returned, current, army, operation, CombatSpeechEvent.ArmyEnteredBase,
            new(goal, location));
    }

    internal GameState ResumeMovementReturnCaptivesCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool)
    {
        var pending = state.ArmyReturnCaptiveContinuation;
        if (pending is not { Caller: ArmyReturnCaptiveCaller.MovementArrival, ArrivalTail: { } tail } ||
            !state.Units.TryGetValue(pending.ArmyUnitId, out var army))
            throw new InvalidDataException("current.movement_arrival.resume_owner");
        //The actual returning image still exists before the suffix admits it
        //to pending70. Retain it locally for the source arrival speech only.
        var returned = ResumePlayerArmyReturnCurrent(state, current, pool);
        if (returned.ArmyReturnCaptiveContinuation is not null) return returned;
        if (tail.Action is not (11 or 12))
        {
            var operation = new FacilityOperation(() => tail.Moment.PreWriterThirdDate,
                tail.Moment.CalendarCycleOrdinal,
                _ => throw new InvalidDataException("current.movement_arrival.unused_campaign_day"));
            returned = AppendArmyCommandSpeech(returned, current, army, operation, CombatSpeechEvent.ArmyEnteredBase,
                new(tail.GoalHexId, tail.DestinationLocationId));
        }
        //The original caller's position/credit/waypoint prefix was committed
        //before suspension. Resume only its previously unexecuted remainder.
        return tail.MarchCallerTailPending
            ? new ArmyMarchCrossingSource(this, current).CompleteMovementCallerAfterArrivalCurrent(returned,
                current, pending.ArmyPhysicalSlot, tail.Moment)
            : returned; //World227 already executed this caller remainder before saving its pending batch.
    }

    private sealed class MovementReturnGovernorQueries(GameState state, OwnedArmyGovernorComparatorInputs inputs)
        : IArmyReturnGovernorCurrentQueries
    {
        public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int physical) =>
            inputs.ReadCurrentComparatorFields(state, physical);
    }
}
