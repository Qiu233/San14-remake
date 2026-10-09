using San14.Core;

namespace San14.Data;

/// <summary>Captures the proven map/definition prefix and this game's current
/// Base/Person identities. This is not a replay of the complete first-player
/// stage. Each capture owns its native tables and linked-list storage.</summary>
public sealed partial class ArmyOperationContext
{
    private readonly LoadedScenario _source;
    private readonly LoadedMap _map;
    private readonly LoadedTerrainTypes _terrain;
    private readonly LoadedSkillDefinitions _skills;
    private readonly LoadedPersonalities _personalities;
    private readonly LoadedObjectDefinitions? _objectDefinitions;
    private readonly MapTopology _mapTopology;

    public ArmyOperationContext(LoadedScenario source, LoadedMap map,
        LoadedTerrainTypes terrain, LoadedSkillDefinitions skills, LoadedPersonalities personalities,
        LoadedObjectDefinitions? objectDefinitions = null)
    {
        _source = source; _map = map; _terrain = terrain; _skills = skills; _personalities = personalities;
        _objectDefinitions = objectDefinitions;
        if (source.Metadata.FormatMarker != 0x66 || map.Map is not { Width: 220, Height: 220, Cells.Length: 48400 } ||
            terrain.Source.Sha256 != source.FixedDataSource.Sha256 || skills.Source.Sha256 != source.FixedDataSource.Sha256 ||
            personalities.Source.Sha256 != source.FixedDataSource.Sha256 ||
            objectDefinitions is not null && objectDefinitions.Source.Sha256 != source.FixedDataSource.Sha256)
            throw new InvalidDataException("Owned Army definitions require coherent complete CE source tables.");
        _ = ScenarioTerritory.Build(map.Map, source.Snapshot);
        _mapTopology = new(map.Map.Width, map.Map.Height,
            map.Map.Cells.Select(cell => new MapTileDefinition(cell.Id, cell.AreaSlot, cell.SurfaceCode)));
    }

    public SourceWorldContext? Capture(GameState state)
    {
        if (state.MapWidth != 220 || state.MapHeight != 220 || state.MapSha256 != _map.Source.Sha256 ||
            !Matches(state, _source.ScenarioSource) || !Matches(state, _source.FixedDataSource)) return null;
        SourceWorldContext native = CaptureSourcePrefix();
        foreach (DistrictState district in state.Districts.Values)
        {
            if (district.Id is < 1 or > 51 || district.ForceId is < 1 or > 51 || district.Number is < 0 or > 255) return null;
            native.SetDistrictRawByte(district.Id, 0x10, (byte)district.ForceId);
            native.SetDistrictRawByte(district.Id, 0x11, (byte)district.Number);
            native.SetDistrictRawByte(district.Id, 0x12, district.LeaderId is int leader ? (byte)leader : null);
            native.SetDistrictRawByte(district.Id, 0x13, district.LeaderId is int high ? (byte)(high >> 8) : null);
        }
        foreach (ForceState force in state.Forces.Values)
        {
            if (force.Id is < 1 or > 51 || force.RulerId is < 0 or > 6000) return null;
            native.SetOwnedForceRulerSlot10(force.Id, force.RulerId is int ruler ? (ushort)ruler : null);
        }
        foreach (OfficerState officer in state.Officers.Values)
        {
            TransferPersonReference? reference = native.ReadPersonLookupReference(officer.Id);
            if (reference?.PhysicalSlot is not int physical || native.ReadPersonIdentity(physical) != officer.Id) return null;
            if (officer.LocationId is < 0 or > ushort.MaxValue || officer.DistrictId is < 0 or > 51) return null;
            native.SetPersonFirstLocation(physical, officer.LocationId is int first ? (ushort)first : null);
            native.SetPersonDistrictSlot(physical, officer.DistrictId is int district ? (byte)district : null);
            //Second location, status and native flags stay independent source
            //fields. A Core role/presence enum does not encode those bytes.
        }
        //Fixed Area34 is the validity/original-city byte. Current city-circle
        //assignment is the separate Area35 written by240D20; projecting Core
        //CityId must not replace34 or reset the fixed representative Hex3A.
        if (state.Areas.Count != _source.Snapshot.Areas.Count(row => row.Slot != 0 && row.CitySlot is >= 1 and <= 51))
            return null;
        foreach ((int id, AreaState area) in state.Areas)
        {
            if (id != area.Id || id is < 1 or > 500 || area.CityId is < 1 or > 51 ||
                !state.Cities.TryGetValue(area.CityId, out CityState? receivingCity) || receivingCity.Id != area.CityId ||
                native.ReadAreaTableReference(id)?.PhysicalSlot != id ||
                native.ReadAreaNativeValidity(new(id)) != true || native.ReadAreaHexIndex(new(id)) != area.HexId)
                return null;
            native.SetAreaCitySlot(id, (byte)area.CityId);
        }
        foreach ((int id, TerritoryState cell) in state.Territory)
        {
            if (id != cell.Id || cell.Id is < 0 or >= 48400 || cell.ForceId is < 0 or > 51 ||
                (cell.AreaId ?? 0) != _mapTopology.Tile(cell.Id).AreaId) return null;
            native.SetHexForceSlot(cell.Id, (byte)(cell.ForceId ?? 0));
        }
        if (state.Territory.Count != 48400) return null;
        foreach (CityState city in state.Cities.Values)
        {
            if (city.Id is < 1 or > 51 || city.ForceId is < 0 or > 51 || city.DistrictId is < 0 or > 51 ||
                native.ReadPhysicalObject(city.Id)?.RawField10 != city.HexId) return null;
            native.SetCityDistrictSlot(city.Id, city.DistrictId is int district ? (byte)district : null);
            native.SetObjectForceSlot13(city.Id, (byte)(city.ForceId ?? 0));
            native.SetObjectRawField14(city.Id, city.Durability is >= 0 and <= ushort.MaxValue
                ? (ushort)city.Durability.Value : null);
        }
        foreach (GateState gate in state.Gates.Values)
        {
            if (gate.Id is < 52 or > 61 || gate.ForceId is < 0 or > 51 ||
                native.ReadPhysicalObject(gate.Id)?.RawField10 != gate.HexId) return null;
            native.SetObjectForceSlot13(gate.Id, (byte)(gate.ForceId ?? 0));
            native.SetObjectRawField14(gate.Id, gate.Durability is >= 0 and <= ushort.MaxValue
                ? (ushort)gate.Durability.Value : null);
        }
        return native;
    }

