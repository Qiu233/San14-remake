using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

/// <summary>The C96A message's Object13 Force argument, independently of its
/// receiving District. Only mutable source operands are captured here.</summary>
internal static class TrapCounterReportForceNameSource
{
    internal static TrapCounterForceNameOperand Capture(GameState state, SourceWorldContext current, byte argument)
    {
        if (argument is < 1 or > 51) return new(argument, TrapCounterForceNameForm.Empty, null, null);
        var force = current.ReadForceTableReference(argument) ?? throw Missing("P3_Force_pointer");
        int physical = Need(force.PhysicalSlot, "P3_Force_physical");
        byte kingdom = Need(current.ReadForceKingdomNameIndex(physical), "P3_current_Force48");
        if (kingdom is >= 1 and <= 100)
            return new(argument, TrapCounterForceNameForm.CustomKingdom, kingdom, null);
        if (!Need(current.ReadForceNativeValidity(force), "P3_Force_validity"))
            return new(argument, TrapCounterForceNameForm.Empty, null, null);
        physical = Need(current.ReadForcePhysicalSlot(force), "P3_fallback_Force_identity");
        ushort raw = Need(current.ReadForceRecord(physical)?.RawRuler10, "P3_fallback_Ruler10");
        var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("P3_Person_pointer");
        int at = Need(person.PhysicalSlot, "P3_Person_physical");
        if (!Need(current.ReadPersonNativeValidity(at), "P3_Person_validity"))
            return new(argument, TrapCounterForceNameForm.Empty, null, null);
        int identity = Need(current.ReadPersonIdentity(at), "P3_Person_identity");
        var image = state.Officers.GetValueOrDefault(identity) ?? throw Missing("P3_current_Person_owner");
        if (physical is < 46 or > 50)
            return new(argument, TrapCounterForceNameForm.PersonWithArmySuffix, null,
                image.Name ?? throw Missing("P3_current_Name"));
        bool complete = identity is >= 5101 and <= 5200 or >= 2062 and <= 2066;
        return new(argument, TrapCounterForceNameForm.PersonWithoutArmySuffix, null,
            complete ? image.Name ?? throw Missing("P3_current_Name") :
            image.CurrentSurname ?? throw Missing("P3_current_Surname"));
    }
}
