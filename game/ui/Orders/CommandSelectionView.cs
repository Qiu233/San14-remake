using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CommandSelectionColumn(string Key, string Caption, int Width, bool Numeric = false);
internal sealed record CommandSelectionCell(string Text, long? Number = null, string? SortKey = null);
internal sealed record CommandSelectionRow(int Id, IReadOnlyList<CommandSelectionCell> Cells,
    bool Enabled, string? DisabledReason = null, bool LockedSelected = false);

/// <summary>A source command selector over a retained candidate publication.
/// Only fourteen row controls are mounted, including for very large rosters.</summary>
internal partial class CommandSelectionView : Control
{
    private sealed record RowDisplay(UiSourcePersonRow Button)
    {
        internal CommandSelectionRow? Entry { get; set; }
    }
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scrollbar = null!;
    private UiSourceBottomButton selectAll = null!;
    private readonly List<RowDisplay> pool = [];
    private readonly List<int> selection = [];
    private readonly HashSet<int> lockedSelection = [];
    private readonly Dictionary<string, UiSourceColumnButton> headers = [];
    private IReadOnlyList<CommandSelectionRow> publication = [], ordered = [];
    private Func<bool>? admitted;
    private Action<IReadOnlyList<int>?>? closed;
    private int maximum, eligibleCount;
    private bool abandoned, ascending, retainOnAccept, requireSelection, singleSelection;
    private string? sortKey;
    private string selectCaption = "", clearCaption = "";

