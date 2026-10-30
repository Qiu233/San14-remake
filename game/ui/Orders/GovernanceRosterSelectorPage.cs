using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Globalization;

/// <summary>The government caller's three-page Person selector. The parent
/// retains its roster until this dialog returns an accepted selection.</summary>
internal partial class GovernanceRosterSelectorPage : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private OfficerInformationPageSet pages = null!;
    private CommandSelectionView selector = null!;
    private Action? returned;
    private Action<IReadOnlyList<int>>? accepted;
    private Action<AdministrationPageChrome>? mountSourcePanel;
    private Action<IReadOnlyList<int>>? selectionChanged;
    private Func<int>? scrollSpeed;
    private readonly Dictionary<int, (string? Key, bool Ascending)> pageState = [];
    private readonly Dictionary<int, Dictionary<string, bool>> directions = [];
    private HashSet<int> initial = [];
    private IReadOnlyList<int> selected = [];
    private int maximum, group = -1, scroll;
    private string title = "", guide = "", clearCaption = "";
    private bool advisor, nextSelectionAscending = true, abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, Theme theme,
        OfficerInformationPageSet pages, IReadOnlyList<int> initialIds, int maximum, bool advisor,
        string actualTitle, Action returned, Action<IReadOnlyList<int>> accepted,
        Func<int>? scrollSpeed = null, string? guide = null,
        Action<AdministrationPageChrome>? mountSourcePanel = null,
        Action<IReadOnlyList<int>>? selectionChanged = null)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(initialIds);
        if (maximum < 0 || advisor && maximum != 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        this.repository = repository; this.text = text; this.pages = pages;
        this.maximum = maximum; this.advisor = advisor; this.title = actualTitle;
        this.returned = returned; this.accepted = accepted;
        this.mountSourcePanel = mountSourcePanel; this.selectionChanged = selectionChanged;
        this.scrollSpeed = scrollSpeed ?? (() => 1); this.guide = guide ?? "";
        clearCaption = Message(advisor ? 0x29e : 0x2a);
        var candidates = pages.CurrentOfficerIds.ToHashSet();
        selected = initialIds.Where(candidates.Contains).Distinct().Take(maximum).ToArray();
        initial = selected.ToHashSet();
        Theme = theme; Name = "GovernanceRosterSelectorPage"; ZIndex = 5;
        MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ShowGroup(0);
        SetMeta("source_layout_name", "00_03_00_list_re");
        SetMeta("source_data_type", "CPersonData");
        SetMeta("selection_maximum", maximum);
        SetMeta("selection_page_count", pages.Groups.Length);
        SetMeta("selection_initial_sort", advisor ? "intelligence-descending-person-id" :
            "policy-level-descending-policy-cost-person-id");
    }

    private bool Admitted() => !abandoned && IsInsideTree() && IsVisibleInTree() && CanProcess();
    private static string ColumnKey(int id) => id == 0 ? "name" : id.ToString(CultureInfo.InvariantCulture);

    private void ShowGroup(int index)
    {
        if (abandoned || group == index || (uint)index >= (uint)pages.Groups.Length) return;
        if (selector is not null)
        {
            selected = selector.SelectedIds;
            var previous = selector.CapturePageState();
            scroll = previous.Scroll; pageState[group] = (previous.Key, previous.Ascending);
            selector.Abandon(); RemoveChild(selector); selector.QueueFree();
        }
        group = index;
        var descriptor = pages.Groups[index];
        var columns = descriptor.Columns.Where(column => column.Visible).ToArray();
        var sourceSlots = descriptor.Columns.Select((column, slot) => (column.Id, slot))
            .ToDictionary(value => value.Id, value => value.slot);
        var page = pages.ReadPage(index);
        if (!directions.TryGetValue(index, out var nextDirections)) directions.Add(index, nextDirections = []);
        nextDirections["selection"] = nextSelectionAscending;
        var current = new CommandSelectionView { Name = "GovernanceCandidates" };
        selector = current; AddChild(current);
        current.Configure(repository, text, 0x15,
            columns.Select(column => new CommandSelectionColumn(ColumnKey(column.Id), column.Caption, column.Width,
                Numeric: column.CellFormat == 5, Align: (byte)(((column.CellFormat - 3) << 4) | 1),
                LineArrange: (byte)(column.CellFormat - 2), InitialAscending: column.InitialAscending)).ToArray(),
            Rows(page), selected, maximum, () => Admitted() && selector == current,
            value =>
            {
                if (value is null) { RequestCancel(); return; }
                selected = value; current.Show();
                var members = value.ToHashSet();
                // The native checked-index reader consumes the current list
                // array, rather than the sequence in which boxes were clicked.
                accepted?.Invoke(pages.CurrentOfficerIds.Where(members.Contains).ToArray());
            }, scrollSpeed!, guide: guide, retainOnAccept: true, requireSelection: false,
            singleSelection: advisor, retainOnReturn: true,
            sourceSort: (key, ascending) =>
            {
                if (key == "selection")
                {
                    pages.SortSelection(current.SelectedIds, selectedFirst: !ascending);
                    nextSelectionAscending = !ascending;
                    page = pages.ReadPage(index);
                }
                else
                {
                    int id = key == "name" ? 0 : int.Parse(key, CultureInfo.InvariantCulture);
                    var result = pages.Sort(index, id, ascending);
                    if (!result.Available || result.Value is not { } sorted)
                    {
                        current.ApplyNotice(text.Get("detail_unknown"));
                        SetMeta("selection_sort_issue", result.UnresolvedInput ?? ""); return null;
                    }
                    page = sorted;
                }
                for (int other = 0; other < pages.Groups.Length; other++)
                    pageState[other] = key == "selection" ? (key, ascending) :
                        other == index ? (key, ascending) : (null, false);
                return Rows(page);
            }, groupCaptions: pages.Groups.Select(value => value.Caption).ToArray(),
            selectedGroup: index, groupSelected: ShowGroup, nextSortDirections: nextDirections,
            selectionSortable: true, clearOnlyCaption: clearCaption);
        current.ApplyTitle(title);
        mountSourcePanel?.Invoke(current.SourceChrome);
        current.SelectionChanged = ids =>
        {
            selected = ids;
            selectionChanged?.Invoke(ids);
        };
        current.SelectionConfirmationAdmission = ids => ids.Count != initial.Count || !initial.SetEquals(ids);
        var state = pageState.GetValueOrDefault(index, (null, false));
        current.RestorePageState((scroll, state.Item1, state.Item2));
        selectionChanged?.Invoke(current.SelectedIds);
        if (page.UnresolvedOwner is { } issue)
        { current.ApplyNotice(text.Get("detail_unknown")); SetMeta("selection_page_issue", issue); }
        SetMeta("selection_active_page", index);

        IReadOnlyList<CommandSelectionRow> Rows(OfficerInformationPage value) => value.Rows.Select(row =>
            new CommandSelectionRow(row.OfficerId, columns.Select(column =>
            {
                var cell = row.Cells[sourceSlots[column.Id]];
                return new CommandSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown"),
                    SortUnavailable: !column.CanSort || cell.SortUnresolvedOwner is not null, SourcePreset: cell.PresetIndex);
            }).ToArray(), true)).ToArray();
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing government selector caption.");
    internal void ApplyNotice(string notice) { if (!abandoned) selector?.ApplyNotice(notice); }
    internal void RequestCancel() { if (!abandoned) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; returned = null; accepted = null; scrollSpeed = null;
        mountSourcePanel = null; selectionChanged = null; selector?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
    public override void _Input(InputEvent input)
    {
        if (!Admitted()) return;
        if (input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
        { GetViewport().SetInputAsHandled(); RequestCancel(); }
    }
}
