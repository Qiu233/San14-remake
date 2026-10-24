namespace San14.Data;

/// <summary>Current22E930 selection in actual District registry order.
/// This getter does not construct history selection or query abilities.</summary>
internal static class ForcePrimaryDistrictSource
{
    internal static DistrictReference Resolve(SourceWorldContext current, ForceReference retainedForce)
    {
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("District_D0"))
        {
            var district = entry ?? throw Missing("District_payload");
            int physical = Need(district.PhysicalSlot, "District_pointer");
            int force = Need(current.ReadForcePhysicalSlot(retainedForce), "Force_ordinal");
            if (force is < 1 or > 51 || Need(current.ReadDistrictForceId(physical), "District10_filter") != force) continue;
            ushort leader = Need(current.ReadDistrictLeader(physical), "District12");
            int freshForce = Need(current.ReadForcePhysicalSlot(retainedForce), "Force_ordinal");
            ushort ruler = freshForce == 0 ? (ushort)5014 :
                Need(current.ReadForceRecord(Need(retainedForce.PhysicalSlot, "Force_pointer"))?.RawRuler10, "Force10");
            if (leader == ruler) return district;
            if (Need(current.ReadDistrictNumber(physical), "District11") == 1) return district;
        }
        return current.ReadDistrictTableReference(0) ?? throw Missing("WorldE0D0");
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("force-primary-district." + field);
}
