using Godot;
using San14.Data;

/// <summary>Concrete original48/50 geometry parameters, ordered53/73 targets,
/// native resize and D/S-only clear, plus actual post-model raster inheritance.
/// Remaining fullscreen raster source is supplied by its independent recipe.</summary>
public sealed class OriginalSceneViewNativeMapTerrainSourceOwner
{
    private readonly OriginalSceneViewNativeMapLoadInputs loaded;
    private readonly OriginalTerrainGbufferSourceParameters parameters;
    private readonly OriginalTerrainGbufferMaterialDrawSource materialDraw;
    private readonly byte ordinaryCallFlags;
    private readonly LoadedRenderGraphTargets terrainTargets, outputTargets;
    private readonly OriginalTerrainGbufferTargetSources targetSources;
    private readonly OriginalSceneViewNativeMapTerrainRasterSourceOwner raster;
    private readonly OriginalDeferredFullscreenRasterSources fullscreen;
    private readonly string contextIdentity;
    private readonly uint currentFlags3E4;
    private TerrainGbufferRasterState? bootstrapRequestHint;
    public OriginalTerrainGbufferColorMaskRecipe RequirePreparedColorMask(OriginalNativeMapFrameContext actualContext)
        => raster.RequirePreparedColorMask(actualContext);
    private OriginalSceneViewNativeMapTerrainSourceOwner(OriginalSceneViewNativeMapLoadInputs loaded,
        OriginalTerrainGbufferSourceParameters parameters, OriginalTerrainGbufferMaterialDrawSource materialDraw,
        byte ordinaryCallFlags,LoadedRenderGraphTargets terrain,
        LoadedRenderGraphTargets output, OriginalTerrainGbufferTargetSources targets,
        OriginalSceneViewNativeMapTerrainRasterSourceOwner raster, OriginalDeferredFullscreenRasterSources fullscreen,
        string contextIdentity, uint flags3E4)
    { this.loaded = loaded; this.parameters = parameters; this.materialDraw=materialDraw;
        this.ordinaryCallFlags=ordinaryCallFlags;terrainTargets = terrain; outputTargets = output;
        targetSources = targets; this.raster = raster; this.fullscreen = fullscreen;
        this.contextIdentity = contextIdentity; currentFlags3E4 = flags3E4; }

    public static OriginalSceneViewNativeMapTerrainSourceOwner ReadSource(OriginalSceneViewNativeMapLoadInputs loaded,
        LoadedRenderGraph graph, LoadedObjects textures, OriginalMainMapContextOwnerSource host,
        string currentContextOwnerIdentity)
    {
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        var source = OriginalTerrainGbufferSourceParameters.ReadSourceDefaults(loaded.Repository, locale, loaded.TerrainMaterial);
        var materialDraw=OriginalTerrainGbufferMaterialDrawSource.Load(loaded.Repository,locale,loaded.TerrainMaterial);
        // Ordinary F8 is the unchanged node56 source's unsuppressed invocation.
        // View selection changes the selected slot, not these call flags.
        var callFlags=OriginalMainSceneNode51Source.Load(loaded.Repository,locale,56).InitialCallFlags;
        var targets = OriginalTerrainGbufferTargetSources.ReadSource(loaded.Repository, locale, graph, textures);
        var raster = OriginalSceneViewNativeMapTerrainRasterSourceOwner.ReadSource(loaded.Repository,
            locale, loaded.TerrainMaterial, host);
        var fullscreen = OriginalDeferredFullscreenRasterSources.ReadSource(loaded.Repository, locale, graph);
        return new(loaded, source,materialDraw,callFlags,targets.GeometryTargets, targets.OutputTargets, targets,
            raster, fullscreen, currentContextOwnerIdentity, host.ContextFlags3E4);
    }

    public OriginalMapSourceCreatedPlayableSceneSource CreateSceneSource(OriginalSceneViewNativeMapFrameCapture capture)
    {
        var resize = PrepareResize(capture);
        return new(loaded.TerrainMaterial, loaded.InitialHeight, terrainTargets, outputTargets,
            resize.TerrainExtents, resize.OutputExtents);
    }

    public OriginalSceneViewNativeMapResizeInputs PrepareResize(OriginalSceneViewNativeMapFrameCapture capture)
    {
        var resize = targetSources.PrepareResize(capture.MainCamera.Camera, contextIdentity,
            capture.DisplayExtent.X, capture.DisplayExtent.Y);
        var all = resize.Targets.ToDictionary(row => row.ObjectNameHash, row => new Vector2I(row.Width, row.Height));
        IEnumerable<uint> Keys(LoadedRenderGraphTargets targets) => targets.ColorTargets.Select(row => row.ObjectNameHash)
            .Append(targets.DepthObjectNameHash).Where(key => key != 0).Distinct();
        return new(Keys(terrainTargets).ToDictionary(key => key, key => all[key]),
            Keys(outputTargets).ToDictionary(key => key, key => all[key]));
    }

