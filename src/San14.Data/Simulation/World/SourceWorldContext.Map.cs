using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext : ICurrentGateMapState
{
    private readonly Dictionary<int, int?> _hexPointers = [], _areaPointers = [];
    private readonly Dictionary<int, ushort?> _hexAreas = [], _areaHexes = [];
    private readonly Dictionary<int, byte?> _hexForces = [], _areaValidityCodes = [], _areaCities = [];

    public string? FixedMapSourceSha256 { get; private set; }

    public void SetHexTablePointer(int slot, int? physicalSlot) => SetPointer(_hexPointers, slot, 48400, physicalSlot, 48400);
    public void SetAreaTablePointer(int slot, int? physicalSlot) => SetPointer(_areaPointers, slot, 501, physicalSlot, 501);
    public void SetHexAreaSlot(int physicalSlot, ushort? value)
    { Check(physicalSlot, 48400, "hex physical slot"); _hexAreas[physicalSlot] = value; }
    public void SetHexForceSlot(int physicalSlot, byte? value)
    { Check(physicalSlot, 48400, "hex physical slot"); _hexForces[physicalSlot] = value; }
    public void SetAreaValidityCode(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaValidityCodes[physicalSlot] = value; }
    public void SetAreaCitySlot(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaCities[physicalSlot] = value; }
    public void SetAreaHexIndex(int physicalSlot, ushort? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaHexes[physicalSlot] = value; }

    public ObjectReference? ReadObjectTableReference(int slot)
    { Check(slot, 3001, "object lookup slot"); return _objectPointers.TryGetValue(slot, out int? physical) ? new(physical) : null; }
    public bool? ReadObjectNativeValidity(ObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = MapPhysical(reference.PhysicalSlot, 3001, "object");
        if (physical == 0) return false;
        S14ConditionNativeObject? obj = ReadPhysicalObject(physical);
        return obj is null ? null : obj.RawField10 < 48400 && obj.RawField12 != 0;
    }
    public ushort? ReadObjectHexIndex(ObjectReference reference) =>
        ReadPhysicalObject(MapPhysical(reference.PhysicalSlot, 3001, "object"))?.RawField10;
    public HexReference? ReadHexTableReference(int slot)
    {
        Check(slot, 48400, "hex lookup slot");
        return _hexPointers.TryGetValue(slot, out int? physical) ? new(physical) :
            _hexIdentityDefinitions is not null ? new(slot) : null;
    }
    public ushort? ReadHexAreaSlot(HexReference reference)
    {
        int physical = MapPhysical(reference.PhysicalSlot, 48400, "hex");
        return _hexAreas.TryGetValue(physical, out ushort? area) ? area :
            _hexAreaDefinitions is not null ? (ushort)_hexAreaDefinitions.Tile(physical).AreaId : null;
    }
    public byte? ReadHexForceSlot(HexReference reference) =>
        _hexForces.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 48400, "hex"));
    public AreaReference? ReadAreaTableReference(int slot)
    { Check(slot, 501, "area lookup slot"); return _areaPointers.TryGetValue(slot, out int? physical) ? new(physical) : null; }
    public bool? ReadAreaNativeValidity(AreaReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!reference.PhysicalSlot.HasValue) return false;
        int physical = MapPhysical(reference.PhysicalSlot, 501, "area");
        if (physical == 0) return false;
        byte? code = _areaValidityCodes.GetValueOrDefault(physical);
        return code.HasValue ? code.Value is >= 1 and <= 51 : null;
    }
    public ushort? ReadAreaHexIndex(AreaReference reference) =>
        _areaHexes.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 501, "area"));
    public byte? ReadAreaCitySlot(AreaReference reference) =>
        _areaCities.GetValueOrDefault(MapPhysical(reference.PhysicalSlot, 501, "area"));
    public BaseReference? ReadCityTableReference(int slot)
    { Check(slot, 52, "city lookup slot"); return _cityPointers.TryGetValue(slot, out int? physical) ? new(BaseKind.City, physical) : null; }
    public DistrictReference? ReadCityDistrictReference(BaseReference reference)
    {
        if (reference.Kind != BaseKind.City) throw new InvalidDataException("Current city district getter requires a city pointer.");
        byte? raw = _cityDistricts.GetValueOrDefault(BasePhysical(reference));
        if (!raw.HasValue) return null;
        return _districtPointers.TryGetValue(raw.Value <= 51 ? raw.Value : 0, out int? physical) ? new(physical) : null;
    }
    public byte? ReadDistrictForceSlot(DistrictReference reference) =>
        ReadDistrictForceId(MapPhysical(reference.PhysicalSlot, 52, "district"));
    public int? ReadDistrictPhysicalSlot(DistrictReference reference) =>
        MapPhysical(reference.PhysicalSlot, 52, "district");

    /// <summary>
    /// Verified fixed CE fields only: Hex10/12, Area34 and Area3A. The successful native
    /// allocation identity initializes these pointer tables. Current Hex14/Area35
    /// are separate mutable inputs and are neither inferred nor reset here.
    /// </summary>
    public void MaterializeFixedMapSource(S14Document fixedData, MapDocument map)
    {
        ArgumentNullException.ThrowIfNull(fixedData); ArgumentNullException.ThrowIfNull(map);
        if (fixedData.FormatMarker != 0x66 || fixedData.DecodedPayload.Length < 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(fixedData.DecodedPayload) != fixedData.FormatMarker ||
            map.Width != 220 || map.Height != 220)
            throw new InvalidDataException("Current map materialization requires the inspected CE fixed-map profile.");
        S14ScenarioTables.ValidateFixedMapCodes(fixedData, map);
        IReadOnlyList<S14AreaDefinition> areas = S14ScenarioTables.ReadAreaDefinitions(fixedData);
        for (int slot = 0; slot < map.Cells.Length; slot++)
        {
            if (map.Cells[slot].Id != slot) throw new InvalidDataException($"Current fixed hex identity differs at {slot}.");
            SetHexSurfaceCode(slot, map.Cells[slot].SurfaceCode);
            SetHexAreaSlot(slot, map.Cells[slot].AreaSlot);
            SetHexTablePointer(slot, slot);
        }
        for (int slot = 0; slot < areas.Count; slot++)
        {
            S14AreaDefinition area = areas[slot];
            if (area.Slot != slot) throw new InvalidDataException($"Current fixed area identity differs at {slot}.");
            SetAreaValidityCode(slot, area.CitySlot);
            SetAreaHexIndex(slot, area.HexId);
            SetAreaTablePointer(slot, slot);
            if (area.RawNeighborSlots is not { Count:8 })
                throw new InvalidDataException($"Fixed area {slot} does not contain eight raw neighbor words.");
            for (int index = 0; index < 8; index++)
                SetAreaNeighborSlot(slot,index,area.RawNeighborSlots[index]);
        }
        FixedMapSourceSha256 = Convert.ToHexString(SHA256.HashData(fixedData.DecodedPayload));
    }

    private static int MapPhysical(int? physicalSlot, int count, string name)
    {
        int physical = physicalSlot ?? throw new InvalidDataException($"Current {name} pointer is explicitly null; native field read would be unsafe.");
        Check(physical, count, $"{name} physical slot");
        return physical;
    }
}
