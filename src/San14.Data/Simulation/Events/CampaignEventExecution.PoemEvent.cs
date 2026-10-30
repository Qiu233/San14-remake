namespace San14.Data;

/// <summary>The ordinary poem event's source descriptor identity. Its actual
/// application skip and presentation return are owned by the live dispatcher.</summary>
public sealed record CampaignEventPoemEvent(int OperationIndex, int SourceOffset, int RawId)
    : CampaignEventCommand(OperationIndex, SourceOffset, 427);
