using Godot;
using San14.Core;
using San14.Data;

/// <summary>The Data owner supplies the actual edited-text normalization and
/// storage/adoption permission. This value is not a Game-bank message.</summary>
internal sealed record EventEditorMessageEditState(EditedEventMessage Value, bool CanConfirm, string Notice);

internal partial class EventEditorMessageView : Control
{
    private AdministrationPageChrome chrome = null!;
    private TextEdit input = null!;
    private EditedEventMessage initial = null!;
    private EventEditorMessageEditState state = null!;
    private Func<string, EventEditorMessageEditState>? evaluate;
    private Func<bool>? admitted;
    private Action<EditedEventMessage?>? closed;
    private bool configured, abandoned, busy;
    internal EditedEventMessage Working => state.Value;

    internal void Configure(GameResourceRepository repository, PreviewText text, EditedEventMessage initial,
        bool creating, Func<string, EventEditorMessageEditState> evaluate, Func<bool> admitted,
        Action<EditedEventMessage?> closed)
    {
        if (configured) throw new InvalidOperationException("An event message editor is configured once.");
        initial.Validate();
        this.initial = initial; this.evaluate = evaluate; this.admitted = admitted; this.closed = closed;
        Name = "EventEditorMessage"; ZIndex = 8; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, creating ? 0x86 : 0x87, RequestBack, Accept);
        var loaded = MapMarkerLayout.LoadNamed(repository, "02_05_01_00_tab3_offset_00");
        var authored = new UiSourceLayout(loaded);
        //5EF290 subtracts(112,292) from each pane's source dimensions,
        //then centres it on(960,540) using integer half sizes.
        //Nodes3/4 are hidden by that caller; node5 is its actual text input.
        var geometry = new Dictionary<int, UiSourceNodeGeometry>();
        foreach (int node in new[] { 1, 2, 5 })
        {
            var size = authored.Rectangle(node).Size - new Vector2(112, 292);
            geometry[node] = new(Size: size, Origin: new(960 - (int)size.X / 2, 540 - (int)size.Y / 2));
        }
        var source = new UiSourceLayout(loaded, geometry);
        source.Blur(chrome.Canvas, 1, source.Rectangle(1));
        source.Art(chrome.Canvas, 2, source.Rectangle(2), chrome.Assets, chrome.SpriteShader);
        var box = source.Rectangle(5);
        input = new TextEdit { Name = "EventEditorMessageInput", Position = box.Position, Size = box.Size,
            Text = initial.Text, WrapMode = TextEdit.LineWrappingMode.Boundary };
        chrome.Canvas.AddChild(input);
        input.TextChanged += Refresh;
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("edited_message_slot", initial.Slot);
        configured = true; Refresh();
    }

    private bool CanEdit() => configured && !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private void Refresh()
    {
        if (!configured || abandoned || evaluate is null) return;
        state = evaluate(input.Text);
        if (state.Value.Slot != initial.Slot)
            throw new InvalidDataException("An article input cannot change its selected library slot.");
        input.Editable = !busy;
        chrome.ApplyDecision(!busy && state.CanConfirm, true);
        chrome.ApplyReturn(!busy);
        chrome.ApplyDefaultHint(state.Notice);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    private void Accept()
    {
        if (!CanEdit()) return;
        Refresh();
        if (state.CanConfirm) Finish(state.Value);
    }
    private void Finish(EditedEventMessage? value)
    { var callback = closed; Abandon(); callback?.Invoke(value); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; evaluate = null; admitted = null; closed = null;
        if (input is not null) { input.Editable = false; input.ReleaseFocus(); }
        chrome?.Abandon(); Hide();
    }
    public override void _ExitTree() => Abandon();
}
