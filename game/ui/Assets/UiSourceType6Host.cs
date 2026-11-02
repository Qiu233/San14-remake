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
    private UiSourceType6Shaders? shaders;
    private readonly HashSet<UiSourceType6> observed = [];
    private readonly Dictionary<UiSourceType6, int> parameterIndices = [];
    private readonly HashSet<UniqueTacticEffectSurface> observedEffects = [];
    private readonly Dictionary<Type, bool> drawingTypes = [];
    // Cache only this Main's actual hierarchy. Every admitted rebuild still
    // reads current deletion eligibility and supported Canvas ordering fields.
    private readonly HashSet<Node> orderedNodes = [];
    private readonly Dictionary<Node, Action> orderCallbacks = [];
    private readonly Dictionary<Node, Node[]> childOrder = [];
    private readonly List<(Node Node, int Parent, CanvasItem? CanvasParent)> treeNodes = [];
    private bool structureDirty = true;
    private bool dirty = true, rebuilding, stopped;
    private int revision;
    private Vector2I graphSize;
    private CutState[] graphCuts = [];
    private sealed record CutState(CanvasItem Item, UiSourceType6Parameters? Parameters, UiSourceType6Geometry? Geometry)
    {
        internal bool Matches(CutState other) => ReferenceEquals(Item, other.Item) && Parameters == other.Parameters &&
            (Geometry is null ? other.Geometry is null : other.Geometry is { } next &&
                Geometry.Region == next.Region && Geometry.Corners.SequenceEqual(next.Corners) && Geometry.Uvs.SequenceEqual(next.Uvs));
    }
    internal int ActiveCuts => graph?.CutCount ?? 0;
    internal int Revision => revision;
    internal UiSourceType6Composition? Graph => graph;
    internal string[] OrderedCuts { get; private set; } = [];

    internal void Configure(Control owner, Action<Exception> error)
    {
        root = owner; failed = error;
        shaders = new UiSourceType6Shaders();
        GetTree().NodeAdded += NodeAdded;
        GetTree().NodeRemoved += NodeRemoved;
        root.Resized += Invalidate;
        RenderingServer.FramePreDraw += BeforeDraw;
    }
    private void Invalidate() { if (!rebuilding && !stopped) dirty = true; }
    private void InvalidateStructure()
    {
        if (rebuilding || stopped) return;
        structureDirty = true; dirty = true;
    }
    private void NodeAdded(Node node)
    {
        if (!rebuilding && !stopped && node != this &&
            node.GetParent() is { } parent && parent is not (SubViewport or UniqueTacticEffectSurface) &&
            orderedNodes.Contains(parent)) InvalidateStructure();
    }
    private void NodeRemoved(Node node)
    {
        if (!rebuilding && !stopped && orderedNodes.Contains(node)) InvalidateStructure();
    }
    private void ObserveOrder(Node node)
    {
        if (!orderedNodes.Add(node)) return;
        Action changed = () => NodeOrderChanged(node);
        orderCallbacks.Add(node, changed); childOrder.Add(node, Children(node));
        node.ChildOrderChanged += changed;
        node.Renamed += Invalidate;
    }
    private static Node[] Children(Node parent)
    {
        int count = parent.GetChildCount();
        if (count == 0) return Array.Empty<Node>();
        var children = new Node[count];
        for (int index = 0; index < count; index++) children[index] = parent.GetChild(index);
        return children;
    }
    private void NodeOrderChanged(Node node)
    {
        if (stopped || node is SubViewport or UniqueTacticEffectSurface) return;
        var children = Children(node);
        if (childOrder.TryGetValue(node, out var previous) && previous.SequenceEqual(children)) return;
        // Godot may reorder internal children that the source walk never
        // included. Only this parent's actual exposed child order is relevant.
        childOrder[node] = children;
        if (!rebuilding) InvalidateStructure();
    }
    private void ReleaseOrderObserver(Node node)
    {
        if (GodotObject.IsInstanceValid(node))
        { node.ChildOrderChanged -= orderCallbacks[node]; node.Renamed -= Invalidate; }
        orderedNodes.Remove(node); orderCallbacks.Remove(node); childOrder.Remove(node);
    }
    private void ReleaseOrderObservers()
    {
        foreach (var node in orderedNodes.ToArray()) ReleaseOrderObserver(node);
    }

    private void WeightChanged(UiSourceType6 owner)
    {
        if (stopped || graph is null) return;
        graph.SetWeight(owner);
        if (parameterIndices.TryGetValue(owner, out int index))
            graphCuts[index] = graphCuts[index] with { Parameters = owner.Parameters };
    }
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
        if (structureDirty)
        {
            foreach (var cut in observed)
                if (GodotObject.IsInstanceValid(cut)) { cut.Changed -= Invalidate; cut.WeightChanged -= WeightChanged; }
            observed.Clear();
            foreach (var effect in observedEffects)
                if (GodotObject.IsInstanceValid(effect)) effect.CanvasCutChanged -= Invalidate;
            observedEffects.Clear();
            var currentNodes = new HashSet<Node>(); treeNodes.Clear(); Observe(root);
            Walk(root, -1, null);
            foreach (var node in orderedNodes.Where(node => !currentNodes.Contains(node)).ToArray()) ReleaseOrderObserver(node);
            structureDirty = false;
            void Observe(Node node) { currentNodes.Add(node); ObserveOrder(node); }
            void Walk(Node parent, int parentIndex, CanvasItem? canvasParent)
            {
                // ObserveOrder captured new parents; ChildOrderChanged keeps
                // existing parents current before this draw boundary.
                var children = childOrder[parent];
                for (int index = 0; index < children.Length; index++)
                {
                    Node node = children[index];
                    if (node == this || node.IsQueuedForDeletion() || graph?.OwnsStructuralNode(node) == true) continue;
                    if (node is CanvasLayer) throw new NotSupportedException("Type6 requires an explicitly integrated CanvasLayer boundary.");
                    int entry = treeNodes.Count; treeNodes.Add((node, parentIndex, canvasParent));
                    Observe(node);
                    if (node is SubViewport) continue;
                    if (node is UiSourceType6 cut) { observed.Add(cut); cut.Changed += Invalidate; cut.WeightChanged += WeightChanged; }
                    if (node is UniqueTacticEffectSurface effect)
                    {
                        observedEffects.Add(effect); effect.CanvasCutChanged += Invalidate;
                        // Its internal image belongs to this one ordered cut.
                        continue;
                    }
                    Walk(node, entry, node as CanvasItem ?? canvasParent);
                }
            }
        }
        var items = new List<(CanvasItem Item, CanvasItem? Parent, int Z, int Order)>();
        var producers = new List<SubViewport>();
        var effectiveZ = new Dictionary<CanvasItem, int>();
        var excluded = new bool[treeNodes.Count];
        foreach (var (entry, index) in treeNodes.Select((entry, index) => (entry, index)))
        {
            // A queued parent may not have left the tree yet. Match the old
            // walk's whole-subtree exclusion, including non-Canvas parents.
            if ((entry.Parent >= 0 && excluded[entry.Parent]) || entry.Node.IsQueuedForDeletion())
            { excluded[index] = true; continue; }
            if (entry.Node is SubViewport viewport) { producers.Add(viewport); continue; }
            if (entry.Node is not CanvasItem item) continue;
            if (item.TopLevel || item.ShowBehindParent || item is Node2D { YSortEnabled: true })
                throw new NotSupportedException("Type6 requires a supported ordinary Canvas drawing order.");
            int parentZ = entry.CanvasParent is { } parent ? effectiveZ[parent] : 0;
            int z = Math.Clamp(item.ZIndex + (item.ZAsRelative ? parentZ : 0), -4096, 4096);
            effectiveZ.Add(item, z); items.Add((item, entry.CanvasParent, z, items.Count));
        }
        var ordered = items.OrderBy(i => i.Z).ThenBy(i => i.Order).ToArray();
        // Most items are text/artwork. Their current visibility does not
        // alter a cut boundary; ask native visibility only for actual cuts.
        var cuts = ordered.Where(i =>
            (i.Item is UiSourceType6 || i.Item is UniqueTacticEffectSurface { CanvasCutEnabled: true }) &&
            i.Item.IsVisibleInTree()).ToArray();
        OrderedCuts = cuts.Select(i => i.Item.GetPath().ToString()).ToArray();
        if (cuts.Length == 0) { Retire(deferRemoval: true); revision++; return; }

        Vector2 visible = root.GetViewport().GetVisibleRect().Size;
        Vector2I size = new((int)visible.X, (int)visible.Y);
        var cutStates = cuts.Select(row => row.Item is UiSourceType6 cut
            ? new CutState(cut, cut.Parameters, cut.Geometry(size)) : new CutState(row.Item, null, null)).ToArray();
        var activeCuts = cuts.Select(row => row.Item).ToHashSet();
        var segments = new Dictionary<CanvasItem, int>();
        int segment = 0;
        foreach (var row in ordered)
        {
            if (activeCuts.Contains(row.Item)) segment++;
            segments.Add(row.Item, segment);
        }
        var masks = segments.ToDictionary(p => p.Key, p => 1u << p.Value);
        // Preorder visits a Canvas ancestor before its descendants. Propagate
        // each live subtree mask once instead of querying every parent chain.
        for (int index = items.Count - 1; index >= 0; index--)
        {
            var row = items[index];
            if (row.Parent is { } parent) masks[parent] |= masks[row.Item];
        }
        foreach (var (item, assignedSegment) in segments)
            if (masks[item] != (1u << assignedSegment) && HasOwnDrawing(item) &&
                item is not IUiSourceDrawing)
                throw new NotSupportedException($"Type6 cannot split an unintegrated drawing ancestor: {item.GetPath()}.");

        if (graph is not null && graphSize == size && graphCuts.Length == cutStates.Length &&
            graphCuts.Zip(cutStates).All(pair => pair.First.Matches(pair.Second)))
        {
            // A portrait/list subtree change does not change the ordered cut
            // resources. Keep their live targets and bind only the current tree.
            graph.RebindItems(segments, masks);
            graph.RebindDependencies(producers);
            return;
        }

        if (graph is not null && graphSize == size && !graph.HasEffectCuts &&
            cutStates.All(state => state.Item is UiSourceType6))
        {
            graph.Reconfigure(cutStates.Select(state => new UiSourceType6Draw(
                (UiSourceType6)state.Item, state.Parameters!, state.Geometry!)).ToArray());
            graph.RebindItems(segments, masks);
            graph.RebindDependencies(producers);
            graphCuts = cutStates; revision++;
            parameterIndices.Clear();
            for (int i = 0; i < cutStates.Length; i++) parameterIndices.Add((UiSourceType6)cutStates[i].Item, i);
            return;
        }

        // Disable old targets now and let Godot retire their nodes after
        // this frame, rather than freeing the viewport tree in FramePreDraw.
        Retire(deferRemoval: true);
        // Ordinary cuts can extend a current-frame encoded prefix instead
        // of redrawing every earlier source segment in each later target.
        // Effect-owned cuts retain their established cumulative producer path.
        graph = new(root, size, cuts.Length, shaders!,
            incrementalPrefixes: cuts.All(row => row.Item is UiSourceType6));
        graph.RebindItems(segments, masks);
        foreach (var state in cutStates)
        {
            if (state.Item is UniqueTacticEffectSurface effect)
            { graph.AddEffectCut(effect); continue; }
            var cut = (UiSourceType6)state.Item;
            var geometry = state.Geometry!;
            // This independent Canvas owner has explicitly neutral stock-copy
            // color state. Original retained globals are not read at runtime.
            graph.AddCut(cut.Parameters, geometry.Corners, geometry.Uvs, geometry.Region,
                1, Vector4.One, Vector4.One, cut);
        }
        graph.RebindDependencies(producers);
        graph.Publish(); graphCuts = cutStates; graphSize = size; revision++;
        parameterIndices.Clear();
        for (int i = 0; i < cutStates.Length; i++)
            if (cutStates[i].Item is UiSourceType6 owner) parameterIndices.Add(owner, i);
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

    private void Retire(bool deferRemoval = false) { graph?.Dispose(deferRemoval); graph = null; graphCuts = []; parameterIndices.Clear(); }
    internal void Stop(bool deferRemoval = false)
    {
        if (stopped) return;
        stopped = true;
        RenderingServer.FramePreDraw -= BeforeDraw;
        if (IsInsideTree())
        { GetTree().NodeAdded -= NodeAdded; GetTree().NodeRemoved -= NodeRemoved; }
        ReleaseOrderObservers(); treeNodes.Clear();
        if (GodotObject.IsInstanceValid(root)) root.Resized -= Invalidate;
        foreach (var cut in observed)
            if (GodotObject.IsInstanceValid(cut)) { cut.Changed -= Invalidate; cut.WeightChanged -= WeightChanged; }
        observed.Clear();
        foreach (var effect in observedEffects)
            if (GodotObject.IsInstanceValid(effect)) effect.CanvasCutChanged -= Invalidate;
        observedEffects.Clear(); Retire(deferRemoval);
        shaders?.Dispose(); shaders = null;
    }
    public override void _ExitTree() => Stop(deferRemoval: true);
}
