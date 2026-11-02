using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Original CE map facts and native radius-one candidate order.</summary>
public sealed class HexTravelMap : IHexTravelMap
{
    private readonly MapDocument _map;
    public string MapSha256 { get; }
    public int Width => _map.Width;
    public int Height => _map.Height;

    public HexTravelMap(LoadedMap loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        _map = loaded.Map;
        if (_map.Width < 1 || _map.Height < 1 || (long)_map.Width * _map.Height != _map.Cells.Length)
            throw new InvalidDataException("Hex travel map dimensions do not cover its cells.");
        MapSha256 = loaded.Source.Sha256;
    }

    public IReadOnlyList<int> RadiusOneCandidates(int hexId) =>
        HexNeighborhood.RadiusOneCandidates(hexId, Width, Height);

    public byte SurfaceCode(int hexId) => Cell(hexId).SurfaceCode;

    public int? AreaId(int hexId)
    {
        ushort area = Cell(hexId).AreaSlot;
        return area == 0 ? null : area;
    }

    private MapCell Cell(int hexId)
    {
        if (hexId < 0 || hexId >= _map.Cells.Length)
            throw new ArgumentOutOfRangeException(nameof(hexId));
        return _map.Cells[hexId];
    }
}
