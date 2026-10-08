namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalAreaMapStatisticsState,
    IOriginalAreaMapStatisticsInitializationState
{
    private readonly Dictionary<int, int?> _surfaceTypePointers = [];
    private readonly Dictionary<int, ushort?> _surfaceTypeFields2A = [], _areaFields68 = [], _areaFields76 = [];
    private readonly Dictionary<int, byte?> _areaFields78 = [];

    public void SetSurfaceTypeTablePointer(int slot, int? physicalSlot) =>
        SetPointer(_surfaceTypePointers, slot, 31, physicalSlot, 31);
    public OriginalSurfaceTypeReference? ReadSurfaceTypeTableReference(int slot)
    { Check(slot, 31, "surface type lookup slot"); return _surfaceTypePointers.TryGetValue(slot, out int? physical) ? new(physical) : null; }
    public void SetSurfaceTypeRawField2A(int physicalSlot, ushort? value)
    { Check(physicalSlot, 31, "surface type physical slot"); _surfaceTypeFields2A[physicalSlot] = value; }
    public ushort? ReadSurfaceTypeRawField2A(OriginalSurfaceTypeReference surface) =>
        _surfaceTypeFields2A.GetValueOrDefault(MapPhysical(surface.PhysicalSlot, 31, "surface type"));

    public int? ReadAreaMapForceSlot(OriginalAreaReference area) => ReadAreaCurrentForceSlot(area);
    public int? CountAreaTerrainCells(OriginalAreaReference area) => OriginalAreaMapStatistics.CountTerrainCells(area, this);
    public int? CountAreaOwnedCells(OriginalAreaReference area) => OriginalAreaMapStatistics.CountOwnedCells(area, this);
    public bool? ReadMapStatisticsWorldPresent() => SpecialModeWorldPresent;
    public int? ReadMapStatisticsWorldField48() => RawWorldField48;
    public void SetAreaRawField68(int physicalSlot, ushort? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFields68[physicalSlot] = value; }
    public void SetAreaRawField76(int physicalSlot, ushort? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFields76[physicalSlot] = value; }
    public void SetAreaRawField78(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFields78[physicalSlot] = value; }
    public ushort? ReadAreaRawField68(OriginalAreaReference area) =>
        _areaFields68.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public ushort? ReadAreaRawField76(OriginalAreaReference area) =>
        _areaFields76.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public byte? ReadAreaRawField78(OriginalAreaReference area) =>
        _areaFields78.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void WriteAreaRawField68(OriginalAreaReference area, ushort value) =>
        SetAreaRawField68(MapPhysical(area.PhysicalSlot, 501, "area"), value);
    public void WriteAreaRawField76(OriginalAreaReference area, ushort value) =>
        SetAreaRawField76(MapPhysical(area.PhysicalSlot, 501, "area"), value);
    public void WriteAreaRawField78(OriginalAreaReference area, byte value) =>
        SetAreaRawField78(MapPhysical(area.PhysicalSlot, 501, "area"), value);
    public bool InitializeAreaMapStatistics(int staticFlag) => OriginalAreaMapStatisticsInitialization.Apply(staticFlag, this);
}
