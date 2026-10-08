using San14.Data;

/// <summary>The current source Sky1B20 selection, Common activation and fresh
/// WORK/Scene gates. Queue selection is verified independently of Terrain1BD0.
/// Optional Star/Cloud packets and completed GPU work remain separate owners.</summary>
public sealed class OriginalSceneViewNativeMapSkyCurrentSelectionOwner
{
    private readonly OriginalSkyAppliedSourceStageOwner stages;
    private readonly OriginalMapLightingBinding binding;
    private OriginalSceneViewNativeMapSkyCurrentSelection? current;
    private bool retained = true;

    private OriginalSceneViewNativeMapSkyCurrentSelectionOwner(OriginalSkyAppliedSourceStageOwner stages,
        OriginalMapLightingBinding binding) { this.stages = stages; this.binding = binding; }

    public static OriginalSceneViewNativeMapSkyCurrentSelectionOwner CreateFresh(
        OriginalGameRepository repository, string locale, OriginalMapLightingBinding actualBinding,
        string ownedWorkIdentity)
        => new(OriginalSkyAppliedSourceStageOwner.CreateFresh(repository, locale, ownedWorkIdentity,
            actualBinding.CameraOwnerIdentity), actualBinding);

    public OriginalSceneViewNativeMapSkyCurrentSelection PrepareCurrent(
        OriginalMapSourceCreatedF103CurrentFrame actualSource,
        OriginalMapSourceCreatedSeasonLightingFrame actualPresent,
        OriginalSkySeasonMainCameraRgbFrame actualRgb,
        OriginalMainSceneNode51Execution actualViewExecution)
    {
        ArgumentNullException.ThrowIfNull(actualSource); ArgumentNullException.ThrowIfNull(actualPresent);
        ArgumentNullException.ThrowIfNull(actualRgb); ArgumentNullException.ThrowIfNull(actualViewExecution);
        if (!retained || actualSource.Lighting.Binding != binding)
            throw new InvalidDataException("Sky selection owner was edited or belongs to another current Main/View.");
        actualSource.Selection.Validate(); actualSource.Feature.ValidateFrame(actualSource.Camera, actualSource.Lighting);
        actualViewExecution.RequireSelectedView(actualSource.FrameNumber, binding);
        if (actualViewExecution.SelectedViewIndex != 0 || actualRgb.Inputs.FrameNumber != actualSource.FrameNumber ||
            !ReferenceEquals(actualRgb, actualPresent.AcceptedSeasonCamera))
            throw new NotSupportedException("Initial Sky source requires the actual same-frame primary owned View and accepted RGB/Present pair.");
        var common = actualSource.Common;
        common.ValidateFrame(actualSource.FrameNumber, common.CommonTableOwnerIdentity, binding.ViewIdentity);
        if (common.CurrentViewAux3F2 is not byte selector || selector is < 1 or > 32)
            throw new InvalidDataException("Sky1B20 needs its actual current Common3F2 selector.");
        var record = actualSource.ViewFields.CurrentPublication.SelectedShadowRecord1BD0 ??
            throw new InvalidDataException("This finite same-record source branch requires its complete owned current kind1 record.");
        var masks = OriginalMapSourceCreatedViewFields.SourceKind1RecordMasks(actualSource.Lighting, selector);
        uint required = 1u << (selector - 1);
        int selected = Enumerable.Range(0, masks.Count).FirstOrDefault(i => (masks[i] & ~required) == 0, -1);
        if (selected < 0 || record.CurrentQueueIndex != selected || record.RecordWord78 != masks[selected] ||
            record.FrameNumber != actualSource.FrameNumber || record.ViewIdentity != binding.ViewIdentity ||
            record.CameraOwnerIdentity != binding.CameraOwnerIdentity || record.LightOwnerIdentity != binding.LightOwnerIdentity ||
            !record.CompleteLightData.Span.SequenceEqual(actualSource.Lighting.LocalLightData.Span))
            throw new InvalidDataException("Terrain's selected record is not the independently computed first current Sky1B20 subset-mask record.");
        var enable = OriginalSkyAmbientSourceEnableInputs.PrepareCurrentSourcePresentTable(actualRgb, common, binding.ViewIdentity);
        var stage = stages.PrepareCurrent(actualPresent, actualSource.Lighting);
        stage.ValidateCurrent(actualSource.Lighting, actualPresent.AcceptedSeasonCamera.Inputs.Source.Selection,
            actualPresent.CurrentSkyCamera);
        return current = new(this, actualSource, actualPresent, actualRgb, actualViewExecution, record, enable, stage);
    }

    internal void RequireCurrent(OriginalSceneViewNativeMapSkyCurrentSelection actual)
    {
        if (!retained || !ReferenceEquals(actual, current))
            throw new InvalidDataException("Sky selection receipt is superseded, edited or belongs to another source owner.");
        actual.Source.Selection.Validate(); actual.Source.Feature.ValidateFrame(actual.Source.Camera, actual.Source.Lighting);
        if (!ReferenceEquals(actual.SelectedSkyRecord, actual.Source.ViewFields.CurrentPublication.SelectedShadowRecord1BD0) ||
            !actual.SelectedSkyRecord.CompleteLightData.Span.SequenceEqual(actual.Source.Lighting.LocalLightData.Span))
            throw new InvalidDataException("Current Sky record was replaced after this accepted source selection.");
        actual.ViewExecution.RequireSelectedView(actual.FrameNumber, binding);
        actual.Stages.ValidateCurrent(actual.Source.Lighting, actual.Present.AcceptedSeasonCamera.Inputs.Source.Selection,
            actual.Present.CurrentSkyCamera);
    }

    public void MarkImportedEditedOrRebound(string writer)
    { ArgumentException.ThrowIfNullOrWhiteSpace(writer); retained = false; current = null; }
}

public sealed class OriginalSceneViewNativeMapSkyCurrentSelection
{
    private readonly OriginalSceneViewNativeMapSkyCurrentSelectionOwner owner;
    internal OriginalSceneViewNativeMapSkyCurrentSelection(OriginalSceneViewNativeMapSkyCurrentSelectionOwner owner,
        OriginalMapSourceCreatedF103CurrentFrame source, OriginalMapSourceCreatedSeasonLightingFrame present,
        OriginalSkySeasonMainCameraRgbFrame rgb, OriginalMainSceneNode51Execution execution,
        OriginalTerrainSelectedKind1Record record, OriginalSkyAmbientSourceEnableInputs enable,
        OriginalSkyAppliedSourceStageFrame stages)
    { this.owner = owner; Source = source; Present = present; Rgb = rgb; ViewExecution = execution;
      SelectedSkyRecord = record; Enable = enable; Stages = stages; }
    public OriginalMapSourceCreatedF103CurrentFrame Source { get; }
    public OriginalMapSourceCreatedSeasonLightingFrame Present { get; }
    public OriginalSkySeasonMainCameraRgbFrame Rgb { get; }
    public OriginalMainSceneNode51Execution ViewExecution { get; }
    public OriginalTerrainSelectedKind1Record SelectedSkyRecord { get; }
    public OriginalSkyAmbientSourceEnableInputs Enable { get; }
    public OriginalSkyAppliedSourceStageFrame Stages { get; }
    public long FrameNumber => Source.FrameNumber;
    public void RequireCurrent() => owner.RequireCurrent(this);
}
