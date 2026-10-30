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
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, rows = null!;
    private UiSourceScrollbar scrollbar = null!;
    private readonly List<RowDisplay> rowPool = [];
    private readonly List<(string Key, UiSourceColumnButton Button)> columnButtons = [];
    private IReadOnlyList<MapInformationListRow> orderedRows = [];
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
    private Func<bool?>? readNameSort;
    private Action<bool?>? applyNameSort;
    internal bool? NameSortAscending => readNameSort?.Invoke();
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
        bool sourcePersonCellBounds = false)
    {
        this.admitted = admitted; this.returned = returned; this.inspect = inspect; orderedRows = publication;
        this.disableUninspectableRows=disableUninspectableRows;this.hoverSourceIndex=hoverSourceIndex;
        Name = "InformationList"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        if (includeHeader)
        {
            header = new(); canvas.AddChild(header); header.Configure(repository, assets, shader, font, title);
        }
        var source = new UiSourceLayout(repository, 0xA3B4A82E);
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
                canvas.AddChild(tab);
                tab.Configure(repository, assets, shader, font, tabCaptions[index],
                    tabCaptions.Count == 1 ? 0x9AFB27B7u : index == 0 ? 0x172EDC24u : 0xD45873AAu);
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
        float totalWidth = columns.Sum(column => column.Width);
        float rowWidth = rowExtentWidth ?? totalWidth;
        if (!float.IsFinite(rowWidth) || rowWidth < totalWidth)
            throw new ArgumentOutOfRangeException(nameof(rowExtentWidth));
        var viewport = new ScrollContainer { Name = "InformationColumns", Position = new(213, 172),
            Size = new(1464, 688), HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        if (tabCaptions is not null)
        {
            viewport.Position = source.Rectangle(14, new(191, 147)).Position;
            viewport.Size = new(1464, 648);
        }
        if (narrowTable) viewport.Size = new(totalWidth, viewport.Size.Y);
        canvas.AddChild(viewport);
        var table = new Control { Name = "InformationTable", CustomMinimumSize = new(rowWidth, 668),
            MouseFilter = MouseFilterEnum.Pass };
        viewport.AddChild(table);
        float left = 0; string? sortedColumn = initialSortColumn; bool ascending = initialSortAscending;
        var nextDirections = retainedSortDirections ?? columns.ToDictionary(column => column.Key, column => column.InitialAscending);
        foreach (var column in columns) nextDirections.TryAdd(column.Key, column.InitialAscending);
        readNameSort = () => sortedColumn == "name" ? ascending : null;
        applyNameSort = direction =>
        {
            if (direction is bool order)
            { sortedColumn = "name"; ascending = order; nextDirections["name"] = !order; }
            foreach (var entry in columnButtons)
                entry.Button.ApplySortDirection(entry.Key == sortedColumn ? ascending : null);
        };
        foreach (var column in columns)
        {
            var heading = new UiSourceColumnButton { Position = new(left, 0), Size = new(column.Width, 48),
                InputAdmission = ReadAdmission, Disabled = sort is null || !column.CanSort };
            table.AddChild(heading);
            heading.Configure(repository, assets, shader, font, column.Caption, personList: true,
                normalLayout: normalColumnLayout);
            columnButtons.Add((column.Key, heading));
            heading.ApplySortDirection(column.Key == sortedColumn ? ascending : null);
            heading.Pressed += () =>
            {
                if (!ReadAdmission() || sort is null || !column.CanSort) return;
                bool requested = retainSortDirections ? nextDirections[column.Key] :
                    sortedColumn == column.Key ? !ascending : column.InitialAscending;
                var sorted = sort(column.Key, requested);
                if (sorted is null) return;
                ascending = requested; nextDirections[column.Key] = !ascending; sortedColumn = column.Key;
                orderedRows = sorted; PositionRows();
                foreach (var entry in columnButtons)
                    entry.Button.ApplySortDirection(entry.Key == sortedColumn ? ascending : null);
            };
            left += column.Width;
        }
        rows = new() { Name = "InformationRows", Position = new(0, 64), Size = new(rowWidth, 596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        if (tabCaptions is not null)
        {
            rows.Position = source.Rectangle(17).Position - source.Rectangle(14).Position;
            rows.Size = new(rowWidth, 598);
        }
        table.AddChild(rows);
        scrollbar = new() { Name = "InformationScrollbar", Position = new(1687, 236), Size = new(16, 596) };
        if (tabCaptions is not null)
        {
            Rect2 box = source.Rectangle(32, new(191, 147));
            scrollbar.Position = box.Position; scrollbar.Size = box.Size;
        }
        if (narrowTable) scrollbar.Position = new(191 + totalWidth + 26, scrollbar.Position.Y);
        canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, Math.Max(0, publication.Count * 48 - (int)rows.Size.Y),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        rows.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        for (int slot = 0; slot < Math.Min(14, publication.Count); slot++)
        {
            var button = new UiSourceListRow { Name = "InformationVisibleRow" + slot,
                InputAdmission = ReadAdmission, ActionMode = BaseButton.ActionModeEnum.Release };
            rows.AddChild(button); button.Configure(repository, assets, shader, font, "", rowWidth, false);
            var cells = new List<UiSourceText>(); left = 0;
            foreach (var column in columns)
            {
                var cell = new UiSourceText { Position = new(left + (sourcePersonCellBounds?10:8), sourcePersonCellBounds?10:8),
                    Size = new(column.Width - (sourcePersonCellBounds?20:12), sourcePersonCellBounds?28:32),
                    MouseFilter = MouseFilterEnum.Ignore };
                button.AddChild(cell); cell.Configure(font, profile); cells.Add(cell); left += column.Width;
            }
            var display = new RowDisplay(button, cells.ToArray(), cells.Select(cell => new Rect2(cell.Position,cell.Size)).ToArray()); rowPool.Add(display);
            button.TextVisualStateChanged += _ => ApplyCells(display);
            button.MouseEntered+=()=>{if(ReadAdmission())this.hoverSourceIndex?.Invoke(display.Entry?.SourceIndex);};
            button.MouseExited+=()=>{if(ReadAdmission())this.hoverSourceIndex?.Invoke(null);};
            button.Pressed += () =>
            {
                if (!ReadAdmission() || display.Entry is not { } entry) return;
                selectedIndex = entry.SourceIndex;
                foreach (var row in rowPool) row.Button.Selected = row.Entry?.SourceIndex == selectedIndex;
                if (entry.CanInspect) this.inspect?.Invoke(entry.SourceIndex);
            };
            button.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        }
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

    internal int ScrollValue => scrollbar.Value;
    internal void RestoreScroll(int value) { scrollbar.SetValue(value); PositionRows(); }
    internal void RegisterHeader(UiSourceHeaderStack stack) => header?.Publish(stack);
    internal void ApplyDetailReturn(IReadOnlyList<MapInformationListRow> ordered, int selected, bool? nameSortAscending)
    {
        if (abandoned) return;
        orderedRows = ordered; selectedIndex = selected;
        applyNameSort?.Invoke(nameSortAscending);
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
        readNameSort = null; applyNameSort = null;hoverSourceIndex=null;
        abandoned = true; admitted = null; returned = null; inspect = null; orderedRows = [];
        foreach (var row in rowPool) { row.Button.InputAdmission = null; row.Button.Disabled = true; row.Entry = null; }
        foreach (var entry in columnButtons) { entry.Button.InputAdmission = null; entry.Button.Disabled = true; }
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
    public override void _ExitTree() { Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
