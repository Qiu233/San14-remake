using San14.Data;

/// <summary>Ordinary Force identities retain their caller-owned sequence over
/// the same authored-group display used by the ordinary Person list.</summary>
internal partial class ForceInformationListView : PagedInformationListView
{
    private ForceInformationPageSet pages = null!;
    internal ForceInformationPageSet Pages => pages;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        ForceInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> inspect, Func<int> scrollSpeed, AdministrationPageChrome.Resources resources)
    {
        this.pages = pages;
        ConfigureList(repository, text, new ForceInformationListContent(pages), admitted,
            close, inspect, scrollSpeed, "ForceInformationList", "force_information_group", resources: resources, sourceReturnMotion: true);
    }
    internal void ApplyDetailReturn(InformationDetailSelectionReturn returned) =>
        ApplySelectionReturn(returned.OrderedIds, returned.SelectedId, returned.NameSortAscending);
    internal void AdoptPages(ForceInformationPageSet replacement)
    {
        AdoptContent(new ForceInformationListContent(replacement)); pages = replacement;
    }
}
