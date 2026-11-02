using Godot;

internal readonly record struct UiSourceType6Draw(UiSourceType6 Owner,
    UiSourceType6Parameters Parameters, UiSourceType6Geometry Geometry);

/// <summary>Finite prefix graph for a structural UI canvas. Controls remain
/// in their original input tree. The caller supplies each real draw boundary;
/// this owner does not infer it from a final screenshot or replay UI callbacks.</summary>
internal sealed class UiSourceType6Composition : IDisposable
{
    private const uint OutputLayer = 1u << 19;
    private readonly Control canvas;
    private readonly Viewport input;
    private readonly uint previousCull, previousCanvasLayer;
    private readonly List<SubViewport> prefixes;
    private readonly List<UiSourceType6Pass> cuts = [];
    private readonly HashSet<Node> structuralNodes = [];
    private readonly List<UniqueTacticEffectSurface> effectCuts = [];
    private int attachedCuts;
    private readonly Dictionary<UiSourceType6, UiSourceType6Pass> animatedCuts = [];
    private readonly Dictionary<CanvasItem, (uint Previous, int Segment)> assigned = [];
    private readonly Dictionary<SubViewport, Rid> dependencies = [];
    private UiSourceType6Quad? output;
    private readonly UiSourceType6Shaders shaders;
    private ShaderMaterial? outputMaterial;
    private bool disposed;
    private readonly bool incrementalPrefixes;
    private sealed record PrefixCopy(CanvasLayer Canvas, UiSourceType6Quad Quad, ShaderMaterial Material);
    private readonly List<PrefixCopy> prefixCopies = [];
    internal IReadOnlyList<SubViewport> Prefixes => prefixes;
    internal IReadOnlyList<UiSourceType6Pass> Cuts => cuts;
    internal int CutCount => attachedCuts;
    internal bool HasEffectCuts => effectCuts.Count != 0;
    internal bool OwnsStructuralNode(Node node) => structuralNodes.Contains(node);

    internal UiSourceType6Composition(Control structuralCanvas, Vector2I size, int cutCount,
        UiSourceType6Shaders shaders, bool incrementalPrefixes = false)
    {
        if (cutCount < 1 || cutCount > 18 || size.X < 4 || size.Y < 4)
            throw new ArgumentOutOfRangeException(nameof(cutCount));
        this.shaders = shaders; this.incrementalPrefixes = incrementalPrefixes;
        canvas = structuralCanvas; input = canvas.GetViewport();
        if (canvas.GetParent() is not Viewport)
            throw new NotSupportedException("The private prefix owner currently requires a direct viewport canvas root so output ancestors cannot cull it.");
        if (!canvas.GetGlobalTransformWithCanvas().IsEqualApprox(Transform2D.Identity))
            throw new NotSupportedException("The private prefix owner currently requires an identity structural canvas transform.");
        previousCull = input.CanvasCullMask; previousCanvasLayer = canvas.VisibilityLayer;
        canvas.VisibilityLayer = OutputLayer | ((1u << (cutCount + 1)) - 1);
        prefixes = Enumerable.Range(0, cutCount + 1).Select(i => new SubViewport
        {
            Name = "UiPrefix" + i, Size = size, Disable3D = true, GuiDisableInput = true,
            TransparentBg = true, UseHdr2D = false, World2D = input.FindWorld2D(),
            CanvasCullMask = incrementalPrefixes ? 1u << i : (1u << (i + 1)) - 1,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        }).ToList();
        structuralNodes.UnionWith(prefixes);
        foreach (var prefix in prefixes) canvas.AddChild(prefix);
        if (incrementalPrefixes)
            for (int index = 1; index < prefixes.Count; index++) AddPrefixCopy(index);
    }

