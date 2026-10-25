using San14.Core;

namespace San14.Data;

public sealed record DistrictAdministrationCity(int Id, string Name, int? HexId,
    int? DistrictId, bool? Available, string? UnresolvedOwner);

public sealed record DistrictAdministrationDistrict(int Id, int Number, int? LeaderId,
    DistrictSettings? Settings, IReadOnlyList<int> CityIds)
{
    public string Name { get; init; } = "";
    public bool IsPrimary { get; init; }
    public bool? CanDissolve { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public sealed record DistrictAdministrationLeader(int Id, string Name);

public sealed record DistrictAdministrationTarget(DistrictTarget Target, string Name,
    IReadOnlyList<int> MapHexIds, bool? Available, string? UnresolvedOwner);

public sealed record DistrictAdministrationRules(int ForceId, int FirstAvailableNumber,
    IReadOnlyList<DistrictAdministrationDistrict> Districts,
    IReadOnlyList<DistrictAdministrationCity> Cities, DistrictSettings? NewSettings)
{
    public string? UnresolvedOwner { get; init; }
    public int? MaximumMilitaryIntelligenceTransfer { get; init; }
    public int? PrimaryMilitaryIntelligence { get; init; }
    public int? MilitaryIntelligenceBefore { get; init; }
}

public sealed record DistrictAdministrationSelection(
    IReadOnlyList<DistrictAdministrationLeader> Leaders,
    IReadOnlyList<DistrictAdministrationTarget> Targets,
    bool? CanSubmit, bool? CanContinue, string? UnresolvedOwner)
{
    public bool? CanConfirmCities { get; init; }
    public bool? CanContinueCities { get; init; }
    public int? MaximumMilitaryIntelligenceTransfer { get; init; }
    public int? PrimaryMilitaryIntelligence { get; init; }
    public int? MilitaryIntelligenceBefore { get; init; }
    public string? ContinueUnresolvedOwner { get; init; }
}
