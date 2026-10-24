using System.Globalization;
using Godot;
using San14.Data;

/// <summary>The active route's numbered ground markers. Camera updates only
/// reproject their cached terrain anchors; they do not search a route.</summary>
internal partial class UnitWaypointMarkerLayer : Control
{
    private readonly List<Control> markers = [];
    private readonly List<int?> hexes = [];
    private UiSourceLayout source = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapFont font = null!;
    private Func<int, Vector2?>? project;
    internal int WaypointCount { get; private set; }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        Func<int, Vector2?> project)
    {
        Name = "UnitSourceWaypoints"; MouseFilter = MouseFilterEnum.Ignore;
        this.assets = assets; this.shader = shader; this.font = font; this.project = project;
        source = new(repository, 0xC46DC9B2);
        SetProcess(false);
    }

    internal void Apply(IReadOnlyList<int> points, int? preview)
    {
        WaypointCount = points.Count;
        hexes.Clear(); foreach (int point in points) hexes.Add(point);
        if (hexes.Count < 5) hexes.Add(preview);
        while (markers.Count < hexes.Count)
        {
            int ordinal = markers.Count + 1;
            var marker = new Control { Name = "Waypoint" + ordinal, Size = source.Rectangle(0).Size,
                MouseFilter = MouseFilterEnum.Ignore };
            AddChild(marker);
            // Numbered markers retain the authored background. The category
            // icon (node2) is hidden by the source number setter.
            source.Art(marker, 1, source.Rectangle(1), assets, shader);
            source.Text(marker, 3, default, ordinal.ToString(CultureInfo.InvariantCulture), font,
                MapTextPresets.Create()[13]);
            marker.SetMeta("source_layout", source.Source.Source.ResourceUri);
            markers.Add(marker);
        }
        for (int i = hexes.Count; i < markers.Count; i++) markers[i].Hide();
        SetProcess(hexes.Count > 0);
        Reproject();
    }

    private void Reproject()
    {
        for (int i = 0; i < hexes.Count; i++)
        {
            Vector2? pixel = hexes[i] is int hex ? project?.Invoke(hex) : null;
            markers[i].Visible = pixel.HasValue;
            if (pixel is Vector2 point) markers[i].Position = point + new Vector2(-25, -70);
        }
    }

    public override void _Process(double delta)
    { if (IsVisibleInTree()) Reproject(); }

    internal void Abandon()
    { project = null; SetProcess(false); Hide(); }
}
