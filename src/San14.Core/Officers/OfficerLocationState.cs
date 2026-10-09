namespace San14.Core;

/// <summary>The place an officer currently occupies, independently of their home base.</summary>
public enum OfficerPositionKind { Unplaced, City, Gate, Area, ArmyReceiver, Unresolved }

public sealed record OfficerPosition
{
    public OfficerPositionKind Kind { get; }
    public int? EntityId { get; }
    public OfficerPosition(OfficerPositionKind kind, int? entityId)
    {
        if (!Enum.IsDefined(kind) ||
            (kind is OfficerPositionKind.Unplaced or OfficerPositionKind.Unresolved ? entityId is not null : entityId is null or < 1))
            throw new InvalidDataException("An officer position must identify its place or its unresolved knowledge.");
        Kind = kind; EntityId = entityId;
    }
    public static OfficerPosition Unplaced { get; } = new(OfficerPositionKind.Unplaced, null);
    public static OfficerPosition Unresolved { get; } = new(OfficerPositionKind.Unresolved, null);
    public static OfficerPosition AtReceiver(ArmyReceiverId receiver) => new(OfficerPositionKind.ArmyReceiver, receiver.Value);
}

/// <summary>A present membership with no district is a known unaffiliated officer.
/// A missing membership is unknown, independently of the displayed district.</summary>
public enum OfficerDistrictMembershipKind { Unaffiliated, District, Unresolved }
public sealed record OfficerDistrictMembership
{
    public OfficerDistrictMembershipKind Kind { get; }
    public int? DistrictId { get; }
    public OfficerDistrictMembership(OfficerDistrictMembershipKind kind, int? districtId)
    {
        if (!Enum.IsDefined(kind) || (kind == OfficerDistrictMembershipKind.District ? districtId is null or < 1 : districtId is not null))
            throw new InvalidDataException("An observed district membership must identify a district, known vacancy or unresolved knowledge.");
        Kind = kind; DistrictId = districtId;
    }
    public static OfficerDistrictMembership Unaffiliated { get; } = new(OfficerDistrictMembershipKind.Unaffiliated, null);
    public static OfficerDistrictMembership Unresolved { get; } = new(OfficerDistrictMembershipKind.Unresolved, null);
}

/// <summary>Current position and observed membership are separate knowledge channels.
/// Neither the officer's home nor their role supplies a missing channel.</summary>
public sealed record OfficerLocationState(OfficerPosition? Position, OfficerDistrictMembership? Membership);

public sealed partial record OfficerState
{
    public OfficerLocationState? LocationState { get; init; }
}
