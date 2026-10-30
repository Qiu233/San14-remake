using Godot;
using San14.Data;

internal sealed record EventEditorChoiceRow(int Id, string Caption, bool Enabled = true, string Notice = "");

/// <summary>The original condition/result selector. Its identifiers belong to
/// the supplied typed catalog; the UI never turns a numeric opcode into input.</summary>
internal partial class EventEditorChoiceView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private readonly List<(EventEditorChoiceRow? Value, EventEditorRowButton Button)> rows = [];
    private IReadOnlyList<EventEditorChoiceRow> choices = [];
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private int? selected;
    private bool abandoned, busy;
    private float rowStep, firstY, clipHeight;

    internal void Configure(GameResourceRepository repository, PreviewText text, string heading,
        IReadOnlyList<EventEditorChoiceRow> choices, int? selected, Func<bool> admitted,
        Action<int?> closed, Func<int> scrollSpeed)
    {
        this.admitted = admitted; this.closed = closed; this.choices = choices;
        this.selected = choices.Any(choice => choice.Id == selected && choice.Enabled) ? selected : null;
        Name = "EventEditorChoice"; ZIndex = 9; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x91, RequestBack, Accept);
        chrome.ApplyTitle(heading);
        var source = new UiSourceLayout(repository, 0x4147CF2E);
        var body = new Control { Name = "EventChoiceBody", Position = new(0, 122),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(body);
        source.Art(body, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        source.Blur(body, 4, source.Rectangle(4)); source.Blur(body, 5, source.Rectangle(5));
        foreach (int node in new[] { 6, 8 }) source.Art(body, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var box = source.Rectangle(16);
        var clip = new Control { Name = "EventChoiceRows", Position = box.Position, Size = box.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        body.AddChild(clip); clipHeight = box.Size.Y;
        firstY = source.Rectangle(17).Position.Y - box.Position.Y;
        rowStep = source.Rectangle(18).Position.Y - source.Rectangle(17).Position.Y;
        var scrollBox = source.Rectangle(9);
        scroll = new() { Name = "EventChoiceScroll", Position = scrollBox.Position, Size = scrollBox.Size };
        body.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader,
            Math.Max(0, (int)(firstY + Math.Max(0, choices.Count - 1) * rowStep + source.Rectangle(17).Size.Y - clipHeight)),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        clip.GuiInput += input => { if (CanEdit()) scroll.HandleWheel(input); };
        if (rowStep <= 0) throw new InvalidDataException("Event selector row geometry has no positive stride.");
        // The title person directory can contain thousands of choices. Only
        // rows intersecting this authored viewport need controls and artwork.
        int poolSize = Math.Min(choices.Count,
            (int)Math.Ceiling((clipHeight + source.Rectangle(17).Size.Y) / rowStep) + 1);
        for (int index = 0; index < poolSize; index++)
        {
            int slot = index;
            var button = new EventEditorRowButton { Name = "EventChoicePool" + slot,
                Size = source.Rectangle(17).Size, InputAdmission = CanEdit };
            clip.AddChild(button); rows.Add((null, button));
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "",
                () =>
                {
                    if (CanEdit() && rows[slot].Value is { Enabled: true } value)
                    { this.selected = value.Id; Refresh(); }
                });
            button.MouseEntered += () =>
            { if (!abandoned) chrome.ApplyHoverHint(rows[slot].Value?.Notice); };
            button.MouseExited += () => { if (!abandoned) chrome.ApplyHoverHint(null); };
            button.GuiInput += input => { if (CanEdit()) scroll.HandleWheel(input); };
        }
        if (this.selected is int initial)
        {
            int index = 0;
            while (index < choices.Count && choices[index].Id != initial) index++;
            float top = firstY + index * rowStep;
            if (top + source.Rectangle(17).Size.Y > clipHeight)
                scroll.SetValue((int)Math.Ceiling(top + source.Rectangle(17).Size.Y - clipHeight));
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri); PositionRows(); Refresh();
    }

    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private void PositionRows()
    {
        int first = Math.Max(0, (int)Math.Floor((scroll.Value - firstY) / rowStep));
        for (int slot = 0; slot < rows.Count; slot++) rows[slot].Button.Name = "EventChoicePool" + slot;
        for (int slot = 0; slot < rows.Count; slot++)
        {
            int index = first + slot;
            var button = rows[slot].Button;
            var value = index < choices.Count ? choices[index] : null;
            rows[slot] = (value, button);
            button.Position = new(0, firstY + index * rowStep - scroll.Value);
            button.Visible = value is not null && button.Position.Y > -button.Size.Y && button.Position.Y < clipHeight;
            button.Name = value is null ? "EventChoicePool" + slot : "EventChoice" + value.Id;
            button.SetMeta("choice_index", value is null ? -1 : index);
            button.Caption = value?.Caption ?? "";
            button.Disabled = busy || value is not { Enabled: true };
            button.Selected = value is not null && value.Id == selected;
        }
    }
    private void Refresh()
    {
        foreach (var (value, button) in rows)
        { button.Disabled = busy || value is not { Enabled: true }; button.Selected = value is not null && value.Id == selected; }
        scroll.ProcessMode = busy ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        chrome.ApplyReturn(!busy);
        chrome.ApplyDecision(!busy && selected.HasValue, true);
    }
    private void Accept() { if (CanEdit() && selected.HasValue) Finish(selected); }
    private void Finish(int? value) { var callback = closed; Abandon(); callback?.Invoke(value); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; closed = null; choices = [];
        foreach (var (_, button) in rows) button.Abandon();
        if (scroll is not null) { scroll.SetProcessInput(false); scroll.ProcessMode = ProcessModeEnum.Disabled; }
        chrome?.Abandon(); Hide();
    }
}
