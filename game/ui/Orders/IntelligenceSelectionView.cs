using Godot;
using San14.Data;

internal sealed record IntelligenceSelectionColumn(int Id, string Caption, int Width, int CellFormat,
    bool CanSort, bool InitialAscending);
internal sealed record IntelligenceSelectionGroup(int Index, string Caption,
    IReadOnlyList<IntelligenceSelectionColumn> Columns);
internal sealed record IntelligenceSelectionCell(string Text, MapInformationListCellStyle Style,
    string? SortUnresolvedOwner);
internal sealed record IntelligenceSelectionRow(int Id, IReadOnlyList<IntelligenceSelectionCell> Cells,
    bool Enabled, InformationTextStatePresets? RowPresets = null, string? FocusHint = null,
    string? LeadingIcon=null,string? LeadingIconUnresolvedOwner=null);
internal sealed record IntelligenceSelectionTable(int TitleMessageId, string Guide,
    IReadOnlyList<IntelligenceSelectionGroup> Groups, int Count,
    Func<int, IReadOnlyList<IntelligenceSelectionRow>> ReadPage,
    Func<int, int, bool, bool> Sort,bool HasLeadingAreaIcon=false,bool ShowReturnButton=true);

/// <summary>The five authored intelligence selectors share one source list
/// shell. Their draft owns candidates, cells and ordering; this mode-one view
/// returns a single released row without a checked column or confirmation.</summary>
internal partial class IntelligenceSelectionView : Control
{
    internal sealed record RelationToggle(bool RelationshipMode, Action<bool> Changed);
    internal sealed record MapMode(int TitleMessageId, Action<bool> ModeChanged,
        Func<InputEventMouseButton, bool> CancelReleased, RelationToggle? Relations = null,bool InitialMap=false);
    private sealed class RowDisplay(UiSourceListRow button)
    {
        internal UiSourceListRow Button { get; } = button;
        internal List<UiSourceText> Labels { get; } = [];
        internal IntelligenceSelectionRow? Entry { get; set; }
    }

