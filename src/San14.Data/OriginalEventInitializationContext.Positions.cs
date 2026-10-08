namespace San14.Data;

public sealed record OriginalPersonRoleAndPositionResult(bool LostForceWritten,
    OriginalPersonBaseSyncResult BaseSync, OriginalPersonPositionRepairResult PositionRepair);

public sealed partial class OriginalEventInitializationContext : IOriginalPersonPositionRepairState, IOriginalPersonLinkedEntityLookupState
{
    private readonly Dictionary<int, int?> _armyPointers = [];
    private readonly Dictionary<int, ushort?> _armyCommanderIds = [];
    private readonly Dictionary<int, byte?> _armyStatusFlags = [];
    private IReadOnlyList<OriginalPersonLinkedEntityReference?>? _personLinkedEntities;
    private readonly Dictionary<int, int?> _linkedEntityStates = [];
    private readonly Dictionary<int, IReadOnlyList<uint?>?> _linkedEntityMembers = [];

    /// <summary>Contiguous A/B/C-E position stages, after their original preceding person normalization.</summary>
    public OriginalPersonRoleAndPositionResult RepairPersonRoleAndLocations(int physicalSlot)
    {
        bool lost = OriginalPersonRoleLocation.ApplyLostForce(physicalSlot, this);
        OriginalPersonBaseSyncResult saved = OriginalPersonBaseSync.Apply(physicalSlot, this);
        return new(lost, saved, OriginalPersonPositionRepair.Apply(physicalSlot, saved, this));
    }

    public void WritePersonFirstLocation(int physicalSlot, ushort location) => SetPersonFirstLocation(physicalSlot, location);
    public void SetArmyUnitTablePointer(int slot, int? physicalSlot) => SetPointer(_armyPointers, slot, 501, physicalSlot, 501);
    public OriginalArmyUnitReference? ReadArmyUnitTableReference(int slot)
    { Check(slot, 501, "army unit lookup slot"); return _armyPointers.TryGetValue(slot, out int? physical) ? new(physical) : null; }
    public void SetArmyUnitCommanderId(int physicalSlot, ushort? value)
    { Check(physicalSlot, 501, "army unit physical slot"); _armyCommanderIds[physicalSlot] = value; }
    public void SetArmyUnitStatusFlag10(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "army unit physical slot"); _armyStatusFlags[physicalSlot] = value; }
    public ushort? ReadArmyUnitCommanderId(OriginalArmyUnitReference reference) =>
        _armyCommanderIds.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 501, "army unit"));
    public bool? ReadArmyUnitNativeValidity(OriginalArmyUnitReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = MapPhysical(reference.PhysicalSlot, 501, "army unit");
        if (physical == 0) return false;
        ushort? commander = _armyCommanderIds.GetValueOrDefault(physical);
        if (!commander.HasValue) return null;
        if (commander.Value == 0) return false;
        byte? status = _armyStatusFlags.GetValueOrDefault(physical);
        return status.HasValue ? status.Value != 0 : null;
    }
    /// <summary>Only consumed 225CD0 reset fields, in original order; not the complete unit reset.</summary>
    public void ResetArmyUnitPositionFields(int physicalSlot)
    { SetArmyUnitCommanderId(physicalSlot, 0); SetArmyUnitStatusFlag10(physicalSlot, 0); }

    public void SetCurrentPersonLinkedEntities(IReadOnlyList<OriginalPersonLinkedEntityReference?>? values) =>
        _personLinkedEntities = values?.ToArray();
    public IReadOnlyList<OriginalPersonLinkedEntityReference?>? ReadCurrentPersonLinkedEntities() =>
        _currentAssociatedContainer is not null ? new AssociatedChainSequence(this, _currentAssociatedContainer) :
        _personLinkedEntities is null ? null : Array.AsReadOnly(_personLinkedEntities.ToArray());
    public void SetLinkedEntityRawState58(int identity, int? value)
    {
        if (identity < 0) throw new InvalidDataException("Negative opaque linked-entity identity.");
        _linkedEntityStates[identity] = value;
    }
    public bool? ReadLinkedEntityNativeValidity(OriginalPersonLinkedEntityReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.Identity.HasValue) return false;
        int? state = _linkedEntityStates.GetValueOrDefault(LinkedIdentity(reference));
        return state.HasValue ? state.Value != -1 : null;
    }
    public void SetLinkedEntityMemberIds(int identity, IReadOnlyList<uint?>? values)
    {
        if (identity < 0) throw new InvalidDataException("Negative opaque linked-entity identity.");
        _linkedEntityMembers[identity] = values?.ToArray();
    }
    public IReadOnlyList<uint?>? ReadLinkedEntityMemberIds(OriginalPersonLinkedEntityReference reference)
    {
        IReadOnlyList<uint?>? values = _linkedEntityMembers.GetValueOrDefault(LinkedIdentity(reference));
        return values is null ? null : Array.AsReadOnly(values.ToArray());
    }
    public OriginalPersonLinkedEntityReference? ReadPersonLinkedEntity(int physicalSlot) => OriginalPersonLinkedEntityLookup.Find(physicalSlot, this);
    private static int LinkedIdentity(OriginalPersonLinkedEntityReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        int identity = reference.Identity ?? throw new InvalidDataException("Null linked entity cannot provide native members.");
        if (identity < 0) throw new InvalidDataException("Negative opaque linked-entity identity.");
        return identity;
    }
}
