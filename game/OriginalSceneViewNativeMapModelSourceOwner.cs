using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>Concrete fresh static class18 packet producer. Model/provider,
/// initialized dimensions, type17 matrices, source image views, queue depth
/// and group raster all come from the loaded original sources. Storage padding
/// belongs to this backend; no original retained GPU allocation is claimed.</summary>
public sealed class OriginalSceneViewNativeMapModelSourceOwner
{
    private readonly OriginalGameRepository repository;
    private readonly LoadedRenderGraph graph;
    private readonly LoadedObjects shaderObjects;
    private readonly bool rendererBit29;
    private readonly Dictionary<(string Asset, int Draw), OriginalMapStaticModelDrawSource> draws = [];
    private readonly Dictionary<(string Asset, int Draw), OriginalModelOrdinaryQueueSelectionSource> selections = [];
    private readonly OriginalNativeWorldShaderTableRegistry shaderTables;
    private readonly bool ownsShaderTables;
    private readonly Dictionary<OriginalModelGbufferSourceDraw, OriginalNativeWorldShaderTableState> selectedStates
        = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<OriginalMapStaticModelAsset> noMaterialOverrideAssets = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<OriginalModelGbufferSourceDraw> finalBaseTableDraws = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(string Asset, int Draw), OriginalNativeWorldShaderTableState> finalBaseTables = [];
    private sealed record RetainedSourceState(string AssetSourceKey,
        OriginalModelGbufferSourceDraw Source, OriginalMapStaticModelExplicitState State);
    private readonly Dictionary<string, RetainedSourceState> retained = new(StringComparer.Ordinal);
    private readonly OriginalMapStaticModelStateProvider runtime;
    private bool valid = true;

