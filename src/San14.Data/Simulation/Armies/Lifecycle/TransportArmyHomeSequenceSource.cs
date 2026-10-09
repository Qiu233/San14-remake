using San14.Core;

namespace San14.Data;

/// <summary>Current word28 belongs to the actual physical receiver's active or
/// pending image. A missing old-save field remains unknown. The creator must
/// first decide whether that header/Home is reached; this reader does not
/// substitute a Gameplay unit ID or enumerate Units to rebuild the table.</summary>
public static class TransportArmyHomeSequenceSource
{
    public static ushort? ReadCurrentWord28(GameState state, int nativeSlot)
    {
        if (state.NativeArmyLifecycle is not { } arena || nativeSlot < 1 || nativeSlot >= arena.Slots.Length)
            return null;
        var slot = arena.Slots[nativeSlot];
        UnitState? image = slot.PendingImage;
        if (slot.ActiveUnitId is int id)
            state.Units.TryGetValue(id, out image);
        return image?.NativeArmyOrigin?.NativeSlotIndex == nativeSlot && image.OfficerId == slot.Commander12
            ? image.NativeHomeSequence28 : null;
    }
}
