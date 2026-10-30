using Godot;
using San14.Data;

/// <summary>The two actual diplomacy actor tabs. Mode1 has no checked front:
/// one admitted row release returns immediately, and only Return is mounted.</summary>
internal partial class DiplomacyExecutorSelectionView : Control
{
    private sealed class Row(UiSourceListRow button)
    {
        internal UiSourceListRow Button { get; } = button;
        internal Dictionary<int, UiSourceText> Labels { get; } = [];
        internal OfficerInformationRow? Entry { get; set; }
    }
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private DiplomacyExecutorSelection model = null!;
    private OfficerInformationPageSet pages = null!;
    private OfficerInformationPage publication = null!;
    private OfficerInformationColumn[] columns = [];
    private Dictionary<int, int> slots = [];
    private Control headersRoot = null!, body = null!;
    private UiSourceScrollbar scrollbar = null!;
    private readonly List<Row> pool = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly Dictionary<int, UiSourceColumnButton> headers = [];
    private readonly Dictionary<(int Group, int Column), bool> nextDirections = [];
    private readonly MapTextPresets presets = MapTextPresets.Create();
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private Func<int, bool>? rowAdmission;
    private bool abandoned, returnAllowed = true, configured;
    private int activePage;
    private int? sortedGroup, sortedColumn;
    private bool sortedAscending;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        AdministrationPageChrome.Resources resources, DiplomacyExecutorSelection model,
        Func<bool> admitted, Action<int?> closed, Func<int> scrollSpeed)
    {
        this.repository = repository; this.text = text; this.model = model; pages = model.Pages;
        this.admitted = admitted; this.closed = closed;
        Name = "DiplomacyExecutorSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x58F, RequestCancel, () => { }, returnOnly: true, resources: resources);
        var hints = new AdministrationMessages(repository, text.SourceLocale);
        chrome.ApplyDefaultHint(hints.Format(0x59A3)); chrome.ApplyReturnHint(hints.Format(0x2733));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191,147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191,147)), chrome.Assets, chrome.SpriteShader);
        Vector2 mount = shell.Rectangle(4, new(191,147)).Position;
        foreach (var group in pages.Groups)
        {
            int id = group.Index; float width = group.Caption.Length * 28 + 36;
            var tab = new UiSourceTabButton { Name = "DiplomacyExecutorTab"+id, Position = mount,
                Size = new(width,64), InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, group.Caption,
                id == 0 ? 0x172EDC24u : 0xD45873AAu);
            tab.Pressed += () => { if (ReadAdmission()) SwitchPage(id); };
            mount.X += width - 6;
        }
        headersRoot = new() { Name = "DiplomacyExecutorHeaders", Position = new(213,214), Size = new(1464,48), MouseFilter = MouseFilterEnum.Pass };
        body = new() { Name = "DiplomacyExecutorsViewport", Position = new(213,264), Size = new(1464,596), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(headersRoot); chrome.Canvas.AddChild(body);
        scrollbar = new() { Name = "DiplomacyExecutorScrollbar", Position = new(1687,265), Size = new(16,597) };
        chrome.Canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0,pages.CurrentOfficerIds.Length*48-596),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        body.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        for (int index = 0; index < Math.Min(14,pages.CurrentOfficerIds.Length); index++)
        {
            var button = new UiSourceListRow { Name = "DiplomacyExecutorVisibleRow"+index, InputAdmission = ReadAdmission,
                ActionMode = BaseButton.ActionModeEnum.Release };
            body.AddChild(button); button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "",1464,false);
            var row = new Row(button); pool.Add(row);
            button.TextVisualStateChanged += _ => ApplyCells(row);
            button.Pressed += () =>
            { if (ReadAdmission() && row.Entry is {} entry && RowAllowed(entry.OfficerId)) Finish(entry.OfficerId); };
            button.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        }
        configured = true; SwitchPage(0);
        SetMeta("source_selection_mode",1); SetMeta("source_initial_selection_empty",true);
        SetMeta("original_diplomacy_executor_columns_recovered",true);
        if(model.InitialOrderUnresolvedInput is {} missing)SetMeta("source_initial_order_issue",missing);
    }

    private void SwitchPage(int index)
    {
        if (!configured || !ReadAdmission()) return;
        activePage = index; publication = pages.ReadPage(index);
        SetMeta("diplomacy_executor_page_issue",publication.UnresolvedOwner ?? "");
        var descriptor = pages.Groups[index];
        columns = descriptor.Columns.Where(column => column.Visible).ToArray();
        slots = descriptor.Columns.Select((column,slot) => (column.Id,slot)).ToDictionary(pair => pair.Id,pair => pair.slot);
        foreach(Node child in headersRoot.GetChildren()) { headersRoot.RemoveChild(child); child.QueueFree(); }
        headers.Clear(); int left = 0;
        foreach (var column in columns)
        {
            int id = column.Id;
            var header = new UiSourceColumnButton { Name = "DiplomacyExecutorColumn"+id, Position = new(left,0),
                Size = new(column.Width,48), InputAdmission = ReadAdmission,
                Disabled = !column.CanSort || publication.Rows.Any(row => row.Cells[slots[id]].SortUnresolvedOwner is not null) };
            headersRoot.AddChild(header); headers.Add(id,header);
            header.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, column.Caption, personList:true);
            nextDirections.TryAdd((index,id),column.InitialAscending);
            header.ApplySortDirection(sortedGroup == index && sortedColumn == id ? sortedAscending : null);
            header.Pressed += () => Sort(id); left += column.Width;
        }
        foreach(var tab in tabs)tab.Selected = tab == tabs[index];
        PositionRows(); chrome.ApplyReturn(returnAllowed);
        SetMeta("diplomacy_executor_active_page",index);
    }

    private void Sort(int column)
    {
        if (!ReadAdmission() || headers[column].Disabled) return;
        bool ascending = nextDirections[(activePage,column)];
        var result = pages.Sort(activePage,column,ascending);
        if (!result.Available || result.Value is not {} sorted) return;
        publication = sorted; nextDirections[(activePage,column)] = !ascending;
        sortedGroup = activePage; sortedColumn = column; sortedAscending = ascending;
        foreach(var (id,header) in headers)header.ApplySortDirection(id == column ? ascending : null);
        PositionRows();
    }

    private void PositionRows()
    {
        if (publication is null) return;
        int first = scrollbar.Value/48;
        for (int slot = 0; slot < pool.Count; slot++)
        {
            var row = pool[slot]; int index = first+slot;
            if(index >= publication.Rows.Length) { row.Entry = null; row.Button.Hide(); continue; }
            row.Entry = publication.Rows[index]; row.Button.Position = new(0,index*48-scrollbar.Value); row.Button.Show();
            row.Button.Disabled = !RowAllowed(row.Entry.OfficerId); ApplyCells(row);
        }
    }

    private void ApplyCells(Row row)
    {
        if (row.Entry is not {} entry) return;
        foreach(var label in row.Labels.Values)label.Hide();
        var health = model.AlteredHealth[entry.OfficerId]; int state = row.Button.VisualState;
        int ordinary = health.Available && health.Value && state < 3 ? state == 2 ? 68 : 67 : 8+Math.Min(state,3);
        int left = 0;
        foreach(var column in columns)
        {
            var cell = entry.Cells[slots[column.Id]];
            if(!row.Labels.TryGetValue(column.Id,out var label))
            {
                label = new UiSourceText { Name = "DiplomacyExecutorCell"+column.Id };
                row.Button.AddChild(label); row.Labels.Add(column.Id,label);
            }
            label.Position = new(left+10,10); label.Size = new(column.Width-20,28); label.Show();
            var profile = new MapFontProfile("diplomacy-executor/dynamic-cell",0,0,.875f,.875f,0,0,
                uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,0,(byte)(((column.CellFormat-3)<<4)|1),
                (byte)(column.CellFormat-2),0,0,0xFF000000,0xFF000000,0xFF000000,0xFF000000)
                { WidthFitMode=2,WidthReserve=0,StrokeColour=0xFF000000,StrokeWidth=3 };
            label.Configure(chrome.Font,profile.WithPreset(presets[cell.StatePresets?.ForState(state) ?? cell.PresetIndex ?? ordinary]));
            label.Text = cell.UnresolvedOwner is null ? cell.Text ?? "" : text.Get("detail_unknown");
            label.FirstGlyphColour = cell.MarkerColour?.CpuPacked; label.QueueRedraw(); left += column.Width;
        }
    }

    private bool RowAllowed(int id) => rowAdmission?.Invoke(id) ?? true;
    private bool ReadAdmission() => !abandoned && closed is not null && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void ApplySelectionAdmission(Func<int,bool>? allowed, bool canReturn)
    {
        if(abandoned)return; rowAdmission=allowed; returnAllowed=canReturn;
        if(configured) { PositionRows(); chrome.ApplyReturn(returnAllowed); }
    }
    internal void RequestCancel() { if(returnAllowed)Finish(null); }
    private void Finish(int? result)
    {
        if(!ReadAdmission() || result is int id && !RowAllowed(id))return;
        var callback=closed; closed=null; admitted=null; Hide(); callback?.Invoke(result);
    }
    internal void Abandon()
    {
        if(abandoned)return; abandoned=true; closed=null; admitted=null; rowAdmission=null;
        foreach(var row in pool) { row.Button.InputAdmission=null; row.Entry=null; row.Button.Disabled=true; }
        foreach(var header in headers.Values)header.InputAdmission=null;
        foreach(var tab in tabs)tab.InputAdmission=null;
        scrollbar?.SetProcessInput(false);
        chrome?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