    public OriginalSceneViewNativeMapModelSourceOwner(OriginalGameRepository repository,
        LoadedRenderGraph graph, LoadedObjects shaderObjects, OriginalMainMapContextOwnerSource actualContext,
        OriginalNativeWorldShaderTableRegistry? sharedShaderTables = null)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
        this.shaderObjects = shaderObjects ?? throw new ArgumentNullException(nameof(shaderObjects));
        shaderTables = sharedShaderTables ?? OriginalNativeWorldShaderTableRegistry.CreateLoaded(shaderObjects);
        ownsShaderTables = sharedShaderTables is null;
        ArgumentNullException.ThrowIfNull(actualContext);
        rendererBit29 = actualContext.ContextOwner3E4Bit29;
        runtime = new(graph, PreparePrevious);
    }

    public OriginalMapStaticModelDrawSource LoadDraw(OriginalMapStaticModelAsset asset, int drawIndex)
    {
        RequireValid(); ArgumentNullException.ThrowIfNull(asset);
        if (draws.TryGetValue((asset.SourceKey, drawIndex), out var cached)) return cached;
        ImportFreshNoMaterialOverride(asset);
        // Both queues select state18. The renderer's actual raw configuration
        // scan classifies this selected shader before either queue consumes it.
        var bindings = OriginalModelShaderResources.LoadBindings(repository, asset.TextureResources);
        var selected = OriginalModelShaderResources.ResolveRenderGraphNodeBaseSource(bindings,
            drawIndex, shaderObjects, graph, 54);
        var provider = selected.Interface.Provider ??
            throw new InvalidDataException("Current ordinary static draw has no selected source provider.");
        var shader = OriginalModelShaderResources.LoadDeclaredG1S(repository, provider);
        var opaque = OriginalModelOrdinaryQueueSelectionSource.ReadFreshSelected(
            OriginalModelRendererStateSourceReader.ReadQueue(graph, 54), selected.Interface, shader);
        var alphaSelected = OriginalModelShaderResources.ResolveRenderGraphNodeBaseSource(bindings,
            drawIndex, shaderObjects, graph, 55);
        var alpha = OriginalModelOrdinaryQueueSelectionSource.ReadFreshSelected(
            OriginalModelRendererStateSourceReader.ReadQueue(graph, 55), alphaSelected.Interface, shader);
        if (opaque.SelectedRenderer.OwnerObjectId != alpha.SelectedRenderer.OwnerObjectId ||
            opaque.SelectedRenderer.ProviderObjectId != alpha.SelectedRenderer.ProviderObjectId ||
            opaque.SelectedRenderer.ShaderSha256 != alpha.SelectedRenderer.ShaderSha256 ||
            opaque.NativeFreshAux4Bit8 != alpha.NativeFreshAux4Bit8 ||
            opaque.SourceQueueSelectsShader == alpha.SourceQueueSelectsShader)
            throw new InvalidDataException("Static queues must select the same actual renderer into exactly one source alpha branch.");
        var selection = opaque.SourceQueueSelectsShader ? opaque : alpha;
        int node = selection.Queue.Node.Index;
        var matrixInput = OriginalModelMatrixBindingSourceReader.PrepareUnboundGroup(bindings, drawIndex) ??
            throw new InvalidDataException("Source-created static draw lacks its actual type17 matrix input.");
        var matrix = OriginalModelMatrixBindingSourceReader.Select(shader, matrixInput);
        var dimensions = OriginalModelShaderSelectionSourceReader.Read(provider, shader);
        var initial = OriginalModelShaderSelectionSourceReader.ResolveSingleOtherDimensionsInitialized(dimensions,
            OriginalModelShaderSelectionSourceReader.ReadInitialCache(dimensions), matrix, 0);
        var source = OriginalModelGbufferSourceDraw.LoadSourceTextureViews(repository, asset.TextureResources,
            shaderObjects, graph, node, drawIndex, initial.Combination.Index, new(false, false, 0));
        cached = new(source, "owned-source-node" + node + "-state2", true, null);
        // C34B76 stores the base ShaderCPU+8 full typed16 table in primitive18.
        // D365CD selects state18's GBuffer provider only later. Two base tables
        // sharing that provider remain different sorting identities.
        var state = LoadFinalOwnedTable(asset, drawIndex);
        selectedStates.Add(source, state);
        finalBaseTableDraws.Add(source);
        draws.Add((asset.SourceKey, drawIndex), cached);
        selections.Add((asset.SourceKey, drawIndex), selection);
        return cached;
    }

    /// <summary>Full primitive18 table of this source-created static Model.
    /// The same owned table can select any original interface, including
    /// shadow state3. Loading it does not require Main visibility, an ordinary
    /// GBuffer provider or a prepared state18 material packet.</summary>
    public OriginalNativeWorldShaderTableState LoadFinalOwnedTable(OriginalMapStaticModelAsset asset, int drawIndex)
    {
        RequireValid(); ArgumentNullException.ThrowIfNull(asset);
        var key = (asset.SourceKey, drawIndex);
        if (finalBaseTables.TryGetValue(key, out var current)) return current;
        ImportFreshNoMaterialOverride(asset);
        var bindings = OriginalModelShaderResources.LoadBindings(repository, asset.TextureResources);
        var actualBase = OriginalModelShaderResources.ResolveDrawBaseSource(bindings, drawIndex, shaderObjects);
        current = RetainSourceShaderTable(actualBase.Binding.Shader);
        finalBaseTables.Add(key, current);
        return current;
    }

    public OriginalModelOrdinaryQueueSelectionSource LoadQueueSelection(OriginalMapStaticModelAsset asset, int drawIndex)
    {
        _ = LoadDraw(asset, drawIndex);
        return selections[(asset.SourceKey, drawIndex)];
    }

    public OriginalNativeWorldShaderTableState ReadCurrentShaderState(OriginalMapStaticModelDrawContext current)
    {
        RequireValid(); ArgumentNullException.ThrowIfNull(current);
        var selected = current.Selection.Source ?? throw new InvalidDataException("Current static shader state is absent.");
        if (!ReferenceEquals(selected, LoadDraw(current.Instance.Asset, current.DrawIndex).Source) ||
            !selectedStates.TryGetValue(selected, out var state))
            throw new InvalidDataException("Static shader state belongs to another actual source/provider generation.");
        return state;
    }

    /// <summary>Actual final primitive18 in this owned fresh construction.
    /// The load-once empty ResourceSetMaterialsReference proves ModelCPU1D8=0
    /// and158=null; no external material setter exists on this owner. Thus
    /// C34CBC keeps the actual base full table, independent of group mapping.
    /// A nonempty material reference requires its separate real producer.</summary>
    public OriginalNativeWorldShaderTableState ReadFinalOwnedTable(OriginalMapStaticModelPreparedState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var state = ReadCurrentShaderState(current.Context);
        if (!finalBaseTableDraws.Contains(current.Context.Selection.Source!))
            throw new NotSupportedException("Current static final table requires its actual material override producer.");
        return state;
    }

    private void ImportFreshNoMaterialOverride(OriginalMapStaticModelAsset asset)
    {
        if (noMaterialOverrideAssets.Contains(asset)) return;
        var model = asset.TextureResources.ModelObject;
        if (model.ClassId != OriginalModelShaderResources.ModelClass || model.Properties.Count <= 43)
            throw new InvalidDataException("Static source material binding needs the complete actual Model declaration.");
        var references = model.Properties[43];
        if (references.Index != 43 || references.NameHash != 0x6C3B792D || references.TypeCode != 5 ||
            references.RawValue.Length != checked(references.ElementCount * 4))
            throw new InvalidDataException("Static ResourceSetMaterialsReference needs its exact original typed DWORD array.");
        if (references.ElementCount != 0)
            throw new NotSupportedException("This static Model has actual material-set references; their current final table producer is required.");
        noMaterialOverrideAssets.Add(asset);
    }

    /// <summary>Actual resource creation/reuse for a base or explicitly
    /// selected material table. A current final-table producer chooses it;
    /// this allocation does not infer that an original material override is
    /// absent.</summary>
    public OriginalNativeWorldShaderTableState RetainSourceShaderTable(OriginalNamedModelShader actualTableOwner)
    {
        RequireValid(); ArgumentNullException.ThrowIfNull(actualTableOwner);
        return shaderTables.RetainImportedTable(actualTableOwner);
    }

    public void RequireCurrentShaderState(OriginalNativeWorldShaderTableState state)
    {
        RequireValid(); ArgumentNullException.ThrowIfNull(state);
        shaderTables.RequireCurrent(state);
    }

    public OriginalModelShaderInterfaceSource ReadSelectedShaderInterface(OriginalNativeWorldShaderTableState state, int stateIndex)
    {
        RequireValid();
        return shaderTables.ReadSelectedInterface(state, stateIndex);
    }

    public OriginalMapStaticModelDrawRuntime PrepareRuntime(OriginalMapStaticModelDrawContext context)
    { RequireValid(); return runtime.PrepareRuntime(context); }

    private OriginalMapStaticModelExplicitState PreparePrevious(OriginalMapStaticModelDrawContext context)
    {
        var draw = context.Selection.Source ?? throw new InvalidDataException("Static source draw is unresolved.");
        if (!ReferenceEquals(draw, LoadDraw(context.Instance.Asset, context.DrawIndex).Source))
            throw new InvalidDataException("Static material state requires this owner's selected immutable source draw.");
        // Instance/queue packet identity survives a city model/category change.
        // Only the exact retained asset and selected draw generation can keep
        // its material cache, pixel template and group raster. A new source
        // starts from its own constructor/material/raster preparation.
        if (retained.TryGetValue(context.PacketKey, out var previous) &&
            previous.AssetSourceKey == context.Instance.Asset.SourceKey &&
            ReferenceEquals(previous.Source, draw)) return previous.State;
        if (draw.Material.MaterialId is null)
            throw new InvalidDataException("This fresh static source needs its own named nMtrID; a missing declaration cannot select zero.");
        var bindings = OriginalModelShaderResources.LoadBindings(repository, context.Instance.Asset.TextureResources);
        var group = OriginalModelRendererStateSourceReader.ReadGroupRaster(bindings, context.DrawIndex);
        var queue = OriginalModelRendererStateSourceReader.ReadQueue(graph, draw.GraphSource.Node.Index);
        var depth = OriginalModelRendererStateSourceReader.PrepareQueueDepth(queue, rendererBit29);
        if (depth.StencilEnabled) throw new InvalidDataException("Base static packet must precede its independent native stencil writer.");
        var attachment = new OriginalModelBlendAttachment(false, RenderingDevice.BlendFactor.One,
            RenderingDevice.BlendFactor.Zero, RenderingDevice.BlendOperation.Add,
            RenderingDevice.BlendFactor.One, RenderingDevice.BlendFactor.Zero, RenderingDevice.BlendOperation.Add,
            true, true, true, true);
        var cull = group?.NativeCullMode switch {
            1 => RenderingDevice.PolygonCullMode.Disabled,
            2 => RenderingDevice.PolygonCullMode.Front,
            3 => RenderingDevice.PolygonCullMode.Back,
            null => throw new InvalidDataException("Static group has no type10 raster override; its separate current queue raster is required."),
            _ => throw new InvalidDataException("Unknown source group cull mode.") };
        var compare = depth.CompareCode switch {
            0 => RenderingDevice.CompareOperator.Never, 1 => RenderingDevice.CompareOperator.Less,
            2 => RenderingDevice.CompareOperator.Equal, 3 => RenderingDevice.CompareOperator.LessOrEqual,
            4 => RenderingDevice.CompareOperator.Greater, 5 => RenderingDevice.CompareOperator.NotEqual,
            6 => RenderingDevice.CompareOperator.GreaterOrEqual, 7 => RenderingDevice.CompareOperator.Always,
            _ => throw new InvalidDataException("Source static queue has unsupported depth compare.") };
        var raster = new OriginalModelRasterInputs(cull, RenderingDevice.PolygonFrontFace.CounterClockwise,
            depth.DepthTest, depth.DepthWrite, compare, Enumerable.Repeat(attachment, 5).ToArray());
        byte[] pixel = new byte[80];
        // Actual standard Generic constructor cSpec=(0,0,0,1). Subsequent
        // source material preparation either writes or retains this owned value.
        int spec = checked((int)draw.PixelDefinitions.Buffers.Single().Variables.Single(v => v.Name.Text == "cSpec").StartOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(pixel.AsSpan(spec + 12, 4), 0x3F800000);
        return new(new byte[160], pixel, new(null, null, 0), new(false, 4, 0),
            "owned-source-material/" + context.Instance.Asset.SourceKey + "/" +
            draw.Shader.Source.DecodedSha256 + "/" + draw.ExplicitCombinationIndex + "/" +
            draw.Material.Draw.MaterialRecord.Index, raster);
    }

    public void CommitAccepted(OriginalMapStaticModelPreparedFrame frame)
    {
        RequireValid(); ArgumentNullException.ThrowIfNull(frame);
        if (!frame.Complete) throw new InvalidDataException("Incomplete static source state cannot become retained.");
        foreach (var state in frame.MaterialStates)
        {
            var previous = PreparePrevious(state.Context);
            var transition = state.Runtime.SourceMaterialTransition ??
                throw new InvalidDataException("Actual source material transition is absent.");
            retained[state.Context.PacketKey] = new(state.Context.Instance.Asset.SourceKey,
                state.Context.Selection.Source!, previous with {
                PreviousPixelTemplate = state.Prepared.Packet.Pixel.Constants[0].ToArray(),
                PreviousMaterial = transition.Next, PreviousAlpha = transition.Alpha.Next });
        }
    }
    public void Invalidate(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter); valid = false; retained.Clear(); draws.Clear(); selections.Clear();
        selectedStates.Clear(); finalBaseTableDraws.Clear(); finalBaseTables.Clear(); noMaterialOverrideAssets.Clear();
        if (ownsShaderTables) shaderTables.Invalidate(actualWriter); }
    private void RequireValid()
    { if (!valid) throw new InvalidDataException("Native static source factory was imported or edited."); }
}
