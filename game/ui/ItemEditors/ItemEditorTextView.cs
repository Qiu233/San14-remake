using Godot;
using San14.Data;

internal partial class ItemEditorTextView : Control
{
    private AdministrationPageChrome chrome = null!;
    private Func<bool>? admitted;
    private Action<string?>? closed;
    private LineEdit? name;
    private TextEdit? description;
    private bool abandoned;
    internal void Configure(GameResourceRepository repository, PreviewText text, bool isDescription,
        string original, Func<bool> admitted, Action<string?> closed)
    {
        this.admitted = admitted; this.closed = closed;
        Name = isDescription ? "NewItemDescriptionEditor" : "NewItemNameEditor";
        ZIndex = 8; MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x7CD, RequestBack, Accept);
        string layout = isDescription ? "02_11_05_edit_explanation" : "02_11_05_edit_name_ot";
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, layout));
        var panel = new Control { Position = isDescription ? new(625,400) : new(598,475), Size = source.Rectangle(0).Size,
            MouseFilter = MouseFilterEnum.Ignore }; chrome.Canvas.AddChild(panel);
        foreach (var node in source.Nodes.Where(node => node.Type is 1 or 3))
            source.Art(panel, node.Index, source.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        source.Text(panel, isDescription ? 22 : 7, default,
            messages.Records[isDescription ? 0x400 : 0x5A8].Text ?? throw new InvalidDataException("Missing item input caption."), chrome.Font);
        int inputNode = isDescription ? 6 : 8; var box = source.Rectangle(inputNode);
        if (isDescription)
        {
            description = new TextEdit { Name = "NewItemDescriptionInput", Position = box.Position, Size = box.Size,
                Text = original, WrapMode = TextEdit.LineWrappingMode.Boundary };
            panel.AddChild(description);
            description.TextChanged += Refresh;
        }
        else
        {
            name = new LineEdit { Name = "NewItemNameInput", Position = box.Position, Size = box.Size,
                Text = original, MaxLength = 6 };
            panel.AddChild(name); name.TextChanged += _ => Refresh();
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private string Value => description?.Text ?? name?.Text ?? "";
    private bool Valid => !Value.Contains('\0') && Value.Length <= (description is null ? 6 : 60);
    private void Refresh() => chrome.ApplyDecision(Valid, true);
    private void Accept() { if (CanEdit() && Valid) Finish(Value); }
    private void Finish(string? value) { var callback = closed; Abandon(); callback?.Invoke(value); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    internal void Abandon() { abandoned = true; admitted = null; closed = null; chrome?.Abandon(); Hide(); }
}
