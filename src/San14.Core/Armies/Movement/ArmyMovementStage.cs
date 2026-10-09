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
public sealed record ArmyMovementResolution(int MovementCredit);
public interface IArmyMovementRules
{
    /// <summary>Borrows the operation world for this call. Null leaves the stage unresolved.</summary>
    ArmyMovementResolution? AdvanceArmy(GameState state, UnitState army,
        NativeArmyProgressStageMoment movementMoment);
}
public sealed record ArmyMovementStageResult(bool Complete, GameState State,
    int? UnresolvedArmyId, int[] AppliedActorOrder, int CompletedBatchCount);

/// <summary>Consumes the prepared actor order inside one transaction. The current
/// implementation advances progress only; crossing a cell remains unresolved.</summary>
public static class ArmyMovementStage
{
    public const int ActorsPerBatch = 50;
    public static ArmyMovementStageResult TryAdvance(GameState state,
        NativeArmyProgressStageMoment moment, ArmyMovementQueue queue, IArmyMovementRules rules)
    {
        var result = TryAdvanceOwned(GameStateCopy.Copy(state), moment, queue, rules);
        return result.Complete ? result : result with { State = state };
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
                    state.NativeArmyLifecycle?.Slots.SingleOrDefault(slot => slot.ReceiverId == receiver)?.ActiveUnitId != unit.Id)
                throw new InvalidDataException("Movement actor changed after preparation.");
        }
        var working = state;
        var order = new List<int>();
        foreach (var actor in actors)
        {
            var unit = working.Units[actor.UnitId];
            var resolution = rules.AdvanceArmy(working, unit, moment);
            if (resolution is null) return new(false, state, actor.UnitId, [], 0);
            if (resolution.MovementCredit < 0) throw new InvalidDataException("Army movement credit cannot be negative.");
            working = working with { Units = new(working.Units)
                { [unit.Id] = unit with { March = unit.March! with { MovementCredit = resolution.MovementCredit } } } };
            order.Add(unit.Id);
        }
        return new(true, working, null, order.ToArray(), (actors.Count + ActorsPerBatch - 1) / ActorsPerBatch);
    }
}
