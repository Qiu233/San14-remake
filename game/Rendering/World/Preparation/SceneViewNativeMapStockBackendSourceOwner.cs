using Godot;
using San14.Data;

/// <summary>Joins the actual same-context Sky, submitted static/Army geometry,
/// Terrain source mask and private completed early draw roster to the retained
/// source stock fields. Preparing a packet never acknowledges a GPU copy.</summary>
public sealed class SceneViewNativeMapStockBackendSourceOwner
{
    private readonly MapCopy97CurrentStockSource source;
    private NativeMapFrameContext? preparedContext;
    private MapCopy97CurrentStockPacket? preparedPacket;
    private NativeMapArmyPopulationAcceptedFrame? preparedWorldArmy;
    private MapSourceCreatedF103CurrentFrame? preparedWorldShading;
    public MapCopy97CurrentStockSource OwnedSource => source;
    private SceneViewNativeMapStockBackendSourceOwner(MapCopy97CurrentStockSource source)
        => this.source = source;
    public static SceneViewNativeMapStockBackendSourceOwner ReadFresh(
        MapOwnedGraphicsColorMaskHistorySource actualFrontend)
        => new(MapCopy97CurrentStockSource.ReadFresh(actualFrontend));

    public (DeferredAuxiliaryStockCopyBackendState Color, DeferredAuxiliaryDepthCopyBackendState Depth)
        PrepareCurrent(MapSourceCreatedNativeSessionPreparedFrame accepted,
            MapOwnedGraphicsMaskPrefix actualCompletedSkyPrefix,
            NativeMapModelPopulationAcceptedFrame actualModels,
            NativeMapArmyPopulationAcceptedFrame actualArmy,
            TerrainGbufferColorMaskRecipe actualPreparedTerrainMask,
            DeferredEarlySourceCompletedFrame actualEarly)
    {
        var context = accepted.Context;
        var frame = accepted.PreparedSourceFrame ?? throw new InvalidDataException("Current stock fields need the complete owned source frame.");
        var geometry = accepted.ActualGeometrySourceFrame ?? throw new InvalidDataException("Current stock fields need the original geometry snapshot.");
        WorldGeometryShadingPhase.RequireSameGeometrySnapshot(geometry, frame);
        if (!ReferenceEquals(actualModels.Context, context) || !ReferenceEquals(actualArmy.Prepared.Context, context) ||
            !ReferenceEquals(actualModels.Frame.Visibility, frame.CpuPopulation?.Visibility) ||
            actualArmy.Prepared.CpuPrimitivePopulation is null || actualArmy.Prepared.Unresolved.Count != 0 ||
            actualEarly.FrameNumber != context.FrameNumber || actualEarly.CurrentColorAttachment.FrameNumber != context.FrameNumber ||
            actualEarly.Submission is not { } early)
            throw new InvalidDataException("Stock current owner needs this actual complete static/Army and completed early attempt.");
        var modelPrimitives=NativeMapStaticSubmission.RequireCurrent(actualModels,context,frame);
        if (actualArmy.WorldQueuesCompletedPublication is not null)
        {
            _ = NativeArmyWorldAcceptedGeometry.RequireCurrent(actualArmy, context, frame);
            if (actualModels.StaticQueueSubmission is null)
                throw new InvalidDataException("World stock prefix needs actual Army49/50 and subsequent static54/55 completion.");
        }
        else
        {
            if (actualArmy.Prepared.CpuPrimitivePopulation.Actors.Count != 0 || actualArmy.Prepared.OrdinaryActors != 0 ||
                actualArmy.Prepared.FlagActors != 0 || actualArmy.FlagStatuses.Count != 0 ||
                actualArmy.OrdinaryGeometrySubmission is not { } armyOrdinary || actualArmy.FlagGeometrySubmission is not { } armyFlags)
                throw new NotSupportedException("Legacy stock prefix requires the actual zero-Army layer submissions.");
            armyOrdinary.RequireCurrent(); armyFlags.RequireCurrent();
            if (!ReferenceEquals(armyOrdinary.Target, context.TerrainFrame) || !ReferenceEquals(armyFlags.Target, context.TerrainFrame) ||
                armyOrdinary.Primitives.Count != 0 || armyFlags.Primitives.Count != 0)
                throw new NotSupportedException("An intervening legacy Army primitive requires its original stock-field writer scope.");
        }
        early.RequireCurrent(context, actualEarly);
        if (modelPrimitives.Count == 0 ||
            early.ExecutedSources.Count == 0 || early.ExecutedSources.Any(s=>s.SourcePlan.Node.Index != 78) ||
            !early.EarlyCausticsByNode.TryGetValue(78, out var caustics) ||
            actualEarly.CurrentColorAttachment.Texture.StorageRid != context.OutputFrame.Textures[0x064B746B].StorageRid ||
            actualPreparedTerrainMask.FrameNumber != context.FrameNumber || actualCompletedSkyPrefix.FrameNumber != context.FrameNumber)
            throw new NotSupportedException("Stock first-source route requires actual only78, source-null Caustics and nonempty submitted static geometry.");
        foreach(var draw in early.ExecutedDraws)
        {
            var selected=draw.Source;
            var authority=draw.Caustics??throw new InvalidDataException("Actual ordinary78 draw has no Caustics authority.");
            authority.Validate(selected);selected.ValidateParameterFrame(frame.Camera,frame.Lighting);
            var first=early.ExecutedSources[0];
            if(selected.SourcePlan.Node.Index!=78||authority.SourceOwnerIdentity!=caustics.SourceOwnerIdentity||
                selected.Combination.Index!=first.Combination.Index||selected.PixelProgramSha256!=first.PixelProgramSha256||
                selected.Current.ContextFlags3F0!=first.Current.ContextFlags3F0||
                selected.Current.NativeColorMode!=first.Current.NativeColorMode||
                selected.Current.QueryD5BeA0!=first.Current.QueryD5BeA0||
                !selected.RequestedVariant96.Span.SequenceEqual(first.RequestedVariant96.Span))
                throw new NotSupportedException("Another early source changes the stock-field retention scope.");
        }
        var submitted = modelPrimitives.Select(p => p.NativeStencil?.Source ??
            throw new InvalidDataException("An actual model primitive has no final native source receipt.")).ToArray();
        var packet = source.PrepareCurrent(actualCompletedSkyPrefix, frame, submitted,
            actualPreparedTerrainMask, early.ExecutedSources[0], caustics,
            ReferenceEquals(geometry, frame) ? null : geometry);
        packet.ValidateCurrent();
        preparedContext = context;
        preparedPacket = packet;
        preparedWorldArmy = actualArmy.WorldQueuesCompletedPublication is null ? null : actualArmy;
        preparedWorldShading = preparedWorldArmy is null ? null : frame;
        var scale = packet.CurrentScreenScale29E0; var color = packet.CurrentColorScale29F0;
        var deviceScale = new Vector2(scale.X, scale.Y); var deviceColor = new Color(color.X, color.Y, color.Z, color.W);
        // Copy97's own draw source disables Blend. Its original and actual
        // destination are depth-only, while the global stencil state is unknown.
        return (new(deviceScale, deviceColor, true, packet.SourceProvenance + ";actual-Copy97-Blendfalse-source"),
            new(deviceScale, deviceColor, packet.CurrentCustomVertexShaderIsAbsent, null, packet.SourceProvenance));
    }

    public MapCopy97CurrentStockPacket RequirePreparedPacket(NativeMapFrameContext context)
    {
        if (!ReferenceEquals(context, preparedContext) || preparedPacket is not { } packet ||
            packet.FrameNumber != context.FrameNumber || !ReferenceEquals(packet.Source, source))
            throw new InvalidDataException("Stock final acknowledgement requires its actual same-context privately prepared packet.");
        if (preparedWorldArmy is { } army)
            _ = NativeArmyWorldAcceptedGeometry.RequireCurrent(army, context, preparedWorldShading!);
        packet.ValidateCurrent();
        return packet;
    }

    public void MarkFailedImportedOrUnknownWriter(string writer) => source.MarkFailedImportedOrUnknownWriter(writer);
}
