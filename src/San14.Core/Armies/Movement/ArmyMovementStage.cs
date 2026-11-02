using System.Collections.Immutable;

namespace San14.Core;

public sealed record ArmyMovementQueueActor(int UnitId, ArmyReceiverId? ReceiverId,
    int CreditAtPreparation, int HexAtPreparation, ArmyMarchState MarchAtPreparation);
public sealed record ArmyMovementQueue
{
    public NativeArmyProgressStageMoment PreparationMoment { get; }
    public IReadOnlyList<ArmyMovementQueueActor> SelectedActors { get; }
    public ArmyMovementQueue(NativeArmyProgressStageMoment preparationMoment,
        IEnumerable<ArmyMovementQueueActor> selectedActors)
    {
        PreparationMoment = preparationMoment;
        SelectedActors = Array.AsReadOnly(selectedActors.ToArray());
    }
}
public sealed record ArmyMovementResolution(int MovementCredit, GameState? UpdatedState = null);
public interface IArmyMovementRules
{
    /// <summary>Borrows the operation world for this call. Null leaves the stage unresolved.</summary>
    ArmyMovementResolution? AdvanceArmy(GameState state, UnitState army,
        NativeArmyProgressStageMoment movementMoment);
}
public sealed record ArmyMovementStageResult(bool Complete, GameState State,
    int? UnresolvedArmyId, int[] AppliedActorOrder, int CompletedBatchCount)
{
    public NativeArmyMovementContinuation? SuspendedMovement { get; init; }
}

/// <summary>Consumes the prepared actor order inside one transaction. The current
/// rules may return a complete private cross-cell transaction. A late unresolved
/// actor still discards every earlier movement in this stage.</summary>
public static class ArmyMovementStage
{
    public const int ActorsPerBatch = 50;
    public static ArmyMovementStageResult TryAdvance(GameState state,
        NativeArmyProgressStageMoment moment, ArmyMovementQueue queue, IArmyMovementRules rules)
    {
        var result = TryAdvanceOwned(GameStateCopy.Copy(state), moment, queue, rules);
        return result.Complete || result.SuspendedMovement is not null ? result : result with { State = state };
    }

