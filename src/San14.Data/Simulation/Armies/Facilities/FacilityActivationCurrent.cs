using San14.Core;

namespace San14.Data;

/// <summary>Only the proven Object16 activation fact and two byte-mask
/// primitives. These do not execute construction/repair or admit effects.</summary>
public static class FacilityActivationCurrent
{
    public static bool? ReadEffectsEnabled(SourceWorldContext current, ObjectReference receiver) =>
        current.ReadMapObjectFields16(receiver) is ushort value ? (value & 1) != 0 : null;

    internal static GameState ClearForConstruction(GameState state, SourceWorldContext current,
        ObjectReference receiver) => WriteBit(state, current, receiver, false);

    internal static GameState EnableAfterRepairCompletion(GameState state, SourceWorldContext current,
        ObjectReference receiver) => WriteBit(state, current, receiver, true);

    private static GameState WriteBit(GameState state, SourceWorldContext current,
        ObjectReference receiver, bool enabled)
    {
        var source = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource
            ?? throw Missing("saved_source_owner");
        var directory = current.CurrentMapObjectDirectory ?? throw Missing("current_source_owner");
        int physical = receiver.PhysicalSlot ?? throw Missing("receiver_pointer");
        var id = directory.ReceiverAtPhysical(physical);
        if (source.PhysicalReceivers[physical] != id || state.MapObjects?.Find(id) is null)
            throw Missing("receiver_identity");
        ushort previous = current.ReadMapObjectFields16(receiver) ?? throw Missing("current_fields16");
        ushort next = enabled ? (ushort)(previous | 1) : (ushort)(previous & 0xfffe);
        var updated = source.WithCurrentFields16(physical, next);
        var resource = CampaignSourceKnowledge.From(state.ResourceState).WithMapObjectSource(updated);
        current.WriteMapObjectFields16(receiver, next);
        return state with { ResourceState = resource };
    }

    private static InvalidDataException Missing(string field) => new("facility.activation." + field);
}
