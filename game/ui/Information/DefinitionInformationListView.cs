using Godot;
using San14.Data;

/// <summary>The ordinary definition list selects a definition in place;
/// footer14 opens its capable officers, rather than an invented record page.</summary>
internal partial class DefinitionInformationListView : PagedInformationListView
{
    private DefinitionInformationPageSet pages = null!;
    private GameResourceRepository repository = null!;
    private Func<bool> admitted = null!;
    private Action<int> openOfficers = null!;
    private UiSourceBottomButton? officers;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        DefinitionInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> openOfficers, Func<int> scrollSpeed, AdministrationPageChrome.Resources resources, Action<int>? inspect = null)
    {
        this.repository = repository; this.pages = pages; this.admitted = admitted; this.openOfficers = openOfficers;
        ConfigureList(repository, text, new DefinitionInformationListContent(pages), admitted,
            close, id => { RefreshOfficers(); inspect?.Invoke(id); }, scrollSpeed, "DefinitionInformationList", "definition_information_group", resources: resources, sourceReturnMotion: true);
    }
    protected override void ConfigureListExtras(MapInformationListView currentList)
    {
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        officers = new() { Name = "DefinitionOfficers", Position = footer.Rectangle(14, new(0, 904)).Position,
            InputAdmission = () => admitted() && list == currentList };
        currentList.SourceFooterRoot.AddChild(officers);
        officers.Configure(repository, currentList.SourceAssets, currentList.SourceShader, currentList.SourceFont,
            pages.OfficersCaption, () =>
            {
                if (admitted() && list == currentList && pages.SelectedDefinitionId is int id && pages.CanBrowseOfficers(id))
                    openOfficers(id);
            });
        RefreshOfficers();
    }
    internal void RefreshSelectionReturn()
    {
        if(pages.SelectedDefinitionId is int selected)ApplySelectionReturn(pages.CurrentDefinitionIds,selected,pages.NameSortAscending);
    }
    internal void ApplyDetailSelectionReturn(InformationDetailSelectionReturn returned)
        =>ApplySelectionReturn(returned.OrderedIds,returned.SelectedId,returned.NameSortAscending);
    private void RefreshOfficers()
    {
        if (officers is not null) officers.Visible = pages.SelectedDefinitionId is int id && pages.CanBrowseOfficers(id);
    }
}
