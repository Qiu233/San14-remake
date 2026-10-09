using Godot;
using San14.Client.Gameplay;
using San14.Data.Formats;

/// <summary>Rasterizes the small browser overview from independent display
/// values. Campaign entities and resource validation stay outside drawing.</summary>
internal static class MapOverviewRaster
{
    public static Image Draw(MapOverviewView view, bool showOwnership, int? selectedForceId,
        int? playerForceId, MapSurface? surface)
    {
        MapOverviewContent content = view.Content;
        Image image = Image.CreateEmpty(content.Width, content.Height, false, Image.Format.Rgb8);
        foreach (MapCell cell in content.Cells)
        {
            byte code = cell.SurfaceCode;
            Color color = code == 23 ? new Color("#0d2630") : code == 19 ? new Color("#28373b") :
                Color.FromHsv((code * 0.137f) % 1.0f, 0.50f, 0.78f);
            if (showOwnership && view.Ownership is { } ownership)
            {
                MapOverviewOwnership tile = ownership[cell.Id];
                Color ownerColor = tile.AreaId is null ? new Color("#0d2630") :
                    selectedForceId is int selected && tile.ForceId == selected ? new Color("#eaa52f") :
                    tile.ForceId is null ? new Color("#a1aba2") : new Color("#64748a");
                color = surface is null ? ownerColor : surface.ColorAt(cell.Id).Lerp(ownerColor,
                    tile.AreaId is null ? 0.20f : tile.ForceId is null ? 0.13f : 0.32f);
            }
            image.SetPixel(cell.Id % content.Width, cell.Id / content.Width, color);
        }
        foreach (MapOverviewMarker gate in view.Gates) Cross(gate.HexId, 1, new Color("#55e5d2"));
        foreach (MapOverviewMarker city in view.Cities)
            Cross(city.HexId, 1, selectedForceId is int selected && city.ForceId == selected
                ? new Color("#ffd66b") : new Color("#f0f5ed"));
        foreach (MapOverviewMarker unit in view.Units)
            Cross(unit.HexId, 2, unit.ForceId == playerForceId ? new Color("#ff4837") : new Color("#e953da"));
        return image;

        void Cross(int hexId, int radius, Color marker)
        {
            int x = hexId % content.Width, y = hexId / content.Width;
            for (int delta = -radius; delta <= radius; delta++)
            {
                if (x + delta >= 0 && x + delta < content.Width) image.SetPixel(x + delta, y, marker);
                if (y + delta >= 0 && y + delta < content.Height) image.SetPixel(x, y + delta, marker);
            }
        }
    }
}
