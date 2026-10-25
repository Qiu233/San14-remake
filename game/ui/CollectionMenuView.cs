using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum CollectionMenuCommand { Music, Officers, Items, Movies, Events, Poems, Scenery }

/// <summary>The original Collection menu. Each available child has a real
/// application consumer; returning from a child preserves this menu owner.</summary>
internal partial class CollectionMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceCollectionMenuButton> buttons = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Action<CollectionMenuCommand>? selected;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<bool> available, Func<bool> admitted, Action returned,
        Action<CollectionMenuCommand> selected)
    {
        if (available.Count != 7) throw new ArgumentException("Collection menu requires seven consumers.", nameof(available));
        this.admitted = admitted; this.returned = returned; this.selected = selected;
        Name = "CollectionMenu"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x32, RequestBack, () => { }, returnOnly: true);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_00_edit_top_new"));
        foreach (var node in source.Nodes.Where(node => node.Index != 4 && node.ParentIndex != 4))
        {
            if (node.Type is 1 or 3) source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
            else if (node.Type == 6) source.Blur(chrome.Canvas, node.Index, source.Rectangle(node.Index));
        }
        var buttonSource = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_00_btn_set_offset_00_new"));
        Vector2 mount = source.Rectangle(2).Position;
        foreach (int index in new[] { 1, 2 })
            buttonSource.Art(chrome.Canvas, index, buttonSource.Rectangle(index, mount), chrome.Assets, chrome.SpriteShader);
        int[] captions = [0x6E, 0x6C, 0x6D, 0x7DA, 0x7DD, 0xB8, 0xB7];
        int[] hints = [0x271E, 0x271F, 0x2720, 0x2721, 0x2723, 0x5CFA, 0x2983];
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var narration = new AdministrationMessages(repository, text.SourceLocale);
        for (int index = 0; index < captions.Length; index++)
        {
            var command = (CollectionMenuCommand)index;
            var rectangle = buttonSource.Rectangle(3 + index * 3, mount);
            var button = new UiSourceCollectionMenuButton { Name = "Collection" + command,
                Position = rectangle.Position, Size = rectangle.Size, Disabled = !available[index] };
            chrome.Canvas.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                messages.Records[captions[index]].Text ?? throw new InvalidDataException("Missing collection caption."),
                text.SourceLocale, command,
                () => { if (CanUse()) this.selected?.Invoke(command); });
            button.InputAdmission = CanUse;
            string hint = narration.Format(hints[index]);
            button.MouseEntered += () => { if (CanUse()) chrome.ApplyHoverHint(hint); };
            button.MouseExited += () => chrome.ApplyHoverHint(null);
        }
        chrome.ApplyDefaultHint(narration.Format(0x2847));
        chrome.ApplyReturnHint(narration.Format(0x2733));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    private bool CanUse() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void RequestBack() { if (CanUse()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; selected = null;
        foreach (var button in buttons) button.Abandon();
        chrome?.Abandon(); Hide();
    }
}
