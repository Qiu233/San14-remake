using Godot;
using San14.Data;
using San14.Data.Formats;
using San14.Core;

internal sealed record OfficerEditorViewOptions(int TitleMessage, bool BiographyVisible, IReadOnlySet<int> EditableSections);

internal sealed record RegisteredOfficerEditorValues(IReadOnlyList<LoadedSprite>? Portrait,
    IReadOnlyList<IReadOnlyDictionary<int, string>> Text, bool CanConfirm, string Notice);

/// <summary>Original ordinary edit pages. The application owns the draft and
/// each child editor; this view only publishes the original section selection.</summary>
internal partial class RegisteredOfficerEditorView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly List<UiSourceButton> commands = [];
    private readonly Dictionary<UiSourceButton, int> commandSections = [];
    private OfficerEditorViewOptions? options;
    private readonly List<Control> panes = [];
    private readonly Dictionary<(int Tab, int Node), UiSourceText> fields = [];
    private readonly Dictionary<int, RegisteredOfficerTraitMarker> traitFields = [];
    private Func<RegisteredOfficerEditorValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action? restoreOriginal;
    private UiSourceBottomButton? restoreOriginalButton;
    private Action<int>? edit;
    private GameResourceRepository repository = null!;
    private string locale = "";
    private UiSourceStandingPortrait? portrait;
    private IReadOnlyList<LoadedSprite>? currentPortrait;
    private Control portraitHost = null!;
    private Rect2 portraitRectangle;
    private bool abandoned, busy;
    private int selectedTab;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<RegisteredOfficerEditorValues> read, Func<bool> admitted,
        Action returned, Action accepted, Action<int> editSection, OfficerEditorViewOptions? options = null)
    {
        this.repository = repository; locale = text.SourceLocale; this.read = read; this.admitted = admitted; this.options = options;
        this.returned = returned; this.accepted = accepted; edit = editSection;
        Name = "RegisteredOfficerEditor"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, options?.TitleMessage ?? 0x0042, RequestBack, () =>
        {
            if (CanEdit() && this.read?.Invoke().CanConfirm == true) this.accepted?.Invoke();
        });
        var source = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadEditor(repository));
        var background = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadBackground(repository));
        var backdrop = new Control { Name = "RegisteredOfficerBackdrop", Size = background.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(backdrop); chrome.Canvas.MoveChild(backdrop, 0);
        foreach (int node in new[] { 2, 3, 4, 5, 6, 8 })
            background.Art(backdrop, node, background.Rectangle(node), chrome.Assets, chrome.SpriteShader,
                sourceMaterial: node is 2 or 3 or 4);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing registered-officer editor text.");
        foreach (int node in new[] { 14, 15, 16 })
            source.Art(chrome.Canvas, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        int[] tabCaptions = [0x03CD, 0x0451, 0x0050];
        for (int index = 0; index < 3; index++)
        {
            int tabIndex = index;
            var box = source.Rectangle(2 + index * 2);
            var button = new UiSourceTabButton { Name = "RegisteredOfficerTab" + index,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); tabs.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(tabCaptions[index]),
                index == 0 ? 0x172EDC24u : index == 2 ? 0xD45873AAu : 0x9AFB27B7u);
            button.Pressed += () => { if (CanEdit() && (tabIndex != 2 || this.options?.BiographyVisible != false)) { selectedTab = tabIndex; Refresh(); } };
            var pane = new Control { Name = "RegisteredOfficerPage" + index, MouseFilter = MouseFilterEnum.Ignore,
                Position = source.Rectangle(17 + index).Position, Size = source.Rectangle(17 + index).Size };
            chrome.Canvas.AddChild(pane); panes.Add(pane);
            var page = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadTab(repository, index));
            if (index == 2)
            {
                page.Blur(pane, 1, page.Rectangle(1));
                page.Art(pane, 2, page.Rectangle(2), chrome.Assets, chrome.SpriteShader);
            }
            foreach (var node in page.Nodes.Where(n => n.Type == 2))
                fields[(index, node.Index)] = page.Text(pane, node.Index, default, "", chrome.Font);
            if (index == 0)
            {
                int[] mounts = [1, 7, 14, 29, 41, 47, 59];
                int[] captions = [0x0051, 0x0052, 0x0452, 0x023D, 0x0053, 0x0234, 0x022B];
                for (int section = 0; section < mounts.Length; section++)
                    Command(pane, page.Rectangle(mounts[section]), section, Message(captions[section]));
                foreach (int node in new[] { 31, 33, 35, 37, 39 })
                {
                    var slot = new RegisteredOfficerTraitMarker { Position = page.Rectangle(node).Position, Size = page.Rectangle(node).Size,
                        InputAdmission = CanEdit };
                    pane.AddChild(slot); commands.Add(slot); commandSections[slot] = 3; traitFields[node] = slot;
                    slot.Disabled = !IsSectionEnabled(3);
                    slot.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", () => { if (CanEdit() && IsSectionEnabled(3)) edit?.Invoke(3); });
                }
            }
            else if (index == 1)
            {
                int[] mounts = [1, 45, 58, 68]; int[] captions = [0x0054, 0x0055, 0x02BC, 0x02BD];
                for (int section = 0; section < mounts.Length; section++)
                    Command(pane, page.Rectangle(mounts[section]), section + 7, Message(captions[section]));
            }
            else
            {
                var descriptionBox = page.Rectangle(5);
                var hit = new Button { Name = "EditRegisteredOfficerDescription", Position = descriptionBox.Position,
                    Size = descriptionBox.Size, Flat = true, Text = "", MouseFilter = MouseFilterEnum.Stop };
                pane.AddChild(hit); hit.Pressed += () => { if (CanEdit() && IsSectionEnabled(11)) edit?.Invoke(11); };
            }
        }
        // The normal editor uses the common full portrait on the left, independently
        // of the three mounted pages. Its caller supplies the resolved current asset.
        portraitHost = new Control { Name = "RegisteredOfficerPortrait", Position = Vector2.Zero,
            Size = background.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        backdrop.AddChild(portraitHost); portraitRectangle = background.Rectangle(7);
        var portraitHit = new Button { Name = "EditRegisteredOfficerPortrait", Position = portraitRectangle.Position,
            Size = portraitRectangle.Size, Flat = true, Text = "" };
        portraitHost.AddChild(portraitHit); portraitHit.Disabled = !IsSectionEnabled(12);
        portraitHit.Pressed += () => { if (CanEdit() && IsSectionEnabled(12)) edit?.Invoke(12); };
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();

        void Command(Control pane, Rect2 box, int section, string caption)
        {
            var button = new RegisteredOfficerSectionButton { Name = "RegisteredOfficerSection" + section,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            pane.AddChild(button); commands.Add(button); commandSections[button] = section;
            button.Disabled = !IsSectionEnabled(section);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption,
                () => { if (CanEdit() && IsSectionEnabled(section)) edit?.Invoke(section); });
        }
    }

    private bool IsSectionEnabled(int section) => options?.EditableSections.Contains(section) ?? true;
    internal void ConfigureRestoreOriginal(Action requested)
    {
        restoreOriginal = requested;
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        var button = new UiSourceBottomButton { Name = "RestoreHistoricalOfficerOriginal",
            Position = footer.Rectangle(17, new(0, 904)).Position, InputAdmission = CanEdit };
        restoreOriginalButton = button; chrome.Canvas.AddChild(button);
        string caption = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1]
            .Records[0x006B].Text ?? throw new InvalidDataException("Missing historical officer restore caption.");
        button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption,
            () => { if (CanEdit()) restoreOriginal?.Invoke(); });
        button.Disabled = busy;
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var value = read();
        foreach (var ((tab, node), field) in fields)
            field.SetKnownColourLine(tab < value.Text.Count ? value.Text[tab].GetValueOrDefault(node) ?? "" : "");
        foreach (var (node, field) in traitFields)
            field.Caption = value.Text.Count > 0 ? value.Text[0].GetValueOrDefault(node) ?? "" : "";
        for (int index = 0; index < panes.Count; index++)
        {
            bool visible = index != 2 || options?.BiographyVisible != false;
            panes[index].Visible = visible && selectedTab == index;
            tabs[index].Visible = visible; tabs[index].Selected = selectedTab == index;
        }
        if (!ReferenceEquals(value.Portrait, currentPortrait))
        {
            if (portrait is not null) { portraitHost.RemoveChild(portrait); portrait.QueueFree(); portrait = null; }
            currentPortrait = value.Portrait;
            if (currentPortrait is { } sprite)
            {
                portrait = new UiSourceStandingPortrait { Position = portraitRectangle.Position };
                portraitHost.AddChild(portrait); portrait.MouseFilter = MouseFilterEnum.Ignore;
                portrait.Configure(repository, chrome.Assets, chrome.SpriteShader, sprite);
            }
        }
        chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyDefaultHint(value.Notice);
    }
    internal void SetBusy(bool value)
    {
        if (abandoned) return; busy = value; chrome.ApplyReturn(!value);
        foreach (var command in commands) command.Disabled = value || !IsSectionEnabled(commandSections[command]);
        foreach (var tab in tabs) tab.Disabled = value;
        if (restoreOriginalButton is { } restore) restore.Disabled = value;
        Refresh();
    }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; read = null; admitted = null; returned = null; accepted = null; edit = null;
        restoreOriginal = null;
        if (restoreOriginalButton is { } restore) { restore.Disabled = true; restore.InputAdmission = null; }
        foreach (var command in commands) { command.Disabled = true; command.InputAdmission = null; }
        foreach (var tab in tabs) tab.Disabled = true;
        chrome?.Abandon(); Hide();
    }

    internal static RegisteredOfficerEditorValues CreateValues(GameResourceRepository repository, PreviewText text,
        RegisteredOfficer officer, RegisteredOfficerEditorCatalog catalog, IReadOnlyList<LoadedSprite>? portrait = null)
    {
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing registered officer value caption.");
        static string Named(IReadOnlyList<RegisteredOfficerEditorChoice> values, int id) =>
            id == 0 ? "" : values.FirstOrDefault(value => value.Id == id)?.Name ?? "";
        var basic = new Dictionary<int, string>
        {
            [3] = officer.Name, [4] = officer.CourtesyName.Length == 0 ? "" : "(" + officer.CourtesyName + ")",
            [5] = Message(0x4E0), [6] = officer.CourtesyName,
            [9] = Named(catalog.Sexes, officer.Sex), [10] = Message(0x4E5), [11] = Named(catalog.Voices, officer.VoiceType),
            [43] = Named(catalog.Doctrines, officer.DoctrineId), [44] = Named(catalog.Policies, officer.PolicyId),
            [45] = Message(0x250), [46] = officer.PolicyBaseCost.ToString()
        };
        // Sex zero is a real candidate identifier, rather than the absent-relationship sentinel.
        basic[9] = catalog.Sexes.FirstOrDefault(value => value.Id == officer.Sex)?.Name ?? "";
        int[] labels = [19, 21, 23, 25, 27], numbers = [20, 22, 24, 26, 28];
        byte[] abilities = [officer.Leadership, officer.Strength, officer.Intelligence, officer.Politics, officer.Charm];
        for (int index = 0; index < 5; index++)
        { basic[labels[index]] = Message(0x238 + index); basic[numbers[index]] = abilities[index].ToString(); }
        int[] traits = [31, 33, 35, 37, 39];
        for (int index = 0; index < traits.Length; index++) basic[traits[index]] = Named(catalog.Personalities, officer.TraitIds[index]);
        for (int index = 0; index < 10; index++)
        {
            basic[49 + index] = Named(catalog.Tactics, officer.TacticIds[index]);
            basic[62 + index * 2] = index < officer.FormationIds.Length ? Named(catalog.Formations, officer.FormationIds[index]) : "";
        }
        var relations = new Dictionary<int, string>
        {
            [3] = Message(0x55F), [4] = officer.BirthYear + "～" + officer.DeathYear,
            [5] = Message(0x4C6), [6] = (officer.DeathYear - officer.BirthYear + 1).ToString(),
            [47] = Message(0x42C), [48] = Named(catalog.Officers, officer.FatherId),
            [49] = Message(0x42D), [50] = Named(catalog.Officers, officer.MotherId),
            [51] = Message(0x42E), [52] = Named(catalog.Officers, officer.SpouseId),
            [53] = Message(0x2BF), [54] = Named(catalog.Officers, officer.SwornBrotherId),
            [56] = Message(0x4EA), [57] = Named(catalog.Officers, officer.CompatibilityPersonId)
        };
        for (int index = 0; index < 8; index++)
        { relations[60 + index] = Named(catalog.Officers, officer.LikePersonIds[index]); relations[70 + index] = Named(catalog.Officers, officer.DislikePersonIds[index]); }
        return new(portrait, [basic, relations, new Dictionary<int, string> { [5] = officer.Description }],
            officer.FamilyName.Length != 0 && officer.GivenName.Length != 0, "");
    }
}

