using San14.Data;

/// <summary>Captures the scene's actual detached BeforeFrame terrain inventory.
/// It publishes CPU View fields; it does not certify GPU stencil or pixels.</summary>
public sealed class OriginalNativeTerrainCurrentViewOwner
{
    private readonly OriginalTerrainViewPublicationSource source;
    private readonly LoadedTerrainGbiMapResources material;
    private bool retained = true;
    private OriginalNativeTerrainCurrentViewFrame? currentFrame;
    private OriginalNativeTerrainCurrentViewOwner(LoadedTerrainGbiMapResources material)
    { this.material = material; source = OriginalTerrainViewPublicationSource.ReadFieldScene(material.Parameters); }
    public string OwnerIdentity { get; } = "owned-current-Landscape:" + Guid.NewGuid().ToString("N");
    public static OriginalNativeTerrainCurrentViewOwner CreateFreshFieldSceneNoExternalAssignments(LoadedTerrainGbiMapResources material)
        => new(material);
    public void MarkImportedEditedOrUnresolved(string writer)
    { ArgumentException.ThrowIfNullOrWhiteSpace(writer); retained = false; currentFrame = null; }
    public OriginalNativeTerrainCurrentViewFrame Prepare(OriginalNativeMapFrameContext context,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalMapSourceCreatedViewPublicationState current)
    {
        currentFrame = null;
        RequireCurrent();
        var batches = context.AcceptedTerrainBatches ?? throw new InvalidDataException("Terrain View owner requires the scene's accepted BeforeFrame batches.");
        if (!ReferenceEquals(context.AcceptedTerrainMaterial, material) || context.PreparedCamera is null || context.FrameNumber != lighting.FrameNumber ||
            current.FrameNumber != context.FrameNumber || current.SelectedShadowWrapper1BD0Present != false ||
            current.OtherAdditionalOwners1B80_1BB0_1B98Absent != true || current.Terrain1BB8 is not null)
            throw new InvalidDataException("Terrain View owner requires the same prepared camera/frame and explicit current no-shadow/other-owner branch.");
        var publication = OriginalTerrainCurrentViewPublication.PrepareWithoutSelectedShadowWrapper(source, lighting,
            batches.SelectMany(b => b.Patches).ToArray(), OwnerIdentity + "/accepted-BeforeFrame:" + context.FrameNumber);
        return currentFrame = new(this, context, batches, current with { AdditionalOwners1B80_1BB0_1BB8_1B98Absent = false, Terrain1BB8 = publication });
    }
    public OriginalNativeTerrainCurrentViewFrame PrepareWithSelectedKind1Record(OriginalNativeMapFrameContext context,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalMapSourceCreatedViewPublicationState current,
        OriginalTerrainSelectedKind1Record selectedRecord, OriginalMapFreshCommonResourceSnapshot currentCommon)
    {
        currentFrame = null; RequireCurrent(); ArgumentNullException.ThrowIfNull(selectedRecord);
        ArgumentNullException.ThrowIfNull(currentCommon);
        var batches = context.AcceptedTerrainBatches ?? throw new InvalidDataException("Present selected Terrain owner requires the accepted BeforeFrame terrain batches.");
        if (!ReferenceEquals(context.AcceptedTerrainMaterial, material) || context.PreparedCamera is null ||
            context.FrameNumber != lighting.FrameNumber || current.FrameNumber != context.FrameNumber ||
            current.ViewIdentity != lighting.Binding.ViewIdentity || current.CameraOwnerIdentity != lighting.Binding.CameraOwnerIdentity ||
            current.SelectedShadowWrapper1BD0Present != true || current.OtherAdditionalOwners1B80_1BB0_1B98Absent != true ||
            current.Terrain1BB8 is not null || current.SelectedShadowRecord1BD0 is not null &&
                !ReferenceEquals(current.SelectedShadowRecord1BD0, selectedRecord))
            throw new InvalidDataException("Present selected Terrain record requires this actual material, Camera/frame and explicit current nonnull1BD0 owner branch.");
        var publication = OriginalTerrainCurrentViewPublication.PrepareWithSelectedKind1Record(source, lighting,
            batches.SelectMany(b => b.Patches).ToArray(), selectedRecord, currentCommon,
            OwnerIdentity + "/accepted-BeforeFrame-selected-kind1:" + context.FrameNumber);
        return currentFrame = new(this, context, batches, current with {
            AdditionalOwners1B80_1BB0_1BB8_1B98Absent = false,
            Terrain1BB8 = publication, SelectedShadowRecord1BD0 = selectedRecord }, currentCommon);
    }
    internal void RequireCurrent()
    { if (!retained) throw new InvalidDataException("Terrain View owner was edited, imported or unresolved."); source.ValidateSourceUnchanged(); }
    internal void RequireCurrent(OriginalNativeTerrainCurrentViewFrame frame)
    { RequireCurrent(); if (!ReferenceEquals(currentFrame, frame)) throw new InvalidDataException("Terrain View publication was superseded or its next preparation failed."); }
}

public sealed class OriginalNativeTerrainCurrentViewFrame
{
    private readonly OriginalNativeTerrainCurrentViewOwner owner;
    private readonly IReadOnlyList<OriginalNativeTerrainBatch> batches;
    private readonly (bool Coarse, TerrainPatchInput[] Patches)[] snapshot;
    private readonly OriginalMapFreshCommonResourceSnapshot? selectedShadowCommon;
    internal OriginalNativeTerrainCurrentViewFrame(OriginalNativeTerrainCurrentViewOwner owner,
        OriginalNativeMapFrameContext context, IReadOnlyList<OriginalNativeTerrainBatch> batches,
        OriginalMapSourceCreatedViewPublicationState publication, OriginalMapFreshCommonResourceSnapshot? selectedShadowCommon = null)
    { this.owner = owner; Context = context; this.batches = batches; CurrentViewPublication = publication;
      this.selectedShadowCommon = selectedShadowCommon;
      snapshot = batches.Select(b => (b.Coarse, b.Patches.ToArray())).ToArray(); }
    public OriginalNativeMapFrameContext Context { get; }
    public OriginalMapSourceCreatedViewPublicationState CurrentViewPublication { get; }
    public void ValidateCurrent(OriginalNativeMapFrameContext actualContext, OriginalMapSourceCreatedF103CurrentFrame frame)
    {
        owner.RequireCurrent(this);
        if (!ReferenceEquals(actualContext, Context) || !ReferenceEquals(actualContext.AcceptedTerrainBatches, batches) ||
            frame.FrameNumber != Context.FrameNumber ||
            !ReferenceEquals(frame.ViewFields.CurrentPublication.Terrain1BB8, CurrentViewPublication.Terrain1BB8) ||
            !ReferenceEquals(frame.ViewFields.CurrentPublication.SelectedShadowRecord1BD0, CurrentViewPublication.SelectedShadowRecord1BD0) ||
            selectedShadowCommon is not null && !ReferenceEquals(frame.Common, selectedShadowCommon) ||
            batches.Count != snapshot.Length || batches.Where((b, i) => b.Coarse != snapshot[i].Coarse || !b.Patches.SequenceEqual(snapshot[i].Patches)).Any())
            throw new InvalidDataException("Terrain View publication no longer matches this actual accepted scene frame and patch inventory.");
    }
}
