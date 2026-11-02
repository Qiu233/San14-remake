namespace San14.Data;

/// <summary>Scenario Area auxiliary values shared by the captures of one
/// definition factory. Current City35, ownership, registries and lists are absent.</summary>
internal sealed class InitialAreaScenarioValues
{
    private readonly LoadedScenario owner;
    private readonly Row[] rows;
    private readonly ushort?[] neighbors;

    internal InitialAreaScenarioValues(LoadedScenario source)
    {
        owner = source;
        var areas = source.Snapshot.Areas;
        if (areas.Count != 501)
            throw new InvalidDataException("Initial Area values require the complete loaded receiver table.");
        rows = new Row[areas.Count]; neighbors = new ushort?[areas.Count * 8];
        for (int slot = 0; slot < areas.Count; slot++)
        {
            var area = areas[slot];
            if (area.Slot != slot || area.RawNeighborSlots is { Count: not 8 })
                throw new InvalidDataException("Initial Area values contain contradictory receiver or neighbor fields.");
            rows[slot] = new(area.ForceSlot, area.RawFlags38, area.RawFlag74);
            for (int index = 0; index < 8; index++) neighbors[slot * 8 + index] = area.RawNeighborSlots?[index];
        }
    }

    internal bool BelongsTo(LoadedScenario source) => ReferenceEquals(owner, source);
    internal byte? Force(int physical) => rows[physical].Force;
    internal byte? Flags38(int physical) => rows[physical].Flags38;
    internal byte? Flag74(int physical) => rows[physical].Flag74;
    internal ushort? Neighbor(int physical, int index) => neighbors[physical * 8 + index];
    private readonly record struct Row(byte? Force, byte? Flags38, byte? Flag74);
}
