using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignEventCommandRow(int EventId, string Name, string Description,
    S14EventEligibilityStatus? Status, bool? Occurred, bool? Acknowledged,
    int SuccessfulConditions, int EvaluatedConditions, string? MissingOwner,
    int Kind, int TriggerKind, bool Repeatable, IReadOnlyList<int> RelatedPersonIds)
{
    public IReadOnlyList<string> ResultLines { get; init; } = [];
    public int SourceOrder { get; init; }
    public CampaignEventCommandRowPresentation? Presentation { get; init; }
    public bool IsEligible => Status == S14EventEligibilityStatus.Satisfied;
    public bool CanExecute => IsEligible && MissingOwner is null;
    internal Lazy<CampaignEventConditionDescription?>? DescriptionSource { get; init; }
    public CampaignEventConditionDescription? Conditions => DescriptionSource?.Value;
}

public sealed record CampaignEventCommandCatalog(IReadOnlyList<CampaignEventCommandRow> Rows,
    bool HasAvailableEvents, IReadOnlyList<string> UnresolvedOwners)
{
    public IReadOnlyList<CampaignEventCommandRow> ReadRows(bool availableOnly)
        => availableOnly ? Rows.Where(row => row.IsEligible).ToArray() : Rows;
}
