namespace San14.Core;

/// <summary>Actual native marching words. Unknown producer stays null; a Core
/// military destination, period distance or load never synthesizes these fields.</summary>
public sealed record NativeArmyMarchState(ushort Credit14, ushort CurrentHex2A,
    ushort TargetHex2C, ushort NextHex48, byte OverrideTargetType3C);
public sealed partial record UnitState
{
    public NativeArmyMarchState? NativeMarch { get; init; }
}
public static class NativeArmyMarchStateValidation
{
    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var army in state.Units.Values)
            if (army.NativeMarch is { } fields && (state.NativeClock is null || state.NativeMapSeason is null ||
                army.NativeArmyOrigin is null || state.MapWidth <= 0 || state.MapHeight <= 0 ||
                fields.CurrentHex2A >= (long)state.MapWidth * state.MapHeight || fields.CurrentHex2A != army.HexId))
                throw new InvalidDataException("Actual native marching fields require their current source-created Army/calendar owner.");
    }
}
public sealed record NativeArmyMovementQueueActor(int UnitId, int NativePoolSlot, ushort CreditAtStage7);
public sealed record NativeArmyMovementQueue(NativeArmyProgressStageMoment Stage7Moment,
    NativeArmyMovementQueueActor[] SelectedActors, string SourceProvenance);
public sealed record NativeArmyMovementUnitResolution(NativeArmyMarchState CurrentFields, string SourceProvenance);
public interface IPlayableNativeArmyMovementRules
{
    /// <summary>Reads the current operation world without retaining or mutating its collections.</summary>
    NativeArmyMovementUnitResolution? AdvanceArmy(GameState state, UnitState army,
        NativeArmyProgressStageMoment stage8Moment);
}
public sealed record NativeArmyMovementStage8Result(bool Complete, GameState State,
    int? UnresolvedArmyId, int[] AppliedActorOrder, int CompletedBatchCount);

/// <summary>Current stage7 list, consumed in source order in groups of50.
/// This initial consumer only accepts the proven insufficient-cost update.
/// A missing actor result rolls the entire stage back; no route or NoEffect fallback.</summary>
public static class NativeArmyMovementStage8AtomicRuntime
{
    public const int SourceBatchSize = 50;
    public static NativeArmyMovementStage8Result TryAdvance(GameState state,
        NativeArmyProgressStageMoment stage8Moment, NativeArmyMovementQueue queue,
        IPlayableNativeArmyMovementRules rules)
    {
        var result = TryAdvanceOwned(GameStateCopy.Copy(state), stage8Moment, queue, rules);
        return result.Complete ? result : result with { State = state };
    }

    /// <summary>Consumes a queue inside the caller's private operation world.</summary>
    public static NativeArmyMovementStage8Result TryAdvanceOwned(GameState state,
        NativeArmyProgressStageMoment stage8Moment, NativeArmyMovementQueue queue,
        IPlayableNativeArmyMovementRules rules)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(stage8Moment);
        ArgumentNullException.ThrowIfNull(queue); ArgumentNullException.ThrowIfNull(rules);
        if (stage8Moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement || stage8Moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            queue.Stage7Moment is null || queue.SelectedActors?.Any(a => a is null) == true ||
            queue.Stage7Moment != stage8Moment with { SchedulerStageIndex = (byte)NativeArmyProgressStage.PrepareMovement } ||
            string.IsNullOrWhiteSpace(queue.SourceProvenance) || queue.SelectedActors is null ||
            queue.SelectedActors.Select(a => a.UnitId).Distinct().Count() != queue.SelectedActors.Length ||
            queue.SelectedActors.Select(a => a.NativePoolSlot).Distinct().Count() != queue.SelectedActors.Length)
            throw new InvalidDataException("Movement queue belongs to another current stage7/8 owner or duplicates an actor.");
        if (queue.SelectedActors.Zip(queue.SelectedActors.Skip(1), (a, b) => a.CreditAtStage7 < b.CreditAtStage7).Any(x => x))
            throw new InvalidDataException("Source movement queue is not in descending native word14 order.");
        foreach (var actor in queue.SelectedActors)
            if (actor is null || !state.Units.TryGetValue(actor.UnitId, out var unit) ||
                unit.NativeArmyOrigin?.NativeSlotIndex != actor.NativePoolSlot || unit.NativeMarch is not { } fields ||
                fields.Credit14 != actor.CreditAtStage7 || fields.CurrentHex2A != unit.HexId)
                throw new InvalidDataException("Movement actor fields changed after the current stage7 list was prepared.");
        var working = state;
        var order = new List<int>();
        for (int index = 0; index < queue.SelectedActors.Length; index++)
        {
            int id = queue.SelectedActors[index].UnitId;
            UnitState army = working.Units[id];
            var resolution = rules.AdvanceArmy(working, army, stage8Moment);
            if (resolution is null) return new(false, state, id, [], 0);
            if (resolution.CurrentFields is not { } fields || string.IsNullOrWhiteSpace(resolution.SourceProvenance) ||
                fields with { Credit14 = army.NativeMarch!.Credit14 } != army.NativeMarch)
                throw new InvalidDataException("Insufficient-cost movement source wrote position/target fields or omitted provenance.");
            working = working with { Units = new(working.Units) { [id] = army with { NativeMarch = fields with { } } } };
            order.Add(id);
        }
        return new(true, working, null, order.ToArray(),
            (queue.SelectedActors.Length + SourceBatchSize - 1) / SourceBatchSize);
    }
}
