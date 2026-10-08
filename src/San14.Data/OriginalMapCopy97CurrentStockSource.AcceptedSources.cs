namespace San14.Data;

public sealed partial class OriginalMapCopy97CurrentStockSource
{
    private AcceptedStockSources? acceptedStockSources;
    private long fullNativeSemanticSourceValidationCount;
    public long FullNativeSemanticSourceValidationCount=>Interlocked.Read(ref fullNativeSemanticSourceValidationCount);
    internal OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot CaptureAcceptedImmutableStockSources(OriginalMapCopy97CurrentStockPacket packet)
    {
        ValidateCurrent(packet);
        if(acceptedStockSources is{} existing){existing.RequireCurrent(packet);return existing.Mask;}
        var mask=packet.Prefix.Source.CaptureAcceptedImmutableSourceSnapshot(packet.Prefix);
        acceptedStockSources=new(this,packet,mask);return mask;
    }
    internal void RevalidateAcceptedStockSourceBoundary(OriginalMapCopy97CurrentStockPacket packet)
    {
        ValidateCurrent(packet);
        RequireScope(packet.Prefix,packet.Frame,packet.Models,packet.Terrain,packet.Early,packet.Caustics,packet.GeometryFrame);
        acceptedStockSources?.Mask.RevalidateMutableSourceBoundary();
    }
    /// <summary>Full RequireScope already ran at acceptance. Keep the exact
    /// native packet identities and private byte snapshots; never replay G1
    /// selection or Directional construction in a draw-time current check.
    /// Mutable source canonical validation remains at the explicit boundary.</summary>
    private sealed class AcceptedStockSources
    {
        private readonly OriginalMapCopy97CurrentStockSource owner;
        private readonly OriginalMapCopy97CurrentStockPacket packet;
        private readonly byte[] camera,lightingCamera,light,feature,lateFeature,lateVariant,lateRow,lateBinding,
            earlyCamera,earlyFeature,earlyVariant,earlyRow,earlyBinding;
        private readonly OriginalDeferredF103VariantWrite[] lateWrites,earlyWrites;
        internal OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot Mask{get;}
        internal AcceptedStockSources(OriginalMapCopy97CurrentStockSource owner,OriginalMapCopy97CurrentStockPacket packet,
            OriginalMapOwnedGraphicsMaskAcceptedSourceSnapshot mask)
        {
            this.owner=owner;this.packet=packet;Mask=mask;var frame=packet.Frame;
            camera=frame.Camera.Prefix.ToArray();lightingCamera=frame.Lighting.PreparedCameraPrefix.ToArray();light=frame.Lighting.LocalLightData.ToArray();
            feature=frame.Feature.CurrentFeature48.ToArray();lateFeature=frame.Selection.CurrentFeature48.ToArray();
            lateVariant=frame.Selection.RequestedVariant96.ToArray();lateRow=frame.Selection.Combination.RawRow.ToArray();
            lateBinding=frame.Selection.BindingVariant.RawBytes.ToArray();lateWrites=frame.Selection.Writes.ToArray();
            earlyCamera=packet.Early.Camera.Prefix.ToArray();earlyFeature=packet.Early.CurrentFeature48.ToArray();
            earlyVariant=packet.Early.RequestedVariant96.ToArray();earlyRow=packet.Early.Combination.RawRow.ToArray();
            earlyBinding=packet.Early.BindingVariant.RawBytes.ToArray();earlyWrites=packet.Early.Writes.ToArray();
        }
        internal void RequireCurrent(OriginalMapCopy97CurrentStockPacket actual)
        {
            Mask.RequireCurrent();var frame=packet.Frame;
            if(!owner.valid||!ReferenceEquals(actual,packet)||!ReferenceEquals(owner.prepared,packet)||
                !ReferenceEquals(packet.Source,owner)||packet.Prefix.PreviousCompletedFrameNumber!=owner.CompletedSourceFrameNumber||
                owner.frontend.CompletedSourceFrameNumber!=owner.CompletedSourceFrameNumber||!owner.frontend.WorkSubmissionStarted||
                packet.FrameNumber!=Mask.FrameNumber||packet.ViewIdentity!=Mask.ViewIdentity||
                !frame.Camera.Prefix.Span.SequenceEqual(camera)||!frame.Lighting.PreparedCameraPrefix.Span.SequenceEqual(lightingCamera)||
                !frame.Lighting.LocalLightData.Span.SequenceEqual(light)||!frame.Feature.CurrentFeature48.Span.SequenceEqual(feature)||
                !frame.Selection.CurrentFeature48.Span.SequenceEqual(lateFeature)||!frame.Selection.RequestedVariant96.Span.SequenceEqual(lateVariant)||
                !frame.Selection.Combination.RawRow.AsSpan().SequenceEqual(lateRow)||!frame.Selection.BindingVariant.RawBytes.AsSpan().SequenceEqual(lateBinding)||
                !frame.Selection.Writes.SequenceEqual(lateWrites)||!packet.Early.Camera.Prefix.Span.SequenceEqual(earlyCamera)||
                !packet.Early.CurrentFeature48.Span.SequenceEqual(earlyFeature)||!packet.Early.RequestedVariant96.Span.SequenceEqual(earlyVariant)||
                !packet.Early.Combination.RawRow.AsSpan().SequenceEqual(earlyRow)||!packet.Early.BindingVariant.RawBytes.AsSpan().SequenceEqual(earlyBinding)||
                !packet.Early.Writes.SequenceEqual(earlyWrites))
                throw new InvalidDataException("Accepted stock semantic snapshot changed its current native Camera/light/feature/variant packet or frontend epoch.");
            packet.Terrain.Source.RequireRetainedAcceptedScope();
        }
    }
}