    private void AddPrefixCopy(int index)
    {
        var target = prefixes[index];
        Vector2 size = target.Size;
        var material = new ShaderMaterial { Shader = shaders.Output };
        material.SetShaderParameter("image", prefixes[index - 1].GetTexture());
        // A viewport-local canvas precedes this prefix's ordinary World2D.
        // It copies current encoded RGBA with blend_disabled, independently
        // of the source controls' transforms, clips and inherited Modulate.
        // The predecessor is already a child dependency of this consumer.
        var local = new CanvasLayer { Name = "UiPrefixPrevious" + index, Layer = -1,
            CustomViewport = target, FollowViewportEnabled = false };
        var quad = new UiSourceType6Quad { Name = "PreviousPrefixEncodedRgba", Size = size,
            Positions = [Vector2.Zero, new(size.X, 0), new(0, size.Y), size],
            Uvs = [Vector2.Zero, Vector2.Right, Vector2.Down, Vector2.One],
            Material = material, VisibilityLayer = 1u << index, MouseFilter = Control.MouseFilterEnum.Ignore };
        local.AddChild(quad); target.AddChild(local);
        prefixCopies.Add(new(local, quad, material));
        structuralNodes.Add(local); structuralNodes.Add(quad);
    }

    private void RetirePrefixCopy(int index)
    {
        var copy = prefixCopies[index];
        if (GodotObject.IsInstanceValid(copy.Quad)) { copy.Quad.Hide(); copy.Quad.Material = null; }
        copy.Material.Dispose(); prefixCopies.RemoveAt(index);
        // The CanvasLayer remains an owned child of the retiring prefix.
    }

    internal void AssignSegment(CanvasItem subtree, int segment)
    {
        if ((uint)segment >= prefixes.Count) throw new ArgumentOutOfRangeException(nameof(segment));
        Visit(subtree);
        void Visit(Node node)
        {
            if (node is CanvasItem item)
            {
                uint layer = 1u << segment;
                AssignItem(item, segment, layer);
            }
            foreach (Node child in node.GetChildren()) Visit(child);
        }
    }

    internal void AssignItem(CanvasItem item, int segment, uint structuralMask)
    {
        if ((uint)segment >= prefixes.Count) throw new ArgumentOutOfRangeException(nameof(segment));
        bool existing = assigned.TryGetValue(item, out var previous);
        if (!existing || previous.Segment != segment)
            assigned[item] = (existing ? previous.Previous : item.VisibilityLayer, segment);
        if (item.VisibilityLayer != structuralMask) item.VisibilityLayer = structuralMask;
        uint drawingLayer = 1u << segment;
        if ((!existing || previous.Segment != segment) && item is IUiSourceDrawing drawing)
            drawing.SetDrawingVisibilityLayer(drawingLayer);
    }

    internal void RebindItems(IReadOnlyDictionary<CanvasItem, int> segments, IReadOnlyDictionary<CanvasItem, uint> masks)
    {
        foreach (var item in assigned.Keys.Where(item => !segments.ContainsKey(item)).ToArray())
        {
            RestoreItem(item, assigned[item].Previous);
            assigned.Remove(item);
        }
        foreach (var (item, segment) in segments) AssignItem(item, segment, masks[item]);
    }

    private static void RestoreItem(CanvasItem item, uint previous)
    {
        if (!GodotObject.IsInstanceValid(item) || item.IsQueuedForDeletion()) return;
        item.VisibilityLayer = previous;
        // Exited controls have released their borrowed font/RIDs. Rebinding
        // must not create a new drawing owner after that exit sequence.
        if (item.IsInsideTree() && item is IUiSourceDrawing drawing) drawing.SetDrawingVisibilityLayer(previous);
    }

    internal void DependOn(SubViewport producer)
    {
        var parent = producer.GetParent().GetViewport();
        Rid originalParent = parent.GetViewportRid();
        if (!dependencies.TryGetValue(producer, out var previousParent) || previousParent != originalParent)
        {
            dependencies[producer] = originalParent;
            RenderingServer.ViewportSetParentViewport(producer.GetViewportRid(), prefixes[0].GetViewportRid());
        }
    }

