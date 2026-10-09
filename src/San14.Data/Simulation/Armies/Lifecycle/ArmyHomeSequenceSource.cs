using San14.Core;

namespace San14.Data;

/// <summary>TC2D2CA0's word28 selection. Ordinary creation reaches only
/// native-valid receivers accepted by2C96E0 at their current Home. Transport
/// has its separate scan over every valid receiver, including field Armies.</summary>
public static class ArmyHomeSequenceSource
{
    public static ushort? ReadNextOrdinary(GameState state, SourceWorldContext current, int commanderId)
    {
        if (state.NativeArmyLifecycle is not { Slots.Length: 501 } arena ||
            current.ReadPersonLookupReference(commanderId)?.PhysicalSlot is not int commander ||
            current.ReadPersonFirstLocation(commander) is not ushort home) return null;
        try
        {
            int sequence = 0;
            foreach (var slot in arena.Slots)
            {
                current.BindUnspecifiedOwnedArmyQueryHeader(slot);
                var army = new ArmyUnitReference(slot.PhysicalSlot);
                if (current.ReadArmyUnitNativeValidity(army) is not bool valid) return null;
                if (!valid) continue;
                UnitState? image = slot.PendingImage;
                if (slot.ActiveUnitId is int id) state.Units.TryGetValue(id, out image);
                current.BindUnspecifiedOwnedArmyQueryPosition(slot.PhysicalSlot, image?.NativeMarch?.CurrentHex2A ?? image?.NativeCurrentHex2A);
                if (!current.IsArmyUnitMapWriteSkipped(army)) continue;
                if (current.ReadArmyUnitCommanderId(army) is not ushort otherId ||
                    current.ReadPersonLookupReference(otherId)?.PhysicalSlot is not int otherPerson ||
                    current.ReadPersonFirstLocation(otherPerson) is not ushort otherHome) return null;
                if (otherHome != home) continue;
                ushort? previous = TransportArmyHomeSequenceSource.ReadCurrentWord28(state, slot.PhysicalSlot);
                if (previous is null) return null;
                if (sequence <= previous.Value) sequence = previous.Value + 1;
            }
            return (ushort)Math.Clamp(sequence, 0, 500);
        }
        catch (InvalidDataException) { return null; }
    }
}
