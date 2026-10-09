namespace San14.Core;

public enum ArmyGroupOfficerKind
{
    Officer,
    NoOfficer,
    DefaultOfficer
}

/// <summary>Commander identity used for group leadership. A default lookup
/// officer is distinct from an absent officer, even when neither is playable.</summary>
public sealed record ArmyGroupOfficerReference(ArmyGroupOfficerKind Kind, int? OfficerId = null)
{
    public static ArmyGroupOfficerReference None { get; } = new(ArmyGroupOfficerKind.NoOfficer);
    public static ArmyGroupOfficerReference Default { get; } = new(ArmyGroupOfficerKind.DefaultOfficer);
}

public sealed record ArmyGroupAssignment(bool IsKnown, int? GroupId);
public sealed record ArmyGroupLeader(bool IsKnown, ArmyGroupOfficerReference Officer);

/// <summary>Military groups own assignments and leaders. Their ordered
/// membership comes from the world's army roster, not a second saved roster.</summary>
public sealed record ArmyGroupsState
{
    public Dictionary<int, ArmyGroupAssignment> UnitAssignments { get; init; } = [];
    public Dictionary<int, ArmyGroupLeader> Leaders { get; init; } = [];
    public ArmyGroupOfficerReference ResetLeader { get; init; } = ArmyGroupOfficerReference.None;

    // An unused group has the normal initial leader. Unknown imported leaders
    // are represented explicitly, rather than by omission.
    public ArmyGroupLeader GetLeader(int groupId) => Leaders.GetValueOrDefault(groupId)
        ?? new(true, ResetLeader);

    public ArmyGroupsState Copy() => this with
    {
        UnitAssignments = new(UnitAssignments),
        Leaders = new(Leaders)
    };
}

/// <summary>Current commander facts projected by the game's data boundary.
/// Leadership eligibility and recognized districts are policy facts, not
/// source table limits embedded in Core.</summary>
public sealed record ArmyGroupCommander(ArmyGroupOfficerReference Officer,
    bool IsValid, int? DistrictId, bool HasRecognizedDistrict, bool CanLeadGroup);

public sealed record ArmyGroupDistrict(int? DistrictId, bool IsRecognized);

public enum ArmyGroupLeaderPolicy
{
    PreserveForPlayerDistrict,
    AssignCommander
}
