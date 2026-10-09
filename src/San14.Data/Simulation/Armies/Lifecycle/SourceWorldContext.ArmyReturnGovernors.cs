namespace San14.Data;

public sealed partial class SourceWorldContext
{
    //No implicit zero or Core GovernorId synchronization: callers materialize
    //the actual current native word2E before preparing a source update.
    private readonly Dictionary<BaseReference, ushort?> _armyReturnGovernorWords2E = [];
    public ushort? ReadArmyReturnBaseGovernor2E(BaseReference reference)
    { _ = BasePhysical(reference); return _armyReturnGovernorWords2E.GetValueOrDefault(reference); }
    public void SetArmyReturnBaseGovernor2E(BaseReference reference, ushort? value)
    { _ = BasePhysical(reference); _armyReturnGovernorWords2E[reference] = value; }
    public ushort? ReadArmyReturnPersonFlags196(int physical)
    { Check(physical, 1851, "governor person"); return _persons.GetValueOrDefault(physical)?.Flags; }
}
