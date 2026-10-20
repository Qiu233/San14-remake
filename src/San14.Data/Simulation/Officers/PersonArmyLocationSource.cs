using San14.Core;

namespace San14.Data;

/// <summary>Ordinary creator SC2D3077/TC2D31F7 calls the actual second-location
/// setter SC2421A0/TC241F20 with kind27 and the selected physical Army ID.
/// Publication belongs to that same deployment transaction, before registration.
/// No Core Unit ID or first/home location participates in this encoding.</summary>
public static class PersonArmyLocationSource
{
    /// <summary>22A200(person,1), then native Army validity. An existing current
    /// pointer/header wins, including aliases. An unbound current view can read
    /// the same campaign's canonical saved source slot and receiver join.</summary>
    public static bool? ReadCommanderArmyValidity(GameState state, SourceWorldContext current, int personPhysical)
    {
        if (current.ReadPersonSecondLocation(personPhysical) is not ushort second ||
            current.ReadPersonIdentity(personPhysical) is not ushort identity) return null;
        int requested = second is >= 62 and <= 561 ? second - 61 : 0;
        var pointer = Lookup(requested);
        if (pointer?.PhysicalSlot is not int physical) return null;
        ushort? commander = current.ReadArmyUnitCommanderId(pointer) ?? Saved(physical)?.Commander12;
        if (commander is null) return null;
        if (commander != identity)
        {
            pointer = Lookup(0);
            if (pointer?.PhysicalSlot is not int fallback) return null;
            physical = fallback;
        }
        bool? valid = current.ReadArmyUnitNativeValidity(pointer);
        if (valid.HasValue) return valid;
        var saved = Saved(physical);
        ushort? finalCommander = current.ReadArmyUnitCommanderId(pointer) ?? saved?.Commander12;
        if (finalCommander is null) return null;
        byte? finalStatus = current.ReadArmyReturnUnitStatus10(pointer) ?? saved?.Status10;
        return finalCommander == 0 ? false : finalStatus is byte status ? status != 0 : null;

        ArmyUnitReference? Lookup(int slot)
        {
            var known = current.ReadArmyUnitTableReference(slot);
            if (known is not null) return known;
            return Saved(slot) is { } source ? new(source.PhysicalSlot) : null;
        }
        ArmyReceiverSourceSlot? Saved(int physicalSlot)
        {
            var lifecycle = state.ReadArmyLifecycle();
            if (lifecycle is null || (uint)physicalSlot >= lifecycle.Slots.Count) return null;
            var source = lifecycle.Slots[physicalSlot];
            //The existing source-admission invariant is ordinal==PhysicalSlot.
            //Campaign ReceiverId is an independent key, never slot+1 arithmetic.
            return source.PhysicalSlot == physicalSlot && lifecycle.Receivers.Find(source.ReceiverId) is not null ? source : null;
        }
    }

    /// <summary>Current second-location lookup and commander identity check.
    /// Does not search live Units or infer an Army from a deployment flag.</summary>
    public static ArmyUnitReference? ReadCommanderArmy(SourceWorldContext current, int personPhysical)
    {
        if (current.ReadPersonSecondLocation(personPhysical) is not ushort second ||
            current.ReadPersonIdentity(personPhysical) is not ushort identity) return null;
        int slot = second is >= 62 and <= 561 ? second - 61 : 0;
        var army = current.ReadArmyUnitTableReference(slot);
        if (army is null || current.ReadArmyUnitCommanderId(army) is not ushort commander) return null;
        return commander == identity ? army : current.ReadArmyUnitTableReference(0);
    }

    public const string ProfileId = "san14:ce-person-army-second-location-20261003";
    public static ushort EncodeArmySecondLocation(int physicalArmySlot)
        => unchecked((uint)(physicalArmySlot - 1)) <= 499 ? (ushort)(physicalArmySlot + 61) : (ushort)0;
}
