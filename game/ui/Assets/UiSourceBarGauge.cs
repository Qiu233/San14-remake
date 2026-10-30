using Godot;
using San14.Data;

/// <summary>CCmnBarGauge's normal resource, palette and integer fill writer.
/// The owning page loads the resource once and shares it with its actual bars.</summary>
internal partial class UiSourceBarGauge : Control
{
    private MapMarkerLayout bar = null!;
    private UiSourceLayout layout = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapCornerColors? colors;
    private Control? fill;
    private int? width;

    internal void Configure(MapMarkerLayout source, UiAssets assets, Shader shader, int? palette)
    {
        bar = source; layout = new(new(source.Layout, source.Source));
        this.assets = assets; this.shader = shader; colors = palette is int index ? MapSpritePalettes.Create()[index] : null;
        Size = layout.Rectangle(0).Size; MouseFilter = MouseFilterEnum.Ignore;
        layout.Art(this, 1, layout.Rectangle(1), assets, shader);
        SetMeta("source_layout", source.Source.ResourceUri);
        SetMeta("source_fill_palette", palette ?? -1);
    }

    internal void Apply(int current, int capacity)
    {
        var geometry = CityGateMapMarkerSources.CalculateBarGeometry(bar, current, capacity,
            new(0, 0, Size.X, Size.Y), width);
        SetMeta("source_current", current); SetMeta("source_capacity", capacity);
        if (width == geometry.SourceFillWidth) return;
        width = geometry.SourceFillWidth;
        if (fill is not null) { RemoveChild(fill); fill.QueueFree(); fill = null; }
        if (width > 0)
        {
            var r = geometry.Fill;
            fill = layout.Art(this, 2, new(r.Left, r.Top, r.Width, r.Height), assets, shader, colors);
        }
        SetMeta("source_fill_width", width.Value);
    }
}
