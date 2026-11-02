namespace San14.Data;

public sealed partial class SourceWorldContext : ISpecialModeAreaResetState
{
    private readonly Dictionary<int, byte?> _areaFlags74 = [], _areaFlags38 = [];
    // Army return borrows only the immutable Area38 read channel. Do not
    // expose the other Area fields through the general initial-values owner.
    private InitialAreaScenarioValues? _armyReturnAreaFlags38Source;
    private readonly Dictionary<(int Physical, int Index), ushort?> _areaNeighborSlots = [];
    public void ApplySpecialModeAreaReset(AreaReference area) => _ = SpecialModeAreaReset.Apply(area, this);
    public void SetAreaFlag74(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFlags74[physicalSlot] = value; }
    public byte? ReadAreaFlag74(AreaReference area)
    { int physical = MapPhysical(area.PhysicalSlot,501,"area"); return _areaFlags74.TryGetValue(physical,out var value) ? value : _initialAreaScenarioValues?.Flag74(physical); }
    public void SetAreaFlags38(int physicalSlot, byte? value)
    { Check(physicalSlot, 501, "area physical slot"); _areaFlags38[physicalSlot] = value; }
    public byte? ReadAreaFlags38(AreaReference area)
    { int physical = MapPhysical(area.PhysicalSlot,501,"area"); return _areaFlags38.TryGetValue(physical,out var value) ? value : (_initialAreaScenarioValues ?? _armyReturnAreaFlags38Source)?.Flags38(physical); }
    // Raw static/component contexts remain current-only. Captured campaign
    // contexts install an explicit read-only or reached-world preparation.
    internal Func<int, byte, Action>? AreaFlags38WritePreparation { get; set; }
    public void WriteAreaFlags38(AreaReference area, byte value)
    {
        int physical = MapPhysical(area.PhysicalSlot, 501, "area");
        Action? publish = AreaFlags38WritePreparation?.Invoke(physical, value);
        SetAreaFlags38(physical, value);
        publish?.Invoke();
    }
    public void SetAreaNeighborSlot(int physicalSlot, int index, ushort? value)
    { Check(physicalSlot, 501, "area physical slot"); Check(index, 8, "area neighbor word index"); _areaNeighborSlots[(physicalSlot,index)] = value; }
    public ushort? ReadAreaNeighborSlot(AreaReference area, int index)
    {
        Check(index,8,"area neighbor word index");int physical = MapPhysical(area.PhysicalSlot,501,"area");
        return _areaNeighborSlots.TryGetValue((physical,index),out var value) ? value : _initialAreaScenarioValues?.Neighbor(physical,index);
    }
    public void WriteAreaValidityCode(AreaReference area, byte value) => SetAreaValidityCode(MapPhysical(area.PhysicalSlot,501,"area"),value);
    public void WriteHexSurfaceCode(HexReference hex, byte value) => SetHexSurfaceCode(MapPhysical(hex.PhysicalSlot,48400,"hex"),value);
}
