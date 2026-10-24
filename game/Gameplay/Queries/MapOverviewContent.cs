using San14.Data.Formats;

namespace San14.Client.Gameplay;

/// <summary>Map content captured once when a resource is loaded. The browser
/// cannot modify the resource reader's cell array through this view.</summary>
internal sealed class MapOverviewContent
{
    public MapOverviewContent(MapDocument map)
    {
        Width = map.Width;
        Height = map.Height;
        Cells = Array.AsReadOnly(map.Cells.ToArray());
    }

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<MapCell> Cells { get; }
}
