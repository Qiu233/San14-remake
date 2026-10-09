using San14.Data.Formats;

namespace San14.Data;

/// <summary>
/// Explicit mutable native inputs shared by eligibility, basic conditions and
/// object-link writes. This Data adapter is not a Core save format or proof that
/// the original first-player stage has completed. Unprovided input is unknown.
/// </summary>
public sealed partial class SourceWorldContext : IS14BasicEventConditionQueries, IS14EventEligibilityState,
    IDistrictPostLoadState, IPersonRoleLocationState, IPersonBaseSyncState,
    IPersonDistrictTransferState, IForcePersonAssociationState
{
    private sealed record PersonFields(ushort? Id, ushort? Flags, byte? Tail, byte? Status, ushort? Location);
    private readonly Dictionary<int, PersonFields> _persons = [];
    private readonly Dictionary<int, byte?> _personDistrictSlots = [];
    private readonly Dictionary<int, ushort?> _personFirstLocations = [];
    private readonly Dictionary<int, ushort?> _forceRulers = [];
    private readonly Dictionary<int, int?> _districtPointers = [], _forcePointers = [];
    // Data-only partial native district window. Unserialized bytes stay unknown.
    private readonly Dictionary<int, byte?[]> _districtFields = [];
    private readonly Dictionary<int, ushort?> _cityLinks = [];
    private readonly Dictionary<int, byte?> _cityFlags = [];
    private readonly Dictionary<int, S14ConditionNativeObject> _objects = [];
    private readonly Dictionary<int, ushort?> _gateLinks = [];
    private readonly Dictionary<int, byte?> _gateFlags = [], _cityDistricts = [];
    private readonly Dictionary<int, byte?> _personResetBytes = [];
    private readonly Dictionary<int, ushort?> _personResetWords = [];
    private readonly Dictionary<(int Force, int Index), ushort?> _associationPersons = [];
    private readonly Dictionary<(int Force, int Index), byte?> _associationTargets = [];
    private List<BaseReference?>? _registeredBases;
    private readonly Dictionary<int, int?> _personPointers = [], _cityPointers = [], _objectPointers = [], _gatePointers = [];
    private bool _personLookupCleared, _basePointerKnown;
    private int? _basePerson;
    public EventConditionState EventConditions { get; } = new();
    public EventEligibilitySession EventEligibility { get; } = new();
    public SourceProvenance? StaticSource { get; private set; }
    // Optional explicit query overrides. Defaults consume the current map state in this context.
    public Func<int, int?>? GateForceReader { get; set; }
    public Func<int, int?>? GateDistrictReader { get; set; }
    public Action<int, int>? DistrictTransferHandler { get; set; }
    public ForceReference? CurrentPlayerForce { get; set; }
    public Action<AssociationRemovalReport>? AssociationRemovalReporter { get; set; }
    public Action<int, int, int>? TransferServiceRefresher { get; set; }

    public short? ReadCurrentYear() => EventConditions.CurrentYear;
    public int? ReadScenarioMode() => EventConditions.ScenarioMode;
    public int? ReadPastGameTime() => EventConditions.PastGameTime;
    public ushort? ReadEmperorId() => EventConditions.EmperorPersonId;
    public bool? ReadEventManagerPresent() => EventEligibility.IsPresent;
    public bool? ReadWorldEventFlag(int index)
    { Check(index, S14EventSourceProfile.WorldFlagCount, "event bit"); return EventConditions.ReadEventFlag(index); }
    public bool? ReadWorldResultFlag(int index)
    { Check(index, S14EventSourceProfile.WorldFlagCount, "event bit"); return EventConditions.ReadResultFlag(index); }
    public bool? ReadManagerEventFlag(int index)
    { Check(index, S14EventSourceProfile.ManagerFlagCount, "event bit"); return EventEligibility.ReadBlockedFlag(index); }
    public void SetWorldEventFlag(int index, bool? value)
    { Check(index, S14EventSourceProfile.WorldFlagCount, "event bit"); EventConditions.SetEventFlag(index, value); }
    public void SetWorldResultFlag(int index, bool? value)
    { Check(index, S14EventSourceProfile.WorldFlagCount, "event bit"); EventConditions.SetResultFlag(index, value); }
    public void SetManagerEventFlag(int index, bool? value)
    { Check(index, S14EventSourceProfile.ManagerFlagCount, "event bit"); EventEligibility.SetBlockedFlag(index, value); }
    public void WriteManagerEventFlag(int index, bool value) => SetManagerEventFlag(index, value);

    public void ResetWorldEventFlags() => EventConditions.ResetFlags();
    public void ResetManagerEventFlags() => EventEligibility.ResetBlockedFlags();
    public void ResetResults() => EventEligibility.ResetResults();
    public void AppendResult(S14EventEligibilityResult result) => EventEligibility.AppendResult(result);
    public void WriteAvailableResult(bool value) => EventEligibility.SetAvailableResult(value);

    public void SetPersonRecord(S14ConditionNativePerson record)
    {
        if (record is null) throw new InvalidDataException("Current person record is unknown.");
        Check(record.PhysicalSlot, 1851, "person physical slot");
        _persons[record.PhysicalSlot] = new(record.CharacterId, record.RawFlags196,
            record.RawFlag1FD, record.RawStatus11E, record.RawLocation11C);
    }
    public void SetPersonIdentity(int physicalSlot, ushort? characterId)
    {
        Check(physicalSlot, 1851, "person physical slot");
        PersonFields old = _persons.GetValueOrDefault(physicalSlot) ?? new(null, null, null, null, null);
        _persons[physicalSlot] = old with { Id = characterId };
    }
    public ushort? ReadPersonIdentity(int physicalSlot)
    {
        Check(physicalSlot, 1851, "person physical slot");
        return _persons.GetValueOrDefault(physicalSlot)?.Id;
    }
    public byte? ReadPersonStatus(int physicalSlot)
    {
        Check(physicalSlot, 1851, "person physical slot");
        return _persons.GetValueOrDefault(physicalSlot)?.Status;
    }
    public ushort? ReadPersonFirstLocation(int physicalSlot)
    {
        Check(physicalSlot, 1851, "person physical slot");
        return _personFirstLocations.GetValueOrDefault(physicalSlot);
    }
    public void SetPersonFirstLocation(int physicalSlot, ushort? location)
    {
        Check(physicalSlot, 1851, "person physical slot");
        _personFirstLocations[physicalSlot] = location;
    }
    public ushort? ReadPersonSecondLocation(int physicalSlot)
    {
        Check(physicalSlot, 1851, "person physical slot");
        return _persons.GetValueOrDefault(physicalSlot)?.Location;
    }
    public void WritePersonSecondLocation(int physicalSlot, ushort location)
    {
        Check(physicalSlot, 1851, "person physical slot");
        PersonFields old = _persons.GetValueOrDefault(physicalSlot) ?? new(null, null, null, null, null);
        _persons[physicalSlot] = old with { Location = location };
    }
    public void WritePersonStatus(int physicalSlot, byte status)
    {
        Check(physicalSlot, 1851, "person physical slot");
        PersonFields old = _persons.GetValueOrDefault(physicalSlot) ?? new(null, null, null, null, null);
        _persons[physicalSlot] = old with { Status = status };
    }
    public void SetPersonDistrictSlot(int physicalSlot, byte? districtSlot)
    {
        Check(physicalSlot, 1851, "person physical slot");
        _personDistrictSlots[physicalSlot] = districtSlot;
    }
    public byte? ReadPersonDistrictSlot(int physicalSlot)
    {
        Check(physicalSlot, 1851, "person physical slot");
        return _personDistrictSlots.GetValueOrDefault(physicalSlot);
    }
    public bool? ReadPersonNativeValidity(int physicalSlot)
    {
        Check(physicalSlot, 1851, "person physical slot");
        PersonFields? value = _persons.GetValueOrDefault(physicalSlot);
        return NativeValidity.Person(physicalSlot, value?.Id, value?.Flags, value?.Tail, value?.Status);
    }
    public S14ConditionNativePerson? ReadPhysicalPerson(int physicalSlot)
    {
        Check(physicalSlot, 1851, "person physical slot");
        if (!_persons.TryGetValue(physicalSlot, out PersonFields? value) || value.Id is not ushort id ||
            value.Flags is not ushort flags || value.Tail is not byte tail || value.Status is not byte status ||
            value.Location is not ushort location) return null;
        return new(physicalSlot, id, flags, tail, status, location);
    }
    public void SetDistrictRecord(NativeDistrict record)
    {
        ArgumentNullException.ThrowIfNull(record);
        SetDistrictRawByte(record.PhysicalSlot, 0x10, record.RawForceSlot10);
        SetDistrictRawByte(record.PhysicalSlot, 0x11, record.RawNumber11);
        WriteDistrictLeader(record.PhysicalSlot, record.RawLeader12);
    }
    public NativeDistrict? ReadDistrictRecord(int physicalSlot)
    {
        byte? force = ReadDistrictRawByte(physicalSlot, 0x10), number = ReadDistrictRawByte(physicalSlot, 0x11);
        ushort? leader = ReadDistrictLeader(physicalSlot);
        return force.HasValue && number.HasValue && leader.HasValue
            ? new(physicalSlot, force.Value, number.Value, leader.Value) : null;
    }
    public bool? ReadDistrictNativeValidity(int physicalSlot) => NativeValidity.District(physicalSlot,
        ReadDistrictRawByte(physicalSlot, 0x10), ReadDistrictRawByte(physicalSlot, 0x11), ReadDistrictLeader(physicalSlot));
    public void SetDistrictTablePointer(int slot, int? physicalSlot) => SetPointer(_districtPointers, slot, 52, physicalSlot, 52);
    public void SetForceTablePointer(int slot, int? physicalSlot) => SetPointer(_forcePointers, slot, 52, physicalSlot, 52);
    public void SetForceRecord(NativeForce record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Check(record.PhysicalSlot, 52, "force physical slot");
        _forceRulers[record.PhysicalSlot] = record.RawRuler10;
    }
    public NativeForce? ReadForceRecord(int physicalSlot)
    {
        Check(physicalSlot, 52, "force physical slot");
        return _forceRulers.GetValueOrDefault(physicalSlot) is ushort ruler ? new(physicalSlot, ruler) : null;
    }
    public ForceReference? ReadPersonForce(int physicalSlot)
    {
        byte? rawDistrict = ReadPersonDistrictSlot(physicalSlot);
        if (!rawDistrict.HasValue) return null;
        int slot = rawDistrict.Value <= 51 ? rawDistrict.Value : 0;
        if (!_districtPointers.TryGetValue(slot, out int? district)) return null;
        int forceSlot = 0;
        if (district.HasValue)
        {
            bool? valid = ReadDistrictNativeValidity(district.Value);
            if (!valid.HasValue) return null;
            if (valid.Value)
            {
                byte? currentForce = ReadDistrictRawByte(district.Value, 0x10);
                if (!currentForce.HasValue) return null;
                forceSlot = currentForce.Value <= 51 ? currentForce.Value : 0;
            }
        }
        return _forcePointers.TryGetValue(forceSlot, out int? force) ? new(force) : null;
    }
    public byte? ReadPersonOldForceSlot(int physicalSlot)
    {
        byte? raw = ReadPersonDistrictSlot(physicalSlot);
        if (!raw.HasValue) return null;
        if (!_districtPointers.TryGetValue(raw.Value <= 51 ? raw.Value : 0, out int? physical)) return null;
        if (!physical.HasValue) return 0;
        bool? valid = ReadDistrictNativeValidity(physical.Value);
        return !valid.HasValue ? null : valid.Value ? ReadDistrictRawByte(physical.Value, 0x10) : (byte)0;
    }
    public void TransferPersonDistrict(int physicalSlot, int requestedDistrict)
    {
        Check(physicalSlot, 1851, "person physical slot");
        if (DistrictTransferHandler is not null) DistrictTransferHandler(physicalSlot, requestedDistrict);
        else PersonDistrictTransfer.Apply(physicalSlot, requestedDistrict, this);
    }
    public ForceReference? ReadForceTableReference(int slot)
    {
        Check(slot, 52, "force lookup slot");
        return _forcePointers.TryGetValue(slot, out int? physical) ? new(physical) : null;
    }
    public byte? ReadDistrictForceWithoutValidity(int districtSlot)
    {
        Check(districtSlot, 52, "district lookup slot");
        if (!_districtPointers.TryGetValue(districtSlot, out int? physical)) return null;
        if (!physical.HasValue) throw new InvalidDataException("Target district pointer is explicitly null; native raw-force read would be unsafe.");
        return ReadDistrictRawByte(physical.Value, 0x10);
    }
    public ForceReference? ReadRequestedDistrictForce(int requestedDistrict)
    {
        if (requestedDistrict < 0) throw new InvalidDataException("Negative target district pointer-table access is unsupported.");
        byte? force = ReadDistrictForceWithoutValidity(requestedDistrict <= 51 ? requestedDistrict : 0);
        return force.HasValue ? ReadForceTableReference(force.Value <= 51 ? force.Value : 0) : null;
    }
    public void WritePersonResetWord1FF(int physicalSlot, ushort value)
    { Check(physicalSlot, 1851, "person physical slot"); _personResetWords[physicalSlot] = value; }
    public void WritePersonResetByte1FE(int physicalSlot, byte value)
    { Check(physicalSlot, 1851, "person physical slot"); _personResetBytes[physicalSlot] = value; }
    public ushort? ReadPersonResetWord1FF(int physicalSlot)
    { Check(physicalSlot, 1851, "person physical slot"); return _personResetWords.GetValueOrDefault(physicalSlot); }
    public byte? ReadPersonResetByte1FE(int physicalSlot)
    { Check(physicalSlot, 1851, "person physical slot"); return _personResetBytes.GetValueOrDefault(physicalSlot); }
    public void WritePersonDistrictSlot(int physicalSlot, byte value) => SetPersonDistrictSlot(physicalSlot, value);
    public ForceReference? ReadCurrentPlayerForce() => CurrentPlayerForce;
    public void RefreshTransferService(int first, int second, int third)
    {
        if (TransferServiceRefresher is not null) { TransferServiceRefresher(first, second, third); return; }
        if (first != 1 || second != 1 || third != 0)
            throw new InvalidDataException("Unsupported current transfer service argument profile.");
        San14.Data.TransferService.Refresh(this, TransferService
            ?? throw new InvalidDataException("Current independent transfer service state is unknown."));
    }
    public void RemoveForcePersonAssociation(ForceReference oldForce, int personPhysicalSlot) =>
        ForcePersonAssociation.Clear(oldForce, personPhysicalSlot, this);
    public ushort? ReadForceAssociationPersonId(ForceReference force, int index) =>
        _associationPersons.GetValueOrDefault(AssociationKey(force, index));
    public byte? ReadForceAssociationTargetSlot(ForceReference force, int index) =>
        _associationTargets.GetValueOrDefault(AssociationKey(force, index));
    public void WriteForceAssociationPersonId(ForceReference force, int index, ushort value) =>
        _associationPersons[AssociationKey(force, index)] = value;
    public void WriteForceAssociationTargetSlot(ForceReference force, int index, byte value) =>
        _associationTargets[AssociationKey(force, index)] = value;
    public void ResetForceAssociations(int physicalSlot)
    {
        Check(physicalSlot, 52, "force physical slot");
        for (int index = 0; index < 3; index++)
        {
            _associationPersons[(physicalSlot, index)] = 0;
            _associationTargets[(physicalSlot, index)] = 0;
        }
    }
    public void EmitAssociationRemovalReport(AssociationRemovalReport report)
    {
        if (AssociationRemovalReporter is not null) AssociationRemovalReporter(report);
        else AssociationReportWriter.Write(report, this);
    }
    private static (int, int) AssociationKey(ForceReference force, int index)
    {
        ArgumentNullException.ThrowIfNull(force);
        int physical = force.PhysicalSlot ?? throw new InvalidDataException("Null force cannot supply native association fields.");
        Check(physical, 52, "force physical slot"); Check(index, 3, "force association slot");
        return (physical, index);
    }
    public bool? ReadForceNativeValidity(ForceReference force)
    {
        ArgumentNullException.ThrowIfNull(force);
        if (!force.PhysicalSlot.HasValue) return false;
        Check(force.PhysicalSlot.Value, 52, "force physical slot");
        return NativeValidity.Force(force.PhysicalSlot.Value, _forceRulers.GetValueOrDefault(force.PhysicalSlot.Value));
    }
    public int? ReadForcePhysicalSlot(ForceReference force)
    {
        ArgumentNullException.ThrowIfNull(force);
        int physical = force.PhysicalSlot ?? throw new InvalidDataException("Force pointer is explicitly null; native physical getter would be unsafe.");
        Check(physical, 52, "force physical slot");
        return physical;
    }
    public byte? ReadDistrictRawByte(int physicalSlot, int nativeOffset)
    {
        Check(physicalSlot, 52, "district physical slot");
        Check(nativeOffset - 0x10, 28, "district native window offset");
        return _districtFields.TryGetValue(physicalSlot, out byte?[]? fields) ? fields[nativeOffset - 0x10] : null;
    }
    public void SetDistrictRawByte(int physicalSlot, int nativeOffset, byte? value)
    {
        Check(physicalSlot, 52, "district physical slot");
        Check(nativeOffset - 0x10, 28, "district native window offset");
        if (!_districtFields.TryGetValue(physicalSlot, out byte?[]? fields))
            _districtFields[physicalSlot] = fields = new byte?[28];
        fields[nativeOffset - 0x10] = value;
    }
    public ushort? ReadDistrictLeader(int physicalSlot)
    {
        byte? low = ReadDistrictRawByte(physicalSlot, 0x12), high = ReadDistrictRawByte(physicalSlot, 0x13);
        return low.HasValue && high.HasValue ? (ushort)(low.Value | high.Value << 8) : null;
    }
    public void WriteDistrictLeader(int physicalSlot, ushort leaderId)
    {
        SetDistrictRawByte(physicalSlot, 0x12, (byte)leaderId);
        SetDistrictRawByte(physicalSlot, 0x13, (byte)(leaderId >> 8));
    }
    /// <summary>2260B0 resets only the proved members; byte23 and byte2B are untouched.</summary>
    public void ResetDistrict(int physicalSlot)
    {
        Check(physicalSlot, 52, "district physical slot");
        Put(0x10, 0, 4); Put(0x14, 0x01000000, 4); Put(0x18, 0xFF000000, 4);
        Put(0x1C, 0x7FFFFFFF, 4); Put(0x20, 0, 2); Put(0x22, 0, 1);
        Put(0x24, 0, 4); Put(0x28, 0xFFFF, 2); Put(0x2A, 0, 1);
        void Put(int offset, uint value, int width)
        {
            for (int index = 0; index < width; index++)
                SetDistrictRawByte(physicalSlot, offset + index, (byte)(value >> (index * 8)));
        }
    }
    public void SetCityRecord(S14ConditionNativeCity record)
    {
        if (record is null) throw new InvalidDataException("Current city record is unknown.");
        Check(record.PhysicalSlot, 52, "city physical slot");
        _cityLinks[record.PhysicalSlot] = record.RawObjectId4E;
        _cityFlags[record.PhysicalSlot] = record.RawFlag69;
    }
    public S14ConditionNativeCity? ReadPhysicalCity(int physicalSlot)
    {
        Check(physicalSlot, 52, "city physical slot");
        return _cityLinks.GetValueOrDefault(physicalSlot) is ushort link &&
            _cityFlags.GetValueOrDefault(physicalSlot) is byte flag ? new(physicalSlot, link, flag) : null;
    }
    /// <summary>Only the proved city+4E reset; other fields and pointer identities are untouched.</summary>
    public void ResetCityObjectLinks() { for (int slot = 0; slot < 52; slot++) _cityLinks[slot] = 0; }
    public void SetObjectRecord(S14ConditionNativeObject record)
    {
        if (record is null) throw new InvalidDataException("Current object record is unknown.");
        Check(record.PhysicalSlot, 3001, "object physical slot");
        _objects[record.PhysicalSlot] = record;
    }
    public S14ConditionNativeObject? ReadPhysicalObject(int physicalSlot)
    {
        Check(physicalSlot, 3001, "object physical slot");
        return _objects.GetValueOrDefault(physicalSlot);
    }

    public void SetPersonTablePointer(int slot, int? physicalSlot) => SetPointer(_personPointers, slot, 6001, physicalSlot, 1851);
    public void SetCityTablePointer(int slot, int? physicalSlot) => SetPointer(_cityPointers, slot, 52, physicalSlot, 52);
    public void SetObjectTablePointer(int slot, int? physicalSlot) => SetPointer(_objectPointers, slot, 3001, physicalSlot, 3001);
    public void SetGateTablePointer(int slot, int? physicalSlot) => SetPointer(_gatePointers, slot, 11, physicalSlot, 11);
    public void SetBasePersonPointer(int? physicalSlot)
    {
        if (physicalSlot.HasValue) Check(physicalSlot.Value, 1851, "base person physical slot");
        _basePointerKnown = true; _basePerson = physicalSlot;
    }
    public S14ConditionPersonLookup? ReadPersonTableSlot(int slot)
    {
        Check(slot, 6001, "person lookup slot");
        if (!_personPointers.TryGetValue(slot, out int? physical)) return _personLookupCleared ? new(null) : null;
        return physical.HasValue ? PersonAt(physical.Value) : new(null);
    }
    public S14ConditionPersonLookup? ReadBasePerson() => !_basePointerKnown ? null
        : _basePerson.HasValue ? PersonAt(_basePerson.Value) : new(null);
    public S14ConditionCityLookup? ReadCityTableSlot(int slot)
    {
        Check(slot, 52, "city lookup slot");
        if (!_cityPointers.TryGetValue(slot, out int? physical)) return null;
        if (!physical.HasValue) return new(null);
        S14ConditionNativeCity? value = ReadPhysicalCity(physical.Value);
        return value is null ? null : new(value);
    }
    public S14ConditionObjectLookup? ReadObjectTableSlot(int slot)
    {
        Check(slot, 3001, "object lookup slot");
        if (!_objectPointers.TryGetValue(slot, out int? physical)) return null;
        if (!physical.HasValue) return new(null);
        S14ConditionNativeObject? value = ReadPhysicalObject(physical.Value);
        return value is null ? null : new(value);
    }
    private S14ConditionPersonLookup? PersonAt(int physical)
    {
        S14ConditionNativePerson? value = ReadPhysicalPerson(physical);
        return value is null ? null : new(value);
    }

    /// <summary>Explicit native lookup reset; does not erase current physical person fields.</summary>
    public void ResetPersonIdLookup() { _personPointers.Clear(); _personLookupCleared = true; }
    /// <summary>Ordered native rebuild; no implicit clear and no rollback on unknown/unsafe identity.</summary>
    public void RebuildPersonIdLookup()
    {
        PostRegistrationLookups.RebuildPersonIds(this);
    }

    /// <summary>Callback for the proved link-write operator; native target validity is a later query.</summary>
    public void WriteObjectLink(ObjectLinkWrite write)
    {
        if (write is null) throw new InvalidDataException("Current object link write is unknown.");
        switch (write.Kind)
        {
            case ObjectLinkTargetKind.City:
                _cityLinks[KnownTarget(_cityPointers, write.TargetPhysicalSlot, 52, "city")] = write.ObjectPhysicalSlot;
                break;
            case ObjectLinkTargetKind.Gate:
                _gateLinks[KnownTarget(_gatePointers, write.TargetPhysicalSlot, 11, "gate")] = write.ObjectPhysicalSlot;
                break;
            default: throw new InvalidDataException("Unknown object-link target kind.");
        }
    }
    public ushort? ReadGateObjectLink(int tableSlot)
    {
        Check(tableSlot, 11, "gate lookup slot");
        return _gatePointers.TryGetValue(tableSlot, out int? physical) && physical.HasValue
            ? _gateLinks.GetValueOrDefault(physical.Value) : null;
    }
    public void SetGateObjectLink(int physicalSlot, ushort? value)
    {
        Check(physicalSlot, 11, "gate physical slot");
        _gateLinks[physicalSlot] = value;
    }
    public void SetGateValidityFlag(int physicalSlot, byte? value)
    {
        Check(physicalSlot, 11, "gate physical slot");
        _gateFlags[physicalSlot] = value;
    }
    public void SetCityDistrictSlot(int physicalSlot, byte? value)
    {
        Check(physicalSlot, 52, "city physical slot");
        _cityDistricts[physicalSlot] = value;
    }
    public BaseReference? ReadCityZeroReference() => _cityPointers.TryGetValue(0, out int? physical)
        ? new(BaseKind.City, physical) : null;
    public IReadOnlyList<BaseReference?>? ReadRegisteredBases() => _baseRegistryConfigured
        ? _baseRegistryHandle is null ? null : new NativeBaseSequence(this)
        : _registeredBases?.AsReadOnly();
    // Explicit resolved external inputs remain supported independently of the native pool.
    public void SetRegisteredBases(IReadOnlyList<BaseReference?>? references)
    { _baseRegistryConfigured = false; _registeredBases = references?.ToList(); }
    public void ResetRegisteredBases() { _baseRegistryConfigured = false; _registeredBases = []; }
    public void RebuildRegisteredBases()
    {
        if (_baseRegistryConfigured) { BaseRegistration.Apply(this); return; }
        // An explicit external resolved-list profile, not the018C90 allocation path.
        List<BaseReference?> current = _registeredBases
            ?? throw new InvalidDataException("Current registered base list is unknown; explicit reset is required.");
        Append(_cityPointers, BaseKind.City, 0, 52);
        Append(_gatePointers, BaseKind.Gate, 1, 11);
        void Append(Dictionary<int, int?> table, BaseKind kind, int first, int end)
        {
            for (int slot = first; slot < end; slot++)
            {
                if (!table.TryGetValue(slot, out int? physical)) throw new InvalidDataException($"Current {kind} pointer {slot} is unknown.");
                if (physical.HasValue) current.Add(new(kind, physical.Value));
            }
        }
    }
    public ushort? ReadBaseObjectLink(BaseReference reference)
    {
        int physical = BasePhysical(reference);
        return reference.Kind == BaseKind.City ? _cityLinks.GetValueOrDefault(physical) : _gateLinks.GetValueOrDefault(physical);
    }
    public bool? ReadBaseNativeValidity(BaseReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = BasePhysical(reference);
        if (physical == 0) return false;
        ushort? link = ReadBaseObjectLink(reference);
        if (!link.HasValue) return null;
        int objectSlot = link.Value <= 3000 ? link.Value : 0;
        if (!_objectPointers.TryGetValue(objectSlot, out int? objectPhysical)) return null;
        if (!objectPhysical.HasValue) return false;
        if (objectPhysical.Value == 0) return false;
        S14ConditionNativeObject? record = ReadPhysicalObject(objectPhysical.Value);
        if (record is null) return null;
        if (record.PhysicalSlot == 0 || record.RawField10 >= 48400 || record.RawField12 == 0) return false;
        byte? flag = reference.Kind == BaseKind.City ? _cityFlags.GetValueOrDefault(physical) : _gateFlags.GetValueOrDefault(physical);
        return flag.HasValue ? flag.Value == 0 : null;
    }
    public int? ReadBaseDistrictSlot(BaseReference reference)
    {
        int physical = BasePhysical(reference);
        return reference.Kind == BaseKind.City ? _cityDistricts.GetValueOrDefault(physical)
            : GateDistrictReader is not null ? GateDistrictReader(physical) : CurrentGateMapQueries.ReadDistrictSlot(reference, this);
    }
    public int? ReadBaseForceSlot(BaseReference reference)
    {
        int physical = BasePhysical(reference);
        if (reference.Kind == BaseKind.Gate) return GateForceReader is not null
            ? GateForceReader(physical) : CurrentGateMapQueries.ReadForceSlot(reference, this);
        byte? raw = _cityDistricts.GetValueOrDefault(physical);
        if (!raw.HasValue) return null;
        if (!_districtPointers.TryGetValue(raw.Value <= 51 ? raw.Value : 0, out int? district)) return null;
        if (!district.HasValue || district.Value == 0) return 0;
        byte? force = ReadDistrictRawByte(district.Value, 0x10);
        if (!force.HasValue) return null;
        if (force.Value == 0) return 0;
        byte? number = ReadDistrictRawByte(district.Value, 0x11);
        if (!number.HasValue) return null;
        return number.Value == 0 ? 0 : force.Value;
    }
    private static int BasePhysical(BaseReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        int physical = reference.PhysicalSlot ?? throw new InvalidDataException("Current base pointer is explicitly null; native field getter would be unsafe.");
        switch (reference.Kind)
        {
            case BaseKind.City: Check(physical, 52, "city physical slot"); break;
            case BaseKind.Gate: Check(physical, 11, "gate physical slot"); break;
            default: throw new InvalidDataException("Current base type is unsupported.");
        }
        return physical;
    }

    /// <summary>
    /// Apply only verified CE source fields to this explicit context. Pointer
    /// arrays use the proved successful native registry-allocation identity.
    /// ID lookup, links, bitmaps, manager presence/results and availability are
    /// not inferred from the source file or automatically reset here.
    /// </summary>
    public void MaterializeStaticSource(LoadedScenario loaded)
    {
        if (loaded is null || loaded.Metadata.FormatMarker != 0x66 || loaded.Metadata.LeadingRecordCount != 1852)
            throw new InvalidDataException("Event source materialization requires the inspected CE scenario profile.");
        ScenarioCityPreview source = loaded.Snapshot;
        if (source.WorldParameters is null || source.ObjectSourceRecords is null || source.Characters.Count != 1851 ||
            source.Cities.Count != 52 || source.Gates.Count != 11 || source.Districts.Count != 52 || source.Forces.Count != 52 || source.Areas.Count != 501 || source.ObjectSourceRecords.Objects.Count != 3001 ||
            source.ObjectSourceRecords.SourceSha256 != loaded.Metadata.DecodedSha256 ||
            source.WorldParameters.DecodedSha256 != loaded.Metadata.DecodedSha256)
            throw new InvalidDataException("Event source materialization has missing or contradictory source tables/hashes.");
        int reportTime = ReadStaticReportWorldTime(source.WorldParameters);
        for (int slot = 0; slot < source.Characters.Count; slot++)
        {
            CharacterPreviewRecord person = source.Characters[slot];
            if (person.Slot != slot || person.CharacterId is < 0 or > ushort.MaxValue)
                throw new InvalidDataException($"Event source person physical slot {slot} is invalid.");
            _persons[slot] = new((ushort)person.CharacterId, person.CreditReference?.RawFlagsWord,
                person.CreditReference?.RawTailCode, person.StatusCode, person.SecondaryCitySlot);
            SetPersonDistrictSlot(slot, person.DistrictSlot);
            SetPersonFirstLocation(slot, person.CitySlot);
            SetTransferPersonTablePointer(slot, slot);
            SetPersonPostLoadByte(slot, 0x124, person.Leadership);
            SetPersonPostLoadByte(slot, 0x125, person.Strength);
            SetPersonPostLoadByte(slot, 0x126, person.Intelligence);
            SetPersonPostLoadByte(slot, 0x127, person.Politics);
            SetPersonPostLoadByte(slot, 0x128, person.Charm);
            SetPersonPostLoadByte(slot, 0x1BA, person.RawPolicyBaseCost);
            MaterializePersonSkillSource(person);
        }
        SetBasePersonPointer(0);
        for (int slot = 0; slot < source.Areas.Count; slot++)
        {
            AreaPreviewRecord area = source.Areas[slot];
            if (area.Slot != slot) throw new InvalidDataException($"Event source area physical slot {slot} is invalid.");
            SetAreaScenarioForceSlot(slot, area.ForceSlot);
            SetAreaFlags38(slot, area.RawFlags38);
            SetAreaFlag74(slot, area.RawFlag74);
            if (area.RawNeighborSlots is { Count: not 8 })
                throw new InvalidDataException($"Event source area {slot} does not contain eight raw neighbor words.");
            for (int index = 0; index < 8; index++)
                SetAreaNeighborSlot(slot,index,area.RawNeighborSlots?[index]);
        }
        for (int slot = 0; slot < source.Districts.Count; slot++)
        {
            DistrictPreviewRecord district = source.Districts[slot];
            if (district.Slot != slot) throw new InvalidDataException($"Event source district physical slot {slot} is invalid.");
            SetDistrictRecord(new(slot, district.ForceSlot, district.Number, district.LeaderSlot));
            SetDistrictTablePointer(slot, slot);
        }
        for (int slot = 0; slot < source.Forces.Count; slot++)
        {
            ForcePreviewRecord force = source.Forces[slot];
            if (force.Slot != slot) throw new InvalidDataException($"Event source force physical slot {slot} is invalid.");
            SetForceRecord(new(slot, force.RulerSlot));
            SetForceReportColor(slot, force.RawReportColor49);
            SetForceRankSlot47(slot,force.RankId is >=0 and <=255 ? (byte)force.RankId.Value : null);
            SetForceTablePointer(slot, slot);
        }
        for (int slot = 0; slot < source.Cities.Count; slot++)
        {
            CityPreviewRecord city = source.Cities[slot];
            if (city.Slot != slot) throw new InvalidDataException($"Event source city physical slot {slot} is invalid.");
            _cityFlags[slot] = city.RawValidityFlag69;
            var reference = new BaseReference(BaseKind.City, slot);
            SetBaseResource(reference, BaseResourceKind.Gold, unchecked((int)city.Gold));
            SetBaseResource(reference, BaseResourceKind.Supplies, unchecked((int)city.Supplies));
            SetBaseResource(reference, BaseResourceKind.Troops, unchecked((int)city.Troops));
            SetBaseMorale44(reference, city.Morale);
            SetCitySizeB8(slot, city.SizeCode);
            SetBaseField46(reference, city.Durability);
            SetCityDistrictSlot(slot, city.DistrictSlot);
            SetCityTablePointer(slot, slot);
        }
        for (int slot = 0; slot < source.Gates.Count; slot++)
        {
            if (source.Gates[slot].Slot != slot) throw new InvalidDataException($"Event source gate physical slot {slot} is invalid.");
            SetGateTablePointer(slot, slot);
            SetGateValidityFlag(slot, source.Gates[slot].RawValidityFlag69);
            var reference = new BaseReference(BaseKind.Gate, slot);
            SetBaseResource(reference, BaseResourceKind.Gold, unchecked((int)source.Gates[slot].Amount0));
            SetBaseResource(reference, BaseResourceKind.Supplies, unchecked((int)source.Gates[slot].Amount1));
            SetBaseResource(reference, BaseResourceKind.Troops, unchecked((int)source.Gates[slot].Troops));
            SetBaseMorale44(reference, source.Gates[slot].RawMorale44);
            SetBaseField46(reference, source.Gates[slot].RawField46);
        }
        for (int slot = 0; slot < source.ObjectSourceRecords.Objects.Count; slot++)
        {
            S14ScenarioObjectRecord value = source.ObjectSourceRecords.Objects[slot];
            if (value.PhysicalSlot != slot) throw new InvalidDataException($"Event source object physical slot {slot} is invalid.");
            SetObjectRecord(new(slot, value.RawField10, value.RawField12));
            SetObjectRawField14(slot, value.RawField14);
            SetObjectForceSlot13(slot, value.RawField13);
            SetObjectTablePointer(slot, slot);
        }
        EventConditions.CurrentYear = source.WorldParameters.RawCurrentYear;
        EventConditions.ScenarioMode = source.WorldParameters.RawModeValue;
        RawWorldField48 = source.WorldParameters.RawField48;
        EventConditions.PastGameTime = source.WorldParameters.RawPastGameTime;
        EventConditions.EmperorPersonId = source.WorldParameters.RawEmperorPersonId;
        RawWorldField450 = reportTime;
        MaterializeStaticSceneryState(loaded);
        MaterializeStaticPersonalityState(loaded);
        StaticSource = loaded.ScenarioSource;
    }

    private static int KnownTarget(Dictionary<int, int?> pointers, int slot, int count, string name)
    {
        Check(slot, count, $"{name} lookup slot");
        if (!pointers.TryGetValue(slot, out int? physical)) throw new InvalidDataException($"Current {name} target pointer {slot} is unknown.");
        return physical ?? throw new InvalidDataException($"Current {name} target pointer {slot} is explicitly null; native write would be unsafe.");
    }
    private static void SetPointer(Dictionary<int, int?> pointers, int slot, int count, int? physical, int physicalCount)
    {
        Check(slot, count, "lookup slot");
        if (physical.HasValue) Check(physical.Value, physicalCount, "physical pointer slot");
        pointers[slot] = physical;
    }
    private static void Check(int value, int count, string field)
    {
        if (value < 0 || value >= count) throw new InvalidDataException($"{field} {value} is outside the supported native profile 0..{count - 1}.");
    }
}
