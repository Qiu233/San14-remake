namespace San14.Data;

/// <summary>One Search chooser over its draft's already prepared candidates.
/// Reading pages, changing sort or checking rows does not prepare a new plan.</summary>
public sealed record TalentSearchPersonSelection(OfficerInformationPageSet Pages,
    IReadOnlyList<int> InitialSelection, int MaximumSelected, int TitleMessageId,
    string GuideText, bool RequireSelection)
{
    public IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth { get; init; }
        = new Dictionary<int, AreaAdministrationQuery<bool>>();
    public string? InitialOrderUnresolvedInput { get; init; }
}
