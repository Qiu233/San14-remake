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
    private readonly HashSet<Node> pendingChildOrders = [];
    private readonly List<Node> treeNodes = [];
    // Rebuilds keep their current eligibility/order checks. Only their
    // bounded work storage belongs to the host, instead of to each draw.
    private readonly HashSet<Node> currentNodes = [];
    private readonly List<(CanvasItem Item, CanvasItem? Parent, int Z, int Order)> items = [];
    private readonly List<(CanvasItem Item, CanvasItem? Parent, int Z, int Order)> ordered = [];
    private readonly List<(CanvasItem Item, CanvasItem? Parent, int Z, int Order)> cuts = [];
    private readonly List<SubViewport> producers = [];
    private readonly Dictionary<CanvasItem, int> effectiveZ = [];
    private const int MinimumCanvasZ = -4096, MaximumCanvasZ = 4096;
    private readonly int[] zPositions = new int[MaximumCanvasZ - MinimumCanvasZ + 1];
    private readonly HashSet<CanvasItem> activeCuts = [];
    private readonly Dictionary<CanvasItem, int> segments = [];
    private readonly Dictionary<CanvasItem, uint> masks = [];
    private bool structureDirty = true, hierarchyMembershipDirty = true;
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
        hierarchyMembershipDirty = true; structureDirty = true; dirty = true;
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
        AttachOrderObserver(node);
    }
    private void AttachOrderObserver(Node node)
    {
        // A captured callback is created only for a new observer. Keeping
        // its scope here avoids a closure on every existing-node visit.
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
        if (rebuilding)
        {
            // Preserve immediate bookkeeping for helper mutations during
            // composition publication; these never invalidate that rebuild.
            var children = Children(node);
            if (childOrder.TryGetValue(node, out var previous) && previous.SequenceEqual(children)) return;
            childOrder[node] = children;
            return;
        }
        if (pendingChildOrders.Contains(node)) return;
        if (childOrder.TryGetValue(node, out var retained) && HasSameChildren(node, retained)) return;
        // MoveChild updates the real input tree immediately. The compositor
        // reads it only at PreDraw, after an owner's whole reorder is done.
        pendingChildOrders.Add(node);
        structureDirty = true; dirty = true;
    }
    private static bool HasSameChildren(Node node, Node[] retained)
    {
        if (node.GetChildCount() != retained.Length) return false;
        for (int i = 0; i < retained.Length; i++)
            if (!EqualityComparer<Node>.Default.Equals(retained[i], node.GetChild(i))) return false;
        return true;
    }
    private void CapturePendingChildOrders()
    {
        foreach (var node in pendingChildOrders)
        {
            if (!orderedNodes.Contains(node) || !GodotObject.IsInstanceValid(node))
            { hierarchyMembershipDirty = true; continue; }
            var children = Children(node);
            if (!hierarchyMembershipDirty &&
                (!childOrder.TryGetValue(node, out var previous) || !HasSameMembership(previous, children)))
                hierarchyMembershipDirty = true;
            childOrder[node] = children;
        }
        pendingChildOrders.Clear();
    }
    private bool HasSameMembership(Node[] previous, Node[] children)
    {
        if (previous.Length != children.Length) return false;
        // This rebuild has not begun its membership walk. Reuse its bounded
        // scratch set only for the actual parents whose order changed.
        try
        {
            currentNodes.EnsureCapacity(previous.Length);
            currentNodes.UnionWith(previous);
            foreach (var child in children)
                if (!currentNodes.Remove(child)) return false;
            return currentNodes.Count == 0;
        }
        finally { currentNodes.Clear(); }
    }
    private void ReleaseOrderObserver(Node node)
    {
        if (GodotObject.IsInstanceValid(node))
        { node.ChildOrderChanged -= orderCallbacks[node]; node.Renamed -= Invalidate; }
        pendingChildOrders.Remove(node);
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
        catch (Exception error)
        {
            Exception failure = error;
            try { Retire(); }
            catch (Exception retirementError)
            {
                failure = new AggregateException("UI composition rebuild and retirement both failed.",
                    error, retirementError);
            }
            failed(failure);
        }
        finally { ClearWorklists(); rebuilding = false; }
    }

    private void Rebuild()
    {
        CapturePendingChildOrders();
        if (structureDirty)
        {
            if (hierarchyMembershipDirty)
            {
                foreach (var cut in observed)
                    if (GodotObject.IsInstanceValid(cut)) { cut.Changed -= Invalidate; cut.WeightChanged -= WeightChanged; }
                observed.Clear();
                foreach (var effect in observedEffects)
                    if (GodotObject.IsInstanceValid(effect)) effect.CanvasCutChanged -= Invalidate;
                observedEffects.Clear();
                currentNodes.EnsureCapacity(orderedNodes.Count); treeNodes.Clear(); Observe(root);
                Walk(root);
                foreach (var node in orderedNodes.Where(node => !currentNodes.Contains(node)).ToArray()) ReleaseOrderObserver(node);
                void Observe(Node node) { currentNodes.Add(node); ObserveOrder(node); }
                void Walk(Node parent)
                {
                    // ObserveOrder captured new parents; ChildOrderChanged keeps
                    // existing parents current before this draw boundary.
                    var children = childOrder[parent];
                    for (int index = 0; index < children.Length; index++)
                    {
                        Node node = children[index];
                        if (node == this || node.IsQueuedForDeletion() || graph?.OwnsStructuralNode(node) == true) continue;
                        if (node is CanvasLayer) throw new NotSupportedException("Type6 requires an explicitly integrated CanvasLayer boundary.");
                        treeNodes.Add(node);
                        Observe(node);
                        if (node is SubViewport) continue;
                        if (node is UiSourceType6 cut) { observed.Add(cut); cut.Changed += Invalidate; cut.WeightChanged += WeightChanged; }
                        if (node is UniqueTacticEffectSurface effect)
                        {
                            observedEffects.Add(effect); effect.CanvasCutChanged += Invalidate;
                            // Its internal image belongs to this one ordered cut.
                            continue;
                        }
                        Walk(node);
                    }
                }
            }
            // Pure child reordering keeps this membership and its observers.
            // The live walk below consumes the final child snapshots directly,
            // instead of flattening every unchanged subtree before scanning it.
            structureDirty = false; hierarchyMembershipDirty = false;
        }
        // The retained hierarchy bounds these local worklists; capacity does
        // not change current eligibility, preorder or Canvas ordering.
        items.EnsureCapacity(treeNodes.Count); ordered.EnsureCapacity(treeNodes.Count);
        effectiveZ.EnsureCapacity(treeNodes.Count);
        segments.EnsureCapacity(treeNodes.Count); masks.EnsureCapacity(treeNodes.Count);
        Collect(root, null);
        void Collect(Node parent, CanvasItem? canvasParent)
        {
            foreach (var node in childOrder[parent])
            {
                // Helpers are outside the admitted source tree. A queued
                // parent excludes its whole subtree, including non-Canvas nodes.
                if (!orderedNodes.Contains(node) || node.IsQueuedForDeletion()) continue;
                if (node is SubViewport viewport) { producers.Add(viewport); continue; }
                CanvasItem? nextCanvasParent = canvasParent;
                if (node is CanvasItem item)
                {
                    // Read the same current properties on every admitted build;
                    // an order signal does not make unsignalled Z changes static.
                    if (item.TopLevel || item.ShowBehindParent || item is Node2D { YSortEnabled: true })
                        throw new NotSupportedException("Type6 requires a supported ordinary Canvas drawing order.");
                    int parentZ = canvasParent is { } ancestor ? effectiveZ[ancestor] : 0;
                    int z = Math.Clamp(item.ZIndex + (item.ZAsRelative ? parentZ : 0), MinimumCanvasZ, MaximumCanvasZ);
                    effectiveZ.Add(item, z); items.Add((item, canvasParent, z, items.Count));
                    nextCanvasParent = item;
                }
                if (node is not UniqueTacticEffectSurface) Collect(node, nextCanvasParent);
            }
        }
        ordered.AddRange(items);
        if (items.Count < zPositions.Length)
        {
            // Small canvases retain the comparison sort without a range sweep.
            ordered.Sort(static (left, right) =>
            {
                int z = left.Z.CompareTo(right.Z);
                return z != 0 ? z : left.Order.CompareTo(right.Order);
            });
        }
        else
        {
            // Canvas Z is already clamped to this finite engine range. A stable
            // range sort retains the same Z/preorder total order for large trees.
            Array.Clear(zPositions);
            foreach (var row in items) zPositions[row.Z - MinimumCanvasZ]++;
            int position = 0;
            for (int z = 0; z < zPositions.Length; z++)
            {
                int count = zPositions[z]; zPositions[z] = position; position += count;
            }
            foreach (var row in items) ordered[zPositions[row.Z - MinimumCanvasZ]++] = row;
        }
        // Most items are text/artwork. Their current visibility does not
        // alter a cut boundary; ask native visibility only for actual cuts.
        foreach (var row in ordered)
            if ((row.Item is UiSourceType6 || row.Item is UniqueTacticEffectSurface { CanvasCutEnabled: true }) &&
                row.Item.IsVisibleInTree()) cuts.Add(row);
        OrderedCuts = cuts.Select(i => i.Item.GetPath().ToString()).ToArray();
        if (cuts.Count == 0) { Retire(deferRemoval: true); revision++; return; }

        Vector2 visible = root.GetViewport().GetVisibleRect().Size;
        Vector2I size = new((int)visible.X, (int)visible.Y);
        var cutStates = cuts.Select(row => row.Item is UiSourceType6 cut
            ? new CutState(cut, cut.Parameters, cut.Geometry(size)) : new CutState(row.Item, null, null)).ToArray();
        foreach (var row in cuts) activeCuts.Add(row.Item);
        int segment = 0;
        foreach (var row in ordered)
        {
            if (activeCuts.Contains(row.Item)) segment++;
            segments.Add(row.Item, segment);
        }
        foreach (var (item, assignedSegment) in segments) masks.Add(item, 1u << assignedSegment);
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
        graph = new(root, size, cuts.Count, shaders!,
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

    private void ClearWorklists()
    {
        // Composition rebinds copy memberships; they retain none of these
        // collections. Release temporary node references even on failure.
        currentNodes.Clear(); items.Clear(); ordered.Clear(); cuts.Clear();
        producers.Clear(); effectiveZ.Clear(); activeCuts.Clear(); segments.Clear(); masks.Clear();
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

    private void Retire(bool deferRemoval = false)
    {
        var retired = graph;
        graph = null; graphCuts = []; parameterIndices.Clear();
        retired?.Dispose(deferRemoval);
    }
    internal void Stop(bool deferRemoval = false)
    {
        if (stopped) return;
        stopped = true;
        RenderingServer.FramePreDraw -= BeforeDraw;
        if (IsInsideTree())
        { GetTree().NodeAdded -= NodeAdded; GetTree().NodeRemoved -= NodeRemoved; }
        ReleaseOrderObservers(); pendingChildOrders.Clear(); treeNodes.Clear();
        if (GodotObject.IsInstanceValid(root)) root.Resized -= Invalidate;
        foreach (var cut in observed)
            if (GodotObject.IsInstanceValid(cut)) { cut.Changed -= Invalidate; cut.WeightChanged -= WeightChanged; }
        observed.Clear();
        foreach (var effect in observedEffects)
            if (GodotObject.IsInstanceValid(effect)) effect.CanvasCutChanged -= Invalidate;
        observedEffects.Clear(); Retire(deferRemoval);
        ClearWorklists();
        shaders?.Dispose(); shaders = null;
    }
    public override void _ExitTree() => Stop(deferRemoval: true);
}
