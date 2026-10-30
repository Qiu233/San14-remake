using San14.Data;

/// <summary>Ordinary Army identities retain their caller-owned sequence over
/// the same authored-group display used by the ordinary Person list.</summary>
internal partial class ArmyInformationListView : PagedInformationListView
{
    private ArmyInformationPageSet pages = null!;
    internal ArmyInformationPageSet Pages => pages;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        ArmyInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> inspect, Func<int> scrollSpeed)
    {
        this.pages = pages;
        ConfigureList(repository, text, new ArmyInformationListContent(pages), admitted,
            close, inspect, scrollSpeed, "ArmyInformationList", "army_information_group");
    }
    internal void AdoptPages(ArmyInformationPageSet replacement)
    {
        AdoptContent(new ArmyInformationListContent(replacement)); pages = replacement;
    }
}
