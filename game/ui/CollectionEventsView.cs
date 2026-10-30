using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The event encyclopedia's original two-column screen. Browsing
/// keeps resource definitions and application progress separate from a world.</summary>
internal partial class CollectionEventsView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private CollectionEventCatalog catalog = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private UiSourceLayout source = null!;
    private Control canvas = null!, rowsHost = null!, contentHost = null!;
    private UiSourceScrollbar scrollbar = null!;
    private UiSourceScrollbar? contentScrollbar;
    private UiSourceParagraph summary = null!;
    private UiSourceText count = null!;
    private UiSourceRoundButton play = null!;
    private readonly List<UiSourceEventButton> cells = [];
    private readonly List<UiSourceTabButton> tabs = [], detailTabs = [];
    private readonly List<(int Column, Control Down, Control Up)> sortArrows = [];
    private IReadOnlyList<CollectionEventRow> rows = [];
    private CollectionEventDetail? selectedDetail;
    private CollectionEventConditions? preparedConditions;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<int>? playbackRequested;
    private Action<int, long>? conditionsRequested;
    private Func<int> scrollSpeed = null!;
    private Action<Action> runUiAction = null!;
    private int tabIndex, detailTab, firstRow = -1, selectedRow = -1;
    private int sortColumn;
    private bool abandoned, sortAscending = true, playbackPrepared;
    private long conditionRequest;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CollectionEventCatalog catalog, Func<bool> admitted, Action returned,
        Func<int> scrollSpeed, Action<Action> runUiAction, Action<int>? playbackRequested = null,
        Action<int, long>? conditionsRequested = null)
    {
        this.repository = repository; this.text = text; this.catalog = catalog; this.admitted = admitted;
        this.returned = returned; this.scrollSpeed = scrollSpeed; this.runUiAction = runUiAction;
        this.playbackRequested = playbackRequested; this.conditionsRequested = conditionsRequested;
        Name = "CollectionEvents"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, catalog.LayoutName));
        foreach (int node in new[] { 3, 61, 63, 64, 66, 68, 78, 105, 57 })
        {
            if (source.Nodes[node].Type == 6) source.Blur(canvas, node, source.Rectangle(node));
            else source.Art(canvas, node, source.Rectangle(node), assets, shader);
        }
        var header = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_header_re_new"));
        header.Art(canvas, 1, header.Rectangle(1), assets, shader);
        header.Text(canvas, 2, default, catalog.Title, font, MapTextPresets.Create()[56]);
        int[] tabNodes = [4, 6, 8, 12, 14];
        for (int index = 0; index < tabNodes.Length; index++)
        {
            int target = index;
            var button = AddTab(tabNodes[index], catalog.Tabs[index].Caption, "CollectionEventCategory" + index,
                index == 0 ? 0x172EDC24u : index == tabNodes.Length - 1 ? 0xD45873AAu : 0x9AFB27B7u);
            tabs.Add(button); button.Pressed += () => runUiAction(() => { if (ReadAdmission() && target != tabIndex) ShowTab(target); });
        }
        AddColumn(22, Caption(0x498), "CollectionEventNumber", 0);
        AddColumn(27, Caption(0x7DE), "CollectionEventCompleted", 1);
        count = source.Text(canvas, 58, default, "", font);
        var listBox = source.Rectangle(39);
        rowsHost = new() { Name = "CollectionEventRows", Position = listBox.Position, Size = listBox.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(rowsHost);
        rowsHost.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        Rect2 summaryBox = source.Rectangle(67);
        summary = new() { Name = "CollectionEventSummary", Position = summaryBox.Position, Size = summaryBox.Size };
        canvas.AddChild(summary);
        summary.Configure(font, MapFontProfile.FromNode(source.Nodes[67], source.Source.Source.ResourceUri));
        for (int index = 0; index < 2; index++)
        {
            int target = index;
            var button = AddTab(index == 0 ? 69 : 71, Caption(index == 0 ? 0x3CE : 0x4A2),
                "CollectionEventDetailTab" + index, index == 0 ? 0x172EDC24u : 0xD45873AAu);
            detailTabs.Add(button);
            button.Pressed += () => runUiAction(() => { if (ReadAdmission() && detailTab != target) { detailTab = target; ShowContent(); } });
        }
        var first = source.Rectangle(83); var last = source.Rectangle(101);
        contentHost = new() { Name = "CollectionEventContent", Position = first.Position,
            Size = new(first.Size.X, last.End.Y - first.Position.Y), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(contentHost);
        contentHost.GuiInput += input => { if (ReadAdmission()) contentScrollbar?.HandleWheel(input); };
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "CollectionEventsReturn",
            Position = footer.Rectangle(4, new(0, 904)).Position, InputAdmission = ReadAdmission };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestBack, UiAudioCue.Cancel);
        play = new() { Name = "CollectionEventPlay", Position = footer.Rectangle(5, new(0, 904)).Position,
            InputAdmission = ReadAdmission, Disabled = true };
        canvas.AddChild(play);
        play.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_kettei",
            () => { if (ReadAdmission() && !play.Disabled && selectedRow >= 0) this.playbackRequested?.Invoke(selectedRow); });
        ShowTab(0); Resized += Fit; Fit();
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("campaign_required", false); SetMeta("playback_host_available", playbackRequested is not null);

        UiSourceTabButton AddTab(int node, string caption, string name, uint layout)
        {
            var box = source.Rectangle(node);
            var button = new UiSourceTabButton { Name = name, Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            canvas.AddChild(button); button.Configure(repository, assets, shader, font, caption, layout); return button;
        }
        void AddColumn(int node, string caption, string name, int column)
        {
            var box = source.Rectangle(node);
            var button = new UiSourceCommandButton { Name = name, Position = box.Position, Size = box.Size,
                InputAdmission = ReadAdmission, Disabled = column == 1 && !catalog.CanSortByCompletion };
            canvas.AddChild(button);
            button.Configure(repository, assets, shader, font, caption,
                () => runUiAction(() =>
                {
                    if (!ReadAdmission() || button.Disabled) return;
                    sortAscending = sortColumn == column ? !sortAscending : true; sortColumn = column;
                    ShowTab(tabIndex, keepSelection: true);
                }));
            var downBox = source.Rectangle(node + 3); downBox.Position -= box.Position;
            var upBox = source.Rectangle(node + 4); upBox.Position -= box.Position;
            sortArrows.Add((column, source.Art(button, node + 3, downBox, assets, shader),
                source.Art(button, node + 4, upBox, assets, shader)));
            if (button.Disabled) button.SetMeta("unresolved_owner", "collection.event.application_occurrence_history");
        }
    }

    private string Caption(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Collection event caption is missing.");

    private void ShowTab(int index, bool keepSelection = false)
    {
        int previousSelection = selectedRow, previousScroll = scrollbar?.Value ?? 0;
        tabIndex = index; selectedRow = -1; selectedDetail = null; firstRow = -1;
        rows = catalog.ReadRows(index, sortColumn, sortAscending);
        bool retained = keepSelection && rows.Any(row => row.RowId == previousSelection);
        if (!retained)
        {
            preparedConditions = null;
            playbackPrepared = false; RemoveMeta("playback_prepared");
            RemoveMeta("conditions_known");
            conditionRequest++; this.conditionsRequested?.Invoke(-1, conditionRequest);
        }
        foreach (var button in cells) { button.Hide(); button.QueueFree(); }
        cells.Clear();
        for (int tab = 0; tab < tabs.Count; tab++) tabs[tab].Selected = tab == index;
        foreach (var arrow in sortArrows)
        {
            arrow.Down.Visible = arrow.Column == sortColumn && !sortAscending;
            arrow.Up.Visible = arrow.Column == sortColumn && sortAscending;
        }
        if (scrollbar is not null) { scrollbar.Hide(); scrollbar.QueueFree(); }
        Rect2 box = source.Rectangle(32);
        scrollbar = new() { Name = "CollectionEventScrollbar", Position = box.Position, Size = box.Size };
        canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, Math.Max(0, rows.Count * 121 - (int)rowsHost.Size.Y),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        count.Text = rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PositionRows(); ShowContent(); summary.Apply(""); summary.RemoveMeta("unresolved_owner"); play.Disabled = true;
        if (keepSelection)
        {
            scrollbar.SetValue(previousScroll);
            if (retained) Select(previousSelection, requestConditions: false);
        }
        SetMeta("catalog_tab", index); SetMeta("catalog_count", rows.Count); SetMeta("selected_row", selectedRow);
        SetMeta("sort_column", sortColumn); SetMeta("sort_ascending", sortAscending);
    }

    private void PositionRows()
    {
        int first = scrollbar.Value / 121;
        if (first != firstRow)
        {
            foreach (var button in cells) { button.Hide(); button.QueueFree(); }
            cells.Clear(); firstRow = first;
            for (int index = first; index < Math.Min(rows.Count, first + 6); index++)
            {
                var row = rows[index];
                var button = new UiSourceEventButton { Name = "CollectionEventRow" + row.RowId,
                    InputAdmission = ReadAdmission, Selected = row.RowId == selectedRow };
                rowsHost.AddChild(button); cells.Add(button);
                button.Configure(repository, assets, shader, font, row, () => runUiAction(() => Select(row.RowId)));
                button.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
            }
        }
        for (int slot = 0; slot < cells.Count; slot++) cells[slot].Position = new(0, (first + slot) * 121 - scrollbar.Value);
    }

    private void Select(int rowId, bool requestConditions = true)
    {
        if (!ReadAdmission()) return;
        bool changed = selectedRow != rowId && requestConditions;
        if (changed)
        {
            preparedConditions = null; playbackPrepared = false;
            RemoveMeta("conditions_known"); RemoveMeta("playback_prepared"); conditionRequest++;
        }
        var row = rows.First(value => value.RowId == rowId);
        selectedRow = rowId; selectedDetail = catalog.ReadDetail(rowId);
        foreach (var cell in cells) cell.Selected = cell.RowId == rowId;
        summary.Apply(selectedDetail.Summary.Text ?? text.Get("detail_unknown"));
        play.Disabled = row.Unlocked != true || playbackRequested is null || conditionsRequested is not null && !playbackPrepared;
        if (selectedDetail.Summary.UnresolvedOwner is { } owner) summary.SetMeta("unresolved_owner", owner);
        else summary.RemoveMeta("unresolved_owner");
        ShowContent(); SetMeta("selected_row", rowId); SetMeta("selected_event", row.EventId);
        if (changed) conditionsRequested?.Invoke(rowId, conditionRequest);
    }

    internal void ApplyConditions(int rowId, long requestId, CollectionEventConditions display)
    {
        if (abandoned || selectedRow != rowId || conditionRequest != requestId || display.RowId != rowId) return;
        preparedConditions = display;
        SetMeta("conditions_known", display.UnresolvedOwner is null);
        if (detailTab == 0) ShowContent();
    }

    internal void SetPlaybackPrepared(int rowId, long requestId, bool available)
    {
        if (abandoned || selectedRow != rowId || conditionRequest != requestId) return;
        playbackPrepared = available;
        play.Disabled = !available || playbackRequested is null || rows.First(row => row.RowId == rowId).Unlocked != true;
        SetMeta("playback_prepared", available);
    }

    private void ShowContent()
    {
        foreach (Node child in contentHost.GetChildren()) { contentHost.RemoveChild(child); child.QueueFree(); }
        contentHost.RemoveMeta("unresolved_owner");
        if (contentScrollbar is not null) { contentScrollbar.Hide(); contentScrollbar.QueueFree(); contentScrollbar = null; }
        for (int index = 0; index < detailTabs.Count; index++) detailTabs[index].Selected = index == detailTab;
        if (selectedDetail is null) return;
        var detail = selectedDetail;
        IReadOnlyList<string> lines = detailTab == 0
            ? detail.ConditionLines.Count != 0 ? detail.ConditionLines
                : detail.ConditionsUnresolvedOwner is not null ? [text.Get("detail_unknown")] : []
            : detail.ContentLines.Count != 0 ? detail.ContentLines
                : detail.ContentParts.Any(part => part.UnresolvedOwner is not null) ? [text.Get("detail_unknown")] : [];
        if (detailTab == 0 && (preparedConditions?.UnresolvedOwner ??
            (preparedConditions is null ? detail.ConditionsUnresolvedOwner : null)) is { } missing)
            contentHost.SetMeta("unresolved_owner", missing);
        else contentHost.RemoveMeta("unresolved_owner");
        var rowSource = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "09_01_01_event_list_offset_02"));
        var contentRows = new Control { Name = "EventDescriptionRows", Size = contentHost.Size, MouseFilter = MouseFilterEnum.Ignore };
        contentHost.AddChild(contentRows);
        int count;
        if (detailTab == 0 && preparedConditions is { } prepared)
        {
            count = prepared.Lines.Count;
            for (int index = 0; index < prepared.Lines.Count; index++)
            {
                var line = prepared.Lines[index];
                var row = new UiSourceEventConditionRow { Name = "EventConditionRow" + index, Position = new(0, index * 52) };
                contentRows.AddChild(row);
                row.Configure(rowSource, assets, shader, font, line.Text, line.IndentPixels,
                    line.ShowBox, line.ShowCheck, line.TextPreset);
            }
            if (prepared.UnresolvedOwner is not null)
            {
                rowSource.Text(contentRows, 22, new(0, count * 52), text.Get("detail_unknown"), font, MapTextPresets.Create()[0]);
                count++;
            }
        }
        else
        {
            count = lines.Count;
            for (int index = 0; index < lines.Count; index++)
            {
                var line = rowSource.Text(contentRows, 22, new(0, index * 52), "", font, MapTextPresets.Create()[0]);
                line.SetKnownColourLine(lines[index]);
            }
        }
        Rect2 scrollBox = source.Rectangle(106);
        contentScrollbar = new() { Name = "CollectionEventContentScrollbar", Position = scrollBox.Position, Size = scrollBox.Size };
        canvas.AddChild(contentScrollbar);
        contentScrollbar.Configure(repository, assets, shader, Math.Max(0, count * 52 - (int)contentHost.Size.Y),
            scrollSpeed(), value => contentRows.Position = new(0, -value), currentScrollSpeed: scrollSpeed);
        SetMeta("detail_tab", detailTab);
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; returned = null; playbackRequested = null;
        preparedConditions = null; playbackPrepared = false;
        conditionRequest++; conditionsRequested?.Invoke(-1, conditionRequest); conditionsRequested = null;
        foreach (var button in cells) button.Disabled = true;
        Hide();
    }
    private void Fit()
    {
        if (canvas is null) return;
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * factor; canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }
    public override void _ExitTree() { Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
