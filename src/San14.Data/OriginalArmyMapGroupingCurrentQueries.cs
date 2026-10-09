using System.Buffers.Binary;
using System.Collections;
using San14.Core;

namespace San14.Data;

/// <summary>Thin current queries over saved Army/base registry links and the
///same operation's Person/District/Force fields. No arena import or World copy.</summary>
public sealed class OriginalArmyMapGroupingCurrentQueries : IOriginalArmyMapGroupingQueries
{
    private readonly GameState _world;
    private readonly OriginalEventInitializationContext _current;
    private readonly LoadedScenario _source;
    private readonly OriginalOwnedArmyGovernorComparatorInputs _posts;
    private readonly string _language, _ruleVersion;
    private readonly Dictionary<int, bool?> _exclusions = [];
    private readonly SavedBaseLookup _bases;
    private OriginalTacticRemakeCurrentAbilities? _abilities;

    public OriginalArmyMapGroupingCurrentQueries(GameState world, OriginalEventInitializationContext current,
        LoadedScenario source, OriginalOwnedArmyGovernorComparatorInputs posts, string language, string ruleVersion)
    {
        _world = world; _current = current; _source = source; _posts = posts;
        _language = language; _ruleVersion = ruleVersion; _bases = new(world, current);
    }

    public IReadOnlyList<int>? ReadRegistry()
    {
        if (_world.NativeArmyLifecycle?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Army) is not { } order) return null;
        var result = new List<int>(order.Count);
        foreach (var entity in order)
        {
            if (entity.Kind != WorldEntityKind.Army) return null;
            result.Add(WorldIndexContext.FindArmyPhysical(_world, entity.Id) ?? 0);
        }
        return result.AsReadOnly();
    }
    public OriginalArmyMapGroupingReceiver ReadReceiver(int physical)
    {
        var slot = Slot(physical);
        if (slot is null) return new(null, null, null, null, null);
        var unit = Unit(slot);
        bool? valid = physical == 0 || slot.Commander12 == 0 ? false : slot.Commander12 is null
            ? null : slot.Status10 is byte status ? status != 0 : null;
        return new(slot.ActiveUnitId ?? unit?.Id, valid, slot.Status10,
            unit?.NativeMarch?.CurrentHex2A ?? unit?.NativeCurrentHex2A,
            unit is not null ? unit.NativeMapUiByte13C : slot.MapUiByte13C);
    }

    public OriginalArmyMapGroupingHex ReadHex(int tableSlot)
    {
        var hex = _current.ReadHexTableReference(tableSlot);
        if (hex is null) return new(null, null, null);
        if (hex.PhysicalSlot is not int physical) return new(false, null, null);
        ushort? occupant = _world.NativeArmyLifecycle?.HexRaw1C.GetValueOrDefault(physical);
        return new(true, physical, occupant); //CHex slot3 is true for every nonnull receiver, including0.
    }

    public bool? IsExcluded233560(int physical)
    {
        if (!_exclusions.TryGetValue(physical, out var value))
            _exclusions.Add(physical, value = OriginalOwnedArmyDestructionCurrent.ReadArmyExcluded233560(_world, _current, physical));
        return value;
    }

    public bool? IsAtHome2C96E0(int physical)
    {
        if (IsExcluded233560(physical) is not bool excluded) return null;
        if (excluded) return false;
        var force = ArmyForce(physical);
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
            !_world.Officers.TryGetValue(person, out var officer) || officer.NativeAbilityRuntime is not { } runtime)
            return null;
        int? office = officer.OfficeId ?? runtime.Office11F;
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

    private NativeArmyLifecycleSlot? Slot(int physical) => _world.NativeArmyLifecycle?.Slots is { } slots &&
        physical < slots.Length && slots[physical].PhysicalSlot == physical ? slots[physical] : null;
    private UnitState? Unit(NativeArmyLifecycleSlot slot) => slot.ActiveUnitId is int id
        ? _world.Units.GetValueOrDefault(id) : slot.PendingImage;
    private OriginalTransferPersonReference? Commander(int physical) => Slot(physical)?.Commander12 is ushort id
        ? _current.ReadPersonLookupReference(id <= 6000 ? id : 0) : null;
    private OriginalDistrictReference? ArmyDistrict(int physical)
    {
        byte? raw = ReadDistrict22BC70(physical);
        return raw is byte district ? _current.ReadDistrictTableReference(district <= 51 ? district : 0) : null;
    }
    private OriginalForceReference? ArmyForce(int physical)
    {
        var district = ArmyDistrict(physical);
        if (district is null || _current.ReadDistrictNativeValidity(district) is not bool valid) return null;
        byte? raw = valid && district.PhysicalSlot is int at ? _current.ReadDistrictRawByte(at, 0x10) : (byte)0;
        return raw is byte force ? _current.ReadForceTableReference(force <= 51 ? force : 0) : null;
    }
    private OriginalBaseReference? CurrentBase(int physical)
    {
        if (Commander(physical)?.PhysicalSlot is not int at || _current.ReadPersonFirstLocation(at) is not ushort location) return null;
        try { return OriginalBaseLookup.Find(location, _bases); }
        catch (InvalidDataException) { return null; }
    }

    private sealed class SavedBaseLookup(GameState world, OriginalEventInitializationContext current) : IOriginalBaseLookupState
    {
        public OriginalBaseReference? ReadCityZeroReference() => current.ReadCityZeroReference();
        public ushort? ReadBaseObjectLink(OriginalBaseReference reference) => current.ReadBaseObjectLink(reference);
        public IReadOnlyList<OriginalBaseReference?>? ReadRegisteredBases() => world.NativeArmyLifecycle?.Indexes.Bases?.Select(entity =>
            entity.Kind switch
            {
                WorldEntityKind.City => new OriginalBaseReference(OriginalBaseKind.City, entity.Id),
                WorldEntityKind.Gate => new OriginalBaseReference(OriginalBaseKind.Gate, entity.Id is int id ? id - 51 : null),
                WorldEntityKind.FallbackCity => new OriginalBaseReference(OriginalBaseKind.City, 0),
                WorldEntityKind.FallbackGate => new OriginalBaseReference(OriginalBaseKind.Gate, 0),
                _ => throw new InvalidDataException("The base index identifies another entity kind.")
            }).ToArray();
    }
}