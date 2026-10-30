using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    public Func<int, int, bool, ushort?>? ConditionPersonRelationListReader { get; set; }

    public ushort? ReadConditionPersonRelationListIdentity(S14EventConditionPersonReference person, int index, bool like)
        => (uint)index < 8 ? ConditionPersonRelationListReader?.Invoke(person.PhysicalSlot, index, like) : null;
}