    private AdministrationPageChrome chrome = null!;
    private AdministrationPageChrome? mapChrome;
    private Control listRoot = null!;
    private MapMode? mapMode;
    private bool listMode = true;
    private IntelligenceSelectionTable table = null!;
    private Control headerHost = null!, body = null!;
    private UiSourceScrollbar scroll = null!;
    private readonly List<RowDisplay> pool = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly Dictionary<int, UiSourceColumnButton> headers = [];
    private readonly Dictionary<(int Group, int Column), bool> nextDirections = [];
    private readonly MapTextPresets presets = MapTextPresets.Create();
    private IReadOnlyList<IntelligenceSelectionRow> rows = [];
    private IReadOnlyDictionary<int,IntelligenceSelectionRow> candidates = new Dictionary<int,IntelligenceSelectionRow>();
    private IReadOnlyList<IntelligenceSelectionColumn> columns = [];
    private GameResourceRepository repository = null!;
    private Func<bool>? admitted;
    private Func<int, bool>? rowAdmission;
    private Action<int?>? closed;
    private int activeGroup = -1;
    private int? sortedGroup, sortedColumn, selectedId;
    private bool sortedAscending, abandoned, returnAllowed = true;
    private bool configured;
    internal Func<IReadOnlyList<int>, bool>? SelectionConfirmationAdmission { get; set; }
    internal IReadOnlyList<int> SelectedIds => selectedId is int id ? [id] : [];
    internal bool IsMapMode => configured && mapMode is not null && !listMode && !abandoned && closed is not null;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IntelligenceSelectionTable table, Func<bool> admitted, Action<int?> closed, Func<int> scrollSpeed,
        MapMode? mapMode = null)
    {
        configured = false; SetProcessInput(false);
        this.repository = repository; this.table = table;
        this.admitted = admitted; this.closed = closed; this.mapMode = mapMode;
        Name = "IntelligenceSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        listRoot = new Control { Name = "IntelligenceListPanel", MouseFilter = MouseFilterEnum.Stop };
        AddChild(listRoot); listRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); listRoot.AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); listRoot.AddChild(chrome);
        chrome.Configure(repository, text, table.TitleMessageId, RequestCancel, () => { }, returnOnly: true);
        chrome.ApplyReturnVisible(table.ShowReturnButton);
        chrome.ApplyDefaultHint(table.Guide);
        chrome.ApplyReturnHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x2733));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191,147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191,147)), chrome.Assets, chrome.SpriteShader);
        Vector2 mount = shell.Rectangle(4, new(191,147)).Position;
        foreach (var group in table.Groups)
        {
            int index = group.Index;
            float width = group.Caption.Length * 28 + 36;
            var tab = new UiSourceTabButton { Name = "IntelligenceTab" + index, Position = mount,
                Size = new(width,64), InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, group.Caption,
                index == 0 ? 0x172EDC24u : index == table.Groups.Count - 1 ? 0xD45873AAu : 0x9AFB27B7u);
            tab.Pressed += () => { if (ReadAdmission()) ShowGroup(index); };
            mount.X += width - 6;
        }
        headerHost = new() { Name = "IntelligenceColumns", Position = new(213,214), Size = new(1464,48),
            MouseFilter = MouseFilterEnum.Pass };
        body = new() { Name = "IntelligenceRows", Position = new(213,264), Size = new(1464,596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(headerHost); chrome.Canvas.AddChild(body);
        scroll = new() { Name = "IntelligenceScrollbar", Position = new(1687,265), Size = new(16,597) };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0,table.Count * 48 - 596),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        body.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
        for (int index = 0; index < Math.Min(14,table.Count); index++)
        {
            var button = new UiSourceListRow { Name = "IntelligenceVisibleRow" + index,
                InputAdmission = ReadAdmission, ActionMode = BaseButton.ActionModeEnum.Release };
            body.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "",table.HasLeadingAreaIcon?1416:1464,table.HasLeadingAreaIcon);
            var row = new RowDisplay(button); pool.Add(row);
            button.TextVisualStateChanged += _ => { ApplyCells(row); ApplyFocusHint(); };
            button.Pressed += () =>
            {
                if (ReadAdmission() && row.Entry is { } entry && RowAllowed(entry)) Finish(entry.Id);
            };
            button.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
        }
        ShowGroup(0);
        if (mapMode is not null)
        {
            mapChrome = new(); AddChild(mapChrome);
            mapChrome.Configure(repository, text, mapMode.TitleMessageId, RequestCancel, () => { }, returnOnly:true,
                resources:new(chrome.Assets,chrome.Font,chrome.SpriteShader));
            mapChrome.ApplyReturnVisible(table.ShowReturnButton);
            mapChrome.ApplyDefaultHint(table.Guide);
            mapChrome.ApplyReturnHint(new AdministrationMessages(repository,text.SourceLocale).Format(0x2733));
            var footer = new UiSourceLayout(repository,0xD8913AB2);
            var captions = repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
            ModeButton(chrome,"IntelligenceMap",0x5D,false);
            ModeButton(mapChrome,"IntelligenceList",0x61,true);
            if (mapMode.Relations is { } relations)
            {
                bool relationshipMode = relations.RelationshipMode;
                var toggle = new UiSourceBottomButton { Name="IntelligenceRelationsToggle",
                    Position=footer.Rectangle(17,new(0,904)).Position, InputAdmission=ReadAdmission };
                mapChrome.Canvas.AddChild(toggle);
                string Caption() => captions.Records[relationshipMode ? 0x8AA : 0x8AB].Text
                    ?? throw new InvalidDataException("Missing intelligence relation caption.");
                toggle.Configure(repository,mapChrome.Assets,mapChrome.SpriteShader,mapChrome.Font,Caption(),() =>
                {
                    if(!ReadAdmission() || !IsMapMode)return;
                    relationshipMode=!relationshipMode;
                    relations.Changed(relationshipMode); toggle.ApplyCaption(Caption());
                });
            }
            if(mapMode.InitialMap)
            {listMode=false;listRoot.Hide();mapChrome.Show();MouseFilter=MouseFilterEnum.Ignore;}
            else mapChrome.Hide();
            void ModeButton(AdministrationPageChrome owner,string name,int caption,bool list)
            {
                var button = new UiSourceBottomButton { Name=name, Position=footer.Rectangle(14,new(0,904)).Position,
                    InputAdmission=ReadAdmission };
                owner.Canvas.AddChild(button);
                button.Configure(repository,owner.Assets,owner.SpriteShader,owner.Font,
                    captions.Records[caption].Text ?? throw new InvalidDataException("Missing intelligence map caption."),
                    () => { if(ReadAdmission())SetListMode(list); });
            }
        }
        SetMeta("source_selection_mode",1);
        SetMeta("source_initial_selection_empty",true);
        SetMeta("intelligence_group_count",table.Groups.Count);
        configured = true; SetProcessInput(mapMode is not null);
    }

    private void ShowGroup(int index)
    {
        if (abandoned || index == activeGroup) return;
        activeGroup = index; columns = table.Groups[index].Columns; rows = table.ReadPage(index);
        if (candidates.Count == 0) candidates = rows.ToDictionary(row => row.Id);
        foreach (Node child in headerHost.GetChildren()) { headerHost.RemoveChild(child); child.QueueFree(); }
        headers.Clear(); int left=table.HasLeadingAreaIcon?48:0;
        if(table.HasLeadingAreaIcon)
        {
            var terrain=new UiSourceColumnButton{Name="SourceTerrainColumn",Position=Vector2.Zero,Size=new(48,48),
                InputAdmission=ReadAdmission,Disabled=rows.Any(row=>row.LeadingIconUnresolvedOwner is not null)};
            headerHost.AddChild(terrain);headers.Add(-1,terrain);
            terrain.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,"",
                ArmyMapUiSources.Create().IconName(new(0x1C3,0)),personList:true);
            nextDirections.TryAdd((index,-1),false);
            terrain.ApplySortDirection(sortedGroup==index&&sortedColumn==-1?sortedAscending:null);
            terrain.Pressed+=()=>Sort(-1);
        }
        for (int slot = 0; slot < columns.Count; slot++)
        {
            var column = columns[slot];
            int id = column.Id;
            var header = new UiSourceColumnButton { Name = "IntelligenceColumn" + id,
                Position = new(left,0), Size = new(column.Width,48), InputAdmission = ReadAdmission,
                Disabled = !column.CanSort || rows.Any(row => row.Cells[slot].SortUnresolvedOwner is not null) };
            headerHost.AddChild(header); headers.Add(id,header);
            header.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, column.Caption, personList:true);
            nextDirections.TryAdd((index,id),column.InitialAscending);
            header.ApplySortDirection(sortedGroup == index && sortedColumn == id ? sortedAscending : null);
            header.Pressed += () => Sort(id);
            left += column.Width;
        }
        foreach (var tab in tabs) tab.Selected = tab == tabs[index];
        PositionRows(); chrome.ApplyReturn(returnAllowed);
        SetMeta("intelligence_active_group",index);
    }

    private void Sort(int column)
    {
        if (!ReadAdmission() || headers[column].Disabled) return;
        bool ascending = nextDirections[(activeGroup,column)];
        if (!table.Sort(activeGroup,column,ascending)) return;
        nextDirections[(activeGroup,column)] = !ascending;
        sortedGroup = activeGroup; sortedColumn = column; sortedAscending = ascending;
        rows = table.ReadPage(activeGroup);
        foreach (var (id,header) in headers) header.ApplySortDirection(id == column ? ascending : null);
        PositionRows();
    }

    private void PositionRows()
    {
        int first = scroll.Value / 48;
        for (int slot = 0; slot < pool.Count; slot++)
        {
            var display = pool[slot]; int index = first + slot;
            if (index >= rows.Count) { display.Entry = null; display.Button.Hide(); continue; }
            display.Entry = rows[index]; display.Button.Position = new(0,index * 48 - scroll.Value);
            display.Button.Disabled = !RowAllowed(display.Entry); display.Button.Show(); ApplyCells(display);
        }
        ApplyFocusHint();
    }

    private void ApplyCells(RowDisplay display)
    {
        if (display.Entry is not { } row) return;
        foreach (var label in display.Labels) label.Hide();
        if(table.HasLeadingAreaIcon)
        {
            display.Button.ApplyContents("",row.LeadingIcon);
            if(row.LeadingIconUnresolvedOwner is {} missing)display.Button.SetMeta("leading_area_icon_unresolved",missing);
            else if(display.Button.HasMeta("leading_area_icon_unresolved"))display.Button.RemoveMeta("leading_area_icon_unresolved");
        }
        int state = display.Button.VisualState, left = table.HasLeadingAreaIcon?48:0;
        for (int index = 0; index < columns.Count; index++)
        {
            var column = columns[index]; var cell = row.Cells[index]; var style = cell.Style;
            if (index == display.Labels.Count)
            {
                var label = new UiSourceText { Name = "IntelligenceCell" + index };
                display.Button.AddChild(label); display.Labels.Add(label);
            }
            var text = display.Labels[index];
            text.Position = new(left + 10,10); text.Size = new(column.Width - 20,28); text.Show();
            var profile = new MapFontProfile("intelligence-selection/dynamic-cell",0,0,.875f,.875f,0,0,
                uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,0,(byte)(((column.CellFormat-3)<<4)|1),
                (byte)(column.CellFormat-2),0,0,0xFF000000,0xFF000000,0xFF000000,0xFF000000)
                { WidthFitMode = 2, WidthReserve = 0, StrokeColour = 0xFF000000, StrokeWidth = 3 };
            int preset = style.StatePresets?.ForState(state) ?? style.PresetIndex ?? row.RowPresets?.ForState(state) ?? 8 + Math.Min(state,3);
            text.Configure(chrome.Font,profile.WithPreset(presets[preset]));
            text.Text = cell.Text; text.FirstGlyphColour = style.FirstGlyphColour; text.QueueRedraw();
            left += column.Width;
        }
    }

    private void ApplyFocusHint()
    {
        if (abandoned) return;
        var hovered = pool.FirstOrDefault(row => row.Entry is not null && row.Button.VisualState is 1 or 4);
        chrome.ApplyDefaultHint(hovered?.Entry?.FocusHint ?? table.Guide);
    }
    private bool RowAllowed(IntelligenceSelectionRow row) => row.Enabled &&
        (rowAdmission?.Invoke(row.Id) ?? true) && (SelectionConfirmationAdmission?.Invoke([row.Id]) ?? true);
    private bool ReadAdmission() => configured && !abandoned && closed is not null &&
        IsInsideTree() && !IsQueuedForDeletion() && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void ApplySelectionAdmission(Func<int,bool>? allowed, bool canReturn)
    {
        if (abandoned) return;
        rowAdmission = allowed; returnAllowed = canReturn;
        PositionRows(); chrome.ApplyReturn(returnAllowed); mapChrome?.ApplyReturn(returnAllowed);
    }
    private void SetListMode(bool list)
    {
        if (mapMode is null || listMode == list) return;
        listMode = list; listRoot.Visible = list; mapChrome!.Visible = !list;
        MouseFilter = list ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        mapMode.ModeChanged(list);
    }
    internal bool CanChooseTarget(int id) => ReadAdmission() && candidates.TryGetValue(id,out var row) && RowAllowed(row);
    internal void PickMapTarget(int id) { if (IsMapMode && CanChooseTarget(id)) Finish(id); }
    public override void _Input(InputEvent input)
    {
        if (!IsMapMode || input is not InputEventMouseButton { ButtonIndex:MouseButton.Right } button ||
            !ReadAdmission() || mapMode is not { } mode) return;
        var viewport = GetViewport();
        if (button.Pressed)
        {
            _ = mode.CancelReleased(button);
            return; // The admitted map surface still owns orbit input.
        }
        // Cancellation may synchronously remove this view from its viewport.
        // Consume the release while attached, before either owner callback.
        viewport.SetInputAsHandled();
        if (mode.CancelReleased(button)) RequestCancel();
    }
    internal void RequestCancel() { if (returnAllowed) Finish(null); }
    private void Finish(int? id)
    {
        if (!ReadAdmission()) return;
        if (id is int chosen && !rows.Any(row => row.Id == chosen && RowAllowed(row))) return;
        selectedId = id;
        var callback = closed; configured = false; closed = null; admitted = null;
        SetProcessInput(false); Hide(); callback?.Invoke(id);
    }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; configured = false; SetProcessInput(false);
        closed = null; admitted = null; rowAdmission = null; SelectionConfirmationAdmission = null;
        foreach (var row in pool) { row.Button.InputAdmission = null; row.Button.Disabled = true; row.Entry = null; }
        foreach (var header in headers.Values) header.InputAdmission = null;
        foreach (var tab in tabs) tab.InputAdmission = null;
        mapMode = null; chrome?.Abandon(); mapChrome?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
