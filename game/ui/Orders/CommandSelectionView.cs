using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CommandSelectionColumn(string Key, string Caption, int Width, bool Numeric = false,
    byte? Align = null, byte? LineArrange = null, bool InitialAscending = true);
internal sealed record CommandSelectionCell(string Text, long? Number = null, string? SortKey = null,
    bool Emphasized = false, uint? FirstGlyphColour = null, int? SortGroup = null, bool SortUnavailable = false,
    PersonLocationOrderKey? LocationOrder = null, int? SourcePreset = null, string? SourceFormattedText = null)
{
    internal static CommandSelectionCell PersonName(string text, string? sourceKey) =>
        new(text, SortKey: sourceKey, SortUnavailable: sourceKey is null);
}
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
    private UiSourceCommonBlur? commonBackdrop;
    private UiSourceScrollbar scrollbar = null!;
    private UiSourceBottomButton selectAll = null!;
    private readonly List<RowDisplay> pool = [];
    private readonly List<int> selection = [];
    private readonly HashSet<int> lockedSelection = [];
    private readonly Dictionary<string, UiSourceColumnButton> headers = [];
    private IReadOnlyList<CommandSelectionRow> publication = [], ordered = [];
    private Func<bool>? admitted;
    private Func<string, bool, IReadOnlyList<CommandSelectionRow>?>? sourceSort;
    private Func<int,bool>? rowAdmission;
    private bool returnAllowed=true;
    private Func<IReadOnlyList<int>, bool>? confirmationAdmission;
    internal Func<IReadOnlyList<int>, bool>? SelectionConfirmationAdmission
    {
        get => confirmationAdmission;
        set { confirmationAdmission = value; if (!abandoned && chrome is not null) RefreshSelection(); }
    }
    private Action<IReadOnlyList<int>?>? closed;
    internal Action<int?>? RowHovered { get; set; }
    internal Action<IReadOnlyList<int>>? SortOrderChanged { get; set; }
    private int? hoveredRow;
    private int maximum, eligibleCount;
    private bool abandoned, ascending, retainOnAccept, retainOnReturn, requireSelection, singleSelection;
    private string? sortKey;
    private string selectCaption = "", clearCaption = "";
    private string? clearOnlyCaption;
    internal Action<IReadOnlyList<int>>? SelectionChanged { get; set; }
    internal AdministrationPageChrome SourceChrome => chrome;
    internal void ApplyTitle(string value) { if (!abandoned) chrome.ApplyTitle(value); }

    // Actual data-selector callers opt in when their source owns a common backdrop.
    internal void ConfigureCommonBackdrop(GameResourceRepository repository)
    {
        if (abandoned || commonBackdrop is not null) return;
        commonBackdrop = new UiSourceCommonBlur();
        AddChild(commonBackdrop); MoveChild(commonBackdrop, 0);
        commonBackdrop.Configure(repository);
    }

    internal void Configure(GameResourceRepository repository, PreviewText text, int titleMessage,
        IReadOnlyList<CommandSelectionColumn> columns, IReadOnlyList<CommandSelectionRow> rows,
        IReadOnlyList<int> initial, int maximum, Func<bool> admitted,
        Action<IReadOnlyList<int>?> closed, Func<int> scrollSpeed, string? guide = null, bool retainOnAccept = false,
        bool requireSelection = false, bool singleSelection = false, bool activateRows = false,
        bool returnOnly = false, bool retainOnReturn = false,
        Func<string, bool, IReadOnlyList<CommandSelectionRow>?>? sourceSort = null,
        IReadOnlyList<string>? groupCaptions = null, int selectedGroup = 0, Action<int>? groupSelected = null,
        IDictionary<string, bool>? nextSortDirections = null, bool selectionSortable = false, string? clearOnlyCaption = null, AdministrationPageChrome.Resources? resources = null)
    {
        if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        this.clearOnlyCaption = clearOnlyCaption;
        this.maximum = maximum; this.admitted = admitted; this.closed = closed; publication = ordered = rows;
        this.sourceSort = sourceSort;
        this.retainOnAccept = retainOnAccept;
        this.retainOnReturn = retainOnReturn;
        this.requireSelection = requireSelection;
        this.singleSelection = singleSelection;
        eligibleCount = rows.Count(row => row.Enabled);
        Name = "CommandSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, titleMessage, RequestCancel, RequestApply,
            showFooterHint: true, returnOnly: returnOnly, resources: resources);
        chrome.ApplyDefaultHint(guide ?? "");
        var source = new UiSourceLayout(repository, 0xA3B4A82E);
        source.Blur(chrome.Canvas, 2, source.Rectangle(2, new(191, 147)));
        source.Art(chrome.Canvas, 3, source.Rectangle(3, new(191, 147)), chrome.Assets, chrome.SpriteShader);
        if (groupCaptions is { Count: > 0 })
        {
            var mount = source.Rectangle(4, new(191, 147)).Position;
            for (int index = 0; index < groupCaptions.Count; index++)
            {
                int group = index;
                string caption = groupCaptions[index];
                float tabWidth = caption.Length * 28 + 36;
                var tab = new UiSourceTabButton { Name = "PersonTab" + index, Position = mount,
                    Size = new(tabWidth, 64), Selected = index == selectedGroup, InputAdmission = ReadAdmission };
                chrome.Canvas.AddChild(tab);
                tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption,
                    index == 0 ? 0x172EDC24u : index == groupCaptions.Count - 1 ? 0xD45873AAu : 0x9AFB27B7u);
                tab.Pressed += () => { if (ReadAdmission()) groupSelected?.Invoke(group); };
                mount.X += tabWidth - 6;
            }
        }
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
        var checkHeader = new UiSourceColumnButton { Name = "PersonnelColumnselection", Size = new(48, 48),
            Disabled = !selectionSortable, InputAdmission = ReadAdmission };
        table.AddChild(checkHeader); checkHeader.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, check, personList: true);
        if (selectionSortable)
        {
            if (sourceSort is null) throw new ArgumentException("Checked-column sorting requires the retained source order.");
            headers.Add("selection", checkHeader);
            checkHeader.Pressed += () => ApplySort("selection", false, -1);
        }
        for (int index = 0; index < columns.Count; index++)
        {
            int columnIndex = index; var column = columns[index];
            var header = new UiSourceColumnButton { Name = "PersonnelColumn" + column.Key,
                Position = new(x, 0), Size = new(column.Width, 48), InputAdmission = ReadAdmission,
                Disabled = rows.Any(row => row.Cells[columnIndex].SortUnavailable) };
            table.AddChild(header); header.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, column.Caption, personList: true);
            headers.Add(column.Key, header);
            header.Pressed += () => ApplySort(column.Key, column.InitialAscending, columnIndex);
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
            column.Align ?? (column.Numeric ? (byte)0x21 : (byte)0x01), column.LineArrange ?? 1)).ToArray();
        for (int index = 0; index < Math.Min(14, rows.Count); index++)
        {
            var button = new UiSourcePersonRow { Name = "PersonnelVisibleRow" + index, InputAdmission = ReadAdmission };
            clip.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, check, rowColumns,
                columns.Select(_ => new DeploymentPersonCell("")).ToArray(), width);
            var display = new RowDisplay(button); pool.Add(display);
            button.Pressed += () =>
            {
                if (!ReadAdmission() || display.Entry is not { } entry || !RowEnabled(entry)) return;
                if (activateRows) { Finish([entry.Id]); return; }
                if (this.singleSelection) { selection.Clear(); selection.Add(entry.Id); }
                else if (!selection.Remove(entry.Id) && selection.Count < this.maximum) selection.Add(entry.Id);
                RefreshSelection(); SelectionChanged?.Invoke(selection.ToArray());
            };
            button.MouseEntered += () => { if (ReadAdmission()) { chrome.ApplyHoverHint(display.Entry?.DisabledReason); PublishRowHover(display.Entry?.Id); } };
            button.MouseExited += () => { if (!abandoned) chrome.ApplyHoverHint(null); if(hoveredRow==display.Entry?.Id)PublishRowHover(null); };
            button.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        selectAll = new() { Position = footer.Rectangle(14, new(0, 904)).Position, InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(selectAll);
        selectAll.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, selectCaption, () =>
        {
            if (!ReadAdmission()) return;
            if (this.clearOnlyCaption is not null || EditableSelectedCount() == EditableMaximum()) selection.RemoveAll(id => !lockedSelection.Contains(id));
            else foreach (var row in ordered)
            {
                if (selection.Count >= this.maximum) break;
                if (RowEnabled(row) && !selection.Contains(row.Id)) selection.Add(row.Id);
            }
            RefreshSelection(); SelectionChanged?.Invoke(selection.ToArray());
        });
        selectAll.Visible = !singleSelection || clearOnlyCaption is not null;
        foreach (int id in initial)
            if (selection.Count < maximum && !selection.Contains(id) && publication.FirstOrDefault(row => row.Id == id) is { } row && (row.Enabled || row.LockedSelected))
            { selection.Add(id); if (row.LockedSelected) lockedSelection.Add(id); }
        PositionRows(); RefreshSelection();

        void ApplySort(string key, bool initialAscending, int columnIndex)
        {
            if (!ReadAdmission() || headers[key].Disabled) return;
            bool priorAscending = ascending; string? priorKey = sortKey;
            ascending = nextSortDirections is { } directions
                ? directions.TryGetValue(key, out bool next) ? next : initialAscending
                : sortKey == key ? !ascending : initialAscending;
            sortKey = key;
            if (this.sourceSort is { } sort)
            {
                var sorted = sort(key, ascending);
                if (sorted is null) { ascending = priorAscending; sortKey = priorKey; return; }
                ordered = sorted;
            }
            else ordered = publication.OrderBy(row => row,
                Comparer<CommandSelectionRow>.Create((a, b) => Compare(a, b, columnIndex))).ToArray();
            if (nextSortDirections is not null) nextSortDirections[key] = !ascending;
            foreach (var (name, item) in headers) item.ApplySortDirection(name == sortKey ? ascending : null);
            PositionRows();
            if (SortOrderChanged is { } changed) changed(ordered.Select(row => row.Id).ToArray());
        }

        int Compare(CommandSelectionRow a, CommandSelectionRow b, int index)
        {
            var first = a.Cells[index]; var second = b.Cells[index];
            if (first.LocationOrder is { } firstLocation && second.LocationOrder is { } secondLocation)
                return firstLocation.Compare(secondLocation, ascending);
            int groups = (first.SortGroup ?? 0).CompareTo(second.SortGroup ?? 0);
            if (groups != 0) return groups;
            bool textKey = first.SortKey is not null || second.SortKey is not null;
            if (!textKey && columns[index].Numeric && first.Number.HasValue != second.Number.HasValue)
                return first.Number.HasValue ? -1 : 1;
            int comparison = !textKey && first.Number is long left && second.Number is long right ? left.CompareTo(right)
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
            if (!ReferenceEquals(display.Entry, entry)) display.Button.ApplyCells(entry.Cells.Select(cell =>
                new DeploymentPersonCell(cell.Text, cell.Emphasized, cell.FirstGlyphColour, cell.SourcePreset, cell.SourceFormattedText)).ToArray());
            display.Entry = entry; display.Button.Position = new(0, index * 48 - scrollbar.Value); display.Button.Show();
        }
        RefreshSelection();
        PublishRowHover(ReadAdmission()?pool.FirstOrDefault(row=>row.Button.IsVisibleInTree()&&row.Button.IsHovered())?.Entry?.Id:null);
    }
    private void RefreshSelection()
    {
        foreach (var display in pool)
        {
            bool selected = display.Entry is { } entry && selection.Contains(entry.Id);
            display.Button.Selected = selected;
            display.Button.Disabled = display.Entry is not { } row || !RowEnabled(row) || !singleSelection && !selected && selection.Count >= maximum;
        }
        if (selectAll is not null)
        {
            selectAll.Disabled = clearOnlyCaption is not null ? EditableSelectedCount() == 0 : maximum == 0 || eligibleCount == 0;
            selectAll.ApplyCaption(clearOnlyCaption ?? (EditableSelectedCount() == EditableMaximum() ? clearCaption : selectCaption));
        }
        chrome.ApplyReturn(returnAllowed);
        bool selectedAdmitted=selection.All(id=>publication.FirstOrDefault(row=>row.Id==id) is { } row&&
            (RowEnabled(row)||lockedSelection.Contains(id)));
        chrome.ApplyDecision((!requireSelection || selection.Count > 0) && selectedAdmitted &&
            (confirmationAdmission?.Invoke(selection) ?? true), true);
    }
    private int EditableSelectedCount() => selection.Count - lockedSelection.Count;
    private int EditableMaximum() => Math.Min(Math.Max(0, maximum - lockedSelection.Count), eligibleCount);
    internal void RequestCancel() { if(returnAllowed)Finish(null); }
    internal IReadOnlyList<int> SelectedIds => selection.ToArray();
    internal bool IsLeadingRow(int id, int count)
    {
        for (int index = 0; index < Math.Min(count, ordered.Count); index++)
            if (ordered[index].Id == id) return true;
        return false;
    }
    internal bool AreLeadingRowsSelected(int count)
    {
        if (count < 0 || ordered.Count < count) return false;
        for (int index = 0; index < count; index++)
            if (!selection.Contains(ordered[index].Id)) return false;
        return true;
    }
    /// <summary>A live controller may narrow this instance's real candidates
    /// and return control. Ordinary selectors retain their published admission.</summary>
    internal void ApplySelectionAdmission(Func<int,bool>? allowed,bool canReturn)
    {
        if(abandoned)return;
        rowAdmission=allowed;returnAllowed=canReturn;
        eligibleCount=publication.Count(RowEnabled);RefreshSelection();
    }
    private bool RowEnabled(CommandSelectionRow row)=>row.Enabled&&(rowAdmission?.Invoke(row.Id)??true);
    internal (int Scroll, string? Key, bool Ascending) CapturePageState() => (scrollbar.Value, sortKey, ascending);
    internal void RestorePageState((int Scroll, string? Key, bool Ascending) state)
    {
        if (abandoned) return;
        sortKey = state.Key; ascending = state.Ascending;
        foreach (var (key, header) in headers) header.ApplySortDirection(key == sortKey ? ascending : null);
        scrollbar.SetValue(state.Scroll);
    }
    internal void ReplaceSelection(IReadOnlyList<int> ids)
    {
        if (abandoned) return;
        selection.Clear(); lockedSelection.Clear();
        foreach (int id in ids)
            if (selection.Count < maximum && !selection.Contains(id) && publication.FirstOrDefault(row => row.Id == id) is { } row &&
                (row.Enabled || row.LockedSelected))
            { selection.Add(id); if (row.LockedSelected) lockedSelection.Add(id); }
        RefreshSelection(); SelectionChanged?.Invoke(selection.ToArray());
    }
    // A multi-tab original selector has one row order and separate column
    // values. Its owner calls this only after a real header sort/tab creation.
    internal void ApplyRowOrder(IReadOnlyList<int> ids)
    {
        if (abandoned) return;
        var rows = publication.ToDictionary(row => row.Id);
        ordered = ids.Select(id => rows[id]).ToArray();
        sortKey = null;
        foreach (var header in headers.Values) header.ApplySortDirection(null);
        PositionRows();
    }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    private void RequestApply()
    {
        if ((!requireSelection || selection.Count > 0) &&
            selection.All(id => publication.FirstOrDefault(row => row.Id == id) is { } row &&
                (RowEnabled(row) || lockedSelection.Contains(id))) &&
            (confirmationAdmission?.Invoke(selection) ?? true)) Finish(selection.ToArray());
    }
    private void PublishRowHover(int? id)
    {if(hoveredRow==id)return;hoveredRow=id;RowHovered?.Invoke(id);}

    private void Finish(IReadOnlyList<int>? value)
    {
        if (!ReadAdmission()) return;
        PublishRowHover(null);
        if (value is not null && retainOnAccept || value is null && retainOnReturn)
        { Hide(); closed?.Invoke(value); return; }
        var callback = closed; closed = null; admitted = null; Hide(); callback?.Invoke(value);
    }
    internal void Abandon()
    {
        SelectionChanged = null;
        PublishRowHover(null);RowHovered=null;SortOrderChanged=null;
        abandoned = true; closed = null; admitted = null; sourceSort = null; rowAdmission = null; confirmationAdmission = null;
        scrollbar?.SetProcessInput(false); chrome?.Abandon(); Hide();
    }
}
