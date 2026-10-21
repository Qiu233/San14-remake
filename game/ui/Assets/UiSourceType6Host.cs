using System.Reflection;
using Godot;

/// <summary>Main's finite ordered Canvas owner. It observes actual cut nodes
/// and tree changes, while existing Controls retain all input and lifecycle.
/// Helper viewports own only rendering; map producers are not invoked again.</summary>
internal partial class UiSourceType6Host : Node
{
    private Control root = null!;
    private Action<Exception> failed = null!;
    private UiSourceType6Composition? graph;
    private readonly HashSet<UiSourceType6> observed = [];
    private readonly Dictionary<Type, bool> drawingTypes = [];
    private bool dirty = true, rebuilding, stopped;
    private int revision;
    internal int ActiveCuts => graph?.Cuts.Count ?? 0;
    internal int Revision => revision;
    internal UiSourceType6Composition? Graph => graph;
    internal string[] OrderedCuts { get; private set; } = [];

    internal void Configure(Control owner, Action<Exception> error)
    {
        root = owner; failed = error;
        GetTree().TreeChanged += Invalidate;
        root.Resized += Invalidate;
        RenderingServer.FramePreDraw += BeforeDraw;
    }
    private void Invalidate() { if (!rebuilding && !stopped) dirty = true; }
    private void BeforeDraw()
    {
        if (!dirty || stopped || !root.IsInsideTree()) return;
        rebuilding = true; dirty = false;
        try { Rebuild(); }
        catch (Exception error) { Retire(); failed(error); }
        finally { rebuilding = false; }
    }

    private void Rebuild()
    {
        Retire();
        foreach (var cut in observed)
            if (GodotObject.IsInstanceValid(cut)) cut.Changed -= Invalidate;
        observed.Clear();
        var items = new List<(CanvasItem Item, int Z, int Order)>();
        var producers = new List<SubViewport>();
        int order = 0;
        Walk(root, 0);
        void Walk(Node parent, int parentZ)
        {
            foreach (Node node in parent.GetChildren())
            {
                if (node == this || node.IsQueuedForDeletion()) continue;
                if (node is SubViewport viewport) { producers.Add(viewport); continue; }
                if (node is CanvasLayer) throw new NotSupportedException("Type6 requires an explicitly integrated CanvasLayer boundary.");
                int z = parentZ;
                if (node is CanvasItem item)
                {
                    if (item.TopLevel || item.ShowBehindParent || item is Node2D { YSortEnabled: true })
                        throw new NotSupportedException("Type6 requires a supported ordinary Canvas drawing order.");
                    z = Math.Clamp(item.ZIndex + (item.ZAsRelative ? parentZ : 0), -4096, 4096);
                    items.Add((item, z, order++));
                    if (item is UiSourceType6 cut) { observed.Add(cut); cut.Changed += Invalidate; }
                }
                Walk(node, z);
            }
        }
        var ordered = items.OrderBy(i => i.Z).ThenBy(i => i.Order).ToArray();
        var cuts = ordered.Where(i => i.Item is UiSourceType6 && i.Item.IsVisibleInTree()).ToArray();
        OrderedCuts = cuts.Select(i => i.Item.GetPath().ToString()).ToArray();
        if (cuts.Length == 0) { revision++; return; }

        Vector2 visible = root.GetViewport().GetVisibleRect().Size;
        Vector2I size = new((int)visible.X, (int)visible.Y);
        var segments = new Dictionary<CanvasItem, int>();
        int segment = 0;
        foreach (var row in ordered)
        {
            if (row.Item is UiSourceType6 && row.Item.IsVisibleInTree()) segment++;
            segments.Add(row.Item, segment);
        }
        var masks = segments.ToDictionary(p => p.Key, p => 1u << p.Value);
        foreach (var (item, assignedSegment) in segments)
        {
            uint bit = 1u << assignedSegment;
            for (Node? parent = item.GetParent(); parent is not null && parent != root; parent = parent.GetParent())
                if (parent is CanvasItem ancestor && masks.ContainsKey(ancestor)) masks[ancestor] |= bit;
        }
        foreach (var (item, assignedSegment) in segments)
            if (masks[item] != (1u << assignedSegment) && HasOwnDrawing(item) &&
                item is not IUiSourceDrawing)
                throw new NotSupportedException($"Type6 cannot split an unintegrated drawing ancestor: {item.GetPath()}.");

        graph = new(root, size, cuts.Length);
        foreach (var (item, assignedSegment) in segments) graph.AssignItem(item, assignedSegment, masks[item]);
        foreach (var row in cuts)
        {
            var cut = (UiSourceType6)row.Item;
            var geometry = cut.Geometry(size);
            // This independent Canvas owner has explicitly neutral stock-copy
            // color state. Original retained globals are not read at runtime.
            graph.AddCut(cut.Parameters, geometry.Corners, geometry.Uvs, geometry.Region,
                1, Vector4.One, Vector4.One, cut);
        }
        foreach (var viewport in producers) graph.DependOn(viewport);
        graph.Publish(); revision++;
    }

    private bool HasOwnDrawing(CanvasItem item)
    {
        if (item is UiSourceType6 or UiSourceHitButton) return false;
        if (!drawingTypes.TryGetValue(item.GetType(), out bool draws))
        {
            string native = item.GetClass();
            var method = item.GetType().GetMethod("_Draw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // These actual Main/map ancestors only arrange their children;
            // unlike PanelContainer/ScrollContainer, they draw no native skin.
            draws = native is not ("Control" or "Node2D" or "MarginContainer" or
                "VBoxContainer" or "HBoxContainer" or "AspectRatioContainer") ||
                method?.DeclaringType != typeof(CanvasItem);
            drawingTypes.Add(item.GetType(), draws);
        }
        return draws;
    }

    private void Retire(bool deferRemoval = false) { graph?.Dispose(deferRemoval); graph = null; }
    internal void Stop(bool deferRemoval = false)
    {
        if (stopped) return;
        stopped = true;
        RenderingServer.FramePreDraw -= BeforeDraw;
        if (IsInsideTree()) GetTree().TreeChanged -= Invalidate;
        if (GodotObject.IsInstanceValid(root)) root.Resized -= Invalidate;
        foreach (var cut in observed)
            if (GodotObject.IsInstanceValid(cut)) cut.Changed -= Invalidate;
        observed.Clear(); Retire(deferRemoval);
    }
    public override void _ExitTree() => Stop(deferRemoval: true);
}
