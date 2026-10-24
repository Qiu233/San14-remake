using System.Globalization;
using Godot;
using San14.Data;

/// <summary>The original popularity page, displaying three captured doctrine
/// groups and three independently scrollable explanation channels.</summary>
internal partial class MapInformationDestinyView : DetailsView
{
    private sealed record ExplanationDisplay(Control Root, UiSourceParagraph Body)
    {
        internal int BoundIndex { get; set; } = -1;
    }
    private sealed record ChannelDisplay(Control Root, UiSourceScrollbar Scrollbar,
        IReadOnlyList<ExplanationDisplay> Rows);
    private readonly Dictionary<int, ChannelDisplay> channels = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private Func<bool>? admitted;
    private bool abandoned;
    private int selected;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        InformationDestinyPage publication, Func<bool> admitted, Action close, Func<int> scrollSpeed)
    {
        this.admitted = admitted;
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var header = new UiSourceHeader(); canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, publication.Title);
        ConfigureSourcePage("InformationPopularity", header, close);
        var source = new UiSourceLayout(repository, 0xC06CEC9C);
        Vector2 mount = new(190, 170);
        source.Art(canvas, 2, source.Rectangle(2, mount), assets, shader);
        foreach (int node in new[] { 5, 6, 10, 26, 27, 31, 32, 36, 52, 53, 57, 58, 62, 78, 79, 83, 84 })
            source.Art(canvas, node, source.Rectangle(node, mount), assets, shader);
        var presets = MapTextPresets.Create();
        source.Text(canvas, 7, mount, publication.TopCaption, font, presets[13]);
        source.Text(canvas, 8, mount, string.Join(" ", publication.TopDoctrineNames), font, presets[0]);
        for (int index = 0; index < Math.Min(3, publication.Groups.Length); index++)
        {
            var group = publication.Groups[index]; int offset = 26 * index;
            source.Text(canvas, 28 + offset, mount, group.TotalCaption, font, presets[13]);
            source.Text(canvas, 29 + offset, mount, Number(group.Total), font, presets[0]);
            source.Text(canvas, 33 + offset, mount, group.CountCaption, font, presets[13]);
            source.Text(canvas, 34 + offset, mount, Number(group.ForceCount), font, presets[0]);
            for (int member = 0; member < Math.Min(2, group.Representatives.Length); member++)
            {
                var representative = group.Representatives[member]; int column = member * 7;
                source.Text(canvas, 16 + offset + column, mount, representative.DoctrineName, font, presets[13]);
                source.Text(canvas, 17 + offset + column, mount,
                    representative.RulerName ?? (representative.UnresolvedOwner is null ? "" : text.Get("detail_unknown")), font, presets[0]);
                if (representative.Portrait is { } sprite)
                {
                    Rect2 box = source.Rectangle(12 + offset + column, mount);
                    var portrait = new UiSourcePortrait { Position = box.Position, Size = box.Size };
                    canvas.AddChild(portrait); portrait.Configure(repository, assets, shader, sprite);
                }
                source.Art(canvas, 15, source.Rectangle(14 + offset + column, mount), assets, shader);
            }
        }
        source.Blur(canvas, 88, source.Rectangle(88, mount));
        source.Art(canvas, 100, source.Rectangle(100, mount), assets, shader);
        source.Art(canvas, 102, source.Rectangle(102, mount), assets, shader);
        uint[] tabLayouts = [0x172EDC24, 0x9AFB27B7, 0xD45873AA];
        for (int index = 0; index < publication.ExplanationChannels.Length; index++)
        {
            int tabIndex = index;
            Rect2 box = source.Rectangle(89 + index * 2, mount);
            var tab = new UiSourceTabButton { Name = "PopularityExplanationTab" + index,
                Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            canvas.AddChild(tab);
            tab.Configure(repository, assets, shader, font, publication.ExplanationChannels[index].Caption, tabLayouts[index]);
            tab.Pressed += () => { if (ReadAdmission()) Select(tabIndex); };
            tabs.Add(tab);
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var back = new UiSourceRoundButton { Name = "PopularityReturn",
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
        Resized += Fit; Fit(); Select(0);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("original_explanation_slot_composition", false);

        string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? text.Get("detail_unknown");

        void Select(int index)
        {
            if (abandoned || (uint)index >= publication.ExplanationChannels.Length) return;
            selected = index;
            foreach (var display in channels.Values) { display.Root.Hide(); display.Root.ProcessMode = ProcessModeEnum.Disabled; }
            foreach (var (tab, position) in tabs.Select((tab, position) => (tab, position))) tab.Selected = position == index;
            if (!channels.TryGetValue(index, out var channel))
            {
                var entries = publication.ExplanationChannels[index].Rows;
                Rect2 box = source.Rectangle(101, mount), scrollBox = source.Rectangle(103, mount);
                var host = new Control { Name = "PopularityExplanationChannel" + index,
                    Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
                canvas.AddChild(host);
                var rows = new Control { Position = box.Position, Size = box.Size, ClipContents = true,
                    MouseFilter = MouseFilterEnum.Pass };
                host.AddChild(rows);
                var scrollbar = new UiSourceScrollbar { Position = scrollBox.Position, Size = scrollBox.Size };
                host.AddChild(scrollbar);
                var pool = new List<ExplanationDisplay>();
                var profile = MapFontProfile.FromNode(source.Nodes[101], source.Source.Source.ResourceUri).WithPreset(presets[0]);
                for (int slot = 0; slot < Math.Min(11, entries.Length); slot++)
                {
                    var root = new Control { Size = new(box.Size.X, 52), MouseFilter = MouseFilterEnum.Pass };
                    rows.AddChild(root);
                    var body = new UiSourceParagraph { Size = root.Size };
                    root.AddChild(body); body.Configure(font, profile);
                    pool.Add(new(root, body));
                    root.GuiInput += input => { if (ReadAdmission() && selected == index) scrollbar.HandleWheel(input); };
                }
                scrollbar.Configure(repository, assets, shader, Math.Max(0, entries.Length * 52 - (int)box.Size.Y),
                    scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
                rows.GuiInput += input => { if (ReadAdmission() && selected == index) scrollbar.HandleWheel(input); };
                channel = new(host, scrollbar, pool); channels.Add(index, channel);
                PositionRows();

                void PositionRows()
                {
                    int first = scrollbar.Value / 52;
                    for (int slot = 0; slot < pool.Count; slot++)
                    {
                        var display = pool[slot]; int row = first + slot;
                        if (row >= entries.Length) { display.Root.Hide(); display.BoundIndex = -1; continue; }
                        display.Root.Position = new(0, row * 52 - scrollbar.Value); display.Root.Show();
                        if (display.BoundIndex == row) continue;
                        display.BoundIndex = row;
                        display.Body.Apply(entries[row].Text ?? text.Get("detail_unknown"));
                    }
                }
            }
            channel.Root.ProcessMode = ProcessModeEnum.Inherit; channel.Root.Show();
        }
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree()
    {
        abandoned = true; admitted = null; channels.Clear(); tabs.Clear();
        base._ExitTree(); font?.Dispose(); assets?.Dispose(); shader?.Dispose();
    }
}
