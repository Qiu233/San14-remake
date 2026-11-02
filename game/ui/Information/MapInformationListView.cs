using Godot;
using San14.Data;

internal sealed record MapInformationListColumn(string Key, string Caption, int Width, bool CanSort = true,
    bool InitialAscending = true);
internal sealed record MapInformationListCellStyle(int CellFormat, int? PresetIndex = null, uint? FirstGlyphColour = null, int? IconSpriteId = null,
    InformationTextStatePresets? StatePresets = null, string? SourceFormattedText = null);
internal sealed record MapInformationListRow(int SourceIndex, IReadOnlyList<string> Cells, bool CanInspect,
    IReadOnlyList<MapInformationListCellStyle>? Styles = null);

/// <summary>A retained publication with fourteen reusable visible rows.
/// Sorting and scrolling never capture or scan a world.</summary>
internal partial class MapInformationListView : Control
{
    private sealed class RowDisplay(UiSourceListRow button, UiSourceText[] cells, Rect2[] bounds)
    {
        internal UiSourceListRow Button { get; } = button;
        internal UiSourceText[] Cells { get; } = cells;
        internal Rect2[] Bounds { get; } = bounds;
        internal UiSourceSprite?[] Icons { get; } = new UiSourceSprite?[cells.Length];
        internal MapInformationListRow? Entry { get; set; }
    }
    private bool ownsResources;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private Control rows => activeTable.RowsHost;
    private UiSourceScrollbar scrollbar = null!;
    private List<RowDisplay> rowPool => activeTable.RowPool;
    private List<(string Key, UiSourceColumnButton Button)> columnButtons => activeTable.ColumnButtons;
    private IReadOnlyList<MapInformationListRow> orderedRows
    { get => activeTable.OrderedRows; set => activeTable.OrderedRows = value; }
    private TableState activeTable = null!;
    private readonly Dictionary<int, TableState> tables = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private GameResourceRepository repository = null!;
    private UiSourceLayout tableSource = null!;
    private MapFontProfile tableProfile = null!;
    private Func<int> scrollSpeed = null!;
    private TableGeometry tableGeometry = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<int>? inspect;
    private UiSourceHeader header = null!;
    private bool abandoned;
    private bool disableUninspectableRows;
    private Action<int?>? hoverSourceIndex;
    private UiSourceText? noticeText;
    private readonly ArmyMapUiSources iconNames = ArmyMapUiSources.Create();
    private MapFontProfile personCellProfile = null!;
    private readonly MapTextPresets personPresets = MapTextPresets.Create();
    private int? selectedIndex;
    internal bool? NameSortAscending => activeTable?.SortedColumn == "name" ? activeTable.Ascending : null;
    internal Control SourceCanvas => canvas;
    internal UiAssets SourceAssets => assets;
    internal MapFont SourceFont => font;
    internal Shader SourceShader => shader;

