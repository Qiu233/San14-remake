using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record EventEditorSummaryValues(string Name, string Description, byte? Category,
    bool Automatic, bool Repeat, bool CanConfirm, string Notice)
{
    public int? EventId { get; init; }
}

/// <summary>The original event summary page. Its application-owned draft and
/// text-entry children are read and changed only through the supplied ports.</summary>
internal partial class EventEditorSummaryView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceCommandButton nameButton = null!;
    private EventEditorDescriptionButton descriptionButton = null!;
    private readonly List<UiSourceRadioButton> categories = [];
    private UiSourceRadioButton manual = null!, automatic = null!, single = null!, repeat = null!;
    private UiSourceText eventId = null!;
    private Func<EventEditorSummaryValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted, editName, editDescription;
    private Action<byte>? categoryChanged;
    private Action<bool>? automaticChanged, repeatChanged;
    private string unknown = "", defaultHint = "";
    private bool busy, abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<EventEditorSummaryValues> read, Func<bool> admitted,
        Action returned, Action accepted, Action editName, Action editDescription,
        Action<byte> categoryChanged, Action<bool> automaticChanged, Action<bool> repeatChanged)
    {
        ArgumentNullException.ThrowIfNull(read); ArgumentNullException.ThrowIfNull(admitted);
        this.read = read; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        this.editName = editName; this.editDescription = editDescription;
        this.categoryChanged = categoryChanged; this.automaticChanged = automaticChanged; this.repeatChanged = repeatChanged;
        Name = "EventEditorSummary"; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x0091, RequestBack, Accept);
        var source = new UiSourceLayout(repository, 0xCE8C8367);
        // The source root is attached to the 1920x1080 parent centre at
        // (-960,+418), putting its local top-left at (0,122).
        var body = new Control { Name = "EventSummaryBody", Position = new(0, 122),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
        source.Art(body, 2, source.Rectangle(2), chrome.Assets, chrome.SpriteShader);
        source.Blur(body, 3, source.Rectangle(3)); source.Blur(body, 4, source.Rectangle(4));
        source.Art(body, 5, source.Rectangle(5), chrome.Assets, chrome.SpriteShader);
        foreach (int node in new[] { 10, 12, 14, 16, 18, 21, 22 })
            source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text
            ?? throw new InvalidDataException("Original event summary caption is missing.");
        unknown = text.Get("detail_unknown"); defaultHint = Message(0x2F6);
        foreach (var (node, caption) in new[] { (11, 0x2EC), (13, 0x400), (15, 0x401),
            (17, 0x2EE), (19, 0x2F0), (23, 0x498) })
            source.Text(body, node, default, Message(caption), chrome.Font);
        eventId = source.Text(body, 24, default, "", chrome.Font);
        eventId.Name = "EventSummaryId";
        var nameBox = source.Rectangle(6);
        nameButton = new() { Name = "EventSummaryName", Position = nameBox.Position,
            Size = nameBox.Size, InputAdmission = CanEdit };
        body.AddChild(nameButton);
        nameButton.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "",
            () => { if (CanEdit()) this.editName?.Invoke(); });
        var descriptionBox = source.Rectangle(8);
        descriptionButton = new() { Name = "EventSummaryDescription", Position = descriptionBox.Position,
            Size = descriptionBox.Size, InputAdmission = CanEdit };
        body.AddChild(descriptionButton);
        descriptionButton.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
            () => { if (CanEdit()) this.editDescription?.Invoke(); });
        int[] categoryMounts = [26, 29, 32];
        for (int index = 0; index < categoryMounts.Length; index++)
        {
            byte value = (byte)index;
            categories.Add(Radio(categoryMounts[index], "EventSummaryCategory" + index,
                0x49A + index, index == 0 ? UiSourceRadioSegment.Left :
                    index == 1 ? UiSourceRadioSegment.Centre : UiSourceRadioSegment.Right,
                () => { this.categoryChanged?.Invoke(value); Refresh(); }));
        }
        manual = Radio(36, "EventSummaryManual", 0x4A8, UiSourceRadioSegment.Left,
            () => { this.automaticChanged?.Invoke(false); Refresh(); });
        automatic = Radio(39, "EventSummaryAutomatic", 0x4A7, UiSourceRadioSegment.Right,
            () => { this.automaticChanged?.Invoke(true); Refresh(); });
        single = Radio(43, "EventSummarySingle", 0x4A9, UiSourceRadioSegment.Left,
            () => { this.repeatChanged?.Invoke(false); Refresh(); });
        repeat = Radio(46, "EventSummaryRepeat", 0x4AA, UiSourceRadioSegment.Right,
            () => { this.repeatChanged?.Invoke(true); Refresh(); });
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Refresh();

        UiSourceRadioButton Radio(int node, string name, int caption, UiSourceRadioSegment segment, Action changed)
        {
            var box = source.Rectangle(node);
            var button = new UiSourceRadioButton { Name = name, Position = box.Position,
                Size = box.Size, InputAdmission = CanEdit };
            body.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment, Message(caption),
                () => { if (CanEdit()) changed(); });
            return button;
        }
    }

    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;

    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var value = read();
        nameButton.Caption = value.Name; descriptionButton.Caption = value.Description;
        eventId.Text = value.EventId?.ToString(CultureInfo.InvariantCulture) ?? unknown;
        for (int index = 0; index < categories.Count; index++) categories[index].Selected = value.Category == index;
        manual.Selected = !value.Automatic; automatic.Selected = value.Automatic;
        single.Selected = !value.Repeat; repeat.Selected = value.Repeat;
        nameButton.Disabled = descriptionButton.Disabled = busy;
        foreach (var button in categories) button.Disabled = busy;
        manual.Disabled = automatic.Disabled = single.Disabled = repeat.Disabled = busy;
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && value.CanConfirm, true);
        chrome.ApplyDefaultHint(value.Notice.Length == 0 ? defaultHint : value.Notice);
    }

    private void Accept() { if (CanEdit() && read?.Invoke().CanConfirm == true) accepted?.Invoke(); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }

    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; read = null; admitted = null; returned = accepted = editName = editDescription = null;
        categoryChanged = null; automaticChanged = repeatChanged = null;
        nameButton?.Abandon(); descriptionButton?.Abandon();
        foreach (var button in categories) button.Abandon();
        manual?.Abandon(); automatic?.Abandon(); single?.Abandon(); repeat?.Abandon();
        chrome?.Abandon(); Hide();
    }
}

