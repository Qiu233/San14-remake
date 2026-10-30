namespace San14.Data;

public sealed record TalentRecruitPersonSelection(OfficerInformationPageSet Pages,
    IReadOnlyList<int> InitialSelection, int MaximumSelected, int TitleMessageId,
    string GuideText, bool Single)
{
    public bool RequireSelection => false;
    public IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth { get; init; }
        = new Dictionary<int, AreaAdministrationQuery<bool>>();
}
