namespace San14.Core;

public enum PersonnelIntelligenceKind { Away, Inspect }

/// <summary>A foreign intelligence journey or an inspection at a city. TargetId
/// is a Force for Away and a City for Inspect; dispatch identity is shared with
/// the ordinary personnel manager.</summary>
public sealed record PersonnelIntelligenceTask(int Id, int DispatchSlot, int[] MemberIds,
    PersonnelIntelligenceKind Kind, int ForceId, int SourceLocationId, int SourceDistrictId,
    int TargetId, int TargetOriginalForceId, int StartHexId, int EndHexId,
    NativeMapSeasonDate StartDate, NativeMapSeasonDate DueDate, int DurationOrLevel,
    int SavedResult, bool Started, bool Cancelled)
{
    public bool IsRetiring { get; init; }
    public bool IsAssociated { get; init; } = true;
    public bool ReportToPlayer { get; init; }
    public PersonnelIntelligenceNarrationProgress? NarrationProgress { get; init; } = new(null);
}
