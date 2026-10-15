using Godot;
using San14.Data;

/// <summary>Current original prefix, Common/Q and retained backend state.
/// Each producer belongs to this accepted Scene attempt.</summary>
public sealed record DeferredAuxiliarySourcePixelInputs(
    MapSourceCreatedAuxiliaryCurrentFrame Current,
    DeferredAuxiliaryPrefixPacket Prefix,
    DeferredAuxiliaryStockCopyBackendState ColorCopyBackend,
    DeferredAuxiliaryDepthCopyBackendState DepthCopyBackend,
    NativeCameraTangentDispatch CameraTangentDispatch);

/// <summary>Actual source pixel writes through node137 plus Copy97. The two
/// later mode16 geometry writers are mandatory before refraction is ready.</summary>
public sealed record DeferredAuxiliarySourcePixelStages(
    MapSourceCreatedNativeSessionPreparedFrame AcceptedFrame,
    DeferredEarlySourceCompletedFrame EarlyColor,
    DeferredAuxiliaryPrefixPacket Prefix,
    DeferredAuxiliaryDepthCopyStage RefractionCopy,
    DeferredAuxiliaryTargetDevice SelectedSpecular,
    DeferredAuxiliaryTargetDevice BlurredDepth)
{
    public bool FollowingRefractionGeometryRequired => true;
    internal DeferredAuxiliarySourcePixelInputs? AcceptedSourceInputs { get; init; }
}

