namespace San14.Data;

/// <summary>The merchant's ten-group Person chooser over its own captured
/// source. The health callback is independent of column palette overrides.</summary>
public sealed record MerchantOfficerSelection(OfficerInformationPageSet Pages,
    IReadOnlyList<int> InitialSelection, int MaximumSelected, int TitleMessageId,
    string GuideText, bool RequireSelection)
{
    public IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth { get; init; }
        = new Dictionary<int, AreaAdministrationQuery<bool>>();
}