    internal void RebindDependencies(IReadOnlyCollection<SubViewport> producers)
    {
        var current = producers.ToHashSet();
        foreach (var producer in dependencies.Keys.Where(view => !current.Contains(view)).ToArray())
        {
            if (GodotObject.IsInstanceValid(producer) && producer.IsInsideTree())
                RenderingServer.ViewportSetParentViewport(producer.GetViewportRid(), producer.GetParent().GetViewport().GetViewportRid());
            dependencies.Remove(producer);
        }
        foreach (var producer in producers) DependOn(producer);
    }

    internal void AddCut(UiSourceType6Parameters parameters, Vector2[] screenQuad,
        Vector2[] sourceUvs, Rect2I region, float alpha, Vector4 captureColor, Vector4 prefillColor,
        Control? drawingParent = null)
    {
        if (disposed || output is not null || attachedCuts == prefixes.Count - 1)
            throw new InvalidOperationException("Type6 graph is sealed or has no remaining cut.");
        int index = attachedCuts++;
        var pass = new UiSourceType6Pass(prefixes[index + 1], prefixes[index], drawingParent ?? canvas, parameters, screenQuad,
            sourceUvs, region, prefixes[index].Size, 1u << (index + 1), alpha, captureColor, prefillColor, shaders);
        cuts.Add(pass); structuralNodes.Add(pass.Vertical);
        if (drawingParent is UiSourceType6 owner) animatedCuts.Add(owner, pass);
    }

    internal void SetWeight(UiSourceType6 owner)
    {
        if (!disposed && animatedCuts.TryGetValue(owner, out var pass)) pass.SetWeight(owner.Parameters);
    }

    internal void AddEffectCut(UniqueTacticEffectSurface surface)
    {
        if (disposed || output is not null || attachedCuts == prefixes.Count - 1)
            throw new InvalidOperationException("UI prefix graph is sealed or has no remaining cut.");
        int index = attachedCuts++;
        // This is the final pane's preceding Canvas, not the native effect's
        // independent offscreen target.
        prefixes[index].Reparent(prefixes[index + 1]);
        effectCuts.Add(surface);
        surface.AttachCanvasPrefix(prefixes[index], prefixes[index + 1], 1u << (index + 1));
    }

    internal void Publish()
    {
        if (disposed || output is not null || attachedCuts != prefixes.Count - 1)
            throw new InvalidOperationException("Type6 graph requires all actual cuts before publication.");
        outputMaterial = new() { Shader = shaders.Output };
        outputMaterial.SetShaderParameter("image", prefixes[^1].GetTexture());
        Vector2 size = (Vector2)prefixes[^1].Size;
        output = new UiSourceType6Quad { Name = "UiComposedOutput", Size = size,
            Positions = [Vector2.Zero, new(size.X, 0), new(0, size.Y), size],
            Uvs = [Vector2.Zero, Vector2.Right, Vector2.Down, Vector2.One], Material = outputMaterial,
            MouseFilter = Control.MouseFilterEnum.Ignore, VisibilityLayer = OutputLayer };
        structuralNodes.Add(output);
        canvas.AddChild(output);
        input.CanvasCullMask = OutputLayer;
    }

