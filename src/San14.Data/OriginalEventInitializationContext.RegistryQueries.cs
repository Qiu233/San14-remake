namespace San14.Data;

/// <summary>Current physical tables consumed by the twelve native registration loops.</summary>
public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<int, int?> _registryItemPointers = [],
        _registryCautionTroopsPointers = [], _registryTable81B50Pointers = [];
    private readonly Dictionary<int, byte?> _registryItemFields50 = [];
    private readonly Dictionary<int, ushort?> _registryCautionTroopsFields18 = [],
        _registryTable81B50Fields10 = [], _registryTable81B50Fields12 = [];

    /// <summary>Does not infer the physical person table from its independent ID lookup table.</summary>
    public void SetRegistryTablePointer(OriginalRegistryKind kind, int slot, int? physicalSlot)
    {
        switch (kind)
        {
            case OriginalRegistryKind.Army: SetArmyUnitTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.City: SetCityTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.Gate: SetGateTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.Force: SetForceTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.Area: SetAreaTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.District: SetDistrictTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.Object: SetObjectTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.Person: SetTransferPersonTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.Item: SetPointer(_registryItemPointers, slot, 202, physicalSlot, 202); break;
            case OriginalRegistryKind.Scenery: SetSceneryTablePointer(slot, physicalSlot); break;
            case OriginalRegistryKind.CautionTroops: SetPointer(_registryCautionTroopsPointers, slot, 501, physicalSlot, 501); break;
            case OriginalRegistryKind.Table81B50: SetPointer(_registryTable81B50Pointers, slot, 1001, physicalSlot, 1001); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    public OriginalRegistryObjectReference? ReadRegistryTableReference(OriginalRegistryKind kind, int slot) => kind switch
    {
        OriginalRegistryKind.Army => RegistryReference(kind, ReadArmyUnitTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.City => RegistryReference(kind, ReadCityTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.Gate => RegistryReference(kind, ReadGateTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.Force => RegistryReference(kind, ReadForceTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.Area => RegistryReference(kind, ReadAreaTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.District => RegistryReference(kind, ReadDistrictTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.Object => RegistryReference(kind, ReadObjectTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.Person => RegistryReference(kind, ReadTransferPersonTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.Item => ReadRegistryNewTable(kind, slot, 202, _registryItemPointers),
        OriginalRegistryKind.Scenery => RegistryReference(kind, ReadSceneryTableReference(slot), value => value.PhysicalSlot),
        OriginalRegistryKind.CautionTroops => ReadRegistryNewTable(kind, slot, 501, _registryCautionTroopsPointers),
        OriginalRegistryKind.Table81B50 => ReadRegistryNewTable(kind, slot, 1001, _registryTable81B50Pointers),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public bool? ReadRegistryObjectNativeValidity(OriginalRegistryObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Kind switch
        {
            OriginalRegistryKind.Army => ReadArmyUnitNativeValidity(new(reference.PhysicalSlot)),
            OriginalRegistryKind.City => ReadBaseNativeValidity(new(OriginalBaseKind.City, reference.PhysicalSlot)),
            OriginalRegistryKind.Gate => ReadBaseNativeValidity(new(OriginalBaseKind.Gate, reference.PhysicalSlot)),
            OriginalRegistryKind.Force => ReadForceNativeValidity(new(reference.PhysicalSlot)),
            OriginalRegistryKind.Area => ReadAreaNativeValidity(new(reference.PhysicalSlot)),
            OriginalRegistryKind.District => ReadDistrictNativeValidity(new OriginalDistrictReference(reference.PhysicalSlot)),
            OriginalRegistryKind.Object => ReadObjectNativeValidity(new(reference.PhysicalSlot)),
            OriginalRegistryKind.Person => reference.PhysicalSlot.HasValue ? ReadPersonNativeValidity(reference.PhysicalSlot.Value) : false,
            OriginalRegistryKind.Item => ReadRegistryItemNativeValidity(reference),
            OriginalRegistryKind.Scenery => ReadSceneryNativeValidity(new(reference.PhysicalSlot)),
            OriginalRegistryKind.CautionTroops => ReadRegistryCautionTroopsNativeValidity(reference),
            OriginalRegistryKind.Table81B50 => ReadRegistryTable81B50NativeValidity(reference),
            _ => throw new ArgumentOutOfRangeException(nameof(reference))
        };
    }

    public byte? ReadRegistryPersonStatus(OriginalRegistryObjectReference reference) =>
        ReadPersonStatus(RegistryQueryPhysical(reference, OriginalRegistryKind.Person, 1851));
    public void SetRegistryItemRawField50(int physicalSlot, byte? value)
    { Check(physicalSlot, 202, "item physical slot"); _registryItemFields50[physicalSlot] = value; }
    public byte? ReadRegistryItemRawField50(OriginalRegistryObjectReference reference) =>
        _registryItemFields50.GetValueOrDefault(RegistryQueryPhysical(reference, OriginalRegistryKind.Item, 202));
    public void SetRegistryCautionTroopsRawField18(int physicalSlot, ushort? value)
    { Check(physicalSlot, 501, "caution troops physical slot"); _registryCautionTroopsFields18[physicalSlot] = value; }
    public void SetRegistryTable81B50RawField10(int physicalSlot, ushort? value)
    { Check(physicalSlot, 1001, "caution army unit physical slot"); _registryTable81B50Fields10[physicalSlot] = value; }
    public void SetRegistryTable81B50RawField12(int physicalSlot, ushort? value)
    { Check(physicalSlot, 1001, "caution army unit physical slot"); _registryTable81B50Fields12[physicalSlot] = value; }

    private bool? ReadRegistryItemNativeValidity(OriginalRegistryObjectReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = RegistryQueryPhysical(reference, OriginalRegistryKind.Item, 202);
        if (physical == 0) return false;
        byte? value = _registryItemFields50.GetValueOrDefault(physical);
        return value.HasValue ? value.Value == 1 : null;
    }
    private bool? ReadRegistryCautionTroopsNativeValidity(OriginalRegistryObjectReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = RegistryQueryPhysical(reference, OriginalRegistryKind.CautionTroops, 501);
        if (physical == 0) return false;
        ushort? value = _registryCautionTroopsFields18.GetValueOrDefault(physical);
        return value.HasValue ? value.Value != 0 : null;
    }
    private bool? ReadRegistryTable81B50NativeValidity(OriginalRegistryObjectReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = RegistryQueryPhysical(reference, OriginalRegistryKind.Table81B50, 1001);
        if (physical == 0) return false;
        ushort? first = _registryTable81B50Fields10.GetValueOrDefault(physical);
        if (!first.HasValue) return null;
        if (first.Value is < 1 or > 500) return false;
        ushort? second = _registryTable81B50Fields12.GetValueOrDefault(physical);
        return second.HasValue ? second.Value is >= 1 and <= 61 : null;
    }
    private static int RegistryQueryPhysical(OriginalRegistryObjectReference reference, OriginalRegistryKind expected, int count)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.Kind != expected) throw new InvalidDataException("Registry field getter has a different native receiver type.");
        return MapPhysical(reference.PhysicalSlot, count, "registry object");
    }
    private static OriginalRegistryObjectReference? RegistryReference<T>(OriginalRegistryKind kind, T? value, Func<T, int?> physical) where T : class =>
        value is null ? null : new(kind, physical(value));
    private static OriginalRegistryObjectReference? ReadRegistryNewTable(OriginalRegistryKind kind, int slot, int count, Dictionary<int, int?> pointers)
    {
        Check(slot, count, "registry table slot");
        return pointers.TryGetValue(slot, out int? physical) ? new(kind, physical) : null;
    }
}
