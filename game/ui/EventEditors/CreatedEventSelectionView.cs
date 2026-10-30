using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CreatedEventSelectionRow(int Slot, string Name, string Description,
    bool CanExecute, string Notice);

/// <summary>The ordinary mode-six edited-event selector. Its owner decides
/// whether an actual selected event can execute and when this view retires.</summary>
internal partial class CreatedEventSelectionView : Control
{
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!, rowClip = null!;
    private UiSourceHeader header = null!;
    private UiSourceRoundButton back = null!;
    private CreatedEventExecuteButton execute = null!;
    private UiSourceScrollbar? scroll;
    private UiSourceParagraph description = null!;
    private UiSourceText hint = null!;
    private readonly List<CreatedEventRowButton> buttons = [];
    private IReadOnlyList<CreatedEventSelectionRow> rows = [];
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private Func<int> scrollSpeed = null!;
    private Rect2 scrollBox;
    private int? selected;
    private bool busy, abandoned;
    private string notice = "";
    private const int RowPitch = 121;
    internal int? SelectedSlot => selected;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<CreatedEventSelectionRow> rows, Func<bool> admitted,
        Action<int?> closed, Func<int> scrollSpeed)
    {
        this.repository = repository; this.admitted = admitted;
        this.closed = closed; this.scrollSpeed = scrollSpeed;
        Name = "CreatedEventSelection"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale);
        shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "09_01_01_event_list_pk_cn"));
        header = new() { Name = "CreatedEventHeader" }; canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, repository.LoadMessages(text.SourceLocale,
            MessageKind.Data).Messages.Sections[1].Records[0x2EB].Text
                ?? throw new InvalidDataException("created_events.header.Data1.2EB"));
        foreach (int node in new[] { 3, 61, 63, 64, 66, 68, 78, 105 })
            if (source.Nodes[node].Type == 6) source.Blur(canvas, node, source.Rectangle(node));
            else source.Art(canvas, node, source.Rectangle(node), assets, shader);
        var area = source.Rectangle(39);
        rowClip = new() { Name = "CreatedEventRows", Position = area.Position,
            Size = area.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(rowClip);
        rowClip.GuiInput += input => { if (CanInteract()) scroll?.HandleWheel(input); };
        scrollBox = source.Rectangle(32);
        var summaryBox = source.Rectangle(67);
        description = new() { Name = "CreatedEventDescription", Position = summaryBox.Position, Size = summaryBox.Size };
        canvas.AddChild(description);
        // The actual text node already has align 0 / line arrange 1 /
        // width-fit 3 / literal character mode 2. No guessed paragraph mode.
        description.Configure(font, MapFontProfile.FromNode(source.Nodes[67], source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[0]));
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        back = new() { Name = "ReturnFromCreatedEvents", Position = footer.Rectangle(4, new(0, 904)).Position,
            InputAdmission = CanInteract };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale, "000_cmn_txt_decision_modoru", RequestBack, UiAudioCue.Cancel);
        execute = new() { Name = "ExecuteCreatedEvent", Position = footer.Rectangle(5, new(0, 904)).Position,
            InputAdmission = CanInteract };
        canvas.AddChild(execute); execute.Configure(repository, assets, shader, text.SourceLocale, Confirm);
        var help = new UiSourceLayout(repository, 0xAB6ADAA7);
        Vector2 helpMount = footer.Rectangle(19, new(0, 904)).Position;
        foreach (var node in help.Nodes.Where(node => node.Type is 1 or 3))
            help.Art(canvas, node.Index, help.Rectangle(node.Index, helpMount), assets, shader);
        hint = help.Text(canvas, help.Nodes.Single(node => node.Type == 2).Index, helpMount, "", font);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_mode", 6);
        // Conditions require the actual prepared Data lines. The source's
        // condition mounts are not filled with synthetic success statements.
        SetMeta("condition_publication", "created_events.condition_lines.not_supplied");
        Refresh(rows); Resized += Fit; Fit();
    }

    internal void Refresh(IReadOnlyList<CreatedEventSelectionRow> publication)
    {
        if (abandoned) return;
        ArgumentNullException.ThrowIfNull(publication);
        if (publication.Select(row => row.Slot).Distinct().Count() != publication.Count)
            throw new InvalidDataException("created_events.duplicate_slot");
        rows = publication;
        if (!rows.Any(row => row.Slot == selected)) selected = null;
        foreach (var button in buttons)
        { button.Abandon(); rowClip.RemoveChild(button); button.QueueFree(); }
        buttons.Clear();
        if (scroll is not null) { canvas.RemoveChild(scroll); scroll.QueueFree(); }
        scroll = new() { Name = "CreatedEventScroll", Position = scrollBox.Position, Size = scrollBox.Size };
        canvas.AddChild(scroll);
        scroll.Configure(repository, assets, shader,
            Math.Max(0, Math.Max(0, publication.Count - 1) * RowPitch +
                (publication.Count == 0 ? 0 : 116) - (int)rowClip.Size.Y), scrollSpeed(),
            _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        foreach (var row in publication)
        {
            var current = row;
            var button = new CreatedEventRowButton { Name = "CreatedEvent" + row.Slot, InputAdmission = CanInteract };
            rowClip.AddChild(button); buttons.Add(button);
            button.Configure(repository, assets, shader, font, row.Slot, row.Name, () =>
            {
                if (!CanInteract()) return;
                selected = current.Slot; notice = ""; UpdateSelection();
            });
            button.GuiInput += input => { if (CanInteract()) scroll.HandleWheel(input); };
            button.MouseEntered += () => { if (!abandoned) hint.Text = current.Notice; };
            button.MouseExited += () => { if (!abandoned) UpdateHint(); };
        }
        PositionRows(); UpdateSelection();
    }

    private bool CanInteract() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private void PositionRows()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i]; button.Position = new(0, i * RowPitch - (scroll?.Value ?? 0));
            button.Visible = button.Position.Y > -RowPitch && button.Position.Y < rowClip.Size.Y;
        }
    }
    private void UpdateSelection()
    {
        for (int i = 0; i < buttons.Count; i++)
        { buttons[i].Selected = rows[i].Slot == selected; buttons[i].Disabled = busy; }
        var value = rows.FirstOrDefault(row => row.Slot == selected);
        description.Apply(value?.Description ?? "");
        execute.Disabled = busy || value?.CanExecute != true;
        back.Disabled = busy;
        if (scroll is not null) scroll.ProcessMode = busy ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        UpdateHint();
    }
    private void UpdateHint() => hint.Text = notice.Length != 0 ? notice :
        rows.FirstOrDefault(row => row.Slot == selected)?.Notice ?? "";
    private void Confirm()
    {
        if (CanInteract() && selected is int slot && rows.Any(row => row.Slot == slot && row.CanExecute))
            closed?.Invoke(slot);
    }
    internal void RequestBack() { if (CanInteract()) closed?.Invoke(null); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; UpdateSelection(); } }
    internal void ApplyNotice(string? value) { if (!abandoned) { notice = value ?? ""; UpdateHint(); } }
    internal void RegisterHeader(UiSourceHeaderStack stack) => header.Publish(stack);
    internal void Abandon()
    {
        if (abandoned) return; abandoned = true; admitted = null; closed = null;
        foreach (var button in buttons) button.Abandon();
        if (back is not null) { back.InputAdmission = null; back.Disabled = true; }
        if (execute is not null) { execute.InputAdmission = null; execute.Disabled = true; }
        if (scroll is not null) { scroll.SetProcessInput(false); scroll.ProcessMode = ProcessModeEnum.Disabled; }
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

internal partial class CreatedEventRowButton : UiSourceButton
{
    private Action? released;
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, int slot, string name, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "09_01_01_event_list_offset_00_cn"));
        Size = source.Rectangle(0).Size; ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 7, 10, 12, 14 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        source.Text(this, 25, default, slot.ToString(System.Globalization.CultureInfo.InvariantCulture), font);
        source.Text(this, 26, default, name, font);
        ConfigureStates(states);
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    internal void Abandon() { released = null; InputAdmission = null; Disabled = true; }
}

internal partial class CreatedEventExecuteButton : UiSourceButton
{
    private Action? released;
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(repository, 0xAD4A3EFA);
        Size = source.Rectangle(0).Size; ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 12, 14 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        string language = locale switch { "TC" => "tw", "SC" => "ch", _ => throw new ArgumentOutOfRangeException(nameof(locale)) };
        // Footer style 9 -> sprite enum 2E7, normal language texture pack.
        var declared = new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(
            "texture_lang_" + language, language + "_000_cmn_txt_decision_jikkou");
        var texture = assets.SrgbSprite(declared.Definition.Name, declared);
        var box = source.Rectangle(16);
        var caption = new UiSourceSprite { Name = "Caption", Position = box.Position, Size = box.Size };
        AddChild(caption); caption.Configure(shader, texture, UiSourceSprite.Blend.Ordinary);
        var palettes = MapSpritePalettes.Create();
        ConfigureStates(states, state => caption.SetSource(texture, palettes[25 + state]));
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_style", 9); SetMeta("source_caption", 0x2E7);
    }
}
