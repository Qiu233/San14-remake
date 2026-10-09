using San14.Core;

namespace San14.Data;

public sealed record ArmyDestructionTrackingWrite(int ArmyPhysicalSlot, byte Counter25Before, byte Counter25After);

/// <summary>TC259360's first manager60 tail (25B9xx): read raw3E, then25
/// only for matching victim identity. The source has no3C/native-validity gate.
/// Its saved receiver may be an active Army or a retained pending image.</summary>
public static class ArmyDestructionTrackingTail
{
    public static (GameState State, ArmyDestructionTrackingWrite[] Writes) ApplyCurrent(
        GameState state, SourceWorldContext current, int victimPhysical)
    {
        var arena = state.NativeArmyLifecycle ?? throw Missing("current.manager60_saved_receiver");
        Dictionary<int, UnitState>? units = null;
        NativeArmyLifecycleSlot[]? slots = null;
        var writes = new List<ArmyDestructionTrackingWrite>();
        foreach (var receiver in ArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
        {
            int physical = receiver.PhysicalSlot ?? throw Missing("current.manager60_Army_pointer");
            var slot = arena.Slots[physical];
            var image = slot.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : slot.PendingImage;
            if (image is null || ArmyOverrideTargetSourceAdapter.Read(state, image).Identity3E is not ushort identity)
            {
                //An already known zero25 makes the complete conjunction false
                //for every possible3E. It proves no writer without inventing a
                //raw identity in legacy saves. Nonzero/unknown25 still needs3E.
                if (image?.NativeTacticRuntime?.Abnormal25 == 0) continue;
                throw Missing("current.manager60_Army3E");
            }
            if (identity != victimPhysical) continue;
            byte before = image!.NativeTacticRuntime?.Abnormal25 ?? throw Missing("current.manager60_Army25");
            if (before == 0) continue;
            //231480 reads current TC1A49050, the already installed SC semantic
            //1A4A080. Source clamping of a request0 wraps a negative cap.
            int cap = ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound)
                ?? throw Missing("current.manager60_231480_cap");
            byte after = cap < 0 ? unchecked((byte)cap) : (byte)0;
            var changed = ArmyOverrideTargetWrites.Apply(image with
            { NativeTacticRuntime = image.NativeTacticRuntime! with { Abnormal25 = after, ProvocationOwner = null } }, ArmyOverrideTargetState.Cleared);
            slots ??= arena.Slots.ToArray();
            if (slot.ActiveUnitId is int id)
            {
                units ??= new(state.Units);
                units[id] = changed;
            }
            slots[physical] = slot with { PendingImage = slot.PendingImage is null ? null : changed,
                OverrideTarget = changed.OverrideTarget };
            writes.Add(new(physical, before, after));
        }
        return slots is null ? (state, []) :
            (state with { Units = units ?? state.Units, NativeArmyLifecycle = arena with { Slots = slots } }, writes.ToArray());
    }

    private static InvalidDataException Missing(string owner) => new("Ordinary Army destruction source owner is unknown: " + owner);
}