    public static ArmyMovementStageResult TryAdvanceOwned(GameState state,
        NativeArmyProgressStageMoment moment, ArmyMovementQueue queue, IArmyMovementRules rules)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            queue.PreparationMoment != moment with { SchedulerStageIndex = (byte)NativeArmyProgressStage.PrepareMovement })
            throw new InvalidDataException("Movement queue belongs to another preparation boundary.");
        var actors = queue.SelectedActors;
        if (actors.Select(actor => actor.UnitId).Distinct().Count() != actors.Count ||
            actors.Where(actor => actor.ReceiverId.HasValue).Select(actor => actor.ReceiverId).Distinct().Count() !=
                actors.Count(actor => actor.ReceiverId.HasValue) ||
            actors.Zip(actors.Skip(1), (first, second) => first.CreditAtPreparation < second.CreditAtPreparation).Any(unsorted => unsorted))
            throw new InvalidDataException("Movement queue duplicates an actor or changes its progress order.");
        foreach (var actor in actors)
        {
            if (!state.Units.TryGetValue(actor.UnitId, out var unit) ||
                unit.March != actor.MarchAtPreparation || unit.March?.MovementCredit != actor.CreditAtPreparation ||
                unit.HexId != actor.HexAtPreparation ||
                actor.ReceiverId is { } receiver &&
                    state.ArmyLifecycle?.Receivers.Find(receiver)?.ActiveUnitId != unit.Id)
                throw new InvalidDataException("Movement actor changed after preparation.");
        }
        return AdvanceCurrentQueueOwned(state, moment, queue, rules);
    }

    /// <summary>Consumes a queue just built by the operation's own current
    /// stage7. External prepared queues keep the validation entry above.
    /// The caller admits the first source batch at stage entry. Between later
    /// actual fifty-actor batches it can reenter that same stage's frame budget;
    /// the private world, queue and traversal stay exclusively on this worker.
    /// Without a host admission callback the owned transaction runs to completion.</summary>
    public static ArmyMovementStageResult AdvanceCurrentQueueOwned(GameState state,
        NativeArmyProgressStageMoment moment, ArmyMovementQueue queue, IArmyMovementRules rules,
        Func<GameState, NativeArmyProgressStageMoment, GameState>? admitNextBatch = null,
        NativeArmyMovementContinuation? resumed = null)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            queue.PreparationMoment != moment with { SchedulerStageIndex = (byte)NativeArmyProgressStage.PrepareMovement })
            throw new InvalidDataException("Movement queue belongs to another current cycle.");
        if (state.ArmyReturnCaptiveContinuation is not null)
            throw new InvalidDataException("Movement cannot visit another actor before captive return finishes.");
        var actors = queue.SelectedActors;
        var working = state;
        var order = resumed?.AppliedActorOrder.ToList() ?? new List<int>();
        int completedBatches = resumed?.CompletedBatchCount ?? 0;
        int actorsIntoBatch = resumed?.ActorsIntoBatch ?? 0;
        bool firstBatch = true;
        for (int batchStart = 0; batchStart < actors.Count;)
        {
            //Stage entry already admits the first remaining slice, including
            //a partially consumed slice saved at a player decision boundary.
            if (!firstBatch && admitNextBatch is not null)
                working = admitNextBatch(working, moment);
            int batchEnd = batchStart + Math.Min(ActorsPerBatch - actorsIntoBatch, actors.Count - batchStart);
            for (int actorIndex = batchStart; actorIndex < batchEnd; actorIndex++)
            {
                var actor = actors[actorIndex];
                if (!working.Units.TryGetValue(actor.UnitId, out var unit) || actor.ReceiverId is { } receiver &&
                    working.ArmyLifecycle?.Receivers.Find(receiver)?.ActiveUnitId != unit.Id)
                    return new(false, state, actor.UnitId, [], 0);
                var resolution = rules.AdvanceArmy(working, unit, moment);
                if (resolution is null) return new(false, state, actor.UnitId, [], 0);
                if (resolution.MovementCredit < 0) throw new InvalidDataException("Army movement credit cannot be negative.");
                working = resolution.UpdatedState ?? working;
                if (!working.Units.TryGetValue(unit.Id, out var updated))
                {
                    //A completed return owns its paid image in pending70.
                    if (actor.ReceiverId is not { } returned ||
                        working.ArmyLifecycle?.Receivers.Find(returned) is not
                            { ActiveUnitId: null, PendingImage: { } pending } || pending.Id != unit.Id)
                        return new(false, state, actor.UnitId, [], 0);
                }
                else if (updated.March?.MovementCredit != resolution.MovementCredit)
                    working = working with { Units = new(working.Units)
                        { [unit.Id] = updated with { March = updated.March! with { MovementCredit = resolution.MovementCredit } } } };
                order.Add(unit.Id);
                if (++actorsIntoBatch == ActorsPerBatch) { completedBatches++; actorsIntoBatch = 0; }
                if (working.ArmyReturnCaptiveContinuation is { } returning)
                {
                    if (returning.Caller != ArmyReturnCaptiveCaller.MovementArrival || returning.ArmyUnitId != unit.Id)
                        throw new InvalidDataException("Movement suspension belongs to another return caller.");
                    var held = new NativeArmyMovementContinuation(queue.PreparationMoment,
                        actors.Skip(actorIndex + 1).Select(next => new ArmyMovementRemainingActor(next.UnitId,
                            next.ReceiverId, next.CreditAtPreparation, next.HexAtPreparation)).ToImmutableArray(),
                        order.ToImmutableArray(), completedBatches, actorsIntoBatch);
                    return new(false, working, null, order.ToArray(), completedBatches) { SuspendedMovement = held };
                }
            }
            firstBatch = false;
            batchStart = batchEnd;
        }
        return new(true, working, null, order.ToArray(), completedBatches + (actorsIntoBatch == 0 ? 0 : 1));
    }
}
