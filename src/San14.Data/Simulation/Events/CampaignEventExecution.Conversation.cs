namespace San14.Data;

/// <summary>Hide only the current message pane; actors and registration remain.</summary>
public sealed record CampaignEventHideMessage(int OperationIndex, int SourceOffset)
    : CampaignEventCommand(OperationIndex, SourceOffset, 249);

/// <summary>Source signed32 millisecond duration. The actual presentation host
/// owns entry-time fast/skip gates and the monotonic time-wait lifecycle.</summary>
public sealed record CampaignEventWait(int OperationIndex, int SourceOffset, int Milliseconds)
    : CampaignEventCommand(OperationIndex, SourceOffset, 326);
