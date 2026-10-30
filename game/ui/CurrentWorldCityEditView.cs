using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CurrentWorldCityPageValues(CityInformationPage Information,
    int? Size, IReadOnlyList<CurrentWorldEditNumberField> Resources,
    string CounterTactic, bool Dirty, string Notice);

/// <summary>The original City editing body. Immutable information supplies its
/// identity and status; only the parent's detached draft supplies resources.</summary>
internal partial class CurrentWorldCityEditView : Control
{
    private AdministrationPageChrome chrome = null!;
    private Func<CurrentWorldCityPageValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted, resourcesRequested, tacticRequested;
    private readonly Dictionary<int, UiSourceText> draftText = [];
    private UiSourceCommandButton resources = null!, tactic = null!;
    private Func<int, string> message = null!;
    private string unknown = "";
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<CurrentWorldCityPageValues> read, Func<bool> admitted, Action returned, Action accepted,
        Action resourcesRequested, Action tacticRequested)
    {
        this.read = read; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        this.resourcesRequested = resourcesRequested; this.tacticRequested = tacticRequested;
        unknown = text.Get("detail_unknown");
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        message = id => data[id].Text ?? throw new InvalidDataException("Missing City editing caption.");
        Name = "CurrentWorldCityEditor"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x77, RequestBack, () =>
            { if (CanEdit() && this.read?.Invoke().Dirty == true) this.accepted?.Invoke(); });
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_01_12_edit_city"));
        Vector2 mount = new(260, 196);
        int portraitLayer = chrome.Canvas.GetChildCount();
        int[] replaced = [7, 9, 23, 64, 71, 116, 138];
        foreach (var node in source.Nodes)
        {
            bool hidden = false;
            for (int? cursor = node.Index; cursor is int at; cursor = source.Nodes[at].ParentIndex)
                if (replaced.Contains(at)) { hidden = true; break; }
            if (hidden) continue;
            if (node.Type is 1 or 3) source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
            else if (node.Type == 6) source.Blur(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount));
        }
        foreach (var (node, caption) in new[] { (18,0x1FD),(20,0x215),(22,0x1FE),(27,0x1FF),(29,0x200),
            (38,0x218),(43,0x217),(48,0x3EC),(53,0x202),(58,0x3ED),(63,0x25E),(70,0x25C),
            (79,0x5EA),(84,0x225),(91,0x20B),(98,0x20C),(105,0x20D),(112,0x20E),(137,0x234) })
            source.Text(chrome.Canvas, node, mount, message(caption), chrome.Font, MapTextPresets.Create()[13]);
        var information = read().Information;
        foreach (var (node, original) in new[] { (19,14),(21,16),(24,19),(28,23),(30,25),(39,48),(44,53),
            (49,58),(54,63),(59,73),(65,79),(72,86) })
        {
            var label = source.Text(chrome.Canvas, node, mount, information.Text.GetValueOrDefault(original, unknown), chrome.Font);
            if (node == 19 && information.ForceNameColor is { } color)
                label.Configure(chrome.Font, MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri) with
                { ColourLT = color.CpuPacked, ColourRT = color.CpuPacked, ColourLB = color.CpuPacked, ColourRB = color.CpuPacked });
            if (information.UnresolvedFields.TryGetValue(original, out string? missing)) label.SetMeta("unresolved_owner", missing);
        }
        foreach (int node in new[] { 66, 73 }) source.Text(chrome.Canvas, node, mount, message(0x469), chrome.Font);
        foreach (int node in new[] { 86, 93, 100, 107, 114 }) source.Text(chrome.Canvas, node, mount, message(0x60C), chrome.Font);
        foreach (int node in new[] { 80, 85, 87, 92, 94, 99, 101, 106, 108, 113, 115 })
            draftText[node] = source.Text(chrome.Canvas, node, mount, "", chrome.Font);
        if (information.StandingPortrait is { } standing)
        {
            var portrait = new UiSourceStandingPortrait { Name = "EditedCityGovernor", Position = source.Rectangle(7, mount).Position };
            chrome.Canvas.AddChild(portrait);
            portrait.Configure(repository, chrome.Assets, chrome.SpriteShader,
                standing.LoadSprites(new LocalizedUiSpriteSource(repository).LoadTexturePackSprite));
            portrait.Scale = source.Rectangle(7, mount).Size / portrait.Size;
            chrome.Canvas.MoveChild(portrait, portraitLayer);
        }
        var badge = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_01_03_00_info_city_offset_00"));
        var nameplate = new Control { Name = "EditedCityNameplate", Position = source.Rectangle(9, mount).Position,
            Size = badge.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(nameplate);
        foreach (var node in badge.Nodes)
            if (node.Type is 1 or 3 && node.Index is not (5 or 6))
                badge.Art(nameplate, node.Index, badge.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        badge.Text(nameplate, 7, default, information.Name, chrome.Font);
        if (information.ForceFlag is { } appearance)
        {
            var flag = new UiSourceForceFlag { Name = "EditedCityForce", Position = badge.Rectangle(4).Position };
            nameplate.AddChild(flag); flag.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, appearance, embedded: true);
        }
        if (information.DistrictNumber is int district)
            source.Art(chrome.Canvas, 23, source.Rectangle(23, mount), chrome.Assets, chrome.SpriteShader,
                commonSpriteOverride: ArmyMapUiSources.Create().IconName(ArmyMapUiSources.DistrictIcon(checked((byte)district))));
        foreach (var (node, original) in new[] { (64,78),(71,85) })
            if (information.SpriteIds.TryGetValue(original, out int sprite))
                source.Art(chrome.Canvas, node, source.Rectangle(node, mount), chrome.Assets, chrome.SpriteShader,
                    commonSpriteOverride: ArmyMapUiSources.Create().IconName(new(sprite, 0)));
        resources = Command(116, "EditCityResources", message(0x2BB), () => this.resourcesRequested?.Invoke());
        tactic = Command(138, "EditCityCounterTactic", "", () => this.tacticRequested?.Invoke());
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();

        UiSourceCommandButton Command(int node, string name, string caption, Action action)
        {
            var box = source.Rectangle(node, mount);
            var button = new UiSourceCommandButton { Name = name, Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption,
                () => { if (CanEdit()) action(); });
            return button;
        }
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var page = read();
        if (page.Resources.Count != 5) throw new InvalidDataException("City editing requires its five source resource rows.");
        draftText[80].Text = page.Size is >= 1 and <= 4 ? message(0x46B + page.Size.Value) : unknown;
        int[] values = [99, 106, 113, 92, 85], maxima = [101, 108, 115, 94, 87];
        for (int index = 0; index < values.Length; index++)
        {
            draftText[values[index]].Text = page.Resources[index].Value?.ToString() ?? unknown;
            draftText[maxima[index]].Text = page.Resources[index].Maximum?.ToString() ?? unknown;
        }
        tactic.Caption = page.CounterTactic; resources.Disabled = tactic.Disabled = busy;
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && page.Dirty, true); chrome.ApplyDefaultHint(page.Notice);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; read = null; admitted = null; returned = accepted = resourcesRequested = tacticRequested = null;
        resources?.Abandon(); tactic?.Abandon(); chrome?.Abandon(); Hide();
    }
}
