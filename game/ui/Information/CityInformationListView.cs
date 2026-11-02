using San14.Data;

/// <summary>Ordinary City identities retain their caller-owned sequence over
/// the same authored-group display used by the ordinary Person list.</summary>
internal partial class CityInformationListView : PagedInformationListView
{
    private CityInformationPageSet pages = null!;
    internal CityInformationPageSet Pages => pages;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        CityInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> inspect, Func<int> scrollSpeed, AdministrationPageChrome.Resources resources)
    {
        this.pages = pages;
        ConfigureList(repository, text, new CityInformationListContent(pages), admitted,
            close, inspect, scrollSpeed, "CityInformationList", "city_information_group", resources: resources);
    }
    internal void ApplyDetailReturn(InformationDetailSelectionReturn returned) =>
        ApplySelectionReturn(returned.OrderedIds, returned.SelectedId, returned.NameSortAscending);
    internal void AdoptPages(CityInformationPageSet replacement)
    {
        AdoptContent(new CityInformationListContent(replacement)); pages = replacement;
    }
}
