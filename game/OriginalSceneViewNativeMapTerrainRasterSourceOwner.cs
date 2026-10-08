using Godot;
using San14.Data;
using System.Runtime.CompilerServices;

/// <summary>Source54/55 actual submitted last class18 primitive -> node56 F8
/// raster. Reads the same accepted model layer receipt, never a previous frame
/// or a caller-created state. World49/50 precedes static54/55; the legacy
/// static-then-Army route retains its zero-Army raster scope.</summary>
public sealed class OriginalSceneViewNativeMapTerrainRasterSourceOwner
{
    private readonly OriginalGameRepository repository;
    private readonly OriginalMainMapContextOwnerSource host;
    private readonly string locale;
    private readonly OriginalTerrainGbufferColorMaskSource colorMasks;
    private readonly LoadedOriginalGeneralSceneTerrainCallerSource terrainCaller;
    private sealed record LoadedInheritance(LoadedRdbAsset Graph, LoadedRdbAsset Shader, LoadedRdbAsset ShaderObjects,
        int DrawIndex, int QueueNode, uint OwnerObject, uint? ProviderObject, OriginalTerrainModelRasterInheritanceSource Source);
    private sealed class LoadedModelInheritance
    {
        public LoadedModelInheritance(LoadedModelShaderBindings bindings) { Bindings=bindings; }
        public LoadedModelShaderBindings Bindings { get; }
        public List<LoadedInheritance> Sources { get; } = new();
    }
    private readonly ConditionalWeakTable<LoadedModelTextureResources, LoadedModelInheritance> loadedModels = new();
    private readonly ConditionalWeakTable<OriginalNativeMapFrameContext, OriginalTerrainGbufferColorMaskRecipe> preparedMasks = new();
    private sealed record PreparedWorldArmy(OriginalNativeMapArmyPopulationAcceptedFrame Army,
        OriginalMapSourceCreatedF103CurrentFrame Geometry);
    private readonly ConditionalWeakTable<OriginalNativeMapFrameContext, PreparedWorldArmy> preparedWorldArmies = new();
    private bool retained = true;
    private OriginalSceneViewNativeMapTerrainRasterSourceOwner(OriginalGameRepository repository,
        string locale, OriginalMainMapContextOwnerSource host, OriginalTerrainGbufferColorMaskSource colorMasks)
    { this.repository = repository; this.locale = locale; this.host = host; this.colorMasks = colorMasks;
        terrainCaller=OriginalMainSceneNode51Source.Load(repository,locale,56).CallerSource; }

