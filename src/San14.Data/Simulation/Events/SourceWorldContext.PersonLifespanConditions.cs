using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    public Func<int, OfficerLifespanState?>? ConditionLifespanReader { get; set; }
    public Func<int?>? ConditionLifespanModeReader { get; set; }

    public bool? ReadConditionPersonAtTerminalYear(int physical) =>
        ReadConditionPersonAtTerminalYear(physical, ConditionLifespanReader, ConditionLifespanModeReader, null);

    // A normal command supplies its actual reached year and current Person
    // owner without binding or rescanning the unrelated event-condition pool.
    internal bool? ReadConditionPersonAtTerminalYear(int physical,
        Func<int, OfficerLifespanState?>? readLifespan, Func<int?>? readMode, int? sourceYear)
    {
        if (ReadConditionPersonFlags(new(physical)) is not ushort flags) return null;
        if ((flags & 8) != 0) return false;
        var force = ReadPersonForce(physical);
        if (force is null || ReadForceNativeValidity(force) is not bool valid) return null;
        if (!valid) return false;
        if (CurrentPlayerForce is null) return null;
        if (CurrentPlayerForce.PhysicalSlot is not int player) throw new InvalidDataException("Terminal-year predicate has a null current-player force pointer.");
        if (player == 0 || player != force.PhysicalSlot) return false;
        if (ReadPersonIdentity(physical) is not ushort id) return null;
        if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id))
        {
            if (ReadPersonFirstLocation(physical) is not ushort location) return null;
            if (location is >= 1 and <= 61 && force.PhysicalSlot is not (>= 46 and <= 50)) return false;
        }
        if (readLifespan?.Invoke(physical) is not { } lifespan) return null;
        if (ReadConditionPersonLifespanExtension(id) is not int extension) return null;
        if (readMode?.Invoke() is not int mode) return null;
        if (sourceYear is null)
        {
            if (ReadConditionDate() is not { } date) return null;
            sourceYear = unchecked((short)date.Year);
        }
        int year = sourceYear.Value;
        int terminal = lifespan.TerminalYear + extension;
        return mode switch
        {
            0 => lifespan.Kind switch { 0 => year >= terminal, 1 => year >= terminal + 5, _ => false },
            1 => lifespan.Kind switch { 0 => year >= terminal + 20, 1 => year >= terminal + 25, _ => false },
            _ => false
        };
    }

    public int? ReadConditionPersonLifespanExtension(ushort personId)
    {
        if (personId is < 1 or > 6000) return 0;
        if (ReadRegistryReferences(RegistryKind.Item) is not { } items) return null;
        int maximum = 0;
        foreach (var entry in items)
        {
            if (entry is not { Kind: RegistryKind.Item, PhysicalSlot: int item }) return null;
            if (ReadCaptureItemOwner4A(item) is not ushort owner) return null;
            if (owner != personId) continue;
            if (ReadCaptureItemSubtype47(item) is not byte kind) return null;
            if (kind != 2) continue;
            if (ReadItemLifespanExtension(item) is not byte value) return null;
            maximum = Math.Max(maximum, Math.Min((int)value, 10));
        }
        return maximum;
    }
}
