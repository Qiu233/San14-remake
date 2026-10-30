using System.Runtime.CompilerServices;
using San14.Core;

namespace San14.Data;

internal sealed record OriginalAiArmyEvaluationChange(ArmyReceiverId ReceiverId, uint? Expected, uint Value);

/// <summary>The common evaluation word belongs to the retained Army receiver.
/// A new logical Army can reuse that same word. Ordinary native getters use
/// their actual source ordinal; publication batches only actual dirty scores.</summary>
internal static class ArmyAiEvaluationSource
{
    private static readonly ConditionalWeakTable<ArmyLifecycleSourceState, ArmyReceiverSourceDirectory> directories = new();

    internal static (ArmyReceiverId ReceiverId, uint? Value) Read(GameState state, int unitId)
    {
        var unit = state.Units.GetValueOrDefault(unitId) ?? throw Missing("active_image");
        int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("receiver_identity");
        var source = Source(state);
        if ((uint)physical >= (uint)source.Slots.Count) throw Missing("receiver_index");
        var slot = source.Slots[physical];
        return (slot.ReceiverId, slot.AiEvaluation);
    }

    internal static uint? ReadReceiver(GameState state, ArmyReceiverId receiverId)
    {
        var source = Source(state);
        var directory = directories.GetValue(source, static owner => new(owner));
        if (!directory.TryResolveSourceOrdinal(receiverId, out int physical)) throw Missing("receiver_link");
        return source.Slots[physical].AiEvaluation;
    }

    internal static GameState Apply(GameState state, IReadOnlyList<OriginalAiArmyEvaluationChange> changes)
    {
        if (changes.Count == 0) return state;
        var lifecycle = state.ReadArmyLifecycle() ?? throw Missing("receiver_owner");
        var source = lifecycle.Source;
        //One index for this publication, never a roster rebuild per score read.
        var directory = directories.GetValue(source, static owner => new(owner));
        ArmyReceiverSourceSlot[]? updated = null;
        foreach (var change in changes)
        {
            if (!directory.TryResolveSourceOrdinal(change.ReceiverId, out int physical)) throw Missing("receiver_link");
            var current = source.Slots[physical];
            //The actual world writer may have updated this receiver since the
            //policy scope borrowed it. Publish only the still-current score.
            if (current.AiEvaluation != change.Expected ||
                (updated is null ? current.AiEvaluation : updated[physical].AiEvaluation) == change.Value) continue;
            updated ??= source.Slots.ToArray();
            updated[physical] = updated[physical] with { AiEvaluation = change.Value };
        }
        return updated is null ? state : state.WithArmyLifecycle(lifecycle with { Source = source with { Slots = updated } });
    }

    private static ArmyLifecycleSourceState Source(GameState state)
        => state.ReadArmyLifecycle()?.Source ?? throw Missing("receiver_owner");
    private static InvalidDataException Missing(string name) => new("original_ai.Army.retained_receiver_evaluation." + name);
}
