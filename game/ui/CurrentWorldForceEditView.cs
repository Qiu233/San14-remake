using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CurrentWorldForcePointDisplay(string Name, int? Points);
internal sealed record CurrentWorldForceRelationDisplay(int ForceId, string Name, string Friendship, string AllianceMonths);
internal sealed record CurrentWorldForcePageValues(ForceInformationPage Information,
    string Rank, string NationalTitle, string DoctrineLevel, int? DoctrineExperience, int? CivilReputation,
    string MilitaryInformation, IReadOnlyList<CurrentWorldForcePointDisplay> DoctrinePoints,
    IReadOnlyList<CurrentWorldForceRelationDisplay> Relationships,
    bool CanEditRank, bool CanEditNationalTitle, bool CanEditDoctrineLevel, bool Dirty, string Notice);

/// <summary>The two original Force editor pages share one detached draft.
/// The diplomacy page keeps its original row buttons and scroll on child return.</summary>
internal partial class CurrentWorldForceEditView : Control
{
    private AdministrationPageChrome chrome = null!;
    private Func<CurrentWorldForcePageValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? requested, relationshipRequested;
    private readonly List<UiSourceCommandButton> commands = [];
    private readonly List<UiSourceCommandButton> tabs = [];
    private readonly List<(UiSourceCommandButton Button, UiSourceText Relation, UiSourceText Months)> relations = [];
    private readonly List<UiSourceText> points = [];
    private Control basic = null!, diplomacy = null!;
    private UiSourceText experience = null!;
    private UiSourceScrollbar scrollbar = null!;
    private bool abandoned, busy;
    private int selectedTab;
    private string unknown = "";

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<CurrentWorldForcePageValues> read, Func<bool> admitted, Action returned, Action accepted,
        Action<int> requested, Action<int> relationshipRequested, Func<int> scrollSpeed)
    {
        this.read = read; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        this.requested = requested; this.relationshipRequested = relationshipRequested;
        unknown = text.Get("detail_unknown");
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        string Message(int id) => data[id].Text ?? throw new InvalidDataException("Missing Force editing caption.");
        Name = "CurrentWorldForceEditor"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x76, RequestBack, () =>
            { if (CanEdit() && this.read?.Invoke().Dirty == true) this.accepted?.Invoke(); });
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        Vector2 shellMount = new(191, 147);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, shellMount));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, shellMount), chrome.Assets, chrome.SpriteShader);
        basic = Body("11_01_12_edit_seiryoku_1_cn", "CurrentWorldForceBasic", out var source, [137, 157, 165, 55, 161, 172]);
        foreach (var (node, caption) in new[] { (17,0x3ED),(23,0x1FE),(28,0x207),(33,0x218),(38,0x2DD),(45,0x2DE),
            (50,0x83E),(54,0x83F),(78,0x20C),(83,0x254),(88,0x3F2),(99,0x20D),(104,0x255),(109,0x3F4),
            (120,0x20E),(125,0x256),(130,0x226),(136,0x21B),(156,0x3C8),(164,0x244),(168,0x6E0),(160,0x835) })
            source.Text(basic, node, default, Message(caption), chrome.Font, MapTextPresets.Create()[13]);
        var information = read().Information;
        foreach (var (node, original) in new[] { (18,18),(24,24),(29,29),(34,34),(39,44),(40,45),(41,46),(46,51),(51,56),
            (79,69),(84,74),(89,79),(100,90),(105,95),(110,100),(121,111),(126,116),(131,121) })
            source.Text(basic, node, default, information.Text.GetValueOrDefault(original, unknown), chrome.Font);
        experience = source.Text(basic, 170, default, "", chrome.Font);
        int[] pointCaption = [0x82F, 0x831, 0x834, 0x833, 0x832, 0x830];
        for (int index = 0; index < pointCaption.Length; index++)
        {
            source.Text(basic, 177 + index * 5, default, Message(pointCaption[index]), chrome.Font, MapTextPresets.Create()[13]);
            points.Add(source.Text(basic, 178 + index * 5, default, "", chrome.Font));
        }
        foreach (int node in new[] { 137, 157, 165, 55, 161, 172 })
        {
            int entry = commands.Count; var box = source.Rectangle(node);
            var button = new UiSourceCommandButton { Name = "EditForce" + new[] { "Rank", "NationalTitle", "DoctrineLevel", "CivilReputation", "MilitaryInformation", "DoctrinePoints" }[entry],
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            basic.AddChild(button); commands.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, entry == 5 ? Message(0x81F) : "",
                () => { if (CanEdit() && !button.Disabled) this.requested?.Invoke(entry); });
        }

        diplomacy = Body("11_01_12_edit_seiryoku_2", "CurrentWorldForceDiplomacy", out var diplomacySource, Enumerable.Range(20, 11).ToArray());
        diplomacySource.Text(diplomacy, 17, default, Message(0x3ED), chrome.Font, MapTextPresets.Create()[13]);
        diplomacySource.Text(diplomacy, 18, default, information.Text.GetValueOrDefault(18, unknown), chrome.Font);
        var rowSource = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_01_12_edit_seiryoku_offset_00"));
        var placeholder = diplomacySource.Rectangle(20);
        var clip = new Control { Name = "ForceEditDiplomacyRows", Position = placeholder.Position + new Vector2(0, placeholder.Size.Y),
            Size = new(placeholder.Size.X, placeholder.Size.Y * 10), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        diplomacy.AddChild(clip);
        scrollbar = new() { Name = "ForceEditDiplomacyScroll", Position = shell.Rectangle(32, shellMount).Position,
            Size = shell.Rectangle(32, shellMount).Size };
        chrome.Canvas.AddChild(scrollbar);
        var hosts = new List<Control>();
        foreach (var relationship in read().Relationships)
        {
            int index = hosts.Count;
            var row = new Control { Name = "ForceEditDiplomacyRow" + relationship.ForceId,
                Position = new(0, index * 64), Size = rowSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Pass };
            clip.AddChild(row); hosts.Add(row);
            foreach (var node in rowSource.Nodes)
            {
                bool replaced = false;
                for (int? cursor = node.Index; cursor is int at; cursor = rowSource.Nodes[at].ParentIndex)
                    if (at is 5 or 11 || at >= 22) { replaced = true; break; }
                if (!replaced && node.Type is 1 or 3) rowSource.Art(row, node.Index, rowSource.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
            }
            foreach (var (node, caption) in new[] { (4,0x632),(15,0x413),(20,0x414) })
                rowSource.Text(row, node, default, Message(caption), chrome.Font, MapTextPresets.Create()[13]);
            var box = rowSource.Rectangle(5);
            var button = new UiSourceCommandButton { Name = "EditForceRelationship" + relationship.ForceId,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            row.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, relationship.Name,
                () => { if (CanEdit()) this.relationshipRequested?.Invoke(relationship.ForceId); });
            relations.Add((button, rowSource.Text(row, 16, default, "", chrome.Font), rowSource.Text(row, 21, default, "", chrome.Font)));
        }
        scrollbar.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, hosts.Count * 64 - (int)clip.Size.Y), scrollSpeed(),
            offset => { for (int index = 0; index < hosts.Count; index++) hosts[index].Position = new(0, index * 64 - offset); },
            currentScrollSpeed: scrollSpeed);
        clip.GuiInput += input => { if (CanEdit()) scrollbar.HandleWheel(input); };

        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Vector2 tabMount = footer.Rectangle(21, new(0, 904)).Position + new Vector2(45, -103);
        for (int index = 0; index < 2; index++)
        {
            int tabIndex = index; string caption = Message(index == 0 ? 0x3CD : 2);
            var tab = new UiSourceCommandButton { Name = index == 0 ? "ForceEditBasicTab" : "ForceEditDiplomacyTab",
                Position = tabMount, Size = new(Math.Max(96, caption.Length * 28 + 20), 48), InputAdmission = CanEdit };
            chrome.Canvas.AddChild(tab); tabs.Add(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption,
                () => { if (CanEdit()) { selectedTab = tabIndex; Refresh(); } });
            tabMount.X += tab.Size.X;
        }
        SetMeta("source_basic_layout", source.Source.Source.ResourceUri);
        SetMeta("source_diplomacy_layout", diplomacySource.Source.Source.ResourceUri); Refresh();

        Control Body(string layout, string name, out UiSourceLayout bodySource, int[] replaced)
        {
            var content = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, layout)); bodySource = content;
            var body = new Control { Name = name, Position = new(191, 197), Size = content.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            chrome.Canvas.AddChild(body);
            if (read().Information.StandingPortrait is { } standing)
            {
                var portrait = new UiSourceStandingPortrait { Name = "EditedForceRuler", Position = content.Rectangle(2).Position };
                body.AddChild(portrait); portrait.Configure(repository, chrome.Assets, chrome.SpriteShader,
                    standing.LoadSprites(new LocalizedUiSpriteSource(repository).LoadTexturePackSprite));
                portrait.Scale = content.Rectangle(2).Size / portrait.Size;
            }
            foreach (var node in content.Nodes)
            {
                bool hidden = false;
                for (int? cursor = node.Index; cursor is int at; cursor = content.Nodes[at].ParentIndex)
                    if (at is 2 or 4 || replaced.Contains(at)) { hidden = true; break; }
                if (hidden) continue;
                if (node.Type is 1 or 3) content.Art(body, node.Index, content.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
                else if (node.Type == 6) content.Blur(body, node.Index, content.Rectangle(node.Index));
            }
            var badge = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, ForceInformationSources.NameplateLayoutName));
            var plate = new Control { Name = "EditedForceNameplate", Position = content.Rectangle(4).Position,
                Size = badge.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            body.AddChild(plate);
            foreach (int node in new[] { 1, 2, 3 }) badge.Art(plate, node, badge.Rectangle(node), chrome.Assets, chrome.SpriteShader);
            if (read().Information.Nameplate is { } nameplate)
            {
                if (nameplate is { FlagValid: true, FlagAppearance: { } appearance })
                {
                    var flag = new UiSourceForceFlag { Position = badge.Rectangle(4).Position };
                    plate.AddChild(flag); flag.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, appearance);
                }
                badge.Text(plate, 7, default, nameplate.RulerName ?? unknown, chrome.Font); badge.Text(plate, 8, default, "", chrome.Font);
            }
            return body;
        }
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var page = read();
        if (page.DoctrinePoints.Count != points.Count || page.Relationships.Count != relations.Count)
            throw new InvalidDataException("Force editing draft changed its retained source roster.");
        commands[0].Caption = page.Rank; commands[1].Caption = page.NationalTitle; commands[2].Caption = page.DoctrineLevel;
        commands[3].Caption = page.CivilReputation?.ToString() ?? unknown; commands[4].Caption = page.MilitaryInformation;
        for (int index = 0; index < commands.Count; index++) commands[index].Disabled = busy || (index switch
            { 0 => !page.CanEditRank, 1 => !page.CanEditNationalTitle, 2 => !page.CanEditDoctrineLevel, _ => false });
        experience.Text = page.DoctrineExperience?.ToString() ?? unknown;
        for (int index = 0; index < points.Count; index++) points[index].Text = page.DoctrinePoints[index].Points?.ToString() ?? unknown;
        for (int index = 0; index < relations.Count; index++)
        {
            relations[index].Relation.Text = page.Relationships[index].Friendship;
            relations[index].Months.Text = page.Relationships[index].AllianceMonths;
            relations[index].Button.Disabled = busy;
        }
        basic.Visible = selectedTab == 0; diplomacy.Visible = scrollbar.Visible = selectedTab == 1;
        for (int index = 0; index < tabs.Count; index++) { tabs[index].Selected = index == selectedTab; tabs[index].Disabled = busy; }
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && page.Dirty, true); chrome.ApplyDefaultHint(page.Notice);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; read = null; admitted = null; returned = accepted = null; requested = relationshipRequested = null;
        foreach (var button in commands) button.Abandon();
        foreach (var row in relations) row.Button.Abandon();
        foreach (var tab in tabs) { tab.Disabled = true; tab.InputAdmission = null; }
        scrollbar?.SetProcessInput(false); chrome?.Abandon(); Hide();
    }
}
