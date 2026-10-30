using San14.Core;

namespace San14.Data;

/// <summary>Captures the proven map/definition prefix and this game's current
/// Base/Person identities. This is not a replay of the complete first-player
/// stage. Each capture owns its native tables and linked-list storage.</summary>
public sealed partial class ArmyOperationContext
{
    private readonly LoadedScenario _source;
    private readonly OfficerSourceDirectory _officerSources = new();
    private readonly LoadedMap _map;
    private readonly LoadedTerrainTypes _terrain;
    private readonly LoadedSkillDefinitions _skills;
    private readonly LoadedPersonalities _personalities;
    private readonly LoadedObjectDefinitions? _objectDefinitions;
    private readonly MapTopology _mapTopology;
    private InitialPersonSkillNormalization? _initialSkillNormalization;
    private readonly Lazy<InitialPersonalityImage> _initialPersonalities;
    private readonly InitialPersonAttributeValues _initialPersonAttributes;

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
        _officerSources.ClearLogicalReferences();
        foreach (var row in source.Snapshot.Characters)
        {
            _officerSources.SetRecordIdentity(row.Slot, row.CharacterId);
            if (row.Slot == 0 || row.CharacterId != 0)
                _officerSources.SetLogicalReference(row.CharacterId, row.Slot);
        }
        _ = ScenarioTerritory.Build(map.Map, source.Snapshot);
        _mapTopology = new(map.Map.Width, map.Map.Height,
            map.Map.Cells.Select(cell => new MapTileDefinition(cell.Id, cell.AreaSlot, cell.SurfaceCode)));
        _initialPersonalities = new(() => SourceWorldContext.PrepareInitialPersonalities(_source, _personalities),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _initialPersonAttributes = new(source);
    }

    //330155's reserved lookup survives even while the receiver's embedded ID
    //is0. This resolves storage, not native validity or a domain allocation.
    internal int? ResolveOfficerSource(int officerId) =>
        PostRegistrationLookups.HirePersonPhysicalSlot(officerId) ??
        PostRegistrationLookups.RegisteredPersonPhysicalSlot(officerId) ?? _officerSources.ResolveOfficer(officerId);

    /// <summary>The normal immutable Hex-table allocation, without capturing
    /// personnel, map ownership or the complete source prefix. Explicit table
    /// overrides on the receiving context retain priority over this owner.</summary>
    internal void BindMapIdentityCurrent(SourceWorldContext current) => current.BindMapIdentityDefinitions(_mapTopology);
    internal int? ReadOfficerSourceIdentity(int record)
    {
        int? id = _officerSources.IdentityAt(record);
        return id is > 0 ? id : PostRegistrationLookups.RegisteredPersonIdAtPhysical(record) ?? id;
    }

    //Only the fresh return-field installer consumes the original byte. A
    //current capture's nullable Domain DistrictId cannot recover known zero.
    internal byte ReadFreshPersonDistrictSlot(int physical)
    {
        if ((uint)physical >= (uint)_source.Snapshot.Characters.Count) throw new ArgumentOutOfRangeException(nameof(physical));
        return _source.Snapshot.Characters[physical].DistrictSlot;
    }

    public SourceWorldContext? Capture(GameState state) => CaptureWithCurrentFields(state, false);