/// <summary>Runs the actual current source early-color to auxiliary pixel
/// chain. Allocation, callbacks that only clear targets, and Copy97 alone do
/// not produce a completed Main Camera/refraction publication.</summary>
public static class DeferredAuxiliarySourcePixelFactory
{
    public static async Task<DeferredAuxiliarySourcePixelStages> ProducePixelStagesAsync(
        MapSourceCreatedNativeSessionPreparedFrame accepted,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army,
        DeferredEarlySourceCompletedFrame early,
        MapSourceCreatedPlayableAuxiliaryOwners owners,
        DeferredAuxiliarySourcePixelInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(accepted); ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(early);
        ArgumentNullException.ThrowIfNull(owners); ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Current); ArgumentNullException.ThrowIfNull(inputs.Prefix);
        ArgumentNullException.ThrowIfNull(inputs.ColorCopyBackend); ArgumentNullException.ThrowIfNull(inputs.DepthCopyBackend);
        var current = inputs.Current;
        var sourceFrame = accepted.PreparedSourceFrame ?? throw new InvalidDataException("Auxiliary source chain needs its complete owned current publication.");
        inputs.Prefix.Validate(sourceFrame, current.Context, inputs.Prefix.ViewGenerationIdentity);
         accepted.Feature.ValidateFrame(accepted.MainCamera, accepted.Lighting);
        var checkedCurrent = MapSourceCreatedAuxiliaryCurrentFactory.Prepare(accepted, current.Common,
            current.Context, current.CommonSettingsOwnerIdentity);
        long frame = accepted.Context.FrameNumber;
        string producer = accepted.Selection.Current.CurrentProducerProvenance;
        if (!ReferenceEquals(population.Context, accepted.Context) || !ReferenceEquals(army.Prepared.Context, accepted.Context) ||
            sourceFrame.CpuPopulation is null || !ReferenceEquals(population.Frame.Visibility, sourceFrame.CpuPopulation.Visibility) ||
            current.FrameNumber != frame || current.Context.FrameNumber != frame ||
            current.CameraFov148 != checkedCurrent.CameraFov148 || current.CameraAspect14C != checkedCurrent.CameraAspect14C ||
            current.CameraFilters.CameraDepthFilterTypeEDC != checkedCurrent.CameraFilters.CameraDepthFilterTypeEDC ||
            current.CameraFilters.CameraTransparencyResolutionED8 != checkedCurrent.CameraFilters.CameraTransparencyResolutionED8 ||
            current.Common.CurrentViewOwnerIdentity != accepted.Feature.CurrentView.ViewIdentity ||
            early.FrameNumber != frame || early.CurrentProducerProvenance != producer ||
            early.CurrentColorAttachment.FrameNumber != frame || early.CurrentColorAttachment.FrameProducerProvenance != producer ||
            early.CurrentColorAttachment.Texture.StorageRid != accepted.Context.OutputFrame.Textures[0x064B746B].StorageRid ||
            early.CurrentColorAttachment.Texture.Source.Object.ObjectId != inputs.Prefix.SourceOuterColorObjectId ||
            early.CurrentColorAttachment.BoundAttachmentIndex != inputs.Prefix.SourceOuterColorSurface ||
            early.CurrentColorAttachment.CurrentWriterReceipt.WriterNodeIndex is < 77 or > 80 ||
            early.CompletedBins.Count == 0 ||
            owners.Source.Graph.Source.DecodedSha256 != accepted.Selection.SourcePlan.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Auxiliary pixels require the exact accepted geometry/Army, earlier color writer and current source prefix.");
        if (!current.ThinSurfaceSourceGate)
            throw new NotSupportedException("Actual source node137 does not execute at this current thin-surface boundary.");

        current.CameraFilters.RequireSameMain(accepted.CpuPopulation!.MainCamera);
        var transparency=current.CameraFilters.ReadTransparencySelection(current.TransparencyResolution8C);
        var specularSource = owners.Source.SelectSpecular(transparency);
        var selectedSpecular = owners.Devices.Single(d => d.Source.Object.ObjectId == specularSource.Texture.Object.ObjectId);
        var blurred = owners.Devices.Single(d => d.Source.Object.ObjectId == owners.Source.BlurredDepth.Texture.Object.ObjectId);
        var mainDepth = accepted.Context.TerrainFrame.Textures[0xE13DDB4F];
        int depthMode=current.CameraFilters.ReadDepthFilterType(current.DepthFilterType98);
        // Resolve the entire finite source schedule before any auxiliary pixel
        // write. Unsupported current modes do not reuse previous-frame content.
        if (depthMode != 1)
            throw new NotSupportedException("Current native depth filter requires an unimplemented source branch.");
        DeferredAuxiliaryFilterPreparation specular;
        if (current.FilterType90 == 1)
        {
            if (current.SampleCount94 is < 1 or > 7)
                throw new NotSupportedException("Current source Gaussian size has no compiled original variant.");
            specular = DeferredAuxiliaryFilterPreparation.Gaussian(owners.Filters.Source,
                new(accepted.Selection, transparency, current.ContextFlags3F0, (uint)current.SampleCount94,
                    current.Common.GaussianDispersion9C, current.SourceProvenance));
        }
        else if (current.FilterType90 is 2 or 3)
            specular = DeferredAuxiliaryFilterPreparation.Ggx(owners.Filters.Source,
                new(accepted.Selection, transparency, current.ContextFlags3F0, current.FilterType90,
                    current.SampleCount94, inputs.CameraTangentDispatch, current.SourceProvenance));
        else throw new NotSupportedException("Current source specular filter requires an unimplemented source branch.");
        string geometryProducer = "accepted ordered source geometry/Army; frame=" + frame + ";" + producer;
        var depth = DeferredAuxiliaryFilterPreparation.Depth(owners.Filters.Source, accepted.Selection,
            current.ContextFlags3F0, (uint)depthMode, geometryProducer);
        if (!inputs.ColorCopyBackend.CurrentBlendIsDisabled || !inputs.DepthCopyBackend.CurrentCustomVertexShaderIsAbsent ||
            inputs.ColorCopyBackend.CurrentScreenScale29E0.X < 1 || inputs.ColorCopyBackend.CurrentScreenScale29E0.Y < 1 ||
            inputs.DepthCopyBackend.CurrentScreenScale29E0.X < 1 || inputs.DepthCopyBackend.CurrentScreenScale29E0.Y < 1)
            throw new NotSupportedException("Auxiliary stock passes need their current full-coverage source backend state.");
        _ = DeferredAuxiliaryDepthCopyStencilAdmission.ForSourceTarget(owners.RefractionDepth.Source,
            owners.RefractionDepth.TargetFormat, inputs.DepthCopyBackend.CurrentGlobalStencilIsDisabled);
        _ = DeferredAuxiliaryStockQuad.FromCurrentViewport(selectedSpecular.ExplicitExtent,
            inputs.ColorCopyBackend.CurrentScreenScale29E0, inputs.ColorCopyBackend.CurrentColorScale29F0,
            inputs.ColorCopyBackend.CurrentBackendProducerProvenance);
        _ = DeferredAuxiliaryStockQuad.FromCurrentViewport(owners.RefractionDepth.ExplicitExtent,
            inputs.DepthCopyBackend.CurrentScreenScale29E0, inputs.DepthCopyBackend.CurrentColorScale29F0,
            inputs.DepthCopyBackend.CurrentBackendProducerProvenance);

        await owners.StockCopy.CopyCurrentColorToSpecularMipZeroAsync(accepted.Selection, transparency,
            early.CurrentColorAttachment, selectedSpecular, inputs.ColorCopyBackend);
        await owners.Filters.ProducePreparedAsync(specular, owners.Devices);
        await owners.Filters.ProducePreparedAsync(depth, owners.Devices, mainDepth);
        var copy = await owners.DepthCopy.CopyDepthAsync(accepted.Selection, mainDepth, owners.RefractionDepth,
            inputs.DepthCopyBackend, geometryProducer);
        return new(accepted, early, inputs.Prefix, copy, selectedSpecular, blurred){AcceptedSourceInputs=inputs};
    }
}
