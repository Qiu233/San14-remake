using Godot;
using San14.Data;

/// <summary>The retained ordinary Person list opened by an existing Army.
/// The host owns its actual candidate sequence and nested detail lifecycle.</summary>
internal partial class ArmyCaptivesView : DetailsView
{
    private MapInformationListView list = null!;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, string title,
        IReadOnlyList<MapInformationListColumn> columns, IReadOnlyList<MapInformationListRow> rows,
        Func<bool> admitted, Action close, Action<int> inspect, Func<int> scrollSpeed,
        Func<string, bool, IReadOnlyList<MapInformationListRow>> sort, string? notice)
    {
        ConfigureSourcePage("ArmyCaptives", null, close);
        list = new() { Name = "ArmyCaptivesList" }; AddChild(list);
        list.Configure(repository, text, title, columns, rows,
            () => !abandoned && IsVisibleInTree() && admitted(), RequestClose, inspect, scrollSpeed, sort, notice);
    }

    internal bool? NameSortAscending => list.NameSortAscending;
    internal void ApplyDetailReturn(IReadOnlyList<MapInformationListRow> rows, int selected, bool? ascending)
        => list.ApplyDetailReturn(rows, selected, ascending);

    internal override void RegisterHeader(UiSourceHeaderStack owner) => list.RegisterHeader(owner);
    public override void _ExitTree()
    {
        abandoned = true; list?.Abandon(); base._ExitTree();
    }
}
