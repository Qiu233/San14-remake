using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext : IS14MessageParameterConditionQueries
{
    public byte? ReadConditionPersonIllness(S14EventConditionPersonReference person) =>
        OfficerHealthSourceAdapter.ReadByte(this, person.PhysicalSlot);
}
