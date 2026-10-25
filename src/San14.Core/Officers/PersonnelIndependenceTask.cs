namespace San14.Core;

/// <summary>The deferred independence negotiation created by a strategic
/// city command. Its captured governor is a person identity, not a force byte.</summary>
public sealed record PersonnelIndependenceTask(int Id, int DispatchSlot, int[] MemberIds,
    int ForceId, int SourceLocationId, int SourceDistrictId, int TargetCityId,
    int CapturedGovernorId, int TargetOriginalForceId, int SourceForceId,
    int StartHexId, int EndHexId, NativeMapSeasonDate StartDate, NativeMapSeasonDate DueDate,
    int SavedResult, int MilitaryIntelligence, bool Started, bool Cancelled)
{
    public bool IsRetiring { get; init; }
    public bool IsAssociated { get; init; } = true;
    public bool ReportToPlayer { get; init; }
}
