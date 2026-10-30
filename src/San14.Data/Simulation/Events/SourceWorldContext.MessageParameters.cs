using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    public int? ReadConditionForceExperience(S14EventConditionForceReference force) =>
        ReadForce164(new(force.PhysicalSlot));
    public int? ReadConditionBaseSupplies(BaseReference reference) =>
        ReadBaseResource(reference, BaseResourceKind.Supplies);
    public int? ReadConditionForceIdentity(S14EventConditionForceReference force) =>
        ReadForcePhysicalSlot(new(force.PhysicalSlot));
    public int? ReadConditionAreaIdentity(AreaReference area) => ReadAreaPhysicalSlot(area);
}
