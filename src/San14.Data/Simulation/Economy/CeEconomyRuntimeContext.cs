using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>Explicit current inputs consumed by the two CE monthly-income functions.
/// Native query state is shared with the initialization replay. Source records establish
/// the deserialize prefix; missing later writers remain unknown.</summary>
public sealed class CeEconomyRuntimeContext : ICeMonthlyIncomeContext
{
    private sealed record AreaFields(ushort? Raw44, ushort? Raw46, ushort? Raw52,
        ushort? Progress3C = null, ushort? Progress3E = null, ushort? Officer36 = null, byte? Bonus6D = null);
    private readonly Dictionary<int, AreaFields> _areas = [];
    private readonly Dictionary<int, CeEconomyPersonContext> _persons = [];
    private readonly Dictionary<int, byte[]?> _activePanels = [], _suppressedPanels = [];
    private readonly Dictionary<int, short?> _panelValues = [];
    private readonly LoadedScenario _source;

    public SourceWorldContext Native { get; }
    public string ScenarioSourceSha256 => _source.ScenarioSource.Sha256;
    public string FixedSourceSha256 => _source.FixedDataSource.Sha256;
    public string? MapSourceSha256 { get; private set; }
    public string InitializationStage { get; private set; } = "static-source-deserialized";
    public string[] UnresolvedInitializationStages { get; private set; } =
        ["economy.startup_worker", "economy.first_strategy_events", "economy.current_person_effects", "economy.current_force_panel_flags"];
    public IReadOnlyList<InitialEconomySourceRow> SourceRows { get; }

