using Godot;
using San14.Data;

/// <summary>A full-page host for the live command controls. It borrows their
/// existing command bindings and returns every control before retiring.</summary>
internal partial class CommandWorkspaceView : Control
{
    private readonly List<(Control Control, Node Parent, int Index)> borrowed = [];
    private AdministrationPageChrome? chrome;
    private PlayableCommandDeck? deck;
    private ScrollContainer? scroll;
    private Action? returned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        PlayableCommandDeck commands, IReadOnlyList<Control> context, Action returned)
    {
        Name = "CommandWorkspace";
        ZIndex = 2;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        this.returned = returned;
        AddChild(new ColorRect { Color = new("#292d2c"), AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore });
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, 0x578, RequestBack, () => { }, returnOnly: true);
        chrome.ApplyDefaultHint(text.Get("play_command_workspace_hint"));
        scroll = new ScrollContainer { Name = "CommandScroll", Position = new(100, 140), Size = new(1720, 710),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Theme = commands.Theme };
        chrome.Canvas.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 16); scroll.AddChild(body);
        // Capture indices before moving anything from a shared parent.
        foreach (var control in context.Prepend(commands))
            borrowed.Add((control, control.GetParent(), control.GetIndex()));
        foreach (var control in context.Take(1)) control.Reparent(body);
        commands.Reparent(body);
        foreach (var control in context.Skip(1)) control.Reparent(body);
        deck = commands;
        deck.TabChanged += OnTabChanged;
        OnTabChanged(deck.CurrentTab);
    }

    private void OnTabChanged(long index) => chrome?.ApplyTitle(deck!.GetTabTitle((int)index));
    internal void FocusCommand(Control control)
    {
        var owner = scroll;
        Callable.From(() =>
        {
            if (returned is null || !IsInsideTree() || owner is null || scroll != owner || !IsAncestorOf(control)) return;
            owner.EnsureControlVisible(control);
            control.GrabFocus();
        }).CallDeferred();
    }
    internal void RequestBack() => returned?.Invoke();

    internal void Abandon()
    {
        returned = null;
        scroll = null;
        if (deck is not null) deck.TabChanged -= OnTabChanged;
        deck = null;
        foreach (var row in borrowed.OrderBy(row => row.Index))
        {
            if (row.Control.GetParent() != row.Parent) row.Control.Reparent(row.Parent);
            row.Parent.MoveChild(row.Control, row.Index);
        }
        borrowed.Clear();
        chrome?.Abandon(); Hide();
    }
}
