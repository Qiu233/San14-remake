using Godot;
using San14.Data;

/// <summary>A retained district subordinate table inside the ordinary information
/// browser. Its list owns scrolling and its parent owns detail navigation.</summary>
internal partial class DistrictRelatedListView : DetailsView
{
    private MapInformationListView list = null!;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, string title,
        IReadOnlyList<MapInformationListColumn> columns, IReadOnlyList<MapInformationListRow> rows,
        Func<bool> admitted, Action close, Action<int> inspect, Func<int> scrollSpeed,
        Func<string, bool, IReadOnlyList<MapInformationListRow>> sort, string? notice)
    {
        ConfigureSourcePage("DistrictRelatedInformation", null, close);
        list = new() { Name = "DistrictRelatedList" }; AddChild(list);
        list.Configure(repository, text, title, columns, rows,
            () => !abandoned && IsVisibleInTree() && admitted(), RequestClose, inspect, scrollSpeed, sort, notice);
    }

    internal override void RegisterHeader(UiSourceHeaderStack owner) => list.RegisterHeader(owner);

    public override void _ExitTree()
    {
        abandoned = true; list?.Abandon(); base._ExitTree();
    }
}
