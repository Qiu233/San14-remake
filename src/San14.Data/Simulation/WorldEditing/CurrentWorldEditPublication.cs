namespace San14.Data;

public sealed record CurrentWorldEditAdmission(bool Allowed, int? BlockedGameMessageId,
    bool RequiresConfirmation);
public sealed record CurrentWorldEditNumber(int? Value, int Minimum, int? Maximum,
    string? UnresolvedOwner = null);
public sealed record CurrentWorldEditOption(int Id, string Name);
public sealed record CurrentWorldEditTarget(int TargetId, string Name);

public sealed record CurrentWorldAreaEditPublication(int TargetId, string Name,
    CurrentWorldEditNumber Commerce, CurrentWorldEditNumber Agriculture, CurrentWorldEditNumber Barracks,
    CurrentWorldEditNumber Population, CurrentWorldEditNumber Security);

public sealed record CurrentWorldCityEditPublication(int TargetId, string Name,
    CurrentWorldEditNumber Size, CurrentWorldEditNumber Gold, CurrentWorldEditNumber Food,
    CurrentWorldEditNumber Troops, CurrentWorldEditNumber Durability, CurrentWorldEditNumber Morale,
    CurrentWorldEditNumber CounterTactic, IReadOnlyList<CurrentWorldEditOption> CounterTactics);

public sealed record CurrentWorldForceDoctrinePoints(int DoctrineId, string Name,
    CurrentWorldEditNumber Points);
public sealed record CurrentWorldForceDoctrineLevelOption(int Level, int Experience);
public sealed record CurrentWorldForceDistrictMilitaryInformation(int DistrictId, string Name,
    CurrentWorldEditNumber MilitaryInformation);
public sealed record CurrentWorldForceRelationship(int ForceId, string Name,
    CurrentWorldEditNumber Friendship, CurrentWorldEditNumber AllianceMonths)
{
    public int? FriendshipLevel { get; init; }
}
public sealed record CurrentWorldForceEditPublication(int TargetId, string Name,
    CurrentWorldEditNumber RankId, CurrentWorldEditNumber NationalTitleId,
    CurrentWorldEditNumber DoctrineExperience, CurrentWorldEditNumber CivilReputation,
    int? SelectedDistrictId, CurrentWorldEditNumber MilitaryInformation,
    IReadOnlyList<CurrentWorldEditOption> Ranks, IReadOnlyList<CurrentWorldEditOption> NationalTitles,
    IReadOnlyList<CurrentWorldEditOption> Districts, IReadOnlyList<CurrentWorldForceDoctrinePoints> DoctrinePoints,
    IReadOnlyList<CurrentWorldForceRelationship> Relationships)
{
    public IReadOnlyList<CurrentWorldEditOption> FriendshipLevels { get; init; } = [];
    public bool CanEditRank { get; init; }
    public bool CanEditNationalTitle { get; init; }
    public bool CanEditDoctrineLevel { get; init; }
    public CurrentWorldEditNumber DoctrineLevel { get; init; } = new(null, 1, 10, "current-world-edit.force_doctrine_level");
    public IReadOnlyList<CurrentWorldForceDoctrineLevelOption> DoctrineLevels { get; init; } = [];
    public IReadOnlyList<string> UnresolvedFields { get; init; } = [];
    public CurrentWorldEditNumber TotalMilitaryInformation { get; init; } =
        new(null, 0, null, "current-world-edit.force_total_military_information");
    public IReadOnlyList<CurrentWorldForceDistrictMilitaryInformation> DistrictMilitaryInformationValues { get; init; } = [];
}