    // This graph keeps exactly the current active targets. No hidden page or
    // historical graph is retained. Size/effect changes use the full rebuild.
    internal void Reconfigure(IReadOnlyList<UiSourceType6Draw> draws)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (output is null || HasEffectCuts || cuts.Count != attachedCuts)
            throw new InvalidOperationException("Only a published ordinary Type6 graph can reuse its targets.");
        if (draws.Count < 1 || draws.Count > 18) throw new ArgumentOutOfRangeException(nameof(draws));
        int oldCount = attachedCuts;
        if (draws.Count < oldCount)
        {
            // Detach the retained producer chain before retiring its former
            // consumers. Otherwise freeing the tail would also free it.
            prefixes[draws.Count].Reparent(canvas);
            for (int index = oldCount - 1; index >= draws.Count; index--) cuts[index].Dispose(deferRemoval: true);
            cuts.RemoveRange(draws.Count, oldCount - draws.Count);
            for (int index = prefixes.Count - 1; index > draws.Count; index--)
            {
                var prefix = prefixes[index];
                prefix.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                if (incrementalPrefixes) RetirePrefixCopy(index - 1);
                prefix.QueueFree(); prefixes.RemoveAt(index);
            }
        }
        Vector2I size = prefixes[0].Size;
        while (prefixes.Count < draws.Count + 1)
        {
            int index = prefixes.Count;
            var prefix = new SubViewport { Name = "UiPrefix" + index, Size = size,
                Disable3D = true, GuiDisableInput = true, TransparentBg = true, UseHdr2D = false,
                World2D = input.FindWorld2D(), CanvasCullMask = incrementalPrefixes ? 1u << index : (1u << (index + 1)) - 1,
                RenderTargetClearMode = SubViewport.ClearMode.Always,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            prefixes.Add(prefix); canvas.AddChild(prefix);
            if (incrementalPrefixes) AddPrefixCopy(index);
        }
        animatedCuts.Clear();
        for (int index = 0; index < draws.Count; index++)
        {
            var draw = draws[index]; var geometry = draw.Geometry;
            if (index < cuts.Count)
                cuts[index].Rebind(prefixes[index + 1], prefixes[index], draw.Owner, draw.Parameters,
                    geometry.Corners, geometry.Uvs, geometry.Region, size, 1u << (index + 1),
                    1, Vector4.One, Vector4.One);
            else
                cuts.Add(new(prefixes[index + 1], prefixes[index], draw.Owner, draw.Parameters,
                    geometry.Corners, geometry.Uvs, geometry.Region, size, 1u << (index + 1),
                    1, Vector4.One, Vector4.One, shaders));
            animatedCuts.Add(draw.Owner, cuts[index]);
        }
        attachedCuts = draws.Count;
        canvas.VisibilityLayer = OutputLayer | ((1u << prefixes.Count) - 1);
        outputMaterial!.SetShaderParameter("image", prefixes[^1].GetTexture());
        structuralNodes.Clear(); structuralNodes.UnionWith(prefixes);
        foreach (var copy in prefixCopies) { structuralNodes.Add(copy.Canvas); structuralNodes.Add(copy.Quad); }
        foreach (var cut in cuts) structuralNodes.Add(cut.Vertical);
        structuralNodes.Add(output);
    }

    public void Dispose() => Dispose(deferRemoval: false);
    internal void Dispose(bool deferRemoval)
    {
        if (disposed) return;
        disposed = true;
        if (GodotObject.IsInstanceValid(output))
        {
            output!.Hide(); output.Material = null;
            if (deferRemoval) output.QueueFree(); else output.Free();
        }
        outputMaterial?.Dispose();
        foreach (var effect in effectCuts)
            if (GodotObject.IsInstanceValid(effect)) effect.DetachCanvasPrefix();
        effectCuts.Clear(); animatedCuts.Clear();
        foreach (var cut in cuts.AsEnumerable().Reverse()) cut.Dispose(deferRemoval);
        foreach (var (view, parent) in dependencies)
            if (GodotObject.IsInstanceValid(view)) RenderingServer.ViewportSetParentViewport(view.GetViewportRid(), parent);
        dependencies.Clear();
        for (int index = prefixCopies.Count - 1; index >= 0; index--) RetirePrefixCopy(index);
        // The final viewport owns the whole acyclic producer chain.
        // Unpublished constructor prefixes can still be independent children.
        foreach (var prefix in prefixes.AsEnumerable().Reverse())
            if (GodotObject.IsInstanceValid(prefix))
            {
                prefix.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                if (deferRemoval) prefix.QueueFree(); else prefix.Free();
            }
        foreach (var (item, state) in assigned) RestoreItem(item, state.Previous);
        assigned.Clear();
        if (GodotObject.IsInstanceValid(input)) input.CanvasCullMask = previousCull;
        if (GodotObject.IsInstanceValid(canvas)) canvas.VisibilityLayer = previousCanvasLayer;
    }
}
