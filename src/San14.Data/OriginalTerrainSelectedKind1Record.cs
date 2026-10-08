namespace San14.Data;

/// <summary>One owned current plain kind1 record selected at View1BD0, with
/// its complete record+8 payload. Record identity is independent of its
/// publisher/creator; an observed prefix cannot construct this packet.</summary>
public sealed class OriginalTerrainSelectedKind1Record
{
    private readonly byte[] lightData;
    private readonly OriginalTerrainCurrentKind1WorldPQPublication world;
    private OriginalTerrainSelectedKind1Record(OriginalMapSourceCreatedLightingPacket lighting,
        OriginalTerrainCurrentKind1WorldPQPublication selected,
        OriginalSkySceneSelectedViewLightPacket payload, uint mask1BF8, string provenance)
    {
        FrameNumber = lighting.FrameNumber; ViewIdentity = lighting.Binding.ViewIdentity;
        CameraOwnerIdentity = lighting.Binding.CameraOwnerIdentity;
        LightOwnerIdentity = lighting.Binding.LightOwnerIdentity;
        RecordOwnerIdentity = selected.CurrentWorldPQOwnerIdentity;
        PayloadOwnerIdentity = payload.Identity.ProviderPacketOwner;
        lightData = payload.CompleteLightData.ToArray(); world = selected;
        ViewShadowMask1BF8 = mask1BF8;
        SourceProvenance = provenance + ";owned-current-View1BD0-kind1-record=" + RecordOwnerIdentity +
            ";record8=" + PayloadOwnerIdentity + ";D78BD2-selection;D5D217/D5D227-record8;" +
            "record-identity-does-not-identify-current-creator;" + selected.CurrentProducerProvenance + ";" + payload.SourceProvenance;
    }
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string CameraOwnerIdentity { get; }
    public string LightOwnerIdentity { get; }
    public string RecordOwnerIdentity { get; }
    public string PayloadOwnerIdentity { get; }
    public uint RecordWord78 => world.CurrentWorldPQWord78;
    public uint ViewShadowMask1BF8 { get; }
    public int CurrentQueueIndex => world.CurrentItemIndex;
    public ReadOnlyMemory<byte> CompleteLightData => lightData;
    public string SourceProvenance { get; }
    public bool OriginalPublisherCreatorObserved => false;

    public static OriginalTerrainSelectedKind1Record FromOwnedCurrentPublication(
        OriginalMapSourceCreatedLightingPacket lighting,
        OriginalTerrainCurrentKind1WorldPQPublication selectedWorldPQ,
        OriginalSkySceneSelectedViewLightPacket explicitCompleteCurrentPayload,
        uint actualViewShadowMask1BF8, string currentSelectionProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(lighting); ArgumentNullException.ThrowIfNull(selectedWorldPQ);
        ArgumentNullException.ThrowIfNull(explicitCompleteCurrentPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSelectionProducerProvenance);
        var identity = explicitCompleteCurrentPayload.Identity;
        if (lighting.FrameNumber <= 0 || selectedWorldPQ.FrameNumber != lighting.FrameNumber ||
            selectedWorldPQ.ViewIdentity != lighting.Binding.ViewIdentity || selectedWorldPQ.CurrentItemIndex < 0 ||
            selectedWorldPQ.CurrentItemTagWord >> 28 != 1 || selectedWorldPQ.CurrentItemPlus8AndWorldPQWord78Retained != true ||
            identity.FrameNumber != lighting.FrameNumber || identity.SelectedViewOwner != lighting.Binding.ViewIdentity ||
            identity.ProviderRecordOwner != selectedWorldPQ.CurrentWorldPQOwnerIdentity ||
            identity.ProviderPacketOwner != lighting.Binding.LightOwnerIdentity ||
            selectedWorldPQ.CurrentWorldPQOwnerIdentity == lighting.Binding.LightOwnerIdentity ||
            new[] { selectedWorldPQ.CurrentQueueOwnerIdentity, selectedWorldPQ.CurrentItemIdentity,
                selectedWorldPQ.CurrentWorldPQOwnerIdentity, selectedWorldPQ.CurrentProducerProvenance }.Any(string.IsNullOrWhiteSpace) ||
            new[] { selectedWorldPQ.CurrentItemIdentity, selectedWorldPQ.CurrentWorldPQOwnerIdentity,
                identity.ProviderPacketOwner }.Any(value => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) ||
            explicitCompleteCurrentPayload.CompleteLightData.Length != OriginalCameraDirectionalLightData.LightDataBytes ||
            !explicitCompleteCurrentPayload.CompleteLightData.Span.SequenceEqual(lighting.LocalLightData.Span))
            throw new InvalidDataException("Selected Terrain shadow record requires the same owned frame/View, independent plain record and complete current10C0 payload; process-address owners or prefixes are not accepted.");
        return new(lighting, selectedWorldPQ, explicitCompleteCurrentPayload, actualViewShadowMask1BF8,
            currentSelectionProducerProvenance);
    }
    internal void ValidateFor(OriginalMapSourceCreatedLightingPacket lighting)
    {
        if (FrameNumber != lighting.FrameNumber || ViewIdentity != lighting.Binding.ViewIdentity ||
            CameraOwnerIdentity != lighting.Binding.CameraOwnerIdentity || LightOwnerIdentity != lighting.Binding.LightOwnerIdentity ||
            !lightData.AsSpan().SequenceEqual(lighting.LocalLightData.Span))
            throw new InvalidDataException("Selected kind1 record belongs to another current Camera/light generation.");
    }
}
