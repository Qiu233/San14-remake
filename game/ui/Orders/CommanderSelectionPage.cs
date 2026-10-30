using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Globalization;

/// <summary>The real five-page Person selector over one strategy publication.
/// Membership, capacity and final authority remain with the ordinary command.</summary>
internal partial class CommanderSelectionPage : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private CommanderSelectionView model = null!;
    private OfficerInformationPageSet? pages;
    private CommandSelectionView selector = null!;
    private Action? returned;
    private Action<IReadOnlyList<int>>? accepted;
    private Func<int>? scrollSpeed;
    private IReadOnlyList<int> selected = [];
    private readonly Dictionary<int, (string? Key, bool Ascending)> pageState = [];
    private readonly Dictionary<int, Dictionary<string, bool>> directions = [];
    private int group = -1, scroll;
    private bool nextSelectionAscending = true;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, Theme theme,
        CommanderSelectionView model, Action returned, Action<IReadOnlyList<int>> accepted, Func<int>? scrollSpeed = null)
    {
        this.repository = repository; this.text = text; this.model = model;
        this.returned = returned; this.accepted = accepted; pages = model.PageSource;
        this.scrollSpeed = scrollSpeed ?? (() => 1);
        Name = "CommanderSelectionPage"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var candidates = model.Candidates.Select(person => person.OfficerId).ToHashSet();
        selected = model.Slots.Take(model.Capacity ?? 0).Where(slot => slot.OfficerId is int id && candidates.Contains(id))
            .Select(slot => slot.OfficerId!.Value).ToArray();
        ShowGroup(0);
        SetMeta("source_layout_name", "00_03_00_list_re");
        SetMeta("source_data_type", "CPersonData");
        SetMeta("selection_maximum", model.Capacity ?? -1);
        SetMeta("selection_page_count", pages?.Groups.Length ?? 0);
        SetMeta("selection_initial_sort", "selected-first-after-five-effective-abilities");
    }

    private bool Admitted() => !abandoned && IsInsideTree() && IsVisibleInTree() && CanProcess();

    private void ShowGroup(int index)
    {
        if (abandoned || group == index) return;
        if (selector is not null)
        {
            selected = selector.SelectedIds;
            var previous = selector.CapturePageState();
            scroll = previous.Scroll; pageState[group] = (previous.Key, previous.Ascending);
            selector.Abandon(); RemoveChild(selector); selector.QueueFree();
        }
        group = index;
        OfficerInformationGroup? descriptor = pages is null ? null : pages.Groups[index];
        OfficerInformationPage? page = pages?.ReadPage(index);
        var columns = descriptor?.Columns.Where(column => column.Visible).ToArray() ?? [];
        var sourceSlots = descriptor?.Columns.Select((column, slot) => (column.Id, slot))
            .ToDictionary(value => value.Id, value => value.slot) ?? [];
        if (!directions.TryGetValue(index, out var nextDirections)) directions.Add(index, nextDirections = []);
        nextDirections["selection"] = nextSelectionAscending;
        var current = new CommandSelectionView { Name = "CommanderCandidates" };
        selector = current; AddChild(current);
        current.Configure(repository, text, 0x32E,
            columns.Length == 0 ? [new("name", Message(0x211), 132)] : columns.Select(column =>
                new CommandSelectionColumn(column.Id == 0 ? "name" : column.Id.ToString(CultureInfo.InvariantCulture),
                    column.Caption, column.Width, Numeric: column.CellFormat == 5,
                    Align: (byte)(((column.CellFormat - 3) << 4) | 1),
                    LineArrange: (byte)(column.CellFormat - 2), InitialAscending: column.InitialAscending)).ToArray(),
            page is null ? [] : Rows(page), selected, model.Capacity ?? 0,
            () => Admitted() && selector == current,
            value =>
            {
                if (value is null) { RequestCancel(); return; }
                // The normal command can reject a stale publication. Keep this
                // same draft available for its real error/notice continuation.
                current.Show(); selected = value; accepted?.Invoke(value);
            }, scrollSpeed!, guide: model.UnresolvedOwner is null ? model.GuideText ?? "" : text.Get("detail_unknown"), retainOnAccept: true,
            sourceSort: (key, ascending) =>
            {
                if (pages is null) return null;
                if (key == "selection")
                {
                    pages.SortSelection(current.SelectedIds, selectedFirst: !ascending);
                    nextSelectionAscending = !ascending;
                    page = pages.ReadPage(index);
                }
                else
                {
                    int column = key == "name" ? 0 : int.Parse(key, CultureInfo.InvariantCulture);
                    var sorted = pages.Sort(index, column, ascending);
                    if (!sorted.Available || sorted.Value is not { } value)
                    { current.ApplyNotice(text.Get("detail_unknown")); SetMeta("selection_sort_issue", sorted.UnresolvedInput ?? ""); return null; }
                    page = value;
                }
                // The native list shares its current sorting indicator across
                // pages. Next directions remain owned by each column/page.
                for (int other = 0; other < pages.Groups.Length; other++)
                    pageState[other] = key == "selection" ? ("selection", ascending) :
                        other == index ? (key, ascending) : (null, false);
                return Rows(page);
            }, groupCaptions: pages?.Groups.Select(value => value.Caption).ToArray(),
            selectedGroup: index, groupSelected: ShowGroup, nextSortDirections: nextDirections,
            selectionSortable: pages is not null);
        current.Name = "CommanderCandidates";
        current.SelectionConfirmationAdmission = _ => model.Available && model.Capacity.HasValue && pages is not null;
        (string? Key, bool Ascending) state = pageState.GetValueOrDefault(index, ("selection", false));
        current.RestorePageState((scroll, state.Key, state.Ascending));
        if (page?.UnresolvedOwner is { } issue)
        { current.ApplyNotice(text.Get("detail_unknown")); SetMeta("selection_page_issue", issue); }
        SetMeta("selection_active_page", index);

        IReadOnlyList<CommandSelectionRow> Rows(OfficerInformationPage value) => value.Rows.Select(row =>
            new CommandSelectionRow(row.OfficerId, columns.Select(column =>
            {
                var cell = row.Cells[sourceSlots[column.Id]];
                return new CommandSelectionCell(cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown"),
                    SortUnavailable: !column.CanSort || cell.SortUnresolvedOwner is not null, SourcePreset: cell.PresetIndex);
            }).ToArray(), model.Available && model.Capacity.HasValue)).ToArray();
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing commander selection caption.");
    internal void ApplyNotice(string notice) { if (!abandoned) selector?.ApplyNotice(notice); }
    internal void RequestCancel() { if (!abandoned) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; returned = null; accepted = null; scrollSpeed = null; selector?.Abandon(); pages = null; Hide();
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
