using San14.Data;

/// <summary>Selected existing armies use the same authored Army preset,
/// columns, cached cell/sort consumers and retained parent as normal lists.</summary>
internal partial class ArmyDetailsListView : PagedInformationListView
{
    internal ArmyInformationPageSet Pages { get; private set; } = null!;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        ArmyInformationPageSet pages, Func<bool> admitted, Action<int> inspect,
        Action close, Func<int> scrollSpeed)
    {
        ConfigureCommonBackdrop(repository);
        Pages = pages;
        ConfigureList(repository, text, new ArmyInformationListContent(pages), admitted,
            close, inspect, scrollSpeed, "UnitSourceDetailsList", "army_details_group");
    }
}