    public OriginalMapSourceCreatedPlayableTerrainFrame PrepareFrame(OriginalSceneViewNativeMapFrameCapture capture)
    {
        var camera = OriginalPreparedCameraSceneSource.ReadCompletePrefix(capture.MainCamera.Camera.Prefix.Span);
        var clear = targetSources.PrepareInitialDepthStencilClear(capture.MainCamera.Camera.FrameNumber,
            contextIdentity, currentFlags3E4);
        // This CPU source plan is an unconsumed request hint. The mandatory
        // post-model hook replaces it from actual same-frame submitted scalar
        // receipts BEFORE Scene creates a terrain pipeline. It is no GPU proof.
        return new(capture.TerrainBatches, camera, PrepareParameters(capture),
            bootstrapRequestHint ?? throw new InvalidDataException("No exact bootstrap model raster request plan was initialized."),
            [], clear.Depth, clear.Stencil) { GeometryClearActions = RenderingDevice.DrawFlags.ClearDepth | RenderingDevice.DrawFlags.ClearStencil };
    }

    public void InitializeUnconsumedRequestHint(OriginalMapStaticModelPreparedFrame actualBootstrap)
    {
        if (bootstrapRequestHint is not null || !actualBootstrap.Complete || actualBootstrap.MaterialStates.Count == 0)
            throw new InvalidDataException("Bootstrap request raster must be initialized once from complete actual source model plans.");
        var last = actualBootstrap.MaterialStates[^1];
        if (last.Context.Selection.Source is not { GraphSource.Node.Index: 54 or 55 } ||
            last.Runtime.Raster.NativeStencil is not null)
            throw new InvalidDataException("Bootstrap raster hint requires its actual ordinary/cutout pre-stencil model source.");
        var source = last.Runtime.Raster;
        bootstrapRequestHint = new(source.Cull, true, source.DepthWrite, source.DepthCompare) { FrontFace = source.FrontFace };
    }

    public TerrainGbufferRasterState PrepareSubmittedTerrainRaster(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalNativeMapModelPopulationAcceptedFrame models, OriginalNativeMapArmyPopulationAcceptedFrame army)
        => raster.PrepareCurrentInherited(accepted, models, army);

    private TerrainGbufferDrawInputs PrepareParameters(OriginalSceneViewNativeMapFrameCapture capture)
    {
        if (capture.SceneGeneration != loaded.InitialFrame.SceneGeneration || capture.TerrainBatches.Count == 0)
            throw new InvalidDataException("Terrain source parameters require this loaded generation and actual nonempty patch inventory.");
        var camera = OriginalPreparedCameraSceneSource.ReadCompletePrefix(capture.MainCamera.Camera.Prefix.Span);
        // Ordinary F8 replaces the generic owner's constructor cSpec through
        // the shared material setter before packing near/coarse draws.
        // GBuffer callback writes nMtrID0; this Landscape has no tool edit.
        // PrepareTerrainCamera overwrites BOTH matrix layouts and Eye from
        // this exact accepted camera; placeholder arrays never reach packing.
        return OriginalDeferredSceneConstants.PrepareTerrainCamera(camera,
            new([], [], camera.SourceEye,materialDraw.PrepareSelectedOrdinaryF8(ordinaryCallFlags).Specular,0,
                loaded.InitialHeight.HeightNormalInfo, parameters.PrepareCliffInfo(), parameters.PrepareNearFar(), 0));
    }

    public IReadOnlyList<OriginalMapMaterialTerrainConsumer> PrepareMaterialConsumers(OriginalSceneViewNativeMapFrameCapture capture)
    {
        var input = PrepareParameters(capture);
        var shader = loaded.TerrainMaterial.Shader.Shader;
        return capture.TerrainBatches.Select(batch =>
        {
            var combination = batch.Coarse ? loaded.TerrainMaterial.CoarseCombination : loaded.TerrainMaterial.Combination;
            var pixel = combination.Stages.Single(stage => stage.StageCode == 0 && stage.Entry is not null).Entry!.ProgramIndex;
            var material = OriginalMapMaterialIdSources.CreateFreshTerrainDefault(loaded.TerrainMaterial.Shader,
                combination.Index, "owned-current-original-terrain-GBuffer/" + capture.RequestNumber + "/" + combination.Index);
            return new OriginalMapMaterialTerrainConsumer("owned-current-terrain/" + combination.Index, material,
                TerrainGbufferConstants.Pixel(shader.ReadResourceDefinitions(pixel), input, loaded.TerrainMaterial.Palette));
        }).ToArray();
    }

    public OriginalDeferredRasterState PrepareDeferredRaster(OriginalMapSourceCreatedNativeSessionPreparedFrame current)
    {
        // Draw-time node166 recipe, not a claim about Aux cleanup/poststate or
        // node139/140 source-null geometry (which performs no raster assignment).
        var definition = fullscreen.PrepareF103(current.Selection);
        if (definition.Kind != OriginalDeferredFullscreenRasterSourceKind.F103 ||
            definition.NativeCullMode != 1 || definition.NodeIndex != 166)
            throw new InvalidDataException("Unknown current original F103 fullscreen raster definition.");
        return new(RenderingDevice.PolygonCullMode.Disabled, definition.EnableBlend);
    }
}
