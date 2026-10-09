namespace San14.Core;

public readonly record struct MapTileDefinition(int Id, int AreaId, int TerrainTypeId);

/// <summary>Read-only map content owned by a campaign. Current ownership,
/// buildings and armies belong to the world, not these definitions.</summary>
public sealed class MapTopology
{
    private readonly MapTileDefinition[] _tiles;
    public int Width { get; }
    public int Height { get; }
    public int TileCount => _tiles.Length;

    public MapTopology(int width, int height, IEnumerable<MapTileDefinition> tiles)
    {
        _tiles = tiles.ToArray();
        if (width <= 0 || height <= 0 || (long)width * height != _tiles.Length ||
            _tiles.Where((tile, index) => tile.Id != index || tile.AreaId < 0 || tile.TerrainTypeId < 0).Any())
            throw new InvalidDataException("Map topology dimensions or tile definitions are invalid.");
        Width = width; Height = height;
    }

    public MapTileDefinition Tile(int id) => _tiles[id];
}