/// <summary>The source explanation switch button, with its own paragraph
/// resource and five settled artwork states. It has no text-input ownership.</summary>
internal partial class EventEditorDescriptionButton : UiSourceButton
{
    private Rect2 hit;
    private MapFont font = null!;
    private MapFontProfile profile = null!;
    private Control textHost = null!;
    private readonly List<UiSourceText> lines = [];
    private Action? released;
    private string current = "";
    private bool configured;

    internal string Caption
    {
        get => current;
        set { if (configured && value != current) { current = value; RebuildText(); } }
    }

    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font, Action released)
    {
        this.font = font; this.released = released;
        var source = new UiSourceLayout(repository, 0x2BBBD2B6).ResizedSubtree(0, Size, true);
        var rectangle = source.Rectangle(1); var centre = rectangle.GetCenter();
        hit = new(new((int)centre.X - (int)(rectangle.Size.X * .5f), (int)centre.Y - (int)(rectangle.Size.Y * .5f)),
            new((int)rectangle.Size.X, (int)rectangle.Size.Y));
        ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        var box = source.Rectangle(15);
        // 5EB310 extends the authored explanation text width by 28, while
        // preserving its actual paragraph height and the original font.
        textHost = new Control { Name = "EventSummaryDescriptionText", Position = box.Position,
            Size = new(box.Size.X + 28, box.Size.Y), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(textHost); profile = MapFontProfile.FromNode(source.Nodes[15], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        {
            foreach (var line in lines) { line.Configure(font, profile.WithPreset(presets[3 + state])); line.QueueRedraw(); }
        });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        configured = true; RebuildText();
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    private void RebuildText()
    {
        foreach (var line in lines) { textHost.RemoveChild(line); line.QueueFree(); }
        lines.Clear();
        float advance = font.Source.Record('\u3042', profile.Face).Height * profile.ScaleY + profile.VerticalSpace;
        int allowedLf = Math.Max(0, Math.Min(64, (int)(textHost.Size.Y / advance) - 1));
        string wrapped = DeploymentNarrationPlainParagraph.WrapKnownColourParagraph(current,
            line => (int)font.Measure(line, profile).Width, (int)textHost.Size.X, allowedLf);
        var paragraph = DeploymentNarrationParagraphPlan.Runs(wrapped);
        var styled = profile.WithPreset(MapTextPresets.Create()[3 + VisualState]);
        for (int index = 0; index < paragraph.Count; index++)
        {
            var line = new UiSourceText { Name = "EventDescriptionLine" + index,
                Position = new(0, index * advance), Size = new(textHost.Size.X, advance) };
            textHost.AddChild(line); lines.Add(line); line.Configure(font, styled);
            line.SetKnownColourLine(paragraph[index].VisibleText);
        }
    }

    internal void Abandon() { released = null; Disabled = true; InputAdmission = null; }
}