    internal void Configure(GameResourceRepository repository, PreviewText text, int titleMessage,
        IReadOnlyList<CommandSelectionColumn> columns, IReadOnlyList<CommandSelectionRow> rows,
        IReadOnlyList<int> initial, int maximum, Func<bool> admitted,
        Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed, string? guide = null, bool retainOnAccept = false,
        bool requireSelection = false, bool singleSelection = false)
    {
        if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        this.maximum = maximum; this.admitted = admitted; this.closed = closed; publication = ordered = rows;
        this.retainOnAccept = retainOnAccept;
        this.requireSelection = requireSelection;
        this.singleSelection = singleSelection;
        eligibleCount = rows.Count(row => row.Enabled);
        Name = "CommandSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, titleMessage, RequestCancel, RequestApply, showFooterHint: true);
        chrome.ApplyDefaultHint(guide ?? "");
        var source = new UiSourceLayout(repository, 0xA3B4A82E);
        source.Blur(chrome.Canvas, 2, source.Rectangle(2, new(191, 147)));
        source.Art(chrome.Canvas, 3, source.Rectangle(3, new(191, 147)), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing person selector caption.");
        string check = Message(0x628); selectCaption = Message(0x3A); clearCaption = Message(0x3B);
        int width = Math.Max(1464, 48 + columns.Sum(column => column.Width));
        var viewport = new ScrollContainer { Position = new(213, 214), Size = new(1464, 664),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        chrome.Canvas.AddChild(viewport);
        var table = new Control { CustomMinimumSize = new(width, 646), MouseFilter = MouseFilterEnum.Pass };
        viewport.AddChild(table);
        int x = 48;
        var checkHeader = new UiSourceColumnButton { Size = new(48, 48), Disabled = true };
        table.AddChild(checkHeader); checkHeader.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, check, personList: true);
        for (int index = 0; index < columns.Count; index++)
        {
            int columnIndex = index; var column = columns[index];
            var header = new UiSourceColumnButton { Name = "PersonnelColumn" + column.Key,
                Position = new(x, 0), Size = new(column.Width, 48), InputAdmission = ReadAdmission };
            table.AddChild(header); header.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, column.Caption, personList: true);
            headers.Add(column.Key, header);
            header.Pressed += () =>
            {
                if (!ReadAdmission()) return;
                ascending = sortKey == column.Key ? !ascending : true; sortKey = column.Key;
                ordered = publication.OrderBy(row => row, Comparer<CommandSelectionRow>.Create((a, b) => Compare(a, b, columnIndex))).ToArray();
                foreach (var (key, item) in headers) item.ApplySortDirection(key == sortKey ? ascending : null);
                PositionRows();
            };
            x += column.Width;
        }
        var clip = new Control { Position = new(0, 50), Size = new(width, 596), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        table.AddChild(clip);
        scrollbar = new() { Position = new(1687, 265), Size = new(16, 597) };
        chrome.Canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, rows.Count * 48 - 596),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        clip.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        var rowColumns = columns.Select((column, index) => new DeploymentPersonColumn(index, column.Width, 0,
            column.Numeric ? (byte)0x21 : (byte)0x01, 1)).ToArray();
        for (int index = 0; index < Math.Min(14, rows.Count); index++)
        {
            var button = new UiSourcePersonRow { Name = "PersonnelVisibleRow" + index, InputAdmission = ReadAdmission };
            clip.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, check, rowColumns,
                columns.Select(_ => new DeploymentPersonCell("")).ToArray(), width);
            var display = new RowDisplay(button); pool.Add(display);
            button.Pressed += () =>
            {
                if (!ReadAdmission() || display.Entry is not { Enabled: true } entry) return;
                if (this.singleSelection) { selection.Clear(); selection.Add(entry.Id); }
                else if (!selection.Remove(entry.Id) && selection.Count < this.maximum) selection.Add(entry.Id);
                RefreshSelection();
            };
            button.MouseEntered += () => { if (ReadAdmission()) chrome.ApplyHoverHint(display.Entry?.DisabledReason); };
            button.MouseExited += () => { if (!abandoned) chrome.ApplyHoverHint(null); };
            button.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        selectAll = new() { Position = footer.Rectangle(14, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(selectAll);
        selectAll.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, selectCaption, () =>
        {
            if (!ReadAdmission()) return;
            if (EditableSelectedCount() == EditableMaximum()) selection.RemoveAll(id => !lockedSelection.Contains(id));
            else foreach (var row in ordered)
            {
                if (selection.Count >= this.maximum) break;
                if (row.Enabled && !selection.Contains(row.Id)) selection.Add(row.Id);
            }
            RefreshSelection();
        });
        selectAll.Visible = !singleSelection;
        foreach (int id in initial)
            if (selection.Count < maximum && !selection.Contains(id) && publication.FirstOrDefault(row => row.Id == id) is { } row && (row.Enabled || row.LockedSelected))
            { selection.Add(id); if (row.LockedSelected) lockedSelection.Add(id); }
        PositionRows(); RefreshSelection();

        int Compare(CommandSelectionRow a, CommandSelectionRow b, int index)
        {
            var first = a.Cells[index]; var second = b.Cells[index];
            if (columns[index].Numeric && first.Number.HasValue != second.Number.HasValue)
                return first.Number.HasValue ? -1 : 1;
            int comparison = first.Number is long left && second.Number is long right ? left.CompareTo(right)
                : columns[index].Key == "name" ? PersonNameOrder.Compare(first.SortKey ?? first.Text, second.SortKey ?? second.Text)
                : StringComparer.Ordinal.Compare(first.SortKey ?? first.Text, second.SortKey ?? second.Text);
            return ascending ? comparison : -comparison;
        }
    }

    private bool ReadAdmission() => !abandoned && closed is not null && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void PositionRows()
    {
        int first = scrollbar.Value / 48;
        for (int slot = 0; slot < pool.Count; slot++)
        {
            var display = pool[slot]; int index = first + slot;
            if (index >= ordered.Count) { display.Entry = null; display.Button.Hide(); continue; }
            var entry = ordered[index];
            if (!ReferenceEquals(display.Entry, entry)) display.Button.ApplyCells(entry.Cells.Select(cell => new DeploymentPersonCell(cell.Text)).ToArray());
            display.Entry = entry; display.Button.Position = new(0, index * 48 - scrollbar.Value); display.Button.Show();
        }
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        foreach (var display in pool)
        {
            bool selected = display.Entry is { } entry && selection.Contains(entry.Id);
            display.Button.Selected = selected;
            display.Button.Disabled = display.Entry?.Enabled != true || !singleSelection && !selected && selection.Count >= maximum;
        }
        if (selectAll is not null)
        {
            selectAll.Disabled = maximum == 0 || eligibleCount == 0;
            selectAll.ApplyCaption(EditableSelectedCount() == EditableMaximum() ? clearCaption : selectCaption);
        }
        chrome.ApplyDecision(!requireSelection || selection.Count > 0, true);
    }
    private int EditableSelectedCount() => selection.Count - lockedSelection.Count;
    private int EditableMaximum() => Math.Min(Math.Max(0, maximum - lockedSelection.Count), eligibleCount);
    internal void RequestCancel() => Finish(null);
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    private void RequestApply() { if (!requireSelection || selection.Count > 0) Finish(selection.ToArray()); }
    private void Finish(IReadOnlyList<int>? value)
    {
        if (!ReadAdmission()) return;
        if (value is not null && retainOnAccept) { Hide(); closed?.Invoke(value); return; }
        var callback = closed; closed = null; admitted = null; Hide(); callback?.Invoke(value);
    }
    internal void Abandon()
    {
        abandoned = true; closed = null; admitted = null;
        scrollbar?.SetProcessInput(false); chrome?.Abandon(); Hide();
    }
}
