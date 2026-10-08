using San14.Core;

namespace San14.Data;

/// <summary>The Base-first result of CE16EB20, before its Army/generic-Object
/// branches. This projection does not read or manufacture Hex1C occupancy.</summary>
public static class OriginalFieldBasePrioritySource
{
    public static bool? HasCurrentBasePriority(OriginalEventInitializationContext current, int hexId) =>
        ReadCurrentBasePriority(current, hexId) is { } selected ? selected.LocationId.HasValue : null;

    public static PlayableBaseContactPriority? ReadCurrentBasePriority(OriginalEventInitializationContext current, int hexId)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (hexId is < 0 or >= 48400) return null;
        var hex = current.ReadHexTableReference(hexId);
        if (hex is null) return null;
        if (hex.PhysicalSlot is null) return new(null);
        if (current.ReadHexObjectLink(hex) is not ushort objectLink) return null;
        var obj = current.ReadObjectTableReference(objectLink <= 3000 ? objectLink : 0);
        if (obj is null || current.ReadObjectNativeValidity(obj) is not bool valid) return null;
        if (!valid) return new(null);
        if (current.ReadObjectPhysicalSlot(obj) is not int physical) return null;
        OriginalBaseReference? target;
        if (physical is >= 1 and <= 51) target = current.ReadCityTableReference(physical);
        else if (physical is >= 52 and <= 61) target = current.ReadGateTableReference(physical - 51);
        else return new(null);
        if (target is null || current.ReadBaseNativeValidity(target) is not bool baseValid) return null;
        if (!baseValid) return new(null);
        return current.ReadBaseObjectLink(target) is ushort location ? new(location) : null;
    }
}

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>Read-only ordinary contact seam. One current definition view;
    /// no complete pool import, native movement write, or Core position guess.</summary>
    public bool? HasCurrentBasePriority(GameState state, int hexId) =>
        ReadCurrentBasePriority(state, hexId) is { } selected ? selected.LocationId.HasValue : null;

    public PlayableBaseContactPriority? ReadCurrentBasePriority(GameState state, int hexId) =>
        CaptureCurrentDefinitions(state) is { } current
            ? OriginalFieldBasePrioritySource.ReadCurrentBasePriority(current, hexId) : null;
}
