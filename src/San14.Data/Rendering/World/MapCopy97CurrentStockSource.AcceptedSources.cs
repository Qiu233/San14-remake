namespace San14.Data;

public sealed partial class MapCopy97CurrentStockSource
{
    private MapOwnedGraphicsMaskAcceptedSourceSnapshot? acceptedMaskSources;
    private long fullNativeSemanticSourceValidationCount;
    public long FullNativeSemanticSourceValidationCount=>Interlocked.Read(ref fullNativeSemanticSourceValidationCount);

    /// <summary>Admission checks the immutable inputs once. Later queries
    /// follow the actual frontend attempt and retained Terrain writer scope.</summary>
    internal void ValidateCurrent(MapCopy97CurrentStockPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if(!valid || !ReferenceEquals(prepared,packet) || !ReferenceEquals(packet.Source,this) ||
            packet.Prefix.PreviousCompletedFrameNumber!=CompletedSourceFrameNumber ||
            frontend.CompletedSourceFrameNumber!=CompletedSourceFrameNumber || !frontend.WorkSubmissionStarted ||
            acceptedMaskSources is not {} mask || !ReferenceEquals(mask.Prefix,packet.Prefix))
            throw new InvalidDataException("Current stock packet was failed, replaced or belongs to another frontend attempt.");
        mask.RequireCurrent();
        packet.Terrain.Source.RequireRetainedAcceptedScope();
    }
}
