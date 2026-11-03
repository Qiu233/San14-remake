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
    private readonly Dictionary<SubViewport, Rid> renderParents = [];
    private UiSourceType6Quad? output;
    private readonly UiSourceType6Shaders shaders;
    private ShaderMaterial? outputMaterial;
    private bool disposed;
    private readonly bool incrementalPrefixes;
    private sealed record PrefixCopy(CanvasLayer Canvas, UiSourceType6Quad Quad, ShaderMaterial Material);
    private readonly List<PrefixCopy> prefixCopies = [];
    private sealed record IdleCut(SubViewport Prefix, UiSourceType6Pass Pass, PrefixCopy? Copy);
    // Local high-water storage only; size/effect/failure/Stop retire this graph.
    // The public graph lists continue to describe active prefixes and cuts.
    private readonly List<IdleCut> idleCuts = [];
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
        RetirePrefixCopy(copy); prefixCopies.RemoveAt(index);
    }

    private static void RetirePrefixCopy(PrefixCopy copy)
    {
        if (GodotObject.IsInstanceValid(copy.Quad)) { copy.Quad.Hide(); copy.Quad.Material = null; }
        copy.Material.Dispose();
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

    // Rendering dependencies do not transfer Node ownership. Reparenting a
    // retained prefix during PreDraw exits/reenters all its Canvas children,
    // whose normal deferred draw ordering has already run for this frame.
    private void SetRenderParent(SubViewport producer, Rid consumer)
    {
        Rid previous = renderParents.TryGetValue(producer, out var retained)
            ? retained : producer.GetParent().GetViewport().GetViewportRid();
        if (previous == consumer) return;
        // This engine's viewport_set_parent_viewport does not dirty its
        // topological sort. Refresh active membership without leaving the tree
        // or detaching any Canvas, preserving same-frame producer-before-user.
        Rid viewport = producer.GetViewportRid();
        RenderingServer.ViewportSetActive(viewport, false);
        RenderingServer.ViewportSetParentViewport(viewport, consumer);
        RenderingServer.ViewportSetActive(viewport, true);
        renderParents[producer] = consumer;
    }

    internal void DependOn(SubViewport producer)
    {
        var parent = producer.GetParent().GetViewport();
        Rid originalParent = parent.GetViewportRid();
        if (!dependencies.TryGetValue(producer, out var previousParent) || previousParent != originalParent)
        {
            dependencies[producer] = originalParent;
            // An external producer can reenter under a different scene owner;
            // its EnterTree setter restored the native scene parent already.
            renderParents.Remove(producer);
            SetRenderParent(producer, prefixes[0].GetViewportRid());
        }
    }

    internal void RebindDependencies(IReadOnlyCollection<SubViewport> producers)
    {
        var current = producers.ToHashSet();
        foreach (var producer in dependencies.Keys.Where(view => !current.Contains(view)).ToArray())
        {
            if (GodotObject.IsInstanceValid(producer) && producer.IsInsideTree())
                SetRenderParent(producer, producer.GetParent().GetViewport().GetViewportRid());
            dependencies.Remove(producer);
            renderParents.Remove(producer);
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
            sourceUvs, region, prefixes[index].Size, 1u << (index + 1), alpha, captureColor, prefillColor, shaders, SetRenderParent);
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

    // Ordinary cuts reuse this graph's same-size high-water targets. Idle
    // branches hold only compositor resources, never source Controls or geometry.
    // Size/effect changes retain the host's full-rebuild boundary.
    internal void Reconfigure(IReadOnlyList<UiSourceType6Draw> draws)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (output is null || HasEffectCuts || cuts.Count != attachedCuts)
            throw new InvalidOperationException("Only a published ordinary Type6 graph can reuse its targets.");
        if (draws.Count < 1 || draws.Count > 18) throw new ArgumentOutOfRangeException(nameof(draws));
        int oldCount = attachedCuts;
        if (draws.Count < oldCount)
        {
            // Detach native dependencies before disabling their consumers.
            // Prefixes retain their Node/Canvas trees throughout hover changes.
            for (int index = draws.Count; index < prefixes.Count; index++)
                SetRenderParent(prefixes[index], input.GetViewportRid());
            for (int index = oldCount - 1; index >= draws.Count; index--)
            {
                var prefix = prefixes[index + 1];
                var pass = cuts[index];
                PrefixCopy? copy = incrementalPrefixes ? prefixCopies[index] : null;
                idleCuts.Add(new(prefix, pass, copy));
                prefixes.RemoveAt(index + 1); cuts.RemoveAt(index);
                if (incrementalPrefixes) prefixCopies.RemoveAt(index);
                prefix.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                prefix.CanvasCullMask = 0;
                if (copy is not null)
                {
                    copy.Quad.Hide();
                    copy.Material.SetShaderParameter("image", default(Variant));
                }
                pass.Suspend();
            }
        }
        Vector2I size = prefixes[0].Size;
        while (prefixes.Count < draws.Count + 1)
        {
            int index = prefixes.Count;
            if (idleCuts.Count != 0)
            {
                var idle = idleCuts[^1];
                // Transfer all owned resources before any engine rebinding can
                // fail, so the normal graph failure path still retires them.
                prefixes.Add(idle.Prefix); cuts.Add(idle.Pass);
                if (idle.Copy is { } retainedCopy) prefixCopies.Add(retainedCopy);
                idleCuts.RemoveAt(idleCuts.Count - 1);
                idle.Prefix.CanvasCullMask = incrementalPrefixes ? 1u << index : (1u << (index + 1)) - 1;
                idle.Prefix.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                if (idle.Copy is { } copy)
                {
                    copy.Material.SetShaderParameter("image", prefixes[index - 1].GetTexture());
                    copy.Quad.Show();
                }
            }
            else
            {
                var prefix = new SubViewport { Name = "UiPrefix" + index, Size = size,
                    Disable3D = true, GuiDisableInput = true, TransparentBg = true, UseHdr2D = false,
                    World2D = input.FindWorld2D(), CanvasCullMask = incrementalPrefixes ? 1u << index : (1u << (index + 1)) - 1,
                    RenderTargetClearMode = SubViewport.ClearMode.Always,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                prefixes.Add(prefix); canvas.AddChild(prefix);
                if (incrementalPrefixes) AddPrefixCopy(index);
            }
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
                    1, Vector4.One, Vector4.One, shaders, SetRenderParent));
            animatedCuts.Add(draw.Owner, cuts[index]);
        }
        attachedCuts = draws.Count;
        canvas.VisibilityLayer = OutputLayer | ((1u << prefixes.Count) - 1);
        outputMaterial!.SetShaderParameter("image", prefixes[^1].GetTexture());
        structuralNodes.Clear(); structuralNodes.UnionWith(prefixes);
        foreach (var copy in prefixCopies) { structuralNodes.Add(copy.Canvas); structuralNodes.Add(copy.Quad); }
        foreach (var cut in cuts) structuralNodes.Add(cut.Vertical);
        foreach (var idle in idleCuts) structuralNodes.Add(idle.Prefix);
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
        foreach (var idle in idleCuts.AsEnumerable().Reverse()) idle.Pass.Dispose(deferRemoval);
        foreach (var (view, parent) in dependencies)
            if (GodotObject.IsInstanceValid(view) && view.IsInsideTree()) SetRenderParent(view, parent);
        dependencies.Clear();
        for (int index = prefixCopies.Count - 1; index >= 0; index--) RetirePrefixCopy(index);
        foreach (var idle in idleCuts)
            if (idle.Copy is { } copy) RetirePrefixCopy(copy);
        // The final viewport owns the whole acyclic producer chain.
        // Unpublished constructor prefixes can still be independent children.
        foreach (var prefix in prefixes.Concat(idleCuts.Select(idle => idle.Prefix)).Reverse())
            if (GodotObject.IsInstanceValid(prefix))
            {
                prefix.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                if (deferRemoval) prefix.QueueFree(); else prefix.Free();
            }
        idleCuts.Clear();
        renderParents.Clear();
        foreach (var (item, state) in assigned) RestoreItem(item, state.Previous);
        assigned.Clear();
        if (GodotObject.IsInstanceValid(input)) input.CanvasCullMask = previousCull;
        if (GodotObject.IsInstanceValid(canvas)) canvas.VisibilityLayer = previousCanvasLayer;
    }
}
