using San14.Core;

namespace San14.Data;

public sealed record PersonRoleAndPositionResult(bool LostForceWritten,
    PersonBaseSyncResult BaseSync, PersonPositionRepairResult PositionRepair);

public sealed partial class SourceWorldContext : IPersonPositionRepairState, IPersonLinkedEntityLookupState
{
    private readonly Dictionary<int, int?> _armyPointers = [];
    //Table aliases are resolved on each lookup. These handles contain only
    //immutable physical identity, never current Army validity or mutable fields.
    private ArmyUnitReference?[]? _armyTableReferences;
    private ArmyUnitReference? _unknownArmyTableReference;
    private readonly Dictionary<int, ushort?> _armyCommanderIds = [];
    private readonly Dictionary<int, byte?> _armyStatusFlags = [];
    private IReadOnlyList<PersonLinkedEntityReference?>? _personLinkedEntities;
    private readonly Dictionary<int, int?> _linkedEntityStates = [];
    private readonly Dictionary<int, IReadOnlyList<uint?>?> _linkedEntityMembers = [];
    // A retained map current borrows its exact saved command inputs. This is
    // not a lifecycle owner: no manager, tree, chain or callbacks are installed.
    private PersonnelCommandTasks? _readOnlyPersonLinkedEntityInputs;
    internal void BindReadOnlyPersonLinkedEntityInputs(PersonnelCommandTasks? owner) =>
        _readOnlyPersonLinkedEntityInputs = owner;
    internal PersonnelCommandTasks? ReadPersonLinkedEntityCommandInputs() =>
        ReadOwnedPersonCommandManager() ?? _readOnlyPersonLinkedEntityInputs;

    /// <summary>Contiguous A/B/C-E position stages, after their original preceding person normalization.</summary>
    public PersonRoleAndPositionResult RepairPersonRoleAndLocations(int physicalSlot)
    {
        bool lost = PersonRoleLocation.ApplyLostForce(physicalSlot, this);
        PersonBaseSyncResult saved = PersonBaseSync.Apply(physicalSlot, this);
        return new(lost, saved, PersonPositionRepair.Apply(physicalSlot, saved, this));
    }

    public void WritePersonFirstLocation(int physicalSlot, ushort location) => SetPersonFirstLocation(physicalSlot, location);
    public void SetArmyUnitTablePointer(int slot, int? physicalSlot) => SetPointer(_armyPointers, slot, 501, physicalSlot, 501);
    public ArmyUnitReference? ReadArmyUnitTableReference(int slot)
    {
        Check(slot, 501, "army unit lookup slot");
        if(!_armyPointers.TryGetValue(slot,out int? physical))return null;
        if(physical is not int identity)return _unknownArmyTableReference??=new(null);
        //Keep the former unresolved/out-of-range receiver behavior at this
        //lookup boundary; its consuming native-field reader owns range admission.
        if((uint)identity>=501)return new(physical);
        var references=_armyTableReferences??=new ArmyUnitReference?[501];
        return references[identity]??=new(identity);
    }
    public void SetArmyUnitCommanderId(int physicalSlot, ushort? value)
    { Check(physicalSlot, 501, "army unit physical slot"); _armyCommanderIds[physicalSlot] = value; }
    public void SetArmyUnitStatusFlag10(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "army unit physical slot"); _armyStatusFlags[physicalSlot] = value; }
    public ushort? ReadArmyUnitCommanderId(ArmyUnitReference reference) =>
        _armyCommanderIds.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 501, "army unit"));
    public bool? ReadArmyUnitNativeValidity(ArmyUnitReference reference)
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

    public void SetCurrentPersonLinkedEntities(IReadOnlyList<PersonLinkedEntityReference?>? values) =>
        _personLinkedEntities = values?.ToArray();
    public IReadOnlyList<PersonLinkedEntityReference?>? ReadCurrentPersonLinkedEntities() =>
        _currentAssociatedContainer is not null ? new AssociatedChainSequence(this, _currentAssociatedContainer) :
        _personLinkedEntities is null ? null : Array.AsReadOnly(_personLinkedEntities.ToArray());
    public void SetLinkedEntityRawState58(int identity, int? value)
    {
        if (identity < 0) throw new InvalidDataException("Negative opaque linked-entity identity.");
        _linkedEntityStates[identity] = value;
    }
    public bool? ReadLinkedEntityNativeValidity(PersonLinkedEntityReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.Identity.HasValue) return false;
        int identity = LinkedIdentity(reference);
        // An explicit current entry, including an explicit unknown, wins.
        int? state = _linkedEntityStates.TryGetValue(identity, out int? value) ? value :
            ReadOwnedPersonCommandManager() is null
                ? PersonReturnCommands.ReadSavedLinkedEntityState58(_readOnlyPersonLinkedEntityInputs, identity) : null;
        return state.HasValue ? state.Value != -1 : null;
    }
    public void SetLinkedEntityMemberIds(int identity, IReadOnlyList<uint?>? values)
    {
        if (identity < 0) throw new InvalidDataException("Negative opaque linked-entity identity.");
        _linkedEntityMembers[identity] = values?.ToArray();
    }
    public IReadOnlyList<uint?>? ReadLinkedEntityMemberIds(PersonLinkedEntityReference reference)
    {
        int identity = LinkedIdentity(reference);
        IReadOnlyList<uint?>? values = _linkedEntityMembers.TryGetValue(identity, out var value) ? value :
            ReadOwnedPersonCommandManager() is null
                ? PersonReturnCommands.ReadSavedLinkedEntityMembers(_readOnlyPersonLinkedEntityInputs, identity, this) : null;
        return values is null ? null : Array.AsReadOnly(values.ToArray());
    }
    public PersonLinkedEntityReference? ReadPersonLinkedEntity(int physicalSlot) =>
        ReadPersonLinkedEntityCommandInputs() is { Disappearances: null } ? null : PersonLinkedEntityLookup.Find(physicalSlot, this);
    private static int LinkedIdentity(PersonLinkedEntityReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        int identity = reference.Identity ?? throw new InvalidDataException("Null linked entity cannot provide native members.");
        if (identity < 0) throw new InvalidDataException("Negative opaque linked-entity identity.");
        return identity;
    }
}
