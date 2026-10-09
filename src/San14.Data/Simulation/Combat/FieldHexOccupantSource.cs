using San14.Core;

namespace San14.Data;

public sealed record FieldHexOccupantReceipt(int NativeKind, int NativePhysicalSlot,
    int? BaseLocationId, int? GameplayArmyId, ObjectReference? CapturedObject);
public sealed record FieldHexOccupantResult(FieldHexOccupantReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>CE16EB20 selection from the latest saved operation snapshot and
/// one current definition view. Army field admission is a separate170900 gate.</summary>
public static class FieldHexOccupantSource
{
    public static FieldHexOccupantResult ReadCurrent(GameState state,
        SourceWorldContext current, int hexId)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        if (hexId is < 0 or >= 48400) return Unknown("current.16EB20_Hex_index");
        var priority = FieldBasePrioritySource.ReadCurrentBasePriority(current, hexId);
        if (priority is null) return Unknown("current.16EB20_Base_priority");
        var hex = current.ReadHexTableReference(hexId)!;
        if (hex.PhysicalSlot is not int hexPhysical) return None();
        ushort objectLink = current.ReadHexObjectLink(hex)!.Value;
        var obj = current.ReadObjectTableReference(objectLink <= 3000 ? objectLink : 0)!;
        bool objectValid = current.ReadObjectNativeValidity(obj)!.Value;
        if (priority.Value.LocationId is int location)
        {
            int objectPhysical = current.ReadObjectPhysicalSlot(obj)!.Value;
            var selected = objectPhysical <= 51 ? current.ReadCityTableReference(objectPhysical)!
                : current.ReadGateTableReference(objectPhysical - 51)!;
            return new(new(selected.Kind == BaseKind.City ? 5 : 6,
                selected.PhysicalSlot!.Value, location, null, obj), null);
        }

        //An operation's current binding includes any earlier occupancy write.
        //Only an unbound view imports the saved map owner.
        current.BindArmyMapOccupancy(state);
        if (current.ReadHexRaw1C(hex) is not ushort raw)
            return Unknown("current.16EB20_Hex_signed1C");
        int signed = unchecked((short)raw);
        if (signed < 0) return Unknown("branch.16EB20_negative_Army_pointer_index");
        if (signed != 0)
        {
            if (state.NativeArmyLifecycle is not { } arena)
                return Unknown("current.16EB20_Army_pointer_table");
            int lookup = signed <= 500 ? signed : 0;
            var army = current.ReadArmyUnitTableReference(lookup);
            if (army is null)
            {
                if (lookup >= arena.Slots.Length || arena.Slots[lookup].PhysicalSlot != lookup)
                    return Unknown("current.16EB20_Army_pointer_table");
                current.SetArmyUnitTablePointer(lookup, lookup);
                army = new(lookup);
            }
            if (army.PhysicalSlot is int physical)
            {
                if (physical >= arena.Slots.Length) return Unknown("current.16EB20_Army_header");
                var slot = arena.Slots[physical];
                current.BindUnspecifiedOwnedArmyQueryHeader(slot);
                bool? valid = current.ReadArmyUnitNativeValidity(army);
                if (valid is null) return Unknown("current.16EB20_Army_header_validity");
                if (valid.Value)
                {
                    bool? excluded = ReadExcluded(state, current, physical);
                    if (excluded is null) return Unknown("current.16EB20_Army233560");
                    if (excluded.Value) return None(); //No generic-Object fallback.
                    if (slot.ActiveUnitId is not int id || !state.Units.TryGetValue(id, out var unit) ||
                        unit.NativeArmyOrigin?.NativeSlotIndex != physical ||
                        current.ReadArmyUnitCommanderId(army) != unit.OfficerId)
                        return Unknown("current.16EB20_native_Army_gameplay_projection");
                    return new(new(27, physical, null, id, null), null);
                }
            }
        }
        return objectValid
            ? new(new(11, current.ReadObjectPhysicalSlot(obj)!.Value, null, null, obj), null) : None();
    }

    /// <summary>Only the target's native-valid/exclusion/at-Home gates. Hostile
    /// relation and actor gates remain with the reached battle producer.</summary>
    public static bool? ReadOrdinaryFieldArmyEligibility(GameState state,
        SourceWorldContext current, FieldHexOccupantReceipt selected)
    {
        if (selected.NativeKind != 27) return false;
        int physical = selected.NativePhysicalSlot;
        if (state.NativeArmyLifecycle is not { } arena || physical is < 1 or > 500 || physical >= arena.Slots.Length)
            return null;
        var slot = arena.Slots[physical]; current.BindUnspecifiedOwnedArmyQueryHeader(slot);
        var army = new ArmyUnitReference(physical);
        if (current.ReadArmyUnitNativeValidity(army) is not bool valid) return null;
        if (!valid) return false;
        if (ReadExcluded(state, current, physical) is not bool excluded) return null;
        if (excluded) return false;
        if (selected.GameplayArmyId is not int id || slot.ActiveUnitId != id ||
            !state.Units.TryGetValue(id, out var unit) || unit.NativeArmyOrigin?.NativeSlotIndex != physical)
            return null;
        current.BindUnspecifiedOwnedArmyQueryPosition(physical, unit.NativeMarch?.CurrentHex2A ?? unit.NativeCurrentHex2A);
        try { return !current.IsArmyUnitMapWriteSkipped(army); }
        catch (InvalidDataException) { return null; }
    }

    private static bool? ReadExcluded(GameState state, SourceWorldContext current, int physical) =>
        current.ReadArmyExclusionForQuerySnapshot(state, physical);
    private static FieldHexOccupantResult None() => new(new(255, 0, null, null, null), null);
    private static FieldHexOccupantResult Unknown(string owner) => new(null, owner);
}

public sealed partial class SourceWorldContext
{
    internal bool? ReadArmyExclusionForQuerySnapshot(GameState state, int physical) =>
        _ownedDestructionReadOnlyState is not null || OwnedDestructionQueue is null
            ? OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, this, physical)
            : ReadOwnedArmyExclusion233560(new(RegistryKind.Army, physical));

    //Existing operation fields, including explicit unknowns, remain current.
    //Only an unbound field receives the actual saved receiver value.
    internal void BindUnspecifiedOwnedArmyQueryHeader(NativeArmyLifecycleSlot slot)
    {
        _armyCommanderIds.TryAdd(slot.PhysicalSlot, slot.Commander12);
        _armyStatusFlags.TryAdd(slot.PhysicalSlot, slot.Status10);
    }
    internal void BindUnspecifiedOwnedArmyQueryPosition(int physical, ushort? raw2A) =>
        _armyUnitHexes2A.TryAdd(physical, raw2A);
}
