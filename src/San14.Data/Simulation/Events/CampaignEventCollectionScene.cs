namespace San14.Data;

/// <summary>The Collection execution gate takes this complete source branch
/// before target resolution. It performs the ordinary blackout, without a
/// camera move or the non-Collection path's additional wait.</summary>
public sealed record CampaignEventSuppressedCamera(int OperationIndex, int SourceOffset, int SourceCode)
    : CampaignEventCommand(OperationIndex, SourceOffset, SourceCode);
