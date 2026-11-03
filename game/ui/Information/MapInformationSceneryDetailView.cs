using Godot;
using San14.Data;

/// <summary>The ordinary CInfoDetailSceneryDlg, with its current-world body,
/// retained selector and browser-owned source navigation.</summary>
internal partial class MapInformationSceneryDetailView : DetailsView
{
    private SceneryInformationView body = null!;
    private Func<bool>? admitted;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, SceneryInformationPage page,
        IReadOnlyList<CollectionDetailEntry> entries, Func<bool> admitted, Action close, Action<int> select,
        Func<int> scrollSpeed, AdministrationPageChrome.Resources resources)
    {
        this.admitted = admitted;
        ConfigureCommonBackdrop(repository);
        ConfigureSourcePage("InformationSceneryDetail", null, close);
        body = new(); AddChild(body);
        body.Configure(repository, text, page, ReadAdmission, RequestClose, resources,
            entries.Select(entry => new MapInformationListRow(entry.Id, [entry.Name], true)).ToArray(), select, scrollSpeed);
        // The browser's detail already supplies the foreground Z owner.
        body.ZIndex = 0;
        ConfigureReturnTransition(resources.Assets.SourceLayout(0xF9C5DB14), body.MotionGroups);
    }

    internal void Apply(SceneryInformationPage page) => body.Apply(page);
    internal override void RegisterHeader(UiSourceHeaderStack owner)
    { body.RegisterHeader(owner); base.RegisterHeader(owner); }
    private bool ReadAdmission() => !abandoned && CanProcess() && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    public override void _ExitTree()
    { abandoned = true; admitted = null; body?.Abandon(); base._ExitTree(); }
}
