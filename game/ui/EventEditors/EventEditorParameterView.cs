using Godot;
using San14.Data;

internal sealed record EventEditorParameterRow(int Id, string Caption, string Value, bool Enabled,
    bool CanPrevious, bool CanNext, string Notice = "");
internal sealed record EventEditorParameterValues(IReadOnlyList<EventEditorParameterRow> Rows, bool CanConfirm, string Notice);

/// <summary>The source's one-to-five parameter dialogs. Selector and step
/// requests are typed application callbacks; cancellation never writes a draft.</summary>
internal partial class EventEditorParameterView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<EventEditorParameterControl> rows = [];
    private Func<EventEditorParameterValues>? read;
    private Func<bool>? admitted;
    private Action? returned, accepted;
    private Action<int>? choose;
    private Action<int, int>? step;
    private bool busy, abandoned;
    internal void Configure(GameResourceRepository repository, PreviewText text, string heading, int parameterCount,
        Func<EventEditorParameterValues> read, Func<bool> admitted, Action returned, Action accepted,
        Action<int> choose, Action<int, int> step, bool alternateFour = false)
    {
        if (parameterCount is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(parameterCount));
        if (alternateFour && parameterCount != 4) throw new ArgumentException("The alternate source panel has four rows.", nameof(parameterCount));
        this.read = read; this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        this.choose = choose; this.step = step;
        Name = "EventEditorParameters"; ZIndex = 10; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x91, RequestBack, Accept);
        chrome.ApplyTitle(heading);
        uint[] layouts = [0xD82838FB, 0xDF8D6E1A, 0xE6F2A339, 0xEE57D858, 0xF5BD0D77];
        var source = new UiSourceLayout(repository, alternateFour ? 0x449D3A85u : layouts[parameterCount - 1]);
        var body = new Control { Name = "EventParametersBody", Position = new(0, 122),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
        source.Art(body, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        source.Blur(body, 4, source.Rectangle(4)); source.Blur(body, 5, source.Rectangle(5));
        source.Art(body, 6, source.Rectangle(6), chrome.Assets, chrome.SpriteShader);
        for (int index = 0; index < parameterCount; index++)
        {
            int rowIndex = index; var box = source.Rectangle(7 + index);
            var row = new EventEditorParameterControl { Name = "EventParameter" + index, Position = box.Position, Size = box.Size };
            body.AddChild(row); rows.Add(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, CanEdit,
                () => { if (CanEdit() && this.read is { } current) { var value = current().Rows[rowIndex]; if (value.Enabled) this.choose?.Invoke(value.Id); } },
                direction => { if (CanEdit() && this.read is { } current) { var value = current().Rows[rowIndex];
                    if (value.Enabled && (direction < 0 ? value.CanPrevious : value.CanNext)) this.step?.Invoke(value.Id, direction); } });
        }
        // The final authored paragraph is a literal parameter explanation,
        // independently of the footer's current modal notice.
        if (!alternateFour)
            source.Art(body, 7 + parameterCount, source.Rectangle(7 + parameterCount), chrome.Assets, chrome.SpriteShader);
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned || read is null) return;
        var value = read();
        if (value.Rows.Count != rows.Count) throw new InvalidDataException("Event parameter rows do not match their authored dialog.");
        for (int index = 0; index < rows.Count; index++) rows[index].Apply(value.Rows[index], busy);
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyDefaultHint(value.Notice);
    }
    private void Accept() { if (CanEdit() && read?.Invoke().CanConfirm == true) accepted?.Invoke(); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; read = null; admitted = null; returned = accepted = null; choose = null; step = null;
        foreach (var row in rows) row.Abandon(); chrome?.Abandon(); Hide();
    }
}

/// <summary>Normal input-condition row D2CE0B5D, using actual behavior 5 for
/// the small selector and behavior 24 for the two function-arrow controls.</summary>
internal partial class EventEditorParameterControl : Control
{
    private UiSourceText caption = null!;
    private EventEditorSmallButton valueButton = null!;
    private EventEditorFunctionButton previous = null!, next = null!;
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        Func<bool> admitted, Action choose, Action<int> step)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var source = new UiSourceLayout(repository, 0xD2CE0B5D);
        source.Art(this, 2, source.Rectangle(2), assets, shader);
        caption = source.Text(this, 3, default, "", font);
        var valueBox = source.Rectangle(8);
        valueButton = new() { Name = "ChooseEventParameter", Position = valueBox.Position, Size = valueBox.Size, InputAdmission = admitted };
        AddChild(valueButton); valueButton.Configure(repository, assets, shader, font, choose);
        previous = Arrow(4, 6, "PreviousEventParameter", () => step(-1));
        next = Arrow(10, 12, "NextEventParameter", () => step(1));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        EventEditorFunctionButton Arrow(int mount, int glyph, string name, Action action)
        {
            var box = source.Rectangle(mount);
            var button = new EventEditorFunctionButton { Name = name, Position = box.Position, Size = box.Size, InputAdmission = admitted };
            AddChild(button); button.Configure(repository, assets, shader, source, glyph, box.Position, action); return button;
        }
    }
    internal void Apply(EventEditorParameterRow value, bool busy)
    {
        caption.Text = value.Caption; valueButton.Caption = value.Value;
        valueButton.Disabled = busy || !value.Enabled;
        previous.Disabled = busy || !value.Enabled || !value.CanPrevious;
        next.Disabled = busy || !value.Enabled || !value.CanNext;
        SetMeta("selector_available", value.Enabled);
    }
    internal void Abandon() { valueButton?.Abandon(); previous?.Abandon(); next?.Abandon(); }
}

internal partial class EventEditorSmallButton : UiSourceButton
{
    private Rect2 hit;
    private UiSourceText caption = null!;
    private Action? released;
    internal string Caption { get => caption.Text; set => caption.Text = value; }
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_btn_hanyou_s")).ResizedSubtree(0, Size, true);
        hit = source.Rectangle(1); ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }.Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        caption = source.Text(this, 16, default, "", font);
        var profile = MapFontProfile.FromNode(source.Nodes[16], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state => { caption.Configure(font, profile.WithPreset(presets[28 + state])); caption.QueueRedraw(); });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    internal void Abandon() { released = null; Disabled = true; InputAdmission = null; }
}

internal partial class EventEditorFunctionButton : UiSourceButton
{
    private Rect2 hit;
    private Action? released;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        UiSourceLayout row, int glyph, Vector2 rowMount, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "cmn_btn_fnc")).ResizedSubtree(0, Size, true);
        hit = source.Rectangle(1); ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }.Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        // 5E80D0 replaces the option-wheel placeholder with the actual
        // Common sprites 1E5/1E4. Their names were extracted offline once.
        source.Art(this, 15, source.Rectangle(15), assets, shader,
            commonSpriteOverride: glyph == 6 ? "000_cmn_icon_sld_l" : "000_cmn_icon_sl_r");
        ConfigureStates(states);
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    internal void Abandon() { released = null; Disabled = true; InputAdmission = null; }
}
