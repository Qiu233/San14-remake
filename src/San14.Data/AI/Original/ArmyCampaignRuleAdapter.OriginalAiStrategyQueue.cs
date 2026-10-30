using System.Collections.Immutable;
using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual fresh/progress Strategy entry. The normal new-game
    /// and next-period callers pass false; restore preserves the saved queue
    /// instead of replaying the original process's list reconstruction path.</summary>
    public GameState ConstructOriginalAiStrategyScheduleCurrent(GameState state, bool startAtPlayer)
    {
        if (state.Phase != TurnPhase.Strategy) throw QueueMissing("constructor_phase");
        var order = state.ForceProcessingOrder?.ForceIds ?? throw QueueMissing("prepared_force_processing_order");
        var current = CaptureOriginalAiDomesticCurrent(state);
        int player = ReadOriginalAiStrategyPlayerForceCurrent(state, current);
        int selector = QueueKnown(BattleRemakeCurrentSettings.ReadSelectorE58(state), "SettingsE58");
        int difficulty = QueueKnown(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28),
            "selected_difficulty");
        var queue = new List<int>();
        foreach (int force in order)
        {
            if (!QueueForceValid(current, force)) continue;
            // The original list insertion flag is zero for the actual player
            // on difficulty>=3 (prepend), and one for every other row (append).
            if (force == player && difficulty >= 3) queue.Insert(0, force);
            else queue.Add(force);
        }
        int first = 0;
        if (startAtPlayer && !ReadOriginalAiStrategyWholeControlCurrent(current))
        {
            int selected = queue.IndexOf(player);
            if (selected >= 0) first = selected;
        }
        return state with { OriginalAiStrategySchedule = new(queue.ToImmutableArray(), first, false) };
    }

    public OriginalAiStrategyQueueBatch ReadOriginalAiStrategyQueueBatchCurrent(GameState state,
        SourceWorldContext current, OriginalAiStrategySchedule schedule, OriginalAiStrategyQueueStage stage)
    {
        if (schedule.ForceIds.IsDefault || schedule.NextForceIndex < 0 || schedule.NextForceIndex > schedule.ForceIds.Length ||
            schedule.AwaitingPlayerCompletion && schedule.NextForceIndex == 0) throw QueueMissing("saved_cursor");
        if (schedule.AwaitingPlayerCompletion)
        {
            if (stage != OriginalAiStrategyQueueStage.AfterPlayer) return new([], schedule, true);
            var next = schedule with { AwaitingPlayerCompletion = false };
            int player = ReadOriginalAiStrategyPlayerForceCurrent(state, current);
            return QueueForceValid(current, player)
                ? new([new(OriginalAiStrategyInvocationKind.PlayerDelegation, player)], next, false)
                : new([], next, false);
        }
        if (schedule.NextForceIndex == schedule.ForceIds.Length) return new([], schedule, true);
        int force = schedule.ForceIds[schedule.NextForceIndex];
        var advanced = schedule with { NextForceIndex = schedule.NextForceIndex + 1 };
        // Re-test each reached row against its actual current receiver after
        // preceding business, rather than prefilter an entire future batch.
        if (!QueueForceValid(current, force)) return new([], advanced, false);
        int currentPlayer = ReadOriginalAiStrategyPlayerForceCurrent(state, current);
        if (force == currentPlayer && !ReadOriginalAiStrategyWholeControlCurrent(current))
            return new([], advanced with { AwaitingPlayerCompletion = true }, true);
        var kind = force is >= 46 and <= 50 ? OriginalAiStrategyInvocationKind.SpecialForce
            : OriginalAiStrategyInvocationKind.ComputerForce;
        return new([new(kind, force)], advanced, false);
    }

    private static bool QueueForceValid(SourceWorldContext current, int force)
    {
        var reference = current.ReadForceTableReference(force is >= 0 and < 52 ? force : 0)
            ?? throw QueueMissing("Force_pointer");
        return QueueKnown(current.ReadForceNativeValidity(reference), "Force_validity");
    }
    private static T QueueKnown<T>(T? value, string field) where T : struct => value ?? throw QueueMissing(field);
    private static InvalidDataException QueueMissing(string field) => new("original_ai.strategy_queue." + field);
}
