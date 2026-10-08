namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalSpecialModeAreaResetState
{
    private readonly Dictionary<int, byte?> _areaFlags74 = [], _areaFlags38 = [];
    private readonly Dictionary<(int Physical, int Index), ushort?> _areaNeighborSlots = [];
    public void ApplySpecialModeAreaReset(OriginalAreaReference area) => _ = OriginalSpecialModeAreaReset.Apply(area, this);
    public void SetAreaFlag74(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFlags74[physicalSlot] = value; }
    public byte? ReadAreaFlag74(OriginalAreaReference area) => _areaFlags74.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void SetAreaFlags38(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFlags38[physicalSlot] = value; }
    public byte? ReadAreaFlags38(OriginalAreaReference area) => _areaFlags38.GetValueOrDefault(MapPhysical(area.PhysicalSlot, 501, "area"));
    public void WriteAreaFlags38(OriginalAreaReference area, byte value) => SetAreaFlags38(MapPhysical(area.PhysicalSlot, 501, "area"), value);
    public void SetAreaNeighborSlot(int physicalSlot, int index, ushort? value)
    { Check(physicalSlot, 501, "area physical slot"); Check(index, 8, "area neighbor word index"); _areaNeighborSlots[(physicalSlot,index)] = value; }
    public ushort? ReadAreaNeighborSlot(OriginalAreaReference area, int index)
    { Check(index, 8, "area neighbor word index"); return _areaNeighborSlots.GetValueOrDefault((MapPhysical(area.PhysicalSlot,501,"area"),index)); }
    public void WriteAreaValidityCode(OriginalAreaReference area, byte value) => SetAreaValidityCode(MapPhysical(area.PhysicalSlot,501,"area"),value);
    public void WriteHexSurfaceCode(OriginalHexReference hex, byte value) => SetHexSurfaceCode(MapPhysical(hex.PhysicalSlot,48400,"hex"),value);
}
