using San14.Data;

/// <summary>Templates, raster payload and previous native caches have explicit
/// ownership. The material token identifies an original prepared payload;
/// instances sharing it can exercise the original pair-identity skip.</summary>
public sealed record MapStaticModelExplicitState(ReadOnlyMemory<byte> VertexTemplate,
    ReadOnlyMemory<byte> PreviousPixelTemplate, ModelDrawMaterialCache PreviousMaterial,
    ModelAlphaState PreviousAlpha, string PreparedMaterialToken,
    ModelRasterInputs Raster);

/// <summary>Adapter for the static model assembler's runtime delegate. Source
/// FC64 queue flags produce alpha enable; material source and explicit previous
/// caches produce compare/reference and cSpec writes. Frame acceptance remains
/// responsible for committing SourceMaterialTransition.Next and Alpha.Next.
/// Type10 raster state and complete first-frame templates remain caller sources.</summary>
public sealed class MapStaticModelStateProvider
{
    private readonly LoadedRenderGraph _graph;
    private readonly Func<MapStaticModelDrawContext, MapStaticModelExplicitState> _previous;
    private readonly Dictionary<int, ModelQueueStateSource> _queues = [];

    public MapStaticModelStateProvider(LoadedRenderGraph graph,
        Func<MapStaticModelDrawContext, MapStaticModelExplicitState> previous)
    {
        ArgumentNullException.ThrowIfNull(graph); ArgumentNullException.ThrowIfNull(previous);
        (_graph, _previous) = (graph, previous);
    }

    public MapStaticModelDrawRuntime PrepareRuntime(MapStaticModelDrawContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ModelGbufferSourceDraw draw = context.Selection.Source ??
            throw new InvalidDataException("Original static model state provider has no eligible source draw.");
        if (draw.GraphSource.Graph.Source.ResourceUri != _graph.Source.ResourceUri ||
            draw.GraphSource.Graph.Source.DecodedSha256 != _graph.Source.DecodedSha256)
            throw new InvalidDataException("Original static model state provider graph differs from the draw's queue source.");
        int index = draw.GraphSource.Node.Index;
        if (!_queues.TryGetValue(index, out ModelQueueStateSource? queue))
        {
            queue = ModelRendererStateSourceReader.ReadQueue(_graph, index);
            _queues.Add(index, queue);
        }
        if (queue.ShaderStateOffset != draw.GraphSource.Interface.StateIndex)
            throw new InvalidDataException("Original static model state provider queue offset differs from the selected type16 interface.");
        MapStaticModelExplicitState state = _previous(context) ??
            throw new InvalidDataException("Original static model explicit previous state is unavailable.");
        if (state.VertexTemplate.Length != 160 || state.PreviousPixelTemplate.Length != 80)
            throw new InvalidDataException("Original static model state provider requires complete rigid VS160 and PS80 templates.");
        ArgumentNullException.ThrowIfNull(state.Raster);
        ModelAlphaTransition queueAlpha = ModelRendererStateSourceReader.PrepareQueueAlpha(queue, state.PreviousAlpha);
        ModelDrawMaterialTransition material = ModelRendererStateSourceReader.PrepareUnmodifiedMaterial(
            draw.Material, state.PreparedMaterialToken, state.PreviousMaterial, queueAlpha.Next);
        // Validate the finite threshold semantics before the assembler publishes
        // any packet. A stale enabled compare beyond four remains unresolved.
        ModelRendererStateSourceReader.PrepareAlphaTest(material.Alpha.Next);
        return new(state.VertexTemplate, state.PreviousPixelTemplate, state.PreviousMaterial.CachedMaterialFlags,
            material.Alpha.Next.Enabled, null, state.Raster) { SourceMaterialTransition = material };
    }
}