    internal void Configure(GameResourceRepository repository, PreviewText text, string title,
        IReadOnlyList<MapInformationListColumn> columns, IReadOnlyList<MapInformationListRow> publication,
        Func<bool> admitted, Action returned, Action<int> inspect, Func<int> scrollSpeed,
        Func<string, bool, IReadOnlyList<MapInformationListRow>?>? sort = null, string? notice = null,
        IReadOnlyList<string>? tabCaptions = null, int selectedTab = 0, Action<int>? selectTab = null,
        bool includeHeader = true, bool completeColumnProfile = false, bool normalColumnLayout = false,
        bool narrowTable = false, int returnNode = 1, string? initialSortColumn = null, bool initialSortAscending = true,
        bool retainSortDirections = false, IDictionary<string, bool>? retainedSortDirections = null,
        bool disableUninspectableRows = false, Action<int?>? hoverSourceIndex = null, float? rowExtentWidth = null,
        bool sourcePersonCellBounds = false, AdministrationPageChrome.Resources? resources = null)
    {
        this.admitted = admitted; this.returned = returned; this.inspect = inspect;
        this.repository=repository;this.scrollSpeed=scrollSpeed;
        this.disableUninspectableRows=disableUninspectableRows;this.hoverSourceIndex=hoverSourceIndex;
        Name = "InformationList"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ownsResources = resources is null;
        if (resources is null)
        { assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader(); }
        else
        { assets = resources.Assets; font = resources.Font; shader = resources.SpriteShader; }
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        if (includeHeader)
        {
            header = new(); canvas.AddChild(header); header.Configure(repository, assets, shader, font, title);
        }
        var source = new UiSourceLayout(repository, 0xA3B4A82E); tableSource=source;
        source.Blur(canvas, 2, source.Rectangle(2, new(191, 147)));
        source.Art(canvas, 3, source.Rectangle(3, new(191, 147)), assets, shader);
        if (tabCaptions is not null)
        {
            Vector2 tabPosition = source.Rectangle(4, new(191, 147)).Position;
            for (int index = 0; index < tabCaptions.Count; index++)
            {
                int tabIndex = index;
                float width = tabCaptions[index].Length * 28 + 36;
                var tab = new UiSourceTabButton { Name = "InformationSection" + index,
                    Position = tabPosition, Size = new(width, 64), InputAdmission = ReadAdmission };
                canvas.AddChild(tab); tabs.Add(tab);
                tab.Configure(repository, assets, shader, font, tabCaptions[index],
                    tabCaptions.Count == 1 ? 0x9AFB27B7u : index == 0 ? 0x172EDC24u
                        : index == tabCaptions.Count - 1 ? 0xD45873AAu : 0x9AFB27B7u);
                tab.Selected = index == selectedTab;
                tab.Pressed += () => { if (ReadAdmission()) selectTab?.Invoke(tabIndex); };
                tabPosition.X += width - 6;
            }
        }
        var help = new UiSourceLayout(repository, 0xAB6ADAA7);
        var textNode = help.Nodes.Single(node => node.Type == 2);
        var profile = MapFontProfile.FromNode(textNode, help.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[13]);
        personCellProfile = new MapFontProfile("ordinary-person/dynamic-cell", 0, 0,
            .875f, .875f, 0, 0, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue,
            0, 0x01, 1, 0, 0, 0xFF000000, 0xFF000000, 0xFF000000, 0xFF000000)
            { WidthFitMode = 2, WidthReserve = 0, StrokeColour = 0xFF000000, StrokeWidth = 3 };
        tableProfile=profile;
        tableGeometry=new(tabCaptions is not null,normalColumnLayout,narrowTable,rowExtentWidth,
            sourcePersonCellBounds,retainSortDirections);
        activeTable=CreateTable(columns,publication,sort,initialSortColumn,initialSortAscending,retainedSortDirections);
        tables.Add(selectedTab,activeTable);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "InformationListReturn",
            Position = footer.Rectangle(returnNode, new(0, 904)).Position, InputAdmission = ReadAdmission };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestBack, UiAudioCue.Cancel);
        if (notice is not null)
        {
            Vector2 mount = footer.Rectangle(19, new(0, 904)).Position;
            foreach (var node in help.Nodes.Where(node => node.Type is 1 or 3))
                help.Art(canvas, node.Index, help.Rectangle(node.Index, mount), assets, shader);
            noticeText=help.Text(canvas, textNode.Index, mount, notice, font);
        }
        SetMeta("original_complete_column_profile", completeColumnProfile);
        Resized += Fit; Fit(); PositionRows();
    }

    private sealed record TableGeometry(bool Tabbed,bool NormalColumnLayout,bool NarrowTable,
        float? RowExtentWidth,bool SourcePersonCellBounds,bool RetainSortDirections);
    private sealed class TableState
    {
        internal Control RowsHost = null!;
        internal ScrollContainer Viewport = null!;
        internal readonly List<RowDisplay> RowPool = [];
        internal readonly List<(string Key,UiSourceColumnButton Button)> ColumnButtons = [];
        internal IReadOnlyList<MapInformationListColumn> Columns = [];
        internal IReadOnlyList<MapInformationListRow> OrderedRows = [];
        internal Func<string,bool,IReadOnlyList<MapInformationListRow>?>? Sort;
        internal string? SortedColumn;
        internal bool Ascending;
        internal IDictionary<string,bool> Directions = null!;
    }

    // One immutable-world list owns its shell and resources. Each authored
    // column schema is created on first use; later tab visits rebind current
    // caller order without rebuilding headers, footer, blur or row artwork.
    internal void PresentGroup(int index,IReadOnlyList<MapInformationListColumn> columns,
        IReadOnlyList<MapInformationListRow> publication,
        Func<string,bool,IReadOnlyList<MapInformationListRow>?>? sort,
        string? sortedColumn,bool ascending,IDictionary<string,bool> directions)
    {
        if(abandoned)return;
        int scroll=scrollbar.Value;
        activeTable.Viewport.Hide();
        if(!tables.TryGetValue(index,out var next))
            tables.Add(index,next=CreateTable(columns,publication,sort,sortedColumn,ascending,directions));
        activeTable=next;next.OrderedRows=publication;next.Sort=sort;
        next.SortedColumn=sortedColumn;next.Ascending=ascending;
        foreach(var (key,button) in next.ColumnButtons)
            button.ApplySortDirection(key==sortedColumn?ascending:null);
        for(int tab=0;tab<tabs.Count;tab++)tabs[tab].Selected=tab==index;
        next.Viewport.ScrollHorizontal=0;
        next.Viewport.Show();
        scrollbar.SetValue(scroll);PositionRows();
    }
    private bool ReadTableAdmission(TableState table)=>ReferenceEquals(activeTable,table)&&ReadAdmission();

    private TableState CreateTable(IReadOnlyList<MapInformationListColumn> columns,
        IReadOnlyList<MapInformationListRow> publication,
        Func<string,bool,IReadOnlyList<MapInformationListRow>?>? sort,
        string? sortedColumn,bool ascending,IDictionary<string,bool>? directions)
    {
        var state=new TableState {Columns=columns,OrderedRows=publication,Sort=sort,
            SortedColumn=sortedColumn,Ascending=ascending,
            Directions=directions??columns.ToDictionary(column=>column.Key,column=>column.InitialAscending)};
        var source=tableSource;
        float totalWidth = columns.Sum(column => column.Width);
        float rowWidth = tableGeometry.RowExtentWidth ?? totalWidth;
        if (!float.IsFinite(rowWidth) || rowWidth < totalWidth)
            throw new ArgumentOutOfRangeException(nameof(tableGeometry.RowExtentWidth));
        var viewport = new ScrollContainer { Name = "InformationColumns", Position = new(213, 172),
            Size = new(1464, 688), HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        if (tableGeometry.Tabbed)
        {
            viewport.Position = source.Rectangle(14, new(191, 147)).Position;
            viewport.Size = new(1464, 648);
        }
        if (tableGeometry.NarrowTable) viewport.Size = new(totalWidth, viewport.Size.Y);
        canvas.AddChild(viewport); state.Viewport=viewport;
        var table = new Control { Name = "InformationTable", CustomMinimumSize = new(rowWidth, 668),
            MouseFilter = MouseFilterEnum.Pass };
        viewport.AddChild(table);
        float left = 0;
        foreach (var column in columns) state.Directions.TryAdd(column.Key,column.InitialAscending);
        foreach (var column in columns)
        {
            var heading = new UiSourceColumnButton { Position = new(left, 0), Size = new(column.Width, 48),
                InputAdmission = () => ReadTableAdmission(state), Disabled = state.Sort is null || !column.CanSort };
            table.AddChild(heading);
            heading.Configure(repository, assets, shader, font, column.Caption, personList: true,
                normalLayout: tableGeometry.NormalColumnLayout);
            state.ColumnButtons.Add((column.Key, heading));
            heading.ApplySortDirection(column.Key == state.SortedColumn ? state.Ascending : null);
            heading.Pressed += () =>
            {
                if (!ReadTableAdmission(state) || state.Sort is null || !column.CanSort) return;
                bool requested = tableGeometry.RetainSortDirections ? state.Directions[column.Key] :
                    state.SortedColumn == column.Key ? !state.Ascending : column.InitialAscending;
                var sorted = state.Sort(column.Key, requested);
                if (sorted is null) return;
                state.Ascending = requested; state.Directions[column.Key] = !requested; state.SortedColumn = column.Key;
                state.OrderedRows = sorted; PositionRows();
                foreach (var entry in state.ColumnButtons)
                    entry.Button.ApplySortDirection(entry.Key == state.SortedColumn ? state.Ascending : null);
            };
            left += column.Width;
        }
        var rows = new Control { Name = "InformationRows", Position = new(0, 64), Size = new(rowWidth, 596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        if (tableGeometry.Tabbed)
        {
            rows.Position = source.Rectangle(17).Position - source.Rectangle(14).Position;
            rows.Size = new(rowWidth, 598);
        }
        table.AddChild(rows); state.RowsHost=rows;
        if(scrollbar is null)
        {
            scrollbar = new UiSourceScrollbar { Name = "InformationScrollbar", Position = new(1687, 236), Size = new(16, 596) };
            if (tableGeometry.Tabbed)
            {
                Rect2 box = source.Rectangle(32, new(191, 147));
                scrollbar.Position = box.Position; scrollbar.Size = box.Size;
            }
            if (tableGeometry.NarrowTable) scrollbar.Position = new(191 + totalWidth + 26, scrollbar.Position.Y);
            canvas.AddChild(scrollbar);
            scrollbar.Configure(repository, assets, shader, Math.Max(0, publication.Count * 48 - (int)rows.Size.Y),
                scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        }
        rows.GuiInput += input => { if (ReadTableAdmission(state)) scrollbar.HandleWheel(input); };
        for (int slot = 0; slot < Math.Min(14, publication.Count); slot++)
        {
            var button = new UiSourceListRow { Name = "InformationVisibleRow" + slot,
                InputAdmission = () => ReadTableAdmission(state), ActionMode = BaseButton.ActionModeEnum.Release };
            rows.AddChild(button); button.Configure(repository, assets, shader, font, "", rowWidth, false);
            var cells = new List<UiSourceText>(); left = 0;
            foreach (var column in columns)
            {
                var cell = new UiSourceText { Position = new(left + (tableGeometry.SourcePersonCellBounds?10:8), tableGeometry.SourcePersonCellBounds?10:8),
                    Size = new(column.Width - (tableGeometry.SourcePersonCellBounds?20:12), tableGeometry.SourcePersonCellBounds?28:32),
                    MouseFilter = MouseFilterEnum.Ignore };
                button.AddChild(cell); cell.Configure(font, tableProfile); cells.Add(cell); left += column.Width;
            }
            var display = new RowDisplay(button, cells.ToArray(), cells.Select(cell => new Rect2(cell.Position,cell.Size)).ToArray()); state.RowPool.Add(display);
            button.TextVisualStateChanged += _ => ApplyCells(display);
            button.MouseEntered+=()=>{if(ReadTableAdmission(state))this.hoverSourceIndex?.Invoke(display.Entry?.SourceIndex);};
            button.MouseExited+=()=>{if(ReadTableAdmission(state))this.hoverSourceIndex?.Invoke(null);};
            button.Pressed += () =>
            {
                if (!ReadTableAdmission(state) || display.Entry is not { } entry) return;
                selectedIndex = entry.SourceIndex;
                foreach (var row in state.RowPool) row.Button.Selected = row.Entry?.SourceIndex == selectedIndex;
                if (entry.CanInspect) this.inspect?.Invoke(entry.SourceIndex);
            };
            button.GuiInput += input => { if (ReadTableAdmission(state)) scrollbar.HandleWheel(input); };
        }
        return state;
    }
    private void ApplyNameSort(bool? direction)
    {
        if(direction is bool order)
        { activeTable.SortedColumn="name";activeTable.Ascending=order;activeTable.Directions["name"]=!order; }
        foreach(var entry in columnButtons)
            entry.Button.ApplySortDirection(entry.Key==activeTable.SortedColumn?activeTable.Ascending:null);
    }

    internal int ScrollValue => scrollbar.Value;
    internal void RestoreScroll(int value) { scrollbar.SetValue(value); PositionRows(); }
    internal void RegisterHeader(UiSourceHeaderStack stack) => header?.Publish(stack);
    internal void ApplyDetailReturn(IReadOnlyList<MapInformationListRow> ordered, int selected, bool? nameSortAscending)
    {
        if (abandoned) return;
        orderedRows = ordered; selectedIndex = selected;
        ApplyNameSort(nameSortAscending);
        PositionRows();
    }
    internal void SelectSourceIndex(int index)
    {
        selectedIndex = index;
        for (int row = 0; row < orderedRows.Count; row++)
        {
            if (orderedRows[row].SourceIndex != index) continue;
            int top = row * 48;
            if (top < scrollbar.Value) scrollbar.SetValue(top);
            else if (top + 48 > scrollbar.Value + rows.Size.Y)
                scrollbar.SetValue(top + 48 - (int)rows.Size.Y);
            break;
        }
        PositionRows();
    }
    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    private void PositionRows()
    {
        int first = scrollbar.Value / 48;
        for (int slot = 0; slot < rowPool.Count; slot++)
        {
            var row = rowPool[slot]; int index = first + slot;
            if (index >= orderedRows.Count) { row.Entry = null; row.Button.Hide(); continue; }
            var entry = orderedRows[index]; row.Entry = entry;
            row.Button.Position = new(0, index * 48 - scrollbar.Value);
            row.Button.Disabled=disableUninspectableRows&&!entry.CanInspect;
            row.Button.Selected = entry.SourceIndex == selectedIndex; row.Button.Show();
            ApplyCells(row);
        }
        //A retained hovered control can now represent a different row after
        //scrolling. Publish that occurrence without another world query.
        if(hoverSourceIndex is not null && ReadAdmission())
        {
            var hovered=rowPool.FirstOrDefault(row=>row.Entry is not null && row.Button.VisualState is 1 or 4);
            if(hovered?.Entry is {} entry)hoverSourceIndex(entry.SourceIndex);
        }
    }
    private void ApplyCells(RowDisplay row)
    {
        if (row.Entry is not { } entry) return;
        for (int column = 0; column < row.Cells.Length; column++)
        {
            var cell = row.Cells[column];
            cell.Position=row.Bounds[column].Position;cell.Size=row.Bounds[column].Size;
            row.Icons[column]?.Hide();
            var formatted=entry.Styles is { } allStyles && column<allStyles.Count
                ? allStyles[column].SourceFormattedText : null;
            if(formatted is null)cell.Text = column < entry.Cells.Count ? entry.Cells[column] : "";
            if (entry.Styles is { } styles && column < styles.Count)
            {
                var style = styles[column];
                if (style.CellFormat is < 3 or > 5) throw new InvalidDataException("Unsupported Person cell format.");
                var profile = personCellProfile with { RawAlign = (byte)(((style.CellFormat - 3) << 4) | 1),
                    RawLineArrange = (byte)(style.CellFormat - 2) };
                var activeProfile=profile.WithPreset(personPresets[style.StatePresets?.ForState(row.Button.VisualState) ?? style.PresetIndex ?? 8 + Math.Min(row.Button.VisualState, 3)]);
                cell.Configure(font, activeProfile);
                if (style.IconSpriteId is int id)
                {
                    string name=iconNames.IconName(new(id,0));
                    var original=assets.Sprite(name).Texture.Region.Size;
                    var icon=row.Icons[column];
                    if (icon is null)
                    {
                        icon=new UiSourceSprite { Name="InformationInlineIcon"+column };
                        row.Button.AddChild(icon);row.Icons[column]=icon;
                        icon.Configure(shader,assets.SrgbSprite(name),UiSourceSprite.Blend.Ordinary);
                    }
                    else icon.SetSource(assets.SrgbSprite(name));
                    icon.SetMeta("source_sprite_id",id);icon.SetMeta("source_sprite",name);
                    UiSourceInlineIconLayout.Apply(cell,icon,row.Bounds[column],font,activeProfile,original);
                }
                if(formatted is not null)cell.SetOfficePreviewLine(formatted);
                cell.FirstGlyphColour = style.FirstGlyphColour;
                cell.QueueRedraw();
            }
        }
    }
    internal void Abandon()
    {
        noticeText=null;
        hoverSourceIndex=null;
        abandoned = true; admitted = null; returned = null; inspect = null;
        foreach(var table in tables.Values)
        {
            table.OrderedRows=[];table.Sort=null;
            foreach(var row in table.RowPool){row.Button.InputAdmission=null;row.Button.Disabled=true;row.Entry=null;}
            foreach(var entry in table.ColumnButtons){entry.Button.InputAdmission=null;entry.Button.Disabled=true;}
        }
        scrollbar?.SetProcessInput(false);
        foreach(var tab in tabs)tab.InputAdmission=null;
        Hide();
    }
    internal void ApplyNotice(string text)
    {
        if(!abandoned && noticeText is {} line)line.Text=text;
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        Abandon();
        if (ownsResources) { font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
    }
}