    /// <summary>Immutable-source consumers used by the native tick adapter do
    /// not need a fresh capture of the 48400 ownership/overlay entries per tick.</summary>
    internal SourceWorldContext CaptureSourcePrefix()
    {
        var native = new SourceWorldContext();
        native.BindMapIdentityDefinitions(_mapTopology);
        native.InitializeStaticTablePointers();
        if (_objectDefinitions is not null) native.MaterializeFixedObjectDefinitions(_objectDefinitions);
        //Fresh CHex ctor221660/2213B0 qword+16 zeroes Hex1A. This
        //explicit allocation precedes source/map-overlay writes.
        native.BindMapDefinitions(_mapTopology);
        for (int physical = 0; physical < 1851; physical++) native.ResetPersonTraitFields(physical);
        native.InitializePersonalityStorage();
        native.MaterializeStaticSource(_source);
        //The scenario projection provides only Area scenario auxiliaries.
        //Fixed34/3A are the separately proved fixed-map fields, already bound
        //to this map by the constructor's ScenarioTerritory validation.
        for (int slot = 0; slot < _source.Snapshot.Areas.Count; slot++)
        {
            AreaPreviewRecord area = _source.Snapshot.Areas[slot];
            if (area.Slot != slot || area.CitySlot > 51 || area.HexId >= 48400)
                throw new InvalidDataException("Owned Army fixed area receivers contradict the loaded map definition.");
            native.SetAreaTablePointer(slot, slot);
            native.SetAreaValidityCode(slot, area.CitySlot);
            native.SetAreaHexIndex(slot, area.HexId);
        }
        //Shared fresh Base reset SC22726D/TC226FFD clears qword+4C,
        //including4E. The CE static serializer skips4E. Registration retains
        //City0; actual Object link writers below subsequently fill live Bases.
        for (int slot = 0; slot < 52; slot++) native.WriteBaseObjectLink(new(BaseKind.City, slot), 0);
        for (int slot = 0; slot < 11; slot++) native.WriteBaseObjectLink(new(BaseKind.Gate, slot), 0);
        native.MaterializeFixedTerrainTypes(_terrain);
        native.InitializeSkillDefinitionStorage(); native.MaterializeFixedSkillDefinitions(_skills);
        native.MaterializeFixedPersonalities(_personalities);
        native.TacticParameterDecoderTable = TacticCeParameterDecoderTable.Instance;
        native.ResetPersonIdLookup(); PostRegistrationLookups.RebuildPersonIds(native);

        var pool = new WorldIndexContext(native);
        native.SetRegistryDescriptor(RegistryKind.Object, new(pool.AllocateDescriptor().Identity));
        foreach (var obj in _source.Snapshot.ObjectSourceRecords!.Objects)
        {
            _ = ObjectRegistration.Register(new(RegistryKind.Object, obj.PhysicalSlot), native);
            if (obj.PhysicalSlot is < 1 or > 61 || native.ReadObjectNativeValidity(new(obj.PhysicalSlot)) != true) continue;
            BaseReference reference = obj.PhysicalSlot <= 51 ? new(BaseKind.City, obj.PhysicalSlot)
                : new(BaseKind.Gate, obj.PhysicalSlot - 51);
            //The consumed331AC0/3311C0 Base link writer, before its lists.
            native.WriteBaseObjectLink(reference, (ushort)obj.PhysicalSlot);
        }
        native.SetBaseRegistryHandle(new(pool.AllocateDescriptor().Identity));
        BaseRegistration.Apply(native);
        foreach (BaseReference? reference in native.ReadRegisteredBases()!)
            if (reference is not null) native.SetBaseHexListHandle(reference, pool.AllocateDescriptor());
        _ = BaseNeighborhoodInitialization.Apply(native);
        //Actual registration helper uses fresh linked-object validity for the
        //City descriptor at registry90. Growth2AAD00 traverses this separate
        //descriptor and later reads current District ownership from the capture.
        native.SetRegistryDescriptor(RegistryKind.City, new(pool.AllocateDescriptor().Identity));
        for (int city = 0; city < 52; city++)
            _ = ObjectRegistration.Register(native.ReadRegistryTableReference(RegistryKind.City, city), native);
        MapOverlayInitialization.ApplyObjects(1, native);
        foreach (var person in _source.Snapshot.Characters)
        {
            if (person.SkillSource is null) continue;
            HexListHandle temporary = PersonSkillPostLoad.Apply(person.Slot, native);
            native.ClearTemporaryHexList(temporary); native.ReleaseTemporaryHexList(temporary);
        }
        return native;
    }

    private static bool Matches(GameState state, SourceProvenance source) =>
        state.ContentHashes.GetValueOrDefault(source.ResourceUri)?.Equals(source.Sha256, StringComparison.OrdinalIgnoreCase) == true;
}
