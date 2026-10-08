using San14.Data.Formats;

namespace San14.Data;

/// <summary>Verified native predicates. Null means a consumed current field is unknown.</summary>
public static class OriginalNativeValidity
{
    public static bool Person(S14ConditionNativePerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return Person(person.PhysicalSlot, person.CharacterId, person.RawFlags196,
            person.RawFlag1FD, person.RawStatus11E)
            ?? throw new InvalidDataException("Complete native person fields cannot yield unknown validity.");
    }

    public static bool? Person(int physicalSlot, ushort? characterId, ushort? flags196,
        byte? tail1FD, byte? status11E)
    {
        if (physicalSlot < 0) throw new InvalidDataException("Negative native person physical slot.");
        if (physicalSlot == 0) return false;
        if (flags196 is not ushort flags) return null;
        if ((flags & 0x4000) != 0) return tail1FD.HasValue ? tail1FD.Value != 0 : null;
        if (characterId is not ushort id) return null;
        if (unchecked((ushort)(id - 5001)) <= 99) return true;
        return status11E.HasValue ? status11E.Value != 0 : null;
    }

    public static bool? District(int physicalSlot, byte? force10, byte? number11, ushort? leader12)
    {
        if (physicalSlot < 0) throw new InvalidDataException("Negative native district physical slot.");
        if (physicalSlot == 0) return false;
        if (force10 is not byte force) return null;
        if (force == 0) return false;
        if (number11 is not byte number) return null;
        if (number == 0) return false;
        return leader12.HasValue ? leader12.Value != 0 : null;
    }

    public static bool? Force(int physicalSlot, ushort? ruler10)
    {
        if (physicalSlot < 0) throw new InvalidDataException("Negative native force physical slot.");
        if (physicalSlot == 0) return false;
        return ruler10.HasValue ? ruler10.Value != 0 : null;
    }
}
