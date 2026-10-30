using San14.Data;

/// <summary>Ordinary Gate identities retain their caller-owned sequence over
/// the same authored-group display used by the ordinary Person list.</summary>
internal partial class GateInformationListView : PagedInformationListView
{
    private GateInformationPageSet pages = null!;
    internal GateInformationPageSet Pages => pages;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        GateInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> inspect, Func<int> scrollSpeed)
    {
        this.pages = pages;
        ConfigureList(repository, text, new GateInformationListContent(pages), admitted,
            close, inspect, scrollSpeed, "GateInformationList", "gate_information_group");
    }
    internal void ApplyDetailReturn(InformationDetailSelectionReturn returned) =>
        ApplySelectionReturn(returned.OrderedIds, returned.SelectedId, returned.NameSortAscending);
    internal void AdoptPages(GateInformationPageSet replacement)
    {
        AdoptContent(new GateInformationListContent(replacement)); pages = replacement;
    }
}
