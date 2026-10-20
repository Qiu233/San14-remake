using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    public int? ReadConditionBaseForceSlot(BaseReference receiver) => ReadBaseForceSlot(receiver);
    public bool? ReadConditionBaseSpecialGovernor(BaseReference receiver)
    {
        if (ReadArmyReturnBaseGovernor2E(receiver) is not ushort governor) return null;
        var person = governor <= 6000 ? ReadPersonLookupReference(governor) : ReadTransferPersonTableReference(0);
        if (person is null) return null;
        if (person.PhysicalSlot is null) return false;
        if (ReadPersonFirstLocation(person.PhysicalSlot.Value) is not ushort home) return null;
        if (home is < 1 or > 61) return false;
        if (ReadBaseForceSlot(receiver) is not int owner) return null;
        var force = ReadForceTableReference(owner < 52 ? owner : 0);
        if (force is null) return null;
        if (ReadForcePhysicalSlot(force) is not int identity) return null;
        return identity is >= 46 and <= 50;
    }
    // The event consumes only the ==1 predicate. This normalized carrier is
    // not the exact status code and is never saved back into the item.
    public byte? ReadConditionItemPresence(RegistryObjectReference item) =>
        ReadRegistryItemListEligibility(item) is bool present ? present ? (byte)1 : (byte)0 : null;
    public Func<int, int, bool?>? ConditionForcePolicyEnabledReader { get; set; }
    public bool? ReadConditionForcePolicyEnabled(S14EventConditionForceReference force, int effect) =>
        ConditionForcePolicyEnabledReader?.Invoke(force.PhysicalSlot, effect);
}
