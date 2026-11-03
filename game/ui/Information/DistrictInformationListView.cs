using San14.Data;

/// <summary>Ordinary District identities retain their caller-owned sequence over
/// the same authored-group display used by the ordinary Person list.</summary>
internal partial class DistrictInformationListView : PagedInformationListView
{
    private DistrictInformationPageSet pages = null!;
    internal DistrictInformationPageSet Pages => pages;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        DistrictInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> inspect, Func<int> scrollSpeed, AdministrationPageChrome.Resources resources)
    {
        this.pages = pages;
        ConfigureList(repository, text, new DistrictInformationListContent(pages), admitted,
            close, inspect, scrollSpeed, "DistrictInformationList", "district_information_group", resources: resources, sourceReturnMotion: true);
    }
    internal void ApplyDetailReturn(InformationDetailSelectionReturn returned) =>
        ApplySelectionReturn(returned.OrderedIds, returned.SelectedId, returned.NameSortAscending);
    internal void AdoptPages(DistrictInformationPageSet replacement)
    {
        AdoptContent(new DistrictInformationListContent(replacement)); pages = replacement;
    }
}
