using San14.Data;

/// <summary>Ordinary Person identities and child return semantics over the
/// shared authored-group list display.</summary>
internal partial class OfficerInformationListView : PagedInformationListView
{
    private OfficerInformationPageSet pages = null!;
    internal OfficerInformationPageSet Pages => pages;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        OfficerInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> inspect, Func<int> scrollSpeed, AdministrationPageChrome.Resources resources)
    {
        this.pages = pages;
        ConfigureList(repository, text, new OfficerInformationListContent(pages), admitted,
            close, inspect, scrollSpeed, "OfficerInformationList", "officer_information_group", resources: resources);
    }
    internal void ApplyDetailReturn(OfficerDetailSelectionReturn returned) =>
        ApplySelectionReturn(returned.OrderedIds, returned.SelectedId, returned.NameSortAscending);
    internal void AdoptPages(OfficerInformationPageSet replacement)
    {
        AdoptContent(new OfficerInformationListContent(replacement)); pages = replacement;
    }
}
