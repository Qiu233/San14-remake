using San14.Data;

/// <summary>Ordinary Area identities retain their caller-owned sequence over
/// the same authored-group display used by the ordinary Person list.</summary>
internal partial class AreaInformationListView : PagedInformationListView
{
    private AreaInformationPageSet pages = null!;
    internal AreaInformationPageSet Pages => pages;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        AreaInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> inspect, Func<int> scrollSpeed)
    {
        this.pages = pages;
        ConfigureList(repository, text, new AreaInformationListContent(pages), admitted,
            close, inspect, scrollSpeed, "AreaInformationList", "area_information_group");
    }
    internal void ApplyDetailReturn(InformationDetailSelectionReturn returned) =>
        ApplySelectionReturn(returned.OrderedIds, returned.SelectedId, returned.NameSortAscending);
    internal void AdoptPages(AreaInformationPageSet replacement)
    {
        AdoptContent(new AreaInformationListContent(replacement)); pages = replacement;
    }
}
