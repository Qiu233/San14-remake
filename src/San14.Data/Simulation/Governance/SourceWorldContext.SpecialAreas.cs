namespace San14.Data;

public sealed partial class SourceWorldContext : ISpecialModeAreaResetState
{
    private readonly Dictionary<int, byte?> _areaFlags74 = [], _areaFlags38 = [];
    private readonly Dictionary<(int Physical, int Index), ushort?> _areaNeighborSlots = [];
    public void ApplySpecialModeAreaReset(AreaReference area) => _ = SpecialModeAreaReset.Apply(area, this);
    public void SetAreaFlag74(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFlags74[physicalSlot] = value; }
    public byte? ReadAreaFlag74(AreaReference area) => _areaFlags74.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void SetAreaFlags38(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFlags38[physicalSlot] = value; }
    public byte? ReadAreaFlags38(AreaReference area) => _areaFlags38.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void WriteAreaFlags38(AreaReference area, byte value) => SetAreaFlags38(MapPhysical(area.PhysicalSlot, 501, "area"), value);
    public void SetAreaNeighborSlot(int physicalSlot, int index, ushort? value)
    { Check(physicalSlot, 501, "area physical slot"); Check(index, 8, "area neighbor word index"); _areaNeighborSlots[(physicalSlot,index)] = value; }
    public ushort? ReadAreaNeighborSlot(AreaReference area, int index)
    { Check(index, 8, "area neighbor word index"); return _areaNeighborSlots.GetValueOrDefault((MapPhysical(area.PhysicalSlot,501,"area"),index)); }
    public void WriteAreaValidityCode(AreaReference area, byte value) => SetAreaValidityCode(MapPhysical(area.PhysicalSlot,501,"area"),value);
    public void WriteHexSurfaceCode(HexReference hex, byte value) => SetHexSurfaceCode(MapPhysical(hex.PhysicalSlot,48400,"hex"),value);
}
