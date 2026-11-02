using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Globalization;

/// <summary>The Unit pane's two actual child lists: formation mode1 accepts
/// on release; tactic mode2 commits up to three checked body occurrences.</summary>
internal partial class DeploymentUnitDefinitionSelectionView : Control
{
    private sealed class RowDisplay(UiSourcePersonRow button)
    { internal UiSourcePersonRow Button { get; } = button; internal DefinitionInformationRow? Entry; }
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private DeploymentDefinitionSelection model = null!;
    private Func<bool>? admitted;
    private Action<IReadOnlyList<int>?>? completed;
    private Func<int> scrollSpeed = null!;
    private MapInformationListView? single;
    private AdministrationPageChrome? chrome;
    private Control headersHost = null!, rowsHost = null!;
    private UiSourceScrollbar scroll = null!;
    private UiSourceBottomButton selectAll = null!;
    private readonly List<RowDisplay> pool = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly Dictionary<int, UiSourceColumnButton> headings = [];
    private readonly Dictionary<int, Dictionary<string, bool>> directions = [];
    private readonly Dictionary<int, (int Column, bool Ascending)> arrows = [];
    private DefinitionInformationPage? publication;
    private DefinitionInformationColumn[] columns = [];
    private int[] cellSlots = [];
    private int group = -1, retainedScroll;
    private bool abandoned, nextCheckedAscending = true;
    private string selectCaption = "", clearCaption = "";

    private AdministrationPageChrome.Resources? resources;

    internal void Configure(GameResourceRepository repository, PreviewText text, DeploymentDefinitionSelection model,
        Func<bool> admitted, Action<IReadOnlyList<int>?> completed, Func<int> scrollSpeed,
        AdministrationPageChrome.Resources? resources=null)
    {
        this.repository = repository; this.text = text; this.model = model; this.resources=resources;
        this.admitted = admitted; this.completed = completed; this.scrollSpeed = scrollSpeed;
        Name = model.Multiple ? "DeploymentTacticDefinitions" : "DeploymentFormationDefinitions";
        ZIndex = 5; MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var blur = new UiSourceCommonBlur(); AddChild(blur); blur.Configure(repository,resources?.Assets.SourceLayout(0xAEF1609E));
        if (model.Multiple) ConfigureMultiple();
        ShowGroup(0);
        SetMeta("deployment_definition_groups", model.Groups.Length);
        SetMeta("deployment_definition_mode", model.Multiple ? 2 : 1);
    }

