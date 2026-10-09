using San14.Data.Formats;

namespace San14.Data;

/// <summary>Projects CE Gate current durability from its associated Object14.
/// Gate46 is a capacity input; final post-load settings/effects are separate.</summary>
public static class GateSourceNormalization
{
    public static int? CurrentDurability(GatePreviewRecord gate, uint marker,
        S14ScenarioObjectCatalog? objects)
    {
        ArgumentNullException.ThrowIfNull(gate);
        // Unsupported editions and old snapshots have no proved Object owner.
        if (marker != 0x66 || objects is null) return null;
        // The actual330D80→330ED9 writer selects Gate(S-51) for physical
        // Object slots52..61. Map hex, owner and display name do not supply S.
        if (gate.Slot is < 1 or > 10 || gate.LocationSlot != 51 + gate.Slot ||
            gate.LocationSlot >= objects.Objects.Count)
            throw new InvalidDataException($"Gate {gate.Slot} has no associated source object.");
        S14ScenarioObjectRecord linked = objects.Objects[gate.LocationSlot];
        // Expected raw12=2 constrains this supported static source profile;
        // original Gate validity/classification is not a raw12==2 predicate.
        if (linked.PhysicalSlot != gate.LocationSlot || linked.RawField12 != 2 ||
            gate.HexId != linked.RawField10 || gate.ForceSlot != linked.RawField13)
            throw new InvalidDataException($"Gate {gate.Slot} associated source object differs from its gate record.");
        // Gate slot7 reads the current linked Object14 before resubmitting it.
        // Do not apply missing post-load capacity/settings/policy inputs here.
        return linked.RawField14;
    }
}
