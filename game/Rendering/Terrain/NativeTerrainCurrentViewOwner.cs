using San14.Data;

/// <summary>Captures the scene's actual detached BeforeFrame terrain inventory.
/// It publishes CPU View fields; it does not certify GPU stencil or pixels.</summary>
public sealed class NativeTerrainCurrentViewOwner
{
    private readonly TerrainViewPublicationSource source;
    private readonly LoadedTerrainGbiMapResources material;
    private bool retained = true;
    private NativeTerrainCurrentViewFrame? currentFrame;
    private NativeTerrainCurrentViewOwner(LoadedTerrainGbiMapResources material)
    { this.material = material; source = TerrainViewPublicationSource.ReadFieldScene(material.Parameters); }
    public string OwnerIdentity { get; } = "owned-current-Landscape:" + Guid.NewGuid().ToString("N");
    public static NativeTerrainCurrentViewOwner CreateFreshFieldSceneNoExternalAssignments(LoadedTerrainGbiMapResources material)
        => new(material);
    public void MarkImportedEditedOrUnresolved(string writer)
    { ArgumentException.ThrowIfNullOrWhiteSpace(writer); retained = false; currentFrame = null; }
    public NativeTerrainCurrentViewFrame Prepare(NativeMapFrameContext context,
        MapSourceCreatedLightingPacket lighting, MapSourceCreatedViewPublicationState current)
    {
        currentFrame = null;
        RequireCurrent();
        var batches = context.AcceptedTerrainBatches ?? throw new InvalidDataException("Terrain View owner requires the scene's accepted BeforeFrame batches.");
        if (!ReferenceEquals(context.AcceptedTerrainMaterial, material) || context.PreparedCamera is null || context.FrameNumber != lighting.FrameNumber ||
            current.FrameNumber != context.FrameNumber || current.SelectedShadowWrapper1BD0Present != false ||
            current.OtherAdditionalOwners1B80_1BB0_1B98Absent != true || current.Terrain1BB8 is not null)
            throw new InvalidDataException("Terrain View owner requires the same prepared camera/frame and explicit current no-shadow/other-owner branch.");
        var publication = TerrainCurrentViewPublication.PrepareWithoutSelectedShadowWrapper(source, lighting,
            batches.SelectMany(b => b.Patches).ToArray(), OwnerIdentity + "/accepted-BeforeFrame:" + context.FrameNumber);
        return currentFrame = new(this, context, batches, current with { AdditionalOwners1B80_1BB0_1BB8_1B98Absent = false, Terrain1BB8 = publication });
    }
    public NativeTerrainCurrentViewFrame PrepareWithSelectedKind1Record(NativeMapFrameContext context,
        MapSourceCreatedLightingPacket lighting, MapSourceCreatedViewPublicationState current,
        TerrainSelectedKind1Record selectedRecord, MapCommonRenderState currentCommon)
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
        var publication = TerrainCurrentViewPublication.PrepareWithSelectedKind1Record(source, lighting,
            batches.SelectMany(b => b.Patches).ToArray(), selectedRecord, currentCommon,
            OwnerIdentity + "/accepted-BeforeFrame-selected-kind1:" + context.FrameNumber);
        return currentFrame = new(this, context, batches, current with {
            AdditionalOwners1B80_1BB0_1BB8_1B98Absent = false,
            Terrain1BB8 = publication, SelectedShadowRecord1BD0 = selectedRecord }, currentCommon);
    }
    internal void RequireCurrent()
    { if (!retained) throw new InvalidDataException("Terrain View owner was edited, imported or unresolved."); }
    internal void RequireCurrent(NativeTerrainCurrentViewFrame frame)
    { RequireCurrent(); if (!ReferenceEquals(currentFrame, frame)) throw new InvalidDataException("Terrain View publication was superseded or its next preparation failed."); }
}

public sealed class NativeTerrainCurrentViewFrame
{
    private readonly NativeTerrainCurrentViewOwner owner;
    private readonly IReadOnlyList<NativeTerrainBatch> batches;
    private readonly (bool Coarse, TerrainPatchInput[] Patches)[] snapshot;
    private readonly MapCommonRenderState? selectedShadowCommon;
    internal NativeTerrainCurrentViewFrame(NativeTerrainCurrentViewOwner owner,
        NativeMapFrameContext context, IReadOnlyList<NativeTerrainBatch> batches,
        MapSourceCreatedViewPublicationState publication, MapCommonRenderState? selectedShadowCommon = null)
    { this.owner = owner; Context = context; this.batches = batches; CurrentViewPublication = publication;
      this.selectedShadowCommon = selectedShadowCommon;
      snapshot = batches.Select(b => (b.Coarse, b.Patches.ToArray())).ToArray(); }
    public NativeMapFrameContext Context { get; }
    public MapSourceCreatedViewPublicationState CurrentViewPublication { get; }
    public void ValidateCurrent(NativeMapFrameContext actualContext, MapSourceCreatedF103CurrentFrame frame)
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