    private void ConfigureMultiple()
    {
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, model.TitleMessageId, RequestCancel, () => Finish(model.SelectedDefinitionIds),resources:resources);
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191, 147)), chrome.Assets, chrome.SpriteShader);
        Vector2 mount = shell.Rectangle(4, new(191, 147)).Position;
        foreach (var descriptor in model.Groups)
        {
            int id = descriptor.Index; float width = descriptor.Caption.Length * 28 + 36;
            var tab = new UiSourceTabButton { Name = "DeploymentDefinitionTab" + id,
                Position = mount, Size = new(width, 64), InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, descriptor.Caption,
                model.Groups.Length == 1 ? 0x9AFB27B7u : id == 0 ? 0x172EDC24u : id == model.Groups.Length - 1 ? 0xD45873AAu : 0x9AFB27B7u);
            tab.Pressed += () => { if (ReadAdmission()) ShowGroup(id); };
            mount.X += width - 6;
        }
        headersHost = new Control { Name = "DeploymentDefinitionHeaders", Position = shell.Rectangle(14, new(191, 147)).Position,
            Size = new(1464, 48), MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(headersHost);
        rowsHost = new Control { Name = "DeploymentDefinitionRows", Position = shell.Rectangle(17, new(191, 147)).Position,
            Size = new(1464, 598), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(rowsHost);
        var box = shell.Rectangle(32, new(191, 147));
        scroll = new() { Name = "DeploymentDefinitionScrollbar", Position = box.Position, Size = box.Size };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, model.OrderedSourceIndices.Count * 48 - 598),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        rowsHost.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        selectAll = new() { Name = "DeploymentDefinitionsSelectAll", Position = footer.Rectangle(14, new(0, 904)).Position,
            InputAdmission = ReadAdmission };
        chrome.Canvas.AddChild(selectAll); selectCaption = Message(0x3A); clearCaption = Message(0x3B);
        selectAll.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, selectCaption,
            () => { if (ReadAdmission()) { model.SelectAllOrClear(); RefreshSelection(); } });
    }

    private void ShowGroup(int index)
    {
        if (abandoned || (uint)index >= (uint)model.Groups.Length || group == index) return;
        group = index;
        if (!directions.TryGetValue(index, out var next)) directions.Add(index, next = []);
        var descriptor = model.Groups[index]; columns = descriptor.Columns.Where(column => column.Visible).ToArray();
        var slots = descriptor.Columns.Select((column, slot) => (column.Id, slot)).ToDictionary(value => value.Id, value => value.slot);
        cellSlots = columns.Select(column => slots[column.Id]).ToArray(); publication = model.ReadPage(index);
        if (!model.Multiple) { ShowSingle(next); return; }
        foreach (var heading in headings.Values) { heading.InputAdmission = null; heading.Disabled = true; }
        foreach (var display in pool) { display.Button.InputAdmission = null; display.Button.Disabled = true; }
        headings.Clear(); pool.Clear(); Clear(headersHost); Clear(rowsHost);
        foreach (var tab in tabs) tab.Selected = tabs.IndexOf(tab) == index;
        foreach (var column in columns) next.TryAdd(Key(column.Id), column.InitialAscending);
        float factor = WidthFactor(columns); int left = 48;
        AddHeader(-1, Message(0x628), 0, 48);
        int[] widths = columns.Select(column => 20 + (int)((column.Width - 20) * factor)).ToArray();
        for (int slot = 0; slot < columns.Length; slot++)
        {
            int width = slot == columns.Length - 1 && factor < 1 ? 1465 - left : widths[slot];
            AddHeader(columns[slot].Id, columns[slot].Caption, left, width); left += width;
        }
        var rowColumns = columns.Select((column, slot) => new DeploymentPersonColumn(column.Id, widths[slot], column.CaptionMessageId,
            (byte)(((column.CellFormat - 3) << 4) | 1), (byte)(column.CellFormat - 2), column.InitialAscending)).ToArray();
        for (int slot = 0; slot < Math.Min(14, publication.Rows.Length); slot++)
        {
            var button = new UiSourcePersonRow { Name = "DeploymentDefinitionVisibleRow" + slot, InputAdmission = ReadAdmission };
            rowsHost.AddChild(button); button.Configure(repository, chrome!.Assets, chrome.SpriteShader, chrome.Font,
                Message(0x628), rowColumns, columns.Select(_ => new DeploymentPersonCell("")).ToArray(), 1464);
            var display = new RowDisplay(button); pool.Add(display);
            button.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
            button.Pressed += () => { if (ReadAdmission() && display.Entry is { } row && model.Toggle(row.SourceIndex)) RefreshSelection(); };
        }
        PositionRows(); RefreshSelection(); SetMeta("deployment_definition_active_group", index);
        if (publication.UnresolvedOwner is { } missing) SetMeta("deployment_definition_page_issue", missing);
        else if (HasMeta("deployment_definition_page_issue")) RemoveMeta("deployment_definition_page_issue");

        void AddHeader(int id, string caption, int x, int width)
        {
            var button = new UiSourceColumnButton { Name = "DeploymentDefinitionColumn" + id,
                Position = new(x, 0), Size = new(width, 48), InputAdmission = ReadAdmission };
            headersHost.AddChild(button); headings.Add(id, button);
            button.Configure(repository, chrome!.Assets, chrome.SpriteShader, chrome.Font, caption, personList: true);
            button.Pressed += () => Sort(id);
            if (arrows.TryGetValue(group, out var arrow)) button.ApplySortDirection(arrow.Column == id ? arrow.Ascending : null);
        }
    }

    private void ShowSingle(Dictionary<string, bool> next)
    {
        if (single is { } old) { retainedScroll = old.ScrollValue; old.Abandon(); RemoveChild(old); old.QueueFree(); }
        var view = new MapInformationListView(); single = view; AddChild(view);
        var descriptor = model.Groups[group]; int selectedGroup = group;
        view.Configure(repository, text, Message(model.TitleMessageId), columns.Select(column => new MapInformationListColumn(
            Key(column.Id), column.Caption, column.Width, column.CanSort, column.InitialAscending)).ToArray(), SingleRows(),
            () => ReadAdmission() && single == view, RequestCancel,
            sourceIndex => { if (ReadAdmission() && single == view && model.Toggle(sourceIndex)) Finish(model.SelectedDefinitionIds); }, scrollSpeed,
            (key, ascending) =>
            {
                int id = key == "name" ? 0 : int.Parse(key, CultureInfo.InvariantCulture);
                var sorted = model.Sort(selectedGroup, id, ascending);
                if (sorted.Value is not { } page) { SetMeta("deployment_definition_sort_issue", sorted.UnresolvedInput ?? ""); return null; }
                publication = page;
                foreach (var descriptor in model.Groups)
                    arrows[descriptor.Index] = descriptor.Index == selectedGroup ? (id, ascending) : (-2, false);
                return SingleRows();
            }, tabCaptions: model.Groups.Select(value => value.Caption).ToArray(), selectedTab: group,
            selectTab: id => { if (ReadAdmission()) ShowGroup(id); }, completeColumnProfile: true,
            initialSortColumn: arrows.TryGetValue(group, out var arrow) && arrow.Column >= 0 ? Key(arrow.Column) : null,
            initialSortAscending: arrows.GetValueOrDefault(group).Ascending,
            retainSortDirections: true, retainedSortDirections: next, rowExtentWidth: 1464, sourcePersonCellBounds: true,resources:resources);
        view.RestoreScroll(retainedScroll);
        var initial = publication!.Rows.FirstOrDefault(row => model.IsSelected(row.SourceIndex));
        if (initial is not null) view.SelectSourceIndex(initial.SourceIndex);
        SetMeta("deployment_definition_active_group", group);
    }

    private IReadOnlyList<MapInformationListRow> SingleRows() => publication!.Rows.Select(row =>
    {
        var cells = cellSlots.Select(slot => row.Cells[slot]).ToArray();
        return new MapInformationListRow(row.SourceIndex,
            cells.Select(cell => cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown")).ToArray(),
            model.IsEligible(row.SourceIndex), cells.Select((cell, slot) =>
                new MapInformationListCellStyle(columns[slot].CellFormat, cell.PresetIndex)).ToArray());
    }).ToArray();

    private void Sort(int column)
    {
        if (!ReadAdmission() || headings[column].Disabled) return;
        bool ascending = column == -1 ? nextCheckedAscending : directions[group][Key(column)];
        var sorted = model.Sort(group, column, ascending);
        if (sorted.Value is not { } page) { SetMeta("deployment_definition_sort_issue", sorted.UnresolvedInput ?? ""); return; }
        publication = page;
        if (column == -1) nextCheckedAscending = !ascending; else directions[group][Key(column)] = !ascending;
        foreach (var descriptor in model.Groups) arrows[descriptor.Index] = descriptor.Index == group || column == -1 ? (column, ascending) : (-2, false);
        foreach (var (id, button) in headings) button.ApplySortDirection(id == column ? ascending : null);
        PositionRows(); RefreshSelection();
    }

    private void PositionRows()
    {
        if (publication is null) return;
        int first = scroll.Value / 48;
        for (int slot = 0; slot < pool.Count; slot++)
        {
            var display = pool[slot]; int index = first + slot;
            if (index >= publication.Rows.Length) { display.Entry = null; display.Button.Hide(); continue; }
            var row = publication.Rows[index]; display.Entry = row;
            display.Button.Position = new(0, index * 48 - scroll.Value); display.Button.Show();
            display.Button.ApplyCells(cellSlots.Select(cellSlot =>
            {
                var value = row.Cells[cellSlot]; return new DeploymentPersonCell(value.UnresolvedOwner is null ? value.Text ?? "" : text.Get("detail_unknown"), SourcePreset: value.PresetIndex);
            }).ToArray());
        }
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (!model.Multiple || chrome is null) return;
        foreach (var display in pool) if (display.Entry is { } row)
        { display.Button.Selected = model.IsSelected(row.SourceIndex); display.Button.Disabled = !model.CanToggle(row.SourceIndex); }
        foreach (var (id, button) in headings)
            button.Disabled = id >= 0 && (!columns.Single(column => column.Id == id).CanSort ||
                publication!.Rows.Any(row => row.Cells[Array.FindIndex(model.Groups[group].Columns.ToArray(), column => column.Id == id)].SortUnresolvedOwner is not null));
        selectAll.Disabled = !model.OrderedSourceIndices.Any(model.IsEligible);
        selectAll.ApplyCaption(model.SelectedCount >= model.MaximumSelected ||
            model.OrderedSourceIndices.Where(model.IsEligible).All(model.IsSelected) ? clearCaption : selectCaption);
        chrome.ApplyReturn(true); chrome.ApplyDecision(model.SelectedCount >= model.MinimumSelected, true);
    }

    private static string Key(int id) => id == 0 ? "name" : id.ToString(CultureInfo.InvariantCulture);
    private static float WidthFactor(IReadOnlyList<DefinitionInformationColumn> columns) =>
        48 + columns.Sum(column => column.Width) < 1465 ? 1 : (1465 - (48 + 20 * columns.Count)) / (float)columns.Sum(column => column.Width - 20);
    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[id].Text
        ?? throw new InvalidDataException("Missing deployment definition caption.");
    private static void Clear(Control owner) { foreach (Node child in owner.GetChildren()) { owner.RemoveChild(child); child.QueueFree(); } }
    private bool ReadAdmission() => !abandoned && completed is not null && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void RequestCancel() => Finish(null);
    private void Finish(IReadOnlyList<int>? ids)
    {
        if (!ReadAdmission() || ids is not null && (ids.Count < model.MinimumSelected || ids.Count > model.MaximumSelected)) return;
        var callback = completed; completed = null; admitted = null; Hide(); callback?.Invoke(ids);
    }
    public override void _ExitTree() => Abandon();
    internal void Abandon()
    {
        if (abandoned) return; abandoned = true; completed = null; admitted = null;
        single?.Abandon(); chrome?.Abandon();
        foreach (var display in pool) { display.Button.InputAdmission = null; display.Button.Disabled = true; }
        foreach (var heading in headings.Values) { heading.InputAdmission = null; heading.Disabled = true; }
        foreach (var tab in tabs) { tab.InputAdmission = null; tab.Disabled = true; }
        if (selectAll is not null) { selectAll.InputAdmission = null; selectAll.Disabled = true; }
        Hide();
    }
}
