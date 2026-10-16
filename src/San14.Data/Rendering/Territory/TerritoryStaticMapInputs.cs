using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Fixed map fields used by the territory surface and area layers.
/// Current ownership and optional display providers belong to their live writers.</summary>
public sealed class TerritoryStaticMapInputs
{
    public const int CellCount = 220 * 220;
    public const int AreaCount = 501;
    private readonly ushort[] terrainFlags, areaSlots;
    private readonly byte[] surfaceCodes, validAreas;

    private TerritoryStaticMapInputs(ushort[] terrain, byte[] surface, ushort[] areas, byte[] valid)
        => (terrainFlags, surfaceCodes, areaSlots, validAreas) = (terrain, surface, areas, valid);

    public ReadOnlySpan<ushort> TerrainFlags => terrainFlags;
    public ReadOnlySpan<byte> SurfaceCodes => surfaceCodes;
    public ReadOnlySpan<ushort> AreaSlots => areaSlots;
    public ReadOnlySpan<byte> ValidAreas => validAreas;

    public static TerritoryStaticMapInputs Create(ScenarioCityPreview scenario, LoadedMap map,
        LoadedTerrainTypes terrain)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terrain);
        if (GameResourceId.File(terrain.Source.SourcePath) != GameResourceId.File(scenario.FixedDataPath))
            throw new InvalidDataException("Territory terrain definitions belong to another fixed source.");
        if (map.Map.Width != 220 || map.Map.Height != 220 || map.Map.Cells.Length != CellCount ||
            terrain.Catalog.Types.Count != 31 || scenario.Areas.Count != AreaCount)
            throw new InvalidDataException("Territory static inputs require the complete map, terrain and fixed area tables.");
        for (int slot = 0; slot < 31; slot++)
        {
            var row = terrain.Catalog.Types[slot];
            if (row.Slot != slot || row.RawBytes.Length != 51 ||
                BinaryPrimitives.ReadUInt16LittleEndian(row.RawBytes.AsSpan(26, 2)) != row.RawField2A)
                throw new InvalidDataException("Territory terrain row differs from its source count field.");
        }
        var flags = new ushort[CellCount];
        var surfaces = new byte[CellCount];
        var areas = new ushort[CellCount];
        for (int id = 0; id < CellCount; id++)
        {
            var cell = map.Map.Cells[id];
            if (cell.Id != id)
                throw new InvalidDataException("Territory map cell physical ID differs.");
            surfaces[id] = cell.SurfaceCode;
            areas[id] = cell.AreaSlot;
            flags[id] = terrain.Catalog.Types[cell.SurfaceCode <= 30 ? cell.SurfaceCode : 0].RawField2A;
        }
        var valid = new byte[AreaCount];
        for (int slot = 0; slot < AreaCount; slot++)
        {
            var area = scenario.Areas[slot];
            if (area.Slot != slot)
                throw new InvalidDataException("Territory fixed area physical order differs.");
            valid[slot] = (byte)(slot != 0 && area.CitySlot is >= 1 and <= 51 ? 1 : 0);
        }
        return new(flags, surfaces, areas, valid);
    }
}
