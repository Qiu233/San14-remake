using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CurrentWorldAreaPageValues(AreaInformationPage Information,
    int? Population, int? Security, bool Dirty, string Notice);

/// <summary>The original Area editing body and its retained current neighbors.
/// Draft projections arrive only when its parent changes the local draft.</summary>
internal partial class CurrentWorldAreaEditView : Control
{
    private AdministrationPageChrome chrome = null!;
    private Func<CurrentWorldAreaPageValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? requested;
    private readonly List<(int SourceNode, UiSourceText Label)> values = [];
    private readonly List<UiSourceCommandButton> commands = [];
    private string unknown = "";
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<CurrentWorldAreaPageValues> read, Func<bool> admitted,
        Action returned, Action accepted, Action<int> requested)
    {
        this.read = read; this.admitted = admitted; this.returned = returned; this.accepted = accepted; this.requested = requested;
        unknown = text.Get("detail_unknown");
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        string Message(int id) => data[id].Text ?? throw new InvalidDataException("Missing Area editing caption.");
        Name = "CurrentWorldAreaEditor"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x78, RequestBack, () =>
            { if (CanEdit() && this.read?.Invoke().Dirty == true) this.accepted?.Invoke(); });
        var information = read().Information;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_01_12_edit_area"));
        Vector2 mount = new(260, 196);
        var hidden = new HashSet<int> { 7, 9, 23, 59, 61, 168, 185, 202 };
        hidden.UnionWith(Enumerable.Range(65, 14)); hidden.UnionWith(Enumerable.Range(94, 8));
        for (int slot = 0; slot < 8; slot++)
        {
            int row = slot;
            hidden.Add(120 + 6 * slot);
            if (!information.Neighbors.Any(neighbor => neighbor.Slot == row)) hidden.Add(119 + 6 * slot);
        }
        bool Hidden(int index)
        {
            for (int? cursor = index; cursor is int at; cursor = source.Nodes[at].ParentIndex)
                if (hidden.Contains(at)) return true;
            return false;
        }
        if (information.StandingPortrait is { } standing)
        {
            var portrait = new UiSourceStandingPortrait { Name = "EditedAreaOverseer", Position = source.Rectangle(7, mount).Position };
            chrome.Canvas.AddChild(portrait); portrait.Configure(repository, chrome.Assets, chrome.SpriteShader,
                standing.LoadSprites(new LocalizedUiSpriteSource(repository).LoadTexturePackSprite));
            portrait.Scale = source.Rectangle(7, mount).Size / portrait.Size;
        }
        foreach (var node in source.Nodes)
        {
            if (Hidden(node.Index)) continue;
            if (node.Type is 1 or 3) source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
            else if (node.Type == 6) source.Blur(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount));
        }
        foreach (var (node, caption) in new[] { (18,0x1FD),(20,0x3D4),(22,0x1FE),(27,0x201),
            (35,0x2B3),(43,0x2B4),(51,0x2B5),(60,0x298),(64,0x253),(82,0x254),(87,0x255),(92,0x256),
            (107,0x3D3),(114,0x613),(118,0x252) })
            if (!Hidden(node)) source.Text(chrome.Canvas, node, mount, Message(caption), chrome.Font, MapTextPresets.Create()[13]);
        foreach (var (node, original) in new[] { (19,14),(21,16),(24,19),(28,23),(36,31),(37,32),(38,33),(39,34),
            (44,39),(45,40),(46,41),(47,42),(52,47),(53,48),(54,49),(55,50),(83,78),(88,83),(93,88),
            (108,120),(109,121),(110,122),(115,127) })
            Bind(node, original);
        var badge = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, AreaInformationSources.NameplateLayoutName));
        var nameplate = new Control { Name = "EditedAreaNameplate", Position = source.Rectangle(9, mount).Position,
            Size = badge.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(nameplate);
        foreach (var node in badge.Nodes)
            if (node.Type is 1 or 3 && node.Index is not (5 or 6))
                badge.Art(nameplate, node.Index, badge.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        badge.Text(nameplate, 7, default, information.Name, chrome.Font);
        badge.Text(nameplate, 8, default, "", chrome.Font);
        if (information.ForceFlag is { } appearance)
        {
            var fill = appearance.Fill;
            badge.Art(nameplate, 5, badge.Rectangle(5), chrome.Assets, chrome.SpriteShader, new(fill, fill, fill, fill));
            var glyph = badge.Text(nameplate, 6, default, appearance.Glyph, chrome.Font);
            uint color = appearance.Letter.CpuPacked;
            glyph.Configure(chrome.Font, MapFontProfile.FromNode(badge.Nodes[6], badge.Source.Source.ResourceUri)
                with { ColourLT = color, ColourRT = color, ColourLB = color, ColourRB = color });
        }
        foreach (var neighbor in information.Neighbors)
        {
            int slot = neighbor.Slot;
            Bind(121 + 6 * slot, 133 + 6 * slot, MapTextPresets.Create()[13]);
            for (int field = 0; field < 3; field++) Bind(122 + 6 * slot + field, 134 + 6 * slot + field);
            if (neighbor.Flag is { } flagAppearance)
            {
                var flag = new UiSourceForceFlag { Name = "EditedAreaNeighbor" + slot, Position = source.Rectangle(120 + 6 * slot, mount).Position };
                chrome.Canvas.AddChild(flag); flag.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, flagAppearance, neighbor: true);
            }
        }
        foreach (var (node, caption) in new[] { (168,0x25B),(185,0x298),(202,0x2C3) })
        {
            int entry = commands.Count;
            var box = source.Rectangle(node, mount);
            var button = new UiSourceCommandButton { Name = "EditArea" + new[] { "Development", "Population", "Security" }[entry],
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); commands.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(caption),
                () => { if (CanEdit()) this.requested?.Invoke(entry); });
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();

        void Bind(int node, int original, MapTextPreset? preset = null)
        {
            if (Hidden(node)) return;
            var label = source.Text(chrome.Canvas, node, mount, "", chrome.Font, preset);
            values.Add((original, label));
            if (node == 19 && information.ForceNameColor is { } color)
                label.Configure(chrome.Font, MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)
                    with { ColourLT = color.CpuPacked, ColourRT = color.CpuPacked, ColourLB = color.CpuPacked, ColourRB = color.CpuPacked });
        }
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var page = read();
        foreach (var (node, label) in values) label.Text = page.Information.Text.GetValueOrDefault(node, unknown);
        commands[1].Caption = page.Population?.ToString() ?? unknown;
        commands[2].Caption = page.Security?.ToString() ?? unknown;
        foreach (var command in commands) command.Disabled = busy;
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && page.Dirty, true); chrome.ApplyDefaultHint(page.Notice);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; read = null; admitted = null; returned = accepted = null; requested = null;
        foreach (var command in commands) command.Abandon();
        chrome?.Abandon(); Hide();
    }
}