    public CeEconomyRuntimeContext(LoadedScenario source, SourceWorldContext native)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(native);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Areas.Count != 501)
            throw new InvalidDataException("Current CE economy context requires all501 inspected source area rows.");
        _source = source; Native = native;
        SourceRows = Array.AsReadOnly(source.Snapshot.Areas.Select(area => InitialEconomySource.Project(area, source)).ToArray());
        foreach (InitialEconomySourceRow row in SourceRows)
            _areas.Add(row.AreaSlot, new(row.Raw44AfterFixedLoad, row.Raw46AfterFixedLoad, row.Raw52AfterScenarioLoad));
    }

    /// <summary>Builds the source-derived map prefix through the consumed331AC0/
    /// 3311E0 writes. This is usable by the monthly provider while startup fields
    /// stay null. It does not declare the first player stage complete.</summary>
    public static CeEconomyRuntimeContext CreateInitial(LoadedScenario source,
        LoadedMap map, LoadedTerrainTypes terrain)
    {
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(terrain);
        if (map.Map.Width != 220 || map.Map.Height != 220 || map.Map.Cells.Length != 48400 ||
            terrain.Source.Sha256 != source.FixedDataSource.Sha256)
            throw new InvalidDataException("Economy map prefix has a contradictory map or fixed terrain source.");
        var native = new SourceWorldContext();
        _ = ScenarioTerritory.Build(map.Map, source.Snapshot); //Validate representativeHex membership against the selected map.
        native.MaterializeStaticSource(source);
        native.MaterializeFixedTerrainTypes(terrain);
        var result = new CeEconomyRuntimeContext(source, native);
        result.MapSourceSha256 = map.Source.Sha256;
        foreach (AreaPreviewRecord area in source.Snapshot.Areas)
        {
            native.SetAreaTablePointer(area.Slot, area.Slot);
            native.SetAreaValidityCode(area.Slot, area.CitySlot);
            native.SetAreaHexIndex(area.Slot, area.HexId);
            //Current240D20's numeric area35 write, independent of its person-location side effects.
            native.SetAreaCitySlot(area.Slot, (byte)Math.Clamp((int)area.CitySlot, 0, 51));
        }
        foreach (var cell in map.Map.Cells)
        {
            if (cell.Id is < 0 or >= 48400 || cell.AreaSlot is < 0 or > 500)
                throw new InvalidDataException("Economy prefix map identity or area membership is outside the native table.");
            native.SetHexTablePointer(cell.Id, cell.Id);
            native.SetHexAreaSlot(cell.Id, cell.AreaSlot);
            native.SetHexSurfaceCode(cell.Id, cell.SurfaceCode);
        }
        //Current331AC0 stores these links only for valid source object receivers.
        foreach (var obj in source.Snapshot.ObjectSourceRecords!.Objects)
            if (obj.PhysicalSlot is >= 1 and <= 51 && obj.RawField10 < 48400 && obj.RawField12 != 0)
                native.WriteBaseObjectLink(new(BaseKind.City, obj.PhysicalSlot), (ushort)obj.PhysicalSlot);
        int? mode = source.Snapshot.WorldParameters?.RawField48;
        //228D10 can alter the topology in special modes. The ordinary prefix is
        //only selected by the actual source mode, never by a scenario title.
        if (mode.HasValue && unchecked((uint)mode.Value - 100u) > 99u)
        {
            foreach (var cell in map.Map.Cells)
            {
                AreaPreviewRecord area = source.Snapshot.Areas[cell.AreaSlot];
                if (native.ReadAreaNativeValidity(new(area.Slot)) != true) continue;
                if (native.ReadBaseNativeValidity(new(BaseKind.City, area.CitySlot <= 51 ? area.CitySlot : 0)) != true) continue;
                //The static ownership segment overwrites every member with area6A.
                //Current241650's independent scenery/service effects are not needed to
                //establish this consumed force14 store.
                native.SetHexForceSlot(cell.Id, (byte)Math.Clamp((int)area.ForceSlot, 0, 51));
            }
            foreach (AreaPreviewRecord area in source.Snapshot.Areas)
            {
                var reference = new AreaReference(area.Slot);
                if (native.ReadAreaNativeValidity(reference) != true) continue;
                //Do not consume an unknown force left outside the static ownership gate.
                if (native.ReadAreaCurrentForceSlot(reference) is null) continue;
                bool complete = map.Map.Cells.Where(cell => cell.AreaSlot == area.Slot)
                    .All(cell => native.ReadHexForceSlot(new(cell.Id)).HasValue);
                if (!complete) continue;
                native.SetAreaRawField76(area.Slot, AreaMapStatisticsInitialization.CountToNativeWord(
                    AreaMapStatistics.CountTerrainCells(reference, native)));
                native.SetAreaRawField68(area.Slot, AreaMapStatisticsInitialization.CountToNativeWord(
                    AreaMapStatistics.CountOwnedCells(reference, native)));
            }
            result.InitializationStage = "ordinary-static-map-economy-prefix";
        }
        return result;
    }

    /// <summary>Applies known current native fields without substituting source
    /// reset values for missing startup results.</summary>
    public void SetAreaCurrentFields(int areaSlot, ushort? raw44, ushort? raw46, ushort? raw52,
        ushort? progress3C, ushort? progress3E, ushort? officer36, byte? bonus6D)
    {
        CheckArea(areaSlot);
        _areas[areaSlot] = new(raw44, raw46, raw52, progress3C, progress3E, officer36, bonus6D);
    }

    /// <summary>Numeric branch of the native progress setters with notification
    /// flag0. A flag1 level-change callback requires the remaining force side effects.</summary>
    public void WriteAreaProgressWithoutNotification(int areaSlot, int nativeOffset, int value)
    {
        CheckArea(areaSlot);
        AreaFields row = _areas[areaSlot];
        ushort bounded = (ushort)Math.Clamp(value, 0, 1000);
        _areas[areaSlot] = nativeOffset switch
        {
            0x3C => row with { Progress3C = bounded },
            0x3E => row with { Progress3E = bounded },
            _ => throw new InvalidDataException("Monthly income consumes only area progress3C/3E.")
        };
    }

    public void SetPersonCurrentContext(int nativePersonId, CeEconomyPersonContext? current)
    {
        if (nativePersonId is < 0 or > 6000) throw new ArgumentOutOfRangeException(nameof(nativePersonId));
        if (current is null) _persons.Remove(nativePersonId); else _persons[nativePersonId] = current;
    }

    /// <summary>Caller branch shared by2BD0D0/2BCBA0. Unknown branch inputs
    /// remain unknown; the fallback percentage is the current global476C.</summary>
    public static int? ReadPersonTaskPercent(bool? global248Nonzero, int? personEffect7E,
        int? personTask220, int? global476C)
    {
        if (global248Nonzero == true) return 100;
        if (global248Nonzero is null || personEffect7E is null) return null;
        if (personEffect7E > 0) return 100;
        return personTask220 switch
        {
            null => null, 0 or 1 => 100, 2 => 64, 3 => 44, _ => global476C
        };
    }

    /// <summary>Exactly ten bytes at nativeforce1CB/1D5; null means unknown.</summary>
    public void SetForceCurrentPanelFlags(int forceSlot, byte[]? active1CB, byte[]? suppressed1D5)
    {
        if (forceSlot is < 0 or > 51 || active1CB is { Length: not 10 } || suppressed1D5 is { Length: not 10 })
            throw new InvalidDataException("Current force panel flags require a physical force slot and10-byte windows.");
        _activePanels[forceSlot] = active1CB?.ToArray(); _suppressedPanels[forceSlot] = suppressed1D5?.ToArray();
    }

    public void MaterializePanelCoefficientSource(LoadedDoctrinePanels loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if (loaded.Source.Sha256 != FixedSourceSha256 || loaded.Catalog.FormatMarker != 0x66 || loaded.Catalog.Panels.Count != 80)
            throw new InvalidDataException("Economy panel coefficients differ from the bound fixed source.");
        foreach (var row in loaded.Catalog.Panels)
        {
            if (row.Slot is < 0 or > 79 || row.RawBytes.Length != 33 ||
                BinaryPrimitives.ReadInt16LittleEndian(row.RawBytes.AsSpan(23, 2)) != row.RawValue)
                throw new InvalidDataException("Economy panel coefficient source row is contradictory.");
        }
        foreach (var row in loaded.Catalog.Panels) _panelValues[row.Slot] = row.RawValue;
    }

    public void SetCurrentPanelCoefficient(int physicalSlot, short? value)
    {
        if (physicalSlot is < 0 or > 79) throw new ArgumentOutOfRangeException(nameof(physicalSlot));
        _panelValues[physicalSlot] = value;
    }

    /// <summary>Current22C510, whose table85F20 is CDoctrinePanelData.
    /// CPolicyEffectData's ten level values are a different table and query.</summary>
    public int? ReadForcePanelModifier(int forceSlot, int effectId)
    {
        if (forceSlot is < 0 or > 51 || effectId is < 1 or > 79)
            throw new InvalidDataException("Economy panel modifier request is outside its audited native input domain.");
        byte[]? active = _activePanels.GetValueOrDefault(forceSlot), suppressed = _suppressedPanels.GetValueOrDefault(forceSlot);
        int index = (effectId - 1) / 8, mask = 1 << ((effectId - 1) % 8);
        if (active is null) return null;
        if ((active[index] & mask) == 0) return 0;
        if (suppressed is null) return null;
        return (suppressed[index] & mask) != 0 ? 0 : _panelValues.GetValueOrDefault(effectId);
    }

    public CeCityIncomeCurrentContext? ReadCity(GameState state, CityState city)
    {
        if (!BoundSource(state) || city.Id is < 0 or > 51) return null;
        BaseReference? reference = Native.ReadCityTableReference(city.Id);
        if (reference is null) return null;
        if (reference.PhysicalSlot is null) return new(false, null, null);
        bool? valid = Native.ReadBaseNativeValidity(reference);
        int? force = Native.ReadBaseForceSlot(reference);
        if (force.HasValue && force.Value != (city.ForceId ?? 0)) return null;
        ForceReference? owner = force is >= 0 ? Native.ReadForceTableReference(force <= 51 ? force.Value : 0) : null;
        bool? ownerValid = owner is null ? null : Native.ReadForceNativeValidity(owner);
        bool? special = ReadSpecialCapacity(reference, owner);
        return new(valid, Native.ReadCitySizeB8(reference), special)
        { NativeOwnerForceValid = ownerValid };
    }

    public CeAreaIncomeCurrentContext? ReadArea(GameState state, CityState city, AreaState area)
    {
        if (!BoundSource(state) || !_areas.TryGetValue(area.Id, out AreaFields? row) ||
            area.EconomySource?.ScenarioSha256 != ScenarioSourceSha256 || area.EconomySource.FixedDataSha256 != FixedSourceSha256)
            return null;
        AreaReference? reference = Native.ReadAreaTableReference(area.Id);
        if (reference?.PhysicalSlot is null) return null;
        foreach (TerritoryState cell in state.Territory.Values.Where(cell => cell.AreaId == area.Id))
        {
            HexReference? hex = Native.ReadHexTableReference(cell.Id);
            if (hex?.PhysicalSlot is null || Native.ReadHexAreaSlot(hex) != area.Id) return null;
            byte? force14 = Native.ReadHexForceSlot(hex);
            if (force14.HasValue && force14.Value != (cell.ForceId ?? 0)) return null;
        }
        byte? citySlot = Native.ReadAreaCitySlot(reference);
        BaseReference? nativeCity = citySlot.HasValue ? Native.ReadCityTableReference(citySlot <= 51 ? citySlot.Value : 0) : null;
        int? force = nativeCity is null ? null : Native.ReadBaseForceSlot(nativeCity);
        if (citySlot.HasValue && citySlot.Value != city.Id || force.HasValue && force.Value != (city.ForceId ?? 0))
            return null;
        ForceReference? owner = force is >= 0 ? Native.ReadForceTableReference(force <= 51 ? force.Value : 0) : null;
        bool? forceValid = owner is null ? null : Native.ReadForceNativeValidity(owner);
        CeEconomyPersonContext? person = row.Officer36.HasValue ? _persons.GetValueOrDefault(row.Officer36.Value) : null;
        return new()
        {
            //The income entrance actually tests the area35-selected CITY.
            NativeAreaValid = nativeCity is null ? null : Native.ReadBaseNativeValidity(nativeCity),
            RepresentativeForceSlot = Native.ReadAreaCurrentForceSlot(reference),
            AlternativeBranch = owner?.PhysicalSlot is int physical ? physical is >= 46 and <= 50 : null,
            RawField44 = row.Raw44, RawField46 = row.Raw46,
            RawField52 = area.NativeNormalAftermath is { } aftermath ? aftermath.Quantity52 : row.Raw52,
            Progress3C = row.Progress3C, Progress3E = row.Progress3E, Bonus6D = row.Bonus6D,
            OwnedCells68 = Native.ReadAreaRawField68(reference), TerrainCells76 = Native.ReadAreaRawField76(reference),
            NativePersonValid = person?.NativeValid, EffectivePerson2 = person?.EffectiveValue2,
            EffectivePerson3 = person?.EffectiveValue3, PersonTaskPercent = person?.TaskPercent,
            PersonEffect24 = person?.Effect24, PersonEffect25 = person?.Effect25,
            ForceEffect35 = forceValid == false ? 0 : force.HasValue && forceValid == true ? ReadForcePanelModifier(force.Value, 0x35) : null,
            ForceEffect34 = forceValid == false ? 0 : force.HasValue && forceValid == true ? ReadForcePanelModifier(force.Value, 0x34) : null,
            AreaFlags38 = Native.ReadAreaFlags38(reference)
        };
    }

    private bool BoundSource(GameState state) => state.ContentHashes.Values.Contains(ScenarioSourceSha256, StringComparer.OrdinalIgnoreCase)
        && state.ContentHashes.Values.Contains(FixedSourceSha256, StringComparer.OrdinalIgnoreCase)
        && (MapSourceSha256 is null || state.MapSha256.Equals(MapSourceSha256, StringComparison.OrdinalIgnoreCase));
    private bool? ReadSpecialCapacity(BaseReference reference, ForceReference? owner)
    {
        ushort? link = Native.ReadBaseObjectLink(reference);
        if (!link.HasValue) return null;
        if (link > 51) return false;
        if (owner?.PhysicalSlot is not int force) return null;
        if (force is < 46 or > 50) return false;
        //The special predicate resolves the related city through Object10 ->
        //Hex12 -> Area35. It is not necessarily the receiver's physical slot.
        ObjectReference? obj = Native.ReadObjectTableReference(link.Value);
        if (obj is null) return null;
        bool? valid = Native.ReadObjectNativeValidity(obj);
        if (!valid.HasValue) return null;
        ushort? rawHex = valid.Value ? Native.ReadObjectHexIndex(obj) : (ushort)0;
        if (!rawHex.HasValue) return null;
        HexReference? hex = Native.ReadHexTableReference(rawHex <= 48399 ? rawHex.Value : 0);
        if (hex is null) return null;
        ushort? rawArea = hex.PhysicalSlot.HasValue ? Native.ReadHexAreaSlot(hex) : (ushort)0;
        if (!rawArea.HasValue) return null;
        AreaReference? area = Native.ReadAreaTableReference(rawArea <= 500 ? rawArea.Value : 0);
        if (area is null) return null;
        bool? areaValid = Native.ReadAreaNativeValidity(area);
        if (!areaValid.HasValue) return null;
        byte? rawCity = areaValid.Value ? Native.ReadAreaCitySlot(area) : (byte)0;
        if (!rawCity.HasValue) return null;
        BaseReference? related = Native.ReadCityTableReference(rawCity <= 51 ? rawCity.Value : 0);
        if (related?.PhysicalSlot is not int physical) return null;
        int? freshForce = Native.ReadBaseForceSlot(reference);
        return freshForce.HasValue ? physical is >= 47 and <= 51 && freshForce == physical - 1 : null;
    }
    private static void CheckArea(int slot)
    { if (slot is < 0 or > 500) throw new ArgumentOutOfRangeException(nameof(slot)); }
}

/// <summary>Native2300B0 outputs plus the caller's task factor and2B2FB0 effects.
/// Values are explicit current results; static abilities alone do not fill this record.</summary>
public sealed record CeEconomyPersonContext(bool? NativeValid, int? EffectiveValue2,
    int? EffectiveValue3, int? TaskPercent, int? Effect24, int? Effect25);
