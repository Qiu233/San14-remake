using System.Collections.Immutable;

namespace San14.Core;

public enum DistrictTargetKind { Force, Foothold, District }
public sealed record DistrictTarget(DistrictTargetKind Kind, int Id);

/// <summary>Catalogue choices controlling the district's use of each resource.</summary>
public sealed record DistrictResourceSettings(int GoldUsageId, int FoodUsageId, int TroopsUsageId);

/// <summary>Allowed mission identities follow the seven choices in the district
/// military-information catalogue. Identity 2 is reserved and unavailable.</summary>
public sealed record DistrictMilitarySettings(bool Enabled, ImmutableHashSet<int> AllowedMissionIds);

public sealed record DistrictSettings(
    int PolicyId,
    DistrictTarget? Target,
    int PriorityId,
    bool RecruitOfficers,
    bool Attack,
    bool Transport,
    bool RecruitTroops,
    bool Merchant,
    ImmutableHashSet<int> AllowedFacilityIds,
    DistrictResourceSettings Resources,
    DistrictMilitarySettings Military)
{
    public bool Demolition { get; init; }
}
