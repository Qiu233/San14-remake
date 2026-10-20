using San14.Core;

namespace San14.Data;

/// <summary>Current physical tables consumed by the twelve native registration loops.</summary>
public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, int?> _registryItemPointers = [],
        _registryCautionTroopsPointers = [], _registryTable81B50Pointers = [];
    private readonly Dictionary<int, ItemStatusKnowledge?> _registryItemStatuses = [];
    private Func<int, bool?>? _cautionGroupEligibility, _cautionRelationEligibility;

    internal void SetCautionEligibilityProjection(Func<int, bool?> group, Func<int, bool?> relation)
    { _cautionGroupEligibility = group; _cautionRelationEligibility = relation; }

    /// <summary>Does not infer the physical person table from its independent ID lookup table.</summary>
    public void SetRegistryTablePointer(RegistryKind kind, int slot, int? physicalSlot)
    {
        switch (kind)
        {
            case RegistryKind.Army: SetArmyUnitTablePointer(slot, physicalSlot); break;
            case RegistryKind.City: SetCityTablePointer(slot, physicalSlot); break;
            case RegistryKind.Gate: SetGateTablePointer(slot, physicalSlot); break;
            case RegistryKind.Force: SetForceTablePointer(slot, physicalSlot); break;
            case RegistryKind.Area: SetAreaTablePointer(slot, physicalSlot); break;
            case RegistryKind.District: SetDistrictTablePointer(slot, physicalSlot); break;
            case RegistryKind.Object: SetObjectTablePointer(slot, physicalSlot); break;
            case RegistryKind.Person: SetTransferPersonTablePointer(slot, physicalSlot); break;
            case RegistryKind.Item: SetPointer(_registryItemPointers, slot, 202, physicalSlot, 202); break;
            case RegistryKind.Scenery: SetSceneryTablePointer(slot, physicalSlot); break;
            case RegistryKind.CautionTroops: SetPointer(_registryCautionTroopsPointers, slot, 501, physicalSlot, 501); break;
            case RegistryKind.Table81B50: SetPointer(_registryTable81B50Pointers, slot, 1001, physicalSlot, 1001); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    public RegistryObjectReference? ReadRegistryTableReference(RegistryKind kind, int slot) => kind switch
    {
        RegistryKind.Army => RegistryReference(kind, ReadArmyUnitTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.City => RegistryReference(kind, ReadCityTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.Gate => RegistryReference(kind, ReadGateTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.Force => RegistryReference(kind, ReadForceTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.Area => RegistryReference(kind, ReadAreaTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.District => RegistryReference(kind, ReadDistrictTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.Object => RegistryReference(kind, ReadObjectTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.Person => RegistryReference(kind, ReadTransferPersonTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.Item => ReadRegistryNewTable(kind, slot, 202, _registryItemPointers),
        RegistryKind.Scenery => RegistryReference(kind, ReadSceneryTableReference(slot), value => value.PhysicalSlot),
        RegistryKind.CautionTroops => ReadRegistryNewTable(kind, slot, 501, _registryCautionTroopsPointers),
        RegistryKind.Table81B50 => ReadRegistryNewTable(kind, slot, 1001, _registryTable81B50Pointers),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public bool? ReadRegistryObjectNativeValidity(RegistryObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Kind switch
        {
            RegistryKind.Army => ReadArmyUnitNativeValidity(new(reference.PhysicalSlot)),
            RegistryKind.City => ReadBaseNativeValidity(new(BaseKind.City, reference.PhysicalSlot)),
            RegistryKind.Gate => ReadBaseNativeValidity(new(BaseKind.Gate, reference.PhysicalSlot)),
            RegistryKind.Force => ReadForceNativeValidity(new(reference.PhysicalSlot)),
            RegistryKind.Area => ReadAreaNativeValidity(new(reference.PhysicalSlot)),
            RegistryKind.District => ReadDistrictNativeValidity(new DistrictReference(reference.PhysicalSlot)),
            RegistryKind.Object => ReadObjectNativeValidity(new(reference.PhysicalSlot)),
            RegistryKind.Person => reference.PhysicalSlot.HasValue ? ReadPersonNativeValidity(reference.PhysicalSlot.Value) : false,
            RegistryKind.Item => ReadRegistryItemNativeValidity(reference),
            RegistryKind.Scenery => ReadSceneryNativeValidity(new(reference.PhysicalSlot)),
            RegistryKind.CautionTroops => ReadRegistryCautionTroopsNativeValidity(reference),
            RegistryKind.Table81B50 => ReadRegistryTable81B50NativeValidity(reference),
            _ => throw new ArgumentOutOfRangeException(nameof(reference))
        };
    }

    public byte? ReadRegistryPersonStatus(RegistryObjectReference reference) =>
        ReadPersonStatus(RegistryQueryPhysical(reference, RegistryKind.Person, 1851));
    public void SetRegistryItemRawField50(int physicalSlot, byte? value)
        => SetRegistryItemStatus(physicalSlot, value is byte code ? ItemStatusKnowledge.Exact(code) : null);
    internal void SetRegistryItemStatus(int physicalSlot, ItemStatusKnowledge? value)
    { Check(physicalSlot, 202, "item physical slot"); _registryItemStatuses[physicalSlot] = value; }
    public byte? ReadRegistryItemRawField50(RegistryObjectReference reference) =>
        _registryItemStatuses.GetValueOrDefault(RegistryQueryPhysical(reference, RegistryKind.Item, 202))?.Code;
    public bool? ReadRegistryItemListEligibility(RegistryObjectReference reference) =>
        _registryItemStatuses.GetValueOrDefault(RegistryQueryPhysical(reference, RegistryKind.Item, 202))?.IsListEligible;

    private bool? ReadRegistryItemNativeValidity(RegistryObjectReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = RegistryQueryPhysical(reference, RegistryKind.Item, 202);
        if (physical == 0) return false;
        return ReadRegistryItemListEligibility(reference);
    }
    private bool? ReadRegistryCautionTroopsNativeValidity(RegistryObjectReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = RegistryQueryPhysical(reference, RegistryKind.CautionTroops, 501);
        if (physical == 0) return false;
        return _cautionGroupEligibility?.Invoke(physical);
    }
    private bool? ReadRegistryTable81B50NativeValidity(RegistryObjectReference reference)
    {
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = RegistryQueryPhysical(reference, RegistryKind.Table81B50, 1001);
        if (physical == 0) return false;
        return _cautionRelationEligibility?.Invoke(physical);
    }
    private static int RegistryQueryPhysical(RegistryObjectReference reference, RegistryKind expected, int count)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.Kind != expected) throw new InvalidDataException("Registry field getter has a different native receiver type.");
        return MapPhysical(reference.PhysicalSlot, count, "registry object");
    }
    private static RegistryObjectReference? RegistryReference<T>(RegistryKind kind, T? value, Func<T, int?> physical) where T : class =>
        value is null ? null : new(kind, physical(value));
    private static RegistryObjectReference? ReadRegistryNewTable(RegistryKind kind, int slot, int count, Dictionary<int, int?> pointers)
    {
        Check(slot, count, "registry table slot");
        return pointers.TryGetValue(slot, out int? physical) ? new(kind, physical) : null;
    }
}
