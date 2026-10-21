using Godot;

/// <summary>Finite prefix graph for a structural UI canvas. Controls remain
/// in their original input tree. The caller supplies each real draw boundary;
/// this owner does not infer it from a final screenshot or replay UI callbacks.</summary>
internal sealed class UiSourceType6Composition : IDisposable
{
    private const uint OutputLayer = 1u << 19;
    private readonly Control canvas;
    private readonly Viewport input;
    private readonly uint previousCull, previousCanvasLayer;
    private readonly SubViewport[] prefixes;
    private readonly List<UiSourceType6Pass> cuts = [];
    private readonly Dictionary<CanvasItem, uint> assigned = [];
    private readonly List<(SubViewport View, Rid Parent)> dependencies = [];
    private UiSourceType6Quad? output;
    private Shader? outputShader;
    private ShaderMaterial? outputMaterial;
    private bool disposed;
    internal IReadOnlyList<SubViewport> Prefixes => prefixes;
    internal IReadOnlyList<UiSourceType6Pass> Cuts => cuts;

    internal UiSourceType6Composition(Control structuralCanvas, Vector2I size, int cutCount)
    {
        if (cutCount < 1 || cutCount > 18 || size.X < 4 || size.Y < 4)
            throw new ArgumentOutOfRangeException(nameof(cutCount));
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
            CanvasCullMask = (1u << (i + 1)) - 1,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        }).ToArray();
        foreach (var prefix in prefixes) canvas.AddChild(prefix);
    }

    internal void AssignSegment(CanvasItem subtree, int segment)
    {
        if ((uint)segment >= prefixes.Length) throw new ArgumentOutOfRangeException(nameof(segment));
        Visit(subtree);
        void Visit(Node node)
        {
            if (node is CanvasItem item)
            {
                assigned.TryAdd(item, item.VisibilityLayer);
                uint layer = 1u << segment;
                item.VisibilityLayer = layer;
                if (item is IUiSourceDrawing drawing) drawing.SetDrawingVisibilityLayer(layer);
            }
            foreach (Node child in node.GetChildren()) Visit(child);
        }
    }

    internal void AssignItem(CanvasItem item, int segment, uint structuralMask)
    {
        if ((uint)segment >= prefixes.Length) throw new ArgumentOutOfRangeException(nameof(segment));
        assigned.TryAdd(item, item.VisibilityLayer);
        item.VisibilityLayer = structuralMask;
        uint drawingLayer = 1u << segment;
        if (item is IUiSourceDrawing drawing) drawing.SetDrawingVisibilityLayer(drawingLayer);
    }

    internal void DependOn(SubViewport producer)
    {
        var parent = producer.GetParent().GetViewport();
        dependencies.Add((producer, parent.GetViewportRid()));
        RenderingServer.ViewportSetParentViewport(producer.GetViewportRid(), prefixes[0].GetViewportRid());
    }

    internal void AddCut(UiSourceType6Parameters parameters, Vector2[] screenQuad,
        Vector2[] sourceUvs, Rect2I region, float alpha, Vector4 captureColor, Vector4 prefillColor,
        Control? drawingParent = null)
    {
        if (disposed || output is not null || cuts.Count == prefixes.Length - 1)
            throw new InvalidOperationException("Type6 graph is sealed or has no remaining cut.");
        int index = cuts.Count;
        cuts.Add(new(prefixes[index + 1], prefixes[index], drawingParent ?? canvas, parameters, screenQuad,
            sourceUvs, region, prefixes[index].Size, 1u << (index + 1), alpha, captureColor, prefillColor));
    }

    internal void Publish()
    {
        if (disposed || output is not null || cuts.Count != prefixes.Length - 1)
            throw new InvalidOperationException("Type6 graph requires all actual cuts before publication.");
        outputShader = new() { ResourceName = "UiPrefixEncodedReplacement", Code = """
            shader_type canvas_item;
            render_mode unshaded, blend_disabled;
            uniform sampler2D image : filter_nearest, repeat_disable;
            void fragment() { COLOR = texture(image, UV); }
            """ };
        outputMaterial = new() { Shader = outputShader };
        outputMaterial.SetShaderParameter("image", prefixes[^1].GetTexture());
        Vector2 size = (Vector2)prefixes[^1].Size;
        output = new UiSourceType6Quad { Name = "UiComposedOutput", Size = size,
            Positions = [Vector2.Zero, new(size.X, 0), new(0, size.Y), size],
            Uvs = [Vector2.Zero, Vector2.Right, Vector2.Down, Vector2.One], Material = outputMaterial,
            MouseFilter = Control.MouseFilterEnum.Ignore, VisibilityLayer = OutputLayer };
        canvas.AddChild(output);
        input.CanvasCullMask = OutputLayer;
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
        outputMaterial?.Dispose(); outputShader?.Dispose();
        foreach (var cut in cuts.AsEnumerable().Reverse()) cut.Dispose(deferRemoval);
        foreach (var (view, parent) in dependencies)
            if (GodotObject.IsInstanceValid(view)) RenderingServer.ViewportSetParentViewport(view.GetViewportRid(), parent);
        dependencies.Clear();
        // The final viewport owns the whole acyclic producer chain.
        // Unpublished constructor prefixes can still be independent children.
        foreach (var prefix in prefixes.Reverse())
            if (GodotObject.IsInstanceValid(prefix))
            {
                prefix.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
                if (deferRemoval) prefix.QueueFree(); else prefix.Free();
            }
        foreach (var (item, previous) in assigned)
        {
            if (!GodotObject.IsInstanceValid(item) || item.IsQueuedForDeletion()) continue;
            item.VisibilityLayer = previous;
            // Exited controls have already released their borrowed font/RIDs.
            // Do not recreate a draw owner during the parent's exit sequence.
            if (item.IsInsideTree() && item is IUiSourceDrawing drawing) drawing.SetDrawingVisibilityLayer(previous);
        }
        assigned.Clear();
        if (GodotObject.IsInstanceValid(input)) input.CanvasCullMask = previousCull;
        if (GodotObject.IsInstanceValid(canvas)) canvas.VisibilityLayer = previousCanvasLayer;
    }
}
