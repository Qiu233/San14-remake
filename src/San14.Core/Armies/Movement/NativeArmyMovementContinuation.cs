using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The still-unvisited actors from the one actual stage7 selection.
/// March itself stays in its ordinary Unit owner; no second route snapshot or
/// reconstructed movement preparation is saved.</summary>
public sealed record ArmyMovementRemainingActor(int UnitId, ArmyReceiverId? ReceiverId,
    int CreditAtPreparation, int HexAtPreparation);

public sealed record NativeArmyMovementContinuation(NativeArmyProgressStageMoment PreparationMoment,
    ImmutableArray<ArmyMovementRemainingActor> RemainingActors, ImmutableArray<int> AppliedActorOrder,
    int CompletedBatchCount, int ActorsIntoBatch)
{
    public ArmyMovementQueue RestoreQueue(GameState state)
    {
        var actors = new ArmyMovementQueueActor[RemainingActors.Length];
        for (int index = 0; index < RemainingActors.Length; index++)
        {
            var saved = RemainingActors[index];
            if (!state.Units.TryGetValue(saved.UnitId, out var unit) || unit.March is not { } march ||
                march.MovementCredit != saved.CreditAtPreparation || unit.HexId != saved.HexAtPreparation ||
                saved.ReceiverId is { } receiver && state.ArmyLifecycle?.Receivers.Find(receiver)?.ActiveUnitId != saved.UnitId)
                throw new InvalidDataException("A remaining movement actor changed while captive treatment was pending.");
            actors[index] = new(saved.UnitId, saved.ReceiverId, saved.CreditAtPreparation, saved.HexAtPreparation, march);
        }
        return new(PreparationMoment, actors);
    }
}

internal static class NativeArmyMovementContinuationValidation
{
    internal static void Validate(GameState state)
    {
        var cursor = state.NativePeriodContinuation?.Cursor;
        if (cursor?.MovementContinuation is not { } movement)
        {
            if (cursor?.StageIndex == (byte)NativeArmyProgressStage.Movement) throw Invalid();
            return;
        }
        if (cursor.StageIndex != (byte)NativeArmyProgressStage.Movement ||
            movement.PreparationMoment is not { } prepared ||
            prepared.PreWriterThirdDate != cursor.Date || prepared.CycleWithinAtomicBlock != cursor.CycleIndex) throw Invalid();
        Validate(state, movement, prepared with
            { SchedulerStageIndex = (byte)NativeArmyProgressStage.Movement });
    }

    internal static void Validate(GameState state, NativeArmyMovementContinuation movement,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            movement.PreparationMoment is not { } prepared ||
            prepared.SchedulerStageIndex != (byte)NativeArmyProgressStage.PrepareMovement ||
            prepared.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            prepared.PreWriterThirdDate != moment.PreWriterThirdDate ||
            prepared.CycleWithinAtomicBlock != moment.CycleWithinAtomicBlock ||
            prepared.CalendarCycleOrdinal != moment.CalendarCycleOrdinal ||
            movement.RemainingActors.IsDefault || movement.AppliedActorOrder.IsDefaultOrEmpty ||
            movement.CompletedBatchCount < 0 || movement.ActorsIntoBatch is < 0 or >= ArmyMovementStage.ActorsPerBatch ||
            (long)movement.CompletedBatchCount * ArmyMovementStage.ActorsPerBatch + movement.ActorsIntoBatch != movement.AppliedActorOrder.Length ||
            movement.AppliedActorOrder.Any(id => id <= 0) ||
            movement.AppliedActorOrder.Distinct().Count() != movement.AppliedActorOrder.Length ||
            movement.RemainingActors.Any(actor => actor.UnitId <= 0 || actor.CreditAtPreparation < 0 ||
                actor.HexAtPreparation < 0 || actor.HexAtPreparation >= (long)state.MapWidth * state.MapHeight ||
                actor.ReceiverId is { Value: <= 0 } ||
                movement.AppliedActorOrder.Contains(actor.UnitId)) ||
            movement.RemainingActors.Zip(movement.RemainingActors.Skip(1),
                (first, second) => first.CreditAtPreparation < second.CreditAtPreparation).Any(unsorted => unsorted) ||
            movement.RemainingActors.Select(actor => actor.UnitId).Distinct().Count() != movement.RemainingActors.Length ||
            movement.RemainingActors.Where(actor => actor.ReceiverId is not null).Select(actor => actor.ReceiverId).Distinct().Count() !=
                movement.RemainingActors.Count(actor => actor.ReceiverId is not null) ||
            state.ArmyReturnCaptiveContinuation is not { Caller: ArmyReturnCaptiveCaller.MovementArrival, ArrivalTail: { } arrival } returning ||
            returning.ArmyUnitId != movement.AppliedActorOrder[^1] ||
            prepared.CalendarCycleOrdinal != returning.SourceRevision || prepared.PreWriterThirdDate != returning.SourceDate ||
            arrival.Moment != prepared with { SchedulerStageIndex = (byte)NativeArmyProgressStage.Movement })
            throw Invalid();
        _ = movement.RestoreQueue(state);
    }

    private static InvalidDataException Invalid() => new("Saved movement continuation has no matching captive return boundary.");
}