    public static OriginalSceneViewNativeMapTerrainRasterSourceOwner ReadSource(OriginalGameRepository repository,
        string locale, LoadedTerrainGbiMapResources actualMaterial, OriginalMainMapContextOwnerSource actualHost)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualHost);
        _ = OriginalTerrainGbufferSourceParameters.ReadSourceDefaults(repository, locale, actualMaterial);
        if (actualHost.Locale != locale || actualHost.ExecutableSha256 !=
            OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, locale))
            throw new InvalidDataException("Terrain inherited raster requires its exact current source Host.");
        return new(repository, locale, actualHost,
            OriginalTerrainGbufferColorMaskSource.ReadSource(repository, locale, actualMaterial));
    }

    public void MarkImportedEditedOrInterveningRasterAssignment(string writer)
    { ArgumentException.ThrowIfNullOrWhiteSpace(writer); retained = false; }

    public TerrainGbufferRasterState PrepareCurrentInherited(OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalNativeMapModelPopulationAcceptedFrame actualModels,
        OriginalNativeMapArmyPopulationAcceptedFrame actualArmy)
    {
        var context = accepted.Context;
        if (!retained || !ReferenceEquals(actualModels.Context, context) ||
            !ReferenceEquals(actualArmy.Prepared.Context, context) ||
            actualArmy.Prepared.Unresolved.Count != 0 || actualArmy.Prepared.CpuPrimitivePopulation is null)
            throw new InvalidDataException("Terrain inheritance requires this actual same-frame complete static/Army preparation.");
        if (accepted.ActualGeometrySourceFrame is not { } source || accepted.PreparedSourceFrame is not { } current ||
            !ReferenceEquals(actualModels.Frame.Visibility, source.CpuPopulation?.Visibility) ||
            source.Context.Source.Host.Locale != locale || source.Context.Source.Host.ExecutableSha256 != host.ExecutableSha256 ||
            source.Context.Source.Host.ContextFlags3E4 != accepted.Lighting.ContextFlags3E4)
            throw new InvalidDataException("Terrain raster inheritance needs this source graph/Host and nonempty actual class18 submitted geometry.");
        OriginalWorldGeometryShadingPhase.RequireSameGeometrySnapshot(source, current);
        bool worldArmy = actualArmy.WorldQueuesCompletedPublication is not null;
        if (worldArmy)
        {
            _ = OriginalNativeArmyWorldAcceptedGeometry.RequireCurrent(actualArmy, context, source);
            if (actualModels.StaticQueueSubmission is null)
                throw new InvalidDataException("World Army49/50 requires the actual subsequent static54/55 completion before Terrain56.");
        }
        else
        {
            if (actualArmy.Prepared.CpuPrimitivePopulation.Actors.Count != 0 || actualArmy.Prepared.OrdinaryActors != 0 ||
                actualArmy.Prepared.FlagActors != 0 || actualArmy.FlagStatuses.Count != 0 ||
                actualArmy.OrdinaryGeometrySubmission is not { } armyOrdinary || actualArmy.FlagGeometrySubmission is not { } armyFlags)
                throw new NotSupportedException("Legacy static-then-Army Terrain inheritance requires actual zero-Army layer submissions.");
            armyOrdinary.RequireCurrent(); armyFlags.RequireCurrent();
            if (!ReferenceEquals(armyOrdinary.Target, context.TerrainFrame) || !ReferenceEquals(armyFlags.Target, context.TerrainFrame) ||
                armyOrdinary.Primitives.Count != 0 || armyFlags.Primitives.Count != 0)
                throw new NotSupportedException("An intervening legacy Army primitive requires its actual last-raster producer.");
        }
        var primitives=OriginalNativeMapStaticSubmission.RequireCurrent(actualModels,context,source);
        if(primitives.Count==0)
            throw new InvalidDataException("Terrain inheritance needs the actual last submitted static primitive.");
        var last = primitives[^1];
        var raster = last.Raster ?? throw new InvalidDataException("Actual last primitive has no immutable submitted raster snapshot.");
        var stencil = last.NativeStencil ?? throw new InvalidDataException("Actual last submitted model lacks its final native class18 source receipt.");
        var state = actualModels.Frame.MaterialStates.SingleOrDefault(item => item.Context.PacketKey == stencil.PrimitiveKey) ??
            throw new InvalidDataException("Last actual submitted raw primitive is absent from the complete same-frame preparation.");
        var selected = state.Context.Selection.Source ?? throw new InvalidDataException("Last actual selected source draw is unavailable.");
        bool actualIdentity;
        if(actualModels.StaticQueueSubmission is{} queues)
        {
            var candidate=queues.Plan.SourceCandidates.SingleOrDefault(c=>c.CandidateIdentity==last.PacketKey);
            actualIdentity=candidate is not null&&ReferenceEquals(candidate.MaterialState,state)&&
                ReferenceEquals(candidate.SourceStencil,stencil.Source);
        }
        else actualIdentity=last.PacketKey==stencil.PrimitiveKey+"|native-record:"+stencil.Source.DrawTimeState.RecordMask78.ToString("X8");
        if (!actualIdentity ||
            state.Context.DrawIndex != last.SourceDrawIndex || selected.Model.Source.ResourceUri != last.ModelUri ||
            selected.Model.Source.DecodedSha256 != last.ModelSha256 || selected.Shader.Source.ResourceUri != last.ShaderUri ||
            selected.Shader.Source.DecodedSha256 != last.ShaderSha256 || state.Runtime.Raster.NativeStencil is not null ||
            selected.GraphSource.Node.Index!=stencil.Source.Source.QueueNodeIndex ||
            state.Runtime.BackendAlphaEnabled!=(stencil.Source.Source.QueueNodeIndex==55) || state.Runtime.ForcedAlphaReference is not null)
            throw new InvalidDataException("Current last submitted primitive identity/source/override scope disagrees with its immutable source plan.");
        // These declarations are loaded once for this actual model asset and
        // draw/provider/graph source. The last submitted primitive and its
        // current raster/stencil remain the inputs to PrepareCurrent below.
        var loaded=loadedModels.GetValue(state.Context.Instance.Asset.TextureResources,
            asset=>new(OriginalModelShaderResources.LoadBindings(repository,asset)));
        var graph=selected.GraphSource;
        var found=loaded.Sources.SingleOrDefault(item=>ReferenceEquals(item.Graph,graph.Graph.Source)&&
            ReferenceEquals(item.Shader,selected.Shader.Source)&&ReferenceEquals(item.ShaderObjects,graph.Interface.Owner.Objects.Source)&&
            item.DrawIndex==last.SourceDrawIndex&&item.QueueNode==graph.Node.Index&&
            item.OwnerObject==graph.Interface.Owner.Object.ObjectId&&item.ProviderObject==graph.Interface.Provider?.Object.ObjectId);
        OriginalTerrainModelRasterInheritanceSource inheritance;
        if(found is not null)inheritance=found.Source;
        else
        {
            inheritance=OriginalTerrainModelRasterInheritanceSource.ReadFreshLastModelSource(loaded.Bindings,
                last.SourceDrawIndex,graph,selected.Shader,terrainCaller);
            loaded.Sources.Add(new(graph.Graph.Source,selected.Shader.Source,graph.Interface.Owner.Objects.Source,
                last.SourceDrawIndex,graph.Node.Index,graph.Interface.Owner.Object.ObjectId,graph.Interface.Provider?.Object.ObjectId,inheritance));
        }
        var recipe = inheritance.PrepareCurrent(stencil.Source, new(context.FrameNumber,
            accepted.Lighting.Binding.ViewIdentity, source.Context.Source.OwnedHostIdentity, stencil.PrimitiveKey,
            accepted.Lighting.ContextFlags3E4, 0, true, true, true, true,
            worldArmy ? "actual-Army48/49/50-aggregate;subsequent-static54/55-last-primitive;node56-F8-caller-save/draw/restore" :
                "actual-static-private-layer-submission-last-primitive;zero-Army-layer-submissions;node56-F8-caller-save/draw/restore"));
        var cull = recipe.NativeCullMode switch {
            1 => RenderingDevice.PolygonCullMode.Disabled, 2 => RenderingDevice.PolygonCullMode.Front,
            3 => RenderingDevice.PolygonCullMode.Back, _ => throw new InvalidDataException("Unknown source inherited cull mode.") };
        var front = recipe.FrontCounterClockwise ? RenderingDevice.PolygonFrontFace.CounterClockwise : RenderingDevice.PolygonFrontFace.Clockwise;
        var compare = (RenderingDevice.CompareOperator)recipe.NativeDepthCompare;
        var savedCull=recipe.SavedNativeCullMode switch {
            1=>RenderingDevice.PolygonCullMode.Disabled,2=>RenderingDevice.PolygonCullMode.Front,
            3=>RenderingDevice.PolygonCullMode.Back,_=>throw new InvalidDataException("Unknown source saved cull mode.") };
        if (recipe.NativeFillMode != 3 || recipe.NativeDepthCompare > 7 || raster.Cull != savedCull || raster.FrontFace != front ||
            raster.DepthWrite != recipe.DepthWrite || raster.DepthCompare != compare)
            throw new InvalidDataException("Actual submitted raster differs from the finite original class18-to-F8 inherited state.");
        var masks = colorMasks.PrepareCurrentFullLightData(context.FrameNumber,
            accepted.Lighting.Binding.ViewIdentity, source.Context.Source.OwnedHostIdentity,
            accepted.Lighting.ContextFlags3E4, source.Context.ContextFlags3F0,
            context.TerrainFrame.ColorViews.Count, accepted.Lighting.LocalLightData.Span,
            accepted.Lighting.Binding.LightOwnerIdentity,
            worldArmy ? "actual-same-frame-full-selected-LightData;actual-geometry-Q;Army49/50-before-static54/55-and-Terrain56" :
                "actual-same-frame-full-selected-LightData;actual-current-Q;post-static-and-zero-Army-direct-F8");
        if (preparedMasks.TryGetValue(context, out var earlier))
        {
            if (earlier.Current != masks.Current) throw new InvalidDataException("This Terrain attempt changed its prepared current mask source.");
        }
        else preparedMasks.Add(context, masks);
        if (worldArmy && !preparedWorldArmies.TryGetValue(context, out _))
            preparedWorldArmies.Add(context, new(actualArmy, source));
        return new(cull, recipe.DepthTest, recipe.DepthWrite, compare)
            { FrontFace = front, PackedColorWriteMasks = masks.PackedColorWriteMasks };
    }

    public OriginalTerrainGbufferColorMaskRecipe RequirePreparedColorMask(OriginalNativeMapFrameContext actualContext)
    {
        if (!retained || !preparedMasks.TryGetValue(actualContext, out var recipe) || recipe.FrameNumber != actualContext.FrameNumber)
            throw new InvalidDataException("Current Terrain mask was not prepared from this actual same-frame submitted model roster.");
        if (preparedWorldArmies.TryGetValue(actualContext, out var army))
            _ = OriginalNativeArmyWorldAcceptedGeometry.RequireCurrent(army.Army, actualContext, army.Geometry);
        var validated = colorMasks.PrepareCurrent(recipe.Current);
        if (validated.PackedColorWriteMasks != recipe.PackedColorWriteMasks)
            throw new InvalidDataException("Prepared Terrain mask changed its exact current source predicate.");
        return recipe;
    }
}
