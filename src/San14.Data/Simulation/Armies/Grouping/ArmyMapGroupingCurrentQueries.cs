using System.Buffers.Binary;
using System.Collections;
using San14.Core;

namespace San14.Data;

/// <summary>Thin current queries over saved Army/base registry links and the
///same operation's Person/District/Force fields. No arena import or World copy.</summary>
public sealed class ArmyMapGroupingCurrentQueries : IArmyMapGroupingQueries
{
    private readonly GameState _world;
    private readonly SourceWorldContext _current;
    private readonly LoadedScenario _source;
    private readonly OwnedArmyGovernorComparatorInputs _posts;
    private readonly string _language, _ruleVersion;
    private readonly Dictionary<int, bool?> _exclusions = [];
    private readonly SavedBaseLookup _bases;
    private TacticRemakeCurrentAbilities? _abilities;

    public ArmyMapGroupingCurrentQueries(GameState world, SourceWorldContext current,
        LoadedScenario source, OwnedArmyGovernorComparatorInputs posts, string language, string ruleVersion)
    {
        _world = world; _current = current; _source = source; _posts = posts;
        _language = language; _ruleVersion = ruleVersion; _bases = new(world, current);
    }

    public IReadOnlyList<int>? ReadRegistry()
    {
        if (_world.ReadArmyLifecycle()?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Army) is not { } order) return null;
        var result = new List<int>(order.Count);
        foreach (var entity in order)
        {
            if (entity.Kind != WorldEntityKind.Army) return null;
            result.Add(WorldIndexContext.FindArmyPhysical(_world, entity.Id) ?? 0);
        }
        return result.AsReadOnly();
    }
    public ArmyMapGroupingReceiver ReadReceiver(int physical)
    {
        var slot = Slot(physical);
        if (slot is null) return new(null, null, null, null, null);
        var unit = Unit(slot);
        bool? valid = physical == 0 || slot.Commander12 == 0 ? false : slot.Commander12 is null
            ? null : slot.Status10 is byte status ? status != 0 : null;
        return new(slot.ActiveUnitId ?? unit?.Id, valid, slot.Status10,
            unit is null ? null : ArmyMarchSourceAdapter.ReadCurrentHex(_world, unit),
            unit is not null ? unit.NativeMapUiByte13C : slot.MapUiByte13C);
    }

    public ArmyMapGroupingHex ReadHex(int tableSlot)
    {
        var hex = _current.ReadHexTableReference(tableSlot);
        if (hex is null) return new(null, null, null);
        if (hex.PhysicalSlot is not int physical) return new(false, null, null);
        _current.BindArmyMapOccupancy(_world);
        ushort? occupant = _current.ReadHexRaw1C(hex);
        return new(true, physical, occupant); //CHex slot3 is true for every nonnull receiver, including0.
    }

    public bool? IsExcluded233560(int physical)
    {
        if (!_exclusions.TryGetValue(physical, out var value))
            _exclusions.Add(physical, value = OwnedArmyDestructionCurrent.ReadArmyExcluded233560(_world, _current, physical));
        return value;
    }

    public bool? IsAtHome2C96E0(int physical)
    {
        if (IsExcluded233560(physical) is not bool excluded) return null;
        if (excluded) return false;
        var force = ReadForce(physical);
        if (force is null) return null;
        if (force.PhysicalSlot is not int forcePhysical) return false;
        if (forcePhysical == 51) return false;
        if (forcePhysical is >= 46 and <= 50)
        {
            //232860 reads E2 even for an invalid commander; only its1..61
            //location range and actual Force physical46..50 are tested.
            var person = Commander(physical);
            if (person?.PhysicalSlot is not int personPhysical ||
                _current.ReadPersonFirstLocation(personPhysical) is not ushort specialLocation) return null;
            if (specialLocation is < 1 or > 61) return false;
        }
        if (ReadReceiver(physical).IsValid is not bool armyValid) return null;
        if (!armyValid) return false;
        if (ReadCommanderValidity(physical) is not bool personValid) return null;
        if (!personValid) return false;
        var selected = CurrentBase(physical);
        if (selected is null || _current.ReadBaseNativeValidity(selected) is not bool baseValid) return null;
        if (!baseValid) return false;
        try
        {
            ushort atHome = _current.ReadBaseCurrentHexIndex(selected);
            return ReadReceiver(physical).Hex2A is ushort hex ? atHome == hex : null;
        }
        catch (InvalidDataException) { return null; }
    }

    public bool? ReadCommanderValidity(int physical)
    {
        var person = Commander(physical);
        return person is null ? null : person.PhysicalSlot is int at ? _current.ReadPersonNativeValidity(at) : false;
    }
    public ushort? ReadPersonIdentity(int physical) => Commander(physical)?.PhysicalSlot is int at
        ? _current.ReadPersonIdentity(at) : null;
    public byte? ReadDistrict22BC70(int physical)
    {
        bool? valid = ReadCommanderValidity(physical);
        return valid is null ? null : !valid.Value ? (byte)0 : Commander(physical)?.PhysicalSlot is int at
            ? _current.ReadPersonDistrictSlot(at) : null;
    }

    public bool? ReadRuler(int physical)
    {
        if (Commander(physical)?.PhysicalSlot is not int at) return null;
        var force = _current.ReadPersonForce(at);
        if (force is null || _current.ReadForceNativeValidity(force) is not bool valid) return null;
        if (!valid) return false;
        ushort? ruler = force.PhysicalSlot is int f ? _current.ReadForceRecord(f)?.RawRuler10 : null;
        return ruler is ushort id && _current.ReadPersonIdentity(at) is ushort person ? id == person : null;
    }
    public bool? ReadDistrictLeader(int physical)
    {
        var district = ArmyDistrict(physical);
        if (district is null || _current.ReadDistrictNativeValidity(district) is not bool valid) return null;
        if (!valid) return false;
        ushort? leader = district.PhysicalSlot is int at ? _current.ReadDistrictLeader(at) : null;
        return leader is ushort id && ReadPersonIdentity(physical) is ushort person ? id == person : null;
    }
    public bool? ReadGovernor(int physical)
    {
        var selected = CurrentBase(physical);
        if (selected is null || _current.ReadBaseNativeValidity(selected) is not bool valid) return null;
        if (!valid) return false;
        return _current.ReadArmyReturnBaseGovernor2E(selected) is ushort governor && ReadPersonIdentity(physical) is ushort person
            ? governor == person : null;
    }
    public byte? ReadPostOrder37(int physical)
    {
        if (Commander(physical)?.PhysicalSlot is not int at || _current.ReadPersonIdentity(at) is not ushort person ||
            !_world.Officers.TryGetValue(person, out var officer) || _current.ResolveOfficerSource(person) != at)
            return null;
        int? office = OfficerAppointmentRules.ReadOfficeId(officer.Appointment);
        if (office is not >= 0 or > 255 || _source.Snapshot.RankParameters?.Offices is not { Count: 256 } rows) return null;
        if (office == 0) return 0;
        byte[] raw = Convert.FromHexString(rows[office.Value].RawBytesHex);
        if (BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(36, 2)) == 0) return 0;
        return _posts.ReadCurrentComparatorFields(_world, at)?.OfficeOrder37;
    }
    public int? ReadLeadership(int physical)
    {
        if (ReadPersonIdentity(physical) is not ushort id) return null;
        _abilities ??= new(_current, _source, _language, _ruleVersion);
        return _abilities.Resolve(_world, id)?.Leadership;
    }

    private ArmyLifecycleReceiverView? Slot(int physical) => _world.ReadArmyLifecycle()?.ReceiverSlots() is { } slots &&
        physical < slots.Length && slots[physical].PhysicalSlot == physical ? slots[physical] : null;
    private UnitState? Unit(ArmyLifecycleReceiverView slot) => slot.ActiveUnitId is int id
        ? _world.Units.GetValueOrDefault(id) : slot.PendingImage;
    private TransferPersonReference? Commander(int physical) => Slot(physical)?.Commander12 is ushort id
        ? _current.ReadPersonLookupReference(id <= 6000 ? id : 0) : null;
    private DistrictReference? ArmyDistrict(int physical)
    {
        byte? raw = ReadDistrict22BC70(physical);
        return raw is byte district ? _current.ReadDistrictTableReference(district <= 51 ? district : 0) : null;
    }
    internal ForceReference? ReadForce(int physical)
    {
        var district = ArmyDistrict(physical);
        if (district is null || _current.ReadDistrictNativeValidity(district) is not bool valid) return null;
        byte? raw = valid && district.PhysicalSlot is int at ? _current.ReadDistrictForceId(at) : (byte)0;
        return raw is byte force ? _current.ReadForceTableReference(force <= 51 ? force : 0) : null;
    }
    private BaseReference? CurrentBase(int physical)
    {
        if (Commander(physical)?.PhysicalSlot is not int at || _current.ReadPersonFirstLocation(at) is not ushort location) return null;
        try { return BaseLookup.Find(location, _bases); }
        catch (InvalidDataException) { return null; }
    }

    private sealed class SavedBaseLookup(GameState world, SourceWorldContext current) : IBaseLookupState
    {
        public BaseReference? ReadCityZeroReference() => current.ReadCityZeroReference();
        public ushort? ReadBaseObjectLink(BaseReference reference) => current.ReadBaseObjectLink(reference);
        public IReadOnlyList<BaseReference?>? ReadRegisteredBases() => world.ReadArmyLifecycle()?.Indexes.Bases?.Select(entity =>
            entity.Kind switch
            {
                WorldEntityKind.City => new BaseReference(BaseKind.City, entity.Id),
                WorldEntityKind.Gate => new BaseReference(BaseKind.Gate, entity.Id is int id ? id - 51 : null),
                WorldEntityKind.FallbackCity => new BaseReference(BaseKind.City, 0),
                WorldEntityKind.FallbackGate => new BaseReference(BaseKind.Gate, 0),
                _ => throw new InvalidDataException("The base index identifies another entity kind.")
            }).ToArray();
    }
}
