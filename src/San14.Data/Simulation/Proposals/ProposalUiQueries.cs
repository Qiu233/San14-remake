using San14.Core;

namespace San14.Data;

/// <summary>A source-order proposal row, independent of the original controller's pointers.</summary>
public sealed record ProposalRow(int Id, int Kind, string Name, string Summary,
    int? ProposerOfficerId, string? ProposerName, bool Adopted, bool CanAdopt)
{
    public string? HomeCityName { get; init; }
    public string? TargetName { get; init; }
    public int? DetailId { get; init; }
    public int? Cost { get; init; }
    public OfficerPortraitRoute? Portrait { get; init; }
    public string? UnresolvedOwner { get; init; }
    public bool OpensOfficerPage { get; init; }
}

/// <summary>The ordinary First/Officer proposal controller's current publication.</summary>
public sealed record ProposalPage(int ForceId, string RulerName,
    IReadOnlyList<ProposalRow> FirstRows, IReadOnlyList<ProposalRow> OfficerRows)
{
    public IReadOnlyList<int> SelectedProposalIds { get; init; } = Array.Empty<int>();
    public bool CanSubmit { get; init; }
    public long? AvailableGold { get; init; }
    public int? AvailableActionPoints { get; init; }
    public int? ActionPointCost { get; init; }
    public string? UnresolvedOwner { get; init; }
}
