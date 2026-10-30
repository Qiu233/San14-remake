using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum CurrentWorldEditEntry { Force, City, Area, Item, Items }

/// <summary>The five original in-campaign editors. Its owner retains the
/// campaign and owns each detached editing draft.</summary>
internal partial class CurrentWorldEditMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceCommandButton> buttons = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Action<CurrentWorldEditEntry>? selected;
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        Func<bool> admitted, Action returned, Action<CurrentWorldEditEntry> selected)
    {
        this.admitted = admitted; this.returned = returned; this.selected = selected;
        Name = "CurrentWorldEditMenu"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x6AC, RequestBack, () => { }, returnOnly: true);
        chrome.ApplyDecision(false, false);
        var authored = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_01_11_info_menu_option_new"));
        int removedHeight = (int)(authored.Rectangle(10).Position.Y - authored.Rectangle(6).Position.Y) * 5;
        var geometry = new Dictionary<int, UiSourceNodeGeometry>();
        foreach (int node in new[] { 0, 3, 4, 5 })
        {
            var size = authored.Rectangle(node).Size;
            geometry[node] = new(Size: new(size.X, size.Y - removedHeight));
        }
        var source = new UiSourceLayout(authored.Source, geometry);
        // The native menu keeps its outer control's original478x664 size
        // while shortening its resource root. Its centered control then
        // doubles Y, giving this same mount for the menu and mark layer.
        Vector2 mount = new(721, 364);
        source.Art(chrome.Canvas, 2, source.Rectangle(2, mount), chrome.Assets, chrome.SpriteShader,
            blend: UiSourceSprite.Blend.AlphaMask);
        var blur = source.Blur(chrome.Canvas, 3, source.Rectangle(3, mount));
        var inner = source.Rectangle(4, mount);
        source.Blur(blur, 4, new(inner.Position - blur.Position, inner.Size));
        source.Art(chrome.Canvas, 5, source.Rectangle(5, mount), chrome.Assets, chrome.SpriteShader);
        var captions = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records;
        var hints = new AdministrationMessages(repository, text.SourceLocale);
        int[] nodes = [6, 10, 14, 33, 34], messages = [0x76, 0x77, 0x78, 0x736, 0x737];
        for (int index = 0; index < nodes.Length; index++)
        {
            var entry = (CurrentWorldEditEntry)index;
            string hint = hints.Format(0x5F19 + index);
            var box = source.Rectangle(nodes[index], mount);
            var button = new UiSourceCommandButton { Name = "CurrentWorldEdit" + entry,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                captions[messages[index]].Text ?? throw new InvalidDataException("Missing current-world edit caption."),
                () => { if (CanEdit()) this.selected?.Invoke(entry); });
            button.MouseEntered += () => { if (CanEdit()) chrome.ApplyHoverHint(hint); };
            button.MouseExited += () => { if (!abandoned) chrome.ApplyHoverHint(null); };
            button.SetMeta("source_node_index", nodes[index]);
        }
        var marks = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_01_11_info_menu_option_pk"));
        string language = text.SourceLocale switch { "TC" => "tw", "SC" => "ch",
            _ => throw new NotSupportedException("Unsupported current-world editor locale.") };
        foreach (int node in new[] { 32, 35, 36, 37, 38 })
            marks.Art(chrome.Canvas, node, marks.Rectangle(node, mount), chrome.Assets, chrome.SpriteShader,
                commonSpriteOverride: language + (node is 37 or 38 ? "_020_sho_mrk_pk" : "_020_sho_mrk_dlc"),
                commonTexturePack: "texture_lang_" + language);
        chrome.ApplyDefaultHint(hints.Format(0x5F18));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_mark_layout", marks.Source.Source.ResourceUri);
    }

    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; chrome.ApplyReturn(!value);
        foreach (var button in buttons) button.Disabled = value;
    }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; selected = null;
        foreach (var button in buttons) button.Abandon();
        chrome?.Abandon(); Hide();
    }
}
