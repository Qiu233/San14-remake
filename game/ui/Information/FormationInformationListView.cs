using Godot;
using San14.Data;

/// <summary>The ordinary Formation list selects a definition in place;
/// footer14 opens its capable officers, rather than an invented record page.</summary>
internal partial class FormationInformationListView : PagedInformationListView
{
    private FormationInformationPageSet pages = null!;
    private GameResourceRepository repository = null!;
    private Func<bool> admitted = null!;
    private Action<int> openOfficers = null!;
    private UiSourceBottomButton? officers;
    internal void Configure(GameResourceRepository repository, PreviewText text,
        FormationInformationPageSet pages, Func<bool> admitted, Action close,
        Action<int> openOfficers, Func<int> scrollSpeed, AdministrationPageChrome.Resources resources)
    {
        this.repository = repository; this.pages = pages; this.admitted = admitted; this.openOfficers = openOfficers;
        ConfigureList(repository, text, new FormationInformationListContent(pages), admitted,
            close, _ => RefreshOfficers(), scrollSpeed, "FormationInformationList", "formation_information_group", resources: resources, sourceReturnMotion: true);
    }
    protected override void ConfigureListExtras(MapInformationListView currentList)
    {
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        officers = new() { Name = "FormationOfficers", Position = footer.Rectangle(14, new(0, 904)).Position,
            InputAdmission = () => admitted() && list == currentList };
        currentList.SourceFooterRoot.AddChild(officers);
        officers.Configure(repository, currentList.SourceAssets, currentList.SourceShader, currentList.SourceFont,
            pages.OfficersCaption, () =>
            {
                if (admitted() && list == currentList && pages.SelectedFormationId is int id && pages.CanBrowseOfficers(id))
                    openOfficers(id);
            });
        RefreshOfficers();
    }
    private void RefreshOfficers()
    {
        if (officers is not null) officers.Visible = pages.SelectedFormationId is int id && pages.CanBrowseOfficers(id);
    }
}
