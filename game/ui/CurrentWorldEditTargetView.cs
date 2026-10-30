using Godot;
using San14.Data;

/// <summary>The normal grouped data selector over the editor's exact target
/// roster. Column readers and sorters are the existing current-world sources.</summary>
internal partial class CurrentWorldEditTargetView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private IPagedInformationListContent pages = null!;
    private MapInformationListView? list;
    private UiSourceRoundButton? confirm;
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private Func<int> scrollSpeed = null!;
    private readonly Dictionary<int, (int Scroll, string? Key, bool Ascending)> states = [];
    private readonly Dictionary<int, IDictionary<string, bool>> directions = [];
    private string title = "", guide = "";
    private int group;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, IPagedInformationListContent pages,
        string title, string guide, Func<bool> admitted, Action<int?> closed, Func<int> scrollSpeed)
    {
        this.repository = repository; this.text = text; this.pages = pages; this.title = title; this.guide = guide;
        this.admitted = admitted; this.closed = closed; this.scrollSpeed = scrollSpeed;
        Name = "CurrentWorldEditTargetSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); ShowGroup(0);
    }
    private bool CanSelect() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private void ShowGroup(int index)
    {
        if (abandoned || (uint)index >= (uint)pages.Groups.Count) return;
        if (list is { } previous)
        {
            var retained = states.GetValueOrDefault(group);
            states[group] = (previous.ScrollValue, retained.Key, retained.Ascending);
            previous.Abandon(); RemoveChild(previous); previous.QueueFree();
        }
        group = index; var descriptor = pages.Groups[index]; var current = pages.ReadPage(index);
        var columns = descriptor.Columns.Where(column => column.Visible).ToArray();
        var columnSlots = descriptor.Columns.Select((column, slot) => (column.Id, slot)).ToDictionary(value => value.Id, value => value.slot);
        var restored = states.TryGetValue(index, out var saved) ? saved : (Scroll: 0, Key: (string?)null, Ascending: true);
        if (!directions.TryGetValue(index, out var sortDirections)) directions[index] = sortDirections = new Dictionary<string, bool>();
        var next = new MapInformationListView(); list = next; AddChild(next);
        next.Configure(repository, text, title,
            columns.Select(column => new MapInformationListColumn(Key(column.Id), column.Caption, column.Width,
                column.CanSort, column.InitialAscending)).ToArray(), Rows(current),
            () => CanSelect() && list == next, RequestBack, sourceIndex =>
            {
                if (!CanSelect() || list != next) return;
                pages.SelectSourceIndex(sourceIndex); next.SelectSourceIndex(sourceIndex);
                confirm!.Disabled = pages.SelectedIdentity is null;
            }, scrollSpeed, (key, ascending) =>
            {
                var result = pages.Sort(index, key == "name" ? 0 : int.Parse(key), ascending);
                if (!result.Available || result.Value is not { } sorted)
                { GD.PushWarning(result.UnresolvedInput ?? "current-world-edit.target_sort"); return null; }
                current = sorted; states[index] = (next.ScrollValue, key, ascending); return Rows(sorted);
            }, guide, pages.Groups.Select(page => page.Caption).ToArray(), index, ShowGroup,
            completeColumnProfile: true, normalColumnLayout: true, returnNode: 4,
            initialSortColumn: restored.Key, initialSortAscending: restored.Ascending,
            retainSortDirections: true, retainedSortDirections: sortDirections);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        confirm = new UiSourceRoundButton { Name = "ConfirmCurrentWorldEditTarget", Position = footer.Rectangle(5, new(0, 904)).Position,
            InputAdmission = CanSelect, Disabled = pages.SelectedIdentity is null };
        next.SourceCanvas.AddChild(confirm);
        confirm.Configure(repository, next.SourceAssets, next.SourceShader, text.SourceLocale, "000_cmn_txt_decision_kettei",
            () => { if (CanSelect() && pages.SelectedIdentity is int id) closed?.Invoke(id); });
        if (pages.SelectedIdentity is int selected && current.Rows.FirstOrDefault(row => row.Identity == selected) is { } row)
            next.SelectSourceIndex(row.SourceIndex);
        next.RestoreScroll(restored.Scroll);

        IReadOnlyList<MapInformationListRow> Rows(PagedInformationPage page) => page.Rows.Select(row =>
        {
            var cells = columns.Select(column => row.Cells[columnSlots[column.Id]]).ToArray();
            return new MapInformationListRow(row.SourceIndex,
                cells.Select(cell => cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown")).ToArray(), true,
                cells.Select((cell, slot) => new MapInformationListCellStyle(columns[slot].CellFormat,
                    cell.PresetIndex, cell.FirstGlyphColour, cell.IconSpriteId)).ToArray());
        }).ToArray();
    }
    private static string Key(int id) => id == 0 ? "name" : id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    internal void AdoptContent(IPagedInformationListContent replacement)
    {
        if (abandoned) return;
        if (pages.SelectedIdentity is int selected)
        {
            var result = replacement.AdoptDetailOrder(group, pages.CurrentIdentities, selected, pages.NameSortAscending);
            if (!result.Available) throw new InvalidDataException(result.UnresolvedInput);
        }
        pages = replacement; ShowGroup(group);
    }
    internal void RequestBack() { if (CanSelect()) closed?.Invoke(null); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; closed = null; list?.Abandon();
        if (confirm is not null) { confirm.Disabled = true; confirm.InputAdmission = null; }
        Hide();
    }
    public override void _ExitTree() => Abandon();
}
