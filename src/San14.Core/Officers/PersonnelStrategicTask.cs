namespace San14.Core;

public enum StrategicCommandTargetKind { Force, City, Person, Area }
public sealed record StrategicCommandTarget(StrategicCommandTargetKind Kind, int Id);

/// <summary>A real strategic journey in the shared personnel command manager.
/// The captured governor and force identities retain the original associations
/// after the target's ownership or appointment changes.</summary>
public sealed record PersonnelStrategicTask(int Id, int DispatchSlot, int[] MemberIds,
    int Kind, int ForceId, int SourceLocationId, int SourceDistrictId,
    StrategicCommandTarget Target, int? SecondForceId, int? TargetGovernorId,
    int TargetOriginalForceId, int SourceForceId, int StartHexId, int EndHexId,
    NativeMapSeasonDate StartDate, NativeMapSeasonDate DueDate, int SavedResult,
    int MilitaryIntelligence, int IndependenceChoice, bool Started, bool Cancelled)
{
    public bool IsRetiring { get; init; }
    public bool IsAssociated { get; init; } = true;
    public bool ReportToPlayer { get; init; }
}