    private SourceWorldContext? CaptureWithCurrentFields(GameState state, bool immediatelyImportSavedIndexes)
    {
        if (state.MapWidth != 220 || state.MapHeight != 220 || state.MapSha256 != _map.Source.Sha256 ||
            !Matches(state, _source.ScenarioSource) || !Matches(state, _source.FixedDataSource)) return null;
        SourceWorldContext native = CaptureSourcePrefixCore(immediatelyImportSavedIndexes);
        PersonalityEditorRuntime.Apply(state.CreatedPersonalities, native);
        AreaFlagsSourceAdapter.BindCurrent(state, native);
        // Overlay every receiver so an old or absent owner cannot retain the
        // scenario's initial kingdom name. The default receiver has its own owner.
        native.SetForceKingdomNameIndex(0, state.DefaultForceKingdomNameIndex);
        native.SetForceReportColor(0, state.DefaultForceReportColorIndex);
        for (int slot = 1; slot < 52; slot++)
            native.SetForceReportColor(slot, state.Forces.GetValueOrDefault(slot)?.ReportColorIndex);
        for (int slot = 1; slot < 52; slot++)
            native.SetForceKingdomNameIndex(slot, state.Forces.GetValueOrDefault(slot)?.KingdomNameIndex);
        foreach (DistrictState district in state.Districts.Values)
        {
            if (district.Id is < 1 or > 51 || district.ForceId is < 1 or > 51 || district.Number is < 0 or > 255) return null;
            native.WriteDistrictAffiliation(district.Id, (byte)district.ForceId, (byte)district.Number);
            ushort? leader = ForceUnregisterInputs.ReadDistrictLeader(state, district);
            native.SetDistrictRawByte(district.Id, 0x12, leader is ushort low ? (byte)low : null);
            native.SetDistrictRawByte(district.Id, 0x13, leader is ushort high ? (byte)(high >> 8) : null);
        }
        foreach (ForceState force in state.Forces.Values)
        {
            if (force.Id is < 1 or > 51 || force.RulerId is < 0 or > 6000) return null;
            native.SetOwnedForceRulerSlot10(force.Id, force.RulerId is int ruler ? (ushort)ruler : null);
        }
        foreach (OfficerState officer in state.Officers.Values)
        {
            //Only an actually saved main-world Person supplies its mutable
            //identity. Empty reserved receivers remain outside Officers.
            if (PostRegistrationLookups.HirePersonPhysicalSlot(officer.Id) is int reserved)
                native.SetPersonIdentity(reserved, checked((ushort)officer.Id));
            if (PostRegistrationLookups.RegisteredPersonPhysicalSlot(officer.Id) is int registered)
            {
                native.SetPersonIdentity(registered, checked((ushort)officer.Id));
                native.SetPersonTablePointer(officer.Id, registered);
            }
            if (native.ResolveOfficerSource(officer.Id) is not int physical) return null;
            if (officer.LocationId is < 0 or > ushort.MaxValue || officer.DistrictId is < 0 or > 51) return null;
            native.SetPersonFirstLocation(physical, OwnedArmyLifecycleDetachedInputs.ReadPersonFirstLocation(state, officer, physical));
            native.SetPersonDistrictSlot(physical, officer.DistrictId is int district ? (byte)district : null);
            native.SetPersonPortraitAppearance(physical, officer.PortraitAppearanceIndex);
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
                area.CityId is int cityId && (!state.Cities.TryGetValue(cityId, out CityState? receivingCity) || receivingCity.Id != cityId) ||
                native.ReadAreaTableReference(id)?.PhysicalSlot != id ||
                native.ReadAreaNativeValidity(new(id)) != true || native.ReadAreaHexIndex(new(id)) != area.HexId)
                return null;
            native.SetAreaCitySlot(id, AreaCityProjection.ToNative(area.CityId));
            native.SetAreaOccupationClaimForceSlot(id, area.OccupationClaimForceSlot);
        }
        // The input world owns this immutable map projection. Each current
        // reads that owner and keeps only its own changed Hex ownership fields.
        // Index/identity checks occur when a real consumer accesses a Hex;
        // do not scan and copy all 48400 cells on every current capture.
        if (!native.BindCurrentTerritoryOwnership(state.Territory)) return null;
        native.SetCityDistrictSlot(0, CityDistrictSourceAdapter.ReadDefault(state));
        foreach (CityState city in state.Cities.Values)
        {
            if (city.Id is < 1 or > 51 || city.ForceId is < 0 or > 51 || city.DistrictId is < 0 or > 51 ||
                native.ReadPhysicalObject(city.Id)?.RawField10 != city.HexId) return null;
            native.SetCityDistrictSlot(city.Id, CityDistrictSourceAdapter.Read(state, city));
            native.SetObjectRawField14(city.Id, city.Durability is >= 0 and <= ushort.MaxValue
                ? (ushort)city.Durability.Value : null);
        }
        foreach (GateState gate in state.Gates.Values)
        {
            if (gate.Id is < 52 or > 61 || gate.ForceId is < 0 or > 51 ||
                native.ReadPhysicalObject(gate.Id)?.RawField10 != gate.HexId) return null;
            native.SetObjectRawField14(gate.Id, gate.Durability is >= 0 and <= ushort.MaxValue
                ? (ushort)gate.Durability.Value : null);
        }
        if (state.LandmarkInvestments is { } landmarks)
            foreach (var (id, landmark) in landmarks) native.SetSceneryFlags47(id, landmark.Flags);
        OwnedMapObjectLifecycle.BindCurrent(state, native);
        return native;
    }

    /// <summary>Immutable-source consumers used by the native tick adapter do
    /// not need a fresh capture of the 48400 ownership/overlay entries per tick.</summary>
    internal SourceWorldContext CaptureSourcePrefix() => CaptureSourcePrefixCore(false);

    private SourceWorldContext CaptureSourcePrefixCore(bool immediatelyImportSavedIndexes)
    {
        var native = new SourceWorldContext();
        native.BindMapIdentityDefinitions(_mapTopology);
        native.InitializeStaticTablePointers();
        if (_objectDefinitions is not null) native.MaterializeFixedObjectDefinitions(_objectDefinitions);
        //Fresh CHex ctor221660/2213B0 qword+16 zeroes Hex1A. This
        //explicit allocation precedes source/map-overlay writes.
        native.BindMapDefinitions(_mapTopology);
        var normalization = Volatile.Read(ref _initialSkillNormalization);
        //Only this complete owned factory immediately replaces these lists.
        //Cold skill post-load still requires its real temporary pool.
        bool omitInitialGraph = immediatelyImportSavedIndexes && normalization is not null;
        if (normalization is null)
            for (int physical = 0; physical < 1851; physical++) native.ResetPersonTraitFields(physical);
        native.MaterializeStaticSource(_source, normalization?.Values, materializePersonalities: false,
            initialAttributes: _initialPersonAttributes);
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
            native.SetAreaFacilityCapacity(slot, area.FacilityCapacity);
            native.SetAreaHexIndex(slot, area.HexId);
        }
        //Shared fresh Base reset SC22726D/TC226FFD clears qword+4C,
        //including4E. The CE static serializer skips4E. Registration retains
        //City0; actual Object link writers below subsequently fill live Bases.
        for (int slot = 0; slot < 52; slot++) native.WriteBaseObjectLink(new(BaseKind.City, slot), 0);
        for (int slot = 0; slot < 11; slot++) native.WriteBaseObjectLink(new(BaseKind.Gate, slot), 0);
        native.MaterializeFixedTerrainTypes(_terrain);
        native.InitializeSkillDefinitionStorage(); native.MaterializeFixedSkillDefinitions(_skills);
        //This definition owner has already composed construction, scenario,
        //fixed and initial-editor writes. Each current keeps private overrides.
        native.BindInitialPersonalities(_initialPersonalities.Value);
        native.TacticParameterDecoderTable = TacticCeParameterDecoderTable.Instance;
        native.ResetPersonIdLookup(); PostRegistrationLookups.RebuildPersonIds(native);
        PostRegistrationLookups.BindHirePersonLookups(native);

        WorldIndexContext? pool = omitInitialGraph ? null : new(native);
        if (pool is not null)
            native.SetRegistryDescriptor(RegistryKind.Object, new(pool.AllocateDescriptor().Identity));
        foreach (var obj in _source.Snapshot.ObjectSourceRecords!.Objects)
        {
            if (pool is not null)
                _ = ObjectRegistration.Register(new(RegistryKind.Object, obj.PhysicalSlot), native);
            if (obj.PhysicalSlot is < 1 or > 61 || native.ReadObjectNativeValidity(new(obj.PhysicalSlot)) != true) continue;
            BaseReference reference = obj.PhysicalSlot <= 51 ? new(BaseKind.City, obj.PhysicalSlot)
                : new(BaseKind.Gate, obj.PhysicalSlot - 51);
            //The consumed331AC0/3311C0 Base link writer, before its lists.
            native.WriteBaseObjectLink(reference, (ushort)obj.PhysicalSlot);
        }
        if (pool is not null)
        {
            native.SetBaseRegistryHandle(new(pool.AllocateDescriptor().Identity));
            BaseRegistration.Apply(native);
            foreach (BaseReference? reference in native.ReadRegisteredBases()!)
                if (reference is not null) native.SetBaseHexListHandle(reference, pool.AllocateDescriptor());
            _ = BaseNeighborhoodInitialization.Apply(native);
            //Default and cold captures retain the real source registration
            //and overlay consumers. Owned warm captures bind current objects
            //before importing the actual saved list owner.
            native.SetRegistryDescriptor(RegistryKind.City, new(pool.AllocateDescriptor().Identity));
            for (int city = 0; city < 52; city++)
                _ = ObjectRegistration.Register(native.ReadRegistryTableReference(RegistryKind.City, city), native);
            MapOverlayInitialization.ApplyObjects(1, native);
        }
        if (normalization is not null) normalization.Apply(native);
        else
        {
            foreach (var person in _source.Snapshot.Characters)
            {
                if (person.SkillSource is null) continue;
                HexListHandle temporary = PersonSkillPostLoad.Apply(person.Slot, native);
                native.ClearTemporaryHexList(temporary); native.ReleaseTemporaryHexList(temporary);
            }
            // This owner retains the loaded definitions and initial editor
            // transformation. Cache only their sparse normalization writes;
            // later current fields still overlay this independent context.
            var prepared = InitialPersonSkillNormalization.Capture(_source, native);
            Interlocked.CompareExchange(ref _initialSkillNormalization, prepared, null);
        }
        return native;
    }

    private static bool Matches(GameState state, SourceProvenance source) =>
        state.ContentHashes.GetValueOrDefault(source.ResourceUri)?.Equals(source.Sha256, StringComparison.OrdinalIgnoreCase) == true;
}
