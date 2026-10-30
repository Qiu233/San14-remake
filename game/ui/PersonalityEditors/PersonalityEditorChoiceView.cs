using Godot;
using San14.Data;

internal sealed record PersonalityEditorOptionRow(int Code, string Caption, int Minimum, int Maximum, int Step);
internal sealed record PersonalityEditorOptionSelection(int Code, int Value);

/// <summary>The original condition/effect list over an actual retained option
/// publication. A small reusable row pool follows the authored source mounts.</summary>
internal partial class PersonalityEditorChoiceView : Control
{
    private sealed record Row(UiSourceListRow Button, UiSourceText Caption)
    { internal int Index { get; set; } = -1; }
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scrollbar = null!;
    private readonly List<Row> rows = [];
    private IReadOnlyList<PersonalityEditorOptionRow> choices = [];
    private Action<PersonalityEditorOptionRow?>? closed;
    private Func<bool>? admitted;
    private int selected = -1, rowHeight, viewportHeight;
    private bool abandoned;
    internal void Configure(GameResourceRepository repository, PreviewText text, int title,
        IReadOnlyList<PersonalityEditorOptionRow> choices, int selectedCode, Func<bool> admitted,
        Action<PersonalityEditorOptionRow?> closed, Func<int> scrollSpeed)
    {
        this.choices = choices; this.admitted = admitted; this.closed = closed;
        selected = choices.ToList().FindIndex(choice => choice.Code == selectedCode);
        Name = "PersonalityOptionChoices"; ZIndex = 9; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, title, RequestBack, Accept);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_31_81_event_edit_jouken"));
        foreach (int node in new[] { 3, 6, 8 })
            source.Art(chrome.Canvas, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        var body = source.Rectangle(16);
        var clip = new Control { Name = "PersonalityChoiceViewport", Position = body.Position, Size = body.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        rowHeight = (int)(source.Rectangle(18).Position.Y - source.Rectangle(17).Position.Y);
        viewportHeight = (int)body.Size.Y;
        var scroll = source.Rectangle(9);
        scrollbar = new UiSourceScrollbar { Name = "PersonalityChoiceScrollbar", Position = scroll.Position, Size = scroll.Size };
        chrome.Canvas.AddChild(scrollbar);
        scrollbar.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, choices.Count * rowHeight - viewportHeight),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        clip.GuiInput += input => { if (CanEdit()) scrollbar.HandleWheel(input); };
        int poolSize = (viewportHeight + rowHeight - 1) / rowHeight + 1;
        float inset = new UiSourceLayout(repository, 0xF2C06161).Nodes[1].Value44;
        for (int index = 0; index < poolSize; index++)
        {
            var button = new UiSourceListRow { Name = "PersonalityChoiceRow" + index, InputAdmission = CanEdit };
            clip.AddChild(button); button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", body.Size.X - inset, true);
            var row = new Row(button, button.GetNode<UiSourceText>("CurrentSelectionCaption")); rows.Add(row);
            button.GuiInput += input => { if (CanEdit()) scrollbar.HandleWheel(input); };
            button.Pressed += () =>
            {
                if (!CanEdit() || row.Index < 0) return;
                selected = row.Index; RefreshSelection();
            };
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri); PositionRows();
    }
    private bool CanEdit() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private void PositionRows()
    {
        if (abandoned) return;
        int first = scrollbar.Value / rowHeight, offset = scrollbar.Value % rowHeight;
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index]; int at = first + index;
            row.Index = at < choices.Count ? at : -1; row.Button.Visible = row.Index >= 0;
            row.Button.Position = new(0, index * rowHeight - offset); row.Button.Disabled = row.Index < 0;
            if (row.Index >= 0) row.Caption.Text = choices[at].Caption;
        }
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        foreach (var row in rows) row.Button.Selected = row.Index == selected && row.Index >= 0;
        chrome.ApplyDecision(selected >= 0 && selected < choices.Count, true);
    }
    private void Accept() { if (CanEdit() && selected >= 0 && selected < choices.Count) Finish(choices[selected]); }
    private void Finish(PersonalityEditorOptionRow? result) { var callback = closed; Abandon(); callback?.Invoke(result); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; closed = null;
        foreach (var row in rows) row.Button.Disabled = true;
        scrollbar?.SetProcessInput(false); chrome?.Abandon(); Hide();
    }
}
