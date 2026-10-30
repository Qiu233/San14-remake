using Godot;
using San14.Data;

/// <summary>The summary's original inline input rectangle (62FB70), retained
/// over the still-visible summary page. The data owner supplies its text gate.</summary>
internal partial class EventEditorTextView : Control
{
    private AdministrationPageChrome chrome = null!;
    private LineEdit? nameInput;
    private TextEdit? descriptionInput;
    private Func<string, bool>? valid;
    private Func<bool>? admitted;
    private Action<string?>? closed;
    private bool abandoned, busy;
    internal string Working => descriptionInput?.Text ?? nameInput?.Text ?? "";
    internal void Configure(GameResourceRepository repository, PreviewText text, bool description,
        string original, Func<string, bool> valid, Func<bool> admitted, Action<string?> closed)
    {
        this.valid = valid; this.admitted = admitted; this.closed = closed;
        Name = description ? "EventDescriptionInput" : "EventNameInput";
        ZIndex = 9; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x91, RequestBack, Accept);
        var source = new UiSourceLayout(repository, 0xCE8C8367);
        var box = source.Rectangle(description ? 8 : 6, new(0, 122));
        box.Position += new Vector2(12, 8); box.Size -= new Vector2(24, 16);
        if (description)
        {
            descriptionInput = new TextEdit { Name = "EventDescriptionText", Position = box.Position,
                Size = box.Size, Text = original, WrapMode = TextEdit.LineWrappingMode.Boundary };
            chrome.Canvas.AddChild(descriptionInput); descriptionInput.TextChanged += Refresh;
        }
        else
        {
            nameInput = new LineEdit { Name = "EventNameText", Position = box.Position,
                Size = box.Size, Text = original, MaxLength = 0 };
            chrome.Canvas.AddChild(nameInput); nameInput.TextChanged += _ => Refresh();
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private void Refresh()
    {
        if (abandoned) return;
        chrome.ApplyReturn(!busy); chrome.ApplyDecision(!busy && valid?.Invoke(Working) == true, true);
    }
    private void Accept() { if (CanEdit() && valid?.Invoke(Working) == true) Finish(Working); }
    private void Finish(string? value) { var callback = closed; Abandon(); callback?.Invoke(value); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; if (nameInput is not null) nameInput.Editable = !value;
        if (descriptionInput is not null) descriptionInput.Editable = !value; Refresh();
    }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; valid = null; admitted = null; closed = null;
        if (nameInput is not null) nameInput.Editable = false;
        if (descriptionInput is not null) descriptionInput.Editable = false;
        chrome?.Abandon(); Hide();
    }
}
