using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>Current original prefix, Common/Q and retained backend state.
/// Each producer belongs to this accepted Scene attempt.</summary>
public sealed record OriginalDeferredAuxiliarySourcePixelInputs(
    OriginalMapSourceCreatedAuxiliaryCurrentFrame Current,
    OriginalDeferredAuxiliaryPrefixPacket Prefix,
    OriginalDeferredAuxiliaryStockCopyBackendState ColorCopyBackend,
    OriginalDeferredAuxiliaryDepthCopyBackendState DepthCopyBackend,
    OriginalNativeCameraTangentDispatch CameraTangentDispatch);

/// <summary>Actual source pixel writes through node137 plus Copy97. The two
/// later mode16 geometry writers are mandatory before refraction is ready.</summary>
public sealed record OriginalDeferredAuxiliarySourcePixelStages(
    OriginalMapSourceCreatedNativeSessionPreparedFrame AcceptedFrame,
    OriginalDeferredEarlySourceCompletedFrame EarlyColor,
    OriginalDeferredAuxiliaryPrefixPacket Prefix,
    OriginalDeferredAuxiliaryDepthCopyStage RefractionCopy,
    OriginalDeferredAuxiliaryTargetDevice SelectedSpecular,
    OriginalDeferredAuxiliaryTargetDevice BlurredDepth)
{
    public bool FollowingRefractionGeometryRequired => true;
    internal OriginalDeferredAuxiliarySourcePixelInputs? AcceptedSourceInputs { get; init; }
}

/// <summary>Runs the actual current source early-color to auxiliary pixel
/// chain. Allocation, callbacks that only clear targets, and Copy97 alone do
/// not produce a completed Main Camera/refraction publication.</summary>
public static class OriginalDeferredAuxiliarySourcePixelFactory
{
    public static async Task<OriginalDeferredAuxiliarySourcePixelStages> ProducePixelStagesAsync(
        OriginalMapSourceCreatedNativeSessionPreparedFrame accepted,
        OriginalNativeMapModelPopulationAcceptedFrame population,
        OriginalNativeMapArmyPopulationAcceptedFrame army,
        OriginalDeferredEarlySourceCompletedFrame early,
        OriginalMapSourceCreatedPlayableAuxiliaryOwners owners,
        OriginalDeferredAuxiliarySourcePixelInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(accepted); ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(army); ArgumentNullException.ThrowIfNull(early);
        ArgumentNullException.ThrowIfNull(owners); ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Current); ArgumentNullException.ThrowIfNull(inputs.Prefix);
        ArgumentNullException.ThrowIfNull(inputs.ColorCopyBackend); ArgumentNullException.ThrowIfNull(inputs.DepthCopyBackend);
        var current = inputs.Current;
        var sourceFrame = accepted.PreparedSourceFrame ?? throw new InvalidDataException("Auxiliary source chain needs its complete owned current publication.");
        inputs.Prefix.Validate(sourceFrame, current.Context, inputs.Prefix.ViewGenerationIdentity);
        accepted.Selection.Validate(); accepted.Feature.ValidateFrame(accepted.MainCamera, accepted.Lighting);
        var checkedCurrent = OriginalMapSourceCreatedAuxiliaryCurrentFactory.Prepare(accepted, current.Common,
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
        if (current.FilterType90 == 1)
        {
            if (current.SampleCount94 is < 1 or > 7)
                throw new NotSupportedException("Current source Gaussian size has no compiled original variant.");
            _ = OriginalDeferredAuxiliaryGaussianSchedule.ReadSpecular(owners.Source, transparency,
                current.ContextFlags3F0, (uint)current.SampleCount94, current.Common.GaussianDispersion9C);
        }
        else if (current.FilterType90 is 2 or 3)
            _ = OriginalDeferredAuxiliaryGgxSchedule.Read(owners.Source, transparency, current.ContextFlags3F0,
                current.FilterType90, current.SampleCount94, current.CameraFov148, current.CameraAspect14C, inputs.CameraTangentDispatch);
        else throw new NotSupportedException("Current source specular filter requires an unimplemented source branch.");
        _ = OriginalDeferredAuxiliaryGaussianSchedule.ReadDepth(owners.Source, current.ContextFlags3F0, (uint)depthMode);
        if (!inputs.ColorCopyBackend.CurrentBlendIsDisabled || !inputs.DepthCopyBackend.CurrentCustomVertexShaderIsAbsent ||
            inputs.ColorCopyBackend.CurrentScreenScale29E0.X < 1 || inputs.ColorCopyBackend.CurrentScreenScale29E0.Y < 1 ||
            inputs.DepthCopyBackend.CurrentScreenScale29E0.X < 1 || inputs.DepthCopyBackend.CurrentScreenScale29E0.Y < 1)
            throw new NotSupportedException("Auxiliary stock passes need their current full-coverage source backend state.");
        _ = OriginalDeferredAuxiliaryDepthCopyStencilAdmission.ForSourceTarget(owners.RefractionDepth.Source,
            owners.RefractionDepth.TargetFormat, inputs.DepthCopyBackend.CurrentGlobalStencilIsDisabled);
        _ = OriginalDeferredAuxiliaryStockQuad.FromCurrentViewport(selectedSpecular.ExplicitExtent,
            inputs.ColorCopyBackend.CurrentScreenScale29E0, inputs.ColorCopyBackend.CurrentColorScale29F0,
            inputs.ColorCopyBackend.CurrentBackendProducerProvenance);
        _ = OriginalDeferredAuxiliaryStockQuad.FromCurrentViewport(owners.RefractionDepth.ExplicitExtent,
            inputs.DepthCopyBackend.CurrentScreenScale29E0, inputs.DepthCopyBackend.CurrentColorScale29F0,
            inputs.DepthCopyBackend.CurrentBackendProducerProvenance);

        await owners.StockCopy.CopyCurrentColorToSpecularMipZeroAsync(accepted.Selection, transparency,
            early.CurrentColorAttachment, selectedSpecular, inputs.ColorCopyBackend);
        if (current.FilterType90 == 1)
            await owners.Filters.ProduceGaussianSpecularMipsAsync(new(accepted.Selection, transparency,
                current.ContextFlags3F0, (uint)current.SampleCount94, current.Common.GaussianDispersion9C,
                current.SourceProvenance), owners.Devices);
        else await owners.Filters.ProduceSpecularMipsAsync(new(accepted.Selection, transparency,
            current.ContextFlags3F0, current.FilterType90, current.SampleCount94, inputs.CameraTangentDispatch,
            current.SourceProvenance), owners.Devices);
        string geometryProducer = "accepted ordered source geometry/Army; frame=" + frame + ";" + producer;
        await owners.Filters.ProduceBlurredDepthAsync(accepted.Selection, current.ContextFlags3F0, (uint)depthMode,
            mainDepth, owners.Devices, geometryProducer);
        var copy = await owners.DepthCopy.CopyDepthAsync(accepted.Selection, mainDepth, owners.RefractionDepth,
            inputs.DepthCopyBackend, geometryProducer);
        return new(accepted, early, inputs.Prefix, copy, selectedSpecular, blurred){AcceptedSourceInputs=inputs};
    }
}
