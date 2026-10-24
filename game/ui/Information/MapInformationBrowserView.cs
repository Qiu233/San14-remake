using Godot;
using San14.Data;

/// <summary>Owns information-menu navigation inside the existing map-detail
/// modal. Child pages retain their publication and never submit orders.</summary>
internal partial class MapInformationBrowserView : DetailsView
{
    private MapInformationMenuView menu = null!;
    private MapInformationListView? list;
    private readonly List<DetailsView> details = [];
    private DetailsView? detail => details.LastOrDefault();
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiSourceHeaderStack headers = null!;
    private Func<int>? scrollSpeed;
    private Func<bool>? admitted;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        UiSourceHeaderStack headers, Func<bool> admitted,
        Func<int, IReadOnlyList<MapInformationMenuEntry>> readMenu,
        Action<int, int> selected, Action history, Action close, Func<int> scrollSpeed,
        string guide, string returnHint, bool historyEnabled, int initialTab, IReadOnlyList<bool> tabEnabled)
    {
        this.repository = repository; this.text = text; this.headers = headers;
        this.admitted = admitted; this.scrollSpeed = scrollSpeed;
        ConfigureSourcePage("MapInformationBrowser", null, close);
        menu = new(); AddChild(menu);
        menu.Configure(repository, text, () => ReadAdmission() && list is null && detail is null,
            readMenu, () => base.RequestClose(), selected, history, guide, returnHint, historyEnabled, initialTab, tabEnabled);
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && !IsQueuedForDeletion() &&
        (admitted?.Invoke() ?? false);

    internal void ShowList(string title, IReadOnlyList<MapInformationListColumn> columns,
        IReadOnlyList<MapInformationListRow> rows, Action<int> inspect,
        Func<string, bool, IReadOnlyList<MapInformationListRow>>? sort = null, string? notice = null)
    {
        if (!ReadAdmission() || list is not null || detail is not null) return;
        var child = new MapInformationListView(); list = child; AddChild(child);
        try
        {
            child.Configure(repository, text, title, columns, rows,
                () => ReadAdmission() && list == child && detail is null,
                ReturnFromList, inspect, scrollSpeed!, sort, notice);
            child.RegisterHeader(headers);
            menu.SetBusy(true); menu.Hide();
        }
        catch { ReturnFromList(); throw; }
    }

    internal void ShowDetail(DetailsView child, Action<Action> configure)
    {
        if (!ReadAdmission()) { child.Free(); return; }
        var previous = detail;
        details.Add(child); AddChild(child);
        if (previous is not null) { previous.Hide(); previous.ProcessMode = ProcessModeEnum.Disabled; }
        else if (list is { } parent) { parent.Hide(); parent.ProcessMode = ProcessModeEnum.Disabled; }
        else { menu.SetBusy(true); menu.Hide(); }
        try
        {
            configure(() => { if (detail == child) ReturnFromDetail(); });
            child.RegisterHeader(headers);
        }
        catch { ReturnFromDetail(); throw; }
    }

    internal void ApplyNotice(string message) => menu.ApplyNotice(message);

    private void ReturnFromDetail()
    {
        var child = detail;
        if (child is not null) details.RemoveAt(details.Count - 1);
        if (child is not null) { child.Hide(); child.GetParent()?.RemoveChild(child); child.QueueFree(); }
        if (!abandoned && detail is { } previous) { previous.ProcessMode = ProcessModeEnum.Inherit; previous.Show(); }
        else if (!abandoned && list is { } parent) { parent.ProcessMode = ProcessModeEnum.Inherit; parent.Show(); }
        else if (!abandoned) { menu.SetBusy(false); menu.Show(); }
    }

    private void ReturnFromList()
    {
        while (details.Count != 0) ReturnFromDetail();
        var child = list; list = null;
        if (child is not null) { child.Abandon(); child.GetParent()?.RemoveChild(child); child.QueueFree(); }
        if (!abandoned) { menu.SetBusy(false); menu.Show(); }
    }

    internal override void RequestClose()
    {
        if (!ReadAdmission()) return;
        if (detail is { } child) child.RequestClose();
        else if (list is not null) ReturnFromList();
        else base.RequestClose();
    }

    public override void _ExitTree()
    {
        abandoned = true; admitted = null; scrollSpeed = null;
        list?.Abandon(); menu?.Abandon(); list = null; details.Clear();
        base._ExitTree();
    }
}