/// <summary>CPersonEditButtonBehavior uses the title editor's own 480x64
/// section button, with caption node15 and text presets3..7.</summary>
internal partial class RegisteredOfficerSectionButton : UiSourceButton
{
    private Rect2 hit;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, Action released)
    {
        var source = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadSectionButton(repository))
            .ResizedSubtree(0, Size, true);
        var box = source.Rectangle(1);
        hit = new(new((int)box.Position.X, (int)box.Position.Y), new((int)box.Size.X, (int)box.Size.Y));
        ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        var caption = source.Text(this, 15, default, text, font);
        var profile = MapFontProfile.FromNode(source.Nodes[15], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        { caption.Configure(font, profile.WithPreset(presets[3 + state])); caption.QueueRedraw(); });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) released(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
}

/// <summary>The five trait mounts are CCmnMrker controls, not ordinary
/// command buttons. Their authored 158x42 rectangle is retained.</summary>
internal partial class RegisteredOfficerTraitMarker : UiSourceButton
{
    private Rect2 hit;
    private UiSourceText caption = null!;
    internal string Caption { get => caption.Text; set => caption.Text = value; }
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, Action released)
    {
        var source = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadTraitMarker(repository))
            .ResizedSubtree(0, Size, true);
        var box = source.Rectangle(1); var centre = box.GetCenter();
        hit = new(new((int)centre.X - (int)(box.Size.X * .5f), (int)centre.Y - (int)(box.Size.Y * .5f)),
            new((int)box.Size.X, (int)box.Size.Y));
        source.Art(this, 2, source.Rectangle(2), assets, shader);
        caption = source.Text(this, 3, default, text, font, MapTextPresets.Create()[0]);
        // CCmnMrker hides its separate focus root after construction.
        FocusMode = FocusModeEnum.None; IgnoreTextureSize = true; ActionMode = ActionModeEnum.Release;
        Pressed += () => { if (!Disabled && SourceInputAdmitted) released(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
}
