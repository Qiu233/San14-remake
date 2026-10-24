using Godot;
using San14.Data;

internal sealed record MapInformationListColumn(string Key, string Caption, int Width);
internal sealed record MapInformationListRow(int SourceIndex, IReadOnlyList<string> Cells, bool CanInspect);

/// <summary>A retained publication with fourteen reusable visible rows.
/// Sorting and scrolling never capture or scan a world.</summary>
internal partial class MapInformationListView : Control
{
    private sealed class RowDisplay(UiSourceListRow button, UiSourceText[] cells)
    {
        internal UiSourceListRow Button { get; } = button;
        internal UiSourceText[] Cells { get; } = cells;
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
    private int? selectedIndex;

    internal void Configure(GameResourceRepository repository, PreviewText text, string title,
        IReadOnlyList<MapInformationListColumn> columns, IReadOnlyList<MapInformationListRow> publication,
        Func<bool> admitted, Action returned, Action<int> inspect, Func<int> scrollSpeed,
        Func<string, bool, IReadOnlyList<MapInformationListRow>>? sort = null, string? notice = null,
        IReadOnlyList<string>? tabCaptions = null, int selectedTab = 0, Action<int>? selectTab = null,
        bool includeHeader = true)
    {
        this.admitted = admitted; this.returned = returned; this.inspect = inspect; orderedRows = publication;
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
                    index == 0 ? 0x172EDC24u : 0xD45873AAu);
                tab.Selected = index == selectedTab;
                tab.Pressed += () => { if (ReadAdmission()) selectTab?.Invoke(tabIndex); };
                tabPosition.X += width - 6;
            }
        }
        var help = new UiSourceLayout(repository, 0xAB6ADAA7);
        var textNode = help.Nodes.Single(node => node.Type == 2);
        var profile = MapFontProfile.FromNode(textNode, help.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[13]);
        float totalWidth = columns.Sum(column => column.Width);
        var viewport = new ScrollContainer { Name = "InformationColumns", Position = new(213, 172),
            Size = new(1464, 688), HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        if (tabCaptions is not null)
        {
            viewport.Position = source.Rectangle(14, new(191, 147)).Position;
            viewport.Size = new(1464, 648);
        }
        canvas.AddChild(viewport);
        var table = new Control { Name = "InformationTable", CustomMinimumSize = new(totalWidth, 668),
            MouseFilter = MouseFilterEnum.Pass };
        viewport.AddChild(table);
        float left = 0; string? sortedColumn = null; bool ascending = true;
        foreach (var column in columns)
        {
            var heading = new UiSourceColumnButton { Position = new(left, 0), Size = new(column.Width, 48),
                InputAdmission = ReadAdmission, Disabled = sort is null };
            table.AddChild(heading);
            heading.Configure(repository, assets, shader, font, column.Caption, personList: true);
            columnButtons.Add((column.Key, heading));
            heading.Pressed += () =>
            {
                if (!ReadAdmission() || sort is null) return;
                ascending = sortedColumn == column.Key ? !ascending : true; sortedColumn = column.Key;
                orderedRows = sort(column.Key, ascending); PositionRows();
                foreach (var entry in columnButtons)
                    entry.Button.ApplySortDirection(entry.Key == sortedColumn ? ascending : null);
            };
            left += column.Width;
        }
        rows = new() { Name = "InformationRows", Position = new(0, 64), Size = new(totalWidth, 596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        if (tabCaptions is not null)
        {
            rows.Position = source.Rectangle(17).Position - source.Rectangle(14).Position;
            rows.Size = new(totalWidth, 598);
        }
        table.AddChild(rows);
        scrollbar = new() { Name = "InformationScrollbar", Position = new(1687, 236), Size = new(16, 596) };
        if (tabCaptions is not null)
        {
            Rect2 box = source.Rectangle(32, new(191, 147));
            scrollbar.Position = box.Position; scrollbar.Size = box.Size;
        }
        canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, Math.Max(0, publication.Count * 48 - (int)rows.Size.Y),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        rows.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        for (int slot = 0; slot < Math.Min(14, publication.Count); slot++)
        {
            var button = new UiSourceListRow { Name = "InformationVisibleRow" + slot,
                InputAdmission = ReadAdmission, ActionMode = BaseButton.ActionModeEnum.Release };
            rows.AddChild(button); button.Configure(repository, assets, shader, font, "", totalWidth, false);
            var cells = new List<UiSourceText>(); left = 0;
            foreach (var column in columns)
            {
                var cell = new UiSourceText { Position = new(left + 8, 8), Size = new(column.Width - 12, 32),
                    MouseFilter = MouseFilterEnum.Ignore };
                button.AddChild(cell); cell.Configure(font, profile); cells.Add(cell); left += column.Width;
            }
            var display = new RowDisplay(button, cells.ToArray()); rowPool.Add(display);
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
            Position = footer.Rectangle(1, new(0, 904)).Position, InputAdmission = ReadAdmission };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestBack, UiAudioCue.Cancel);
        if (notice is not null)
        {
            Vector2 mount = footer.Rectangle(19, new(0, 904)).Position;
            foreach (var node in help.Nodes.Where(node => node.Type is 1 or 3))
                help.Art(canvas, node.Index, help.Rectangle(node.Index, mount), assets, shader);
            help.Text(canvas, textNode.Index, mount, notice, font);
        }
        SetMeta("original_complete_column_profile", false);
        Resized += Fit; Fit(); PositionRows();
    }

    internal void RegisterHeader(UiSourceHeaderStack stack) => header?.Publish(stack);
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
            row.Button.Selected = entry.SourceIndex == selectedIndex; row.Button.Show();
            for (int column = 0; column < row.Cells.Length; column++)
                row.Cells[column].Text = column < entry.Cells.Count ? entry.Cells[column] : "";
        }
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; inspect = null; orderedRows = [];
        foreach (var row in rowPool) { row.Button.InputAdmission = null; row.Button.Disabled = true; row.Entry = null; }
        foreach (var entry in columnButtons) { entry.Button.InputAdmission = null; entry.Button.Disabled = true; }
        Hide();
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree() { Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
