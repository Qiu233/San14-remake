using San14.Core;

namespace San14.Data;

/// <summary>The low source AI-action word of the actual Army receiver.
/// Active/pending images retain their value; released receivers retain the
/// constructor/reset observation until the next creation admits that slot.</summary>
internal static class ArmyAiActionFlagsSource
{
    internal static ushort? Read(GameState state, int physical)
    {
        var arena = state.ReadArmyLifecycle() ?? throw Missing("receiver_owner");
        if ((uint)physical >= (uint)arena.Slots.Count) throw Missing("receiver_index");
        var source = arena.Source.Slots[physical];
        var slot = arena.Receivers.Find(source.ReceiverId) ?? throw Missing("receiver_link");
        if (slot.ActiveUnitId is int unit)
            return (state.Units.GetValueOrDefault(unit) ?? throw Missing("active_image")).AiActionFlags;
        return slot.PendingImage is { } pending ? pending.AiActionFlags : source.AiActionFlags;
    }

    internal static GameState Or(GameState state, int unitId, ushort bits)
    {
        var army = state.Units.GetValueOrDefault(unitId) ?? throw Missing("active_image");
        ushort old = Read(state, army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("receiver_identity"))
            ?? throw Missing("retained_word");
        return Write(state, unitId, unchecked((ushort)(old | bits)));
    }

    internal static GameState Write(GameState state, int unitId, ushort flags)
    {
        var army = state.Units.GetValueOrDefault(unitId) ?? throw Missing("active_image");
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("receiver_identity");
        var arena = state.ReadArmyLifecycle() ?? throw Missing("receiver_owner");
        if ((uint)physical >= (uint)arena.Slots.Count) throw Missing("receiver_index");
        if (army.AiActionFlags == flags && arena.Source.Slots[physical].AiActionFlags == flags) return state;
        var slots = arena.Source.Slots.ToArray();
        slots[physical] = slots[physical] with { AiActionFlags = flags };
        return (state with { Units = new(state.Units) { [unitId] = army with { AiActionFlags = flags } } })
            .WithArmyLifecycle(arena with { Source = arena.Source with { Slots = slots } });
    }

    private static InvalidDataException Missing(string name) => new("current.army_ai_action_flags." + name);
}
