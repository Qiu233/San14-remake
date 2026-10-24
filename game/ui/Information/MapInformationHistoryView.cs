using Godot;
using San14.Data;

/// <summary>The source-time event log. Fifteen visible rows are reused while
/// scrolling the retained history publication; no world is queried on input.</summary>
internal partial class MapInformationHistoryView : DetailsView
{
    private sealed record RowDisplay(Control Root, Control Flag, UiSourceText FlagText,
        UiSourceText Year, UiSourceText Month, UiSourceText Period, UiSourceText Body)
    {
        internal int BoundIndex { get; set; } = -1;
    }
    private readonly List<RowDisplay> pool = [];
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, rows = null!;
    private UiSourceScrollbar scrollbar = null!;
    private InformationHistoryPage publication = null!;
    private MapSpritePalettes palettes = null!;
    private Func<bool>? admitted;
    private bool abandoned;
    private string unknown = "";

    internal void Configure(GameResourceRepository repository, PreviewText text,
        InformationHistoryPage publication, Func<bool> admitted, Action close, Func<int> scrollSpeed)
    {
        this.publication = publication; this.admitted = admitted; unknown = text.Get("detail_unknown");
        assets = new(repository); font = new(repository, text.SourceLocale);
        shader = UiSourceSprite.CreateShader(); palettes = MapSpritePalettes.Create();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var header = new UiSourceHeader(); canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, publication.Title);
        ConfigureSourcePage("InformationHistory", header, close);
        var source = new UiSourceLayout(repository, 0xBC4131C3);
        Vector2 mount = new(341, 135);
        source.Blur(canvas, 3, source.Rectangle(3, mount));
        source.Art(canvas, 5, source.Rectangle(5, mount), assets, shader);
        Rect2 area = source.Rectangle(7, mount);
        rows = new() { Name = "HistoryRows", Position = area.Position, Size = area.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(rows);
        var rowSource = new UiSourceLayout(repository, 0xE1B83276);
        var bodyPreset = MapTextPresets.Create()[0];
        for (int index = 0; index < Math.Min(15, publication.Rows.Length); index++)
        {
            var root = new Control { Name = "HistoryVisibleRow" + index, Size = rowSource.Rectangle(0).Size,
                MouseFilter = MouseFilterEnum.Pass };
            rows.AddChild(root);
            var flag = rowSource.Art(root, 2, rowSource.Rectangle(2), assets, shader);
            var flagText = rowSource.Text(root, 3, default, "", font);
            var year = rowSource.Text(root, 4, default, "", font, bodyPreset);
            var month = rowSource.Text(root, 5, default, "", font, bodyPreset);
            var period = rowSource.Text(root, 6, default, "", font, bodyPreset);
            var body = rowSource.Text(root, 7, default, "", font, bodyPreset);
            pool.Add(new(root, flag, flagText, year, month, period, body));
            root.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        }
        Rect2 scrollBox = source.Rectangle(6, mount);
        scrollbar = new() { Name = "HistoryScrollbar", Position = scrollBox.Position, Size = scrollBox.Size };
        canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, assets, shader, Math.Max(0, publication.Rows.Length * 50 - (int)rows.Size.Y),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        rows.GuiInput += input => { if (ReadAdmission()) scrollbar.HandleWheel(input); };
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "HistoryReturn",
            Position = footer.Rectangle(1, new(0, 904)).Position, InputAdmission = ReadAdmission };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestClose, UiAudioCue.Cancel);
        if (publication.UnresolvedOwner is not null)
        {
            var help = new UiSourceLayout(repository, 0xAB6ADAA7);
            Vector2 helpMount = footer.Rectangle(19, new(0, 904)).Position;
            foreach (var node in help.Nodes.Where(node => node.Type is 1 or 3))
                help.Art(canvas, node.Index, help.Rectangle(node.Index, helpMount), assets, shader);
            help.Text(canvas, help.Nodes.Single(node => node.Type == 2).Index, helpMount,
                text.Get("information_partial"), font);
        }
        Resized += Fit; Fit();
        scrollbar.SetValue(scrollbar.Range); PositionRows();
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private void PositionRows()
    {
        int first = scrollbar.Value / 50;
        for (int slot = 0; slot < pool.Count; slot++)
        {
            var display = pool[slot]; int index = first + slot;
            if (index >= publication.Rows.Length) { display.Root.Hide(); display.BoundIndex = -1; continue; }
            var row = publication.Rows[index];
            display.Root.Position = new(0, index * 50 - scrollbar.Value - 5); display.Root.Show();
            if (display.BoundIndex == index) continue;
            display.BoundIndex = index;
            display.Year.Visible = display.Month.Visible = display.Period.Visible = row.ShowDate;
            display.Year.Text = row.YearText ?? unknown; display.Month.Text = row.MonthText ?? unknown;
            display.Period.Text = row.PeriodText ?? unknown;
            var fill = MapSpritePalettes.ForceFill(row.ColorIndex);
            foreach (var sprite in display.Flag.GetChildren().OfType<UiSourceSprite>())
                sprite.SetColors(new(fill, fill, fill, fill));
            display.FlagText.SetRuns(new UiSourceTextRun(row.ForceShortName, palettes.ForceLetter(row.ColorIndex).CpuPacked));
            display.Body.SetKnownColourLine(row.Text);
        }
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        abandoned = true; admitted = null; pool.Clear();
        base._ExitTree(); font?.Dispose(); assets?.Dispose(); shader?.Dispose();
    }
}
