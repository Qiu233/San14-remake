using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record MapInformationMenuEntry(int Node, string Caption, bool Visible,
    bool Enabled, string? Hint = null);

/// <summary>The original three-tab information selector. The retained page
/// supplies its available destinations; this view never queries a world.</summary>
internal partial class MapInformationMenuView : Control
{
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly Dictionary<int, UiSourceCommandButton> commands = [];
    private readonly Dictionary<int, string?> hints = [];
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private UiSourceHeader header = null!;
    private readonly Dictionary<int, (Node2D Root, Vector2 ContentMount)> motionGroups = [];
    private UiSourceRoundButton back = null!;
    private UiSourceBottomButton history = null!;
    private UiSourceText guide = null!;
    private Func<bool>? admitted;
    private Func<int, IReadOnlyList<MapInformationMenuEntry>>? readEntries;
    private Action? returned, openHistory;
    private Action<int, int>? accepted;
    private int selectedTab;
    private bool abandoned, busy;
    private string defaultHint = "", returnHint = "";
    private string? hoveredHint;

    internal int SelectedTab => selectedTab;
    internal UiSourceHeader SourceHeader => header;
    internal IReadOnlyDictionary<int, (Node2D Root, Vector2 ContentMount)> MotionGroups => motionGroups;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<bool> admitted, Func<int, IReadOnlyList<MapInformationMenuEntry>> readEntries,
        Action returned, Action<int, int> accepted, Action openHistory,
        string guideText, string returnHint, bool historyEnabled, int initialTab, IReadOnlyList<bool> tabEnabled,
        AdministrationPageChrome.Resources resources)
    {
        this.admitted = admitted;
        this.readEntries = readEntries;
        this.returned = returned;
        this.accepted = accepted;
        this.openHistory = openHistory;
        this.defaultHint = guideText;
        this.returnHint = returnHint;
        Name = "InformationMenu";
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = resources.Assets;
        font = resources.Font;
        shader = resources.SpriteShader;
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var bodyMotion = new Node2D { Name = "InformationBodyMotion" }; canvas.AddChild(bodyMotion);
        var body = new Control { Name = "InformationBody", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        bodyMotion.AddChild(body);
        var headerMotion = new Node2D { Name = "InformationHeaderMotion" }; canvas.AddChild(headerMotion);
        header = new() { Name = "InformationTitle" }; headerMotion.AddChild(header);
        var captions = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        header.Configure(repository, assets, shader, font,
            captions[0x26].Text ?? throw new InvalidDataException("Missing information header caption."));
        var footerMotion = new Node2D { Name = "InformationFooterMotion" }; canvas.AddChild(footerMotion);
        motionGroups.Add(1, (bodyMotion, Vector2.Zero));
        motionGroups.Add(4, (headerMotion, Vector2.Zero));
        motionGroups.Add(6, (footerMotion, new(0, 904)));
        var source = assets.SourceLayout(0x79821E40);
        Vector2 size = source.Rectangle(0).Size;
        Vector2 mount = new((int)(960 - size.X / 2), (int)(540 - size.Y / 2));
        source.Blur(body, 17, source.Rectangle(17, mount));
        source.Art(body, 19, source.Rectangle(19, mount), assets, shader);
        int[] tabNodes = [2, 6, 10], tabCaptions = [0x050C, 0x0598, 0x0563];
        uint[] tabLayouts = [0x172EDC24, 0x9AFB27B7, 0xD45873AA];
        for (int index = 0; index < tabNodes.Length; index++)
        {
            int tabIndex = index;
            Rect2 box = source.Rectangle(tabNodes[index], mount);
            var tab = new UiSourceTabButton { Name = "InformationTab" + index,
                Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            body.AddChild(tab);
            tab.Configure(repository, assets, shader, font,
                captions[tabCaptions[index]].Text ?? throw new InvalidDataException("Missing information tab caption."),
                tabLayouts[index]);
            tab.Disabled = !tabEnabled[index];
            tab.Pressed += () => { if (ReadAdmission()) ApplyTab(tabIndex); };
            tabs.Add(tab);
        }
        for (int node = 20; node <= 60; node += 4)
        {
            int sourceNode = node;
            Rect2 box = source.Rectangle(node, mount);
            var button = new UiSourceCommandButton { Name = "InformationCommand" + node,
                Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            body.AddChild(button);
            button.Configure(repository, assets, shader, font, "", () =>
            {
                if (ReadAdmission() && !button.Disabled) this.accepted?.Invoke(selectedTab, sourceNode);
            });
            button.MouseEntered += () => { if (ReadAdmission()) SetHover(hints.GetValueOrDefault(sourceNode)); };
            button.MouseExited += () => { if (!abandoned) SetHover(null); };
            commands.Add(node, button);
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Vector2 footerMount = new(0, 904);
        back = new() { Name = "InformationReturn", Position = footer.Rectangle(1, footerMount).Position };
        footerMotion.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru",
            RequestBack, UiAudioCue.Cancel);
        history = new() { Name = "InformationHistory", Position = footer.Rectangle(14, footerMount).Position,
            InputAdmission = ReadAdmission };
        footerMotion.AddChild(history);
        history.Configure(repository, assets, shader, font,
            captions[0x05BB].Text ?? throw new InvalidDataException("Missing history log caption."),
            () => { if (ReadAdmission() && !history.Disabled) this.openHistory?.Invoke(); });
        history.Disabled = !historyEnabled;
        var help = new UiSourceLayout(repository, 0xAB6ADAA7);
        Vector2 helpMount = footer.Rectangle(19, footerMount).Position;
        var narrationMotion = new Node2D { Name = "InformationNarrationMotion" }; canvas.AddChild(narrationMotion);
        var helpBody = new Control { Name = "InformationGuide", MouseFilter = MouseFilterEnum.Ignore };
        narrationMotion.AddChild(helpBody);
        motionGroups.Add(7, (narrationMotion, helpMount));
        foreach (var node in help.Nodes.Where(node => node.Type is 1 or 3))
            help.Art(helpBody, node.Index, help.Rectangle(node.Index, helpMount), assets, shader);
        guide = help.Text(helpBody, help.Nodes.Single(node => node.Type == 2).Index, helpMount, guideText, font);
        Resized += Fit;
        Fit();
        ApplyTab(initialTab);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);

    private void ApplyTab(int tab)
    {
        if (abandoned || readEntries is null) return;
        selectedTab = tab;
        foreach (var button in commands.Values) { button.Hide(); button.Disabled = true; }
        hints.Clear();
        foreach (var entry in readEntries(tab))
        {
            if (!commands.TryGetValue(entry.Node, out var button))
                throw new InvalidDataException("Information menu destination has no source mount.");
            button.Caption = entry.Caption;
            button.Visible = entry.Visible;
            button.Disabled = busy || !entry.Enabled;
            hints[entry.Node] = entry.Hint;
        }
        for (int index = 0; index < tabs.Count; index++) tabs[index].Selected = index == tab;
        SetHover(null);
    }

    private void SetHover(string? text) { hoveredHint = text; guide.Text = text ?? defaultHint; }
    internal void ApplyNotice(string text) { if (!abandoned) { defaultHint = text; SetHover(null); } }
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value;
        back.Disabled = value;
        // Tabs disabled by the source mode remain disabled after a child closes.
        foreach (var tab in tabs) tab.InputAdmission = ReadAdmission;
        ApplyTab(selectedTab);
    }
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true;
        admitted = null; readEntries = null; returned = openHistory = null; accepted = null;
        foreach (var button in commands.Values) button.Abandon();
        foreach (var tab in tabs) tab.Disabled = true;
        Hide();
    }
    public override void _Process(double delta)
    {
        if (!abandoned && IsVisibleInTree())
            guide.Text = GetViewport().GuiGetHoveredControl() == back ? returnHint : hoveredHint ?? defaultHint;
    }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree() => Abandon();
}
