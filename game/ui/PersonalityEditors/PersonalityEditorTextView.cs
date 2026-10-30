using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original name input returns at most two UTF16 units. The
/// parent's working personality changes only after this child's confirmation.</summary>
internal partial class PersonalityEditorTextView : Control
{
    private AdministrationPageChrome chrome = null!;
    private LineEdit input = null!;
    private Func<bool>? admitted;
    private Action<string?>? closed;
    private bool abandoned;
    internal string Working => input.Text;
    internal void Configure(GameResourceRepository repository, PreviewText text, string original,
        Func<bool> admitted, Action<string?> closed)
    {
        this.admitted = admitted; this.closed = closed;
        Name = "PersonalityNameEditor"; ZIndex = 8; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x7F5, RequestBack, Accept);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_11_05_edit_name_ot"));
        var panel = new Control { Name = "OriginalPersonalityNamePanel", Position = new(598, 475),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(panel);
        foreach (var node in source.Nodes.Where(node => node.Type is 1 or 3))
            source.Art(panel, node.Index, source.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        var caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[0x5A8].Text
            ?? throw new InvalidDataException("Missing new-personality name caption.");
        source.Text(panel, 7, default, caption, chrome.Font);
        var box = source.Rectangle(8);
        input = new LineEdit { Name = "PersonalityNameInput", Position = box.Position, Size = box.Size, Text = original, MaxLength = 2 };
        panel.AddChild(input); input.TextChanged += _ => Refresh();
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private bool Valid => input.Text.Length is >= 1 and <= 2 && !input.Text.Contains('\0');
    private void Refresh() { if (!abandoned) chrome.ApplyDecision(Valid, true); }
    private void Accept() { if (CanEdit() && Valid) Finish(input.Text); }
    private void Finish(string? result) { var callback = closed; Abandon(); callback?.Invoke(result); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; closed = null; if (input is not null) input.Editable = false;
        chrome?.Abandon(); Hide();
    }
}
