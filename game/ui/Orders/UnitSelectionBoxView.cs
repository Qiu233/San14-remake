using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Only the original node1 artwork. The selector supplies canonical
/// integer endpoints; this view never collects units or handles input.</summary>
internal partial class UnitSelectionBoxView : Control
{
    private sealed record Slice(UiSourceSprite Sprite, int Column, int Row);
    private readonly List<Slice> slices = [];
    private Control? artwork;
    private float[] margins = [];
    private bool ninePart, configured, abandoned;
    private Vector2 backingSize;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader)
    {
        if (configured || abandoned) throw new InvalidOperationException("Selection box is already configured or retired.");
        Name = "UnitSourceSelectionBox";
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(false);
        Hide();
        var loaded = UnitSelectionBoxSource.Load(repository);
        var source = new UiSourceLayout(loaded, new Dictionary<int, UiSourceNodeGeometry>
        {
            // 9328A0 writes node1's actual top-left, backing dimensions and
            // scale. Authored offsets/scales are not added a second time.
            [1] = new(Size: new Vector2(50, 50), Origin: Vector2.Zero, Scale: Vector2.One)
        });
        if (source.Nodes.Count <= 1 || source.Nodes[1].Type is not (1 or 3))
            throw new InvalidDataException("Original selection node1 is not supported sprite artwork.");
        ninePart = source.Nodes[1].Type == 3;
        var sprite = LcskSpriteNodeReader.Read(loaded.Layout, source.Nodes[1]);
        margins = sprite.SourceMargins.ToArray();
        if (ninePart && (margins.Any(v => v < 0) || margins[2] + margins[3] > 50 || margins[0] + margins[1] > 50))
            throw new NotSupportedException("Selection artwork margins exceed the source minimum backing size.");
        // Build every declared slice once, including a centre that is exactly
        // zero at the minimum50 backing but becomes visible on a larger drag.
        backingSize = ninePart
            ? new(Math.Max(50, margins[2] + margins[3] + 1), Math.Max(50, margins[0] + margins[1] + 1))
            : new(50, 50);
        artwork = source.Art(this, 1, new(Vector2.Zero, backingSize), assets, shader);
        var parts = artwork.GetChildren().OfType<UiSourceSprite>().ToArray();
        var (xs, ys) = Axes(backingSize);
        int partIndex = 0;
        foreach (var group in sprite.Groups)
        {
            if (group.ComponentCount == 0) continue;
            int x = ninePart ? group.Index / 3 : 0;
            int y = ninePart ? group.Index % 3 : 0;
            if (xs[x + 1] == xs[x] || ys[y + 1] == ys[y]) continue;
            if (partIndex >= parts.Length)
                throw new InvalidDataException("Selection artwork part geometry is incomplete.");
            slices.Add(new(parts[partIndex++], x, y));
        }
        if (partIndex != parts.Length)
            throw new InvalidDataException("Selection artwork has unsupported projected parts.");
        SetMeta("source_layout", loaded.Source.ResourceUri);
        SetMeta("source_node_index", 1);
        configured = true;
    }

    internal void Apply(Rect2? rectangle)
    {
        if (abandoned) return;
        if (!configured) throw new InvalidOperationException("Selection box is not configured.");
        if (rectangle is not Rect2 value) { Hide(); return; }
        if (!value.Position.IsFinite() || !value.End.IsFinite())
            throw new ArgumentOutOfRangeException(nameof(rectangle));
        // Pointer coordinates are the integer canonical coordinates of the
        // existing input owner. Normalize both endpoints before computing size.
        int x0 = (int)value.Position.X, y0 = (int)value.Position.Y;
        int x1 = (int)value.End.X, y1 = (int)value.End.Y;
        int left = Math.Min(x0, x1), top = Math.Min(y0, y1);
        int width = Math.Max(x0, x1) - left, height = Math.Max(y0, y1) - top;
        Position = new(left, top);
        Size = new(width, height);
        if (width == 0 || height == 0) { Hide(); return; }
        Vector2 backing = new(Math.Max(width, 50), Math.Max(height, 50));
        if (backing != backingSize)
        {
            var (xs, ys) = Axes(backing);
            foreach (var slice in slices)
            {
                int x = slice.Column, y = slice.Row;
                slice.Sprite.Position = new(xs[x], ys[y]);
                slice.Sprite.Size = new(xs[x + 1] - xs[x], ys[y + 1] - ys[y]);
            }
            artwork!.Size = backing;
            backingSize = backing;
        }
        artwork!.Scale = new(width < 51 ? width / 50f : 1f,
            height < 51 ? height / 50f : 1f);
        Show();
    }

    private (float[] X, float[] Y) Axes(Vector2 size)
        => ninePart
            ? ([0, margins[2], size.X - margins[3], size.X],
               [0, margins[0], size.Y - margins[1], size.Y])
            : ([0, size.X], [0, size.Y]);

    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true;
        Hide();
        // Children own their shader materials; assets and shader are borrowed.
        QueueFree();
    }
}
