namespace San14.Data;

/// <summary>A source-resolved City, Force ruler Home or Person Home camera
/// target. Its raw Object Hex word is retained, including the unplaced value.</summary>
public sealed record CampaignEventCameraToTarget(int OperationIndex, int SourceOffset, int SourceCode,
    ushort HexId, int CameraParameter) : CampaignEventCommand(OperationIndex, SourceOffset, SourceCode);
