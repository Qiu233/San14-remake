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
        if (_map.Width != 220 || _map.Height != 220 || _map.Cells.Length != 48400)
            throw new InvalidDataException("The inspected original hex-neighborhood profile requires a 220x220 map.");
        MapSha256 = loaded.Source.Sha256;
    }

    public IReadOnlyList<int> RadiusOneCandidates(int hexId) =>
        HexNeighborhood.RadiusOneCandidates(hexId);

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
